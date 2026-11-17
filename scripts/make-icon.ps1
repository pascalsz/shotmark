# Erzeugt src/ShotMark.App/Assets/ShotMark.ico (16/24/32/48/64/256 px, PNG-komprimiert).
Add-Type -AssemblyName System.Drawing
$sizes = 16, 24, 32, 48, 64, 256
$pngs = @()
foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $r = [single]($s * 0.22)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $w = [single]($s - 1)
    $path.AddArc(0, 0, $r * 2, $r * 2, 180, 90)
    $path.AddArc($w - $r * 2, 0, $r * 2, $r * 2, 270, 90)
    $path.AddArc($w - $r * 2, $w - $r * 2, $r * 2, $r * 2, 0, 90)
    $path.AddArc(0, $w - $r * 2, $r * 2, $r * 2, 90, 90)
    $path.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0xFC, 0x42, 0x42))), $path)
    # weiße Auswahl-Ecken
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([single][Math]::Max(1.5, $s * 0.09))
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
    $a = [single]($s * 0.24); $b = [single]($s * 0.76); $l = [single]($s * 0.18)
    $g.DrawLines($pen, [System.Drawing.PointF[]]@((New-Object System.Drawing.PointF $a, ($a + $l)), (New-Object System.Drawing.PointF $a, $a), (New-Object System.Drawing.PointF ($a + $l), $a)))
    $g.DrawLines($pen, [System.Drawing.PointF[]]@((New-Object System.Drawing.PointF ($b - $l), $a), (New-Object System.Drawing.PointF $b, $a), (New-Object System.Drawing.PointF $b, ($a + $l))))
    $g.DrawLines($pen, [System.Drawing.PointF[]]@((New-Object System.Drawing.PointF $b, ($b - $l)), (New-Object System.Drawing.PointF $b, $b), (New-Object System.Drawing.PointF ($b - $l), $b)))
    $g.DrawLines($pen, [System.Drawing.PointF[]]@((New-Object System.Drawing.PointF ($a + $l), $b), (New-Object System.Drawing.PointF $a, $b), (New-Object System.Drawing.PointF $a, ($b - $l))))
    # kleiner Haken in der Mitte
    $pen2 = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([single][Math]::Max(1.5, $s * 0.1))
    $pen2.StartCap = 'Round'; $pen2.EndCap = 'Round'; $pen2.LineJoin = 'Round'
    $g.DrawLines($pen2, [System.Drawing.PointF[]]@((New-Object System.Drawing.PointF ($s * 0.38), ($s * 0.51)), (New-Object System.Drawing.PointF ($s * 0.47), ($s * 0.60)), (New-Object System.Drawing.PointF ($s * 0.63), ($s * 0.42))))
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += ,($ms.ToArray())
    $bmp.Dispose()
}
$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $out
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $d = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$d); $bw.Write([byte]$d); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$pngs[$i].Length); $bw.Write([UInt32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Flush()
$target = Join-Path $PSScriptRoot '..\src\ShotMark.App\Assets\ShotMark.ico'
[System.IO.File]::WriteAllBytes($target, $out.ToArray())
# Vorschau
[System.IO.File]::WriteAllBytes((Join-Path $PSScriptRoot '..\src\ShotMark.App\Assets\ShotMark-256.png'), $pngs[-1])
Write-Output "ok $target"
