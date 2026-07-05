# 31 · 네비게이션 스크롤 위치 — 기존 vs 현재 (검토용 노트)

> C1(코어 트리 가상화 소비) 전환 중 발생한 **"폴더 이동 시 스크롤 위치" 회귀**와 그 해결 과정을 기록한다.
> 나중에 재검토(특히 가변 행 높이·성능 개선 시)용. 관련: [29 ADR-0004](29-adr-0004-core-tree-model.md)·[refactor-001 worklog](journal/archive/refactor-001-worklog.md) QA #5~#7.

## 1. 요구 동작
- **폴더 진입**(더블클릭): 새 목록의 **첫 항목이 맨 위**에 보인다.
- **상위로 이동**(위로/뒤로): 방금 떠난 폴더가 선택된 채 **화면 가운데**(기본, 설정 `ViewOptions.UpNavTargetAlign`)에 보인다.

## 2. 왜 회귀했나 (기존 → C1)

| | 기존 (C1 이전) | C1 이후 |
| --- | --- | --- |
| 목록 소스 | `ObservableCollection<DirItem>` | `VirtualTreeCollection`(코어 스트림 가상화) |
| 폴더 로드 | `items.Clear()` → 항목마다 `items.Add(it)` | `items.Open(path)` → **`Reset` 1회 통지**(같은 인스턴스 재사용) |
| 스크롤 리셋 | `Clear()`로 목록이 비며 ItemsRepeater가 **자연히 top으로** | Reset은 **스크롤 오프셋을 유지** → 진입 시 이전 위치 잔존(버그 #5) |
| 대상 스크롤 | `Repeater.TryGetElement(index)?.StartBringIntoView()` | 동일 코드로는 **미실체화(오프스크린) 대상 = null → 무동작**(버그 #6) |

핵심: **Reset 기반 재사용은 스크롤을 리셋하지 않고, `TryGetElement`는 실체화된 행만** 다룬다.

## 3. 시도별 코드 비교

### v0 — 기존 (참고)
```csharp
// 로드: Clear→Add 반복 → 자연 top / 대상: 실체화된 것만
public void BringIndexIntoView(int index)
{
    if (Repeater.TryGetElement(index) is UIElement el) el.StartBringIntoView();
}
```

### v1 — 강제 실체화 + 정렬 (❌ 먼 인덱스에서 공백)
```csharp
var el = Repeater.GetOrCreateElement(index);   // 오프스크린도 실체화
Repeater.UpdateLayout();
el.StartBringIntoView(new BringIntoViewOptions { VerticalAlignmentRatio = ratio });
```
- 근거리 대상은 OK. **먼 인덱스(예: 30↓)** → ItemsRepeater의 **실체화 창과 스크롤 오프셋 불일치** → 상단 공백(위 스크롤 무동작, 아래로 스크롤해야 채워짐).

### v1.1 — 위 코드를 `DispatcherQueue`로 지연 (❌ 여전히 공백)
- Reset 레이아웃 이후로 미뤄도 `GetOrCreateElement`의 앵커 문제 자체는 남음 → **먼 인덱스 공백 지속**.

### v2 — 균일 행 높이 오프셋 + `ChangeView` (✅ 해결, 현재)
```csharp
public void ScrollIndexIntoView(int index, double verticalAlignmentRatio)
{
    if (index < 0) return;
    DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
    {
        var view = Repeater.ItemsSourceView;
        if (view is null || index >= view.Count) return;
        double stride = EstimateRowStride();                       // 실체화된 행에서 실측
        double offset = index * stride - (BodyScroll.ViewportHeight - stride) * verticalAlignmentRatio;
        BodyScroll.ChangeView(null, Math.Max(0, offset), null, disableAnimation: true);
    });
}
// 진입: BodyScroll.ChangeView(null, 0, null, true)  // ScrollToTop
```
- `GetOrCreateElement`/`StartBringIntoView` **폐기**. 목표 오프셋을 계산해 **`ScrollViewer.ChangeView`로 정상 스크롤** → 가상화가 뷰포트를 정상 채움(**공백 없음**). 행 높이가 **균일**하므로 가운데 정렬도 정확.

## 4. 왜 v2가 맞나
- `ChangeView(offset)`는 **정상 스크롤 경로** → ItemsRepeater 가상화가 그 오프셋 구간을 실체화(공백 없음). `GetOrCreateElement`처럼 앵커를 강제로 심지 않는다.
- 이 그리드의 행은 **단일 라인·동일 패딩 = 균일 높이** → `index × stride` 오프셋이 정확. `stride`는 실체화된 행에서 실측(폴백 24).
- Reset 직후 확정(extent/viewport 계산 완료) 위해 `DispatcherQueue.Low` 지연 유지.

## 5. 트레이드오프 / 향후 재검토 포인트
- **균일 높이 가정 의존**: 향후 **가변 행 높이**(멀티라인·썸네일 큰 아이콘·그룹 헤더) 도입 시 `index×stride` 계산이 어긋난다 → 그때는 누적 높이 인덱스(부모별/행별 오프셋 캐시)나 코어의 행 매핑(O(log n), 슬라이스 4)과 연동 필요.
- **뒤로/앞으로 스크롤 복원 미구현**: 현재 back/forward도 top/center 규칙만 적용. 이전 스크롤 위치 복원은 미포함(후속).
- **성능(슬라이스 4)**: 펼침/접힘이 아직 `Reset` 통지 → 범위 diff(Insert/Remove)로 바꾸면 스크롤 안정성·성능 개선. 그때 이 스크롤 로직도 함께 점검.
- **설정화**: 대상 정렬 위치(가운데/맨앞/맨뒤)는 `ViewOptions.UpNavTargetAlign`(기본 0.5)로 데이터화됨 — 선택 UI는 설정 화면 백로그([26 §8](26-command-palette.md)).

## 6. 참고 (구현 위치·커밋)
- 코드: [app/Nexa.Controls/NexaFileGrid.xaml.cs](../app/Nexa.Controls/NexaFileGrid.xaml.cs) `ScrollToTop`/`ScrollIndexIntoView`/`EstimateRowStride` · [MainWindow.xaml.cs](../app/Nexa.App/MainWindow.xaml.cs) `LoadDirectory`(ScrollToTop)·`SelectByPath`(ScrollIndexIntoView).
- 커밋: `a0bfb31`(v1 도입) → `6408a00`(v1.1 지연) → `e90c8fb`(**v2 해결**).
