#requires -version 7
<#
.SYNOPSIS
    Raises PackageValidationBaselineVersion in src/NCode.Buffers/NCode.Buffers.csproj to a version that is on nuget.org (ADR-1007).
.DESCRIPTION
    Waits until nuget.org lists the version, since a version that is not indexed yet cannot be downloaded for validation
    (ADR-1001), then sets the baseline and adds a changelog entry under [Unreleased]. It never lowers the baseline, and it
    changes nothing when the baseline already holds the version. Prints the version it set, or nothing.
.PARAMETER Version
    The released version, such as 5.0.2. A leading v is ignored.
.PARAMETER TimeoutMinutes
    How long to wait for nuget.org to list the version.
.PARAMETER Root
    The repository root; build/Test-UpdateBaseline.ps1 points it at a copy.
.PARAMETER SkipNugetCheck
    Doesn't wait for nuget.org; for build/Test-UpdateBaseline.ps1 only.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Version,
    [int]$TimeoutMinutes = 30,
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [switch]$SkipNugetCheck
)
$ErrorActionPreference = 'Stop'
$root = $Root
$version = $Version.TrimStart('v')
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "'$Version' isn't a stable version such as 5.0.2." }

$project = Join-Path $root 'src/NCode.Buffers/NCode.Buffers.csproj'
$projectText = [IO.File]::ReadAllText($project)
$pattern = '<PackageValidationBaselineVersion>(\d+\.\d+\.\d+)</PackageValidationBaselineVersion>'
$current = [regex]::Match($projectText, $pattern)
if (-not $current.Success) { throw "$project has no PackageValidationBaselineVersion with a stable version." }
if ([version]$current.Groups[1].Value -ge [version]$version) { return }

$index = 'https://api.nuget.org/v3-flatcontainer/ncode.buffers/index.json'
$deadline = [DateTime]::UtcNow.AddMinutes($TimeoutMinutes)
$delay = 15
while (-not $SkipNugetCheck) {
    # A failed or slow request counts as "not listed yet", so one network error doesn't end the wait.
    try {
        $answer = Invoke-RestMethod -Uri $index -TimeoutSec 30 -SkipHttpErrorCheck -StatusCodeVariable status
        if ($status -eq 200 -and $answer.versions -contains $version) { break }
    }
    catch { Write-Warning "nuget.org request failed: $($_.Exception.Message)" }
    if ([DateTime]::UtcNow -ge $deadline) { throw "nuget.org doesn't list NCode.Buffers $version after $TimeoutMinutes minutes." }
    Start-Sleep -Seconds ($delay + (Get-Random -Maximum 5))
    $delay = [Math]::Min($delay * 2, 120)
}

$utf8 = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText($project, [regex]::Replace($projectText, $pattern, "<PackageValidationBaselineVersion>$version</PackageValidationBaselineVersion>"), $utf8)

$changelog = Join-Path $root 'CHANGELOG.md'
$entry = "- Package validation compares every package with the published $version package ([ADR-1001](docs/decisions/1001-each-package-is-validated-against-the-last-released-package.md))."
$text = [IO.File]::ReadAllText($changelog) -replace "`r`n", "`n"
$unreleased = [regex]::Match($text, '(?s)## \[Unreleased\]\n(.*?)(?=\n## \[|\n\[unreleased\]:)')
if (-not $unreleased.Success) { throw 'CHANGELOG.md has no [Unreleased] section.' }
$body = $unreleased.Groups[1].Value
if ($body -match '(?m)^### Changed\n') {
    $newBody = [regex]::Replace($body, '(?m)^### Changed\n\n?', "### Changed`n`n$entry`n", 1)
}
else {
    # Keep a Changelog order: Changed goes after Added and before Deprecated, Removed, Fixed and Security.
    $later = [regex]::Match($body, '(?m)^### (Deprecated|Removed|Fixed|Security)\n')
    $section = "### Changed`n`n$entry`n`n"
    if ($later.Success) { $newBody = $body.Insert($later.Index, $section) }
    else { $newBody = $body.TrimEnd("`n") + "`n`n" + $section.TrimEnd("`n") + "`n"; $newBody = "`n" + $newBody.TrimStart("`n") }
}
$text = $text.Substring(0, $unreleased.Groups[1].Index) + $newBody + $text.Substring($unreleased.Groups[1].Index + $body.Length)
[IO.File]::WriteAllText($changelog, $text, $utf8)
$version
