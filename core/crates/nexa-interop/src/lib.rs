//! nexa-interop — C ABI 표면. WinUI(C#) 호스트가 P/Invoke로 로드하는 cdylib.
//!
//! 스캐폴딩 단계: ABI 버전 함수 1개. 후속 단위에서 핸들 기반 API·이벤트 스트림 추가.

use std::os::raw::{c_int, c_uint};

/// 인터롭 ABI 버전. C# 측과 호환성 점검용(불일치 시 로드 거부 가능).
#[no_mangle]
pub extern "C" fn nexa_abi_version() -> c_uint {
    1
}

/// 왕복(round-trip) PoC: C# 호스트가 보낸 두 정수의 합을 반환한다.
///
/// Rust↔C# 인터롭 경계의 **값 전달·반환**을 검증하기 위한 최소 함수.
/// 오버플로는 래핑 처리하여 경계를 넘는 패닉(언와인딩)을 방지한다.
#[no_mangle]
pub extern "C" fn nexa_poc_add(a: c_int, b: c_int) -> c_int {
    a.wrapping_add(b)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn abi_version_is_one() {
        assert_eq!(nexa_abi_version(), 1);
    }

    #[test]
    fn poc_add_roundtrip() {
        assert_eq!(nexa_poc_add(2, 3), 5);
        assert_eq!(nexa_poc_add(-1, 1), 0);
        assert_eq!(nexa_poc_add(0, 0), 0);
    }
}
