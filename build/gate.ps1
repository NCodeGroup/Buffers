#requires -version 7
<#
.SYNOPSIS
    The project's gate: the README samples check, then the Definition of Done, build/dod.ps1, whose Format stage runs the CSharpier check (ADR-1002, ADR-1005).
.DESCRIPTION
    build/dod.ps1 is template-owned and has no README stage, so this script runs that check before it, as
    project-template ADR-0117 says. -Fast and -Filter are passed on to build/dod.ps1.
#>
[CmdletBinding()]
param([switch]$Fast, [string]$Filter)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    dotnet tool restore
    if ($LASTEXITCODE) { exit $LASTEXITCODE }
    # The READMEs' C# samples are copied from the tests (ADR-1005); a sample changed in only one place fails here.
    $markdown = { git ls-files --cached --others --exclude-standard -- '*.md' | Where-Object { Test-Path -LiteralPath $_ } | ForEach-Object { "$((Get-FileHash -LiteralPath $_).Hash) $_" } }
    $before = & $markdown
    dotnet mdsnippets
    if ($LASTEXITCODE) { 'README samples: FAILED. dotnet mdsnippets could not merge the samples.'; exit 1 }
    if (Compare-Object @(& $markdown) @($before)) { 'README samples: FAILED. dotnet mdsnippets updated a README from the tests; commit the result.'; exit 1 }
    $dod = @{}
    if ($Fast) { $dod.Fast = $true }
    if ($Filter) { $dod.Filter = $Filter }
    & (Join-Path $PSScriptRoot 'dod.ps1') @dod
    exit $LASTEXITCODE
}
finally { Pop-Location }
