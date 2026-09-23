# Saltwind verification index (Codex)

Status: **independent follow-up found open correctness issues; Unity rerun blocked by main-thread timeouts**. **C13 external playtest and desktop perf remain pending**. See [CODEX_INDEPENDENT_FOLLOWUP_2026-09-23.md](CODEX_INDEPENDENT_FOLLOWUP_2026-09-23.md) for severity-ordered findings and current evidence. The resolution log and original review addendum retain earlier claims/history; the checkpoint table below is Cursor's prior status, not a new independent pass.

## Reproduce tests

```powershell
cd "C:\Kshitij\Work\X2C\web3\GitHub\Pirate-Game"
$env:UNITY_PROJECT_PATH = Join-Path (Get-Location) 'Pirate Game Unity'
unity close "Pirate Game Unity"   # if interactive Editor open
unity test 'Pirate Game Unity' --mode EditMode --output 'plan/saltwind-verification/editmode-results.xml'
unity test 'Pirate Game Unity' --mode PlayMode --output 'plan/saltwind-verification/playmode-results.xml'
```

Connected Editor: `unity command run_tests --mode EditMode --filter Saltwind`.

Regression log: [step14-regression-evidence.md](step14-regression-evidence.md).

## Deliverables map

| Artifact | Path | Status |
| --- | --- | --- |
| Implementation log | [IMPLEMENTATION_LOG.md](IMPLEMENTATION_LOG.md) | Updated |
| Resolution log | [CODEX_RESOLUTION_LOG.md](CODEX_RESOLUTION_LOG.md) | R1–R10 + matrix |
| Lab target bands | [SALTWIND_LAB_TARGET_BANDS.md](SALTWIND_LAB_TARGET_BANDS.md) | Documented |
| Protected settings | [PROTECTED_SETTINGS.json](PROTECTED_SETTINGS.json) | Baseline |
| Balance decisions | [BALANCE_DECISIONS.md](BALANCE_DECISIONS.md) | v1 + v2 |
| Profile export | [profile-export/](profile-export/) | Hash below |
| Balance lab | [balance-lab/](balance-lab/) | Re-run **1800** after calibration |
| C13 external | [C13_EXTERNAL_PLAYTEST.md](C13_EXTERNAL_PLAYTEST.md) | **Pending** |
| Perf trace | [PERF_TRACE_PENDING.md](PERF_TRACE_PENDING.md) | **Pending** |

## Git / profile pins

| Pin | Value |
| --- | --- |
| Baseline commit (Step 00) | `701bfa7d081b646631b6db15b890d51e46d5ad40` |
| Reviewed implementation (Codex audit) | `6a21ba63` |
| **Current delivery / resolution commit** | **`c69d5668af04297021e7dced23a49ac2435d9747`** (includes post-review fixes in `1893e86a` and `c69d5668`; independently inspected) |
| Profile content hash | **`e4ffa52b4475d137e89c960eebf2de9ee42a3280c9f859cb5ca1761c1baa7eb7`** |
| Profile balance version | **2** |

## Checkpoint summary (C02–C14)

**Prior implementation claims below:** independent review reopens encounter correctness (C03), composition/pressure (C04), reward identity (C09), legacy-owned unlock purchases (C11), storm/navigation evidence (C05/C06), test/save isolation and reruns (C10/C14), and lab/live disclosure (C12). R1/R2/R6 Unity gates are not independently passed in this follow-up. Separate existing-assembly .NET simulation results: [independent-v6-net/](independent-v6-net/); these do not replace Unity, device or human checks.

| Gate | Result | Limitations |
| --- | --- | --- |
| **C02** spawn schedule | **Passed with limitations** | EditMode exclusive bands + 540s rate gate; live 9:00 measured spawn count still optional |
| **C03** encounters | **Passed with limitations** | PlayMode partial-cache + boss tracker tests; not every formation edge in scene |
| **C04** composition / pressure | **Passed with limitations** | Profile-driven director; pause/resume pulse PlayMode partial |
| **C05** finale / storm math | **Passed with limitations** | PlayMode storm sail at min safe scale; not full human OT session |
| **C06** POI layout / routes | **Passed with limitations** | 100-seed layout gate + route validation; not every adversarial island in scene |
| **C07** progression | **Passed with limitations** | Export progression + lab fill-state; not full LevelOffer UI |
| **C08** economy | **Passed with limitations** | Profile chest curve + lab checkpoints |
| **C09** settlement / Marks | **Passed with limitations** | Pure record reads; full t=600 RunManager tick optional |
| **C10** reset / pause | **Passed with limitations** | PlayMode pause + retry generation |
| **C11** results / unlocks | **Passed with limitations** | Effective ownership gates |
| **C12** balance lab | **Passed with limitations** | 1800-run matrix + bands; not live telemetry parity |
| **C13** human / device | **Pending external** | No newcomer panel / mobile / perf sign-off |
| **C14** delivery package | **Passed with limitations** | Index + exports; C13 outstanding |

## Codex prompt

`plan/SALTWIND_CURSOR_IMPLEMENTATION_HANDOFF.md` §7.
