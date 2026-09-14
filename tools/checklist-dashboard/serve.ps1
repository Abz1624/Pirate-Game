# Serves the checklist dashboard and repo markdown files on http://localhost:8080
$ErrorActionPreference = "Stop"
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dashboard = Join-Path $root "tools\checklist-dashboard"

Write-Host "Rogue Pirates Checklist Dashboard"
Write-Host "  Root:      $root"
Write-Host "  Dashboard: http://localhost:8080/tools/checklist-dashboard/"
Write-Host ""
Write-Host "Press Ctrl+C to stop."
Write-Host ""

Set-Location $root

$python = Get-Command python -ErrorAction SilentlyContinue
if (-not $python) {
    $python = Get-Command py -ErrorAction SilentlyContinue
    if ($python) {
        & py -m http.server 8080
        exit $LASTEXITCODE
    }
    Write-Error "Python not found. Install Python 3 or use: npx --yes serve ."
    exit 1
}

& python -m http.server 8080
