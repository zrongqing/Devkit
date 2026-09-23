$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$composeFile = Join-Path $repositoryRoot 'compose.yaml'

docker compose -f $composeFile up -d qdrant redis
if ($LASTEXITCODE -ne 0) { throw 'Study dependency startup failed.' }
docker compose -f $composeFile ps
