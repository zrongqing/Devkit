[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Application', 'Identity', 'Business', 'FileStorage')][string]$Context,
    [string]$ConnectionString = '',
    [string]$DatabaseConfigFile = $env:DEVKIT_DATABASE_CONFIG_FILE,
    [switch]$SeedBootstrapAccount,
    [string]$BootstrapUserName = $env:BootstrapAccount__UserName,
    [string]$BootstrapEmail = $env:BootstrapAccount__Email,
    [string]$BootstrapPassword = $env:BootstrapAccount__Password
)
$ErrorActionPreference = 'Stop'
if ($Context -eq 'Identity') { $Context = 'Application' }
if ($SeedBootstrapAccount -and $Context -ne 'Application') { throw 'Bootstrap accounts belong to Application.' }
$connectionVariable = "ConnectionStrings__Devkit"
if (-not [string]::IsNullOrWhiteSpace($DatabaseConfigFile)) {
    $DatabaseConfigFile = (Resolve-Path -LiteralPath $DatabaseConfigFile).Path
}
if ($SeedBootstrapAccount -and (@($BootstrapUserName, $BootstrapEmail, $BootstrapPassword) | Where-Object { [string]::IsNullOrWhiteSpace($_) })) {
    throw 'Bootstrap user name, email and password are required.'
}
$previous = @{}
foreach ($name in @($connectionVariable, 'DEVKIT_DATABASE_CONFIG_FILE', 'BootstrapAccount__Enabled', 'BootstrapAccount__UserName', 'BootstrapAccount__Email', 'BootstrapAccount__Password')) {
    $previous[$name] = [Environment]::GetEnvironmentVariable($name)
}
$serverRoot = Split-Path -Parent $PSScriptRoot
Push-Location $serverRoot
try {
    if (-not [string]::IsNullOrWhiteSpace($ConnectionString)) {
        [Environment]::SetEnvironmentVariable($connectionVariable, $ConnectionString)
    }
    [Environment]::SetEnvironmentVariable('DEVKIT_DATABASE_CONFIG_FILE', $DatabaseConfigFile)
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
    foreach ($name in $previous.Keys) {
        if ($null -eq $previous[$name]) {
            # Preserve absence: an empty connection variable overrides database.json on .NET 10.
            Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue
        }
        else {
            [Environment]::SetEnvironmentVariable($name, $previous[$name])
        }
    }
    Pop-Location
}
