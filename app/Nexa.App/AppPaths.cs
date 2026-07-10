using System;
using System.IO;

namespace Nexa.App;

/// <summary>
/// 영속 파일 물리 경로의 단일 원천(docs/43) + <b>포터블 모드</b>(docs/12 §3 Portable-ready).
/// 판정 = exe 옆 <c>portable.ini</c> 존재 <b>또는</b> <c>--portable</c> 실행 인자(시작 시 1회, 이후 불변).
/// 포터블이면 모든 영속물(설정·세션·언어팩·로그)이 exe 옆 <c>./data/</c> 아래로 모인다
/// (USB/공유폴더 자기완결 — 로밍/로컬 구분은 포터블에선 무의미). 설치형은 표준
/// <c>%APPDATA%</c>(로밍)·<c>%LOCALAPPDATA%</c>(머신 로컬) <c>\NexaDir</c> — 단일 코드 경로, 위치만 분기.
/// </summary>
internal static class AppPaths
{
    /// <summary>포터블 모드 여부(시작 시 1회 판정). 기능 차이는 없고 영속물 위치만 바뀐다.</summary>
    public static bool IsPortable { get; } = DetectPortable();

    /// <summary>로밍 성격(설정·언어팩) 루트 — 설치형 <c>%APPDATA%\NexaDir</c> / 포터블 <c>exe\data</c>.</summary>
    public static string RoamingRoot => IsPortable
        ? PortableDataDir
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NexaDir");

    /// <summary>머신 로컬 성격(세션·크래시 로그) 루트 — 설치형 <c>%LOCALAPPDATA%\NexaDir</c> / 포터블 <c>exe\data</c>.</summary>
    public static string LocalRoot => IsPortable
        ? PortableDataDir
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexaDir");

    private static string PortableDataDir => Path.Combine(AppContext.BaseDirectory, "data");

    private static bool DetectPortable()
    {
        try
        {
            if (File.Exists(Path.Combine(AppContext.BaseDirectory, "portable.ini")))
            {
                return true;
            }
            foreach (string a in Environment.GetCommandLineArgs())
            {
                if (string.Equals(a, "--portable", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            // 판정 실패 = 설치형 기본(안전한 쪽).
        }
        return false;
    }
}
