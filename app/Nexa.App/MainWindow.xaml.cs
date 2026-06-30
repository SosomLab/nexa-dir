using System;
using Microsoft.UI.Xaml;

namespace Nexa.App;

/// <summary>메인 윈도우. 후속 단위에서 경로 바·듀얼 패널·트리를 채운다.</summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ShowInteropRoundTrip();
        LoadDirectory(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
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
        catch (Exception ex)
        {
            StatusText.Text = $"인터롭 실패: {ex.Message}";
        }
    }

    /// <summary>
    /// 코어 디렉터리 스트리밍 열거(nexa_dir_*)를 호출해 폴더 내용을 목록에 표시한다.
    /// 실패는 헤더 메시지로 격리(앱은 계속 동작).
    /// </summary>
    private void LoadDirectory(string path)
    {
        try
        {
            var items = NativeInterop.ReadDir(path);
            DirRepeater.ItemsSource = items;
            DirHeader.Text = $"{path} — {items.Count}개 항목 (코어 스트리밍 열거, ItemsRepeater 가상화)";
        }
        catch (Exception ex)
        {
            DirHeader.Text = $"디렉터리 열거 실패: {ex.Message}";
        }
    }
}
