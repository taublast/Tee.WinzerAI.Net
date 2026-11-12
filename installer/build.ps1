<#
Baut die Auslieferung von WinzerAI:
  artifacts\publish\win-<arch>\          die App als Ordner (eigenständig, braucht kein installiertes .NET)
  artifacts\WinzerAI-<version>-<arch>.zip          zum Entpacken und Starten
  artifacts\WinzerAI-<version>-<arch>-setup.exe    Installer – nur wenn Inno Setup 6 (ISCC.exe) vorhanden ist

Aufruf:  pwsh installer\build.ps1 [-Arch x64,arm64] [-SkipTests]

Die App wird als Ordner veröffentlicht, nicht als einzelne EXE: Als Einzeldatei startet WPF unter .NET 11 nicht
(DirectWriteForwarder.dll lässt sich aus dem Bündel nicht laden).
#>
param(
    [string[]]$Arch = @('x64', 'arm64'),
    [switch]$SkipTests
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { $env:Path = "C:\Program Files\dotnet;" + $env:Path }
[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props')
$version = $props.Project.PropertyGroup.Version
Write-Host "WinzerAI $version"

if (-not $SkipTests) {
    dotnet test Winzer.slnx -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests fehlgeschlagen – keine Auslieferung.' }
}

$iscc = (Get-Command iscc -ErrorAction SilentlyContinue).Source
if (-not $iscc) {
    $iscc = Get-ChildItem 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe', 'C:\Program Files\Inno Setup 6\ISCC.exe' -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
}

$artifacts = Join-Path $root 'artifacts'
foreach ($a in $Arch) {
    $rid = "win-$a"
    $publish = Join-Path $artifacts "publish\$rid"
    if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
    dotnet publish src/Winzer.Desktop -c Release -r $rid --self-contained -o $publish
    if ($LASTEXITCODE -ne 0) { throw "Veröffentlichen für $rid fehlgeschlagen." }

    $zip = Join-Path $artifacts "WinzerAI-$version-$a.zip"
    if (Test-Path $zip) { Remove-Item $zip }
    Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip
    Write-Host ("{0}  {1:N0} MB" -f $zip, ((Get-Item $zip).Length / 1MB))

    if ($iscc) {
        & $iscc "/DAppVersion=$version" "/DArch=$a" "/DSourceDir=$publish" "/DOutputDir=$artifacts" (Join-Path $PSScriptRoot 'WinzerAI.iss')
        if ($LASTEXITCODE -ne 0) { throw "Inno Setup für $a fehlgeschlagen." }
    }
}
if (-not $iscc) {
    Write-Warning 'Inno Setup 6 (ISCC.exe) nicht gefunden – es entstanden nur die ZIP-Pakete. Installieren: winget install JRSoftware.InnoSetup'
}
