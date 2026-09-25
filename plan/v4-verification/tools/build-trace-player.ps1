# Builds the Windows development player used for parallel trace runs, without touching the open Editor.
# Mirrors Assets/Packages/ProjectSettings into a private copy of the project (its own Library, Windows target)
# and runs a batch-mode Unity there. Only changed files are copied, so later builds reimport little.
# Usage: .\build-trace-player.ps1   (prints the player path; exit code 0 on success)
param(
    [string] $Root = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path 'Pirate Game Unity/Builds/V4Trace'),
    [string] $Unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Continue'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$src = Join-Path $repo 'Pirate Game Unity'
$copy = Join-Path $Root 'project'
$buildDir = Join-Path $Root 'build'
$log = Join-Path $Root 'build.log'
New-Item -ItemType Directory -Force $copy, $buildDir | Out-Null

$sw = [Diagnostics.Stopwatch]::StartNew()
foreach ($dir in 'Assets', 'Packages', 'ProjectSettings') {
    # /MIR mirrors (deletes files removed at the source); robocopy exit codes below 8 mean success.
    robocopy (Join-Path $src $dir) (Join-Path $copy $dir) /MIR /NFL /NDL /NJH /NJS /NP /R:2 /W:1 | Out-Null
    if ($LASTEXITCODE -ge 8) { Write-Error "robocopy failed for $dir ($LASTEXITCODE)"; exit 1 }
}
$mirror = [int]$sw.Elapsed.TotalSeconds

& $Unity -batchmode -quit -nographics -projectPath $copy -buildTarget Win64 `
    -executeMethod RoguePirates.V4.Editor.V4TraceBuild.BuildFromCommandLine -v4BuildOut $buildDir -logFile $log | Out-Null
$code = $LASTEXITCODE
$total = [int]$sw.Elapsed.TotalSeconds
$exe = Join-Path $buildDir 'PirateV4Trace.exe'
Write-Output "mirror ${mirror}s, total ${total}s, exit $code, player: $exe"
if ($code -ne 0 -or -not (Test-Path $exe)) {
    Get-Content $log -Tail 40 | Where-Object { $_ -match 'error|Error|V4TraceBuild' }
    exit 1
}
