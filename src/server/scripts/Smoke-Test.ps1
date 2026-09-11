[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://localhost:8080',
    [string]$RedisContainer = ''
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
$suffix = [Guid]::NewGuid().ToString('N')
$userName = "smoke-$suffix"
$password = 'SmokeTestPassword123'
$email = "$userName@example.test"

$ready = Invoke-RestMethod -Method Get -Uri "$BaseUrl/health/ready"
if ($null -eq $ready) { throw 'Readiness endpoint did not return a response.' }

$registerBody = @{ userName = $userName; email = $email; password = $password } | ConvertTo-Json
$registered = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/v1/auth/register" -ContentType 'application/json' -Body $registerBody
if ($registered.data.userName -ne $userName) { throw 'Registration response was invalid.' }

$loginBody = @{ account = $email; password = $password } | ConvertTo-Json
$login = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/v1/auth/login" -ContentType 'application/json' -Body $loginBody
$headers = @{ Authorization = "Bearer $($login.data.accessToken)" }
$profile = Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/v1/auth/me" -Headers $headers
if ($profile.data.id -ne $registered.data.id) { throw 'Authenticated profile did not match registration.' }

$refreshBody = @{ refreshToken = $login.data.refreshToken } | ConvertTo-Json
$refreshed = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/v1/auth/refresh" -ContentType 'application/json' -Body $refreshBody
if ($refreshed.data.refreshToken -eq $login.data.refreshToken) { throw 'Refresh token was not rotated.' }

$logoutBody = @{ refreshToken = $refreshed.data.refreshToken } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/v1/auth/logout" -ContentType 'application/json' -Body $logoutBody

Push-Location $repositoryRoot
try {
    if ([string]::IsNullOrWhiteSpace($RedisContainer)) {
        $cacheKeys = docker compose exec -T redis redis-cli --scan --pattern 'devkit:auth:user:*:profile'
    }
    else {
        $cacheKeys = docker exec $RedisContainer redis-cli --scan --pattern 'devkit:auth:user:*:profile'
    }

    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($cacheKeys -join ''))) {
        throw 'Redis profile cache key was not found.'
    }

    $userId = ([Guid]$registered.data.id).ToString('N')
    if ([string]::IsNullOrWhiteSpace($RedisContainer)) {
        docker compose exec -T redis redis-cli del "devkit:auth:user:${userId}:profile" "devkit:auth:user:${userId}:version" | Out-Null
    }
    else {
        docker exec $RedisContainer redis-cli del "devkit:auth:user:${userId}:profile" "devkit:auth:user:${userId}:version" | Out-Null
    }

    if ($LASTEXITCODE -ne 0) { throw 'Redis smoke-test cache cleanup failed.' }
}
finally {
    Pop-Location
}

Write-Host "Smoke test passed for $userName."
