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

# Service settings (Dockerfile build, /health check, restart policy, one replica) live in .railway/railway.ts and are
# pushed with `railway config apply` before the code goes up. Without --confirm-destructive the apply refuses to delete
# anything (for example a variable missing from railway.ts), so a bad file stops the deploy instead of breaking the API.
$iacDir = Join-Path $PSScriptRoot '..\.railway'
if (-not (Test-Path (Join-Path $iacDir 'node_modules\railway'))) {
    npm ci --prefix $iacDir --no-fund --no-audit
    if ($LASTEXITCODE -ne 0) { throw "npm ci in .railway failed ($LASTEXITCODE)." }
}
# The Railway SDK checks the CLI version by running $env:_ (else `railway`, which Windows cannot exec as an npm shim).
$railwayExe = Join-Path (Split-Path (Get-Command railway).Source) 'node_modules\@railway\cli\bin\railway.exe'
if (Test-Path $railwayExe) { $env:_ = $railwayExe }
Push-Location $iacDir
try {
    railway config apply --yes
    if ($LASTEXITCODE -ne 0) { throw "railway config apply failed ($LASTEXITCODE)." }
} finally { Pop-Location }

Push-Location $PSScriptRoot
try {
    railway up --service api --ci
    if ($LASTEXITCODE -ne 0) { throw "railway up failed ($LASTEXITCODE)." }
} finally { Pop-Location }

Start-Sleep -Seconds 15
Invoke-RestMethod 'https://api-production-dad8.up.railway.app/health'
