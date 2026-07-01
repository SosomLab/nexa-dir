using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace Nexa.Controls;

/// <summary>
/// 컬럼 너비 조절 그립(헤더 셀 우측 8px). 마우스를 올리면 좌우 화살표(<see cref="InputSystemCursorShape.SizeWestEast"/>)
/// 커서로 바뀌어 리사이즈 가능 위치임을 식별시킨다. WinUI 3는 <c>UIElement.ProtectedCursor</c>(protected)로만
/// 커서를 지정할 수 있어, 이를 노출하는 전용 서브클래스로 둔다(<see cref="Grid"/> 기반: 투명 히트영역).
/// </summary>
public sealed class ColumnResizeGrip : Grid
{
    public ColumnResizeGrip()
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
    }
}
