using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace Nexa.App;

/// <summary>
/// 코어 트리(nexa-tree, ABI v3)의 <b>가시 노드 평면 스트림</b>을 가상화로 소비하는 컬렉션.
/// 보이는 인덱스의 <see cref="DirItem"/>만 지연 생성·캐시하고(전량 구체화 안 함), 펼침/접힘/선택은
/// 코어에 위임한다(단일 진실원천 = 코어). <c>NexaFileGrid</c>(ItemsRepeater) ItemsSource로 사용.
///
/// 슬라이스 3b-2 컴포넌트. C1 설계: docs/29·07.
/// 펼침/접힘 구조 변경은 코어 diff(<see cref="TreeRange"/>)를 살려 <b>범위 Add/Remove</b>로 세밀 통지하고
/// <c>_cache</c> 인덱스를 시프트한다(전체 Reset·재실체화 회피 — 감사 P2). 폴더 (재)열기만 <see cref="NotifyCollectionChangedAction.Reset"/>.
/// </summary>
internal sealed class VirtualTreeCollection : IList, IReadOnlyList<DirItem>, INotifyCollectionChanged, IDisposable
{
    private IntPtr _handle = IntPtr.Zero;
    private readonly Dictionary<int, DirItem> _cache = new();
    private int _caretIndex = -1;   // 키보드 캐럿(현재 위치) 가시 인덱스, 없으면 -1
    private bool _panelFocused = true; // 이 패널이 활성인가(선택색 파랑/회색)

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <summary>행이 새로 실체화될 때 1회 호출(아이콘 지연 로드 등). 호스트가 설정.</summary>
    public Action<DirItem>? RowBuilt { get; set; }

    /// <summary>현재 코어 트리 핸들(없으면 <see cref="IntPtr.Zero"/>).</summary>
    public IntPtr Handle => _handle;

    /// <summary>현재 열린 루트 경로.</summary>
    public string RootPath { get; private set; } = string.Empty;

    /// <summary>
    /// 트리를 <b>백그라운드 스레드에서</b> 열고(가시성 필터) 저장된 펼침 경로까지 재적용한다.
    /// UI 스레드를 블록하지 않도록 <c>LoadDirectory</c>가 <c>Task.Run</c>으로 호출하고, 완성된
    /// 핸들을 <see cref="AdoptHandle"/>로 채택한다(감사 P1, NFR-P1/R5). 새 핸들은 아직 아무도
    /// 공유하지 않으므로(별칭 없음) 스레드 안전. 반환: (핸들, 펼침 재적용 전 직접 자식 수).
    /// 실패 시 (<see cref="IntPtr.Zero"/>, 0).
    /// </summary>
    public static (IntPtr Handle, int DirectCount) OpenAndExpand(
        string path, bool showHidden, bool showDotFiles, IReadOnlyList<string> expandedPaths,
        NativeInterop.NexaSortKey[]? sortKeys)
    {
        IntPtr handle = NativeInterop.TreeOpen(path, showHidden, showDotFiles);
        if (handle == IntPtr.Zero)
        {
            return (IntPtr.Zero, 0);
        }
        if (sortKeys is not null)
        {
            // null=미설정(코어 기본 이름오름 유지). 배열(빈 포함)=지정 정렬/명시적 없음(열거)을 새 핸들에 적용(COL-2c 지속).
            NativeInterop.TreeSetSort(handle, sortKeys, foldersFirst: true);
        }
        int direct = NativeInterop.TreeVisibleLen(handle);   // 펼침 재적용 전 직접 자식 수
        // 얕은→깊은 순(부모 먼저 펼쳐야 자식이 가시화되어 다음 경로가 매칭됨). 경로당 단일 호출(P3).
        var ordered = new List<string>(expandedPaths);
        ordered.Sort((a, b) => Depth(a).CompareTo(Depth(b)));
        foreach (var target in ordered)
        {
            NativeInterop.TreeExpandPath(handle, target);
        }
        return (handle, direct);
    }

    /// <summary>
    /// 백그라운드에서 완성된 핸들을 채택한다(UI 스레드). 이전 핸들을 해제·캐시를 비운 뒤 새 핸들로
    /// 교체하고 <see cref="NotifyCollectionChangedAction.Reset"/>를 통지한다. 로드 중에는 이전 핸들이
    /// 계속 표시되므로 빈 화면 깜빡임이 없다.
    /// </summary>
    public void AdoptHandle(IntPtr handle, string rootPath)
    {
        Close();                 // 이전 핸들 해제 + 캐시 비움
        _handle = handle;
        RootPath = rootPath;
        _caretIndex = -1;
        RaiseReset();
    }

    /// <summary>
    /// 정렬 사양을 코어에 적용한다(로드된 모든 폴더 재정렬 + 가시목록 재구성, 펼침 보존, COL-2c).
    /// 순서가 전면 바뀌므로 캐시·캐럿을 비우고 <see cref="NotifyCollectionChangedAction.Reset"/> 통지.
    /// <paramref name="keys"/>가 비면 정렬 없음(열거 순서).
    /// </summary>
    public void SetSort(NativeInterop.NexaSortKey[] keys, bool foldersFirst)
    {
        if (_handle == IntPtr.Zero)
        {
            return;
        }
        NativeInterop.TreeSetSort(_handle, keys, foldersFirst);
        _cache.Clear();
        _caretIndex = -1;
        RaiseReset();
    }

    // ── 캐럿 / 패널 포커스 (전이 행에 얹는 UI 상태) ───────────────────

    /// <summary>키보드 캐럿의 현재 가시 인덱스(없으면 -1).</summary>
    public int CaretIndex => _caretIndex;

    /// <summary>캐럿 위치의 행(범위 밖이면 null).</summary>
    public DirItem? CaretItem => _caretIndex >= 0 && _caretIndex < Count ? this[_caretIndex] : null;

    /// <summary>캐럿을 <paramref name="index"/>로 옮긴다(이전/새 캐럿 행의 표시 갱신).</summary>
    public void SetCaret(int index)
    {
        if (index == _caretIndex)
        {
            return;
        }
        if (_caretIndex >= 0 && _cache.TryGetValue(_caretIndex, out var old))
        {
            old.IsCaret = false;
        }
        _caretIndex = index;
        if (index >= 0 && _cache.TryGetValue(index, out var cur))
        {
            cur.IsCaret = true;
        }
    }

    /// <summary>이 패널의 활성(포커스) 여부 설정 → 실체화된 행의 선택색(파랑/회색) 갱신.</summary>
    public void SetPanelFocused(bool focused)
    {
        if (_panelFocused == focused)
        {
            return;
        }
        _panelFocused = focused;
        foreach (var item in _cache.Values)
        {
            item.PanelFocused = focused;
        }
    }

    /// <summary>
    /// 경로가 일치하는 가시 행 인덱스(없으면 -1). 끝 구분자·대소문자 무시.
    /// 코어가 직접 매칭(단일 호출) — 행별 <c>TreeRowPath</c> P/Invoke·문자열 복사를 제거(감사 P3).
    /// </summary>
    public int IndexOfPath(string fullPath) =>
        _handle == IntPtr.Zero ? -1 : NativeInterop.TreeIndexOfPath(_handle, fullPath);

    /// <summary>
    /// 타입어헤드 접두사 매칭(docs/32) — <paramref name="prefix"/>로 시작하는 가시 행 인덱스(없으면 -1).
    /// <paramref name="caret"/>=현재 캐럿(-1=없음), <paramref name="scope"/>=0 GlobalFirst/1 CurrentLevel/2 VisibleStream(기본).
    /// 코어가 마샬 없이 가시 스트림을 스캔(2단계 버퍼·5단계 앱 배선이 소비).
    /// </summary>
    public int FindPrefix(int caret, string prefix, uint scope) =>
        _handle == IntPtr.Zero ? -1 : NativeInterop.TreeFindPrefix(_handle, caret, prefix, scope);

    /// <summary>현재 가시 행 수(코어 질의).</summary>
    public int Count => _handle == IntPtr.Zero ? 0 : NativeInterop.TreeVisibleLen(_handle);

    /// <summary>가시 인덱스의 행. 캐시에 있으면 재사용(선택/아이콘 상태 유지), 없으면 코어에서 생성.</summary>
    public DirItem this[int index]
    {
        get
        {
            if (_cache.TryGetValue(index, out var cached))
            {
                return cached;
            }
            var item = Build(index);
            _cache[index] = item;
            RowBuilt?.Invoke(item);   // 새 실체화 → 아이콘 지연 로드 등
            return item;
        }
    }

    private DirItem Build(int index)
    {
        if (_handle != IntPtr.Zero && NativeInterop.TreeGetRow(_handle, index, out TreeRow r))
        {
            string path = NativeInterop.TreeRowPath(_handle, index) ?? string.Empty;
            return new DirItem(r.Name, r.Kind, r.Size, r.ModifiedUnixMs, path, (int)r.Depth, r.Attrs, r.Id)
            {
                IsExpanded = r.Expanded,
                IsSelected = NativeInterop.TreeIsSelected(_handle, r.Id),
                IsCaret = index == _caretIndex,
                PanelFocused = _panelFocused,
            };
        }
        // 방어: 범위 밖(레이아웃 지연 등) — 빈 행.
        return new DirItem(string.Empty, NexaFileKind.File, 0, -1, string.Empty, 0);
    }

    // ── 펼침 / 접힘 (코어 위임) ───────────────────────────────────────

    /// <summary>
    /// 폴더면 펼침/접힘을 토글한다. 코어가 돌려주는 변경 구간(<see cref="TreeRange"/>)을 그대로 살려
    /// <b>범위 Add/Remove로 정밀 통지</b>하고 <c>_cache</c>·캐럿 인덱스를 시프트한다(전체 Reset·무효화 아님).
    /// → 토글 행 위쪽은 재실체화·아이콘 재로드 없이 제자리, 스크롤도 안 튄다(감사 P2, 60fps).
    /// </summary>
    public void ToggleExpand(DirItem item)
    {
        if (_handle == IntPtr.Zero || !item.IsDir)
        {
            return;
        }
        bool wasExpanded = item.IsExpanded;
        TreeRange r = wasExpanded
            ? NativeInterop.TreeCollapse(_handle, item.Id)
            : NativeInterop.TreeExpand(_handle, item.Id);
        // 디스클로저 글리프는 항상 토글 — 자식이 0개인 빈 폴더도 펼침/접힘 표시가 되도록(코어는 빈 폴더도
        // expanded 상태를 유지). 가시 행 변경(자식 삽입/제거)이 있을 때만 캐시 시프트 + 범위 통지.
        item.IsExpanded = !wasExpanded;
        if (r.Inserted > 0 || r.Removed > 0)
        {
            ApplyDiff(r.Start, r.Removed, r.Inserted);   // 캐시/캐럿 인덱스 시프트(위쪽 행 DirItem·아이콘 보존)
            RaiseChange(
                r.Inserted > 0 ? NotifyCollectionChangedAction.Add : NotifyCollectionChangedAction.Remove,
                r.Start,
                r.Inserted > 0 ? r.Inserted : r.Removed);
        }
    }

    private static int Depth(string path) => path.TrimEnd('\\', '/').Count(c => c is '\\' or '/');

    // ── 선택 (코어 위임, OrderedSet) ─────────────────────────────────

    /// <summary>단일(mode 0)/토글(mode 1) 선택 후 캐시 항목의 선택 표시 갱신.</summary>
    public void Select(DirItem item, uint mode)
    {
        if (_handle == IntPtr.Zero)
        {
            return;
        }
        NativeInterop.TreeSelect(_handle, item.Id, mode);
        RefreshSelectionVisuals();
    }

    /// <summary>anchor~item 가시 범위 선택.</summary>
    public void SelectRange(DirItem item)
    {
        if (_handle == IntPtr.Zero)
        {
            return;
        }
        NativeInterop.TreeSelectRange(_handle, item.Id);
        RefreshSelectionVisuals();
    }

    /// <summary>가시 노드 전체 선택.</summary>
    public void SelectAll()
    {
        if (_handle == IntPtr.Zero)
        {
            return;
        }
        NativeInterop.TreeSelectAll(_handle);
        RefreshSelectionVisuals();
    }

    /// <summary>선택 해제.</summary>
    public void ClearSelection()
    {
        if (_handle == IntPtr.Zero)
        {
            return;
        }
        NativeInterop.TreeClearSelection(_handle);
        RefreshSelectionVisuals();
    }

    /// <summary>현재 선택 수.</summary>
    public int SelectionCount => _handle == IntPtr.Zero ? 0 : NativeInterop.TreeSelectedLen(_handle);

    /// <summary>선택 경로(삽입 순서) — 작업 엔진 입력(혼합 부모 허용).</summary>
    public IReadOnlyList<string> SelectedPaths()
    {
        var list = new List<string>();
        if (_handle == IntPtr.Zero)
        {
            return list;
        }
        int n = NativeInterop.TreeSelectedLen(_handle);
        for (int i = 0; i < n; i++)
        {
            string? p = NativeInterop.TreeSelectedPath(_handle, i);
            if (p is not null)
            {
                list.Add(p);
            }
        }
        return list;
    }

    /// <summary>캐시된(실체화된) 행들의 <see cref="DirItem.IsSelected"/>를 코어 상태로 동기화.</summary>
    private void RefreshSelectionVisuals()
    {
        foreach (var item in _cache.Values)
        {
            item.IsSelected = NativeInterop.TreeIsSelected(_handle, item.Id);
        }
    }

    /// <summary>
    /// 코어 변경 구간(<paramref name="start"/>에서 <paramref name="removed"/>개 제거 후 <paramref name="inserted"/>개 삽입)에
    /// 맞춰 <c>_cache</c>(인덱스→행)와 캐럿 인덱스를 시프트한다. 위쪽(&lt; start)은 그대로 두어 실체화·아이콘을 보존.
    /// </summary>
    private void ApplyDiff(int start, int removed, int inserted)
    {
        int delta = inserted - removed;
        if (_cache.Count > 0)
        {
            var rebuilt = new Dictionary<int, DirItem>(_cache.Count);
            foreach (var (k, v) in _cache)
            {
                if (k < start)
                {
                    rebuilt[k] = v;             // 위쪽: 유지(실체화·아이콘 보존)
                }
                else if (k >= start + removed)
                {
                    rebuilt[k + delta] = v;     // 아래쪽: 시프트
                }
                // [start, start+removed): 제거된 행 → 버림
            }
            _cache.Clear();
            foreach (var (k, v) in rebuilt)
            {
                _cache[k] = v;
            }
        }
        if (_caretIndex >= start)
        {
            _caretIndex = _caretIndex < start + removed
                ? start - 1                 // 제거된 범위 안이었으면 토글 행으로
                : _caretIndex + delta;
        }
    }

    /// <summary>범위 변경(Add/Remove)을 통지한다. ItemsRepeater는 인덱스·개수만 사용하고 실제 행은 소스 인덱서로 재조회한다.</summary>
    private void RaiseChange(NotifyCollectionChangedAction action, int start, int count) =>
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(action, new CountOnlyList(count), start));

    /// <summary>범위 통지에서 개수만 필요할 때 쓰는 경량 IList(행 실체화 회피 — 대형 펼침에도 무블록).</summary>
    private sealed class CountOnlyList : IList
    {
        public CountOnlyList(int count) => Count = count;
        public int Count { get; }
        public bool IsFixedSize => true;
        public bool IsReadOnly => true;
        public bool IsSynchronized => false;
        public object SyncRoot => this;
        public object? this[int index] { get => null; set => throw new NotSupportedException(); }
        public int Add(object? value) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public bool Contains(object? value) => false;
        public int IndexOf(object? value) => -1;
        public void Insert(int index, object? value) => throw new NotSupportedException();
        public void Remove(object? value) => throw new NotSupportedException();
        public void RemoveAt(int index) => throw new NotSupportedException();
        public void CopyTo(Array array, int index) { }
        public IEnumerator GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
            {
                yield return null;
            }
        }
    }

    private void RaiseReset() =>
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

    /// <summary>트리 핸들 해제 + 캐시 비움.</summary>
    public void Close()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeInterop.TreeClose(_handle);
            _handle = IntPtr.Zero;
        }
        _cache.Clear();
    }

    public void Dispose() => Close();

    // ── 조회 (읽기 전용; 대량이면 Count만큼 순회 — 선택은 코어로 이관 권장) ─────

    /// <summary>
    /// Id로 가시 인덱스를 찾는다(없으면 -1). 코어에 위임(단일 호출) — 큰 폴더에서 클릭마다
    /// 모든 행을 재실체화(P/Invoke·아이콘 로드)하던 선형 탐색을 제거(성능 슬라이스 4-3).
    /// </summary>
    public int IndexOf(DirItem item) =>
        _handle == IntPtr.Zero ? -1 : NativeInterop.TreeIndexOf(_handle, item.Id);

    public bool Contains(DirItem item) => IndexOf(item) >= 0;

    public IEnumerator<DirItem> GetEnumerator()
    {
        int n = Count;
        for (int i = 0; i < n; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ── IList (비제네릭, ItemsSource용) — 변경은 코어 주도이므로 직접 변형 미지원 ──

    bool IList.IsFixedSize => false;
    bool IList.IsReadOnly => true;
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => this;

    object? IList.this[int index]
    {
        get => this[index];
        set => throw new NotSupportedException("코어 트리가 소유 — 직접 설정 불가.");
    }

    int IList.IndexOf(object? value) => value is DirItem d ? IndexOf(d) : -1;

    bool IList.Contains(object? value) => value is DirItem d && Contains(d);

    void ICollection.CopyTo(Array array, int index)
    {
        int n = Count;
        for (int i = 0; i < n; i++)
        {
            array.SetValue(this[i], index + i);
        }
    }

    int IList.Add(object? value) => throw new NotSupportedException();
    void IList.Insert(int index, object? value) => throw new NotSupportedException();
    void IList.Remove(object? value) => throw new NotSupportedException();
    void IList.RemoveAt(int index) => throw new NotSupportedException();
    void IList.Clear() => throw new NotSupportedException();
}
