#requires -version 7
<#
.SYNOPSIS
    The project's gate: the CSharpier formatting check, then the Definition of Done, build/dod.ps1 (ADR-1002).
.DESCRIPTION
    build/dod.ps1 is template-owned and has no formatter stage, so this script runs the check before it, as
    project-template ADR-0117 says. -Fast and -Filter are passed on to build/dod.ps1.
#>
[CmdletBinding()]
param([switch]$Fast, [string]$Filter)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    dotnet tool restore
    if ($LASTEXITCODE) { exit $LASTEXITCODE }
    dotnet csharpier check .
    if ($LASTEXITCODE) { 'Formatting: FAILED. Run dotnet csharpier format . and commit the result.'; exit 1 }
    $dod = @{}
    if ($Fast) { $dod.Fast = $true }
    if ($Filter) { $dod.Filter = $Filter }
    & (Join-Path $PSScriptRoot 'dod.ps1') @dod
    exit $LASTEXITCODE
}
finally { Pop-Location }
