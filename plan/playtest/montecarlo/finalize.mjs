// Adds the calibration evidence to results.json (for the report): each recorded voyage (plan/playtest/sessions) beside
// the model before C9a.1 (calibration-before.json: the 7 Oct model on today's data.json) and the model now (sim.mjs
// fitted with fit_sessions.mjs, each voyage replayed on the config it was played on: session_data.mjs), per minute with
// the model's p10-p90 band, and the survival time.
//
//   node finalize.mjs   (after batch.mjs)
import fs from 'fs';
import { loadSessions, gameSide, modelSide, runSession, kindOfNew } from './fit_sessions.mjs';
const out = JSON.parse(fs.readFileSync(new URL('./results.json', import.meta.url)));
const before = JSON.parse(fs.readFileSync(new URL('./calibration-before.json', import.meta.url))).sessions;
const fitted = fs.existsSync(new URL('./calibration-sessions.json', import.meta.url)) ? JSON.parse(fs.readFileSync(new URL('./calibration-sessions.json', import.meta.url))) : null;
out.calibration = { fitted: fitted && fitted.fitted, loss: fitted && fitted.loss, model: fitted && fitted.model, sessions: [] };
const inside = (v, b) => b != null && v >= b[0] && v <= b[2];
for (const x of loadSessions()) {
  const g = gameSide(x.s), now = modelSide(runSession(x.data, g, x.model, 200), g, kindOfNew(x.data)), was = before[x.file];
  const minutes = g.minutes.map((a, i) => ({ m: a.m, game: { level: a.level, kills: a.kills, hull: a.hull, dmg: Math.round(a.dmg), elites: a.elites },
    before: was && was.minutes[i] ? { level: was.minutes[i].level, kills: was.minutes[i].kills, hull: was.minutes[i].hull, dmg: was.minutes[i].dmg, afloat: was.minutes[i].afloat } : null,
    now: { level: now.minutes[i].level, kills: now.minutes[i].kills, hull: now.minutes[i].hull, dmg: now.minutes[i].dmg, elites: now.minutes[i].elites, afloat: now.minutes[i].afloat } }));
  const share = k => minutes.filter(r => inside(r.game[k], r.now[k])).length / Math.max(1, minutes.length);
  out.calibration.sessions.push({ id: x.meta.id, file: x.file, end: g.end, sunk: g.sunk, configHash: x.meta.configHash, today: x.meta.today, note: x.meta.note, minutes,
    survival: { before: was ? { end: was.end, sunkBy: was.sunkBy, endRank: was.endRank } : null, now: { end: now.end, sunkBy: now.sunkBy, endRank: now.endRank } },
    inside: { level: share('level'), kills: share('kills'), dmg: share('dmg'), hull: share('hull') },
    by: { game: Object.fromEntries(Object.entries(g.by).map(([k, v]) => [k, Math.round(v.damage)])), model: Object.fromEntries(Object.entries(now.by).map(([k, v]) => [k, Math.round(v)])) } });
}
fs.writeFileSync(new URL('./results.json', import.meta.url), JSON.stringify(out));
const clock = s => s == null ? '-' : !Number.isFinite(s) ? '>20:00' : `${Math.floor(s / 60)}:${String(Math.round(s % 60)).padStart(2, '0')}`;
for (const c of out.calibration.sessions) {
  const s = c.survival.now;
  console.log(`${c.id} ${c.sunk ? 'sunk' : 'ended'} ${clock(c.end)}: model end p10 ${clock(s.end[0])} p50 ${clock(s.end[1])} p90 ${clock(s.end[2])}, sunk by then ${Math.round(s.sunkBy * 100)}%; ` +
    `minutes inside p10-p90: level ${Math.round(c.inside.level * 100)}%, kills ${Math.round(c.inside.kills * 100)}%, damage ${Math.round(c.inside.dmg * 100)}%, hull ${Math.round(c.inside.hull * 100)}%`);
}
