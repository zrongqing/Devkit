[CmdletBinding()]
param(
    [switch]$DependenciesOnly
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$environmentFile = Join-Path $repositoryRoot '.env'

Push-Location $repositoryRoot
try {
    if ($DependenciesOnly) {
        if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot 'web/dist/index.html'))) {
            throw 'Build web first: npm --prefix web ci; npm --prefix web run build.'
        }
        docker compose -f compose.yaml -f compose.development.yaml up --detach nginx redis qdrant
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
