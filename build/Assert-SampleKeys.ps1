#requires -version 7
<#
.SYNOPSIS
    Checks the keys of the C# samples: each is PascalCase letters and digits and is defined once, and only a site
    sample's key starts with Site (ADR-1010).
.DESCRIPTION
    The READMEs and the site's pages take their samples by key from every C# file, so a key is PascalCase letters and
    digits and is defined once, keys that differ only in letter case counting as one, and only a site sample's key, in
    tests/NCode.Buffers.Site.Tests, starts with Site. It reads a marker as MarkdownSnippets and the site's build do: a
    line that starts with '// begin-snippet: ' after its indentation, in that letter case, whose key is the rest of the
    line. Reads every C# file git lists, committed or not, and none it ignores, such as a plan's worktree. Prints
    nothing when every key holds; otherwise prints 'Sample keys: FAILED.' and each problem, and exits 1. Finding no key
    at all is a problem too: the READMEs always have samples, so a broken listing or pattern fails instead of passing.
    build/gate.ps1 runs it.
.PARAMETER Root
    The repository root; build/Test-AssertSampleKeys.ps1 points it at a fixture repository.
#>
[CmdletBinding()]
param([string]$Root = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
Push-Location -LiteralPath $Root
try {
    $siteSamples = 'tests/NCode.Buffers.Site.Tests/'
    $sources = @(git ls-files --cached --others --exclude-standard -- '*.cs' | Where-Object { Test-Path -LiteralPath $_ })
    $keys = @(if ($sources) {
            Select-String -LiteralPath $sources -Pattern '^\s*// begin-snippet: (.*?)\s*$' -CaseSensitive | ForEach-Object {
                [pscustomobject]@{ Key = $_.Matches[0].Groups[1].Value; At = "$([IO.Path]::GetRelativePath($PWD.Path, $_.Path).Replace('\', '/')):$($_.LineNumber)" }
            }
        })
    if (-not $keys) { 'Sample keys: FAILED.'; 'No C# file git lists defines a sample.'; exit 1 }
    $problems = @(
        $keys | Where-Object { $_.Key -cnotmatch '^[A-Z][A-Za-z0-9]*\z' } | ForEach-Object { "$($_.At): '$($_.Key)' is not PascalCase letters and digits" }
        $keys | Group-Object Key | Where-Object Count -GT 1 | ForEach-Object { "$($_.Name) is defined $($_.Count) times: $($_.Group.At -join ', ')" }
        $keys | Where-Object { $_.At.StartsWith($siteSamples, [StringComparison]::Ordinal) -and $_.Key -cnotlike 'Site*' } | ForEach-Object { "$($_.At): $($_.Key) is a site sample, so its key starts with Site" }
        $keys | Where-Object { -not $_.At.StartsWith($siteSamples, [StringComparison]::Ordinal) -and $_.Key -clike 'Site*' } | ForEach-Object { "$($_.At): $($_.Key) starts with Site, as only a site sample's key does" }
    )
    if ($problems) { 'Sample keys: FAILED.'; $problems; exit 1 }
}
finally { Pop-Location }
