[CmdletBinding()]
param(
    [string]$Tag = 'devkit-server:local',
    [string]$Version = '0.1.0'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')

docker build `
    --file (Join-Path $repositoryRoot 'src/server/src/Devkit.Server.Api/Dockerfile') `
    --build-arg "VERSION=$Version" `
    --tag $Tag `
    $repositoryRoot
if ($LASTEXITCODE -ne 0) { throw 'Docker image build failed.' }

Write-Host "Built $Tag (version $Version)."
