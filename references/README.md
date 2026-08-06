# Reference assets

## Pirate Nation Art

**Use this folder:** `references/piratenation-art-full/`  
Complete local Windows copy of all **154,280** assets (includes `Voxel Game Assets/ships`, mobs, world items, etc.).

Also available via WSL: `\\wsl$\Ubuntu\home\kshitij\piratenation-refs\piratenation-art`

`references/piratenation-art/` is an older incomplete checkout (locked if Cursor has files open from it). Safe to delete after closing those tabs, once you are using `-full`.

Trailing spaces / illegal Windows path characters were renamed in the full tree (211 path fixes) so NTFS can store everything.

**Git LFS:** Most media (`png`, `svg`, `gltf`, `mp4`, `vxm`, …) is stored in Git LFS. A plain clone only has tiny pointer files until you run `git lfs pull`.

Full LFS content has been pulled (~45 GB / 150k+ objects) into the WSL clone and synced into `piratenation-art-full`.

### Browse visually

```powershell
node tools/asset-browser/serve.mjs
```

Opens http://127.0.0.1:8765 (auto-selects `piratenation-art-full`).
