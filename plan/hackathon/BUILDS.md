# Hackathon builds (12 Oct 2026)

Branch `hackathon-demo`, final rebuild 12 Oct 15:16-16:05 at `fad370cf` (island defence designer and conquest raids, boss teleport fix, snapshot minimap; earlier: raids without SOL stakes, full wallet address with Copy address and View on explorer). Unity 6000.3.11f1, Solana Unity SDK pinned at `bd0d30f6` (1.3.0). Both are
release (non-development) builds that talk to the live API `https://api-production-dad8.up.railway.app` (devnet).

| Build | Path | Size | Build time | Result |
|---|---|---|---|---|
| Desktop WebGL (playtest profile) | `Pirate Game Unity/Builds/WebGL-Desktop-Hackathon/RoguePirates/` | 120.2 MB download (data 111.0 MB, wasm 14.8 MB, Brotli) | 34 min (final build, done 15:51; 37 min at 13:39, the first full build 45 min) | Succeeded (report counts 1 error log, as in the 13:39 build; the build itself succeeded) |
| Android APK (Solana Mobile / any arm64 Android 7.1+) | `Pirate Game Unity/Builds/Android-Hackathon/RoguePirates-hackathon.apk` | 148.6 MB | 12 min (final build, done 16:05; 8 min at 13:39, the first build 20 min) | Succeeded, 0 errors |

Build reports: `Builds/V5WebGL-Desktop-Hackathon-report.txt`, `Builds/Android-Hackathon/RoguePirates-hackathon-report.txt`.

## How they were made

Both builds ran inside the open Editor through `unity command eval_file`, using the existing build profiles:

- WebGL: `V5WebGLProfiles.BuildDesktopVariant("Hackathon", null)`: the desktop profile (1 GB heap ceiling, DXT
  textures, 1024 texture cap, Brotli with the decompression fallback, data caching, release logs without stack traces);
  the project's WebGL settings are restored afterwards. Session downloads stay as in the normal playtest build (the
  public build comes later).
- Android: `V5AndroidProfiles.Build("RoguePirates-hackathon", ...)` with the package id set to
  `fun.roguepirates.hackathon` for this build only (restored to `com.x2c.roguepirates` afterwards): IL2CPP, ARM64 only,
  ASTC, min API 25, target API 36, app name "Rogue Pirates", signed with the Unity debug key. The Editor's build target is
  switched back to WebGL afterwards.

## Settings changed (kept: the Android build needs them)

- `ProjectSettings/ProjectSettings.asset`: `useCustomMainGradleTemplate: 1` (Player > Android > Publishing Settings >
  Custom Main Gradle Template).
- New `Assets/Plugins/Android/mainTemplate.gradle` (+ `.meta`, and `Assets/Plugins.meta`): Unity's default main
  template, patched by the SDK (`Solana/Fix Android Dependencies`, also run by the SDK before every Android build) with
  androidx.browser 1.8.0, androidx.core 1.13.1, versionedparcelable 1.2.1, guava 33.5.0-android and the
  listenablefuture/kotlin-stdlib exclusions, so Gradle has no duplicate-class errors.

Nothing else was kept: the font, TextMesh Pro, URP asset and graphics/shader-graph settings churn the builds wrote was
reverted.

No WebGL template change is needed: the SDK's `SolanaWalletAdapterWebGL.jslib` is compiled into the build and loads
`@magicblock-labs/unity-wallet-adapter@1.2.1` from jsdelivr when the player presses Connect Wallet, so the Default
template works. (The SDK's own `xNFT` template is only for Backpack xNFTs and is not used.)

## Run the WebGL build locally

```powershell
python plan/v5/tools/serve-webgl.py "Pirate Game Unity/Builds/WebGL-Desktop-Hackathon/RoguePirates" 8090
```

Open http://localhost:8090/ in desktop Chrome with Phantom or Solflare installed and set to **Devnet**. The API allows
`http://localhost:*` (CORS), so test on localhost; another machine on the LAN would load from
`http://<this PC's LAN IP>:8090/`, an origin the API's CORS may not allow.

Only one server may own the port: on 12 Oct an old server from 11 Oct (serving `WebGL-Desktop-Net`) was still on 8090
and answered instead, so the page showed the old build. If the menu has a "Server voyage (test)" button and no
Connect Wallet button, that is the wrong build: stop the other `serve-webgl.py` and clear the site data (DevTools >
Application > Clear site data), since Unity caches the data file in IndexedDB.

Checked on 12 Oct (built-in Chrome browser, localhost:8090, cache cleared): loads to the main menu; the wallet panel opens
on the first launch (web3 mode) with Connect Wallet; the menu shows DAILY RACE, ISLANDS, MARKET, PVP (1V1), LEADERBOARD,
LEDGER; no errors in the console (only the usual unsupported-shader notices and WebGL "uniform buffer too small"
warnings). Connect Wallet loaded `unity-wallet-adapter@1.2.1` from jsdelivr (`window.walletAdapterLib` present) and
opened the SDK's chooser ("Connect a wallet on Solana to continue": Phantom, Solflare). Approving in Phantom was not
automated.

Final build re-checked on 12 Oct 15:55 (built-in browser, localhost:8090, the new data file fetched fresh with 200): loads to the
main menu with the wallet panel open; after Close the menu shows DAILY RACE, ISLANDS, MARKET, PVP (1V1), LEADERBOARD,
LEDGER; no console errors (only the WebGL "uniform buffer too small" warnings).

## Install the APK

With a phone connected over USB (developer options and USB debugging on):

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe" install -r "Pirate Game Unity\Builds\Android-Hackathon\RoguePirates-hackathon.apk"
```

Or side-load: copy the APK to the phone (USB, Drive, Telegram), open it, allow "install unknown apps" for that app.
The package is `fun.roguepirates.hackathon`, so it installs next to the playtest app (`com.x2c.roguepirates`).

On the phone, install a Mobile Wallet Adapter wallet (Phantom or Solflare; Seeker also ships its own Seed Vault
wallet) and set it to devnet. Connect Wallet opens the Android wallet chooser; the wallet returns to the game after approving.

Final APK (16:05): not installed yet, the Seeker was not connected over USB during the rebuild (`adb devices` empty at 15:20,
15:27 and 16:05). Install it with the command above (`install -r` keeps the app data).

Installed on the owner's Seeker over USB on 12 Oct 13:39 (the previous APK) (`adb install -r`, app data kept: still signed in): the menu loads at about 58 fps (2670x1200), and the wallet panel shows the full address; Copy address copies it (screens `plan/hackathon/screens/seeker-menu-nostake.png`, `seeker-wallet-panel.png`, `seeker-wallet-copied.png`).

APK contents checked: `fun.roguepirates.hackathon`, label "Rogue Pirates", minSdk 25, targetSdk 36, `arm64-v8a` only;
the dex has the SDK's `MwaChooserHelper` (the MWA wallet chooser), androidx.browser and guava; the data has the
`RoguePirates.Hackathon` assembly and its startup hooks.

## Known limits

- Devnet only. Phantom/Solflare must be switched to devnet, and the wallet needs devnet SOL for 1v1 wagers and market buys (raids cost only RUM).
- Release builds: the in-game test wallet is off (it is for the Editor and development builds), so a real wallet is
  needed on both platforms.
- The APK is signed with the Unity debug key: fine for side-loading and the demo, not for the Play Store or the
  Solana dApp Store (a release keystore is the owner's to make).
- The WebGL wallet adapter script comes from jsdelivr at runtime, so the WebGL build needs internet access for wallets.
- First WebGL load downloads about 120 MB (cached afterwards). Brotli is decoded by the browser only over https; over
  plain http the loader decompresses it in JavaScript (slower first start, the console says so).
- The Android build is ARM64 only (every Seeker, Saga and current phone; no 32-bit or x86 emulators).
- The Editor showed an "Opening file failed" dialog during the first WebGL build because files under `Assets/Temp/`
  (screenshots) were deleted while the Editor still knew them; the build waited until it was cancelled. Delete such
  files from inside Unity, or refresh the Editor before building.
