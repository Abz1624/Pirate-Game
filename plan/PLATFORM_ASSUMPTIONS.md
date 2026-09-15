# Platform assumptions (M0-007)

## Target OS / browsers

- Android 10+ (ARM64)
- iOS 15+ (ARM64)
- Desktop: Windows 10+, macOS 12+
- Mobile browsers: Safari iOS, Chrome Android
- Desktop browsers: Chrome, Edge, Firefox (WebGL2)

## Reference devices (provisional)

| Tier | Example device | Role |
|---|---|---|
| Minimum | Mid-range Android 2020 | 30 FPS gate |
| Recommended | iPhone 13 / Pixel 6 | 60 FPS gate |
| Browser min | iPhone SE 2020 Safari | Mobile web gate |

Owner confirmation required before launch certification.

## Build signing

- Android: keystore TBD (owner)
- iOS: Apple Developer account TBD (owner)
- Steam: App ID TBD

## Frame rate policy

Authoritative simulation at 30 Hz. Rendering may vary; outcomes must match across 30/60/120 render rates.
