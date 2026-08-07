<#
.SYNOPSIS
  Copies gameplay-relevant Pirate Nation assets into Unity's Pirate Assets folder.

.DESCRIPTION
  Imports only models (GLTF/GLB) and images from selected reference categories.
  Skips videos, voxel sources, audio, and other file types.

  Categories:
    - Combat Cards
    - Voxel Game Assets: ships, Mob Enemies, New Crafted Items, world items,
      chests, collectibles, resources, Achievements

  Each source folder keeps typed subfolders:
    GLTF/   - .gltf, .glb
    Images/ - .png, .jpg, .svg, etc.

.PARAMETER Force
  Re-copy even when destination file exists with the same size.
#>
param(
    [string]$SourceRoot = (Join-Path $PSScriptRoot "..\references\piratenation-art-full"),
    [string]$DestRoot   = (Join-Path $PSScriptRoot "..\engine\unity\Pirate Game\Assets\Pirate Assets"),
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$VideoExt = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]@('.mp4', '.mov', '.webm', '.avi', '.mkv', '.m4v', '.wmv'),
    [StringComparer]::OrdinalIgnoreCase
)
$GltfExt = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]@('.gltf', '.glb'),
    [StringComparer]::OrdinalIgnoreCase
)
$ImageExt = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]@('.png', '.jpg', '.jpeg', '.svg', '.gif', '.webp', '.bmp', '.tga', '.tif', '.tiff'),
    [StringComparer]::OrdinalIgnoreCase
)
$SkipExt = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]@('.meta', '.ds_store'),
    [StringComparer]::OrdinalIgnoreCase
)

# Relative paths under $SourceRoot
$SourceCategories = @(
    'Combat Cards',
    'Voxel Game Assets\ships',
    'Voxel Game Assets\Mob Enemies',
    'Voxel Game Assets\New Crafted Items',
    'Voxel Game Assets\world items',
    'Voxel Game Assets\chests',
    'Voxel Game Assets\collectibles',
    'Voxel Game Assets\resources',
    'Voxel Game Assets\Achievements'
)

function Get-TypeFolder([string]$extension) {
    if ($GltfExt.Contains($extension)) { return 'GLTF' }
    if ($ImageExt.Contains($extension)) { return 'Images' }
    return $null
}

function Should-SkipRedundantImage([string]$filePath, [string]$fileName) {
    if ($filePath -match '\\PNG\\') { return $true }
    if ($filePath -match '\\Thumbnails\\') { return $true }
    if ($fileName -match '\.(vxm|vxr|vxa)\.png$') { return $true }
    if ($fileName -eq 'thumbnail_animated_preview.png') { return $true }
    if ($fileName -match '^thumbnail_x(64|128|256)\.png$') { return $true }
    if ($fileName -match '^thumbnail_with_rarity') { return $true }
    if ($fileName -match '_(Front|Back|Left|Right)\.png$') { return $true }
    return $false
}

function Get-ImportRelativeDir([string]$relativeDir) {
    $parts = $relativeDir -split '\\'
    $shortParts = [System.Collections.Generic.List[string]]::new()
    foreach ($part in $parts) {
        if ($part -eq 'Unusable') { return $null }
        if ($part -match '^(?<base>.+)-20\d{6}T\d{6}Z-\d{3}$') {
            $shortParts.Add($Matches.base)
            continue
        }
        if ($part -match '^\d{8}$') { continue }
        $shortParts.Add($part)
    }
    if ($shortParts.Count -eq 0) { return $null }
    return [string]::Join('\', $shortParts)
}

function Should-CopyFile([string]$extension, [string]$filePath, [string]$fileName) {
    if ($SkipExt.Contains($extension)) { return $false }
    if ($VideoExt.Contains($extension)) { return $false }
    if ($filePath -match '\\Unusable\\') { return $false }
    $typeFolder = Get-TypeFolder $extension
    if ($typeFolder -eq $null) { return $false }
    if ($typeFolder -eq 'Images' -and $filePath -notmatch 'Combat Cards|Terrain Textures') {
        if (Should-SkipRedundantImage $filePath $fileName) { return $false }
    }
    return $true
}

if (-not (Test-Path $SourceRoot)) {
    throw "Source not found: $SourceRoot"
}

$sourceRootFull = (Resolve-Path $SourceRoot).Path.TrimEnd('\')
if (-not (Test-Path $DestRoot)) {
    New-Item -ItemType Directory -Path $DestRoot -Force | Out-Null
}
$destRootFull = (Resolve-Path $DestRoot).Path.TrimEnd('\')

$stats = [ordered]@{
    Copied  = 0
    Skipped = 0
    Ignored = 0
    Videos  = 0
}

$sw = [System.Diagnostics.Stopwatch]::StartNew()
Write-Host "Selective import from: $sourceRootFull"
Write-Host "                  to: $destRootFull"
Write-Host ""

foreach ($category in $SourceCategories) {
    $categoryPath = Join-Path $sourceRootFull $category
    if (-not (Test-Path $categoryPath)) {
        Write-Warning "Category not found, skipping: $category"
        continue
    }

    Write-Host ">> $category"

    Get-ChildItem -Path $categoryPath -Recurse -File -Force | ForEach-Object {
        $file = $_
        $ext = $file.Extension

        if ($VideoExt.Contains($ext)) {
            $stats.Videos++
            return
        }

        $typeFolder = Get-TypeFolder $ext
        if (-not (Should-CopyFile $ext $file.FullName $file.Name)) {
            $stats.Ignored++
            return
        }

        $relativeDir = $file.DirectoryName.Substring($sourceRootFull.Length).TrimStart('\')
        $relativeDir = Get-ImportRelativeDir $relativeDir
        if (-not $relativeDir) {
            $stats.Ignored++
            return
        }
        $destDir = Join-Path $destRootFull (Join-Path $relativeDir $typeFolder)
        $destFile = Join-Path $destDir $file.Name

        if (-not (Test-Path $destDir)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }

        if ((Test-Path $destFile) -and -not $Force) {
            $existing = Get-Item $destFile
            if ($existing.Length -eq $file.Length) {
                $stats.Skipped++
                return
            }
        }

        Copy-Item -Path $file.FullName -Destination $destFile -Force
        $stats.Copied++

        if ($stats.Copied % 2000 -eq 0) {
            $elapsed = $sw.Elapsed.ToString('hh\:mm\:ss')
            Write-Host "   [$elapsed] Copied $($stats.Copied) files..."
        }
    }
}

$sw.Stop()
Write-Host ""
Write-Host "=== Import complete ($($sw.Elapsed.ToString('hh\:mm\:ss'))) ==="
$stats.GetEnumerator() | ForEach-Object { Write-Host ("{0,-8} {1}" -f ($_.Key + ':'), $_.Value) }

Write-Host ""
Write-Host "Running post-import prune to keep one thumbnail per asset..."
& (Join-Path $PSScriptRoot "prune-pirate-assets.ps1") -AssetsRoot $DestRoot
