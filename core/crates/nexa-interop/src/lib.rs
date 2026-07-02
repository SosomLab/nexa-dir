//! nexa-interop — C ABI 표면. WinUI(C#) 호스트가 P/Invoke로 로드하는 cdylib.
//!
//! ABI 버전·왕복 PoC + **디렉터리 스트리밍 열거**(핸들 기반 open/next/close).

use std::ffi::{c_char, CStr, CString};
use std::os::raw::{c_int, c_uint};
use std::path::PathBuf;
use std::ptr;
use std::time::UNIX_EPOCH;

use nexa_core::FileKind;
use nexa_tree::{SelectMode, Tree};
use nexa_vfs::{read_dir_entries, Entry};

/// `FileKind` → C ABI 정수(0=file, 1=dir, 2=symlink). 열거·트리 표면 공용.
fn kind_code(kind: FileKind) -> u32 {
    match kind {
        FileKind::File => 0,
        FileKind::Dir => 1,
        FileKind::Symlink => 2,
    }
}

/// 인터롭 ABI 버전. C# 측과 호환성 점검용(호스트가 로드 시 일치 확인 — 슬라이스 3).
/// v2: `NexaEntry.attrs`. v3: 코어 트리/선택 표면(`nexa_tree_*`, `NexaRow`/`NexaRange`).
#[no_mangle]
pub extern "C" fn nexa_abi_version() -> c_uint {
    3
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
    pub modified_unix_ms: i64, // 없으면 -1
    pub attrs: u32,            // Windows 파일 속성 비트(FILE_ATTRIBUTE_*), 비Windows=0
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
        let kind = kind_code(entry.kind);
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

// ── 코어 트리/선택 표면 (C1 슬라이스 2, ABI v3) ──────────────────────────────

/// 트리 열거/선택 핸들(불투명). `nexa_tree_open`이 생성, `nexa_tree_close`로 해제.
/// 반환 문자열(`NexaRow.name`·선택 경로)의 수명 보장을 위해 최근 문자열을 보관한다.
pub struct TreeHandle {
    tree: Tree,
    row_name: Option<CString>,
    sel_path: Option<CString>,
}

/// C ABI 가시 행(코어 `VisibleRow` 미러). 8→4→1바이트 순 배치(패딩 최소·C# 미러 용이).
/// `name`은 다음 `nexa_tree_row`/`nexa_tree_close` 호출 전까지만 유효(핸들 소유).
#[repr(C)]
pub struct NexaRow {
    pub id: u64,
    pub size: u64,
    pub modified_unix_ms: i64,
    pub name: *const c_char,
    pub depth: u32,
    pub kind: u32, // 0=file, 1=dir, 2=symlink
    pub attrs: u32,
    pub expanded: u8,     // 0/1
    pub has_children: u8, // 0/1
}

/// C ABI 가시 목록 변경 구간(코어 `RangeChange` 미러).
#[repr(C)]
pub struct NexaRange {
    pub start: u64,
    pub removed: u64,
    pub inserted: u64,
}

/// 경로로 트리를 연다(최상위 열거, 펼침 없음). 실패(널/경로오류/IO) 시 널.
///
/// # Safety
/// `path`는 유효한 NUL 종단 UTF-8 C 문자열이거나 널이어야 한다.
/// 반환된 핸들은 `nexa_tree_close`로 정확히 한 번 해제해야 한다.
#[no_mangle]
pub unsafe extern "C" fn nexa_tree_open(path: *const c_char) -> *mut TreeHandle {
    if path.is_null() {
        return ptr::null_mut();
    }
    let path = match CStr::from_ptr(path).to_str() {
        Ok(s) => PathBuf::from(s),
        Err(_) => return ptr::null_mut(),
    };
    match Tree::open(path) {
        Ok(tree) => Box::into_raw(Box::new(TreeHandle {
            tree,
            row_name: None,
            sel_path: None,
        })),
        Err(_) => ptr::null_mut(),
    }
}

/// 트리 핸들을 해제한다. 널은 무시.
///
/// # Safety
/// `handle`은 `nexa_tree_open`이 반환한 핸들이며 이전에 해제되지 않았어야 한다.
#[no_mangle]
pub unsafe extern "C" fn nexa_tree_close(handle: *mut TreeHandle) {
    if !handle.is_null() {
        drop(Box::from_raw(handle));
    }
}

/// 현재 가시 행 수. 널이면 0.
///
/// # Safety
/// `handle`은 유효한 트리 핸들이거나 널이어야 한다.
#[no_mangle]
pub unsafe extern "C" fn nexa_tree_visible_len(handle: *mut TreeHandle) -> u64 {
    if handle.is_null() {
        return 0;
    }
    (*handle).tree.visible_len() as u64
}

/// 가시 인덱스의 행을 `out`에 채운다. 반환: `1`=행, `0`=범위 밖, `-1`=널 인자.
/// `out.name`은 다음 `nexa_tree_row`/`nexa_tree_close` 호출 전까지만 유효.
///
/// # Safety
/// `handle`은 유효 핸들, `out`은 쓰기 가능한 `NexaRow`여야 한다.
#[no_mangle]
pub unsafe extern "C" fn nexa_tree_row(
    handle: *mut TreeHandle,
    index: u64,
    out: *mut NexaRow,
) -> c_int {
    if handle.is_null() || out.is_null() {
        return -1;
    }
    let h = &mut *handle;
    let Some(row) = h.tree.row(index as usize) else {
        return 0;
    };
    h.row_name = Some(CString::new(row.name).unwrap_or_default());
    let out = &mut *out;
    out.id = row.id;
    out.size = row.size;
    out.modified_unix_ms = row.modified_unix_ms;
    out.name = h.row_name.as_ref().map_or(ptr::null(), |c| c.as_ptr());
    out.depth = row.depth;
    out.kind = kind_code(row.kind);
    out.attrs = row.attrs;
    out.expanded = row.expanded as u8;
    out.has_children = row.has_children as u8;
    1
}

/// `id`(디렉터리)를 펼치고 변경 구간을 `out`에 채운다.
/// 반환: `1`=성공, `0`=IO 오류(무변경), `-1`=널 인자.
///
/// # Safety
/// `handle`은 유효 핸들, `out`은 쓰기 가능한 `NexaRange`여야 한다.
#[no_mangle]
pub unsafe extern "C" fn nexa_tree_expand(
    handle: *mut TreeHandle,
    id: u64,
    out: *mut NexaRange,
) -> c_int {
    if handle.is_null() || out.is_null() {
        return -1;
    }
    match (*handle).tree.expand(id) {
        Ok(rc) => {
            write_range(out, rc.start, rc.removed, rc.inserted);
            1
        }
        Err(_) => {
            write_range(out, 0, 0, 0);
            0
        }
    }
}

/// `id`를 접고 변경 구간을 `out`에 채운다. 반환: `1`=성공, `-1`=널 인자.
///
/// # Safety
/// `handle`은 유효 핸들, `out`은 쓰기 가능한 `NexaRange`여야 한다.
#[no_mangle]
pub unsafe extern "C" fn nexa_tree_collapse(
    handle: *mut TreeHandle,
    id: u64,
    out: *mut NexaRange,
) -> c_int {
    if handle.is_null() || out.is_null() {
        return -1;
    }
    let rc = (*handle).tree.collapse(id);
    write_range(out, rc.start, rc.removed, rc.inserted);
    1
}

/// `NexaRange`에 값을 쓴다(내부 헬퍼).
///
/// # Safety
/// `out`은 쓰기 가능한 `NexaRange`여야 한다.
unsafe fn write_range(out: *mut NexaRange, start: usize, removed: usize, inserted: usize) {
    let out = &mut *out;
    out.start = start as u64;
    out.removed = removed as u64;
    out.inserted = inserted as u64;
}

/// 선택 갱신: `mode` `0`=단일(기존 해제), `1`=비연속 토글. 그 외는 무시.
///
/// # Safety
/// `handle`은 유효 핸들이거나 널이어야 한다.
#[no_mangle]
pub unsafe extern "C" fn nexa_tree_select(handle: *mut TreeHandle, id: u64, mode: u32) {
    if handle.is_null() {
        return;
    }
    let m = match mode {
        0 => SelectMode::Single,
        1 => SelectMode::Toggle,
        _ => return,
    };
    (*handle).tree.select(id, m);
}

/// anchor~`id`의 가시 범위를 선택한다.
///
/// # Safety
/// `handle`은 유효 핸들이거나 널이어야 한다.
#[no_mangle]
pub unsafe extern "C" fn nexa_tree_select_range(handle: *mut TreeHandle, id: u64) {
    if !handle.is_null() {
        (*handle).tree.select_range(id);
    }
}

/// 현재 가시 노드를 전체 선택한다.
///
/// # Safety
/// `handle`은 유효 핸들이거나 널이어야 한다.
#[no_mangle]
pub unsafe extern "C" fn nexa_tree_select_all(handle: *mut TreeHandle) {
    if !handle.is_null() {
        (*handle).tree.select_all_visible();
    }
}

/// 선택을 모두 해제한다.
///
/// # Safety
/// `handle`은 유효 핸들이거나 널이어야 한다.
#[no_mangle]
pub unsafe extern "C" fn nexa_tree_clear_selection(handle: *mut TreeHandle) {
    if !handle.is_null() {
        (*handle).tree.clear_selection();
    }
}

/// `id` 선택 여부(`1`/`0`). 널이면 0.
///
/// # Safety
/// `handle`은 유효 핸들이거나 널이어야 한다.
#[no_mangle]
pub unsafe extern "C" fn nexa_tree_is_selected(handle: *mut TreeHandle, id: u64) -> c_int {
    if handle.is_null() {
        return 0;
    }
    c_int::from((*handle).tree.is_selected(id))
}

/// 선택 수. 널이면 0.
///
/// # Safety
/// `handle`은 유효 핸들이거나 널이어야 한다.
#[no_mangle]
pub unsafe extern "C" fn nexa_tree_selected_len(handle: *mut TreeHandle) -> u64 {
    if handle.is_null() {
        return 0;
    }
    (*handle).tree.selection_count() as u64
}

/// 선택(삽입 순서) `index`번째 경로. 범위 밖/널이면 널.
/// 반환 포인터는 다음 `nexa_tree_selected_path`/`nexa_tree_close` 호출 전까지만 유효.
///
/// # Safety
/// `handle`은 유효 핸들이거나 널이어야 한다.
#[no_mangle]
pub unsafe extern "C" fn nexa_tree_selected_path(
    handle: *mut TreeHandle,
    index: u64,
) -> *const c_char {
    if handle.is_null() {
        return ptr::null();
    }
    let h = &mut *handle;
    let paths = h.tree.selected_paths();
    let Some(p) = paths.get(index as usize) else {
        return ptr::null();
    };
    h.sel_path = Some(CString::new(p.to_string_lossy().as_ref()).unwrap_or_default());
    h.sel_path.as_ref().map_or(ptr::null(), |c| c.as_ptr())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn abi_version_is_three() {
        assert_eq!(nexa_abi_version(), 3);
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

    fn empty_row() -> NexaRow {
        NexaRow {
            id: 0,
            size: 0,
            modified_unix_ms: 0,
            name: ptr::null(),
            depth: 0,
            kind: 9,
            attrs: 0,
            expanded: 0,
            has_children: 0,
        }
    }

    #[test]
    fn tree_abi_open_expand_select_collapse() {
        let base = std::env::temp_dir().join(format!("nexa_interop_tree_{}", std::process::id()));
        let _ = std::fs::remove_dir_all(&base);
        std::fs::create_dir_all(base.join("sub")).unwrap();
        std::fs::write(base.join("sub/child.txt"), b"c").unwrap();
        std::fs::write(base.join("top.txt"), b"t").unwrap();

        let cpath = CString::new(base.to_str().unwrap()).unwrap();
        unsafe {
            let h = nexa_tree_open(cpath.as_ptr());
            assert!(!h.is_null());
            assert_eq!(nexa_tree_visible_len(h), 2); // sub(dir), top.txt

            let mut row = empty_row();
            assert_eq!(nexa_tree_row(h, 0, &mut row), 1);
            assert_eq!(row.kind, 1); // dir
            assert_eq!(row.has_children, 1);
            assert_eq!(CStr::from_ptr(row.name).to_string_lossy(), "sub");
            let sub_id = row.id;

            // 펼침 → child.txt 1개 삽입
            let mut rng = NexaRange {
                start: 0,
                removed: 0,
                inserted: 0,
            };
            assert_eq!(nexa_tree_expand(h, sub_id, &mut rng), 1);
            assert_eq!((rng.start, rng.removed, rng.inserted), (1, 0, 1));
            assert_eq!(nexa_tree_visible_len(h), 3);

            assert_eq!(nexa_tree_row(h, 1, &mut row), 1); // child.txt
            assert_eq!(row.depth, 1);
            let child_id = row.id;
            assert_eq!(nexa_tree_row(h, 2, &mut row), 1); // top.txt
            let top_id = row.id;

            // 교차 선택(다른 부모): child(single) + top(toggle)
            nexa_tree_select(h, child_id, 0);
            nexa_tree_select(h, top_id, 1);
            assert_eq!(nexa_tree_selected_len(h), 2);
            assert_eq!(nexa_tree_is_selected(h, child_id), 1);
            let p0 = CStr::from_ptr(nexa_tree_selected_path(h, 0)).to_string_lossy();
            assert!(p0.ends_with("child.txt"), "got {p0}");

            // 접힘 → child.txt 제거
            assert_eq!(nexa_tree_collapse(h, sub_id, &mut rng), 1);
            assert_eq!((rng.start, rng.removed, rng.inserted), (1, 1, 0));
            assert_eq!(nexa_tree_visible_len(h), 2);

            // 경계/널 방어
            assert_eq!(nexa_tree_row(h, 99, &mut row), 0);
            nexa_tree_close(h);
            assert_eq!(nexa_tree_visible_len(ptr::null_mut()), 0);
        }
        std::fs::remove_dir_all(&base).unwrap();
    }
}
