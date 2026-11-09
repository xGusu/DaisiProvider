# Draws the DaisiProvider app icon: a ZIP with an arrow into a folder.
# Writes daisiprovider.ico (16 to 256 px, PNG entries) next to this script
# and, with -PreviewPath, a 256 px PNG for a quick look.
# Run with Windows PowerShell 5.1 (System.Drawing is built in):
#   powershell -NoProfile -File assets\icon\make-icon.ps1
param([string]$PreviewPath)

Add-Type -AssemblyName System.Drawing

function New-RoundedPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function Get-Color([string]$hex) { [System.Drawing.ColorTranslator]::FromHtml($hex) }

# Every shape is laid out on a 256 x 256 grid and scaled to the target size,
# so small sizes are drawn sharp instead of shrunk from a big bitmap.
function New-IconBitmap([int]$size) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.ScaleTransform($size / 256.0, $size / 256.0)

    $navy = New-Object System.Drawing.SolidBrush (Get-Color '#1E293B')
    $amber = New-Object System.Drawing.SolidBrush (Get-Color '#F59E0B')
    $blueBack = New-Object System.Drawing.SolidBrush (Get-Color '#2563EB')
    $blue = New-Object System.Drawing.SolidBrush (Get-Color '#3B82F6')
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)

    # Background tile in the app's menu colour.
    $g.FillPath($navy, (New-RoundedPath 8 8 240 240 44))

    # ZIP: amber block with zipper teeth and a pull tab.
    $g.FillPath($amber, (New-RoundedPath 20 60 68 136 10))
    for ($i = 0; $i -lt 6; $i++) {
        $toothX = if ($i % 2 -eq 0) { 42 } else { 54 }
        $g.FillRectangle($navy, $toothX, 68 + $i * 13, 12, 8)
    }
    $g.FillPath($navy, (New-RoundedPath 43 148 22 34 5))
    $g.FillPath($amber, (New-RoundedPath 49 166 10 10 3))

    # Arrow from the ZIP into the folder.
    $g.FillRectangle($white, 96, 118, 36, 20)
    $head = [System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF(126, 96)),
        (New-Object System.Drawing.PointF(156, 128)),
        (New-Object System.Drawing.PointF(126, 160)))
    $g.FillPolygon($white, $head)

    # Folder: tab behind, body in front.
    $g.FillPath($blueBack, (New-RoundedPath 162 80 36 28 6))
    $g.FillPath($blue, (New-RoundedPath 162 94 78 100 9))

    $g.Dispose()
    return $bitmap
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$images = foreach ($size in $sizes) {
    $bitmap = New-IconBitmap $size
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    if ($size -eq 256 -and $PreviewPath) { $bitmap.Save($PreviewPath, [System.Drawing.Imaging.ImageFormat]::Png) }
    $bitmap.Dispose()
    , $stream.ToArray()
}

# ICO file: 6-byte header, 16-byte entry per size, then the PNG data.
$output = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($output)
$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $edge = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $writer.Write([byte]$edge)
    $writer.Write([byte]$edge)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]32)
    $writer.Write([UInt32]$images[$i].Length)
    $writer.Write([UInt32]$offset)
    $offset += $images[$i].Length
}
foreach ($image in $images) { $writer.Write($image) }
$writer.Flush()

$icoPath = Join-Path $PSScriptRoot 'daisiprovider.ico'
[System.IO.File]::WriteAllBytes($icoPath, $output.ToArray())
Write-Output "Wrote $icoPath ($($output.Length) bytes)"
