# Quick helpers for Unity CLI in this repo.
# Usage: .\tools\unity-cli.ps1 status | open | test | commands

param(
    [Parameter(Position = 0)]
    [ValidateSet("status", "open", "test", "commands", "doctor", "pipeline")]
    [string]$Action = "status"
)

$ProjectRoot = Split-Path $PSScriptRoot -Parent
$UnityProject = Join-Path $ProjectRoot "Pirate Game Unity"
$env:UNITY_PROJECT_PATH = $UnityProject

switch ($Action) {
    "status"   { unity status --json --project-path $UnityProject }
    "open"     { unity open $UnityProject }
    "test"     { unity test $UnityProject --mode EditMode --output (Join-Path $ProjectRoot "test-results.xml") }
    "commands" { unity command --project-path $UnityProject }
    "doctor"   { unity doctor }
    "pipeline" { unity pipeline list }
}
