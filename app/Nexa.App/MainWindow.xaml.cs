using Microsoft.UI.Xaml;

namespace Nexa.App;

/// <summary>메인 윈도우. 후속 단위에서 경로 바·듀얼 패널·트리를 채운다.</summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ShowInteropRoundTrip();
    }

    /// <summary>
    /// 인터롭 왕복 PoC — Rust 코어(nexa-interop)를 P/Invoke로 호출해 결과를 표시한다.
    /// ABI 버전 점검 + nexa_poc_add(2,3) 왕복. 실패 시 메시지로 격리(앱은 계속 동작).
    /// </summary>
    private void ShowInteropRoundTrip()
    {
        try
        {
            uint abi = NativeInterop.nexa_abi_version();
            int sum = NativeInterop.nexa_poc_add(2, 3);
            StatusText.Text = $"인터롭 OK — abi={abi}, nexa_poc_add(2, 3)={sum}";
        }
        catch (System.Exception ex)
        {
            StatusText.Text = $"인터롭 실패: {ex.Message}";
        }
    }
}
