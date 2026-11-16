#requires -version 7
<#
.SYNOPSIS
    Tests build/Assert-SampleKeys.ps1 against fixture repositories, offline.
.DESCRIPTION
    Covers a clean set of keys, the lines and files the check must not read a key from, and each rule it enforces: a
    key that is not PascalCase letters and digits, a key defined twice, also in another letter case, a site sample's
    key without the Site prefix, any other key with it, and no key at all. Each failing fixture breaks one rule, and
    each case checks the exit code and every line the check prints. Exits 1 on the first failed check. build/gate.ps1
    runs it before the check.
#>
$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'Assert-SampleKeys.ps1'
$utf8 = [Text.UTF8Encoding]::new($false)
$readme = 'tests/NCode.Buffers.Tests/ReadmeSamplesTests.cs'
$signatures = 'tests/NCode.Buffers.Tests/ReadmeSamples/Signatures.cs'
$other = 'tests/NCode.Buffers.Tests/OtherTests.cs'
$more = 'tests/NCode.Buffers.Tests/MoreTests.cs'
$empty = 'tests/NCode.Buffers.Tests/EmptyTests.cs'
$quickStart = 'tests/NCode.Buffers.Site.Tests/Start/QuickStartSamples.cs'
$guide = 'tests/NCode.Buffers.Site.Tests/Guides/VerifyAMacSamples.cs'
# A folder whose name only starts with the site project's holds no site samples.
$beside = 'tests/NCode.Buffers.Site.Tests.Extra/ExtraSamples.cs'
# A plan's worktree is a copy of the repository, which git ignores.
$worktree = ".claude/worktrees/plan/$readme"

# $Samples maps each fixture file to the key of its one sample, whose indented begin marker is on line 3; $Lines maps a
# file to another line 3. A $Staged file is added to the index, as a committed one is, and the others stay untracked,
# so the check must read both. A $Deleted file is added to the index and then deleted, as one deleted but not
# committed is. Every fixture repository ignores .claude/worktrees/, as this one does.
function Invoke-Case {
    param(
        [string]$Name,
        [hashtable]$Samples = @{},
        [string[]]$Problems = @(),
        [hashtable]$Lines = @{},
        [string[]]$Staged = @(),
        [string[]]$Deleted = @()
    )
    $root = Join-Path ([IO.Path]::GetTempPath()) "sample-keys-test-$([guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Path $root | Out-Null
    try {
        git init -q $root
        if ($LASTEXITCODE) { throw 'git init failed' }
        [IO.File]::WriteAllText((Join-Path $root '.gitignore'), ".claude/worktrees/`n", $utf8)
        $third = @{}
        foreach ($path in $Samples.Keys) { $third[$path] = "    // begin-snippet: $($Samples[$path])" }
        foreach ($path in $Lines.Keys) { $third[$path] = $Lines[$path] }
        foreach ($path in $third.Keys) {
            $file = Join-Path $root $path
            New-Item -ItemType Directory -Path (Split-Path -Parent $file) -Force | Out-Null
            [IO.File]::WriteAllText($file, "// A fixture.`n`n$($third[$path])`n    _ = 0;`n    // end-snippet`n", $utf8)
        }
        foreach ($path in $Staged + $Deleted) {
            git -C $root add -- $path
            if ($LASTEXITCODE) { throw "git add $path failed" }
        }
        foreach ($path in $Deleted) { Remove-Item -LiteralPath (Join-Path $root $path) }

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

Invoke-Case 'clean set' @{ $readme = 'ZeroedAfterUse'; $quickStart = 'SiteWatchAKeyDisappear'; $guide = 'SiteVerifyMacWebhook'; $beside = 'Extra' }

# MarkdownSnippets and the site's build read a marker only at the start of a line, after its indentation, and only in
# lower case, and a key ends where its line's trailing spaces start; a marker they don't read defines no key here.
Invoke-Case 'a marker after code on its line' @{ $readme = 'ZeroedAfterUse' } -Lines @{ $signatures = '    _ = "// begin-snippet: ZeroedAfterUse";' }
Invoke-Case 'a marker in other letter case' @{ $readme = 'ZeroedAfterUse' } -Lines @{ $signatures = '    // Begin-Snippet: ZeroedAfterUse' }
Invoke-Case 'spaces after a key' -Lines @{ $readme = '    // begin-snippet: ZeroedAfterUse   ' }
Invoke-Case 'a file git ignores' @{ $readme = 'ZeroedAfterUse'; $worktree = 'ZeroedAfterUse' }
Invoke-Case 'a file deleted from the work tree' @{ $readme = 'ZeroedAfterUse'; $signatures = 'ZeroedAfterUse' } -Deleted $signatures

Invoke-Case 'key that is not PascalCase' @{ $readme = 'zeroedAfterUse'; $other = 'Zeroed_After'; $more = '9Lives' } `
    -Lines @{ $signatures = '    // begin-snippet: Zeroed After Use'; $empty = '    // begin-snippet: ' } `
    -Problems "${empty}:3: '' is not PascalCase letters and digits", `
    "${more}:3: '9Lives' is not PascalCase letters and digits", `
    "${other}:3: 'Zeroed_After' is not PascalCase letters and digits", `
    "${signatures}:3: 'Zeroed After Use' is not PascalCase letters and digits", `
    "${readme}:3: 'zeroedAfterUse' is not PascalCase letters and digits"
# git lists untracked files first, then the ones in the index.
Invoke-Case 'key defined twice' @{ $signatures = 'Zeroed'; $readme = 'Zeroed'; $quickStart = 'SiteZeroed' } -Staged $readme `
    -Problems "Zeroed is defined 2 times: ${signatures}:3, ${readme}:3"
Invoke-Case 'keys that differ only in case' @{ $signatures = 'ZeroedAfterUse'; $readme = 'ZeroedAfteruse' } `
    -Problems "ZeroedAfterUse is defined 2 times: ${signatures}:3, ${readme}:3"
# The prefix is Site in that letter case, as the tools match keys.
Invoke-Case 'site key without Site' @{ $readme = 'Zeroed'; $quickStart = 'ZeroedAfterUse'; $guide = 'SITEVerifyMacWebhook' } `
    -Problems "${guide}:3: SITEVerifyMacWebhook is a site sample, so its key starts with Site", `
    "${quickStart}:3: ZeroedAfterUse is a site sample, so its key starts with Site"
Invoke-Case 'other key with Site' @{ $readme = 'SiteZeroed'; $signatures = 'SITEMap'; $quickStart = 'SiteZeroedAfterUse'; $beside = 'SiteExtra' } `
    -Problems "${beside}:3: SiteExtra starts with Site, as only a site sample's key does", `
    "${readme}:3: SiteZeroed starts with Site, as only a site sample's key does"
Invoke-Case 'no key at all' -Lines @{ $readme = '    _ = 1;' } -Problems 'No C# file git lists defines a sample.'
# Every case passed. The last one left the check's exit code, 1, in $LASTEXITCODE, which build/gate.ps1 reads.
exit 0
