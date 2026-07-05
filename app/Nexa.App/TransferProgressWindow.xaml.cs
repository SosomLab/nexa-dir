using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace Nexa.App;

/// <summary>
/// 파일 전송(복사/이동) <b>진행 창</b>(별도 Window) — 탐색기식. 시작 시 표시하고 바이트 진행률을 라이브 갱신,
/// 완료 후 <b>기본은 열린 채 유지</b>(자동 닫기 off, 사용자가 닫음 — 설정 <see cref="ViewOptions.AutoCloseTransferWindow"/>).
/// 취소 버튼/완료 전 창 닫기는 전송을 취소한다(<see cref="Token"/>).
/// </summary>
public sealed partial class TransferProgressWindow : Window
{
    private readonly CancellationTokenSource _cts = new();
    private bool _finished;

    public TransferProgressWindow(string verb)
    {
        InitializeComponent();
        Title = $"{verb} 진행";
        TitleText.Text = $"{verb} 중…";
        AppWindow.Resize(new SizeInt32(480, 230));   // 작은 고정 크기
        // 완료 전에 창을 닫으면(사용자 X) 전송 취소로 간주.
        Closed += (_, _) => { if (!_finished) { _cts.Cancel(); } };
    }

    /// <summary>전송 취소 토큰 — 취소 버튼/완료 전 닫기 시 신호.</summary>
    public CancellationToken Token => _cts.Token;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>창을 활성화하고 <b>맨 앞으로</b> 올려 포커스를 준다(Activate만으론 전경 보장 안 됨).</summary>
    public void ActivateForeground()
    {
        Activate();
        AppWindow.MoveInZOrderAtTop();   // z-order 최상위
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        SetForegroundWindow(hwnd);       // 전경 창 + 포커스
    }

    /// <summary>총 크기 합산 동안 부정형(indeterminate).</summary>
    public void SetPreparing()
    {
        Bar.IsIndeterminate = true;
        PercentText.Text = "준비 중…";
    }

    /// <summary>총 바이트 확정 → 확정형(determinate)으로 전환.</summary>
    public void SetDeterminate(long totalBytes)
    {
        Bar.IsIndeterminate = false;
        Bar.Maximum = totalBytes > 0 ? totalBytes : 1;
        Bar.Value = 0;
    }

    /// <summary>진행 갱신 — 누적 바이트/총 바이트, 현재 파일 번호/개수·이름.</summary>
    public void Report(long copiedBytes, long totalBytes, int fileNo, int fileTotal, string currentName)
    {
        Bar.Value = Math.Min(copiedBytes, Bar.Maximum);
        int pct = totalBytes > 0 ? (int)(copiedBytes * 100 / totalBytes) : 100;
        PercentText.Text = $"{pct}%";
        DetailText.Text = fileTotal > 1 ? $"{fileNo}/{fileTotal} · {currentName}" : currentName;
    }

    /// <summary>완료 처리 — 결과 요약 표시. <paramref name="autoClose"/>면 즉시 닫고, 아니면 열린 채 유지(닫기 버튼 활성).</summary>
    public void Complete(string summary, bool autoClose)
    {
        _finished = true;
        Bar.IsIndeterminate = false;
        Bar.Value = Bar.Maximum;
        PercentText.Text = "100%";
        TitleText.Text = summary;
        CancelBtn.IsEnabled = false;
        CloseBtn.IsEnabled = true;
        if (autoClose)
        {
            Close();
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        _cts.Cancel();
        CancelBtn.IsEnabled = false;
        TitleText.Text = "취소 중…";
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
