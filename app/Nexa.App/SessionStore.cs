using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.UI.Dispatching;

namespace Nexa.App;

// ─────────────────────────────────────────────────────────────────────────────
// 세션(탭) 상태 영속화 — 일반 설정과 **별도 파일**(session.json)에 저장.
//
// ● 저장 대상(스키마 = 저장이 필요한 대상의 정리):
//   - 활성 패널(좌/우)               … SessionState.ActiveLeft
//   - 패널별:
//       · 활성 탭 인덱스             … PanelSession.ActiveTab
//       · 열린 탭 목록              … PanelSession.Tabs
//           - 탭의 현재 폴더 경로     … TabSession.Path
//           - 탭 내 폴더 펼침(열림) 집합 … TabSession.Expanded  (경로 목록)
//           - 탭 내 정렬 상태         … TabSession.Sort         (키+방향)
//   ※ 정렬은 현재 아키텍처상 **패널 단위**(PanelView.SortKeys)라 캡처 시 그 패널의 정렬을
//     각 탭에 동일 기록한다. 스키마는 **탭 단위**로 두어 per-tab 정렬(COL-2d 후속) 시 그대로 확장.
//
// ● I/O 최소화 + 유휴시간 활용 + 급종료 대비(요구):
//   1) 요청/수행 분리 + Tick 코얼레싱: MarkDirty()는 "요청"으로 dirty 플래그만 set(초저비용·멱등).
//      단일 반복 Tick 타이머가 "수행"으로 Tick마다 dirty를 1회 소비 → 짧은 시간에 요청이 아무리 몰려도
//      Tick당 최대 1회만 저장한다(N개의 요청 → 1회 쓰기).
//   2) 유휴 실행: Tick의 실제 저장은 DispatcherQueuePriority.Low로 큐잉 → UI 유휴에 수행.
//   3) 무변경 스킵: 직렬화 해시가 직전 저장과 같으면 디스크 쓰기 생략(불필요 I/O 0).
//   4) 원자적 쓰기: temp에 쓰고 교체(File.Replace/Move) → 쓰기 중 크래시에도 파일 무손상.
//   5) 안전 주기(자가치유): 요청이 없어도 일정 Tick마다 1회 강제 캡처 → 훅 누락/장시간 세션 대비(무변경이면 쓰기 생략).
//   6) 종료 flush: 창 Closed·ProcessExit에서 즉시(동기) flush → 정상 종료 시 최종 상태 확정.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>세션 상태 루트(session.json). 버전으로 후속 마이그레이션 대비.</summary>
internal sealed class SessionState
{
    public int Version { get; set; } = 1;
    public bool ActiveLeft { get; set; } = true;
    public PanelSession Left { get; set; } = new();
    public PanelSession Right { get; set; } = new();
    public BottomPanelState Bottom { get; set; } = new();
    public LayoutState Layout { get; set; } = new();
}

/// <summary>레이아웃 표시 상태(머신 로컬) — 퀵 런처 바·우 패널(듀얼) 표시. 하단 패널은 <see cref="BottomPanelState"/>.</summary>
internal sealed class LayoutState
{
    public bool ShowLauncher { get; set; } = true;
    public bool ShowRightPanel { get; set; } = true;
}

/// <summary>하단 도킹 패널 상태(표시/높이/좌우 분리/콘텐츠 종류) — BP-1.</summary>
internal sealed class BottomPanelState
{
    public bool Visible { get; set; } = true;
    public double Height { get; set; } = 180;
    public bool Split { get; set; } = true;
    public int LeftKind { get; set; }    // BottomPanelKind
    public int RightKind { get; set; }
}

/// <summary>패널(좌 또는 우) 세션 — 활성 탭 + 열린 탭 목록.</summary>
internal sealed class PanelSession
{
    public int ActiveTab { get; set; }
    public List<TabSession> Tabs { get; set; } = new();
}

/// <summary>탭 하나의 세션 — 경로 + 펼침(열림) 집합 + 정렬 + 잠금/고정(TAB-MENU).</summary>
internal sealed class TabSession
{
    public string Path { get; set; } = string.Empty;
    public List<string> Expanded { get; set; } = new();
    public List<SortKeyState> Sort { get; set; } = new();

    /// <summary>탭 잠금(닫기 동작 제외). 기본 false — 과거 세션 파일과 호환.</summary>
    public bool Locked { get; set; }

    /// <summary>탭 고정(핀 아이콘·핀 그룹 맨 앞). 기본 false.</summary>
    public bool Pinned { get; set; }
}

/// <summary>정렬 키 1개(코어 <c>NexaSortKey</c>의 직렬화 형태). Key: 0=이름 1=확장자 2=크기 3=수정날짜 4=종류.</summary>
internal sealed class SortKeyState
{
    public uint Key { get; set; }
    public bool Descending { get; set; }
}

/// <summary>
/// <see cref="SessionState"/>를 파일에 저장/로드하는 엔진. 디바운스+유휴 실행+주기 자동저장+종료 flush로
/// I/O를 최소화하면서 급종료에도 최종 상태가 보존되도록 한다. UI 스레드(DispatcherQueue)에서 생성·구동.
/// </summary>
internal sealed class SessionStore
{
    private readonly string _path;
    private readonly DispatcherQueue _dispatcher;
    private readonly Func<SessionState> _capture;
    private readonly DispatcherQueueTimer _tick;   // 단일 소비 타이머(Tick마다 최대 1회 저장)

    private volatile bool _dirty;              // 저장 "요청" 플래그 — MarkDirty가 set, Tick이 소비
    private int _ticks;                        // Tick 카운터(안전 주기 강제 캡처용)
    private string _lastHash = string.Empty;   // 직전에 디스크에 쓴 내용 해시(무변경 스킵용)
    private bool _flushed;                      // 종료 flush 재진입 방지

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    /// <summary>Tick(소비) 주기 — 요청이 몰려도 이 주기마다 최대 1회만 저장(코얼레싱 단위).</summary>
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    /// <summary>안전 주기(Tick 수) — 요청 플래그가 없어도 이 주기마다 1회 강제 캡처(훅 누락 자가치유·무변경 시 쓰기 생략).</summary>
    private const int SafetyTicks = 60;

    public SessionStore(string path, DispatcherQueue dispatcher, Func<SessionState> capture)
    {
        _path = path;
        _dispatcher = dispatcher;
        _capture = capture;

        // 수행(consumer): 단일 반복 타이머. Tick마다 요청 플래그를 1회 소비 → Tick당 최대 1회 저장.
        _tick = dispatcher.CreateTimer();
        _tick.Interval = TickInterval;
        _tick.IsRepeating = true;
        _tick.Tick += (_, _) => OnTick();
        _tick.Start();
    }

    /// <summary>
    /// 저장 "요청"(producer) — dirty 플래그만 set(초저비용·멱등). 단시간에 여러 번 호출돼도
    /// 다음 Tick에서 <b>1회만</b> 저장된다(수행은 <see cref="OnTick"/>가 담당, 요청/수행 분리).
    /// </summary>
    public void MarkDirty() => _dirty = true;

    /// <summary>수행(consumer) — Tick마다 최대 1회. 요청이 있었거나(또는 안전 주기) 저장을 1번 수행하고 플래그를 소비.</summary>
    private void OnTick()
    {
        bool safety = (++_ticks % SafetyTicks) == 0;   // 훅 누락 자가치유(강제 캡처, 무변경이면 쓰기 생략)
        if (!_dirty && !safety)
        {
            return;
        }
        _dirty = false;   // 소비 시점에 해제 → 저장 중 새로 들어온 요청은 다음 Tick에 반영(유실 없음)
        // 유휴 실행: UI가 한가할 때(Low) 캡처+저장 → 활성 상호작용과 경쟁하지 않음.
        _dispatcher.TryEnqueue(DispatcherQueuePriority.Low, () => FlushIfChanged());
    }

    /// <summary>현재 상태를 캡처·직렬화해 <b>내용이 바뀐 경우에만</b> 원자적으로 저장한다.</summary>
    private void FlushIfChanged()
    {
        string json;
        try
        {
            json = JsonSerializer.Serialize(_capture(), JsonOpts);
        }
        catch
        {
            return;   // 캡처/직렬화 실패는 무시(다음 기회에 저장) — 앱 동작 방해 금지
        }
        string hash = Hash(json);
        if (hash == _lastHash)
        {
            return;   // 무변경 → 디스크 쓰기 생략
        }
        if (WriteAtomic(json))
        {
            _lastHash = hash;
        }
    }

    /// <summary>종료 시 즉시(동기) 최종 상태를 저장한다(창 Closed·ProcessExit에서 1회).</summary>
    public void Flush()
    {
        if (_flushed)
        {
            return;
        }
        _flushed = true;
        _dirty = false;
        _tick.Stop();
        FlushIfChanged();
    }

    /// <summary>temp에 쓰고 교체 → 쓰기 도중 크래시에도 기존 파일이 깨지지 않는다.</summary>
    private bool WriteAtomic(string json)
    {
        try
        {
            string dir = Path.GetDirectoryName(_path)!;
            Directory.CreateDirectory(dir);
            string tmp = _path + ".tmp";
            File.WriteAllText(tmp, json, new UTF8Encoding(false));
            if (File.Exists(_path))
            {
                File.Replace(tmp, _path, null);   // 원자적 교체(같은 볼륨)
            }
            else
            {
                File.Move(tmp, _path);
            }
            return true;
        }
        catch
        {
            return false;   // 저장 실패는 격리(앱 계속) — 다음 주기/종료에 재시도
        }
    }

    private static string Hash(string s)
    {
        byte[] h = SHA256.HashData(Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(h);
    }

    /// <summary>세션 파일 로드(없거나 손상 시 null → 기본 시작). 예외는 격리.</summary>
    public static SessionState? Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }
            return JsonSerializer.Deserialize<SessionState>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>세션 파일 표준 경로: <c>%LOCALAPPDATA%\NexaDir\session.json</c>(일반 설정과 별도 파일).
    /// 포터블 모드(docs/12 §3)=<c>exe\data\session.json</c> — <see cref="AppPaths"/>가 분기.</summary>
    public static string DefaultPath() => Path.Combine(AppPaths.LocalRoot, "session.json");
}
