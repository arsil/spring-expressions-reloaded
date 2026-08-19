<#
.SYNOPSIS
    Runs the net40 tests via nunit3-console, for every test project or for one.

.DESCRIPTION
    net40 is the one target framework that cannot be run with `dotnet test`:
    Microsoft.NET.Test.Sdk has an empty .NETFramework4.0 dependency group (no test host), so VSTest
    has nothing to run tests with. `dotnet test -f net40` therefore discovers nothing and EXITS 0 -
    never read that as "tests pass". Every other TFM is a plain `dotnet test`:

        dotnet test SpringExpressionsTests\SpringExpressionsTests.csproj -f net472
        dotnet test SpringExpressionsTests\SpringExpressionsTests.csproj -f net10.0

    This script is the net40 stand-in for `dotnet test SpringExpressions.sln`, so like that command it
    runs EVERY test project by default. Use -Suite to narrow it to one.

    Notes on nunit3-console, which differs from `dotnet test`:
      * Its EXIT CODE IS THE FAILURE COUNT, not a pass/fail boolean. 22 failures => exit 22. This
        script does not treat a non-zero runner exit as an error, and exits 0 itself unless a run
        failed to produce results at all.
      * There is no --filter. Use --test=<full.name> for one test or one fixture, or --where=<expr>.
      * [Test, Explicit] tests are reported as skipped and counted in `total`, whereas VSTest omits
        them entirely, so net40 totals run two higher than the same suite under `dotnet test`.

.PARAMETER Suite
    Which test project to run:
      All         - every test project, one after the other (default), as `dotnet test` does on the solution
      Tests       - SpringExpressionsTests, the current suite
      LegacyTests - SpringExpressionsLegacyTests, the frozen upstream suite
    Each writes its own result file: TestResults\net40.xml and TestResults\net40-legacy.xml.

.PARAMETER Test
    Optional fully-qualified test or fixture name, passed through to --test=. Applied to every suite
    selected. Both projects contain fixtures of the same name (SpringExpressions.ExpressionEvaluatorTests
    among others), so with -Suite All a shared name runs in both, which is usually what you want when
    comparing them. A suite with no match is reported and skipped rather than failing the run.

.EXAMPLE
    .\run-net40-tests.ps1
    .\run-net40-tests.ps1 -Suite LegacyTests
    .\run-net40-tests.ps1 -Test SpringExpressions.OpANDTests
    .\run-net40-tests.ps1 -Suite Tests -Test SpringExpressionsTests.Expressions.NumericPromotionTests
#>
[CmdletBinding()]
param(
    [string] $Test,

    [ValidateSet('All', 'Tests', 'LegacyTests')]
    [string] $Suite = 'All'
)

$ErrorActionPreference = 'Stop'
$repo = $PSScriptRoot

$suiteTable = [ordered] @{
    'Tests'       = @{ Project = 'SpringExpressionsTests';       Result = 'net40.xml' }
    'LegacyTests' = @{ Project = 'SpringExpressionsLegacyTests'; Result = 'net40-legacy.xml' }
}

$selected = if ($Suite -eq 'All') { @($suiteTable.Keys) } else { @($Suite) }

$resultsDir = Join-Path $repo 'TestResults'
if (-not (Test-Path $resultsDir)) { New-Item -ItemType Directory $resultsDir | Out-Null }

$summaries = New-Object System.Collections.Generic.List[object]

foreach ($name in $selected) {
    $projectName = $suiteTable[$name].Project
    $project     = Join-Path $repo "$projectName\$projectName.csproj"
    $resultFile  = Join-Path $resultsDir $suiteTable[$name].Result

    Write-Host ''
    Write-Host "=== $projectName (net40) ===" -ForegroundColor Cyan
    & dotnet build $project -f net40 --nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed for $projectName (exit $LASTEXITCODE)." }

    # Resolve the runner from the package's GeneratePathProperty rather than assuming
    # $env:USERPROFILE\.nuget\packages, which is wrong when NUGET_PACKAGES is redirected.
    $pkgRoot = (& dotnet msbuild $project -getProperty:PkgNUnit_ConsoleRunner -p:TargetFramework=net40).Trim()
    if (-not $pkgRoot) { throw 'Could not resolve $(PkgNUnit_ConsoleRunner). Run `dotnet restore` first.' }

    $runner = Join-Path $pkgRoot 'tools\nunit3-console.exe'
    if (-not (Test-Path $runner)) { throw "nunit3-console.exe not found at $runner" }

    $assembly = Join-Path $repo "$projectName\bin\Release\net40\$projectName.dll"
    if (-not (Test-Path $assembly)) { throw "Test assembly not found at $assembly" }

    # Removed first so a run that produces nothing cannot be read as the previous run's results.
    if (Test-Path $resultFile) { Remove-Item $resultFile -Force -Confirm:$false }

    # --work keeps nunit-agent_*.log and any other runner output inside TestResults instead of
    # scattering it across the repo root.
    $runnerArgs = @($assembly, '--noheader', "--result:$resultFile", "--work:$resultsDir")
    if ($Test) { $runnerArgs += "--test=$Test" }

    Write-Host "Running via $(Split-Path $pkgRoot -Leaf)..." -ForegroundColor Cyan
    & $runner @runnerArgs | Out-Host
    $runnerExit = $LASTEXITCODE

    if (-not (Test-Path $resultFile)) {
        if ($Test) {
            Write-Host "  no test matching '$Test' in $projectName - skipped" -ForegroundColor DarkYellow
            continue
        }
        throw "Runner produced no result file for $projectName (exit $runnerExit)."
    }

    [xml] $xml = Get-Content $resultFile
    $r = $xml.'test-run'

    if ($Test -and [int] $r.total -eq 0) {
        Write-Host "  no test matching '$Test' in $projectName - skipped" -ForegroundColor DarkYellow
        continue
    }

    $summaries.Add([pscustomobject] @{
        Suite   = $projectName
        Total   = [int] $r.total
        Passed  = [int] $r.passed
        Failed  = [int] $r.failed
        Skipped = [int] $r.skipped
        Exit    = $runnerExit
        Result  = $resultFile
    })
}

Write-Host ''
foreach ($s in $summaries) {
    Write-Host ('net40 [{0}]: total={1} passed={2} failed={3} skipped={4} (runner exit {5} = failure count)' -f `
        $s.Suite, $s.Total, $s.Passed, $s.Failed, $s.Skipped, $s.Exit) -ForegroundColor Yellow
    Write-Host "  results: $($s.Result)"
}

if ($summaries.Count -gt 1) {
    $t = ($summaries | Measure-Object Total   -Sum).Sum
    $p = ($summaries | Measure-Object Passed  -Sum).Sum
    $f = ($summaries | Measure-Object Failed  -Sum).Sum
    $k = ($summaries | Measure-Object Skipped -Sum).Sum
    Write-Host ('net40 [ALL]: total={0} passed={1} failed={2} skipped={3}' -f $t, $p, $f, $k) -ForegroundColor Yellow
}

if ($summaries.Count -eq 0) { Write-Host 'Nothing ran.' -ForegroundColor DarkYellow }

# The compiled backend is WIP, so failures are expected. Compare against the recorded baselines in
# CLAUDE.md rather than expecting zero. Exit 0 unless a run failed to produce results at all.
exit 0
