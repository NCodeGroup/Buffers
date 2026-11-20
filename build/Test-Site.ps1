#requires -version 7
<#
.SYNOPSIS
    Tests build/site.ps1 against a fake toolchain and a fake node, offline.
.DESCRIPTION
    Covers the script run from the main checkout, which finds the toolchain beside it; the script run from a worktree
    of that checkout, which finds the same toolchain and passes -Out, -Port and -Browser on; node's exit code, which
    the script returns; a missing toolchain; and no node on PATH. The fixture is a temporary Git repository with one
    worktree, a toolchain folder beside it that holds only scripts/site.mjs, and a fake node that logs its arguments,
    so it needs neither Node.js nor the NCodeGroup/docs toolchain. Each case checks the exit code and, where node
    runs, the arguments it was given. Exits 1 on the first failed check. build/gate.ps1 runs it.
#>
$ErrorActionPreference = 'Stop'
$sitePs1 = Join-Path $PSScriptRoot 'site.ps1'
$guid = [guid]::NewGuid().ToString('N')
$root = Join-Path ([IO.Path]::GetTempPath()) "site-test-$guid"
$main = Join-Path $root 'main'
$wt = Join-Path $main '.claude/worktrees/wt'
$log = Join-Path $root 'node.log'
$marker = "fake toolchain $guid"
$savedPath = $env:PATH
$savedLog = $env:SITE_TEST_LOG
$savedExit = $env:SITE_TEST_EXIT

# Runs a native command of the fixture quietly and stops when it fails.
function Invoke-Git {
    $null = git @args 2>&1
    if ($LASTEXITCODE) { throw "git $($args -join ' ') exited $LASTEXITCODE" }
}

# Runs the script with the log deleted first; returns its exit code, its output and the lines node logged.
function Invoke-Site {
    param([string]$Script, [hashtable]$Parameters)
    Remove-Item -LiteralPath $log -ErrorAction SilentlyContinue
    $global:LASTEXITCODE = 0
    $output = & $Script @Parameters 2>&1 | Out-String
    $code = $LASTEXITCODE
    $lines = if (Test-Path -LiteralPath $log) { @(Get-Content -LiteralPath $log) } else { @() }
    [pscustomobject]@{ Code = $code; Output = $output; Lines = $lines }
}

function Assert-Code($Run, [int]$Expected) {
    if ($Run.Code -ne $Expected) { throw "exited $($Run.Code) instead of ${Expected}:`n$($Run.Output)" }
}

# A logged path is checked by reading a file through it, never by comparing its spelling.
function Assert-FileHolds($Run, [int]$Index, [string]$Name, [string]$Text) {
    $line = if ($Index -lt $Run.Lines.Count) { $Run.Lines[$Index] }
    $path = if ($line -and $Name) { Join-Path $line $Name } else { $line }
    $isFile = $path -and (Test-Path -LiteralPath $path -PathType Leaf)
    $first = if ($isFile) { Get-Content -LiteralPath $path -TotalCount 1 }
    if ($first -cne $Text) { throw "line $($Index + 1) of the log, '$line', does not lead to $Name with '$Text'" }
}

function Assert-Line($Run, [int]$Index, [string]$Text) {
    if ($Index -ge $Run.Lines.Count -or $Run.Lines[$Index] -cne $Text) {
        throw "line $($Index + 1) of the log is '$($Run.Lines[$Index])' instead of '$Text'"
    }
}

# The log holds the option, then its value on the next line.
function Assert-Option($Run, [string]$Option, [string]$Value) {
    $at = [array]::IndexOf($Run.Lines, $Option)
    if ($at -lt 0 -or $at + 1 -ge $Run.Lines.Count -or $Run.Lines[$at + 1] -cne $Value) {
        throw "the log has no $Option followed by '$Value':`n$($Run.Lines -join "`n")"
    }
}

function Invoke-Case {
    param([string]$Name, [scriptblock]$Body)
    try { & $Body }
    catch { Write-Host "FAILED: $Name`n$_"; exit 1 }
    Write-Host "ok: $Name"
}

New-Item -ItemType Directory -Path $root | Out-Null
try {
    # The main checkout, a Git repository that holds the script; its toolchain is the folder docs beside it.
    New-Item -ItemType Directory -Path (Join-Path $main 'build') -Force | Out-Null
    Copy-Item -LiteralPath $sitePs1 -Destination (Join-Path $main 'build/site.ps1')
    Invoke-Git -C $main init -q -b main
    Invoke-Git -C $main add -A
    Invoke-Git -C $main -c user.name=test -c user.email=test@example.com -c commit.gpgsign=false commit -q -m fixture
    Invoke-Git -C $main worktree add -q $wt -b wt
    # Tells the two checkouts apart, whatever the spelling of their paths.
    Set-Content -LiteralPath (Join-Path $main 'whoami.txt') -Value 'main' -NoNewline
    Set-Content -LiteralPath (Join-Path $wt 'whoami.txt') -Value 'worktree' -NoNewline
    New-Item -ItemType Directory -Path (Join-Path $root 'docs/scripts') -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $root 'docs/scripts/site.mjs') -Value $marker -NoNewline
    New-Item -ItemType Directory -Path (Join-Path $root 'empty') | Out-Null

    # The fake node logs its arguments, one per line, and exits with SITE_TEST_EXIT.
    $bin = Join-Path $root 'bin'
    New-Item -ItemType Directory -Path $bin | Out-Null
    Set-Content -LiteralPath (Join-Path $bin 'node.ps1') -NoNewline -Value (
        "Set-Content -LiteralPath `$env:SITE_TEST_LOG -Value `$args`n" +
        "exit [int]`$env:SITE_TEST_EXIT`n")
    if ($IsWindows) {
        Set-Content -LiteralPath (Join-Path $bin 'node.cmd') -NoNewline -Value (
            "@pwsh -NoProfile -File `"%~dp0node.ps1`" %*`r`n" +
            "@exit /b %ERRORLEVEL%`r`n")
    }
    else {
        $launcher = Join-Path $bin 'node'
        Set-Content -LiteralPath $launcher -NoNewline -Value (
            "#!/bin/sh`n" +
            "exec pwsh -NoProfile -File `"`$(dirname `"`$0`")/node.ps1`" `"`$@`"`n")
        chmod +x $launcher
        if ($LASTEXITCODE) { throw "chmod exited $LASTEXITCODE" }
    }
    $env:PATH = $bin + [IO.Path]::PathSeparator + $savedPath
    $env:SITE_TEST_LOG = $log
    $env:SITE_TEST_EXIT = '0'
    $out = Join-Path $root 'out'

    Invoke-Case 'the main checkout finds the toolchain beside it' {
        $run = Invoke-Site (Join-Path $main 'build/site.ps1') @{ Command = 'build'; Out = $out }
        Assert-Code $run 0
        Assert-FileHolds $run 0 '' $marker
        Assert-Line $run 1 'build'
        Assert-FileHolds $run 2 'whoami.txt' 'main'
        Assert-Line $run 3 '--out'
        if ([IO.Path]::GetFullPath($run.Lines[4]) -ne [IO.Path]::GetFullPath($out)) {
            throw "line 5 of the log is '$($run.Lines[4])' instead of '$out'"
        }
    }

    Invoke-Case 'a worktree finds the same toolchain' {
        $run = Invoke-Site (Join-Path $wt 'build/site.ps1') @{
            Command = 'check'; Out = $out; Port = 4500; Browser = 'chrome'
        }
        Assert-Code $run 0
        Assert-FileHolds $run 0 '' $marker
        Assert-Line $run 1 'check'
        Assert-FileHolds $run 2 'whoami.txt' 'worktree'
        Assert-Option $run '--out' $out
        Assert-Option $run '--port' '4500'
        Assert-Option $run '--browser' 'chrome'
    }

    Invoke-Case "node's exit code passes through" {
        $env:SITE_TEST_EXIT = '3'
        try { $run = Invoke-Site (Join-Path $main 'build/site.ps1') @{ Command = 'preview' } }
        finally { $env:SITE_TEST_EXIT = '0' }
        Assert-Code $run 3
    }

    Invoke-Case 'a missing toolchain fails' {
        $run = Invoke-Site (Join-Path $main 'build/site.ps1') @{
            Command = 'build'; Toolchain = (Join-Path $root 'nowhere')
        }
        Assert-Code $run 1
        if ($run.Output -notmatch 'Site: FAILED\. No NCodeGroup/docs toolchain') { throw "printed:`n$($run.Output)" }
        if (Test-Path -LiteralPath $log) { throw 'node ran' }
    }

    Invoke-Case 'no node on PATH fails' {
        $env:PATH = Join-Path $root 'empty'
        try {
            $run = Invoke-Site (Join-Path $main 'build/site.ps1') @{ Command = 'build'; Toolchain = "$root/docs" }
        }
        finally { $env:PATH = $bin + [IO.Path]::PathSeparator + $savedPath }
        Assert-Code $run 1
        if ($run.Output -notmatch 'Node\.js is not on PATH') { throw "printed:`n$($run.Output)" }
    }
}
finally {
    $env:PATH = $savedPath
    $env:SITE_TEST_LOG = $savedLog
    $env:SITE_TEST_EXIT = $savedExit
    try { $null = git -C $main worktree remove --force $wt 2>&1 } catch { }
    Remove-Item -LiteralPath $root -Recurse -Force
}
# Every case passed.
exit 0
