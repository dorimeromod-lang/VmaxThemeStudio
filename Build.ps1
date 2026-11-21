param(
    [switch]$RunSelfTest
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $root 'src'
$exe = Join-Path $root 'VmaxThemeStudio.exe'
$manifest = Join-Path $root 'app.manifest'
$icon = Join-Path $root 'VmaxThemeStudio.ico'

$csc = $null
$explicit = 'C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\Roslyn\csc.exe'
if (Test-Path -LiteralPath $explicit) {
    $csc = $explicit
} else {
    $found = Get-ChildItem -Path 'C:\Program Files\Microsoft Visual Studio' -Filter 'csc.exe' -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\Roslyn\\csc\.exe$' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if ($found) { $csc = $found.FullName }
}
if (-not $csc) { throw 'Не найден csc.exe (Roslyn). Укажите путь вручную в Build.ps1.' }

$fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$refs = @(
    (Join-Path $fw 'WPF\PresentationFramework.dll'),
    (Join-Path $fw 'WPF\PresentationCore.dll'),
    (Join-Path $fw 'WPF\WindowsBase.dll'),
    (Join-Path $fw 'System.Xaml.dll'),
    (Join-Path $fw 'System.Drawing.dll'),
    (Join-Path $fw 'System.Windows.Forms.dll'),
    (Join-Path $fw 'System.Xml.dll'),
    (Join-Path $fw 'System.IO.Compression.dll'),
    (Join-Path $fw 'System.IO.Compression.FileSystem.dll'),
    (Join-Path $fw 'System.Core.dll')
)
foreach ($r in $refs) {
    if (-not (Test-Path -LiteralPath $r)) { throw "Не найдена ссылка: $r" }
}

$sources = @(Get-ChildItem -LiteralPath $src -Filter '*.cs' | Sort-Object Name | ForEach-Object { $_.FullName })
if ($sources.Count -eq 0) { throw "В $src нет .cs файлов." }

$cscArgs = @(
    '/nologo',
    '/target:winexe',
    '/platform:x64',
    '/optimize+',
    '/debug-',
    '/warn:4',
    '/codepage:65001',
    "/out:$exe",
    "/win32manifest:$manifest",
    "/win32icon:$icon"
)
foreach ($r in $refs) { $cscArgs += "/reference:$r" }
$cscArgs += $sources

Write-Host "Компиляция $($sources.Count) файлов..." -ForegroundColor Cyan
& $csc $cscArgs
if ($LASTEXITCODE -ne 0) {
    throw "СБОРКА НЕ УДАЛАСЬ (код $LASTEXITCODE)"
}

Write-Host "СБОРКА OK: $exe" -ForegroundColor Green

if ($RunSelfTest) {
    Write-Host "Запуск самотеста..." -ForegroundColor Cyan
    & $exe --selftest
    if ($LASTEXITCODE -ne 0) {
        throw "САМОТЕСТ НЕ ПРОЙДЕН (код $LASTEXITCODE)"
    }
}
