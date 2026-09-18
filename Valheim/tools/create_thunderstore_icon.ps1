param(
    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$bitmap = [System.Drawing.Bitmap]::new(256, 256)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
try {
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::FromArgb(27, 35, 43))

    $shadow = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(16, 20, 24))
    $wood = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(126, 71, 37))
    $woodLight = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(174, 111, 58))
    $woodDark = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(77, 42, 25))
    $metal = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(214, 170, 77))
    $raven = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(48, 69, 84))
    $ravenLight = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(67, 93, 109))
    $ravenOutline = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(13, 20, 27), 5)
    $eye = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(62, 221, 255))
    $outline = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(43, 24, 16), 8)
    $line = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(82, 45, 24), 5)
    try {
        # Hugin, perched behind the chest: spread wings, a hooked beak and a blue eye.
        $huginState = $graphics.Save()
        $graphics.TranslateTransform(0, -9)
        $leftWing = [System.Drawing.Point[]]@(
            [System.Drawing.Point]::new(22, 110), [System.Drawing.Point]::new(43, 49),
            [System.Drawing.Point]::new(75, 78), [System.Drawing.Point]::new(92, 25),
            [System.Drawing.Point]::new(114, 89), [System.Drawing.Point]::new(121, 121))
        $rightWing = [System.Drawing.Point[]]@(
            [System.Drawing.Point]::new(130, 113), [System.Drawing.Point]::new(153, 49),
            [System.Drawing.Point]::new(174, 82), [System.Drawing.Point]::new(218, 47),
            [System.Drawing.Point]::new(195, 114), [System.Drawing.Point]::new(161, 127))
        $graphics.FillPolygon($raven, $leftWing)
        $graphics.DrawPolygon($ravenOutline, $leftWing)
        $graphics.FillPolygon($raven, $rightWing)
        $graphics.DrawPolygon($ravenOutline, $rightWing)
        # Hugin's angular raven head: crown feathers, swept cheeks, narrow eyes and a long beak.
        $head = [System.Drawing.Point[]]@(
            [System.Drawing.Point]::new(82, 42), [System.Drawing.Point]::new(96, 16),
            [System.Drawing.Point]::new(111, 5), [System.Drawing.Point]::new(128, 17),
            [System.Drawing.Point]::new(145, 5), [System.Drawing.Point]::new(162, 18),
            [System.Drawing.Point]::new(176, 42), [System.Drawing.Point]::new(169, 69),
            [System.Drawing.Point]::new(153, 88), [System.Drawing.Point]::new(128, 99),
            [System.Drawing.Point]::new(103, 88), [System.Drawing.Point]::new(87, 69))
        $graphics.FillPolygon($ravenLight, $head)
        $graphics.DrawPolygon($ravenOutline, $head)
        $graphics.FillPolygon($raven, [System.Drawing.Point[]]@(
            [System.Drawing.Point]::new(89, 41), [System.Drawing.Point]::new(108, 22),
            [System.Drawing.Point]::new(124, 37), [System.Drawing.Point]::new(128, 69),
            [System.Drawing.Point]::new(101, 65)))
        $graphics.FillPolygon($raven, [System.Drawing.Point[]]@(
            [System.Drawing.Point]::new(167, 41), [System.Drawing.Point]::new(148, 22),
            [System.Drawing.Point]::new(132, 37), [System.Drawing.Point]::new(128, 69),
            [System.Drawing.Point]::new(155, 65)))
        $graphics.FillPolygon($eye, [System.Drawing.Point[]]@(
            [System.Drawing.Point]::new(103, 47), [System.Drawing.Point]::new(117, 43),
            [System.Drawing.Point]::new(113, 53), [System.Drawing.Point]::new(101, 55)))
        $graphics.FillPolygon($eye, [System.Drawing.Point[]]@(
            [System.Drawing.Point]::new(153, 43), [System.Drawing.Point]::new(167, 47),
            [System.Drawing.Point]::new(155, 55), [System.Drawing.Point]::new(143, 53)))
        $graphics.FillPolygon($shadow, [System.Drawing.Point[]]@(
            [System.Drawing.Point]::new(128, 54), [System.Drawing.Point]::new(140, 77),
            [System.Drawing.Point]::new(128, 112), [System.Drawing.Point]::new(116, 77)))
        $graphics.Restore($huginState)

        $chestState = $graphics.Save()
        $graphics.TranslateTransform(0, 20)
        $graphics.FillEllipse($shadow, 35, 194, 186, 30)
        $graphics.FillRectangle($woodDark, 42, 93, 172, 107)
        $graphics.FillRectangle($wood, 47, 75, 162, 54)
        $graphics.FillRectangle($woodLight, 51, 80, 154, 19)
        $graphics.DrawRectangle($outline, 43, 76, 170, 123)
        $graphics.DrawLine($line, 45, 130, 211, 130)
        $graphics.DrawLine($line, 85, 79, 85, 198)
        $graphics.DrawLine($line, 171, 79, 171, 198)
        $graphics.FillRectangle($metal, 108, 121, 40, 31)
        $graphics.FillRectangle($woodDark, 115, 127, 26, 18)
        $graphics.DrawEllipse($outline, 85, 42, 86, 86)
        $graphics.FillEllipse($metal, 93, 50, 70, 70)
        $graphics.FillEllipse($woodDark, 102, 59, 52, 52)
        $graphics.DrawLine([System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(214, 170, 77), 7), 110, 84, 146, 84)
        $graphics.DrawLine([System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(214, 170, 77), 7), 128, 66, 128, 102)
        $graphics.Restore($chestState)
    }
    finally {
        $shadow.Dispose(); $wood.Dispose(); $woodLight.Dispose(); $woodDark.Dispose(); $metal.Dispose()
        $raven.Dispose(); $ravenLight.Dispose(); $ravenOutline.Dispose(); $eye.Dispose()
        $outline.Dispose(); $line.Dispose()
    }

    $directory = Split-Path -Parent $OutputPath
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    $bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
}
finally {
    $graphics.Dispose()
    $bitmap.Dispose()
}
