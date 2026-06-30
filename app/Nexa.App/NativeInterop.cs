using System.Runtime.InteropServices;

namespace Nexa.App;

/// <summary>
/// Rust 코어(nexa-interop cdylib)의 C ABI 표면에 대한 P/Invoke 바인딩.
/// dll은 빌드 시 <c>core/target/&lt;profile&gt;/nexa_interop.dll</c> 에서 앱 출력 디렉토리로 복사된다
/// (Nexa.App.csproj의 BuildNexaInterop/CopyNexaInterop 타겟). 상세: docs/18 §디버깅·빌드.
/// </summary>
internal static class NativeInterop
{
    private const string Dll = "nexa_interop";

    /// <summary>인터롭 ABI 버전(호환성 점검용). 불일치 시 로드 거부 등에 사용 예정.</summary>
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint nexa_abi_version();

    /// <summary>왕복 PoC: 두 정수의 합을 코어(Rust)에서 계산해 반환.</summary>
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nexa_poc_add(int a, int b);
}
