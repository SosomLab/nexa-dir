# make-app-icon.ps1 — Nexa Dir 앱 아이콘 생성기 (GDI+, 외부 도구 불요 · Windows 전용)
#
# 컨셉(2026-07-10 개편): "풀블리드 폴더 + 대형 터미널 프롬프트 >_" — 마테리얼 플랫.
#   · 캔버스를 꽉 채우는 폴더(뒤판 탭 + 앞판 2톤), 배경 투명 = 폴더 실루엣이 곧 아이콘
#   · 실루엣 테두리 = 브랜드 accent 파랑(#3D8BFF) — 흰/검정 배경 모두에서 윤곽 확보
#   · 앞판 중앙에 초록 >_ (개발자/내장 터미널 상징)
# 테마 변형(-Theme): 지오메트리 동일, 색배열만 교체.
#   · dark(기본 세트): 다크 슬레이트 폴더 + 밝은 초록 → nexa-dir-* (앱 창/작업표시줄 기본)
#   · light: 밝은 폴더(연한 파랑 톤) + 진한 초록 → nexa-dir-light-*
# 산출: app/Nexa.App/Assets/AppIcon/<접두사>-1024.png(마스터, 재생성용) + <접두사>.ico(16~256 멀티사이즈, PNG 엔트리)
# (16~512 개별 PNG는 실사용처가 없어 생성하지 않음 — 앱은 ICO만 참조)
# (파일명 접두사 "nexa-dir" — "nexa"는 다른 프로그램들과 공용 브랜드라 제품명으로 구분)
# 재실행 안전(같은 테마는 덮어씀). 1024px 마스터에서 고품질 다운스케일.

param(
    [ValidateSet('light', 'dark', 'all')]
    [string]$Theme = 'all'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$repo = Split-Path $PSScriptRoot -Parent
$outDir = Join-Path $repo 'app\Nexa.App\Assets\AppIcon'
New-Item -ItemType Directory -Force $outDir | Out-Null

# ── 테마 팔레트 ─────────────────────────────────────────────────────
function C([string]$argb) { [System.Drawing.Color]::FromArgb([Convert]::ToInt32($argb, 16)) }
$palettes = @{
    # 기본 세트 — 다크 슬레이트 폴더(앞판=터미널 화면처럼 어둡게) + 밝은 초록
    dark = @{
        Prefix   = 'nexa-dir'
        Edge     = C 'FF3D8BFF'   # 실루엣 테두리(accent)
        Back     = C 'FF44536C'   # 뒤판·탭
        FrontTop = C 'FF2A3344'   # 앞판 그라디언트
        FrontBot = C 'FF1C222E'
        Green    = C 'FF2EDC5C'   # >_ 프롬프트
    }
    # 라이트 세트 — 밝은 폴더(연한 파랑 톤) + 대비용 진한 초록
    light = @{
        Prefix   = 'nexa-dir-light'
        Edge     = C 'FF3D8BFF'
        Back     = C 'FFB9CCE8'
        FrontTop = C 'FFF3F7FD'
        FrontBot = C 'FFDDE7F4'
        Green    = C 'FF17B04A'
    }
}

function RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

# ── 1024 마스터 렌더(팔레트 주입) ──────────────────────────────────
function RenderMaster($pal) {
    $master = New-Object System.Drawing.Bitmap(1024, 1024)   # 배경 투명
    $g = [System.Drawing.Graphics]::FromImage($master)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

    # 실루엣 테두리: 탭·본체 외곽을 펜으로 먼저 긋고 위에 채움 —
    # 채움이 안쪽 절반을 덮어 바깥쪽 절반만 테두리로 남는다(이음새 없음).
    $edgePen = New-Object System.Drawing.Pen($pal.Edge, 34)
    $edgePen.LineJoin = 'Round'
    $g.DrawPath($edgePen, (RoundRect 40 96 400 200 56))
    $g.DrawPath($edgePen, (RoundRect 40 176 944 728 72))

    # 폴더: 뒤판(탭 포함) + 앞판(2톤 그라디언트)
    $backBrush = New-Object System.Drawing.SolidBrush($pal.Back)
    $g.FillPath($backBrush, (RoundRect 40 96 400 200 56))      # 탭(좌상)
    $g.FillPath($backBrush, (RoundRect 40 176 944 728 72))     # 본체 뒤판
    $frontBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Point(0, 280)), (New-Object System.Drawing.Point(0, 904)), $pal.FrontTop, $pal.FrontBot)
    $g.FillPath($frontBrush, (RoundRect 40 280 944 624 72))    # 앞판

    # 초록 >_ (대형, 앞판 중앙)
    $chevPen = New-Object System.Drawing.Pen($pal.Green, 96)
    $chevPen.StartCap = 'Round'; $chevPen.EndCap = 'Round'; $chevPen.LineJoin = 'Round'
    $g.DrawLines($chevPen, [System.Drawing.Point[]]@(
        (New-Object System.Drawing.Point(310, 452)), (New-Object System.Drawing.Point(490, 592)), (New-Object System.Drawing.Point(310, 732))))
    $g.FillPath((New-Object System.Drawing.SolidBrush($pal.Green)), (RoundRect 560 686 230 92 46))   # _

    $g.Dispose()
    return $master
}

# ── PNG 세트(고품질 다운스케일) + ICO ───────────────────────────────
function ResizeTo([System.Drawing.Bitmap]$src, [int]$s) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s)
    $gg = [System.Drawing.Graphics]::FromImage($bmp)
    $gg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $gg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $gg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $attr = New-Object System.Drawing.Imaging.ImageAttributes
    $attr.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)   # 가장자리 번짐 방지
    $rect = New-Object System.Drawing.Rectangle(0, 0, $s, $s)
    $gg.DrawImage($src, $rect, 0, 0, $src.Width, $src.Height, 'Pixel', $attr)
    $gg.Dispose()
    return $bmp
}
function PngBytes([System.Drawing.Bitmap]$bmp) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    return , $ms.ToArray()   # NoEnumerate — byte[]를 개별 바이트로 풀지 않게(ICO 블롭 유실 방지)
}

function EmitSet($pal) {
    $master = RenderMaster $pal
    $prefix = $pal.Prefix

    # PNG는 1024 마스터만 보존(향후 다른 크기 재생성용) — 실사용은 ICO뿐(csproj ApplicationIcon + SetIcon).
    [System.IO.File]::WriteAllBytes((Join-Path $outDir "$prefix-1024.png"), (PngBytes $master))

    # ICO(16~256, PNG 압축 엔트리 — Vista+ 표준)
    $icoSizes = 16, 24, 32, 48, 64, 128, 256
    $blobs = [System.Collections.Generic.List[byte[]]]::new()
    foreach ($s in $icoSizes) {
        $bmp = ResizeTo $master $s
        [byte[]]$bytes = PngBytes $bmp
        $bmp.Dispose()
        $blobs.Add($bytes)
    }
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)
    $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$icoSizes.Count)   # ICONDIR
    $offset = 6 + 16 * $icoSizes.Count
    for ($i = 0; $i -lt $icoSizes.Count; $i++) {
        $s = $icoSizes[$i]
        $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))   # 256은 0으로 표기
        $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
        $bw.Write([byte]0); $bw.Write([byte]0)
        $bw.Write([uint16]1); $bw.Write([uint16]32)
        $bw.Write([uint32]$blobs[$i].Length)
        $bw.Write([uint32]$offset)
        $offset += $blobs[$i].Length
    }
    foreach ($b in $blobs) { $bw.Write($b) }
    $bw.Flush()
    [System.IO.File]::WriteAllBytes((Join-Path $outDir "$prefix.ico"), $ms.ToArray())
    $master.Dispose()

    Write-Host "[$prefix] 아이콘 생성 완료 → $outDir ($prefix-1024.png + $prefix.ico $($icoSizes.Count)엔트리)"
}

$themes = if ($Theme -eq 'all') { 'dark', 'light' } else { , $Theme }
foreach ($t in $themes) { EmitSet $palettes[$t] }
