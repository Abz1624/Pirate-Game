# V5 helper: recompile the open Editor, wait for it, then optionally run tests and wait for them (with a stall watchdog).
# Usage:  powershell -File plan/v5/tools/unity-run.ps1 -Mode EditMode [-Filter V5FlowM1Tests] [-Out path.json]
#         powershell -File plan/v5/tools/unity-run.ps1 -CompileOnly
param(
    [string]$Mode = "EditMode",
    [string]$Filter = "",
    [string]$Out = "",
    [switch]$CompileOnly,
    [int]$StallMinutes = 15
)
$ErrorActionPreference = "Continue"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
$env:UNITY_PROJECT_PATH = Join-Path $root "Pirate Game Unity"
$statusFile = Join-Path $env:UNITY_PROJECT_PATH "Temp\pipeline_test_status.json"

function Wait-Ready([int]$seconds = 600) {
    $deadline = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $deadline) {
        $st = unity status --json 2>$null | Out-String
        if ($st -match '"state": "ready"') {
            $c = unity command console --tail 1 --level error 2>$null | Out-String
            if ($c -match '"compiling":false') { return $c }
        }
        Start-Sleep -Seconds 3
    }
    throw "Editor not ready after $seconds s"
}

unity command recompile 2>$null | Out-Null
Start-Sleep -Seconds 2
$c = Wait-Ready
if ($c -match '"compilationFailed":true') {
    Write-Output "COMPILE FAILED"
    unity command console --tail 40 --level error | Out-String | Write-Output
    exit 2
}
Write-Output "compiled OK"
if ($CompileOnly) { exit 0 }

if (Test-Path $statusFile) { Remove-Item $statusFile -Force }
$args = @("command", "run_tests", "--mode", $Mode, "--async_tests", "true")
if ($Filter) { $args += @("--filter", $Filter) }
& unity @args | Out-Null

# Watchdog: progress = the Editor's CPU time still growing. No growth for $StallMinutes minutes = stalled.
$editorPid = ((unity status --json 2>$null | Out-String) | ConvertFrom-Json).data.instances[0].pid
$lastCpu = -1; $lastChange = Get-Date
while ($true) {
    Start-Sleep -Seconds 5
    if (Test-Path $statusFile) {
        $raw = Get-Content $statusFile -Raw
        if ($raw -match '"status"\s*:\s*"(completed|failed|error|aborted)"') { break }
    }
    $p = Get-Process -Id $editorPid -ErrorAction SilentlyContinue
    if ($null -eq $p) { Write-Output "EDITOR GONE (pid $editorPid)"; exit 4 }
    if ($p.CPU -gt $lastCpu + 1) { $lastCpu = $p.CPU; $lastChange = Get-Date }
    if (((Get-Date) - $lastChange).TotalMinutes -gt $StallMinutes) {
        Write-Output "STALLED: Editor CPU flat for $StallMinutes minutes"
        unity command cancel_tests 2>$null | Out-Null
        exit 3
    }
}
$j = Get-Content $statusFile -Raw | ConvertFrom-Json
if ($Out) { Copy-Item $statusFile $Out -Force }
Write-Output ("{0}: {1} total, {2} passed, {3} failed, {4} skipped, {5:0.0} s" -f $Mode, $j.summary.total, $j.summary.passed, $j.summary.failed, $j.summary.skipped, $j.duration)
$j.results | Where-Object { $_.Status -ne "Passed" } | ForEach-Object { Write-Output ("FAIL {0}`n  {1}" -f $_.FullName, $_.Message) }
if ($j.summary.failed -gt 0) { exit 1 } else { exit 0 }
