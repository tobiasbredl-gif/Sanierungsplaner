# Reproducible Windows PNG/ICO from the same 108-unit geometry as the Android vectors.
param([string]$Repository=(Split-Path $PSScriptRoot -Parent))
Add-Type -AssemblyName System.Drawing
function Render-Icon([int]$Size){
 $bitmap=[Drawing.Bitmap]::new($Size,$Size)
 $g=[Drawing.Graphics]::FromImage($bitmap)
 $g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
 $g.Clear([Drawing.Color]::Transparent)
 $g.ScaleTransform($Size/108.0,$Size/108.0)
 $round=[Drawing.Drawing2D.GraphicsPath]::new()
 $round.AddArc(0,0,48,48,180,90);$round.AddArc(60,0,48,48,270,90);$round.AddArc(60,60,48,48,0,90);$round.AddArc(0,60,48,48,90,90);$round.CloseFigure()
 $bg=[Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.Point]::new(0,0),[Drawing.Point]::new(108,108),[Drawing.ColorTranslator]::FromHtml('#236B57'),[Drawing.ColorTranslator]::FromHtml('#103D32'))
 $g.FillPath($bg,$round)
 $cream=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#F5F2E8'))
 $dark=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#103D32'))
 $roof=[Drawing.Pen]::new($cream,6);$roof.StartCap='Round';$roof.EndCap='Round';$roof.LineJoin='Round'
 $g.DrawLines($roof,[Drawing.PointF[]]@([Drawing.PointF]::new(28,49),[Drawing.PointF]::new(54,27),[Drawing.PointF]::new(80,49)))
 $g.FillRectangle($cream,34,48,33,29)
 $line=[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#236B57'),4);$line.StartCap='Round';$line.EndCap='Round'
 $g.DrawLine($line,43,54,56,54);$g.DrawLine($line,43,63,52,63)
 $g.FillEllipse($dark,60,59,30,30)
 $gold=[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#E8BC6B'),6);$gold.StartCap='Round';$gold.EndCap='Round';$gold.LineJoin='Round'
 $g.DrawLines($gold,[Drawing.PointF[]]@([Drawing.PointF]::new(66,74),[Drawing.PointF]::new(72,80),[Drawing.PointF]::new(84,66)))
 $g.Dispose();$round.Dispose();$bg.Dispose();$cream.Dispose();$dark.Dispose();$roof.Dispose();$line.Dispose();$gold.Dispose()
 return $bitmap
}
$preview=Render-Icon 512
$preview.Save((Join-Path $Repository 'assets/app-icon.png'),[Drawing.Imaging.ImageFormat]::Png);$preview.Dispose()
$sizes=@(16,24,32,48,64,128,256)
$images=@()
foreach($size in $sizes){$bitmap=Render-Icon $size;$memory=[IO.MemoryStream]::new();$bitmap.Save($memory,[Drawing.Imaging.ImageFormat]::Png);$images+=,@($memory.ToArray());$memory.Dispose();$bitmap.Dispose()}
$stream=[IO.File]::Create((Join-Path $Repository 'src/Sanierungsplaner.Desktop/Assets/app.ico'))
$writer=[IO.BinaryWriter]::new($stream)
$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$sizes.Count)
$offset=6+16*$sizes.Count
for($i=0;$i -lt $sizes.Count;$i++){$dimension=if($sizes[$i] -eq 256){0}else{$sizes[$i]};$writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$images[$i].Count);$writer.Write([uint32]$offset);$offset+=$images[$i].Count}
foreach($bytes in $images){$writer.Write([byte[]]$bytes)}
$writer.Dispose();$stream.Dispose()
