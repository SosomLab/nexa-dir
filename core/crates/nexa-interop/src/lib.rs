//! nexa-interop — C ABI 표면. WinUI(C#) 호스트가 P/Invoke로 로드하는 cdylib.
//!
//! ABI 버전·왕복 PoC + **디렉터리 스트리밍 열거**(핸들 기반 open/next/close).

use std::ffi::{c_char, CStr, CString};
use std::os::raw::{c_int, c_uint};
use std::path::PathBuf;
use std::ptr;
use std::time::UNIX_EPOCH;

use nexa_core::FileKind;
use nexa_vfs::{read_dir_entries, Entry};

/// 인터롭 ABI 버전. C# 측과 호환성 점검용(불일치 시 로드 거부 가능).
/// v2: `NexaEntry`에 `attrs`(Windows 파일 속성) 필드 추가.
#[no_mangle]
pub extern "C" fn nexa_abi_version() -> c_uint {
    2
}

/// 왕복(round-trip) PoC: C# 호스트가 보낸 두 정수의 합을 반환한다.
///
/// Rust↔C# 인터롭 경계의 **값 전달·반환**을 검증하기 위한 최소 함수.
/// 오버플로는 래핑 처리하여 경계를 넘는 패닉(언와인딩)을 방지한다.
#[no_mangle]
pub extern "C" fn nexa_poc_add(a: c_int, b: c_int) -> c_int {
    a.wrapping_add(b)
}

// ── 디렉터리 스트리밍 열거 (핸들 기반) ──────────────────────────────────────

/// 디렉터리 열거 핸들(불투명). `nexa_dir_open`이 생성, `nexa_dir_close`로 해제.
/// 현재 엔트리의 이름 문자열을 보관해 `NexaEntry.name` 포인터의 수명을 보장한다.
pub struct DirHandle {
    iter: Box<dyn Iterator<Item = std::io::Result<Entry>>>,
    current_name: Option<CString>,
}

/// C ABI로 전달하는 디렉터리 엔트리.
/// `name`은 **다음 `nexa_dir_next`/`nexa_dir_close` 호출 전까지만** 유효(핸들 소유).
#[repr(C)]
pub struct NexaEntry {
    pub name: *const c_char,
    pub kind: u32, // 0=file, 1=dir, 2=symlink
    pub size: u64,
    pub modified_unix_ms: i64,        // 없으면 -1
    pub attrs: u32,                   // Windows 파일 속성 비트(FILE_ATTRIBUTE_*), 비Windows=0
}

/// 디렉터리 열거를 시작한다. 실패(널/경로오류/IO)면 널 반환.
///
/// # Safety
/// `path`는 유효한 NUL 종단 UTF-8 C 문자열이거나 널이어야 한다.
/// 반환된 핸들은 `nexa_dir_close`로 정확히 한 번 해제해야 한다.
#[no_mangle]
pub unsafe extern "C" fn nexa_dir_open(path: *const c_char) -> *mut DirHandle {
    if path.is_null() {
        return ptr::null_mut();
    }
    let path = match CStr::from_ptr(path).to_str() {
        Ok(s) => PathBuf::from(s),
        Err(_) => return ptr::null_mut(),
    };
    match read_dir_entries(path) {
        Ok(iter) => Box::into_raw(Box::new(DirHandle {
            iter: Box::new(iter),
            current_name: None,
        })),
        Err(_) => ptr::null_mut(),
    }
}

/// 다음 엔트리를 `out`에 채운다. 반환: `1`=엔트리, `0`=끝, `-1`=오류(널 인자).
/// 엔트리 단위 오류(권한 등)는 건너뛰고 다음 유효 엔트리까지 진행한다.
///
/// # Safety
/// `handle`은 `nexa_dir_open`이 반환한 유효 핸들, `out`은 쓰기 가능한 `NexaEntry`여야 한다.
/// 채워진 `out.name`은 다음 `nexa_dir_next`/`nexa_dir_close` 호출 전까지만 유효하다.
#[no_mangle]
pub unsafe extern "C" fn nexa_dir_next(handle: *mut DirHandle, out: *mut NexaEntry) -> c_int {
    if handle.is_null() || out.is_null() {
        return -1;
    }
    let h = &mut *handle;
    for item in h.iter.by_ref() {
        let Ok(entry) = item else { continue };
        let kind = match entry.kind {
            FileKind::File => 0,
            FileKind::Dir => 1,
            FileKind::Symlink => 2,
        };
        let modified = entry
            .modified
            .and_then(|t| t.duration_since(UNIX_EPOCH).ok())
            .map_or(-1, |d| d.as_millis() as i64);
        h.current_name = Some(CString::new(entry.name).unwrap_or_default());
        let out = &mut *out;
        out.name = h.current_name.as_ref().map_or(ptr::null(), |c| c.as_ptr());
        out.kind = kind;
        out.size = entry.size;
        out.modified_unix_ms = modified;
        out.attrs = entry.attrs;
        return 1;
    }
    0
}

/// 디렉터리 핸들을 해제한다. 널은 무시.
///
/// # Safety
/// `handle`은 `nexa_dir_open`이 반환한 핸들이며 이전에 해제되지 않았어야 한다.
#[no_mangle]
pub unsafe extern "C" fn nexa_dir_close(handle: *mut DirHandle) {
    if !handle.is_null() {
        drop(Box::from_raw(handle));
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn abi_version_is_two() {
        assert_eq!(nexa_abi_version(), 2);
    }

    #[test]
    fn poc_add_roundtrip() {
        assert_eq!(nexa_poc_add(2, 3), 5);
        assert_eq!(nexa_poc_add(-1, 1), 0);
        assert_eq!(nexa_poc_add(0, 0), 0);
    }

    #[test]
    fn dir_handle_enumerates() {
        let base = std::env::temp_dir().join(format!("nexa_interop_dir_{}", std::process::id()));
        let _ = std::fs::remove_dir_all(&base);
        std::fs::create_dir_all(&base).unwrap();
        std::fs::write(base.join("f.txt"), b"hi").unwrap();

        let cpath = CString::new(base.to_str().unwrap()).unwrap();
        let mut names = Vec::new();
        unsafe {
            let h = nexa_dir_open(cpath.as_ptr());
            assert!(!h.is_null());
            let mut e = NexaEntry {
                name: ptr::null(),
                kind: 99,
                size: 0,
                modified_unix_ms: 0,
                attrs: 0,
            };
            while nexa_dir_next(h, &mut e) == 1 {
                let name = CStr::from_ptr(e.name).to_string_lossy().into_owned();
                names.push((name, e.kind, e.size));
            }
            nexa_dir_close(h);
        }
        std::fs::remove_dir_all(&base).unwrap();

        assert_eq!(names.len(), 1);
        assert_eq!(names[0].0, "f.txt");
        assert_eq!(names[0].1, 0); // file
        assert_eq!(names[0].2, 2); // "hi" = 2 bytes
    }

    #[test]
    fn dir_open_null_and_missing() {
        unsafe {
            assert!(nexa_dir_open(ptr::null()).is_null());
        }
        let missing = std::env::temp_dir().join("nexa_interop_missing_zzz");
        let cpath = CString::new(missing.to_str().unwrap()).unwrap();
        unsafe {
            assert!(nexa_dir_open(cpath.as_ptr()).is_null());
        }
    }
}
