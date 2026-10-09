# Owner re-test, 10 Oct (Railway, image e1f56626d9b5, WebGL net client)

- Stutter and lag gone for the ship and the enemies; enemies stopped jumping.
- Switching tabs while level-up cards are up: cards vanish and the ship stays stuck afterwards (still broken).
- After picking a card, enemies take a small noticeable pause before resuming.
- Owner decision: move the leash target just outside the camera view (about 31 m ahead instead of 26 m), in normal play and online.

Key console lines (owner's Chrome, 1920x1080, fps about 40, 98% of frames over 20 ms):
- sailing=50s: cardSets=4 picks=4, then:
  [V5 Net] resync (behind the server's ack): ack 1745, margin -1, tick 1744 -> 1753 (lead 8)
  [V5 Net] resync (behind the server's ack): ack 1755, margin -11, tick 1753 -> 1763 (lead 8)
  [V5 Net] resync (behind the server's ack): ack 1765, margin -21, tick 1763 -> 1773 (lead 8)
  [V5 Net] resync (behind the server's ack): ack 1775, margin -31, tick 1773 -> 1783 (lead 8)
- sailing=60s: cardSets=5 picks=5 aways=1, then the tab was hidden (fps=1.0 lines) for about 50 s.
- sailing=112s (back): predErrMax 0.73, corrMax 0.93, shipJitterP95 1.6, then:
  [V5 Net] resync (behind the server's ack): ack 2247, margin -18, tick 2245 -> 2256 (lead 9)
  [V5 Net] resync (behind the server's ack): ack 2257, margin -28, tick 2256 -> 2266 (lead 9)
  stats sailing=122s: shipJitterP95=2.50, kills stay 52 (ship stuck).
- Early: [V5 Net] resync (inputs late): ack 205, margin -3, tick 214 -> 220 (lead 15); renderDelayMs 350 at start and after return (else about 145).
