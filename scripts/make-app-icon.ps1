# make-app-icon.ps1 — Nexa Dir 앱 아이콘 생성기 (GDI+, 외부 도구 불요 · Windows 전용)
#
# 컨셉: "트리형 다중선택 파일탐색기 + 개발자 도구"
#   · 다크 라운드 배경(프로툴/다크 기본, DR-2)
#   · 폴더(루트)에서 뻗는 트리 가이드 + 행 3개 — 그중 2개가 accent로 "다중 선택"됨
#   · 우상단 </> 글리프 — 개발자 지향
# 산출: app/Nexa.App/Assets/AppIcon/nexa-{16..1024}.png + nexa.ico(16~256 멀티사이즈, PNG 엔트리)
# 재실행 안전(덮어씀). 1024px 마스터에서 고품질 다운스케일.

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$repo = Split-Path $PSScriptRoot -Parent
$outDir = Join-Path $repo 'app\Nexa.App\Assets\AppIcon'
New-Item -ItemType Directory -Force $outDir | Out-Null

# ── 색 (앱 accent #3D8BFF 계열) ─────────────────────────────────────
function C([string]$argb) { [System.Drawing.Color]::FromArgb([Convert]::ToInt32($argb, 16)) }
$bgTop      = C 'FF232C3C'
$bgBottom   = C 'FF0D1219'
$accent     = C 'FF3D8BFF'
$accentHi   = C 'FF6EB2FF'
$folderTop  = C 'FF4DA3FF'
$folderBot  = C 'FF2F6FE0'
$treeGray   = C 'FF46536A'
$rowStroke  = C 'FF4A576D'
$rowGray    = C 'FF6A7A93'
$whiteHi    = C 'E8FFFFFF'
$whiteBar   = C 'C8FFFFFF'

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

# ── 1024 마스터 렌더 ────────────────────────────────────────────────
$size = 1024
$master = New-Object System.Drawing.Bitmap($size, $size)
$g = [System.Drawing.Graphics]::FromImage($master)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

# 배경(다크 라운드 사각 + 세로 그라디언트)
$bgPath = RoundRect 0 0 1024 1024 200
$bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.Point(0, 0)), (New-Object System.Drawing.Point(0, 1024)), $bgTop, $bgBottom)
$g.FillPath($bgBrush, $bgPath)

# 폴더(루트) — 탭 + 본체, 파랑 그라디언트
$folderBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.Point(0, 150)), (New-Object System.Drawing.Point(0, 360)), $folderTop, $folderBot)
$tab = RoundRect 150 160 190 90 24
$body = RoundRect 150 212 320 140 32
$g.FillPath($folderBrush, $tab)
$g.FillPath($folderBrush, $body)

# 트리 가이드(세로 줄기 + 가지 3개) — 굵은 라운드 캡
$treePen = New-Object System.Drawing.Pen($treeGray, 22)
$treePen.StartCap = 'Round'; $treePen.EndCap = 'Round'
$g.DrawLine($treePen, 250, 352, 250, 810)
foreach ($y in 470, 640, 810) { $g.DrawLine($treePen, 250, $y, 345, $y) }

# 행 3개(파일/폴더 행) — 1·3=선택(accent 채움), 2=미선택(외곽선)
function DrawRow($g, [float]$top, [bool]$selected, [float]$barW) {
    $row = RoundRect 365 $top 515 120 30
    if ($selected) {
        $g.FillPath((New-Object System.Drawing.SolidBrush($script:accent)), $row)
        $chip = RoundRect 400 ($top + 30) 60 60 14
        $g.FillPath((New-Object System.Drawing.SolidBrush($script:whiteHi)), $chip)
        $bar = RoundRect 490 ($top + 45) $barW 30 15
        $g.FillPath((New-Object System.Drawing.SolidBrush($script:whiteBar)), $bar)
    }
    else {
        $pen = New-Object System.Drawing.Pen($script:rowStroke, 10)
        $g.DrawPath($pen, $row)
        $chip = RoundRect 400 ($top + 30) 60 60 14
        $g.FillPath((New-Object System.Drawing.SolidBrush($script:rowGray)), $chip)
        $bar = RoundRect 490 ($top + 45) ($barW * 0.8) 30 15
        $g.FillPath((New-Object System.Drawing.SolidBrush($script:rowGray)), $bar)
    }
}
DrawRow $g 410 $true  330
DrawRow $g 580 $false 300
DrawRow $g 750 $true  260

# </> 글리프(우상단) — 개발자 지향
$codePen = New-Object System.Drawing.Pen($accentHi, 30)
$codePen.StartCap = 'Round'; $codePen.EndCap = 'Round'
$codePen.LineJoin = 'Round'
$g.DrawLines($codePen, [System.Drawing.Point[]]@(
    (New-Object System.Drawing.Point(700, 180)), (New-Object System.Drawing.Point(645, 247)), (New-Object System.Drawing.Point(700, 314))))
$g.DrawLine($codePen, 795, 168, 742, 326)
$g.DrawLines($codePen, [System.Drawing.Point[]]@(
    (New-Object System.Drawing.Point(852, 180)), (New-Object System.Drawing.Point(907, 247)), (New-Object System.Drawing.Point(852, 314))))

$g.Dispose()

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

$pngSizes = 16, 24, 32, 48, 64, 128, 256, 512, 1024
foreach ($s in $pngSizes) {
    $bmp = if ($s -eq 1024) { $master } else { ResizeTo $master $s }
    [System.IO.File]::WriteAllBytes((Join-Path $outDir "nexa-$s.png"), (PngBytes $bmp))
    if ($s -ne 1024) { $bmp.Dispose() }
}

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
[System.IO.File]::WriteAllBytes((Join-Path $outDir 'nexa.ico'), $ms.ToArray())
$master.Dispose()

Write-Host "아이콘 생성 완료 → $outDir (PNG $($pngSizes.Count)종 + nexa.ico $($icoSizes.Count)엔트리)"
