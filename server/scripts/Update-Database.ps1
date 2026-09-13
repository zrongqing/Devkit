[CmdletBinding()]
param(
    [string]$ConnectionString = $env:ConnectionStrings__Default,
    [switch]$SeedBootstrapAccount,
    [string]$BootstrapUserName = $env:BootstrapAccount__UserName,
    [string]$BootstrapEmail = $env:BootstrapAccount__Email,
    [string]$BootstrapPassword = $env:BootstrapAccount__Password
)

$ErrorActionPreference = 'Stop'
$serverRoot = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    throw 'Pass -ConnectionString or set ConnectionStrings__Default.'
}

$previousBootstrapEnabled = $env:BootstrapAccount__Enabled
$previousBootstrapUserName = $env:BootstrapAccount__UserName
$previousBootstrapEmail = $env:BootstrapAccount__Email
$previousBootstrapPassword = $env:BootstrapAccount__Password

if ($SeedBootstrapAccount) {
    if ([string]::IsNullOrWhiteSpace($BootstrapUserName) `
        -or [string]::IsNullOrWhiteSpace($BootstrapEmail) `
        -or [string]::IsNullOrWhiteSpace($BootstrapPassword)) {
        throw 'Bootstrap user name, email, and password are required when -SeedBootstrapAccount is used.'
    }

    $env:BootstrapAccount__Enabled = 'true'
    $env:BootstrapAccount__UserName = $BootstrapUserName
    $env:BootstrapAccount__Email = $BootstrapEmail
    $env:BootstrapAccount__Password = $BootstrapPassword
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
    if ($SeedBootstrapAccount) {
        $env:BootstrapAccount__Enabled = $previousBootstrapEnabled
        $env:BootstrapAccount__UserName = $previousBootstrapUserName
        $env:BootstrapAccount__Email = $previousBootstrapEmail
        $env:BootstrapAccount__Password = $previousBootstrapPassword
    }

    Pop-Location
}
