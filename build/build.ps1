# Bouwt Dusk.exe en de tests met de C#-compiler die in Windows zit (.NET Framework 4.8), en draait de tests.
# Gebruik: powershell -ExecutionPolicy Bypass -File build\build.ps1
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe" }
$out = Join-Path $root "out"
New-Item -ItemType Directory -Force -Path $out | Out-Null

$src = @(Get-ChildItem (Join-Path $root "src") -Filter *.cs | ForEach-Object { $_.FullName })
$refs = "/r:System.dll", "/r:System.Core.dll", "/r:System.Drawing.dll", "/r:System.Windows.Forms.dll"

Write-Host "Dusk.exe bouwen..."
& $csc /nologo /target:winexe /optimize+ /platform:anycpu /warn:4 /warnaserror+ `
    "/out:$out\Dusk.exe" "/win32icon:$root\assets\Dusk.ico" "/win32manifest:$root\assets\Dusk.manifest" `
    @refs @src
if ($LASTEXITCODE -ne 0) { throw "Bouwen van Dusk.exe mislukt" }

Write-Host "Tests bouwen..."
$tests = @(Get-ChildItem (Join-Path $root "tests") -Filter *.cs | ForEach-Object { $_.FullName })
& $csc /nologo /target:exe /platform:anycpu /main:Dusk.Tests "/out:$out\Dusk.Tests.exe" @refs @src @tests
if ($LASTEXITCODE -ne 0) { throw "Bouwen van de tests mislukt" }

Write-Host "Tests draaien..."
& "$out\Dusk.Tests.exe"
if ($LASTEXITCODE -ne 0) { throw "Er zijn tests mislukt" }
$size = [math]::Round((Get-Item "$out\Dusk.exe").Length / 1KB)
Write-Host "Klaar: $out\Dusk.exe ($size KB)"
