# Pirate Assets

Gameplay assets — **one GLTF model + one thumbnail per asset bundle**, plus full card art and terrain tiles.

## What's included

| Folder | Contents |
| --- | --- |
| `Combat Cards/Images/` | All 151 power-up card PNGs |
| `Voxel Game Assets/ships/` | Ship GLTF + `Images/thumbnail.png` |
| `Voxel Game Assets/Mob Enemies/` | Enemy GLTF + thumbnail per variant |
| `Voxel Game Assets/world items/` | Terrain, decorations, bosses, buoys, VFX, UI |
| `Voxel Game Assets/chests/` | Loot chest models + thumbnails |
| `Voxel Game Assets/collectibles/` | Keys, trophies, tokens |
| `Voxel Game Assets/resources/` | Gold, ore, pickups |
| `Voxel Game Assets/Achievements/` | Rank trophies |
| `Voxel Game Assets/New Crafted Items/` | Crafted item models |

## Per-asset layout

```
ships/Voyager Ships/item_5x10_piratefrigatevoyager/
├── GLTF/              (or VE/GLTF/)
│   └── item_5x10_piratefrigatevoyager.gltf
└── Images/
    └── thumbnail.png  ← single UI/store icon
```

**Removed during prune:** voxel part previews (`*.vxm.png`), orthographic renders (`PNG/Front.png` etc.), duplicate thumbnail sizes (x64/x128/x256), rarity variants, animated preview stills, duplicate `_2` folders.

**Preserved as-is:** all Combat Cards, all Terrain Textures.

## Maintenance scripts

```powershell
# Full re-import from references (runs prune automatically)
.\tools\import-pirate-assets.ps1

# Prune only (after manual changes)
.\tools\prune-pirate-assets.ps1
```

## Unity usage

Uses **Unity glTFast** (`com.unity.cloud.gltfast`).

1. **Pirate Game → GLTF → Reimport All Pirate GLTFs**
2. **Pirate Game → GLTF → Instantiate Selected In Scene** (`Ctrl+Shift+G`)
3. Use `Images/thumbnail.png` for UI icons (set Texture Type to Sprite)
