// V5 M9a (C9a.1, 8 Oct 2026): the game config each recorded session was played on, so the model replays every voyage
// on its own config (not on today's). The five voyages span four builds (their header configHash differs):
//
//   session                         configHash  played on (commit)                 snapshot source
//   20261006-152859 (S1, 4:25)      1cc1b64e    playtest-r10 (2d55fed4)            afad2196 export + patches (below)
//   20261007-122453 (S2, 14:01)     1ca9fe47    45a37c07 (before the Logbook fix)  afad2196 export + old offer weights
//   20261007-141608 (S3, 11:33)     5ce95e3f    63a2708a (Logbook fix)             afad2196 export (f0b57ee7; the assets differ
//                                                                                   from 63a2708a only in look fields)
//   20261007-204854 (S4, 13:15)     2ebb45e6    3bd86f6d (D20: XP 0.8, equal DPS)  7bfb9403 export (configHash 2ebb45e6, exact)
//   20261008-001147 (S5, 16:38)     daa47525    23227d5f (XP 0.4, D21, Q20)        7f84c1da export (configHash daa47525, exact)
//
// S5 is today's balance: data.json (the Editor export now) differs from its snapshot only in the Unlocks asset (the
// Ramming Prow save alias), which the model does not read. The commits come from the session start times and the
// configHash: the two exact matches are Unity exports committed with the build; S1-S3 were played before any export
// with their hash, so their snapshot is the nearest export (afad2196, same balance as 63a2708a) with the asset
// differences applied by hand (git diff of the Data assets between the commits):
//   S2: OfferWeights row "6 weapons" 55/0/45/0 (the Logbook fix 90f59681 made it 45/0/30/25).
//   S1: as S2, plus island hits 10 hull (RunConfig.islandImpactDamage; 5 from b9809326), the arena fight floor 3 s of
//       DPS (SaltwindDirector.arenaFocusMinSeconds; 2 from 3e618074), and not everything unlocked (be57c7a5 unlocked it).
// Pre-D20 sessions (S1-S3) paid XP the old way (each burst piece at least 1 XP, more pieces at a higher effects density):
// their snapshot carries `model: { legacyXpBurst: true, vfxDensity: 1 }`, the High tier. The tier is the device's
// (AutoQuality: High under 20 ms a frame, the usual desktop WebGL case), not a fitted constant: of the three tiers
// (0.45, 0.75, 1), only High reproduces the sessions' early levels (S3 at 2:00 / 4:00: game 18 / 29; High 20 / 27,
// Mid 17 / 23, Low 13 / 18, with the fitted constants).
//
//   node session_data.mjs     (rebuilds session-data/*.json from the committed exports; needs git)
import fs from 'fs';
import path from 'path';
import { execFileSync } from 'child_process';
import { fileURLToPath } from 'url';

const here = path.dirname(fileURLToPath(import.meta.url));
export const SESSION_DATA_DIR = path.join(here, 'session-data');

/** The recorded voyages the model is fitted on, oldest first. `today`: played on today's balance. */
export const SESSIONS = [
  { file: 'session-20261006-152859-rook.json', id: 'S1', configHash: '1cc1b64e', commit: '2d55fed4', today: false,
    note: 'playtest-r10: island hits 10 hull, XP 0.5 (old payout), pre-D20 weapons' },
  { file: 'session-20261007-122453-rook.json', id: 'S2', configHash: '1ca9fe47', commit: '45a37c07', today: false,
    note: 'island hits 5 hull, XP 0.5 (old payout), pre-D20 weapons, old offer weights' },
  { file: 'session-20261007-141608-rook.json', id: 'S3', configHash: '5ce95e3f', commit: '63a2708a', today: false,
    note: 'as S2 with the Logbook offer fix' },
  { file: 'session-20261007-204854-rook.json', id: 'S4', configHash: '2ebb45e6', commit: '3bd86f6d', today: false,
    note: 'D20: XP 0.8 (exact payout, new table), equal weapon DPS; Ramming Prow, no Q20 milestones' },
  { file: 'session-20261008-001147-rook.json', id: 'S5', configHash: 'daa47525', commit: '23227d5f', today: true,
    note: "today's balance: XP 0.4, Squid Ink Pots (D21), Q20 milestones" },
];

export const snapshotPath = s => path.join(SESSION_DATA_DIR, s.file.replace(/^session-/, 'data-'));

/** The session's config (data snapshot) and the model settings that belong to its build (the XP payout). */
export function loadSessionData(s) {
  const d = JSON.parse(fs.readFileSync(snapshotPath(s), 'utf8'));
  return { data: d, model: (d.meta && d.meta.session && d.meta.session.model) || {} };
}

function build() {
  const exportAt = c => JSON.parse(execFileSync('git', ['show', `${c}:plan/playtest/montecarlo/data.json`], { cwd: here, encoding: 'utf8', maxBuffer: 1 << 26 }));
  const legacy = { legacyXpBurst: true, vfxDensity: 1 };
  const oldOfferRow = d => { const row = d.offerWeights.find(r => r.maxWeapons === 6); Object.assign(row, { weaponUpgrade: 55, newWeapon: 0, logbookUpgrade: 45, newLogbook: 0 }); };
  const recipes = {
    S1: { from: 'afad2196', model: legacy, patch: d => { oldOfferRow(d); d.run.islandImpactDamage = 10; d.director.ttk.arena = 3; d.unlocks.unlockEverything = false; },
      patches: ['offerWeights[6 weapons] 55/0/45/0', 'run.islandImpactDamage 10', 'director.ttk.arena 3', 'unlocks.unlockEverything false'] },
    S2: { from: 'afad2196', model: legacy, patch: oldOfferRow, patches: ['offerWeights[6 weapons] 55/0/45/0'] },
    S3: { from: 'afad2196', model: legacy, patch: () => {}, patches: [] },
    S4: { from: '7bfb9403', model: {}, patch: () => {}, patches: [] },
    S5: { from: '7f84c1da', model: {}, patch: () => {}, patches: [] },
  };
  fs.mkdirSync(SESSION_DATA_DIR, { recursive: true });
  for (const s of SESSIONS) {
    const r = recipes[s.id], d = exportAt(r.from);
    r.patch(d);
    d.meta = { ...(d.meta || {}), session: { id: s.id, file: s.file, sessionConfigHash: s.configHash, playedOn: s.commit, exportFrom: r.from, exportConfigHash: d.meta?.configHash?.slice(0, 8),
      patches: r.patches, model: r.model, note: s.note } };
    fs.writeFileSync(snapshotPath(s), JSON.stringify(d, null, 1));
    console.log(`${s.id} ${path.basename(snapshotPath(s))}: export ${r.from} (${d.meta.configHash.slice(0, 8)})${r.patches.length ? ' + ' + r.patches.join(', ') : ''}${r.model.legacyXpBurst ? '; old XP payout at density ' + r.model.vfxDensity : ''}`);
  }
  // Today's check: data.json against the snapshot of the session on today's balance.
  const now = JSON.parse(fs.readFileSync(path.join(here, 'data.json'), 'utf8')), today = JSON.parse(fs.readFileSync(snapshotPath(SESSIONS.find(x => x.today)), 'utf8'));
  delete now.meta; delete today.meta;
  console.log(JSON.stringify(now) === JSON.stringify(today) ? "data.json equals the S5 snapshot (today's balance)" : 'WARNING: data.json differs from the S5 snapshot: the game changed since the last recorded session');
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) build();
