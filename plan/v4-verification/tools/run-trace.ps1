# Runs one pinned V4 trace in the open Editor and waits for the JSONL file.
# Usage: .\run-trace.ps1 -Scene Assets/_Game/Scenes/v3/V3SaltwindRun.unity -Seed 101 -Out plan/v4-verification/phase-00-composition/v3-trace-seed-101-a.jsonl -Label v3-101-a
param(
    [Parameter(Mandatory)] [string] $Scene,
    [Parameter(Mandatory)] [int] $Seed,
    [Parameter(Mandatory)] [string] $Out,
    [string] $Label = "trace",
    [float] $BossAt = 240,
    [float] $MaxSim = 900,
    [int] $TimeoutSeconds = 900
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$env:UNITY_PROJECT_PATH = Join-Path $repo 'Pirate Game Unity'
$outFull = Join-Path $repo $Out
if (Test-Path $outFull) { Remove-Item $outFull }

$evalDir = Join-Path $env:TEMP 'v4-trace-eval'
New-Item -ItemType Directory -Force $evalDir | Out-Null
$evalFile = Join-Path $evalDir "run-$Label.cs"
$code = "return RoguePirates.V4.Editor.V4TraceSession.Run(`"$Scene`", $Seed, `"$($Out -replace '\\','/')`", `"$Label`", ${BossAt}f, ${MaxSim}f);"
Set-Content -Path $evalFile -Value $code -Encoding utf8

# Wait until the Editor is back in Edit Mode before starting.
$waited = 0
while ($waited -lt 120) {
    $st = unity command editor_status 2>&1 | Out-String
    if ($st -match '"playMode":"stopped"' -and $st -match '"compiling":false') { break }
    Start-Sleep -Seconds 2; $waited += 2
}

$start = unity command eval_file --file $evalFile 2>&1 | Out-String
if ($start -notmatch '"result":"started"') { Write-Error "Trace did not start: $start" }

$elapsed = 0
while (-not (Test-Path $outFull) -and $elapsed -lt $TimeoutSeconds) { Start-Sleep -Seconds 5; $elapsed += 5 }
if (-not (Test-Path $outFull)) { Write-Error "Timed out after $TimeoutSeconds s waiting for $outFull" }
Start-Sleep -Seconds 3
$lines = Get-Content $outFull
Write-Output "wrote $Out ($($lines.Count) lines) in ${elapsed}s"
Write-Output ($lines | Select-Object -Last 3)
