# Generates Assets\TidalDiscord.ico (32-bit BMP frames: 256 + 32 px).
# Re-run any time you want to regenerate the placeholder icon,
# or just drop your own TidalDiscord.ico into Assets\.

Add-Type -AssemblyName System.Drawing

function New-IconBitmap([int]$size)
{
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $margin = [int]($size * 0.04)
    $radius = [int]($size * 0.22)
    $inner = $size - (2 * $margin)
    $rect = New-Object System.Drawing.Rectangle(
        $margin, $margin, $inner, $inner)

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($rect.X, $rect.Y, $radius, $radius, 180, 90)
    $path.AddArc($rect.Right - $radius, $rect.Y, $radius, $radius, 270, 90)
    $path.AddArc($rect.Right - $radius, $rect.Bottom - $radius, $radius, $radius, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $radius, $radius, $radius, 90, 90)
    $path.CloseFigure()

    $bg = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 10, 10, 14))
    $g.FillPath($bg, $path)

    $accent = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0, 220, 231))
    $barW = [int]($size * 0.42)
    $barH = [Math]::Max(2, [int]($size * 0.045))
    $g.FillRectangle($accent,
        [int](($size - $barW) / 2),
        [int]($size * 0.70),
        $barW, $barH)

    $fontSize = $size * 0.52
    $font = New-Object System.Drawing.Font(
        "Segoe UI", $fontSize, [System.Drawing.FontStyle]::Bold,
        [System.Drawing.GraphicsUnit]::Pixel)
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center
    $textRect = New-Object System.Drawing.RectangleF(
        0, [float](-$size * 0.05), $size, $size)
    $g.DrawString("T", $font, [System.Drawing.Brushes]::White, $textRect, $format)

    $g.Dispose(); $font.Dispose(); $bg.Dispose()
    $accent.Dispose(); $path.Dispose(); $format.Dispose()

    return $bmp
}

# Returns the DIB section of an ICO frame: BITMAPINFOHEADER +
# bottom-up BGRA pixels + zeroed AND mask.
function Get-IcoFrame([System.Drawing.Bitmap]$bmp)
{
    $size = $bmp.Width
    $rect = New-Object System.Drawing.Rectangle(0, 0, $size, $size)
    $data = $bmp.LockBits(
        $rect,
        [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    $pixels = New-Object byte[] ($size * $size * 4)
    [System.Runtime.InteropServices.Marshal]::Copy(
        $data.Scan0, $pixels, 0, $pixels.Length)
    $bmp.UnlockBits($data)
    $bmp.Dispose()

    $maskStride = (($size + 31) -shr 5) * 4
    $maskSize = $maskStride * $size

    $ms = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter $ms

    $w.Write([uint32]40)                    # biSize
    $w.Write([int32]$size)                  # biWidth
    $w.Write([int32]($size * 2))            # biHeight (XOR + AND)
    $w.Write([uint16]1)                     # biPlanes
    $w.Write([uint16]32)                    # biBitCount
    $w.Write([uint32]0)                     # biCompression = BI_RGB
    $w.Write([uint32]($pixels.Length + $maskSize))
    $w.Write([int32]0)                      # biXPelsPerMeter
    $w.Write([int32]0)                      # biYPelsPerMeter
    $w.Write([uint32]0)                     # biClrUsed
    $w.Write([uint32]0)                     # biClrImportant

    # DIBs are stored bottom-up.
    $rowBytes = $size * 4
    for ($y = $size - 1; $y -ge 0; $y--)
    {
        $w.Write($pixels, $y * $rowBytes, $rowBytes)
    }

    $w.Write((New-Object byte[] $maskSize))

    $result = $ms.ToArray()
    $w.Dispose(); $ms.Dispose()
    return $result
}

$frames = @(
    @{ Bytes = [byte[]](Get-IcoFrame (New-IconBitmap 256)); Size = [byte]0 },
    @{ Bytes = [byte[]](Get-IcoFrame (New-IconBitmap 32));  Size = [byte]32 }
)

$ms = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $ms

$w.Write([uint16]0)                      # reserved
$w.Write([uint16]1)                      # type: icon
$w.Write([uint16]$frames.Count)          # frame count

$offset = 6 + 16 * $frames.Count
foreach ($frame in $frames)
{
    $w.Write($frame.Size)                # width  (0 = 256)
    $w.Write($frame.Size)                # height
    $w.Write([byte]0)                    # palette
    $w.Write([byte]0)                    # reserved
    $w.Write([uint16]1)                  # planes
    $w.Write([uint16]32)                 # bpp
    $w.Write([uint32]$frame.Bytes.Length)
    $w.Write([uint32]$offset)
    $offset += $frame.Bytes.Length
}

foreach ($frame in $frames)
{
    $w.Write($frame.Bytes)
}

$outDir = Join-Path $PSScriptRoot "Assets"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$outPath = Join-Path $outDir "TidalDiscord.ico"
[IO.File]::WriteAllBytes($outPath, $ms.ToArray())

$w.Dispose(); $ms.Dispose()

Write-Host "Wrote $outPath"
