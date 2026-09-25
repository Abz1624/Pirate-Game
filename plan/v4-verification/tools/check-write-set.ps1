# Fails if any file changed since -Since (commit) lies outside the V4 allowed write set (plan D1).
# Usage: .\check-write-set.ps1 -Since <commit> [-Phase 0]
param(
    [Parameter(Mandatory)] [string] $Since,
    [int] $Phase = 0
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
Push-Location $repo
try {
    $changed = @()
    $changed += git diff --name-only $Since HEAD
    $changed += git diff --name-only
    $changed += git diff --name-only --cached
    $changed += git ls-files --others --exclude-standard
    $changed = $changed | Where-Object { $_ } | Sort-Object -Unique

    $allowed = @(
        '^Pirate Game Unity/Assets/_Game/V4/',
        '^Pirate Game Unity/Assets/_Game/V4\.meta$',
        '^Pirate Game Unity/Assets/_Game/Scenes/V4/',
        '^Pirate Game Unity/Assets/_Game/Scenes/V4\.meta$',
        '^Pirate Game Unity/Assets/_Game/Tests/V4/',
        '^Pirate Game Unity/Assets/_Game/Tests/V4\.meta$',
        '^Pirate Game Unity/ProjectSettings/EditorBuildSettings\.asset$',
        '^plan/v4-verification/',
        '^docs/V4_IMPLEMENTATION_PLAN\.md$',
        '^docs/GAMEPLAY_FLOW_AND_MODULES\.md$'
    )
    if ($Phase -ge 15) { $allowed += '.*' }

    $violations = $changed | Where-Object {
        $path = $_
        -not ($allowed | Where-Object { $path -match $_ })
    }

    Write-Output "Changed files since ${Since}: $($changed.Count)"
    if ($violations) {
        Write-Output "WRITE-SET VIOLATIONS ($($violations.Count)):"
        $violations | ForEach-Object { Write-Output "  $_" }
        exit 1
    }
    Write-Output "Write set clean."
}
finally { Pop-Location }
