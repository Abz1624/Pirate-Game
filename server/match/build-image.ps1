# V5 M9b part 2a: builds the match server image LOCALLY (owner, 8 Oct: Railway never builds it, it only runs the image).
#   1. the Unity Linux Dedicated Server build (V5_NET) into "Pirate Game Unity/Builds/LinuxServer", through the open
#      Editor (unity command) or, with no Editor open on the project, a batch-mode Unity;
#   2. docker build, tagged ghcr.io/<owner>/rogue-pirates-match:<git sha> and rogue-pirates-match:local.
# Pushing is a separate step (push-image.ps1). Nothing here talks to Railway or the registry.
# Usage, from anywhere:
#   powershell -NoProfile -ExecutionPolicy Bypass -File server/match/build-image.ps1 [-SkipUnityBuild] [-Owner x2c-fun] [-Tag <tag>]
param(
    [switch]$SkipUnityBuild,
    [string]$Owner = "x2c-fun",
    [string]$Tag = "",
    [int]$UnityTimeoutMinutes = 120
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$project = Join-Path $repo 'Pirate Game Unity'
$buildDir = Join-Path $project 'Builds\LinuxServer'
$report = Join-Path $project 'Builds\V5NetServer-report.json'
$unityVersion = '6000.3.11f1'
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { $env:PATH = "C:\Program Files\Docker\Docker\resources\bin;$env:PATH" }
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw 'docker not found (Docker Desktop must be installed and running).' }

$Owner = $Owner.ToLowerInvariant() # GHCR names are lower case
if (-not $Tag) {
    $Tag = (git -C $repo rev-parse --short=12 HEAD).Trim()
    # Uncommitted changes under the server's code make a "-dirty" tag, so a pushed image always names what it holds.
    $dirty = git -C $repo status --porcelain -- 'Pirate Game Unity/Assets/_Game' 'Pirate Game Unity/Packages' 'server/match'
    if ($dirty) { $Tag += '-dirty' }
}
$image = "ghcr.io/$Owner/rogue-pirates-match:$Tag"

if (-not $SkipUnityBuild) {
    if (Test-Path $report) { Remove-Item $report -Force }
    $started = Get-Date
    $env:UNITY_PROJECT_PATH = $project
    $status = (unity status --json 2>$null | Out-String)
    if ($status -match '"state": "ready"') {
        Write-Output "Unity build through the open Editor (switches to Linux Server and back; this takes a while)..."
        unity command --detach --timeout ($UnityTimeoutMinutes * 60) eval --timeout ($UnityTimeoutMinutes * 60) "return RoguePirates.V5.Net.Editor.V5NetServerBuild.Build();" | Out-Null
        $deadline = $started.AddMinutes($UnityTimeoutMinutes)
        while (-not (Test-Path $report)) {
            if ((Get-Date) -gt $deadline) { throw "No build report after $UnityTimeoutMinutes minutes ($report)." }
            Start-Sleep -Seconds 10
        }
    } else {
        $editors = (unity editors --format json 2>$null | Out-String | ConvertFrom-Json).data
        $exe = ($editors | Where-Object { $_.version -eq $unityVersion }).location
        if (-not $exe) { $exe = "C:\Program Files\Unity\Hub\Editor\$unityVersion\Editor\Unity.exe" }
        $log = Join-Path $project 'Builds\V5NetServer-build.log'
        Write-Output "Unity build in batch mode ($exe), log $log ..."
        $p = Start-Process -FilePath $exe -PassThru -Wait -NoNewWindow -ArgumentList @(
            '-batchmode', '-quit', '-projectPath', "`"$project`"", '-executeMethod', 'RoguePirates.V5.Net.Editor.V5NetServerBuild.BuildFromCommandLine', '-logFile', "`"$log`"")
        if ($p.ExitCode -ne 0) { throw "Unity batch build failed ($($p.ExitCode)); see $log" }
    }
    $json = Get-Content $report -Raw
    Write-Output "Unity: $json"
    if ($json -notmatch '"result":"Succeeded"') { throw 'The Unity server build did not succeed.' }
}
if (-not (Test-Path (Join-Path $buildDir 'RoguePirates.x86_64'))) { throw "No server build in $buildDir (run without -SkipUnityBuild)." }

Write-Output "docker build $image ..."
$sw = [Diagnostics.Stopwatch]::StartNew()
docker build -f (Join-Path $PSScriptRoot 'Dockerfile') --build-context "match=$PSScriptRoot" `
    --label "org.opencontainers.image.source=https://github.com/X2C-fun/Pirate-Game" `
    --label "org.opencontainers.image.revision=$Tag" `
    --label "org.opencontainers.image.description=Rogue Pirates match server (V5 M9b spike)" `
    -t $image -t 'rogue-pirates-match:local' $buildDir
if ($LASTEXITCODE -ne 0) { throw "docker build failed ($LASTEXITCODE)." }
$bytes = [long](docker image inspect -f '{{.Size}}' $image)
Write-Output ("built {0} in {1:0} s, image size {2:0.0} MB unpacked (docker images shows the compressed pull size)" -f $image, $sw.Elapsed.TotalSeconds, ($bytes / 1MB))
Write-Output "next: python server/match/local_voyage_test.py (local check), then server/match/push-image.ps1 -Tag $Tag (needs GHCR_TOKEN)"
