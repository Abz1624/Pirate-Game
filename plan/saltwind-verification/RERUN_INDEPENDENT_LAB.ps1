# Read-only simulation rerun using the existing Unity-compiled, pure simulation assembly.
# This does not run Unity tests, import assets, or write player saves.
param([int]$SeedsPerCell = 100)
$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '../..')
$dll = Join-Path $repo 'Pirate Game Unity/Library/ScriptAssemblies/RoguePirates.Simulation.dll'
Add-Type -Path $dll
$json = (Get-Content -Raw (Join-Path $PSScriptRoot 'profile-export/SaltwindSoloStageBalanceProfile.json')).Trim()
$export = [RoguePirates.Simulation.Balance.StageBalanceProfileExportCodec]::FromCanonicalJson($json)
$export.ContentHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($json))).ToLowerInvariant()
$options = [RoguePirates.Simulation.Balance.SaltwindBalanceLabBatchOptions]::new()
$options.BaseSeed = 12000
$options.SeedsPerCell = $SeedsPerCell
$report = [RoguePirates.Simulation.Balance.SaltwindBalanceLabBatchRunner]::RunMatrix($export, $options)
$output = Join-Path $PSScriptRoot 'independent-v6-net'
[RoguePirates.Simulation.Balance.SaltwindBalanceLabBatchRunner]::WriteReportFiles($report, $output)
@{
    observedAtUtc = [DateTime]::UtcNow.ToString('o')
    hostRuntime = [System.Runtime.InteropServices.RuntimeInformation]::FrameworkDescription
    assemblySha256 = (Get-FileHash $dll -Algorithm SHA256).Hash.ToLowerInvariant()
    assemblyLastWriteUtc = (Get-Item $dll).LastWriteTimeUtc.ToString('o')
    canonicalExportByteHash = $export.ContentHash
    conservativeBossHullScale = [RoguePirates.Simulation.Balance.SaltwindLabMatrixCalibration]::ConservativeBossHullScale
    sourceCommit = (git -C $repo rev-parse HEAD)
    seedsPerCell = $SeedsPerCell
    baseSeed = 12000
    limitation = 'Existing compiled assembly under .NET, not a fresh Unity compilation or NUnit/PlayMode run. Simulation stops at boss victory; not overtime survival evidence.'
} | ConvertTo-Json | Set-Content (Join-Path $output 'independent-provenance.json')
$report.ToHumanSummary()
