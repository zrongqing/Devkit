[CmdletBinding()]
param(
    [switch]$DependenciesOnly
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
$environmentFile = Join-Path $repositoryRoot '.env'

Push-Location $repositoryRoot
try {
    if ($DependenciesOnly) {
        docker compose up --detach redis
    }
    else {
        if (-not (Test-Path -LiteralPath $environmentFile)) {
            throw 'Create .env from .env.example and replace all example secrets first.'
        }

        docker compose up --detach --build
    }

    if ($LASTEXITCODE -ne 0) { throw 'Docker Compose startup failed.' }
}
finally {
    Pop-Location
}
