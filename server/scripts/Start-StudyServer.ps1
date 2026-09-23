[CmdletBinding()]
param(
    [string]$DataRoot = 'D:\server data\devkit',
    [string]$Url = 'http://localhost:12511',
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$serverRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $serverRoot 'src\Devkit.Server.Api\Devkit.Server.Api.csproj'
$configDirectory = Join-Path $DataRoot 'config'
New-Item -ItemType Directory -Path $configDirectory -Force | Out-Null
$configFile = Join-Path $configDirectory 'workspace.json'
if (-not (Test-Path -LiteralPath $configFile)) {
    $sample = Get-Content -LiteralPath (Join-Path $serverRoot 'deploy\exam-study\workspace.example.json') -Raw | ConvertFrom-Json
    $sample.Workspace.DataRoot = $DataRoot
    $sample | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $configFile -Encoding utf8
}

$env:DEVKIT_CONFIG_FILE = $configFile
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = $Url
$runArguments = @('run', '--project', $project, '--no-launch-profile')
if ($NoBuild) { $runArguments += '--no-build' }
& dotnet @runArguments
if ($LASTEXITCODE -ne 0) { throw 'Study server stopped with an error.' }
