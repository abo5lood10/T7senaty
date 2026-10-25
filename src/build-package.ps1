# يجهّز مجلد نظيف + zip للإرسال لصديق: Desktop\T7senaty-Share\T7senaty  و  T7senaty.zip
$ErrorActionPreference = 'Stop'
$src = $PSScriptRoot
$app = Split-Path $src -Parent
$out = Join-Path ([Environment]::GetFolderPath('Desktop')) 'T7senaty-Share'
$pkg = Join-Path $out 'T7senaty'
$zip = Join-Path $out 'T7senaty.zip'

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force $pkg | Out-Null
$utf8bom = New-Object Text.UTF8Encoding $true; $utf8 = New-Object Text.UTF8Encoding $false; $ascii = New-Object Text.ASCIIEncoding

# البرنامج + المثبّت
Copy-Item "$app\T7senaty.exe" $pkg
Copy-Item "$src\install.ps1" $pkg
[IO.File]::WriteAllText("$pkg\install.ps1", [IO.File]::ReadAllText("$src\install.ps1"), $utf8bom)
[IO.File]::WriteAllText("$pkg\تثبيت.bat", "@echo off`r`nstart `"`" powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"%~dp0install.ps1`"`r`n", $ascii)
[IO.File]::WriteAllText("$pkg\تشغيل.bat", "@echo off`r`ncd /d `"%~dp0`"`r`nstart `"`" `"%~dp0T7senaty.exe`" --session --launch`r`nexit`r`n", $ascii)

# Magpie (بدون logs / cache، والإعدادات قالب يعدّله المثبّت)
$mg = "$pkg\Magpie"; New-Item -ItemType Directory -Force "$mg\config" | Out-Null
foreach ($f in 'Magpie.exe', 'Microsoft.UI.Xaml.dll', 'resources.pri', 'TouchHelper.exe', 'Updater.exe') { Copy-Item "$app\Magpie\$f" $mg }
Copy-Item "$app\Magpie\effects" "$mg\effects" -Recurse
Copy-Item "$app\Magpie\config\config.json" "$mg\config\config.json"

# الكود المصدري
$s2 = "$pkg\src"; New-Item -ItemType Directory -Force $s2 | Out-Null
foreach ($f in 'T7senaty.cs', 'build.ps1', 'make-icon.ps1', 'T7senaty.ico', 'nfl-logo-source.png') { Copy-Item "$src\$f" $s2 }

# خط الرجعة (القيم الأصلية تُلتقط عند التثبيت على جهاز الصديق)
$bb = "$pkg\backBB"; New-Item -ItemType Directory -Force $bb | Out-Null
Copy-Item "$app\backBB\rollback.ps1" $bb
foreach ($f in Get-ChildItem "$app\backBB" -Filter *.bat) { Copy-Item $f.FullName $bb }
Copy-Item "$app\backBB\اقرأني.txt" $bb

$readme = @'
T7senaty — تحسين مشاهدة البث على محاكي MuMu Player
=====================================================

المطلوب:
  - ويندوز 10 أو 11
  - MuMu Player 12 مثبّت (ومشغّل مرة وحدة على الأقل)
  - كرت شاشة معقول (كرت قوي = أفضل نتيجة)

التثبيت (مرة وحدة):
  1) فك ضغط المجلد كامل في مكان ثابت (مثل سطح المكتب أو D:\)  -  لا تشغّله من داخل ملف zip
  2) دبل كليك على:  تثبيت.bat
     يلقى MuMu تلقائياً، يحفظ إعداداتك الأصلية، يضبط الجودة، وينشئ اختصار T7senaty على سطح المكتب
  3) لو طلع تحذير من ويندوز (SmartScreen): اضغط  More info  ثم  Run anyway
     (البرنامج غير موقّع رقمياً لأنه مسوّى بشكل شخصي، وكوده كامل داخل المجلد src)

الاستخدام:
  شغّل الاختصار  T7senaty  من سطح المكتب -> يشغّل المحاكي + كل التحسينات
  داخل المحاكي:  F11 = شاشة كاملة محسّنة بالذكاء الاصطناعي  (F11 مرة ثانية = رجوع)
  لما تقفل المحاكي، كل شي يتقفل ويرجع طبيعي تلقائياً.
  البرنامج ما يشتغل مع ويندوز ولا لحاله أبداً.

وش يسوي (وقت تشغيل المحاكي فقط):
  - أولوية عالية للمحاكي، وللأداة اللي تكبّر الصورة
  - يخفي الماوس بعد ثانية بدون حركة
  - يمنع انطفاء الشاشة
  - دقة المحاكي 1600x900 وإخفاء عداد الفريمات
  - تحسين الصورة بالذكاء الاصطناعي (Magpie + FSRCNNX) عند F11
  - كليك يمين على أيقونة T7senaty (جنب الساعة): تغيير نمط التحسين، فحص النت

ملاحظات:
  - ما فيه برنامج يعطي المحاكي سرعة أكبر من سرعة النت. لو البث يقطّع والنت سليم، غيّر السيرفر من داخل التطبيق.
  - التحسين يخلي الصورة أنظف وأحد، مو 4K حقيقي. جودة البث نفسه هي الحد الأعلى.
  - النمط الافتراضي هو "أقصى جودة" (شبكة CuNNy 8x32 الأكبر في Magpie). أول F11 في حياتك يتأخر
    حوالي 10 ثواني لأنه يجهّز الفلتر مرة وحدة (يتخزّن بعدها وما يتكرر).
  - لو كرتك ضعيف وحسّيت ثقل أو تقطيع: كليك يمين على الأيقونة > نمط تحسين الصورة >
    "ذكاء اصطناعي + ألوان أقوى" (أخف) أو "FSR" (الأخف).
  - التصميم مضبوط لشاشة بمقياس 100%. لو مقياس شاشتك أكبر (125% مثلاً) ممكن تحتاج تعدّل
    قص الشريط العلوي من Magpie\config\config.json (الحقل  top  داخل  cropping).

الرجوع الكامل (يحذف البرنامج ويرجّع كل شي مثل قبل):
  افتح المجلد backBB وشغّل:  رجوع كامل (حذف T7senaty).bat
  لو اختفى الماوس: شغّل  إرجاع الماوس (طوارئ).bat  أو سجّل خروج وادخل.

الملفات:
  T7senaty.exe        البرنامج        |  settings.ini   الإعدادات (تنكتب عند التثبيت)
  Magpie\             أداة تكبير الصورة (v0.13.0)  -  مجاني ومفتوح المصدر GPL-3.0
                      https://github.com/Blinue/Magpie   (من تطوير Blinue)
  src\                الكود المصدري لـ T7senaty (C#)  +  build.ps1 لإعادة البناء
  backBB\             خط الرجعة
'@
[IO.File]::WriteAllText("$pkg\اقرأني.txt", $readme, $utf8)

Compress-Archive -Path $pkg -DestinationPath $zip -CompressionLevel Optimal
"package: $pkg"
"zip: $zip  ($([math]::Round((Get-Item $zip).Length/1MB,1)) MB)"
Get-ChildItem $pkg -Recurse -File | Where-Object { $_.FullName -notmatch '\\effects\\' } | ForEach-Object { $_.FullName.Replace("$pkg\", '') }
