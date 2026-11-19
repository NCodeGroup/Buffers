#requires -version 7
<#
.SYNOPSIS
    Builds, checks, serves or previews the site in docs/site/ with the NCodeGroup/docs toolchain (ADR-1009), from the
    main checkout and from a plan's worktree under .claude/worktrees/ alike.
.DESCRIPTION
    Runs node <toolchain>/scripts/site.mjs <Command> <this checkout> and exits with node's exit code. It runs node
    itself, never npm, so nothing reaches npm's shim, and it finds the toolchain, a clone of NCodeGroup/docs, beside
    the main checkout, so the command works from any current folder.

    build writes the site into -Out, outside this repository, and stops on a sample key no test defines, a file a page
    includes that is not there, a component that reads a token the brand does not offer, or a guide without its
    headings. check loads each page of that build at 1440, 1280, 1024 and 390 px, as the documentation site workflow
    does, and names every element that leaves its content column or hides content sideways, in the Chromium-based
    browser -Browser or DOCS_BROWSER names, or the one 'npm run browser' installs in the toolchain. dev serves the site
    at http://localhost:4321/ by default and follows each change. preview builds as the workflow does and
    serves that build.

    A toolchain clone runs one command at a time: site.mjs says 'This toolchain checkout is in use' when another
    build is running. Node.js must be on PATH at the version the toolchain's package.json names in engines. Nothing is
    written into this repository.
.PARAMETER Command
    What to run: build, check, dev or preview.
.PARAMETER Out
    The folder build writes the site to and check reads it from; site.mjs refuses a folder inside this repository.
.PARAMETER Port
    The port dev and preview serve on; site.mjs picks the default when it is not given.
.PARAMETER Browser
    The Chromium-based browser check uses, as a path or a command name; DOCS_BROWSER names one too.
.PARAMETER Toolchain
    The folder of the NCodeGroup/docs clone; by default a folder named docs beside the main checkout.
.EXAMPLE
    ./build/site.ps1 build -Out ../site-out
.EXAMPLE
    ./build/site.ps1 check -Out ../site-out -Browser "$env:ProgramFiles\Google\Chrome\Application\chrome.exe"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateSet('build', 'check', 'dev', 'preview')]
    [string]$Command,
    [string]$Out,
    [ValidateRange(1, 65535)]
    [int]$Port,
    [string]$Browser,
    [string]$Toolchain
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Toolchain) {
    $common = git -C $root rev-parse --path-format=absolute --git-common-dir
    if ($LASTEXITCODE) { exit 1 }
    # The common Git folder is the main checkout's .git, for the main checkout and every worktree of it alike.
    $Toolchain = Join-Path (Split-Path -Parent (Split-Path -Parent $common)) 'docs'
}
$resolve = { param($path) $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($path) }
$Toolchain = & $resolve $Toolchain
if ($Out) { $Out = & $resolve $Out }
if ($Browser -and ($Browser.Contains('\') -or $Browser.Contains('/'))) { $Browser = & $resolve $Browser }
$site = Join-Path $Toolchain 'scripts/site.mjs'
if (-not (Test-Path -LiteralPath $site -PathType Leaf)) {
    Write-Output "Site: FAILED. No NCodeGroup/docs toolchain at ${Toolchain}: clone https://github.com/NCodeGroup/docs there and set it up as its README says, or name its folder with -Toolchain."
    exit 1
}
if (-not (Get-Command node -CommandType Application -ErrorAction SilentlyContinue)) {
    Write-Output "Site: FAILED. Node.js is not on PATH: install the version the toolchain's package.json names in engines."
    exit 1
}
$arguments = @($site, $Command, $root)
if ($Out) { $arguments += '--out', $Out }
if ($PSBoundParameters.ContainsKey('Port')) { $arguments += '--port', $Port }
if ($Browser) { $arguments += '--browser', $Browser }
& node @arguments
exit $LASTEXITCODE
