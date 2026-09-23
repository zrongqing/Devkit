[CmdletBinding()]
param(
    [string]$DataRoot = 'D:\server data\devkit',
    [switch]$AllowDockerDataElsewhere
)

$ErrorActionPreference = 'Stop'
$serverRoot = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $serverRoot 'deploy\exam-study\compose.yaml'
if (-not $AllowDockerDataElsewhere) {
    $settingsFile = Join-Path $env:APPDATA 'Docker\settings-store.json'
    if (-not (Test-Path -LiteralPath $settingsFile)) {
        $settingsFile = Join-Path $env:APPDATA 'Docker\settings.json'
    }
    $settings = Get-Content -LiteralPath $settingsFile -Raw | ConvertFrom-Json
    $expected = [IO.Path]::GetFullPath((Join-Path $DataRoot 'docker')).TrimEnd('\')
    $actual = [string]$settings.DataFolder
    if ([string]::IsNullOrWhiteSpace($actual) -or
        -not ([IO.Path]::GetFullPath($actual).TrimEnd('\').Equals($expected, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFullPath($actual).StartsWith($expected + '\', [StringComparison]::OrdinalIgnoreCase))) {
        throw "Docker disk location is not under $expected. Move it in Docker Desktop Settings / Resources / Advanced first, or explicitly pass -AllowDockerDataElsewhere."
    }
}

docker compose -f $composeFile up -d
if ($LASTEXITCODE -ne 0) { throw 'Study dependency startup failed.' }
docker compose -f $composeFile ps
