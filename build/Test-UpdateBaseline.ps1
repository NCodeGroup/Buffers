#requires -version 7
<#
.SYNOPSIS
    Tests build/Update-Baseline.ps1 against copies of the csproj and CHANGELOG.md, offline.
.DESCRIPTION
    Covers every branch that edits the files: a baseline below, equal to and above the version, and each placement of the
    changelog entry. Exits 1 on the first failed check. build/gate.ps1 runs it.
#>
$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'Update-Baseline.ps1'
$utf8 = [Text.UTF8Encoding]::new($false)
$entry = '- Package validation compares every package with the published 5.0.2 package ([ADR-1001](docs/decisions/1001-each-package-is-validated-against-the-last-released-package.md)).'

function Invoke-Case([string]$Name, [string]$Baseline, [string]$Unreleased, [string]$ExpectedUnreleased) {
    $root = Join-Path ([IO.Path]::GetTempPath()) "baseline-test-$([guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Path (Join-Path $root 'src/NCode.Buffers') | Out-Null
    try {
        $projectPath = Join-Path $root 'src/NCode.Buffers/NCode.Buffers.csproj'
        $changelogPath = Join-Path $root 'CHANGELOG.md'
        $project = "<Project><PropertyGroup><PackageValidationBaselineVersion>$Baseline</PackageValidationBaselineVersion></PropertyGroup></Project>`n"
        $changelog = "# Changelog`n`n## [Unreleased]`n$Unreleased`n## [5.0.1] - 2026-11-14`n`n### Added`n`n- Old.`n`n[unreleased]: https://example.test/compare/v5.0.1...HEAD`n"
        [IO.File]::WriteAllText($projectPath, $project, $utf8)
        [IO.File]::WriteAllText($changelogPath, $changelog, $utf8)

        $set = & $script -Version v5.0.2 -Root $root -SkipNugetCheck

        $actualProject = [IO.File]::ReadAllText($projectPath)
        $actualChangelog = [IO.File]::ReadAllText($changelogPath)
        # A baseline already at or above the version changes nothing; a lower one is raised, once.
        if ($Baseline -eq '5.0.2' -or [version]$Baseline -gt [version]'5.0.2') {
            if ($set -or $actualProject -ne $project -or $actualChangelog -ne $changelog) { throw 'changed files for a baseline that was already high enough' }
        }
        else {
            if ($set -ne '5.0.2') { throw "printed '$set' instead of 5.0.2" }
            if ($actualProject -notmatch '<PackageValidationBaselineVersion>5\.0\.2<') { throw 'baseline not raised' }
            $expected = "# Changelog`n`n## [Unreleased]`n$ExpectedUnreleased`n## [5.0.1] - 2026-11-14`n`n### Added`n`n- Old.`n`n[unreleased]: https://example.test/compare/v5.0.1...HEAD`n"
            if ($actualChangelog -ne $expected) { throw "changelog differs:`n$actualChangelog" }
            & $script -Version 5.0.2 -Root $root -SkipNugetCheck | Out-Null
            if ([IO.File]::ReadAllText($changelogPath) -ne $expected) { throw 'a second run changed the changelog' }
        }
    }
    catch { Write-Host "FAILED: $Name`n$_"; exit 1 }
    finally { Remove-Item -LiteralPath $root -Recurse -Force }
    Write-Host "ok: $Name"
}

Invoke-Case 'empty Unreleased' '5.0.0' "`n" "`n### Changed`n`n$entry`n"
Invoke-Case 'existing Changed' '5.0.0' "`n### Changed`n`n- Old entry.`n" "`n### Changed`n`n$entry`n- Old entry.`n"
Invoke-Case 'Added before Changed' '5.0.0' "`n### Added`n`n- New.`n" "`n### Added`n`n- New.`n`n### Changed`n`n$entry`n"
Invoke-Case 'Changed before Fixed' '5.0.0' "`n### Fixed`n`n- A bug.`n" "`n### Changed`n`n$entry`n`n### Fixed`n`n- A bug.`n"
Invoke-Case 'baseline equal' '5.0.2' "`n" "`n"
Invoke-Case 'baseline higher' '5.1.0' "`n" "`n"
