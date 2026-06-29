//! nexa-interop — C ABI 표면. WinUI(C#) 호스트가 P/Invoke로 로드하는 cdylib.
//!
//! 스캐폴딩 단계: ABI 버전 함수 1개. 후속 단위에서 핸들 기반 API·이벤트 스트림 추가.

use std::os::raw::c_uint;

/// 인터롭 ABI 버전. C# 측과 호환성 점검용(불일치 시 로드 거부 가능).
#[no_mangle]
pub extern "C" fn nexa_abi_version() -> c_uint {
    1
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn abi_version_is_one() {
        assert_eq!(nexa_abi_version(), 1);
    }
}
