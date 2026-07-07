using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Nexa.ViewModels;

namespace Nexa.App;

/// <summary>
/// 휴지통 항목 <b>복원</b>(B-13u S2 — 삭제 undo) — 휴지통 셸 폴더를 열거해 "원래 위치+이름"이 일치하는 항목을
/// 찾아 셸 <c>undelete</c> 동사를 실행한다(탐색기 Ctrl+Z와 동일 메커니즘). Windows 전용.
/// <para>이름 매칭은 정확 일치 우선, 실패 시 확장자 숨김 표시를 감안해 확장자 제외 일치 폴백.</para>
/// </summary>
internal static class RecycleBin
{
    /// <summary>원래 경로 목록에 해당하는 휴지통 항목들을 복원. 반환=복원 실행된 항목 수(요청 수보다 작을 수 있음).</summary>
    public static int RestoreByOriginalPaths(IReadOnlyList<string> originalPaths)
    {
        var wanted = new List<string>(originalPaths.Select(p => p.TrimEnd('\\', '/')));
        var matched = new List<IntPtr>();          // 복원할 child PIDL(휴지통 폴더 기준)
        var allPidls = new List<IntPtr>();         // 해제용 전체
        IntPtr binPidl = IntPtr.Zero;
        object? binObj = null;
        object? icmObj = null;
        try
        {
            if (SHGetDesktopFolder(out IShellFolder desktop) != 0)
            {
                return 0;
            }
            try
            {
                if (SHGetSpecialFolderLocation(IntPtr.Zero, CSIDL_BITBUCKET, out binPidl) != 0)
                {
                    return 0;
                }
                Guid iidFolder2 = typeof(IShellFolder2).GUID;
                desktop.BindToObject(binPidl, IntPtr.Zero, ref iidFolder2, out binObj);
            }
            finally
            {
                Marshal.ReleaseComObject(desktop);
            }
            var bin2 = (IShellFolder2)binObj;
            var bin = (IShellFolder)binObj;

            bin.EnumObjects(IntPtr.Zero, SHCONTF_FOLDERS | SHCONTF_NONFOLDERS | SHCONTF_INCLUDEHIDDEN, out IntPtr enumPtr);
            var e = (IEnumIDList)Marshal.GetObjectForIUnknown(enumPtr);
            try
            {
                while (wanted.Count > 0 && e.Next(1, out IntPtr child, out uint fetched) == 0 && fetched == 1)
                {
                    allPidls.Add(child);
                    string name = DetailsOf(bin2, child, COL_NAME);
                    string loc = DetailsOf(bin2, child, COL_ORIGINAL_LOCATION);
                    if (name.Length == 0 || loc.Length == 0)
                    {
                        continue;
                    }
                    string original = Path.Combine(loc, name).TrimEnd('\\', '/');
                    int idx = wanted.FindIndex(w => string.Equals(w, original, StringComparison.OrdinalIgnoreCase));
                    if (idx < 0)
                    {
                        // 확장자 숨김 표시 폴백: 같은 폴더 + 확장자 제외 이름 일치.
                        idx = wanted.FindIndex(w =>
                            string.Equals(Path.GetDirectoryName(w) ?? "", loc.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)
                            && string.Equals(Path.GetFileNameWithoutExtension(w), name, StringComparison.OrdinalIgnoreCase));
                    }
                    if (idx >= 0)
                    {
                        matched.Add(child);
                        wanted.RemoveAt(idx);   // 경로당 최초 일치 1건(다중 버전은 후속 — 삭제 시각 비교)
                    }
                }
            }
            finally
            {
                Marshal.ReleaseComObject(e);
                Marshal.Release(enumPtr);
            }
            if (matched.Count == 0)
            {
                return 0;
            }

            // 일치 항목들의 IContextMenu → "undelete" 동사(탐색기 복원과 동일 — 이름 충돌 등은 셸 UI가 처리).
            Guid iidIcm = IID_IContextMenu;
            bin.GetUIObjectOf(IntPtr.Zero, (uint)matched.Count, matched.ToArray(), ref iidIcm, IntPtr.Zero, out icmObj);
            var icm = (IContextMenu)icmObj;
            IntPtr verb = Marshal.StringToHGlobalAnsi("undelete");
            try
            {
                var inv = new CMINVOKECOMMANDINFOEX
                {
                    cbSize = Marshal.SizeOf<CMINVOKECOMMANDINFOEX>(),
                    lpVerb = verb,
                    nShow = 1,   // SW_SHOWNORMAL
                };
                int hr = icm.InvokeCommand(ref inv);
                return hr == 0 ? matched.Count : 0;
            }
            finally
            {
                Marshal.FreeHGlobal(verb);
            }
        }
        catch
        {
            return matched.Count > 0 ? 0 : 0;   // 셸 예외 격리 — 복원 실패로 보고
        }
        finally
        {
            if (icmObj is not null)
            {
                Marshal.ReleaseComObject(icmObj);
            }
            if (binObj is not null)
            {
                Marshal.ReleaseComObject(binObj);
            }
            foreach (IntPtr p in allPidls)
            {
                Marshal.FreeCoTaskMem(p);
            }
            if (binPidl != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(binPidl);
            }
        }
    }

    /// <summary>휴지통 상세 컬럼 텍스트(0=이름 · 1=원래 위치, XP 이후 고정 인덱스).</summary>
    private static string DetailsOf(IShellFolder2 folder, IntPtr pidl, uint col)
    {
        try
        {
            folder.GetDetailsOf(pidl, col, out SHELLDETAILS sd);
            var sb = new StringBuilder(520);
            if (StrRetToBufW(ref sd.str, pidl, sb, (uint)sb.Capacity) == 0)
            {
                return sb.ToString();
            }
        }
        catch
        {
            // 개별 항목 실패 격리
        }
        return string.Empty;
    }

    private const int CSIDL_BITBUCKET = 0x000A;
    private const uint SHCONTF_FOLDERS = 0x20;
    private const uint SHCONTF_NONFOLDERS = 0x40;
    private const uint SHCONTF_INCLUDEHIDDEN = 0x80;
    private const uint COL_NAME = 0;
    private const uint COL_ORIGINAL_LOCATION = 1;
    private static readonly Guid IID_IContextMenu = new("000214E4-0000-0000-C000-000000000046");

    // ── COM (수동 vtable — ShellContextMenu.cs 동일 패턴, 파일 지역) ──
    [StructLayout(LayoutKind.Explicit, Size = 272)]   // x64: uType(0) + union(8, 최대 cStr[260])
    private struct STRRET
    {
        [FieldOffset(0)] public uint uType;
        [FieldOffset(8)] public IntPtr pOleStr;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SHELLDETAILS
    {
        public int fmt;
        public int cxChar;
        public STRRET str;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CMINVOKECOMMANDINFOEX
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public IntPtr lpVerb;
        public IntPtr lpParameters;
        public IntPtr lpDirectory;
        public int nShow;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr lpTitle;
        public IntPtr lpVerbW;
        public IntPtr lpParametersW;
        public IntPtr lpDirectoryW;
        public IntPtr lpTitleW;
        public POINT ptInvoke;
    }

    [ComImport, Guid("000214E6-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellFolder
    {
        void ParseDisplayName(IntPtr hwnd, IntPtr pbc, [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName, out uint pchEaten, out IntPtr ppidl, ref uint pdwAttributes);
        void EnumObjects(IntPtr hwnd, uint grfFlags, out IntPtr ppenumIDList);
        void BindToObject(IntPtr pidl, IntPtr pbc, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
        void BindToStorage(IntPtr pidl, IntPtr pbc, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
        [PreserveSig] int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);
        void CreateViewObject(IntPtr hwndOwner, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
        void GetAttributesOf(uint cidl, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, ref uint rgfInOut);
        void GetUIObjectOf(IntPtr hwndOwner, uint cidl, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, ref Guid riid, IntPtr rgfReserved, [MarshalAs(UnmanagedType.Interface)] out object ppv);
        void GetDisplayNameOf(IntPtr pidl, uint uFlags, IntPtr pName);
        void SetNameOf(IntPtr hwnd, IntPtr pidl, [MarshalAs(UnmanagedType.LPWStr)] string pszName, uint uFlags, out IntPtr ppidlOut);
    }

    [ComImport, Guid("93F2F68C-1D1B-11D3-A30E-00C04F79ABD1"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellFolder2
    {
        // IShellFolder 전체 재선언(vtable 순서)
        void ParseDisplayName(IntPtr hwnd, IntPtr pbc, [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName, out uint pchEaten, out IntPtr ppidl, ref uint pdwAttributes);
        void EnumObjects(IntPtr hwnd, uint grfFlags, out IntPtr ppenumIDList);
        void BindToObject(IntPtr pidl, IntPtr pbc, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
        void BindToStorage(IntPtr pidl, IntPtr pbc, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
        [PreserveSig] int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);
        void CreateViewObject(IntPtr hwndOwner, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
        void GetAttributesOf(uint cidl, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, ref uint rgfInOut);
        void GetUIObjectOf(IntPtr hwndOwner, uint cidl, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, ref Guid riid, IntPtr rgfReserved, [MarshalAs(UnmanagedType.Interface)] out object ppv);
        void GetDisplayNameOf(IntPtr pidl, uint uFlags, IntPtr pName);
        void SetNameOf(IntPtr hwnd, IntPtr pidl, [MarshalAs(UnmanagedType.LPWStr)] string pszName, uint uFlags, out IntPtr ppidlOut);
        // IShellFolder2 확장
        void GetDefaultSearchGUID(out Guid pguid);
        void EnumSearches([MarshalAs(UnmanagedType.Interface)] out object ppenum);
        void GetDefaultColumn(uint dwRes, out uint pSort, out uint pDisplay);
        void GetDefaultColumnState(uint iColumn, out uint pcsFlags);
        void GetDetailsEx(IntPtr pidl, IntPtr pscid, [MarshalAs(UnmanagedType.Struct)] out object pv);
        void GetDetailsOf(IntPtr pidl, uint iColumn, out SHELLDETAILS psd);
        void MapColumnToSCID(uint iColumn, IntPtr pscid);
    }

    [ComImport, Guid("000214F2-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumIDList
    {
        [PreserveSig] int Next(uint celt, out IntPtr rgelt, out uint pceltFetched);
        [PreserveSig] int Skip(uint celt);
        [PreserveSig] int Reset();
        [PreserveSig] int Clone([MarshalAs(UnmanagedType.Interface)] out object ppenum);
    }

    [ComImport, Guid("000214E4-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu
    {
        [PreserveSig] int QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
        [PreserveSig] int InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);
        [PreserveSig] int GetCommandString(UIntPtr idCmd, uint uType, IntPtr pReserved, IntPtr pszName, uint cchMax);
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetDesktopFolder(out IShellFolder ppshf);

    [DllImport("shell32.dll")]
    private static extern int SHGetSpecialFolderLocation(IntPtr hwnd, int csidl, out IntPtr ppidl);

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int StrRetToBufW(ref STRRET pstr, IntPtr pidl, StringBuilder pszBuf, uint cchBuf);
}

/// <summary>휴지통 삭제 배치(B-13u S2) — undo: 휴지통에서 원래 위치로 복원(셸 undelete) / redo: 다시 휴지통 삭제.
/// 셸 COM 의존이라 앱 계층(연산 계약 <see cref="IReversibleOp"/>은 ViewModels).</summary>
internal sealed class DeleteBatchOp : IReversibleOp
{
    private readonly IReadOnlyList<string> _paths;

    public DeleteBatchOp(IReadOnlyList<string> paths, string description)
    {
        _paths = paths;
        Description = description;
    }

    public string Description { get; }

    public void Undo()
    {
        int restored = RecycleBin.RestoreByOriginalPaths(_paths);
        if (restored < _paths.Count)
        {
            throw new IOException($"{_paths.Count - restored}개 항목을 휴지통에서 복원하지 못했습니다.");
        }
    }

    public void Redo()
    {
        int failed = 0;
        foreach (string p in _paths)
        {
            try
            {
                if (FileOps.Exists(p))
                {
                    FileOps.DeleteToRecycleBin(p);
                }
            }
            catch (Exception)
            {
                failed++;
            }
        }
        if (failed > 0)
        {
            throw new IOException($"{failed}개 항목을 다시 삭제하지 못했습니다.");
        }
    }
}
