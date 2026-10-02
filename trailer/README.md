# Rogue Pirates trailer

Remotion project for the trailer's motion graphics and, later, the final edit. Storyboard and owner decisions:
https://claude.ai/artifact/3QGxkXaGVX9nsx2ij8LwDy

```bash
npm install
npm run studio                                    # preview every composition
npx remotion render Captains out/captains.mp4     # render one
```

Compositions (1920x1080, 60 fps):

| Id | What | Length |
|---|---|---|
| `Captains` | Six Captain cards in the website's art and type, then "Pick your Captain" | 6.2 s |
| `Ledger` | Penny reads the run's ledger on the scroll; the SUNK stamp | 3.3 s |
| `EndCard` | Logo, "The sea keeps receipts.", roguepirates.fun, Chippy | 5 s |

Assets in `public/`:

- `web/`, `fonts/`: copied from the Rogue-Pirates-Website repo (Captain art, emblems, frames, tentacles; Cinzel and Alegreya under the OFL).
- `art/`: the owner's trailer assets (2 Oct 2026 zip), converted to WebP. `chippy.webp` was cut out of its white background here.

Licence: Remotion is free for individuals and companies of up to 3 employees; larger companies need a Company License.
