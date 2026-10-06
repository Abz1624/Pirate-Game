# Deploys the API to Railway from Windows PowerShell (the repo's .env holds RAILWAY_TOKEN; it is never printed).
# Usage, from the repo root or anywhere:  powershell -NoProfile -ExecutionPolicy Bypass -File server/deploy.ps1
$ErrorActionPreference = 'Stop'
$envFile = Join-Path $PSScriptRoot '..\.env'
if (-not (Test-Path $envFile)) { throw "No .env at $envFile (copy .env.example and add RAILWAY_TOKEN)." }
foreach ($line in Get-Content $envFile) {
    if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)\s*$') {
        [Environment]::SetEnvironmentVariable($Matches[1], $Matches[2].Trim('"', "'", ' '), 'Process')
    }
}
if (-not $env:RAILWAY_TOKEN) { throw 'RAILWAY_TOKEN is empty in .env.' }
if (-not (Get-Command railway -ErrorAction SilentlyContinue)) { throw 'Railway CLI not found: npm install -g @railway/cli' }

Push-Location $PSScriptRoot
try {
    railway up --service api --ci
    if ($LASTEXITCODE -ne 0) { throw "railway up failed ($LASTEXITCODE)." }
} finally { Pop-Location }

Start-Sleep -Seconds 15
Invoke-RestMethod 'https://api-production-dad8.up.railway.app/health'
