# يحوّل صورة الشعار (src\nfl-logo-source.png) إلى أيقونة T7senaty.ico بخلفية شفافة وبعدة مقاسات
Add-Type -AssemblyName System.Drawing
$src = $PSScriptRoot
$srcImg = "$src\nfl-logo-source.png"

# 1) إزالة الخلفية المنقّطة: نمسح الرمادي الغامق المتصل بحافة الصورة (الشعار محاط بإطار أبيض فيوقف المسح عنده)
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System; using System.Drawing; using System.Drawing.Imaging; using System.Collections.Generic;
public static class Cut {
  static bool Bg(byte r, byte g, byte b) { int mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b)); return mx < 85 && mx - mn < 22; }
  public static Bitmap Run(Bitmap src) {
    int w = src.Width, h = src.Height;
    var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(bmp)) g.DrawImage(src, 0, 0, w, h);
    var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
    int stride = data.Stride;
    byte[] px = new byte[stride * h];
    System.Runtime.InteropServices.Marshal.Copy(data.Scan0, px, 0, px.Length);
    bool[] gone = new bool[w * h];
    var q = new Queue<int>();
    for (int x = 0; x < w; x++) { Seed(px, stride, gone, q, w, h, x, 0); Seed(px, stride, gone, q, w, h, x, h - 1); }
    for (int y = 0; y < h; y++) { Seed(px, stride, gone, q, w, h, 0, y); Seed(px, stride, gone, q, w, h, w - 1, y); }
    while (q.Count > 0) { int i = q.Dequeue(); int x = i % w, y = i / w; Seed(px, stride, gone, q, w, h, x + 1, y); Seed(px, stride, gone, q, w, h, x - 1, y); Seed(px, stride, gone, q, w, h, x, y + 1); Seed(px, stride, gone, q, w, h, x, y - 1); }
    bool[] edge = new bool[w * h];
    for (int y = 1; y < h - 1; y++) for (int x = 1; x < w - 1; x++) {
      int i = y * w + x; if (gone[i]) continue; int o = y * stride + x * 4;
      int mx = Math.Max(px[o + 2], Math.Max(px[o + 1], px[o])), mn = Math.Min(px[o + 2], Math.Min(px[o + 1], px[o]));
      if (mx < 140 && mx - mn < 30 && (gone[i - 1] || gone[i + 1] || gone[i - w] || gone[i + w])) edge[i] = true;
    }
    for (int i = 0; i < gone.Length; i++) if (gone[i] || edge[i]) { int o = (i / w) * stride + (i % w) * 4; px[o] = 0; px[o + 1] = 0; px[o + 2] = 0; px[o + 3] = 0; }
    System.Runtime.InteropServices.Marshal.Copy(px, 0, data.Scan0, px.Length);
    bmp.UnlockBits(data);
    return bmp;
  }
  static void Seed(byte[] px, int stride, bool[] gone, Queue<int> q, int w, int h, int x, int y) {
    if (x < 0 || y < 0 || x >= w || y >= h) return;
    int i = y * w + x; if (gone[i]) return;
    int o = y * stride + x * 4; if (!Bg(px[o + 2], px[o + 1], px[o])) return;
    gone[i] = true; q.Enqueue(i);
  }
}
"@

$in = [Drawing.Bitmap]::FromFile($srcImg)
$cut = [Cut]::Run($in); $in.Dispose()

# اقتصاص للمحتوى المرئي
$minX = $cut.Width; $minY = $cut.Height; $maxX = 0; $maxY = 0
for ($y = 0; $y -lt $cut.Height; $y++) { for ($x = 0; $x -lt $cut.Width; $x++) { if ($cut.GetPixel($x, $y).A -gt 10) { if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }; if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y } } } }
$cw = $maxX - $minX + 1; $ch = $maxY - $minY + 1
$cut.Save("$src\nfl-logo.png")

function Square([int]$s) {
    $bmp = New-Object Drawing.Bitmap $s, $s, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bmp); $g.Clear([Drawing.Color]::Transparent)
    $g.InterpolationMode = 'HighQualityBicubic'; $g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'
    $pad = [math]::Max(1, [int]($s * 0.03)); $avail = $s - 2 * $pad
    $scale = [math]::Min($avail / $cw, $avail / $ch); $dw = [int]($cw * $scale); $dh = [int]($ch * $scale)
    $g.DrawImage($cut, (New-Object Drawing.Rectangle ([int](($s - $dw) / 2)), ([int](($s - $dh) / 2)), $dw, $dh), $minX, $minY, $cw, $ch, [Drawing.GraphicsUnit]::Pixel)
    $g.Dispose(); return $bmp
}

$sizes = 256, 64, 48, 32, 24, 16
$pngs = foreach ($s in $sizes) { $b = Square $s; $ms = New-Object IO.MemoryStream; $b.Save($ms, [Drawing.Imaging.ImageFormat]::Png); if ($s -eq 256) { $b.Save("$src\icon-preview.png") }; $b.Dispose(); , $ms.ToArray() }
$fs = [IO.File]::Create("$src\T7senaty.ico"); $w = New-Object IO.BinaryWriter $fs
$w.Write([int16]0); $w.Write([int16]1); $w.Write([int16]$sizes.Count)
$off = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) { $d = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }; $w.Write([byte]$d); $w.Write([byte]$d); $w.Write([byte]0); $w.Write([byte]0); $w.Write([int16]1); $w.Write([int16]32); $w.Write([int]$pngs[$i].Length); $w.Write([int]$off); $off += $pngs[$i].Length }
foreach ($p in $pngs) { $w.Write($p) }
$w.Close(); $fs.Close(); $cut.Dispose()
Write-Host "icon written: $src\T7senaty.ico"
