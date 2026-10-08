# V5 M9b part 2a: pushes a locally built match server image to the GitHub container registry, for the Railway test
# service to run (Railway pulls it; it never builds). Run by the main session after review, never automatically.
# Needs a GitHub token with write:packages in the environment variable GHCR_TOKEN (the owner provides it; it is passed
# to docker login on stdin and never printed), and the GitHub user it belongs to in GHCR_USER (default: the owner name).
# Usage:
#   $env:GHCR_TOKEN = '<token>'; powershell -NoProfile -ExecutionPolicy Bypass -File server/match/push-image.ps1 -Tag <tag from build-image.ps1>
param(
    [Parameter(Mandatory = $true)][string]$Tag,
    [string]$Owner = "x2c-fun",
    [string]$User = $env:GHCR_USER
)
$ErrorActionPreference = 'Stop'
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { $env:PATH = "C:\Program Files\Docker\Docker\resources\bin;$env:PATH" }
$Owner = $Owner.ToLowerInvariant()
if (-not $User) { $User = $Owner }
if (-not $env:GHCR_TOKEN) { throw 'Set GHCR_TOKEN (a GitHub token with write:packages) first.' }
if ($Tag -like '*-dirty') { throw "Refusing to push a -dirty image ($Tag): commit first and rebuild." }
$image = "ghcr.io/$Owner/rogue-pirates-match:$Tag"
docker image inspect $image *> $null
if ($LASTEXITCODE -ne 0) { throw "No local image $image (run build-image.ps1)." }

$env:GHCR_TOKEN | docker login ghcr.io -u $User --password-stdin
if ($LASTEXITCODE -ne 0) { throw 'docker login ghcr.io failed.' }
try {
    docker push $image
    if ($LASTEXITCODE -ne 0) { throw "docker push failed ($LASTEXITCODE)." }
    $digest = docker image inspect -f '{{index .RepoDigests 0}}' $image
    Write-Output "pushed $image"
    Write-Output "digest $digest (give Railway this exact reference)"
} finally {
    docker logout ghcr.io | Out-Null
}
