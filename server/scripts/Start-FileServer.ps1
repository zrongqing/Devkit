[CmdletBinding()]
param(
    [string]$DataRoot = 'D:\.server data\devkit',
    [string]$Url = 'http://localhost:12513',
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$serverRoot = Split-Path -Parent $PSScriptRoot
$configDirectory = Join-Path $DataRoot 'config'
New-Item -ItemType Directory -Path $configDirectory -Force | Out-Null
$configFile = Join-Path $configDirectory 'file-storage.json'
if (-not (Test-Path -LiteralPath $configFile)) {
    $sample = Get-Content -LiteralPath (Join-Path $serverRoot 'deploy/file-storage.example.json') -Raw | ConvertFrom-Json
    $sample.FileStorage.DataRoot = $DataRoot
    $sample | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $configFile -Encoding utf8
}
$env:DEVKIT_FILESTORAGE_CONFIG_FILE = $configFile
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = $Url
$runArguments = @('run', '--project', (Join-Path $serverRoot 'src/FileStorage/Devkit.FileStorage.Api'), '--no-launch-profile')
if ($NoBuild) { $runArguments += '--no-build' }
& dotnet @runArguments
if ($LASTEXITCODE -ne 0) { throw 'File server stopped with an error.' }
