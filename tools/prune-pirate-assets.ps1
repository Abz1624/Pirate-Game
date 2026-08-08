<#
.SYNOPSIS
  Prunes redundant images from Pirate Assets, keeping one GLTF + one thumbnail per asset.

.DESCRIPTION
  Removes:
    - Voxel part previews (*.vxm.png, *.vxr.png, *.vxa.png)
    - Orthographic render folders (PNG/)
    - Duplicate thumbnail sizes and rarity variants
    - Animated preview stills
    - Known duplicate _2 folders (Sets_2, World Bosses_2)

  Keeps all files under:
    - Combat Cards (card art is the content)
    - Terrain Textures (tile set needed for levels)

.PARAMETER WhatIf
  Report what would be deleted without deleting.
#>
param(
    [string]$AssetsRoot = (Join-Path $PSScriptRoot "..\Pirate Game Unity\Assets\Pirate Assets"),
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

$PreserveAllImagesUnder = @(
    'Combat Cards',
    'Terrain Textures'
)

$DuplicateFolders = @(
    'Voxel Game Assets\world items\Sets_2',
    'Voxel Game Assets\world items\World Bosses_2'
)

$ArchiveFolderPattern = '-20\d{6}T\d{6}Z-\d{3}$'
$DateOnlyFolderPattern = '^\d{8}$'

function Remove-AssetPath([string]$Path) {
    if (-not (Test-Path $Path)) { return }
    if ($WhatIf) {
        Write-Host "  [dry-run] delete $Path"
        return
    }
    Remove-Item $Path -Recurse -Force -ErrorAction SilentlyContinue
}

function Remove-AssetFile([string]$Path) {
    if (-not (Test-Path $Path)) { return }
    if ($WhatIf) {
        return
    }
    Remove-Item $Path -Force -ErrorAction SilentlyContinue
    $meta = "$Path.meta"
    if (Test-Path $meta) { Remove-Item $meta -Force -ErrorAction SilentlyContinue }
}

function Test-PreserveAllImages([string]$fullPath) {
    foreach ($segment in $PreserveAllImagesUnder) {
        if ($fullPath -match [regex]::Escape($segment)) { return $true }
    }
    return $false
}

function Move-AssetItem([string]$Source, [string]$Destination) {
    if (-not (Test-Path $Source)) { return }
    $destParent = Split-Path $Destination -Parent
    if ($destParent -and -not (Test-Path $destParent)) {
        New-Item -ItemType Directory -Path $destParent -Force | Out-Null
    }
    if ($WhatIf) {
        Write-Host "  [dry-run] move $Source -> $Destination"
        return
    }
    Move-Item -Path $Source -Destination $Destination -Force
    $sourceMeta = "$Source.meta"
    $destMeta = "$Destination.meta"
    if (Test-Path $sourceMeta) {
        Move-Item -Path $sourceMeta -Destination $destMeta -Force
    }
}

function Get-FlattenedArchiveLeaf([string]$archiveDir) {
    $archiveLeaf = Split-Path $archiveDir -Leaf
    $archiveBase = $archiveLeaf -replace $ArchiveFolderPattern, ''
    $current = $archiveDir
    while ($true) {
        $children = @(Get-ChildItem $current -Force | Where-Object { -not $_.Name.EndsWith('.meta') })
        if ($children.Count -ne 1 -or -not $children[0].PSIsContainer) {
            break
        }
        $child = $children[0]
        if ($child.Name -match $DateOnlyFolderPattern) {
            $current = $child.FullName
            continue
        }
        if ($child.Name -eq $archiveBase) {
            $current = $child.FullName
            continue
        }
        break
    }
    return $current
}

function Get-AssetRoot([string]$gltfPath, [string]$assetsRootFull) {
    $dir = Split-Path $gltfPath -Parent
    $leaf = Split-Path $dir -Leaf
    if ($leaf -eq 'GLTF') { $dir = Split-Path $dir -Parent }
    if ((Split-Path $dir -Leaf) -eq 'VE') { $dir = Split-Path $dir -Parent }
    return $dir
}

function Select-BestThumbnail($candidates) {
    $ordered = $candidates | Sort-Object {
        $name = $_.Name
        if ($name -match '^ui_item_.*\.png$') { return 0 }
        if ($name -eq 'thumbnail_x512.png') { return 1 }
        if ($name -eq 'thumbnail.png') { return 2 }
        if ($name -match '^thumbnail_with_rarity_x512\.png$') { return 3 }
        if ($name -match '^thumbnail') { return 4 }
        return 5
    }, { -$_.Length }
    return $ordered | Select-Object -First 1
}

if (-not (Test-Path $AssetsRoot)) {
    throw "Assets root not found: $AssetsRoot"
}

$assetsRootFull = (Resolve-Path $AssetsRoot).Path.TrimEnd('\')
$stats = [ordered]@{
    UnusableFolders = 0
    ArchiveFlattened = 0
    Phase1Deleted = 0
    Phase1Bytes   = 0
    Phase2Deleted = 0
    Phase2Bytes   = 0
    ThumbsKept    = 0
    DupFolders    = 0
}

$sw = [System.Diagnostics.Stopwatch]::StartNew()
Write-Host "Pruning: $assetsRootFull"
if ($WhatIf) { Write-Host "(dry-run mode)`n" }

# --- Phase 0a: remove deprecated Unusable asset trees ---
Write-Host "Phase 0a: removing Unusable folders..."
Get-ChildItem $assetsRootFull -Recurse -Directory -Force |
    Where-Object { $_.Name -eq 'Unusable' } |
    ForEach-Object {
        Write-Host "Removing Unusable folder: $($_.FullName.Substring($assetsRootFull.Length))"
        Remove-AssetPath $_.FullName
        Remove-AssetFile "$($_.FullName).meta"
        $stats.UnusableFolders++
    }

# --- Phase 0b: flatten zip/download archive wrappers (shortens Windows paths) ---
Write-Host "`nPhase 0b: flattening archive wrapper folders..."
Get-ChildItem $assetsRootFull -Recurse -Directory -Force |
    Where-Object { $_.Name -match $ArchiveFolderPattern } |
    Sort-Object { $_.FullName.Length } -Descending |
    ForEach-Object {
        $archiveDir = $_.FullName
        $leafDir = Get-FlattenedArchiveLeaf $archiveDir
        if ($leafDir -eq $archiveDir) { return }

        $leafName = Split-Path $leafDir -Leaf
        $destDir = Join-Path (Split-Path $archiveDir -Parent) $leafName
        if ((Test-Path $destDir) -and ($destDir -ne $leafDir)) {
            Write-Warning "Skip flattening archive; destination exists: $destDir"
            return
        }

        Write-Host "Flattening archive: $($archiveDir.Substring($assetsRootFull.Length))"
        Move-AssetItem $leafDir $destDir
        Remove-AssetPath $archiveDir
        Remove-AssetFile "$archiveDir.meta"
        $stats.ArchiveFlattened++
    }

# --- Phase 0c: remove known duplicate folder trees ---
foreach ($rel in $DuplicateFolders) {
    $dupPath = Join-Path $assetsRootFull $rel
    if (Test-Path $dupPath) {
        $size = (Get-ChildItem $dupPath -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
        Write-Host "Removing duplicate folder: $rel"
        Remove-AssetPath $dupPath
        Remove-AssetFile "$dupPath.meta"
        $stats.DupFolders++
        $stats.Phase1Bytes += $size
    }
}

# --- Phase 1: global redundant image patterns ---
Write-Host "`nPhase 1: redundant image patterns..."

$imageExt = @('.png', '.jpg', '.jpeg', '.gif', '.webp', '.bmp', '.tga', '.tif', '.tiff')
Get-ChildItem $assetsRootFull -Recurse -File -Force | ForEach-Object {
    $file = $_
    if ($imageExt -notcontains $file.Extension.ToLower()) { return }
    if (Test-PreserveAllImages $file.FullName) { return }

    $delete = $false
    $name = $file.Name
    $path = $file.FullName

    if ($name -match '\.(vxm|vxr|vxa)\.png$') { $delete = $true }
    if ($path -match '\\PNG\\') { $delete = $true }
    if ($name -eq 'thumbnail_animated_preview.png') { $delete = $true }
    if ($name -match '^thumbnail_x(64|128|256)\.png$') { $delete = $true }
    if ($name -match '^thumbnail_with_rarity') { $delete = $true }
    if ($path -match '\\Thumbnails\\') { $delete = $true }
    if ($name -match '_(Front|Back|Left|Right)\.png$') { $delete = $true }
    if ($name -match '_[AB]\.png$' -and $path -match '\\PNG\\') { $delete = $true }

    if ($delete) {
        $stats.Phase1Bytes += $file.Length
        $stats.Phase1Deleted++
        if ($WhatIf) {
            if ($stats.Phase1Deleted -le 20) { Write-Host "  [dry-run] $($file.FullName.Substring($assetsRootFull.Length))" }
        } else {
            Remove-AssetFile $file.FullName
        }
    }
}

if ($WhatIf -and $stats.Phase1Deleted -gt 20) {
    Write-Host "  [dry-run] ... and $($stats.Phase1Deleted - 20) more phase-1 files"
}

# --- Phase 2: one thumbnail per GLTF asset bundle ---
Write-Host "`nPhase 2: consolidate thumbnails per asset..."

$gltfFiles = Get-ChildItem $assetsRootFull -Recurse -File -Include *.gltf, *.glb -Force |
    Where-Object { -not (Test-PreserveAllImages $_.FullName) }

$assetRoots = $gltfFiles | ForEach-Object { Get-AssetRoot $_.FullName $assetsRootFull } | Sort-Object -Unique

foreach ($assetRoot in $assetRoots) {
    $images = Get-ChildItem $assetRoot -Recurse -File -Force |
        Where-Object {
            $imageExt -contains $_.Extension.ToLower() -and
            $_.FullName -notmatch '\\GLTF\\' -and
            -not (Test-PreserveAllImages $_.FullName)
        }

    if (-not $images -or $images.Count -eq 0) { continue }

    $keeper = Select-BestThumbnail $images
    if (-not $keeper) { continue }

    $thumbDir = Join-Path $assetRoot 'Images'
    $destFile = Join-Path $thumbDir 'thumbnail.png'

    if (-not $WhatIf) {
        if (-not (Test-Path $thumbDir)) {
            New-Item -ItemType Directory -Path $thumbDir -Force | Out-Null
        }
        if ($keeper.FullName -ne $destFile) {
            Copy-Item $keeper.FullName $destFile -Force
        }
    }

    foreach ($img in $images) {
        if ($img.FullName -eq $keeper.FullName -and $keeper.FullName -eq $destFile) { continue }
        if (-not $WhatIf -and $img.FullName -eq $destFile) { continue }
        $stats.Phase2Bytes += $img.Length
        $stats.Phase2Deleted++
        if ($WhatIf) { continue }
        Remove-AssetFile $img.FullName
    }

    if (-not $WhatIf -and $keeper.FullName -ne $destFile) {
        Remove-AssetFile $keeper.FullName
    }

    $stats.ThumbsKept++
}

# --- Phase 3: remove empty directories ---
if (-not $WhatIf) {
    Write-Host "`nPhase 3: removing empty folders..."
    $empty = @()
    do {
        $empty = Get-ChildItem $assetsRootFull -Recurse -Directory -Force |
            Where-Object { (Get-ChildItem $_.FullName -Force | Measure-Object).Count -eq 0 }
        foreach ($dir in $empty) {
            Remove-Item $dir.FullName -Force -ErrorAction SilentlyContinue
            $meta = "$($dir.FullName).meta"
            if (Test-Path $meta) { Remove-Item $meta -Force }
        }
    } while ($empty.Count -gt 0)
}

$sw.Stop()
$totalSaved = $stats.Phase1Bytes + $stats.Phase2Bytes

Write-Host ""
Write-Host "=== Prune complete ($($sw.Elapsed.ToString('mm\:ss'))) ==="
Write-Host ("Phase 1 deleted: {0} files ({1:N1} MB)" -f $stats.Phase1Deleted, ($stats.Phase1Bytes/1MB))
Write-Host ("Phase 2 deleted: {0} files ({1:N1} MB)" -f $stats.Phase2Deleted, ($stats.Phase2Bytes/1MB))
Write-Host ("Thumbnails kept: {0} asset bundles" -f $stats.ThumbsKept)
Write-Host ("Unusable folders removed: {0}" -f $stats.UnusableFolders)
Write-Host ("Archive wrappers flattened: {0}" -f $stats.ArchiveFlattened)
Write-Host ("Duplicate folders removed: {0}" -f $stats.DupFolders)
Write-Host ("Estimated space saved: {0:N2} GB" -f ($totalSaved/1GB))

if (-not $WhatIf) {
    $remaining = (Get-ChildItem $assetsRootFull -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
    $remainingCount = (Get-ChildItem $assetsRootFull -Recurse -File -ErrorAction SilentlyContinue).Count
    Write-Host ("Remaining: {0:N2} GB, {1:N0} files" -f ($remaining/1GB), $remainingCount)
}
