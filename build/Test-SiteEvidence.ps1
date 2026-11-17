#requires -version 7
<#
.SYNOPSIS
    Checks the documentation site's evidence: every test, sample and source line its data files name, and every pin
    a page marks on a sample (ADR-1010).
.DESCRIPTION
    docs/site/_components/data/evidence.json lists the tests, site samples, source lines and outside pages the site's
    claims cite, and links a test or a source line at the commit it names, so the line a link opens never moves. This
    script fails when:
    - a test is no longer declared in its file, or the line evidence.json gives for it at that commit does not
      declare it;
    - a source range does not start with its `match` text at that commit, or runs past the end of the file;
    - a site sample's key is not defined in its file;
    - a claim, a leak card (leaks.json) or a buffer (buffers.json) names evidence that evidence.json does not list;
    - a source, an outside page or a leak card's reference names a kind that evidence.json's kinds do not list;
    - a pin on a page, a quoted text in the info string of a `cs snippet=<Key>` block, does not match exactly one line
      of that sample;
    - an API map entry names a line PublicAPI.Shipped.txt lacks, a shipped line has no entry, or a line is listed
      twice (build/Assert-ApiMap.ps1).
    When the clone lacks the pinned commit, as a shallow clone does, the checks at that commit are skipped with a
    warning; CI fetches the full history.
#>
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    $data = 'docs/site/_components/data'
    $evidence = Get-Content -Raw "$data/evidence.json" | ConvertFrom-Json -AsHashtable
    $leaks = Get-Content -Raw "$data/leaks.json" | ConvertFrom-Json -AsHashtable
    $buffers = Get-Content -Raw "$data/buffers.json" | ConvertFrom-Json -AsHashtable
    $commit = $evidence.commit
    $problems = [Collections.Generic.List[string]]::new()

    git cat-file -e "$commit^{commit}" 2>$null
    $pinned = $LASTEXITCODE -eq 0
    if (-not $pinned) { Write-Warning "Site evidence: commit $commit is not in this clone, so the lines it pins are not checked. Fetch the full history to check them." }
    $atCommit = @{}
    function Get-PinnedLines([string]$file) {
        if (-not $atCommit.ContainsKey($file)) {
            $text = git show "${commit}:$file" 2>$null
            $atCommit[$file] = if ($LASTEXITCODE -eq 0) { @($text) } else { $null }
        }
        $atCommit[$file]
    }

    foreach ($name in $evidence.tests.Keys) {
        $test = $evidence.tests[$name]
        $method = ($name -split '\.', 2)[1]
        $declaration = "\b$([regex]::Escape($method))\s*\("
        if (-not (Test-Path -LiteralPath $test.file)) { $problems.Add("test $name`: $($test.file) does not exist"); continue }
        if (-not (Select-String -LiteralPath $test.file -Pattern $declaration -Quiet)) { $problems.Add("test $name is not declared in $($test.file)") }
        if ($pinned) {
            $lines = Get-PinnedLines $test.file
            if (-not $lines) { $problems.Add("test $name`: $($test.file) is not in commit $commit") }
            elseif ($test.line -lt 1 -or $test.line -gt $lines.Count -or $lines[$test.line - 1] -notmatch $declaration) { $problems.Add("test $name`: line $($test.line) of $($test.file) at $commit does not declare it") }
        }
    }

    foreach ($key in $evidence.samples.Keys) {
        $file = $evidence.samples[$key].file
        if (-not (Test-Path -LiteralPath $file)) { $problems.Add("sample $key`: $file does not exist"); continue }
        if (-not (Select-String -LiteralPath $file -Pattern "^\s*// begin-snippet: $([regex]::Escape($key))\s*$" -CaseSensitive -Quiet)) { $problems.Add("sample $key is not defined in $file") }
    }

    foreach ($id in $evidence.sources.Keys) {
        $source = $evidence.sources[$id]
        if ($source.branch -or -not $source.lines) {
            if (-not (Test-Path -LiteralPath $source.file)) { $problems.Add("source $id`: $($source.file) does not exist") }
        }
        if ($source.branch -or -not $pinned) { continue }
        $lines = Get-PinnedLines $source.file
        if (-not $lines) { $problems.Add("source $id`: $($source.file) is not in commit $commit"); continue }
        if ($source.lines) {
            $from, $to = $source.lines
            if ($to -gt $lines.Count) { $problems.Add("source $id`: lines $from-$to run past the end of $($source.file) at $commit") }
            elseif (-not $lines[$from - 1].Contains($source.match)) { $problems.Add("source $id`: line $from of $($source.file) at $commit does not contain `"$($source.match)`"") }
        }
    }

    # Every kind an entry or a reference names is one of evidence.json's kinds.
    foreach ($group in 'sources', 'external') {
        foreach ($id in $evidence[$group].Keys) {
            $kind = $evidence[$group][$id].kind
            if ($kind -and -not $evidence.kinds.ContainsKey($kind)) { $problems.Add("$group entry $id has the kind `"$kind`", which evidence.json's kinds do not list") }
        }
    }
    foreach ($leak in $leaks.leaks) {
        foreach ($ref in $leak.evidence) { if ($ref.kind -and -not $evidence.kinds.ContainsKey($ref.kind)) { $problems.Add("leak card $($leak.id) names the kind `"$($ref.kind)`", which evidence.json's kinds do not list") } }
    }

    # Every reference the data files make resolves to an entry of evidence.json.
    function Test-Ref([string]$where, [hashtable]$ref) {
        foreach ($kind in 'test', 'sample', 'source', 'external') {
            if ($ref.ContainsKey($kind) -and -not $evidence["${kind}s" -replace 'externals', 'external'].ContainsKey($ref[$kind])) {
                $problems.Add("$where names the $kind `"$($ref[$kind])`", which evidence.json does not list")
            }
        }
    }
    foreach ($claim in $evidence.claims) {
        foreach ($t in @($claim.tests)) { if ($t) { Test-Ref "claim $($claim.id)" @{ test = $t } } }
        foreach ($s in @($claim.samples)) { if ($s) { Test-Ref "claim $($claim.id)" @{ sample = $s } } }
        foreach ($s in @($claim.sources)) { if ($s) { Test-Ref "claim $($claim.id)" @{ source = $s } } }
        foreach ($e in @($claim.external)) { if ($e) { Test-Ref "claim $($claim.id)" @{ external = $e } } }
    }
    foreach ($leak in $leaks.leaks) { foreach ($ref in $leak.evidence) { Test-Ref "leak card $($leak.id)" $ref } }
    foreach ($outcome in $buffers.outcomes) { foreach ($t in $outcome.evidence) { Test-Ref "buffer $($outcome.id)" @{ test = $t } } }
    if (Test-Path -LiteralPath "$data/api-map.json") {
        $apiMap = Get-Content -Raw "$data/api-map.json" | ConvertFrom-Json -AsHashtable
        foreach ($entry in $apiMap.entries) { Test-Ref "API map entry $($entry.id)" @{ source = $entry.source } }
    }

    # Pins: each quoted text in a sample block's info string matches exactly one line of the sample.
    $regions = @{}
    foreach ($file in @(git ls-files --cached --others --exclude-standard -- 'tests/*.cs' | Where-Object { Test-Path -LiteralPath $_ })) {
        $key = $null
        foreach ($line in Get-Content -LiteralPath $file) {
            if ($line -match '^\s*// begin-snippet: (\S+)\s*$') { $key = $Matches[1]; $regions[$key] = [Collections.Generic.List[string]]::new(); continue }
            if ($line -match '^\s*// end-snippet\s*$') { $key = $null; continue }
            if ($key) { $regions[$key].Add($line) }
        }
    }
    foreach ($page in @(git ls-files --cached --others --exclude-standard -- 'docs/site/*.mdx' | Where-Object { Test-Path -LiteralPath $_ })) {
        $number = 0
        foreach ($line in Get-Content -LiteralPath $page) {
            $number++
            if ($line -notmatch '^\s*```\w+\s+snippet=(\S+)(.*)$') { continue }
            $key, $rest = $Matches[1], $Matches[2]
            if (-not $regions.ContainsKey($key)) { continue } # The site's build reports a key no sample defines.
            foreach ($pin in [regex]::Matches($rest, '(?<![=\w])"([^"]+)"')) {
                $text = $pin.Groups[1].Value
                $count = @($regions[$key] | Where-Object { $_.Contains($text) }).Count
                if ($count -ne 1) { $problems.Add("$page`:$number`: the pin `"$text`" matches $count lines of $key, not one") }
            }
        }
    }

    # The API map covers every line of PublicAPI.Shipped.txt once. The check sets no exit code when it passes, so start
    # from a clean one; it prints 'API map: FAILED.' first, then one line per problem.
    $global:LASTEXITCODE = 0
    $apiMap = @(& (Join-Path $PSScriptRoot 'Assert-ApiMap.ps1'))
    if ($LASTEXITCODE) { foreach ($line in $apiMap | Select-Object -Skip 1) { $problems.Add($line) } }

    if ($problems.Count) { 'Site evidence: FAILED.'; $problems; exit 1 }
    'Site evidence: passed.'
}
finally { Pop-Location }
