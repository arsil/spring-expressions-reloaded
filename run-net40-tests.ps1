<#
.SYNOPSIS
    Runs the net40 test suite via nunit3-console.

.DESCRIPTION
    net40 is the one target framework that cannot be run with `dotnet test`:
    Microsoft.NET.Test.Sdk has an empty .NETFramework4.0 dependency group (no test host), so VSTest
    has nothing to run tests with. The other two TFMs are plain `dotnet test`:

        dotnet test SpringExpressionsTests\SpringExpressionsTests.csproj -f net472
        dotnet test SpringExpressionsTests\SpringExpressionsTests.csproj -f netcoreapp2.1

    Notes on nunit3-console, which differs from `dotnet test`:
      * Its EXIT CODE IS THE FAILURE COUNT, not a pass/fail boolean. 63 failures => exit 63.
        This script therefore does not treat a non-zero runner exit as a script error.
      * There is no --filter. Use --test=<full.name> for one test or one fixture, or --where=<expr>.
      * [Test, Explicit] tests are reported as skipped and counted in `total`, whereas VSTest omits
        them entirely. That is why net40 shows total=267 while net472 shows total=265, with both
        agreeing on 202 passed / 63 failed.

.PARAMETER Test
    Optional fully-qualified test or fixture name, passed through to --test=.

.EXAMPLE
    .\run-net40-tests.ps1
    .\run-net40-tests.ps1 -Test SpringExpressionsTests.Expressions.NumericPromotionTests
#>
[CmdletBinding()]
param(
    [string] $Test
)

$ErrorActionPreference = 'Stop'
$repo = $PSScriptRoot
$project = Join-Path $repo 'SpringExpressionsTests\SpringExpressionsTests.csproj'

Write-Host 'Building net40...' -ForegroundColor Cyan
& dotnet build $project -f net40 --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)." }

# Resolve the runner from the package's GeneratePathProperty rather than assuming
# $env:USERPROFILE\.nuget\packages, which is wrong when NUGET_PACKAGES is redirected.
$pkgRoot = (& dotnet msbuild $project -getProperty:PkgNUnit_ConsoleRunner -p:TargetFramework=net40).Trim()
if (-not $pkgRoot) { throw 'Could not resolve $(PkgNUnit_ConsoleRunner). Run `dotnet restore` first.' }

$runner = Join-Path $pkgRoot 'tools\nunit3-console.exe'
if (-not (Test-Path $runner)) { throw "nunit3-console.exe not found at $runner" }

$assembly = Join-Path $repo 'SpringExpressionsTests\bin\Release\net40\SpringExpressionsTests.dll'
if (-not (Test-Path $assembly)) { throw "Test assembly not found at $assembly" }

$resultsDir = Join-Path $repo 'TestResults'
if (-not (Test-Path $resultsDir)) { New-Item -ItemType Directory $resultsDir | Out-Null }
$resultFile = Join-Path $resultsDir 'net40.xml'

# --work keeps nunit-agent_*.log and any other runner output inside TestResults instead of
# scattering it across the repo root.
$runnerArgs = @($assembly, '--noheader', "--result:$resultFile", "--work:$resultsDir")
if ($Test) { $runnerArgs += "--test=$Test" }

Write-Host "Running net40 tests via $(Split-Path $pkgRoot -Leaf)..." -ForegroundColor Cyan
& $runner @runnerArgs | Out-Host
$runnerExit = $LASTEXITCODE

if (-not (Test-Path $resultFile)) { throw "Runner produced no result file (exit $runnerExit)." }

[xml] $xml = Get-Content $resultFile
$r = $xml.'test-run'
Write-Host ''
Write-Host ('net40: total={0} passed={1} failed={2} skipped={3} (runner exit {4} = failure count)' -f `
    $r.total, $r.passed, $r.failed, $r.skipped, $runnerExit) -ForegroundColor Yellow
Write-Host "Full results: $resultFile"

# The compiled backend is WIP, so failures are expected. Compare against the recorded baseline in
# CLAUDE.md rather than expecting zero. Exit 0 unless the run itself failed to produce results.
exit 0
