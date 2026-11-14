<#
.SYNOPSIS
Runs the tests with coverage, lists every line no test runs, and fails below the minimum coverage.

.DESCRIPTION
Runs the NCode.Buffers.Tests project with the coverlet 'XPlat Code Coverage' collector, reads the Cobertura report,
prints every source line that no test runs, and prints the line and branch coverage. The script exits with 1 when the
line coverage or the branch coverage is below its minimum, and with 0 otherwise.

.PARAMETER MinimumLineRate
The lowest accepted line coverage, from 0.0 to 1.0. The default is 0.98.

.PARAMETER MinimumBranchRate
The lowest accepted branch coverage, from 0.0 to 1.0. The default is 0.98.

.EXAMPLE
./build/Test-Coverage.ps1
#>
[CmdletBinding()]
param(
    [ValidateRange(0.0, 1.0)][double]$MinimumLineRate = 0.98,
    [ValidateRange(0.0, 1.0)][double]$MinimumBranchRate = 0.98
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$results = Join-Path ([IO.Path]::GetTempPath()) "ncode-buffers-coverage-$([guid]::NewGuid().ToString('N'))"
$exitCode = 0

try {
    dotnet test (Join-Path $root 'NCode.Buffers.Tests') --collect 'XPlat Code Coverage' --results-directory $results
    if ($LASTEXITCODE) { throw "dotnet test failed with exit code $LASTEXITCODE." }

    $reports = @(Get-ChildItem -LiteralPath $results -Recurse -Filter 'coverage.cobertura.xml')
    if ($reports.Count -eq 0) { throw 'dotnet test wrote no coverage.cobertura.xml report.' }

    foreach ($report in $reports) {
        [xml]$xml = Get-Content -LiteralPath $report.FullName -Raw
        $source = @($xml.coverage.sources.source)[0]

        $entries = foreach ($class in $xml.coverage.packages.package.classes.class) {
            foreach ($line in $class.lines.line) {
                [pscustomobject]@{
                    file   = [IO.Path]::GetRelativePath($root, (Join-Path $source $class.filename))
                    number = [int]$line.number
                    hits   = [int]$line.hits
                }
            }
        }

        $uncovered = @($entries |
            Group-Object -Property file, number |
            Where-Object { -not ($_.Group | Where-Object { $_.hits -gt 0 }) } |
            ForEach-Object { $_.Group[0] })

        foreach ($group in ($uncovered | Group-Object -Property file | Sort-Object -Property Name)) {
            $file = $group.Name
            $numbers = @($group.Group | ForEach-Object { $_.number } | Sort-Object) -join ', '
            Write-Host "${file}: lines $numbers not covered"
        }

        $lineRate = [double]$xml.coverage.'line-rate'
        $branchRate = [double]$xml.coverage.'branch-rate'
        Write-Host ('Line coverage {0:P1} (minimum {1:P1}), branch coverage {2:P1} (minimum {3:P1})' -f $lineRate, $MinimumLineRate, $branchRate, $MinimumBranchRate)

        if ($lineRate -lt $MinimumLineRate -or $branchRate -lt $MinimumBranchRate) { $exitCode = 1 }
    }
}
finally {
    Remove-Item -LiteralPath $results -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ($exitCode ? 'Coverage: FAILED' : 'Coverage: PASSED')
exit $exitCode
