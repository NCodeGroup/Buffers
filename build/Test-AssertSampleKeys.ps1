#requires -version 7
<#
.SYNOPSIS
    Tests build/Assert-SampleKeys.ps1 against fixture repositories, offline.
.DESCRIPTION
    Covers a clean set of keys and each rule the check enforces: a key defined twice, also in another letter case, a
    site sample's key without the Site prefix, and any other key with it. Each failing fixture breaks one rule, and
    each case checks the exit code and every line the check prints. Exits 1 on the first failed check. build/gate.ps1
    runs it before the check.
#>
$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'Assert-SampleKeys.ps1'
$utf8 = [Text.UTF8Encoding]::new($false)
$readme = 'tests/NCode.Buffers.Tests/ReadmeSamplesTests.cs'
$signatures = 'tests/NCode.Buffers.Tests/ReadmeSamples/Signatures.cs'
$index = 'tests/NCode.Buffers.Site.Tests/IndexSamples.cs'
$guide = 'tests/NCode.Buffers.Site.Tests/Guides/VerifyAMacSamples.cs'
# A folder whose name only starts with the site project's holds no site samples.
$beside = 'tests/NCode.Buffers.Site.Tests.Extra/ExtraSamples.cs'

# $Samples maps each fixture file to the key of its one sample, whose indented begin marker is on line 3.
function Invoke-Case([string]$Name, [hashtable]$Samples, [string[]]$Problems) {
    $root = Join-Path ([IO.Path]::GetTempPath()) "sample-keys-test-$([guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Path $root | Out-Null
    try {
        # The check lists files with git, which lists a new repository's files as untracked.
        git init -q $root
        if ($LASTEXITCODE) { throw 'git init failed' }
        foreach ($path in $Samples.Keys) {
            $file = Join-Path $root $path
            New-Item -ItemType Directory -Path (Split-Path -Parent $file) -Force | Out-Null
            [IO.File]::WriteAllText($file, "// A fixture.`n`n    // begin-snippet: $($Samples[$path])`n    _ = 0;`n    // end-snippet`n", $utf8)
        }

        $printed = @(& $script -Root $root)

        $code = $LASTEXITCODE
        $expectedCode = if ($Problems) { 1 } else { 0 }
        if ($code -ne $expectedCode) { throw "exited $code instead of $expectedCode" }
        $expected = if ($Problems) { @('Sample keys: FAILED.') + $Problems } else { @() }
        if (($printed -join "`n") -cne ($expected -join "`n")) { throw "printed:`n$($printed -join "`n")" }
    }
    catch { Write-Host "FAILED: $Name`n$_"; exit 1 }
    finally { Remove-Item -LiteralPath $root -Recurse -Force }
    Write-Host "ok: $Name"
}

Invoke-Case 'clean set' @{ $readme = 'ZeroedAfterUse'; $index = 'SiteZeroedAfterUse'; $guide = 'SiteVerifyAMac'; $beside = 'Extra' }
Invoke-Case 'key defined twice' @{ $signatures = 'Zeroed'; $readme = 'Zeroed'; $index = 'SiteZeroed' } `
    "Zeroed is defined 2 times: ${signatures}:3, ${readme}:3"
Invoke-Case 'keys that differ only in case' @{ $signatures = 'ZeroedAfterUse'; $readme = 'ZeroedAfteruse' } `
    "ZeroedAfterUse is defined 2 times: ${signatures}:3, ${readme}:3"
Invoke-Case 'site key without Site' @{ $readme = 'Zeroed'; $index = 'ZeroedAfterUse'; $guide = 'siteVerifyAMac' } `
    "${guide}:3: siteVerifyAMac is a site sample, so its key starts with Site", `
    "${index}:3: ZeroedAfterUse is a site sample, so its key starts with Site"
Invoke-Case 'other key with Site' @{ $readme = 'SiteZeroed'; $index = 'SiteZeroedAfterUse'; $beside = 'SiteExtra' } `
    "${beside}:3: SiteExtra starts with Site, as only a site sample's key does", `
    "${readme}:3: SiteZeroed starts with Site, as only a site sample's key does"
# Every case passed. The last one left the check's exit code, 1, in $LASTEXITCODE, which build/gate.ps1 reads.
exit 0
