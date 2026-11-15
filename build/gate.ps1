#requires -version 7
<#
.SYNOPSIS
    The project's gate: the sample key check and the README samples check, then the Definition of Done, build/dod.ps1, whose Format stage runs the CSharpier check (ADR-1002, ADR-1005, ADR-1010).
.DESCRIPTION
    build/dod.ps1 is template-owned and has no stage for the samples, so this script runs those checks before it, as
    project-template ADR-0117 says. -Fast and -Filter are passed on to build/dod.ps1.
#>
[CmdletBinding()]
param([switch]$Fast, [string]$Filter)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    dotnet tool restore
    if ($LASTEXITCODE) { exit $LASTEXITCODE }
    # The READMEs and the site's pages take their samples by key from every C# file, so a key is defined once, and
    # only a site sample's key, in tests/NCode.Buffers.Site.Tests, starts with Site (ADR-1010).
    $siteSamples = 'tests/NCode.Buffers.Site.Tests/'
    $sources = @(git ls-files --cached --others --exclude-standard -- '*.cs' | Where-Object { Test-Path -LiteralPath $_ })
    $keys = @(Select-String -LiteralPath $sources -Pattern '^\s*// begin-snippet: (\S+)' -CaseSensitive | ForEach-Object {
            [pscustomobject]@{ Key = $_.Matches[0].Groups[1].Value; At = "$([IO.Path]::GetRelativePath($PWD.Path, $_.Path).Replace('\', '/')):$($_.LineNumber)" }
        })
    $problems = @(
        $keys | Group-Object Key | Where-Object Count -GT 1 | ForEach-Object { "$($_.Name) is defined $($_.Count) times: $($_.Group.At -join ', ')" }
        $keys | Where-Object { $_.At.StartsWith($siteSamples) -and $_.Key -cnotlike 'Site*' } | ForEach-Object { "$($_.At): $($_.Key) is a site sample, so its key starts with Site" }
        $keys | Where-Object { -not $_.At.StartsWith($siteSamples) -and $_.Key -clike 'Site*' } | ForEach-Object { "$($_.At): $($_.Key) starts with Site, as only a site sample's key does" }
    )
    if ($problems) { 'Sample keys: FAILED.'; $problems; exit 1 }
    # The READMEs' C# samples are copied from the tests (ADR-1005); a sample changed in only one place fails here.
    # MarkdownSnippets would also rewrite a site page (.mdx) into HTML comments that MDX rejects, so mdsnippets.json
    # excludes *.mdx, and the pages are hashed too: a page the tool changed fails here (ADR-1010).
    $markdown = { git ls-files --cached --others --exclude-standard -- '*.md' '*.mdx' | Where-Object { Test-Path -LiteralPath $_ } | ForEach-Object { "$((Get-FileHash -LiteralPath $_).Hash) $_" } }
    $before = & $markdown
    dotnet mdsnippets
    if ($LASTEXITCODE) { 'README samples: FAILED. dotnet mdsnippets could not merge the samples.'; exit 1 }
    $changed = @(Compare-Object @(& $markdown) @($before) | ForEach-Object { ($_.InputObject -split ' ', 2)[1] } | Select-Object -Unique)
    $pages = @($changed -like '*.mdx')
    if ($pages) { "Site pages: FAILED. dotnet mdsnippets rewrote $($pages -join ', '); restore it, and keep *.mdx in ExcludeMarkdownFiles of mdsnippets.json."; exit 1 }
    if ($changed) { 'README samples: FAILED. dotnet mdsnippets updated a README from the tests; commit the result.'; exit 1 }
    # The baseline script edits files in an automated pull request, so its branches are tested offline (ADR-1007).
    ./build/Test-UpdateBaseline.ps1
    if ($LASTEXITCODE) { exit $LASTEXITCODE }
    $dod = @{}
    if ($Fast) { $dod.Fast = $true }
    if ($Filter) { $dod.Filter = $Filter }
    & (Join-Path $PSScriptRoot 'dod.ps1') @dod
    exit $LASTEXITCODE
}
finally { Pop-Location }
