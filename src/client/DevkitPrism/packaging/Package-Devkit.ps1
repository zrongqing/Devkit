[CmdletBinding()]
param(
    [Parameter()]
    [string]$Version,

    [Parameter()]
    [ValidatePattern('^[0-9A-Za-z][0-9A-Za-z.-]*$')]
    [string]$VersionSuffix,

    [Parameter()]
    [string]$OutputDirectory,

    [Parameter()]
    [string]$InnoCompilerPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (Test-Path variable:PSNativeCommandUseErrorActionPreference) {
    $PSNativeCommandUseErrorActionPreference = $true
}

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,

        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    Write-Host "> $FilePath $($Arguments -join ' ')"
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath"
    }
}

function Resolve-InnoCompiler {
    param([string]$RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        $resolved = Resolve-Path -LiteralPath $RequestedPath -ErrorAction Stop
        if (-not (Test-Path -LiteralPath $resolved.Path -PathType Leaf)) {
            throw "Inno Setup compiler is not a file: $RequestedPath"
        }

        return $resolved.Path
    }

    $command = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw 'Inno Setup 6 compiler (ISCC.exe) was not found. Install Inno Setup 6, add ISCC.exe to PATH, or pass -InnoCompilerPath.'
}

function Get-AbsolutePath {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$BasePath
    )

    if ([System.IO.Path]::IsPathFullyQualified($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $Path))
}

$packagingRoot = $PSScriptRoot
$clientRoot = (Resolve-Path (Join-Path $packagingRoot '..')).Path
$repositoryRoot = (Resolve-Path (Join-Path $clientRoot '..\..\..')).Path
$solutionPath = Join-Path $clientRoot 'DevkitPrism.slnx'
$applicationProject = Join-Path $clientRoot 'Devkit\Devkit.csproj'
$buildPropertiesPath = Join-Path $clientRoot 'Directory.Build.props'
$installerDefinition = Join-Path $packagingRoot 'Devkit.iss'
$clientBuildRoot = Join-Path $repositoryRoot 'build\client'
$configurationOutput = Join-Path $clientBuildRoot 'Release\net10.0-windows'
$stagingRoot = Join-Path $clientBuildRoot 'package-staging'
$publishDirectory = Join-Path $stagingRoot 'app'

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $clientBuildRoot 'package'
}
else {
    $OutputDirectory = Get-AbsolutePath -Path $OutputDirectory -BasePath (Get-Location).Path
}

[xml]$buildProperties = Get-Content -LiteralPath $buildPropertiesPath -Raw
$versionPrefixNode = $buildProperties.SelectSingleNode('/Project/PropertyGroup/VersionPrefix')
if ($null -eq $versionPrefixNode -or [string]::IsNullOrWhiteSpace($versionPrefixNode.InnerText)) {
    throw "VersionPrefix is missing from $buildPropertiesPath"
}

if (-not [string]::IsNullOrWhiteSpace($Version) -and -not [string]::IsNullOrWhiteSpace($VersionSuffix)) {
    throw 'Use either -Version or -VersionSuffix, not both.'
}

$resolvedVersion = if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $Version.Trim()
}
elseif (-not [string]::IsNullOrWhiteSpace($VersionSuffix)) {
    "$($versionPrefixNode.InnerText.Trim())-$($VersionSuffix.Trim())"
}
else {
    $versionPrefixNode.InnerText.Trim()
}

if ($resolvedVersion -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?(?:\+[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$') {
    throw "Version '$resolvedVersion' is not a supported semantic version. Example: 0.1.0 or 0.1.0-ci.42."
}

$numericVersion = "$(($resolvedVersion -split '[-+]')[0]).0"

$safeFileVersion = $resolvedVersion -replace '\+', '-'
$outputBaseName = "Devkit-Setup-$safeFileVersion-win-x64"
$installerPath = Join-Path $OutputDirectory "$outputBaseName.exe"
$checksumPath = "$installerPath.sha256"
$innoCompiler = Resolve-InnoCompiler -RequestedPath $InnoCompilerPath

$expectedModules = @(
    'Devkit.Modules.Demo',
    'Devkit.Modules.ModuleName',
    'Devkit.Modules.Ssamc'
)

try {
    foreach ($pathToReset in @($configurationOutput, $stagingRoot)) {
        if (Test-Path -LiteralPath $pathToReset) {
            Remove-Item -LiteralPath $pathToReset -Recurse -Force
        }
    }

    New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    Remove-Item -LiteralPath $installerPath, $checksumPath -Force -ErrorAction SilentlyContinue

    Invoke-NativeCommand -FilePath 'dotnet' -Arguments @(
        'restore', $solutionPath,
        '--runtime', 'win-x64',
        '--nologo'
    )

    Invoke-NativeCommand -FilePath 'dotnet' -Arguments @(
        'build', $solutionPath,
        '--configuration', 'Release',
        '--no-restore',
        '--nologo',
        "-p:Version=$resolvedVersion"
    )

    Invoke-NativeCommand -FilePath 'dotnet' -Arguments @(
        'test', $solutionPath,
        '--configuration', 'Release',
        '--no-build',
        '--no-restore',
        '--nologo'
    )

    Invoke-NativeCommand -FilePath 'dotnet' -Arguments @(
        'publish', $applicationProject,
        '--configuration', 'Release',
        '--runtime', 'win-x64',
        '--self-contained', 'false',
        '--no-restore',
        '--nologo',
        '--output', $publishDirectory,
        "-p:Version=$resolvedVersion"
    )

    $moduleDestinationRoot = Join-Path $publishDirectory 'modules'
    New-Item -ItemType Directory -Path $moduleDestinationRoot -Force | Out-Null

    foreach ($moduleName in $expectedModules) {
        $moduleSource = Join-Path $configurationOutput "modules\$moduleName"
        $moduleAssembly = Join-Path $moduleSource "$moduleName.dll"
        if (-not (Test-Path -LiteralPath $moduleAssembly -PathType Leaf)) {
            throw "Expected module assembly was not produced: $moduleAssembly"
        }

        $moduleDestination = Join-Path $moduleDestinationRoot $moduleName
        New-Item -ItemType Directory -Path $moduleDestination -Force | Out-Null
        Copy-Item -Path (Join-Path $moduleSource '*') -Destination $moduleDestination -Recurse -Force
    }

    $allowedRuntimeDirectories = @('win', 'win-x64')
    Get-ChildItem -LiteralPath $moduleDestinationRoot -Recurse -Directory |
        Where-Object {
            $_.Parent.Name -eq 'runtimes' -and
            $_.Name -notin $allowedRuntimeDirectories
        } |
        Remove-Item -Recurse -Force

    Get-ChildItem -LiteralPath $publishDirectory -Recurse -File -Filter '*.pdb' |
        Remove-Item -Force

    $requiredFiles = @(
        (Join-Path $publishDirectory 'Devkit.exe'),
        (Join-Path $publishDirectory 'Devkit.dll'),
        (Join-Path $publishDirectory 'Devkit.deps.json'),
        (Join-Path $publishDirectory 'Devkit.runtimeconfig.json'),
        (Join-Path $publishDirectory 'appsettings.json')
    )

    foreach ($requiredFile in $requiredFiles) {
        if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
            throw "Required application file is missing from staging: $requiredFile"
        }
    }

    foreach ($moduleName in $expectedModules) {
        $stagedModule = Join-Path $moduleDestinationRoot "$moduleName\$moduleName.dll"
        if (-not (Test-Path -LiteralPath $stagedModule -PathType Leaf)) {
            throw "Required module file is missing from staging: $stagedModule"
        }
    }

    $unexpectedTestFile = Get-ChildItem -LiteralPath $publishDirectory -Recurse -File |
        Where-Object Name -Match '\.Tests\.' |
        Select-Object -First 1
    if ($null -ne $unexpectedTestFile) {
        throw "A test artifact was included in staging: $($unexpectedTestFile.FullName)"
    }

    Invoke-NativeCommand -FilePath $innoCompiler -Arguments @(
        "/DAppVersion=$resolvedVersion",
        "/DVersionInfoVersion=$numericVersion",
        "/DSourceDir=$publishDirectory",
        "/DOutputDir=$OutputDirectory",
        "/DOutputBaseFilename=$outputBaseName",
        $installerDefinition
    )

    if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
        throw "Inno Setup did not produce the expected installer: $installerPath"
    }

    $hash = Get-FileHash -LiteralPath $installerPath -Algorithm SHA256
    "$($hash.Hash.ToLowerInvariant())  $([System.IO.Path]::GetFileName($installerPath))" |
        Set-Content -LiteralPath $checksumPath -Encoding utf8NoBOM

    Write-Host ''
    Write-Host "Installer: $installerPath"
    Write-Host "SHA-256:  $checksumPath"
}
catch {
    Remove-Item -LiteralPath $installerPath, $checksumPath -Force -ErrorAction SilentlyContinue
    throw
}

