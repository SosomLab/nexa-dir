using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Nexa.ViewModels;

namespace Nexa.App;

/// <summary>
/// 셸 아이콘(16px 목록 썸네일) 로더 — <b>종류/확장자 LRU 캐시 + 속도 제한(throttle) 로딩 큐</b>(감사 P6).
///
/// <para>요청은 즉시 셸 호출하지 않고 <b>큐</b>에 넣는다. 타이머(<see cref="TickMs"/>ms)가 틱마다 <b>동시 상한
/// (<see cref="MaxConcurrent"/>)</b> 내에서만 큐를 꺼내 로드하므로, 스크롤이 아무리 빨라도 동시·신규 셸 호출이
/// 상한을 넘지 않는다. 화면 밖으로 나간 행은 <see cref="Cancel"/>이 큐에서 제거 → 지나간 행은 로드하지 않는다
/// (Explorer/Finder식 "스크롤 정착 후 로딩"과 동일 원리). 이로써 빠른 스크롤 시 셸 썸네일 파이프라인 과부하
/// (네이티브 크래시)를 방지한다.</para>
///
/// <para>같은 확장자는 한 번만 셸 호출해 <see cref="ImageSource"/>를 공유하고(중복 제거), 캐시는 LRU로 상한
/// (<see cref="Capacity"/>, NFR-M2). 모든 접근은 UI 스레드(요청·타이머·await 연속 모두)라 락이 필요 없다.</para>
/// </summary>
internal sealed class ShellIconCache
{
    private const int Capacity = 256;       // LRU 상한(엔트리 ≈ 화면에 보이는 확장자 종류 수)
    private const int MaxConcurrent = 4;    // 동시 셸 호출 하드 상한
    private const int TickMs = 80;          // 큐 처리 주기(스크롤 중에도 이 속도로만 로드)

    private readonly Dictionary<string, ImageSource> _cache = new();
    private readonly LinkedList<string> _lru = new();                        // 앞=최근 사용
    private readonly Dictionary<string, LinkedListNode<string>> _nodes = new();

    // 로딩 큐: 화면에 보이는 미로드 행(요청 순서). 스크롤로 벗어난 행은 Cancel로 제거되어 로드되지 않음.
    private readonly LinkedList<DirItem> _queue = new();
    private readonly Dictionary<DirItem, LinkedListNode<DirItem>> _queued = new();
    private int _active;
    private readonly DispatcherQueueTimer _pump;

    public ShellIconCache(DispatcherQueue ui)
    {
        _pump = ui.CreateTimer();
        _pump.Interval = TimeSpan.FromMilliseconds(TickMs);
        _pump.IsRepeating = true;
        _pump.Tick += (_, _) => Pump();
    }

    /// <summary>행이 화면에 실체화되면 아이콘 요청(UI 스레드). 캐시 히트면 즉시, 미스면 큐에 넣는다(타이머가 처리).</summary>
    public void Request(DirItem item)
    {
        string key = IconKey.For(item.IsDir, item.FullPath);
        if (_cache.TryGetValue(key, out var cached))
        {
            Touch(key);
            item.IconImage = cached;   // 즉시 글리프→실제 아이콘(추가 셸 호출 없음)
            return;
        }
        if (item.IconImage is not null || _queued.ContainsKey(item))
        {
            return;   // 이미 로드됨 / 이미 큐에 있음
        }
        _queued[item] = _queue.AddLast(item);
        if (!_pump.IsRunning)
        {
            _pump.Start();
        }
    }

    /// <summary>행이 화면 밖으로 나가면(요소 재활용) 큐에서 제거 — 지나간 행은 로드하지 않는다(빠른 스크롤 부하 제한, P6).</summary>
    public void Cancel(DirItem item)
    {
        if (_queued.Remove(item, out var node))
        {
            _queue.Remove(node);
        }
    }

    private void Pump()
    {
        while (_active < MaxConcurrent && _queue.First is { } first)
        {
            var item = first.Value;
            _queue.RemoveFirst();
            _queued.Remove(item);
            _ = LoadAsync(item);   // 동기 구간에서 _active 증가 후 첫 await까지 진행
        }
        if (_queue.Count == 0 && _active == 0)
        {
            _pump.Stop();   // 할 일 없으면 타이머 정지(유휴 비용 0)
        }
    }

    private async Task LoadAsync(DirItem item)
    {
        _active++;
        try
        {
            string key = IconKey.For(item.IsDir, item.FullPath);
            if (_cache.TryGetValue(key, out var now))
            {
                Touch(key);
                item.IconImage = now;   // 대기 중 다른 행이 채웠으면 히트
                return;
            }
            StorageItemThumbnail thumb = item.IsDir
                ? await (await StorageFolder.GetFolderFromPathAsync(item.FullPath)).GetThumbnailAsync(ThumbnailMode.ListView, 16)
                : await (await StorageFile.GetFileFromPathAsync(item.FullPath)).GetThumbnailAsync(ThumbnailMode.ListView, 16);
            // 이미지 썸네일(사진)과 셸 아이콘(exe·파일형식·폴더) 모두 렌더. 빈 썸네일만 스킵.
            if (thumb is null || thumb.Size == 0)
            {
                return;
            }
            var bmp = new BitmapImage();
            await bmp.SetSourceAsync(thumb);
            Put(key, bmp);
            item.IconImage = bmp;
        }
        catch
        {
            // 접근 불가·미지원 → 글리프 폴백 유지(오류 격리).
        }
        finally
        {
            _active--;
            if (!_pump.IsRunning && _queue.Count > 0)
            {
                _pump.Start();   // 남은 큐 이어서 처리
            }
        }
    }

    private void Put(string key, ImageSource img)
    {
        if (_cache.ContainsKey(key))
        {
            Touch(key);
            return;
        }
        _cache[key] = img;
        _nodes[key] = _lru.AddFirst(key);
        if (_cache.Count > Capacity)
        {
            var last = _lru.Last!;                 // 최소 사용 축출
            _lru.RemoveLast();
            _cache.Remove(last.Value);
            _nodes.Remove(last.Value);
        }
    }

    private void Touch(string key)
    {
        if (_nodes.TryGetValue(key, out var node))
        {
            _lru.Remove(node);
            _lru.AddFirst(node);
        }
    }
}
