# P11: a livelier raid economy (proposal, 6 Oct)

You asked to revisit the treasury rules (Q23b) because you want an economy where things happen. Today's rule protects
so much that raids barely matter. Example: with 1,000 RUM, a raid takes 40 and the worst day costs 100.

## What makes it "happen"

1. **More RUM at risk**, so raids are worth doing and defending against.
2. **Shorter, earned protection**: shields come from being raided, not by default, and attacking ends your shield (as in
   Clash of Clans).
3. **Reasons to spend instead of hoard**: things to buy (upgrades, defenses, cosmetics, repairs), so RUM keeps moving.
4. **Revenge and rivals**: the islands that hit you are targets you want to hit back.
5. **Hard floors** so nobody is wiped out or quits: a daily loss cap and protection for new players.

## Three options (numbers with 1,000 RUM on the island)

| | A. Lively (suggested) | B. Balanced | C. Keep Q23b |
|---|---|---|---|
| Vault (never raidable) | Holds up to a cap that grows with island level (e.g. 300 at the start); the rest is exposed | 60% | 80% |
| Exposed treasury | 700 | 400 | 200 |
| One successful raid takes | 25% of the treasury = **175** | 25% = **100** | 20% = **40** |
| A partial raid (half the defenses beaten) | Half of that | Half of that | n/a |
| Shield after being raided | 4 h if you lost ≥15% of your RUM, 2 h if less | 6 h | 8 h after any raid |
| Attacking someone | Ends your own shield | Ends your own shield | No effect |
| Most you can lose in a day | 25% = **250** | 15% = **150** | 10% = **100** |
| New-player protection | 48 h, ends early if you raid | 72 h | 72 h |
| Revenge | A free revenge raid on your attacker within 24 h, ignoring their shield once | Same | None |
| Raid entry fee (a sink) | A small fee that scales with the target's treasury | Same | None |

**A** makes RUM flow: big islands are juicy targets, hoarding past the vault cap is risky, and spending (upgrades that
raise the vault cap and defenses) is the way to protect yourself. **B** is a middle ground. **C** keeps today's
decision.

## Things to keep whichever you choose

- **Floors**: the daily cap and new-player protection stay, and the AI-captain raids (6 a day, Q9) count toward the
  daily cap, so offline players can't be farmed.
- **Anti-collusion is off for now (Q27)**: in A, two accounts could pass RUM through raids. The daily cap, the entry fee
  and forfeits paying nothing (Q34) limit it; the M28 audit switches the collusion rules on before RUM has value.
- **Testing**: all numbers are config. The balance simulator gets an economy mode (RUM in from voyages, out through raids
  and spending) to check that a typical player's RUM grows over a week rather than shrinking.

## What I need from you

Pick A, B or C (or change any number). The rules are built in M14 and M15, so nothing changes in the game today.
