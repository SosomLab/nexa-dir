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
/// 슬라이스 3b-2 컴포넌트 — MainWindow 배선은 후속(3b-2 배선/3b-3). C1 설계: docs/29·07.
/// 구조 변경(펼침/접힘)은 현재 <see cref="NotifyCollectionChangedAction.Reset"/>로 통지(정확·단순).
/// 범위 diff(RangeChange) 기반 세밀 통지는 성능 슬라이스(4)에서.
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

    /// <summary>가시성 필터를 적용해 <paramref name="path"/> 트리를 (재)연다. 실패 시 빈 목록.</summary>
    public bool Open(string path, bool showHidden, bool showDotFiles)
    {
        Close();
        _handle = NativeInterop.TreeOpen(path, showHidden, showDotFiles);
        RootPath = path;
        _caretIndex = -1;
        RaiseReset();
        return _handle != IntPtr.Zero;
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
    /// 경로가 일치하는 가시 행 인덱스(없으면 -1). 끝 구분자 무시·대소문자 무시.
    /// 경로만 필요하므로 <see cref="DirItem"/>를 실체화하지 않고 코어 <c>TreeRowPath</c>만 조회
    /// (아이콘 로드·행 캐시 오염 회피, 성능 슬라이스 4-3).
    /// </summary>
    public int IndexOfPath(string fullPath)
    {
        if (_handle == IntPtr.Zero)
        {
            return -1;
        }
        string target = fullPath.TrimEnd('\\', '/');
        int n = Count;
        for (int i = 0; i < n; i++)
        {
            string p = NativeInterop.TreeRowPath(_handle, i) ?? string.Empty;
            if (string.Equals(p.TrimEnd('\\', '/'), target, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }

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

    /// <summary>폴더면 펼침/접힘 토글. 구조가 바뀌므로 캐시 무효화 + Reset 통지.</summary>
    public void ToggleExpand(DirItem item)
    {
        if (_handle == IntPtr.Zero || !item.IsDir)
        {
            return;
        }
        _ = item.IsExpanded
            ? NativeInterop.TreeCollapse(_handle, item.Id)
            : NativeInterop.TreeExpand(_handle, item.Id);
        InvalidateAndReset();
    }

    /// <summary>
    /// 저장된 펼침 경로들을 재적용한다(F18 진입/이동 간 펼침 유지). 얕은→깊은 순으로 처리해
    /// 부모를 먼저 펼쳐야 자식이 가시화되어 찾을 수 있다. 코어를 직접 질의(캐시 미사용)하고
    /// 전부 처리한 뒤 캐시 무효화 + Reset을 <b>1회</b>만 통지한다.
    /// </summary>
    public void ExpandPaths(IEnumerable<string> paths)
    {
        if (_handle == IntPtr.Zero)
        {
            return;
        }
        // 경로 구분자 수(깊이) 오름차순 — 부모 먼저.
        var ordered = new List<string>(paths);
        ordered.Sort((a, b) => Depth(a).CompareTo(Depth(b)));

        bool changed = false;
        foreach (var target in ordered)
        {
            string norm = target.TrimEnd('\\', '/');
            int n = NativeInterop.TreeVisibleLen(_handle);
            for (int i = 0; i < n; i++)
            {
                if (!NativeInterop.TreeGetRow(_handle, i, out TreeRow r) || !r.HasChildren || r.Expanded)
                {
                    continue;
                }
                string p = NativeInterop.TreeRowPath(_handle, i) ?? string.Empty;
                if (string.Equals(p.TrimEnd('\\', '/'), norm, StringComparison.OrdinalIgnoreCase))
                {
                    NativeInterop.TreeExpand(_handle, r.Id);   // 가시 목록이 늘어남(다음 target에 반영)
                    changed = true;
                    break;
                }
            }
        }
        if (changed)
        {
            InvalidateAndReset();
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

    private void InvalidateAndReset()
    {
        _cache.Clear();
        RaiseReset();
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
