#requires -version 7
<#
.SYNOPSIS
    The project's gate: the sample key check, the README samples check, the site evidence check and the API map check (build/Assert-ApiMap.ps1), then the Definition of Done, build/dod.ps1, whose Format stage runs the CSharpier check (ADR-1002, ADR-1005, ADR-1010).
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
    # The READMEs and the site's pages take their samples by key from every C# file, so every marker has the one form
    # both tools read alike, a key is PascalCase and defined once, and only a site sample's key, in
    # tests/NCode.Buffers.Site.Tests, starts with Site (ADR-1010). The check's rules are tested offline first, so a
    # rule broken in the check fails here instead of passing every key.
    ./build/Test-AssertSampleKeys.ps1
    if ($LASTEXITCODE) { exit $LASTEXITCODE }
    ./build/Assert-SampleKeys.ps1
    if ($LASTEXITCODE) { exit $LASTEXITCODE }
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
    # The site's claims link tests and source lines at a pinned commit, and its pages pin sample lines by their text;
    # each must still name what it names (ADR-1010). The API map must cover every line of PublicAPI.Shipped.txt once;
    # the map check's rules are tested offline first, so a rule broken in build/Assert-ApiMap.ps1 fails here instead
    # of passing every map.
    ./build/Test-AssertApiMap.ps1
    if ($LASTEXITCODE) { exit $LASTEXITCODE }
    ./build/Test-SiteEvidence.ps1
    if ($LASTEXITCODE) { exit $LASTEXITCODE }
    $dod = @{}
    if ($Fast) { $dod.Fast = $true }
    if ($Filter) { $dod.Filter = $Filter }
    & (Join-Path $PSScriptRoot 'dod.ps1') @dod
    exit $LASTEXITCODE
}
finally { Pop-Location }
