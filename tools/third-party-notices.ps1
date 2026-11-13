<#
Erzeugt THIRD-PARTY-NOTICES.txt für eine veröffentlichte WinzerAI-Fassung: die Lizenztexte aller Fremdpakete, die
laut <App>.deps.json tatsächlich ausgeliefert werden, dazu die Drittanbieter-Hinweise, die diese Pakete selbst
mitbringen (.NET-Laufzeit, Skia, HarfBuzz, Microsoft.Extensions).

Aufruf:  pwsh tools\third-party-notices.ps1 -DepsJson artifacts\publish\win-x64\WinzerAI.deps.json -Out <Datei>

Woher der Lizenztext kommt, in dieser Reihenfolge:
  1. tools\licenses\<Datei> laut $KnownLicenses unten – für Pakete, die keine Lizenzdatei mitbringen. Die Texte stammen aus
     dem Repository des Pakets, und zwar vom Commit, den die .nuspec nennt.
  2. Die Lizenzdatei im NuGet-Paket (LICENSE, LICENSE.txt, LICENSE.md …).
Fehlt beides, bricht das Skript ab: Ein neues oder geändertes Paket braucht dann einen Eintrag in $KnownLicenses.

Die Übersicht am Anfang der Datei liest die Lizenzseite im Spiel (Winzer.Presentation.ThirdPartyNotices).
Ihr Format – Zeilen „  Paket<TAB>Version<TAB>Lizenz“ zwischen „Übersicht“ und der ersten Leerzeile – nicht ändern,
ohne die Seite anzupassen.
#>
param(
    [Parameter(Mandatory)] [string]$DepsJson,
    [Parameter(Mandatory)] [string]$Out
)
$ErrorActionPreference = 'Stop'
$licenses = Join-Path $PSScriptRoot 'licenses'

# Pakete ohne eigene Lizenzdatei: Muster für die Paket-ID → Datei in tools\licenses, Lizenz, Herkunft
$KnownLicenses = @(
    @{ Match = '^AppoMobi\.Gestures$';      File = 'AppoMobi.Gestures.txt';      License = 'MIT';          Source = 'https://github.com/taublast/AppoMobi.Gestures' }
    @{ Match = '^AppoMobi\.Specials$';      File = 'AppoMobi.Specials.txt';      License = 'MIT';          Source = 'https://github.com/taublast/AppoMobi.Specials' }
    @{ Match = '^DrawnUi\.';                File = 'DrawnUi.txt';                License = 'MIT';          Source = 'https://github.com/DrawnUi/DrawnUi.Net' }
    @{ Match = '^ExCSS$';                   File = 'ExCSS.txt';                  License = 'MIT';          Source = 'https://github.com/TylerBrinks/ExCSS' }
    @{ Match = '^Polly$';                   File = 'Polly.txt';                  License = 'BSD-3-Clause'; Source = 'https://github.com/App-vNext/Polly' }
    @{ Match = '^Polly\.Extensions\.Http$'; File = 'Polly.Extensions.Http.txt';  License = 'BSD-3-Clause'; Source = 'https://github.com/App-vNext/Polly.Extensions.Http' }
    @{ Match = '^Svg\.Custom$';             File = 'Svg.Custom.txt';             License = 'MS-PL';        Source = 'https://github.com/svg-net/SVG' }
    @{ Match = '^(Svg\.|ShimSkiaSharp$)';   File = 'Svg.Skia.txt';               License = 'MIT';          Source = 'https://github.com/wieslawsoltes/Svg.Skia' }
    @{ Match = '^Microsoft\.Extensions\.Http\.Polly$'; File = 'Microsoft.Extensions.Http.Polly.txt'; License = 'MIT'; Source = 'https://github.com/dotnet/aspnetcore' }
    @{ Match = '^Avalonia\.Angle\.';      File = 'ANGLE.txt';                  License = 'BSD-3-Clause'; Source = 'https://github.com/AvaloniaUI/angle (ANGLE)' }
)

$nuget = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $HOME '.nuget\packages' }
$deps = Get-Content $DepsJson -Raw | ConvertFrom-Json
$app = ($deps.libraries.PSObject.Properties | Where-Object { $_.Value.type -eq 'project' } | Select-Object -First 1).Name -replace '/.*$'

function Read-Text([string]$path) { ((Get-Content $path -Raw -Encoding utf8) -replace "`r`n", "`n").Trim() }

function Get-LicenseName([string]$text) {
    if ($text -match 'Permission is hereby granted, free of charge') { return 'MIT' }
    if ($text -match 'Redistribution and use in source and binary forms' -and $text -match 'Neither the name') { return 'BSD-3-Clause' }
    if ($text -match 'Microsoft Public License') { return 'MS-PL' }
    if ($text -match 'Apache License') { return 'Apache-2.0' }
    return 'siehe Text'
}

$packages = [System.Collections.Generic.List[object]]::new()
$notices = [ordered]@{}   # Inhalt → Pakete, die diese Drittanbieter-Hinweise mitbringen

foreach ($lib in $deps.libraries.PSObject.Properties) {
    if ($lib.Value.type -notin 'package', 'runtimepack') { continue }
    $id, $version = $lib.Name -split '/', 2
    $id = $id -replace '^runtimepack\.'
    $dir = Join-Path $nuget ($(if ($lib.Value.path) { $lib.Value.path } else { "$($id.ToLowerInvariant())/$version" }))
    if (-not (Test-Path $dir)) { throw "Paket $id $version nicht im NuGet-Cache ($dir) – erst dotnet restore/publish ausführen." }

    $knownEntry = $KnownLicenses | Where-Object { $id -match $_.Match } | Select-Object -First 1
    $files = Get-ChildItem $dir -File | Where-Object { $_.Name -match '^licen[cs]e(\.(txt|md))?$' }
    if ($knownEntry) {
        $text = Read-Text (Join-Path $licenses $knownEntry.File)
        $license = $knownEntry.License
        $source = $knownEntry.Source
    } elseif ($files) {
        $text = Read-Text $files[0].FullName
        [xml]$nuspec = Get-Content (Get-ChildItem $dir -Filter *.nuspec | Select-Object -First 1).FullName -Raw
        $meta = $nuspec.package.metadata
        $expr = $meta.license.'#text'
        $license = if ($meta.license.type -eq 'expression' -and $expr) { $expr } else { Get-LicenseName $text }
        $source = if ($meta.repository.url) { $meta.repository.url } elseif ($meta.projectUrl) { $meta.projectUrl } else { '' }
    } else {
        throw "Für $id $version gibt es keinen Lizenztext – Datei nach tools\licenses legen und in `$KnownLicenses eintragen."
    }
    $packages.Add([pscustomobject]@{ Id = $id; Version = $version; License = $license; Source = $source; Text = $text })

    foreach ($n in Get-ChildItem $dir -File | Where-Object { $_.Name -match '^third-party-notices' }) {
        $content = Read-Text $n.FullName
        if (-not $notices.Contains($content)) { $notices[$content] = [System.Collections.Generic.List[string]]::new() }
        $notices[$content].Add("$id $version")
    }
}
$packages = $packages | Sort-Object Id

$rule = '=' * 79
$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("$app – Lizenzhinweise für Software von Dritten")
[void]$sb.AppendLine('')
[void]$sb.AppendLine("$app selbst steht unter der MIT-Lizenz (LICENSE.txt). Mitgeliefert wird die folgende Software von Dritten.")
[void]$sb.AppendLine('Erzeugt von tools/third-party-notices.ps1 aus der Paketliste der Auslieferung.')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('Übersicht')
foreach ($p in $packages) { [void]$sb.AppendLine("  $($p.Id)`t$($p.Version)`t$($p.License)") }
[void]$sb.AppendLine('')

# Lizenztexte; Pakete mit gleichem Text stehen in einem Abschnitt
foreach ($group in $packages | Group-Object Text) {
    $first = $group.Group[0]
    [void]$sb.AppendLine($rule)
    foreach ($p in $group.Group) { [void]$sb.AppendLine("$($p.Id) $($p.Version)") }
    [void]$sb.AppendLine("Lizenz: $($first.License)" + $(if ($first.Source) { "  ·  $($first.Source)" }))
    [void]$sb.AppendLine($rule)
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine($first.Text)
    [void]$sb.AppendLine('')
}

# Hinweise, die die Pakete selbst für eingebaute Fremdsoftware mitbringen
foreach ($entry in $notices.GetEnumerator()) {
    [void]$sb.AppendLine($rule)
    [void]$sb.AppendLine("Drittanbieter-Hinweise aus: $($entry.Value -join ', ')")
    [void]$sb.AppendLine($rule)
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine($entry.Key)
    [void]$sb.AppendLine('')
}

$text = $sb.ToString() -replace "`r?`n", "`r`n"
[System.IO.File]::WriteAllText([System.IO.Path]::GetFullPath($Out), $text, [System.Text.UTF8Encoding]::new($false))
Write-Host ("{0}: {1} Pakete, {2} Hinweisdateien" -f $Out, $packages.Count, $notices.Count)
