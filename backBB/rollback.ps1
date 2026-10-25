# T7senaty — خط الرجعة (backBB)
# يرجّع كل شي مثل ما كان قبل تركيب T7senaty ثم يحذف مجلد T7senaty بالكامل.
#   rollback.ps1              -> رجوع كامل
#   rollback.ps1 -MouseOnly   -> يرجّع الماوس فقط (طوارئ)
#   (-Yes -ReportFile <ملف>  للاختبار بدون نوافذ)
param([switch]$MouseOnly, [switch]$Relaunched, [string]$AppDir, [switch]$Yes, [string]$ReportFile)

$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type -Namespace T7senatyBack -Name Win -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SystemParametersInfo(uint action, uint param, System.IntPtr pv, uint winIni);
[StructLayout(LayoutKind.Sequential)] public struct PPTS { public uint Version; public uint ControlMask; public uint StateMask; }
[DllImport("kernel32.dll")] public static extern bool SetProcessInformation(System.IntPtr h, int infoClass, ref PPTS info, int size);
'@

function Msg([string]$text, [string]$buttons = 'OK', [string]$icon = 'Information') {
    $owner = New-Object System.Windows.Forms.Form -Property @{
        TopMost = $true; ShowInTaskbar = $false; FormBorderStyle = 'None'; Opacity = 0
        StartPosition = 'CenterScreen'; Size = (New-Object System.Drawing.Size 1, 1) }
    $owner.Show(); $owner.Activate()
    try { [System.Windows.Forms.MessageBox]::Show($owner, $text, 'T7senaty - backBB', $buttons, $icon, 'Button1', 'RtlReading, RightAlign') }
    finally { $owner.Dispose() }
}

$devNames = 'MuMuNxDevice', 'MuMuNxHeadless', 'MuMuPlayer', 'MuMuVMMHeadless'

function Restore-Mouse {
    [T7senatyBack.Win]::SystemParametersInfo(0x57, 0, [IntPtr]::Zero, 0) | Out-Null
}

if ($MouseOnly) {
    Restore-Mouse
    Msg 'رجع الماوس طبيعي ✓'
    exit 0
}

if (-not $Relaunched) {
    $AppDir = Split-Path $PSScriptRoot -Parent
    if (-not $Yes) {
        $answer = Msg ("راح يتم:`n`n" +
            "• إيقاف T7senaty وحذف مجلده بالكامل`n" +
            "• إرجاع دقة المحاكي وعداد الفريمات مثل ما كانت`n" +
            "• إلغاء تشغيل T7senaty التلقائي مع ويندوز`n" +
            "• إرجاع الماوس وأولوية المحاكي طبيعية`n`n" +
            "متأكد تبي ترجع كل شي؟") 'YesNo' 'Question'
        if ($answer -ne 'Yes') { exit 0 }
    }

    # المحاكي لازم يكون مقفل: هو يحفظ إعداداته من الذاكرة ويكتب فوق أي تعديل
    if (Get-Process $devNames -ErrorAction SilentlyContinue) {
        if ($Yes) {
            if ($ReportFile) { [IO.File]::WriteAllText($ReportFile, 'MUMU_RUNNING', (New-Object Text.UTF8Encoding $true)) }
            exit 1
        }
        $close = Msg ("المحاكي شغّال الحين.`nلازم يتقفل عشان ترجع إعداداته مثل ما كانت.`n`nأقفله لك وأكمل؟") 'YesNo' 'Question'
        if ($close -ne 'Yes') { exit 0 }
        $orig = Get-Content (Join-Path $PSScriptRoot 'original-values.json') -Raw -Encoding UTF8 | ConvertFrom-Json
        $mgr = Join-Path $orig.mumu_dir 'nx_main\MuMuManager.exe'
        if (Test-Path $mgr) { & $mgr control -v all shutdown | Out-Null }
        for ($i = 0; $i -lt 60 -and (Get-Process $devNames -ErrorAction SilentlyContinue); $i++) { Start-Sleep -Seconds 1 }
        if (Get-Process $devNames -ErrorAction SilentlyContinue) {
            Msg 'ما قدرت أقفل المحاكي — اقفله بيدك وشغّل الرجوع مرة ثانية.' 'OK' 'Warning'
            exit 1
        }
    }

    # نشغّل نسخة من السكربت من مجلد مؤقت عشان نقدر نحذف مجلد T7senaty كامل
    $tmpDir = Join-Path $env:TEMP 'T7senaty-rollback'
    New-Item -ItemType Directory -Force $tmpDir | Out-Null
    Copy-Item $PSCommandPath (Join-Path $tmpDir 'rollback.ps1') -Force
    $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$tmpDir\rollback.ps1`"", '-Relaunched', '-AppDir', "`"$AppDir`"")
    if ($ReportFile) { $argList += @('-ReportFile', "`"$ReportFile`"") }
    Start-Process powershell.exe -WorkingDirectory $env:TEMP -WindowStyle Hidden -ArgumentList $argList
    exit 0
}

# ---------- الرجوع الفعلي (من النسخة المؤقتة) ----------
Start-Sleep -Seconds 2
$report = New-Object System.Collections.Generic.List[string]
$problems = New-Object System.Collections.Generic.List[string]

# 1) إيقاف T7senaty و Magpie (النسخة اللي داخل مجلد T7senaty فقط)
Get-Process T7senaty -ErrorAction SilentlyContinue | ForEach-Object {
    try { $_.Kill(); $_.WaitForExit(5000) | Out-Null } catch { }
}
Get-Process Magpie -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($AppDir, [StringComparison]::OrdinalIgnoreCase) } | ForEach-Object {
    try { $_.Kill(); $_.WaitForExit(5000) | Out-Null } catch { }
}
Restore-Mouse
$report.Add('✓ T7senaty و Magpie توقفوا والماوس رجع طبيعي')

# 2) إلغاء التشغيل التلقائي
Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name T7senaty -ErrorAction SilentlyContinue
if (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name T7senaty -ErrorAction SilentlyContinue) {
    $problems.Add('التشغيل التلقائي ما انحذف')
} else { $report.Add('✓ انلغى التشغيل التلقائي مع ويندوز') }

# 3) أولوية المحاكي ترجع طبيعية (لو كان شغّال)
Get-Process $devNames -ErrorAction SilentlyContinue | ForEach-Object {
    try {
        $_.PriorityClass = 'Normal'
        $s = New-Object T7senatyBack.Win+PPTS
        $s.Version = 1   # ControlMask = 0 -> ويندوز يرجع يتحكم بالوضع الافتراضي
        [T7senatyBack.Win]::SetProcessInformation($_.Handle, 4, [ref]$s, 12) | Out-Null
    } catch { }
}
$report.Add('✓ أولوية المحاكي رجعت طبيعية')

# 4) إعدادات المحاكي الأصلية (والمحاكي مقفل)
$origFile = Join-Path $AppDir 'backBB\original-values.json'
if (-not (Test-Path $origFile)) {
    $problems.Add('ملف القيم الأصلية original-values.json مفقود')
} elseif (Get-Process $devNames -ErrorAction SilentlyContinue) {
    $problems.Add('المحاكي شغّال — اقفله وشغّل الرجوع مرة ثانية')
} else {
    $orig = Get-Content $origFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $mgr = Join-Path $orig.mumu_dir 'nx_main\MuMuManager.exe'

    # أ) أول شي MuMuManager، لأنه يعيد كتابة ملفات الإعدادات من عنده
    if (Test-Path $mgr) {
        $keys = @($orig.mumu_settings.PSObject.Properties)
        $setArgs = @('setting', '-v', 'all'); foreach ($k in $keys) { $setArgs += @('-k', $k.Name, '-val', [string]$k.Value) }
        & $mgr @setArgs | Out-Null
        $getArgs = @('setting', '-v', '0'); foreach ($k in $keys) { $getArgs += @('-k', $k.Name) }
        $now = & $mgr @getArgs | Out-String | ConvertFrom-Json
        $bad = @($keys | Where-Object { -not ([string]$now.($_.Name)).Equals([string]$_.Value) })
        if ($bad.Count -eq 0) { $report.Add('✓ دقة المحاكي رجعت مثل ما كانت') }
        else { $problems.Add('إعدادات المحاكي ما رجعت: ' + (($bad | ForEach-Object { $_.Name }) -join ', ')) }
    } else { $problems.Add('ما لقيت MuMuManager.exe') }

    # ب) بعده نرجّع القيم الأصلية داخل الملفات نفسها
    $patterns = @{
        show_fps                      = '("show_fps"\s*:\s*\{\s*"enabled"\s*:\s*")([^"]*)(")'
        fps_limit                     = '("fps_limit"\s*:\s*")([^"]*)(")'
        intelligence_preselect_enable = '("intelligence_preselect_enable"\s*:\s*")([^"]*)(")'
    }
    foreach ($e in @($orig.shell_config) + @($orig.customer_config)) {
        if (-not $e -or -not (Test-Path $e.file)) { continue }
        try {
            $raw = [IO.File]::ReadAllBytes($e.file)
            $bom = $raw.Length -ge 3 -and $raw[0] -eq 0xEF -and $raw[1] -eq 0xBB -and $raw[2] -eq 0xBF
            $text = [IO.File]::ReadAllText($e.file)
            $new = $text
            foreach ($p in $e.PSObject.Properties) {
                if ($patterns.ContainsKey($p.Name)) { $new = ([regex]$patterns[$p.Name]).Replace($new, ('${1}' + $p.Value + '${3}'), 1) }
            }
            if (-not $new.Equals($text)) { [IO.File]::WriteAllText($e.file, $new, (New-Object Text.UTF8Encoding $bom)) }
        } catch { $problems.Add('ما قدرت أرجّع ' + (Split-Path $e.file -Leaf) + ': ' + $_.Exception.Message) }
    }
    $report.Add('✓ عداد الفريمات وحد الفريمات رجعت مثل ما كانت')
}

# 5) حذف مجلد T7senaty (فقط لو كل شي فوق رجع صح)
if ($problems.Count -eq 0) {
    # اختصار سطح المكتب
    Remove-Item (Join-Path ([Environment]::GetFolderPath('Desktop')) 'T7senaty.lnk') -Force -ErrorAction SilentlyContinue
    # Magpie يشتغل بوضع محمول، لكن لو انكتب له شي في LocalAppData وما كان موجود قبل، نحذفه
    if ($orig -and $orig.magpie_localappdata_existed -eq $false) {
        $magpieData = Join-Path $env:LOCALAPPDATA 'Magpie'
        if (Test-Path $magpieData) { Remove-Item $magpieData -Recurse -Force -ErrorAction SilentlyContinue }
    }
    for ($i = 0; $i -lt 5 -and (Test-Path $AppDir); $i++) {
        Remove-Item $AppDir -Recurse -Force -ErrorAction SilentlyContinue
        if (Test-Path $AppDir) { Start-Sleep -Seconds 1 }
    }
    if (Test-Path $AppDir) { $problems.Add('مجلد T7senaty ما انحذف (يمكن ملف مفتوح) — احذفه بيدك') }
    else { $report.Add('✓ انحذف مجلد T7senaty بالكامل (ومعه Magpie)') }
} else {
    $problems.Add('ما حذفت مجلد T7senaty عشان تقدر تعيد المحاولة')
}

$msg = ($report -join "`n")
if ($ReportFile) {
    [IO.File]::WriteAllText($ReportFile, ($msg + "`nPROBLEMS: " + $problems.Count + "`n" + ($problems -join "`n")), (New-Object Text.UTF8Encoding $true))
} elseif ($problems.Count -gt 0) {
    Msg ($msg + "`n`nمشاكل:`n• " + ($problems -join "`n• ")) 'OK' 'Warning'
} else {
    Msg ($msg + "`n`nتم الرجوع لكل شي مثل قبل ✓`nالدقة وعداد الفريمات يرجعون من التشغيلة الجاية للمحاكي.")
}
Remove-Item $PSCommandPath -Force -ErrorAction SilentlyContinue
