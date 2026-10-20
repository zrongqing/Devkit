[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Identity', 'Business', 'FileStorage')][string]$Context,
    [string]$ConnectionString = '',
    [switch]$SeedBootstrapAccount,
    [string]$BootstrapUserName = $env:BootstrapAccount__UserName,
    [string]$BootstrapEmail = $env:BootstrapAccount__Email,
    [string]$BootstrapPassword = $env:BootstrapAccount__Password
)
$ErrorActionPreference = 'Stop'
if ($SeedBootstrapAccount -and $Context -ne 'Identity') { throw 'Bootstrap accounts belong to Identity.' }
$connectionVariable = "ConnectionStrings__$Context"
if ([string]::IsNullOrWhiteSpace($ConnectionString)) { $ConnectionString = [Environment]::GetEnvironmentVariable($connectionVariable) }
if ([string]::IsNullOrWhiteSpace($ConnectionString)) { throw "Pass -ConnectionString or set $connectionVariable." }
if ($SeedBootstrapAccount -and (@($BootstrapUserName, $BootstrapEmail, $BootstrapPassword) | Where-Object { [string]::IsNullOrWhiteSpace($_) })) {
    throw 'Bootstrap user name, email and password are required.'
}
$previous = @{}
foreach ($name in @($connectionVariable, 'BootstrapAccount__Enabled', 'BootstrapAccount__UserName', 'BootstrapAccount__Email', 'BootstrapAccount__Password')) {
    $previous[$name] = [Environment]::GetEnvironmentVariable($name)
}
$serverRoot = Split-Path -Parent $PSScriptRoot
Push-Location $serverRoot
try {
    [Environment]::SetEnvironmentVariable($connectionVariable, $ConnectionString)
    $env:BootstrapAccount__Enabled = $SeedBootstrapAccount.IsPresent.ToString()
    if ($SeedBootstrapAccount) {
        $env:BootstrapAccount__UserName = $BootstrapUserName
        $env:BootstrapAccount__Email = $BootstrapEmail
        $env:BootstrapAccount__Password = $BootstrapPassword
    }
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed.' }
    dotnet ef database update --context "${Context}DbContext" `
        --project "src/$Context/Devkit.$Context.Infrastructure/Devkit.$Context.Infrastructure.csproj" `
        --startup-project "src/$Context/Devkit.$Context.Api/Devkit.$Context.Api.csproj"
    if ($LASTEXITCODE -ne 0) { throw "$Context database migration failed." }
}
finally {
    foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name]) }
    Pop-Location
}
