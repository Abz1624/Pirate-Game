# Runs trace seeds in parallel in the Windows development player built by build-trace-player.ps1.
# Each run is a separate player process: borderless 1920x1080 window, request passed with -v4trace, quits when done.
# The player's PlayerPrefs (shared by all instances) are backed up before and restored after; TraceBoot pins the
# keys a run reads to one fixed state, so instances cannot affect each other.
# Usage: .\run-traces-parallel.ps1 -Runs 'V4SaltwindRun|101|v4-101-a|plan/.../player-v4-trace-seed-101-a.jsonl', ...
param(
    [Parameter(Mandatory)] [string[]] $Runs,          # "scene|seed|label|outPath" (outPath relative to repo root)
    [int] $MaxParallel = 4,
    [float] $BossAt = 240,
    [float] $KillBossAt = 300,
    [float] $MaxSim = 900,
    [int] $TimeoutSeconds = 1800,
    [string] $Root = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path 'Pirate Game Unity/Builds/V4Trace')
)
$ErrorActionPreference = 'Continue'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$exe = Join-Path $Root 'build\PirateV4Trace.exe'
if (-not (Test-Path $exe)) { Write-Error "Player not built: $exe (run build-trace-player.ps1)"; exit 1 }
$work = Join-Path $Root 'runs'
New-Item -ItemType Directory -Force $work | Out-Null
$commit = (git -C $repo rev-parse HEAD).Trim()

# Back up the player's PlayerPrefs (company/product from ProjectSettings: DefaultCompany / Pirate Game).
$prefsKey = 'HKCU\Software\DefaultCompany\Pirate Game'
$prefsBackup = Join-Path $Root 'player-prefs-backup.reg'
$hadPrefs = $false
reg query $prefsKey 2>$null | Out-Null
if ($LASTEXITCODE -eq 0) { $hadPrefs = $true; reg export $prefsKey $prefsBackup /y | Out-Null }

$queue = New-Object System.Collections.Queue
foreach ($r in $Runs) { $queue.Enqueue($r) }
$running = @()
$results = @()
$sw = [Diagnostics.Stopwatch]::StartNew()

function Start-Run([string] $spec) {
    $scene, $seed, $label, $out = $spec.Split('|')
    $outFull = [IO.Path]::GetFullPath((Join-Path $repo $out))
    New-Item -ItemType Directory -Force (Split-Path $outFull) | Out-Null
    if (Test-Path $outFull) { Remove-Item $outFull -Force }
    $request = [ordered]@{
        label = $label; seed = [int]$seed; captureDeltaTime = 0.016666668; bossAtSeconds = $BossAt
        killBossAtSeconds = $KillBossAt; maxSimSeconds = $MaxSim; pauseResponseFrames = 3; pilot = 'player-v1'
        outPath = $outFull; scene = $scene; commit = $commit; stopPlayModeWhenDone = $true
        createdUtcTicks = [DateTime]::UtcNow.Ticks
    }
    $reqPath = Join-Path $work "$label.json"
    $request | ConvertTo-Json | Set-Content -Path $reqPath -Encoding utf8
    $logPath = Join-Path $work "$label.log"
    $argLine = "-v4trace `"$reqPath`" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -popupwindow -logFile `"$logPath`""
    $p = Start-Process -FilePath $exe -ArgumentList $argLine -PassThru
    return [pscustomobject]@{ Label = $label; Out = $outFull; Proc = $p; Started = $sw.Elapsed.TotalSeconds }
}

while ($queue.Count -gt 0 -or $running.Count -gt 0) {
    while ($queue.Count -gt 0 -and $running.Count -lt $MaxParallel) { $running += Start-Run $queue.Dequeue() }
    Start-Sleep -Seconds 2
    $still = @()
    foreach ($r in $running) {
        $age = $sw.Elapsed.TotalSeconds - $r.Started
        if ($r.Proc.HasExited) {
            $ok = Test-Path $r.Out
            $results += [pscustomobject]@{ Label = $r.Label; Seconds = [int]$age; Ok = $ok; Out = $r.Out }
        } elseif ($age -gt $TimeoutSeconds) {
            Stop-Process -Id $r.Proc.Id -Force
            $results += [pscustomobject]@{ Label = $r.Label; Seconds = [int]$age; Ok = $false; Out = 'timeout' }
        } else { $still += $r }
    }
    $running = $still
}
$wall = [int]$sw.Elapsed.TotalSeconds

# Restore the player's PlayerPrefs exactly as they were.
reg delete $prefsKey /f 2>$null | Out-Null
if ($hadPrefs) { reg import $prefsBackup 2>$null | Out-Null }

$results | Sort-Object Label | ForEach-Object { "{0,-14} {1,5}s  {2}" -f $_.Label, $_.Seconds, ($(if ($_.Ok) { 'ok' } else { 'FAILED ' + $_.Out })) }
"wall ${wall}s for $($Runs.Count) runs, max $MaxParallel in parallel"
if ($results | Where-Object { -not $_.Ok }) { exit 1 }
