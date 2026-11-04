[CmdletBinding()]
param(
    [ValidateSet('Application', 'Identity', 'Business', 'FileStorage', 'All')][string]$Context = 'All',
    [string]$Tag = '',
    [string]$Version = '0.1.0'
)
$ErrorActionPreference = 'Stop'
if ($Context -eq 'Identity') { $Context = 'Application' }
if ($Context -eq 'All' -and $Tag) { throw 'Use -Context Application, Business or FileStorage when setting a custom -Tag.' }
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$contexts = if ($Context -eq 'All') { @('Application', 'Business', 'FileStorage') } else { @($Context) }
foreach ($item in $contexts) {
    $image = if ($Tag) { $Tag } else { "devkit-$($item.ToLowerInvariant()):local" }
    docker build --file (Join-Path $repositoryRoot "server/src/$item/Devkit.$item.Api/Dockerfile") `
        --build-arg "VERSION=$Version" --tag $image $repositoryRoot
    if ($LASTEXITCODE -ne 0) { throw "$item image build failed." }
    Write-Host "Built $image (version $Version)."
}
