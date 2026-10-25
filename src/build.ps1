# يبني T7senaty.exe من T7senaty.cs باستخدام مترجم C# الموجود أصلاً في ويندوز (بدون تحميل أي شي)
$ErrorActionPreference = 'Stop'
$src = $PSScriptRoot
$app = Split-Path $src -Parent
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$refs = '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll'
$common = '/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/codepage:65001'

$icon = if (Test-Path "$src\T7senaty.ico") { @("/win32icon:$src\T7senaty.ico") } else { @() }

& $csc @common @refs @icon "/out:$app\T7senaty.exe" "$src\T7senaty.cs"
if ($LASTEXITCODE -ne 0) { throw 'compile failed' }
Write-Host "OK -> $app\T7senaty.exe"
