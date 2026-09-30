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

# A modified scene makes the test runner ask "save changes?" in a modal dialog that blocks the Editor (seen after a forced
# restart's scene recovery). Never discard anyone's edits: stop and name the modified scenes instead.
$check = Join-Path $env:TEMP "v5-dirty.cs"
Set-Content -Path $check -Encoding UTF8 -Value @'
var dirty = new System.Collections.Generic.List<string>();
for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++) { var sc = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i); if (sc.isDirty) dirty.Add(string.IsNullOrEmpty(sc.path) ? sc.name : sc.path); }
return dirty.Count == 0 ? "clean" : "DIRTY: " + string.Join(", ", dirty);
'@
$d = unity command eval_file --file $check 2>&1 | Out-String
if ($d -match '"result":"(DIRTY:[^"]*)"') {
    Write-Output "UNSAVED SCENES ($($Matches[1])): save or discard them in the Editor, then run again (tests would block on a save prompt)"
    exit 5
}
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
        # A modal Editor dialog is the usual cause: name it so it can be answered.
        Add-Type -TypeDefinition 'using System; using System.Text; using System.Collections.Generic; using System.Runtime.InteropServices; public static class V5W { public delegate bool P(IntPtr h, IntPtr l); [DllImport("user32.dll")] public static extern bool EnumWindows(P f, IntPtr l); [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p); [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h); [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n); [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n); public static List<string> Dialogs(uint pid) { var r = new List<string>(); EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); if (p == pid && IsWindowVisible(h)) { var c = new StringBuilder(64); GetClassName(h, c, 64); if (c.ToString() == "#32770") { var t = new StringBuilder(256); GetWindowText(h, t, 256); r.Add(t.ToString()); } } return true; }, IntPtr.Zero); return r; } }' -ErrorAction SilentlyContinue
        [V5W]::Dialogs([uint32]$editorPid) | ForEach-Object { Write-Output "OPEN DIALOG: $_" }
        unity command cancel_tests 2>$null | Out-Null
        exit 3
    }
}
$j = Get-Content $statusFile -Raw | ConvertFrom-Json
if ($Out) { Copy-Item $statusFile $Out -Force }
Write-Output ("{0}: {1} total, {2} passed, {3} failed, {4} skipped, {5:0.0} s" -f $Mode, $j.summary.total, $j.summary.passed, $j.summary.failed, $j.summary.skipped, $j.duration)
$j.results | Where-Object { $_.Status -ne "Passed" } | ForEach-Object { Write-Output ("FAIL {0}`n  {1}" -f $_.FullName, $_.Message) }
if ($j.summary.failed -gt 0) { exit 1 } else { exit 0 }
