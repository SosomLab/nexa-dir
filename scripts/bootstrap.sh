#!/usr/bin/env bash
# Nexa Dir macOS 개발 환경 부트스트랩 (Homebrew 기반).
# 코어(Rust)·크로스플랫폼 C# 개발용. WinUI 셸 빌드는 Windows 필요(docs/11).
# 패키지 관리자로 설치되지 않으면 수동 다운로드 URL을 안내한다.
# 사용: bash scripts/bootstrap.sh
set -uo pipefail

info() { printf '\033[36m▶ %s\033[0m\n' "$*"; }
ok()   { printf '\033[32m✓ %s\033[0m\n' "$*"; }
warn() { printf '\033[33m! %s\033[0m\n' "$*"; }

manual() { # 수동 설치 안내
  warn "패키지 관리자로 '$1' 설치 실패 — 직접 설치하세요:"
  printf '   다운로드: %s\n' "$2"
}

# --- Homebrew 확인 (없으면 수동 설치 안내 후 종료) ---
if ! command -v brew >/dev/null 2>&1; then
  warn "Homebrew(brew)가 없습니다. 먼저 설치하세요:"
  echo '   /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"'
  echo '   안내: https://brew.sh'
  exit 1
fi
ok "Homebrew 확인됨"

brew_install() { # $1=formula  $2=manual_url  [$3=--cask]
  local pkg="$1" url="$2" cask="${3:-}"
  if brew list $cask "$pkg" >/dev/null 2>&1; then ok "$pkg (이미 설치됨)"; return 0; fi
  info "brew install $cask $pkg"
  if brew install $cask "$pkg"; then ok "$pkg 설치"; else manual "$pkg" "$url"; fi
}

# --- 도구 설치 ---
brew_install git        "https://git-scm.com/downloads"
brew_install rustup     "https://rustup.rs"
brew_install dotnet-sdk "https://dotnet.microsoft.com/download" --cask
brew_install visual-studio-code "https://code.visualstudio.com/download" --cask

# --- Rust 툴체인 초기화 ---
if command -v rustup-init >/dev/null 2>&1 && [ ! -d "$HOME/.cargo" ]; then
  info "rustup-init 실행"; rustup-init -y --no-modify-path || manual "rustup" "https://rustup.rs"
fi
# cargo 환경 로드
[ -f "$HOME/.cargo/env" ] && source "$HOME/.cargo/env"
if command -v rustup >/dev/null 2>&1; then
  rustup default stable >/dev/null 2>&1 || true
  ok "Rust: $(rustc --version 2>/dev/null || echo '재시작 후 PATH 반영 필요')"
else
  manual "rustup" "https://rustup.rs"
fi

echo
ok "macOS 부트스트랩 완료. 새 터미널에서:"
echo "   cargo test --manifest-path core/Cargo.toml      # 코어(Rust) — 맥 빌드/테스트 가능"
echo "   # WinUI 앱(app/)은 Windows 필요 — docs/11 참조"
