$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$composeFile = Join-Path $repositoryRoot 'compose.yaml'
$developmentFile = Join-Path $repositoryRoot 'compose.development.yaml'
if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot 'web/dist/index.html'))) {
    throw 'Build web first: npm --prefix web ci; npm --prefix web run build.'
}

docker compose -f $composeFile -f $developmentFile up -d nginx qdrant redis
if ($LASTEXITCODE -ne 0) { throw 'Study dependency startup failed.' }
docker compose -f $composeFile -f $developmentFile ps
