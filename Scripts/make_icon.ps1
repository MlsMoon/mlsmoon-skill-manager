$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root "assets"
New-Item -ItemType Directory -Force -Path $assets | Out-Null
$pngPath = Join-Path $assets "logo.png"
$icoPath = Join-Path $assets "app.ico"

$bmp = New-Object System.Drawing.Bitmap 256, 256
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::FromArgb(18, 20, 26))
$accent = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(124, 156, 255))
$bg = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(18, 20, 26))
$g.FillEllipse($accent, 28, 28, 200, 200)
$g.FillEllipse($bg, 78, 18, 168, 168)
$pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(61, 220, 151), 10)
$g.DrawEllipse($pen, 108, 108, 40, 40)
$g.DrawLine($pen, 148, 128, 188, 128)
$g.Dispose()
$bmp.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)

$ms = New-Object System.IO.MemoryStream
$bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
$png = $ms.ToArray()
$bmp.Dispose()

$stream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $stream
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]1)
$writer.Write([byte]0)
$writer.Write([byte]0)
$writer.Write([byte]0)
$writer.Write([byte]0)
$writer.Write([uint16]1)
$writer.Write([uint16]32)
$writer.Write([int32]$png.Length)
$writer.Write([int32]22)
$writer.Write($png)
[System.IO.File]::WriteAllBytes($icoPath, $stream.ToArray())
$writer.Dispose()
$stream.Dispose()
Write-Host "wrote $pngPath"
Write-Host "wrote $icoPath"
