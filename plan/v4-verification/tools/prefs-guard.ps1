# Backs up / restores the Editor's PlayerPrefs for this project around V3 test suite runs.
# V3 tests write real PlayerPrefs (defect register R-67): V3IntegrationTests deletes v3_home_seed and
# v3_first_conquest_complete, and PlayMode boss kills lock a home seed. Run -Save before a suite and -Restore after.
# The Editor caches PlayerPrefs in memory: restore with the Editor closed, or it may write its cached values back.
param([switch] $Save, [switch] $Restore, [string] $Backup = (Join-Path $env:TEMP 'pirate-game-editor-prefs.reg'))
$key = 'HKCU\Software\Unity\UnityEditor\DefaultCompany\Pirate Game'
if ($Save) {
    reg export $key $Backup /y | Out-Null
    Write-Output "saved $key -> $Backup"
}
elseif ($Restore) {
    if (-not (Test-Path $Backup)) { Write-Error "no backup at $Backup"; exit 1 }
    reg delete $key /f | Out-Null
    reg import $Backup | Out-Null
    Write-Output "restored $key from $Backup"
}
else { Write-Output 'usage: prefs-guard.ps1 -Save | -Restore [-Backup path]' }
