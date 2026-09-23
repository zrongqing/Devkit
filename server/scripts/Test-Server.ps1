[CmdletBinding()]
param(
    [string]$DataRoot = 'D:\server data\devkit',
    [string]$Filter = ''
)

$ErrorActionPreference = 'Stop'
if (-not [IO.Path]::IsPathFullyQualified($DataRoot)) { throw 'DataRoot must be an absolute path.' }
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$testRoot = Join-Path $DataRoot 'temp\tests'
$outputRoot = (Join-Path $DataRoot 'temp\test-build') + [IO.Path]::DirectorySeparatorChar
$resultRoot = Join-Path $DataRoot 'temp\test-results'
$previousTestRoot = $env:DEVKIT_TEST_ROOT
$env:DEVKIT_TEST_ROOT = $testRoot
Push-Location $repositoryRoot
try {
    # The repository uses a shared server output folder. Sequential project execution
    # prevents a running testhost from locking dependencies needed by another project.
    $arguments = @('test', 'server/Devkit.Server.slnx', '-m:1', "-p:BaseOutputPath=$outputRoot",
        '--results-directory', $resultRoot, '--logger', 'trx;LogFilePrefix=server', '--verbosity', 'minimal')
    if ($Filter) { $arguments += @('--filter', $Filter) }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "Server tests failed. Results: $resultRoot" }
}
finally {
    Pop-Location
    $env:DEVKIT_TEST_ROOT = $previousTestRoot
}
