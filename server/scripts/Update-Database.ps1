[CmdletBinding()]
param(
    [string]$ConnectionString = $env:ConnectionStrings__Default
)

$ErrorActionPreference = 'Stop'
$serverRoot = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    throw 'Pass -ConnectionString or set ConnectionStrings__Default.'
}

Push-Location $serverRoot
try {
    $env:ConnectionStrings__Default = $ConnectionString
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed.' }

    dotnet ef database update `
        --project src/Devkit.Server.Infrastructure/Devkit.Server.Infrastructure.csproj `
        --startup-project src/Devkit.Server.Api/Devkit.Server.Api.csproj
    if ($LASTEXITCODE -ne 0) { throw 'Database migration failed.' }
}
finally {
    Pop-Location
}
