# Reference devices and frame-rate target — research, 30 Sep 2026

Collected by a research agent. [opened] = page read; [snippet] = search text only (GSMArena, Notebookcheck and Canalys
refused the connection). Budget numbers are engineering estimates to be confirmed on a real phone.

## Market

- 2025 global top 10: 7 iPhones and 3 Samsungs; the Galaxy A16 5G was the best-selling Android phone
  ([MacRumors](https://www.macrumors.com/2026/01/29/iphone-16-worlds-best-selling-phone-2025/), opened).
- Latin America 2025: #1 Galaxy A06, #2 Galaxy A16 4G, #3 Moto G15, #4 Redmi 14C; most under $200
  ([Communications Today](https://www.communicationstoday.co.in/samsung-tops-latin-america-smartphone-bestseller-list-in-2025/), opened).
- iOS share (StatCounter, Aug 2026): worldwide 32%, US 61%, Brazil 25%, Indonesia 15%, India 7%. iOS 26 runs on iPhone
  11 / SE 2 and newer; the iPhone 13 is the most-used model (TelemetryDeck, opened).
- Alex Russell's global P75 phone (a quarter of users have worse) is the **Galaxy A24 4G / Helio G99**; "anything based
  on the Helio G99" qualifies ([Performance Inequality Gap 2026](https://infrequently.org/2025/11/performance-inequality-gap-2026/), opened).
- Average RAM hit 8.4 GB in 2025 (Counterpoint headline); an estimated 25–35% of active Android phones still have 4 GB or
  less, more in India and LatAm (estimate).

| Phones | Chip | GPU | 3DMark Wild Life (UL median) |
|---|---|---|---|
| Galaxy A06, Redmi 13C | Helio G85 | Mali-G52 MC2 | 753 |
| Galaxy A15, A16 4G, A24 | Helio G99 | Mali-G57 MC2 | 1,226 |
| Galaxy A16 5G | Dimensity 6300 | Mali-G57 MC2 | 1,350 |
| iPhone 13 | A15 | Apple | 8,581 |

## WebGL constraints

- Unity 6 supports mobile browsers with lower memory limits; C# runs on one thread; keep draw calls low
  ([Unity WebGL performance](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-performance.html), opened).
- Chrome on 4–6 GB Android phones is 32-bit; Unity heap growth fails around 219–256 MB; Unity staff advise treating
  mobile as capped at 256 MB ([Unity discussions](https://discussions.unity.com/t/android-chromium-unable-to-grow-allocated-memory-above-256mb-confirmed/818820), opened, 2020–21; 64-bit Chrome only on 8 GB+ phones, snippet 2021).
- iOS Safari: since iOS 18.2 about 1.6 GB on an SE 2, more on newer iPhones; conservative guidance 256–384 MB.
- Google Play's slow-session bar: a session is slow if over 25% of frames miss 20 fps; games target 30 or 60 fps
  ([Android vitals](https://developer.android.com/games/optimize/vitals/slow-session), opened).
- No credible published Unity WebGL fps figures exist for G85/G99 phones: this must be measured on a real device.

## Recommendation

- **Reference low-end Android: Samsung Galaxy A15 or A16 4G** (Helio G99, Mali-G57 MC2, 4 GB, 32-bit Chrome): about
  70–80% of players have this or better. Target: **a locked, steady 30 fps** (aim ~25 ms average frame time; at least
  95% of frames under 34 ms over a 15-minute run).
- **Floor device: Galaxy A06 or Redmi 14C** (Helio G85/G81, Mali-G52 MC2, 4 GB): must stay playable at 30 fps on the Low
  preset, never below 20 fps.
- **Reference iPhone: iPhone 11 / SE 2nd gen (A13)**: memory, not the GPU, is the limit.
- **iPhone 11+, mid-range Android and desktop: 60 fps**, quality tier picked from measured frame time.
- Budget estimates for the reference phone: 100–150 draw calls; 100–150k visible triangles (enemies 300–800 triangles);
  **60–80 active enemies** and 150–200 pooled projectiles; simulation ≤ 8–10 ms per frame; ≤ 1,000–1,500 live
  particles; render scale 0.6–0.75, no post-processing beyond colour grading; **Unity heap ~256 MB (max 512 MB), whole
  tab under ~400–500 MB**; compressed textures ≤ 1K, under ~100 MB; 20–30 MB first download; WebGL2, not WebGPU.
