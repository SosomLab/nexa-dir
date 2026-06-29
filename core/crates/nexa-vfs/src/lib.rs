//! nexa-vfs — 가상 파일시스템 추상화. 모든 저장소를 통일 인터페이스로 다룬다.
//!
//! 스캐폴딩 단계의 스텁. 후속 단위에서 로컬 **스트리밍 열거**(FR-A1) 구현 예정.

use nexa_core::FileKind;

/// 디렉터리 항목(최소 형태). 후속 단위에서 메타데이터 확장.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Entry {
    pub name: String,
    pub kind: FileKind,
}

/// 저장소 공급자 추상화. (로컬/SFTP/S3/클라우드)
///
/// 후속 단위에서 `list`/`stat`/`read`/`watch` 등을 추가한다.
pub trait Provider {
    /// 공급자 스킴 식별자 (예: "local", "sftp", "s3").
    fn scheme(&self) -> &str;
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn entry_holds_kind() {
        let e = Entry { name: "a.txt".into(), kind: FileKind::File };
        assert_eq!(e.kind, FileKind::File);
        assert_eq!(e.name, "a.txt");
    }
}
