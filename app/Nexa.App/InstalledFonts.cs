using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Nexa.App;

/// <summary>
/// 설치된 글꼴 패밀리 열거(GDI <c>EnumFontFamiliesExW</c>) + 글꼴 이름 검증(PREF-3, docs/40).
/// 설정 창 글꼴 콤보 목록·직접 입력 검증에 사용. 최초 1회 열거 후 캐시(설치 변경은 재시작 반영).
/// 외부 패키지 없이 P/Invoke만 사용(의존성 정책 — 퍼미시브 온리와 무관하게 무의존이 최선).
/// </summary>
internal static class InstalledFonts
{
    private const byte DefaultCharset = 1;   // DEFAULT_CHARSET

    private static IReadOnlyList<string>? _families;
    private static HashSet<string>? _set;

    /// <summary>설치 글꼴 패밀리(정렬·중복 제거, 세로쓰기 '@' 변형 제외).</summary>
    public static IReadOnlyList<string> Families()
    {
        EnsureLoaded();
        return _families!;
    }

    /// <summary>
    /// 쉼표 목록("Cascadia Mono, Consolas")에서 <b>설치되지 않은 첫 글꼴 이름</b>을 반환(모두 설치면 null).
    /// 빈 입력/빈 토큰은 빈 문자열("")을 반환해 오류로 취급한다. 토큰 앞뒤 공백·따옴표는 무시.
    /// </summary>
    public static string? FirstMissing(string familyList)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(familyList))
        {
            return string.Empty;
        }
        foreach (string raw in familyList.Split(','))
        {
            string name = raw.Trim().Trim('\'', '"');
            if (name.Length == 0 || !_set!.Contains(name))
            {
                return name;
            }
        }
        return null;
    }

    private static void EnsureLoaded()
    {
        if (_families is not null)
        {
            return;
        }
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        IntPtr hdc = GetDC(IntPtr.Zero);
        try
        {
            var lf = new LOGFONTW { lfCharSet = DefaultCharset };
            EnumFontFamExProc proc = (ref ENUMLOGFONTEXW e, IntPtr _, uint _, IntPtr _) =>
            {
                string name = e.elfLogFont.lfFaceName;
                if (!string.IsNullOrEmpty(name) && name[0] != '@')   // '@'=세로쓰기 변형 제외
                {
                    set.Add(name);
                }
                return 1;   // 계속 열거
            };
            _ = EnumFontFamiliesExW(hdc, ref lf, proc, IntPtr.Zero, 0);
            GC.KeepAlive(proc);
        }
        catch
        {
            // 열거 실패 시 빈 목록 — 검증은 통과 불가가 아니라 "목록 없음"으로 격리(설정 자체는 동작).
        }
        finally
        {
            _ = ReleaseDC(IntPtr.Zero, hdc);
        }
        var list = new List<string>(set);
        list.Sort(StringComparer.OrdinalIgnoreCase);
        _families = list;
        _set = set;
    }

    // ── GDI 인터롭 ───────────────────────────────────────────────────

    private delegate int EnumFontFamExProc(ref ENUMLOGFONTEXW lpelfe, IntPtr lpntme, uint fontType, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct LOGFONTW
    {
        public int lfHeight;
        public int lfWidth;
        public int lfEscapement;
        public int lfOrientation;
        public int lfWeight;
        public byte lfItalic;
        public byte lfUnderline;
        public byte lfStrikeOut;
        public byte lfCharSet;
        public byte lfOutPrecision;
        public byte lfClipPrecision;
        public byte lfQuality;
        public byte lfPitchAndFamily;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string lfFaceName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ENUMLOGFONTEXW
    {
        public LOGFONTW elfLogFont;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string elfFullName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string elfStyle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string elfScript;
    }

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern int EnumFontFamiliesExW(IntPtr hdc, ref LOGFONTW lpLogfont, EnumFontFamExProc lpProc, IntPtr lParam, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
}
