#requires -version 7
<#
.SYNOPSIS
    Tests build/Assert-ApiMap.ps1 against fixture folders, offline.
.DESCRIPTION
    Covers a map that lists every shipped line once, whatever the file's comment and empty lines; a shipped line no
    entry lists; a line an entry lists that is not shipped; a line two entries list; and no map file. Each fixture is a
    folder under the temp path, not a repository, that holds PublicAPI.Shipped.txt and the map. Each case checks the
    exit code and every line the check prints. Exits 1 on the first failed check. build/gate.ps1 runs it before the
    check.
#>
$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'Assert-ApiMap.ps1'
$utf8 = [Text.UTF8Encoding]::new($false)
$shippedPath = 'src/NCode.Buffers/PublicAPI.Shipped.txt'
$mapPath = 'docs/site/_components/data/api-map.json'
$type = 'NCode.Buffers.BufferFactory'
$rent = 'static NCode.Buffers.BufferFactory.Rent(int length) -> System.Buffers.IMemoryOwner<byte>!'

# An entry of the map, which lists the lines it covers.
function Entry([string]$Id, [string[]]$Api) { @{ id = $Id; api = @($Api) } }

# $Entries is the map's entries, or $null for no map file. The shipped file holds a comment, an empty line and two
# lines, the second with trailing white space.
function Invoke-Case {
    param(
        [string]$Name,
        [hashtable[]]$Entries,
        [string[]]$Problems = @()
    )
    $root = Join-Path ([IO.Path]::GetTempPath()) "api-map-test-$([guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Path $root | Out-Null
    try {
        $shipped = Join-Path $root $shippedPath
        New-Item -ItemType Directory -Path (Split-Path -Parent $shipped) -Force | Out-Null
        [IO.File]::WriteAllText($shipped, "#nullable enable`n`n$type`n$rent  `n", $utf8)
        if ($null -ne $Entries) {
            $map = Join-Path $root $mapPath
            New-Item -ItemType Directory -Path (Split-Path -Parent $map) -Force | Out-Null
            $json = @{ groups = @(); entries = @($Entries) } | ConvertTo-Json -Depth 5
            [IO.File]::WriteAllText($map, "$json`n", $utf8)
        }

        # The check sets no exit code when it passes, so start from a clean one.
        $global:LASTEXITCODE = 0
        $printed = @(& $script -Root $root)

        $code = $LASTEXITCODE
        $expectedCode = if ($Problems) { 1 } else { 0 }
        if ($code -ne $expectedCode) { throw "exited $code instead of $expectedCode" }
        $expected = if ($Problems) { @('API map: FAILED.') + $Problems } else { @() }
        if (($printed -join "`n") -cne ($expected -join "`n")) { throw "printed:`n$($printed -join "`n")" }
    }
    catch { Write-Host "FAILED: $Name`n$_"; exit 1 }
    finally { Remove-Item -LiteralPath $root -Recurse -Force }
    Write-Host "ok: $Name"
}

Invoke-Case 'clean map' -Entries (Entry 'buffer-factory' @($type)), (Entry 'buffer-factory-rent' @($rent))
Invoke-Case 'a shipped line no entry lists' -Entries (Entry 'buffer-factory' @($type)) `
    -Problems "API map: no entry for '$rent'"
Invoke-Case 'a line that is not shipped' -Entries (Entry 'buffer-factory' @($type)), (Entry 'buffer-factory-rent' @($rent, 'static NCode.Buffers.BufferFactory.Gone() -> void')) `
    -Problems "API map: 'static NCode.Buffers.BufferFactory.Gone() -> void' (entry buffer-factory-rent) is not in PublicAPI.Shipped.txt"
Invoke-Case 'a line two entries list' -Entries (Entry 'buffer-factory' @($type, $rent)), (Entry 'buffer-factory-rent' @($rent)) `
    -Problems "API map: '$rent' is listed 2 times: buffer-factory, buffer-factory-rent"
Invoke-Case 'no map file' -Entries $null -Problems "API map: $mapPath does not exist"
# Every case passed. The last one left the check's exit code, 1, in $LASTEXITCODE, which a caller may read.
exit 0
