# T7senaty - المثبّت
# يلقى مسار MuMu Player، يحفظ إعداداته الأصلية في backBB (عشان الرجوع يرجّع إعدادات هذا الجهاز بالذات)،
# يضبط الإعدادات، وينشئ اختصار T7senaty على سطح المكتب. ما ينسخ شي برا هذا المجلد.
#   -Silent            بدون نوافذ (للاختبار)
#   -MuMuDir <مسار>    تحديد مسار MuMu يدوياً
#   -ShortcutDir <مسار> مكان الاختصار (الافتراضي: سطح المكتب)
param([switch]$Silent, [string]$MuMuDir, [string]$ShortcutDir)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
$AppDir = $PSScriptRoot
$exe = Join-Path $AppDir 'T7senaty.exe'
$bb = Join-Path $AppDir 'backBB'

function Msg([string]$text, [string]$buttons = 'OK', [string]$icon = 'Information') {
    if ($Silent) { Write-Host $text; return 'Yes' }
    $owner = New-Object System.Windows.Forms.Form -Property @{ TopMost = $true; ShowInTaskbar = $false; FormBorderStyle = 'None'; Opacity = 0; StartPosition = 'CenterScreen'; Size = (New-Object System.Drawing.Size 1, 1) }
    $owner.Show(); $owner.Activate()
    try { [System.Windows.Forms.MessageBox]::Show($owner, $text, 'T7senaty - التثبيت', $buttons, $icon, 'Button1', 'RtlReading, RightAlign') } finally { $owner.Dispose() }
}
function Fail([string]$text) { Msg $text 'OK' 'Error' | Out-Null; exit 1 }

try {
    if (-not (Test-Path $exe)) { Fail 'ما لقيت T7senaty.exe. فك ضغط المجلد كامل أول، وشغّل التثبيت من داخله (مو من داخل ملف zip).' }
    if ($AppDir -match '\\Temp\\|\\Rar\$|\.zip') { Fail 'فك ضغط المجلد كامل في مكان ثابت (مثل سطح المكتب أو D:\) وبعدين شغّل التثبيت.' }

    # ---------- 1) مسار MuMu ----------
    function Test-MuMu([string]$d) { $d -and (Test-Path (Join-Path $d 'nx_main\MuMuManager.exe')) }
    $dir = $null
    if (Test-MuMu $MuMuDir) { $dir = $MuMuDir }
    if (-not $dir) {
        $cands = New-Object System.Collections.Generic.List[string]
        foreach ($proc in 'MuMuNxMain', 'MuMuNxDevice') { Get-Process $proc -ErrorAction SilentlyContinue | ForEach-Object { try { $p = $_.Path } catch { $p = $null }; if ($p) { $cands.Add((Split-Path (Split-Path $p -Parent) -Parent)); $cands.Add((Split-Path $p -Parent)) } } }
        foreach ($k in 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*', 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*', 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*') {
            Get-ItemProperty $k -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -like '*MuMu*' } | ForEach-Object { if ($_.InstallLocation) { $cands.Add($_.InstallLocation.TrimEnd('\')) }; if ($_.UninstallString) { $cands.Add((Split-Path ($_.UninstallString.Trim('"')) -Parent)) } }
        }
        foreach ($d in 'C:\Program Files\Netease\MuMuPlayer', 'C:\Program Files\Netease\MuMuPlayerGlobal', 'C:\Program Files\Netease\MuMu Player 12', 'D:\Program Files\Netease\MuMuPlayer', 'E:\Program Files\Netease\MuMuPlayer') { $cands.Add($d) }
        $dir = $cands | Where-Object { Test-MuMu $_ } | Select-Object -First 1
    }
    if (-not $dir -and -not $Silent) {
        Msg "ما لقيت MuMu Player تلقائياً.`nاختر مجلد التثبيت (اللي فيه المجلد nx_main)." | Out-Null
        $fb = New-Object System.Windows.Forms.FolderBrowserDialog; $fb.Description = 'اختر مجلد MuMu Player (فيه nx_main)'
        if ($fb.ShowDialog() -eq 'OK' -and (Test-MuMu $fb.SelectedPath)) { $dir = $fb.SelectedPath }
    }
    if (-not $dir) { Fail 'ما لقيت MuMu Player. تأكد إنه مثبّت (النسخة 12) وشغّله مرة وحدة على الأقل، وبعدها أعد التثبيت.' }
    $mgr = Join-Path $dir 'nx_main\MuMuManager.exe'

    # ---------- 2) المحاكي لازم يكون مقفل ----------
    $devs = 'MuMuNxDevice', 'MuMuNxHeadless', 'MuMuPlayer', 'MuMuVMMHeadless'
    if (Get-Process $devs -ErrorAction SilentlyContinue) {
        if ((Msg "المحاكي شغّال الحين.`nلازم يتقفل عشان تنضبط إعداداته.`n`nأقفله لك وأكمل؟" 'YesNo' 'Question') -ne 'Yes') { exit 0 }
        & $mgr control -v all shutdown | Out-Null
        for ($i = 0; $i -lt 60 -and (Get-Process $devs -ErrorAction SilentlyContinue); $i++) { Start-Sleep 1 }
        if (Get-Process $devs -ErrorAction SilentlyContinue) { Fail 'ما قدرت أقفل المحاكي. اقفله بيدك وشغّل التثبيت مرة ثانية.' }
        Start-Sleep 3
    }

    # ---------- 3) الإعدادات الأصلية (قبل أي تعديل) ----------
    $info = & $mgr info -v all | Out-String | ConvertFrom-Json
    $vm = @($info.PSObject.Properties | ForEach-Object { $_.Name })[0]
    if ($null -eq $vm) { Fail 'ما لقيت جهاز أندرويد داخل MuMu. افتح MuMu وسوّ جهاز أول، وبعدين أعد التثبيت.' }
    $cur = & $mgr setting -v $vm -k resolution_mode -k show_frame_rate | Out-String | ConvertFrom-Json

    $shellCfgs = @(Get-ChildItem (Join-Path $dir 'vms\*\configs\shell_config.json') -ErrorAction SilentlyContinue)
    $custCfgs = @(Get-ChildItem (Join-Path $dir 'vms\*\configs\customer_config.json') -ErrorAction SilentlyContinue)
    function V([string]$text, [string]$pattern) { [regex]::Match($text, $pattern).Groups[1].Value }

    $firstTime = -not (Test-Path (Join-Path $bb 'original-values.json'))
    if ($firstTime) {
        New-Item -ItemType Directory -Force (Join-Path $bb 'original-files') | Out-Null
        foreach ($f in @($shellCfgs) + @($custCfgs)) { Copy-Item $f.FullName (Join-Path $bb ('original-files\' + $f.Directory.Parent.Name + '__' + $f.Name)) -Force }
        $orig = [ordered]@{
            captured                    = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + ' (before any change)'
            mumu_dir                    = $dir
            mumu_settings               = [ordered]@{ resolution_mode = [string]$cur.resolution_mode; show_frame_rate = [string]$cur.show_frame_rate }
            shell_config                = @($shellCfgs | ForEach-Object { $t = [IO.File]::ReadAllText($_.FullName); [ordered]@{ file = $_.FullName; show_fps = (V $t '"show_fps"\s*:\s*\{\s*"enabled"\s*:\s*"([^"]*)"'); fps_limit = (V $t '"fps_limit"\s*:\s*"([^"]*)"') } })
            customer_config             = @($custCfgs | ForEach-Object { $t = [IO.File]::ReadAllText($_.FullName); [ordered]@{ file = $_.FullName; intelligence_preselect_enable = (V $t '"intelligence_preselect_enable"\s*:\s*"([^"]*)"') } })
            autostart_existed           = $false
            magpie_localappdata_existed = (Test-Path (Join-Path $env:LOCALAPPDATA 'Magpie'))
        }
        [IO.File]::WriteAllText((Join-Path $bb 'original-values.json'), ($orig | ConvertTo-Json -Depth 6), (New-Object Text.UTF8Encoding $false))
    }

    # ---------- 4) ملف الإعدادات ----------
    $ini = @"
# T7senaty settings  (1 = on, 0 = off)
Priority=1
HideMouse=1
HideDelayMs=1000
KeepAwake=1
F11=1
AI=1
AIPreset=0
PendingMuMu=0
Resolution=tablet.1
VmIndex=$vm
MuMuDir=$dir
"@
    [IO.File]::WriteAllText((Join-Path $AppDir 'settings.ini'), $ini, (New-Object Text.UTF8Encoding $false))

    # ---------- 5) إعدادات Magpie: مسار المحاكي عند هذا الجهاز ----------
    $mcfg = Join-Path $AppDir 'Magpie\config\config.json'
    $devExe = Get-ChildItem (Join-Path $dir 'nx_device\*\shell\MuMuNxDevice.exe') -ErrorAction SilentlyContinue | Select-Object -First 1
    if ((Test-Path $mcfg) -and $devExe) {
        $t = [IO.File]::ReadAllText($mcfg)
        $esc = $devExe.FullName.Replace('\', '\\')
        $t = [regex]::Replace($t, '("name"\s*:\s*"MuMu \(T7senaty\)"[\s\S]*?"pathRule"\s*:\s*")((?:[^"\\]|\\.)*)(")', { param($m) $m.Groups[1].Value + $esc + $m.Groups[3].Value }, 'None')
        [IO.File]::WriteAllText($mcfg, $t, (New-Object Text.UTF8Encoding $false))
    }

    # ---------- 6) تطبيق إعدادات المحاكي (الدقة + إخفاء عداد الفريمات) ----------
    $p = Start-Process $exe -ArgumentList '--apply' -Wait -PassThru
    if ($p.ExitCode -ne 0) { Fail 'ما قدرت أطبّق إعدادات المحاكي. التفاصيل داخل T7senaty.log.' }

    # ---------- 7) الاختصار ----------
    if (-not $ShortcutDir) { $ShortcutDir = [Environment]::GetFolderPath('Desktop') }
    $lnk = Join-Path $ShortcutDir 'T7senaty.lnk'
    $sh = New-Object -ComObject WScript.Shell
    $s = $sh.CreateShortcut($lnk)
    $s.TargetPath = $exe; $s.Arguments = '--session --launch'; $s.WorkingDirectory = $AppDir
    $s.IconLocation = "$exe,0"; $s.Description = 'T7senaty - MuMu'; $s.Save()

    Msg ("تم التثبيت ✓`n`nالمحاكي: $dir`nتحسين الصورة والأولوية وإخفاء الماوس: جاهزة`n`nشغّل الاختصار  T7senaty  من سطح المكتب.`nداخل المحاكي: F11 = شاشة كاملة محسّنة.`n`nللرجوع الكامل: افتح المجلد backBB وشغّل ملف الرجوع.") | Out-Null
}
catch {
    Fail ("صار خطأ أثناء التثبيت:`n" + $_.Exception.Message)
}
