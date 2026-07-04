using System;
using System.IO;
using Microsoft.UI.Dispatching;

namespace Nexa.App;

/// <summary>
/// 한 패널이 보고 있는 <b>현재 폴더</b>의 변경을 감시해 자동 갱신을 유발한다(B-12w 1차, 감사 C-3).
/// <para>성능 규약(docs/33): <b>비재귀</b>(현재 폴더만) · 변경 이벤트를 <b>디바운스</b>(300ms 코얼레싱)해
/// 대량 작업 시 1회만 갱신 · 콜백은 스레드풀 → <see cref="DispatcherQueue"/>로 마샬 후 UI에서 갱신.
/// 감시 실패(권한 등)는 무시하고 수동 F5로 폴백. 정석(코어 notify/VFS Provider)은 C-2/C-3 후속.</para>
/// </summary>
internal sealed class FolderWatcher : IDisposable
{
    private readonly DispatcherQueue _dq;
    private readonly Action _onChanged;
    private readonly DispatcherQueueTimer _debounce;
    private FileSystemWatcher? _fsw;
    private string _path = string.Empty;

    public FolderWatcher(DispatcherQueue dq, Action onChanged)
    {
        _dq = dq;
        _onChanged = onChanged;
        _debounce = dq.CreateTimer();
        _debounce.Interval = TimeSpan.FromMilliseconds(300);
        _debounce.IsRepeating = false;
        _debounce.Tick += (_, _) => _onChanged();
    }

    /// <summary><paramref name="path"/> 폴더를 감시한다(같은 경로면 유지). 빈/없는 경로면 감시 중지.</summary>
    public void Watch(string path)
    {
        if (string.Equals(path, _path, StringComparison.OrdinalIgnoreCase) && _fsw is not null)
        {
            return;   // 이미 같은 폴더 감시 중
        }
        Stop();
        _path = path;
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            return;
        }
        try
        {
            _fsw = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = false,   // 현재 폴더만(성능)
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                    | NotifyFilters.Size | NotifyFilters.LastWrite,
            };
            _fsw.Created += OnFsEvent;
            _fsw.Deleted += OnFsEvent;
            _fsw.Renamed += OnFsRenamed;
            _fsw.Changed += OnFsEvent;
            _fsw.Error += (_, _) => _dq.TryEnqueue(Restart);   // 버퍼 오버플로 등 → 재시작 + 갱신
            _fsw.EnableRaisingEvents = true;
        }
        catch
        {
            _fsw = null;   // 권한/네트워크 등 실패 — 수동 F5 폴백
        }
    }

    private void OnFsEvent(object sender, FileSystemEventArgs e) => Coalesce();

    private void OnFsRenamed(object sender, RenamedEventArgs e) => Coalesce();

    // 스레드풀 콜백 → UI 스레드에서 디바운스 타이머 재시작(300ms 내 다발 이벤트를 1회로).
    private void Coalesce() => _dq.TryEnqueue(() =>
    {
        _debounce.Stop();
        _debounce.Start();
    });

    private void Restart()
    {
        string p = _path;
        Stop();
        Watch(p);
        _onChanged();
    }

    /// <summary>감시 중지(핸들 해제). 경로 상태는 유지하지 않음.</summary>
    public void Stop()
    {
        if (_fsw is not null)
        {
            _fsw.EnableRaisingEvents = false;
            _fsw.Dispose();
            _fsw = null;
        }
    }

    public void Dispose()
    {
        Stop();
        _debounce.Stop();
    }
}
