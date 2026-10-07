// Adds the calibration evidence to results.json (for the report): each recorded session (plan/playtest/sessions) beside
// the model before I8z.7 (calibration-before.json: the old sim.mjs and data.json) and the model now (sim.mjs on the
// Unity export, fitted with fit_sessions.mjs), per minute and for the fights.
//
//   node finalize.mjs   (after batch.mjs)
import fs from 'fs';
import { simulateRun } from './sim.mjs';
import { loadSessions, gameSide, modelSide, kindOfNew } from './fit_sessions.mjs';
const out = JSON.parse(fs.readFileSync(new URL('./results.json', import.meta.url)));
const data = JSON.parse(fs.readFileSync(new URL('./data.json', import.meta.url)));
const before = JSON.parse(fs.readFileSync(new URL('./calibration-before.json', import.meta.url))).sessions;
const fitted = fs.existsSync(new URL('./calibration-sessions.json', import.meta.url)) ? JSON.parse(fs.readFileSync(new URL('./calibration-sessions.json', import.meta.url))) : null;
out.calibration = { fitted: fitted && fitted.fitted, loss: fitted && fitted.loss, sessions: [] };
for (const { file, s } of loadSessions()) {
  const g = gameSide(s), now = modelSide(simulateRun, data, g, {}, 200, kindOfNew(data)), was = before[file];
  out.calibration.sessions.push({
    file, end: g.end, sunk: g.sunk, configHash: String(s.header.configHash || '').slice(0, 10),
    minutes: g.minutes.map((a, i) => ({ m: a.m, game: { level: a.level, kills: a.kills, hull: a.hull, dmg: Math.round(a.dmg), dps: Math.round(a.dps) },
      before: was && was.minutes[i] ? { level: was.minutes[i].level, kills: was.minutes[i].kills, hull: was.minutes[i].hull, dmg: was.minutes[i].dmg, afloat: was.minutes[i].afloat } : null,
      now: { level: now.minutes[i].level, kills: now.minutes[i].kills, hull: now.minutes[i].hull, dmg: now.minutes[i].dmg, dps: now.minutes[i].dps, afloat: now.minutes[i].afloat, levelP: now.minutes[i].levelP, killsP: now.minutes[i].killsP } })),
    fights: { game: g.fights, before: was ? was.fights : null, now: now.fights },
    sunkBy: { before: was ? was.sunkBy : null, now: now.sunkBy },
  });
}
fs.writeFileSync(new URL('./results.json', import.meta.url), JSON.stringify(out));
for (const c of out.calibration.sessions) {
  const last = c.minutes[c.minutes.length - 1];
  console.log(`${c.file}: at ${last.m}:00 level ${last.game.level} | before ${last.before?.level ?? '-'} | now ${last.now.level}; kills ${last.game.kills} | ${last.before?.kills ?? '-'} | ${last.now.kills}; ` +
    `hull ${Math.round(last.game.hull * 100)}% | ${last.before?.hull != null ? Math.round(last.before.hull * 100) + '%' : '-'} | ${last.now.hull != null ? Math.round(last.now.hull * 100) + '%' : '-'}; sunk by the end ${c.sunk ? 'yes' : 'no'} | ${Math.round((c.sunkBy.before ?? 0) * 100)}% | ${Math.round(c.sunkBy.now * 100)}%`);
}
