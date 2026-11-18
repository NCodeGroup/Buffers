#requires -version 7
<#
.SYNOPSIS
    Generates the site's review checklist data from the Checklist section of each guide (ADR-1010).
.DESCRIPTION
    Reads docs/site/site.config.json, walks its sidebar in order and keeps the guides that a requiredHeadings rule
    listing '## Checklist' covers, which the site's build holds to that rule (NCodeGroup/docs ADR-1011): the slugs,
    lower-cased, that one of the rule's pages globs matches and its except list does not name. A glob reads * and ?
    within a folder and ** across folders, as the site's build does. For each guide it reads the title from the
    frontmatter and the items of its '## Checklist' section: the lines after that heading up to the next line that
    starts with '## ' or '<Related>'. An item starts with '- ', and a following line that starts with spaces continues
    it. A fenced code block is skipped: a line whose trimmed start is three or more backticks or tildes opens a fence,
    and a line of the same character and at least that length, with nothing else on it, closes it. No line inside a
    fence, and no fence line, is the heading, the end of the section or an item. These are errors: a guide with no
    'title:' line in its frontmatter, a covered guide without a '## Checklist' section, a configuration without a
    requiredHeadings rule that lists '## Checklist', and a pages glob with a character other than *, ** and ?.

    Writes docs/site/_components/data/checklists.json, which the review checklist page renders, so the page never holds
    a second copy of a guide's checklist. With -Check, writes nothing: prints 'Review checklist: FAILED.' and exits 1
    when the file differs from what the guides give, or is missing. build/gate.ps1 runs it with -Check.
.PARAMETER Check
    Compares the generated text with the file instead of writing it.
.PARAMETER Root
    The repository root; build/Test-UpdateReviewChecklist.ps1 points it at a fixture folder.
#>
[CmdletBinding()]
param(
    [switch]$Check,
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)
$ErrorActionPreference = 'Stop'
Push-Location -LiteralPath $Root
try {
    $site = 'docs/site'
    $output = "$site/_components/data/checklists.json"
    $config = Get-Content -Raw -LiteralPath "$site/site.config.json" | ConvertFrom-Json -AsHashtable

    # The rules the site's build holds the guides to (NCodeGroup/docs ADR-1011); the ones that list a Checklist name the
    # guides this page gathers.
    $rules = @($config.requiredHeadings | Where-Object { $_.headings -ccontains '## Checklist' })
    if (-not $rules) { throw "Review checklist: $site/site.config.json has no requiredHeadings rule that lists '## Checklist'." }
    # The globs as the site's build reads them: * and ? within a folder, ** across folders.
    function ConvertTo-GlobPattern([string]$Glob) {
        if ($Glob -match '[\[\]{}!()|+@]') { throw "Review checklist: the pages glob '$Glob' in $site/site.config.json uses a pattern this script does not read; use *, ** and ?." }
        $pattern = [regex]::Escape($Glob) -replace '\\\*\\\*/', '(?:.*/)?' -replace '\\\*\\\*', '.*' -replace '\\\*', '[^/]*' -replace '\\\?', '[^/]'
        "^$pattern$"
    }
    # Every glob is read once here, so a pattern this script does not read stops it whatever the sidebar holds.
    $patterns = @{}
    foreach ($rule in $rules) { foreach ($glob in $rule.pages) { $patterns[$glob] = ConvertTo-GlobPattern $glob } }
    function Test-Covered([string]$Slug) {
        foreach ($rule in $rules) {
            $matched = @($rule.pages | Where-Object { $Slug -cmatch $patterns[$_] }).Count
            if ($matched -and -not (@($rule.except) -ccontains $Slug)) { return $true }
        }
        $false
    }

    # An item of the sidebar is a slug, an object with a slug, or a group with items.
    function Get-Slugs($items) {
        foreach ($item in $items) {
            if ($item -is [string]) { $item }
            elseif ($item.ContainsKey('items')) { Get-Slugs $item.items }
            elseif ($item.ContainsKey('slug')) { $item.slug }
        }
    }

    $groups = @(foreach ($slug in @(Get-Slugs $config.sidebar | Where-Object { Test-Covered $_.ToLowerInvariant() })) {
        $lines = @(Get-Content -LiteralPath "$site/$slug.mdx")
        $titleLine = $lines | Where-Object { $_.StartsWith('title: ', [StringComparison]::Ordinal) } | Select-Object -First 1
        if ($null -eq $titleLine) { throw "Review checklist: $site/$slug.mdx has no 'title:' line in its frontmatter." }
        $title = $titleLine.Substring(7).Trim()

        # A line inside a fenced code block, or a fence line, is not part of the guide's structure.
        $fenced = [bool[]]::new($lines.Count)
        $fenceChar = [char]0
        $fenceLength = 0
        for ($i = 0; $i -lt $lines.Count; $i++) {
            $trimmed = $lines[$i].TrimStart()
            if ($fenceLength) {
                $fenced[$i] = $true
                $closing = $trimmed.TrimEnd()
                if ($closing.Length -ge $fenceLength -and $closing.Trim($fenceChar).Length -eq 0) { $fenceLength = 0 }
            }
            elseif ($trimmed -match '^(`{3,}|~{3,})') {
                $fenced[$i] = $true
                $fenceChar = $Matches[1][0]
                $fenceLength = $Matches[1].Length
            }
        }

        $start = -1
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if (-not $fenced[$i] -and $lines[$i] -ceq '## Checklist') { $start = $i; break }
        }
        if ($start -lt 0) { throw "Review checklist: $site/$slug.mdx has no '## Checklist' section, which the requiredHeadings rule in $site/site.config.json requires." }
        $items = [Collections.Generic.List[string]]::new()
        for ($i = $start + 1; $i -lt $lines.Count; $i++) {
            if ($fenced[$i]) { continue }
            $line = $lines[$i]
            if ($line.StartsWith('## ', [StringComparison]::Ordinal) -or $line.StartsWith('<Related>', [StringComparison]::Ordinal)) { break }
            if ($line.StartsWith('- ', [StringComparison]::Ordinal)) { $items.Add($line.Substring(2).Trim()) }
            elseif ($line.StartsWith(' ', [StringComparison]::Ordinal) -and $items.Count) { $items[$items.Count - 1] += ' ' + $line.Trim() }
        }
        [ordered]@{ slug = $slug; title = $title; items = @($items) }
    })

    $data = [ordered]@{
        '$comment' = "Generated by build/Update-ReviewChecklist.ps1 from each guide's Checklist section, in sidebar order; do not edit."
        groups = $groups
    }
    $text = ($data | ConvertTo-Json -Depth 5).Replace("`r`n", "`n") + "`n"
    $path = Join-Path $PWD.Path $output
    if ($Check) {
        if (-not (Test-Path -LiteralPath $path) -or [IO.File]::ReadAllText($path) -cne $text) {
            "Review checklist: FAILED. $output is out of date; run ./build/Update-ReviewChecklist.ps1."
            exit 1
        }
    }
    else { [IO.File]::WriteAllText($path, $text, [Text.UTF8Encoding]::new($false)) }
}
finally { Pop-Location }
