// Rogue Pirates: numbers-only Monte Carlo of a Saltwind voyage (playtest round 5, 3 Oct 2026).
// Written from scratch (not the V5 simulator). One second per step; enemies are counted in groups, not simulated as
// bodies. The same file runs in Node (batch.mjs) and in the report page. Every assumption is a named constant below.

// ---- Model assumptions (not in the game data) --------------------------------------------------------------------
export const MODEL = {
  // Per weapon at level 1: single-target hits per cycle (x base damage), cycle seconds, one hit's share of base damage,
  // and how many enemies a firing reaches in a dense crowd (pierce, fan, beam, area, orbit). From the code review of
  // every behaviour (2 Oct).
  weapons: {
    deck_cannons: [1, 1.05, 1, 1], swivel_guns: [1.1, 0.78, 0.55, 1], long_nine: [1, 2.4, 1, 3], harpoon_gun: [1, 1.2, 1, 1],
    chainshot: [1, 1.5, 1, 2], broadside_battery: [1.8, 1.5, 0.6, 1.8], flying_cutlasses: [0.875, 1, 0.35, 3],
    anchor_flail: [1, 3.6, 1, 3], lighthouse_lantern: [1, 6, 1, 4], flamethrower: [9.4, 3.3, 0.28, 3],
    powder_kegs: [0.72, 2.2, 0.72, 3], lightning_mast: [1, 1.8, 1, 3], cursed_doubloons: [1, 2, 1, 3], ramming_prow: [0.3, 0.3, 1, 3],
    siren_bell: [1, 3, 1, 5], mortar: [1, 1.8, 1, 3], maelstrom_charge: [3.7, 5, 0.2, 4], monkey_gunners: [13, 6, 1, 1], parrot_squadron: [22, 4, 1, 1],
  },
  crowdForFullSplash: 25,      // area weapons reach their full target count at this many enemies alive
  engagement: 0.9,             // share of the enemies the player fights (the rest wander off)
  lootShare: 1,                // share of XP, gold and chests collected (the owner collected ~all; the report profile uses 0.8)
  evolutionMultiplier: 1.6,    // an evolved weapon's damage, x
  chestEverySeconds: 80,       // a chest opened this often with 80% loot (gold flow)
  rarityWeights: [55, 28, 12, 4, 1],
  linesPerRarity: [1, 1, 2, 2, 3],
  cardMix: { upgrade: 0.5, newWeapon: 0.3, logbook: 0.2 },
  // Incoming damage: contacts/s = contactRate x (melee enemies alive)^0.6 x (1 - dodge); shots/s = enemies x 1/2.5 s x hit chance.
  // Calibrated (harm 0.5 of the first guess): contactRate 0.09, shot hit 0.3, focus 0.25, boss 0.35 hits/s, all x0.5.
  contactRate: 0.045, dodge: 0.7, shotHitChance: 0.15, shotInterval: 2.5,
  bossHitsPerSecond: 0.175, focusHitsPerSecond: 0.125,
  focusShareOfDps: 0.5,        // single-target DPS spent on a miniboss or elite while waves are still coming
  eliteXp: 2,                  // elites pay x XP
  maxMinutes: 20,
  // Calibrated against the owner's playtest (3 Oct, current data): level 24 and 515 kills at 5:13, level 28 and 702
  // kills at 7:18, hull mostly high (the game felt too easy). See calibrate.mjs.
  accuracy: 0.3,              // share of the build's damage that lands on an enemy (misses, travel, overkill on the dying)
  arrivalSeconds: 4,           // a new enemy spawns off-screen and needs this long to come into range
  topUpPerSecond: 0.5,         // the flow floor's top-up (fitted: kills are spawn-limited, as in the playtest)
};

// Stat ids (Foundation/Core/GameEnums.cs StatId).
const S = { Hull: 0, HullRegen: 1, Shield: 2, Armor: 3, Evasion: 4, Damage: 5, AttackSpeed: 6, CritChance: 7, CritDamage: 8, Quantity: 9, Size: 10,
  Duration: 11, ProjectileSpeed: 12, Knockback: 13, PickupRadius: 14, Luck: 15, XPGain: 16, GoldGain: 17, DoubloonGain: 18, EliteDamage: 19, MoveSpeed: 20 };

export function rng(seed) { let s = seed >>> 0 || 1; return () => { s ^= s << 13; s >>>= 0; s ^= s >>> 17; s ^= s << 5; s >>>= 0; return s / 4294967296; }; }
const pickW = (r, items, w) => { let t = 0; for (const x of items) t += w(x); let v = r() * t; for (const x of items) { v -= w(x); if (v <= 0) return x; } return items[items.length - 1]; };

/** One voyage. params: { captain, spawnMult, enemyDamageMult, enemyHpMult, seed, policy } -> run record. */
export function simulateRun(data, params) {
  const r = rng(params.seed || 1), M = { ...MODEL, ...(params.model || {}) };
  const tl = data.timeline, P = data.progression, E = data.enemies, D = data.director;
  const cap = data.captains[params.captain] || Object.values(data.captains)[0];
  const stats = { [S.Damage]: 1, [S.AttackSpeed]: 1, [S.CritChance]: 0.05, [S.CritDamage]: 1.5, [S.Quantity]: 0, [S.Size]: 1, [S.XPGain]: 1, [S.EliteDamage]: 1, [S.HullRegen]: 0, [S.MoveSpeed]: 1, [S.PickupRadius]: 1, [S.Duration]: 1 };
  const statAdd = (id, add, mul = 1) => { stats[id] = ((stats[id] ?? 0) + add) * mul; };
  let maxHull = cap.hull, hull = maxHull, level = 1, xp = 0, kills = 0, spawns = 0, damageTaken = 0;
  const weapons = [], logbooks = {}, items = [], evolved = [];
  const addWeapon = id => weapons.push({ id, level: 1, dmg: 1, cd: 1, qty: 0, size: 1, evo: false });
  addWeapon(cap.weapon);
  const groups = []; // { id, hp, n, ranged, dmg, xp, elite, ready }
  const focus = [];  // single targets: { id, hp, maxHp, dmg, boss, arena, xp }
  let healAt = 90 + r() * 60, lastHeal = 0, chestAt = M.chestEverySeconds, bandKills = 0, bandSpawns = 0, bandStart = 0, bandRate = 1, bandElite = 0;
  let spawnAcc = 0, topUpAcc = 0, overtimeAcc = 0, bossDone = false, bossSpawned = false, bossKill = -1, died = false, minHull = 1;
  const minutes = [], focusLog = [], damageBy = {}, killsBy = {};
  const xpToNext = lv => { const t = P.xpTable; if (lv - 1 < t.length) return t[lv - 1]; const n = t.length; return t[n - 1] + (lv - n) * ((t[n - 1] - t[n - 5]) / 4); };
  const minuteOf = t => t / 60, hpCurve = m => data.curve.hp[0] + data.curve.hp[1] * m + data.curve.hp[2] * m * m, dmgCurve = m => data.curve.dmg[0] + data.curve.dmg[1] * m;

  const weaponNumbers = w => {
    const m = M.weapons[w.id] || [1, 1, 1, 1], base = data.weapons[w.id];
    const lvl = w.dmg * w.level * 0.1 + 1;
    const crit = 1 + Math.min(1, stats[S.CritChance]) * Math.max(0, stats[S.CritDamage] - 1);
    const dmgMult = lvl * stats[S.Damage] * crit * (w.evo ? M.evolutionMultiplier : 1);
    const qty = (base.quantity + w.qty + Math.floor(stats[S.Quantity])) / base.quantity;
    const single = base.damage * m[0] * qty * dmgMult / (m[1] * w.cd / stats[S.AttackSpeed]);
    return { single, perHit: base.damage * m[2] * dmgMult, targets: m[3] * (1 + 0.5 * (w.size * stats[S.Size] - 1)) };
  };
  const singleDps = () => weapons.reduce((s, w) => s + weaponNumbers(w).single, 0);

  const spawnEnemy = (id, t, hpMult = 1, eliteRoll = false) => {
    const e = E[id]; if (!e) return;
    const alive = groups.reduce((s, g) => s + g.n, 0) + focus.length;
    if (alive >= tl.cap) return;
    let hp = e.hp * hpCurve(minuteOf(t)) * hpMult * (params.enemyHpMult || 1), elite = false;
    if (eliteRoll && r() < bandElite) {
      // An elite is a priority target (the player turns on it), like a miniboss that fights alongside the waves.
      hp = Math.max(hp * D.eliteHp, singleDps() * (params.eliteTtk ?? D.ttk.elite));
      focus.push({ id: 'elite_' + id, hp, maxHp: hp, dmg: e.dmg * 1.1, boss: false, arena: false, xp: e.xp * M.eliteXp, start: t, elite: true });
      spawns++; bandSpawns++; return;
    }
    groups.push({ id, hp, n: 1, ranged: e.ranged, dmg: e.dmg * (elite ? 1.1 : 1), xp: e.xp * (elite ? M.eliteXp : 1), elite, ready: t + M.arrivalSeconds });
    spawns++; bandSpawns++;
  };
  const pickBandEnemy = band => {
    const pools = band.pools.filter(p => p.entries.length); let roll = r(), pool = pools[0];
    if (pools.length > 1 && roll > pools[0].share) { let th = pools[0].share; pool = pools[pools.length - 1]; for (let i = 1; i < pools.length; i++) { th += pools[i].share; if (roll <= th) { pool = pools[i]; break; } } }
    return pickW(r, pool.entries, x => x[1])[0];
  };
  const waveAt = t => { const w = tl.waves; let a = w[0], b = null; for (const x of w) { if (x[0] <= t) a = x; else { b = x; break; } } if (!b) return [a[1], a[2]]; const k = (t - a[0]) / (b[0] - a[0]); return [a[1] + (b[1] - a[1]) * k, a[2] + (b[2] - a[2]) * k]; };

  const levelUp = () => {
    level++;
    const evoReady = weapons.find(w => !w.evo && w.level >= P.evolutionLevel && Object.values(data.evolutions).some(e => e.weapon === w.id && items.includes(e.item)));
    if (evoReady) { evoReady.evo = true; evolved.push(evoReady.id); return; }
    const rarity = pickW(r, [0, 1, 2, 3, 4], i => M.rarityWeights[i]);
    const canNew = weapons.length < P.weaponSlots, canBook = Object.keys(logbooks).length < P.logbookSlots || Object.entries(logbooks).some(([k, v]) => v < data.logbooks[k].max);
    const upgradable = weapons.filter(w => w.level < (data.weapons[w.id].maxLevel || P.maxWeaponLevel));
    const roll = r(), mix = M.cardMix;
    if (canNew && roll < mix.newWeapon) {
      const offerable = data.starterWeapons && data.starterWeapons.length ? data.starterWeapons : Object.keys(data.weapons);
      const pool = offerable.filter(id => data.weapons[id] && !weapons.some(w => w.id === id));
      if (!pool.length) return;
      addWeapon(params.policy?.weapons ? pickW(r, pool, id => params.policy.weapons[id] ?? 1) : pool[Math.floor(r() * pool.length)]);
    } else if (canBook && roll < mix.newWeapon + mix.logbook) {
      const books = Object.values(data.logbooks).filter(b => (logbooks[b.id] ?? 0) < b.max && (logbooks[b.id] || Object.keys(logbooks).length < P.logbookSlots));
      if (!books.length) return;
      const b = books[Math.floor(r() * books.length)], add = b.add * data.progression.rarityMult[rarity];
      logbooks[b.id] = (logbooks[b.id] ?? 0) + 1;
      if (b.stat === S.Hull) { maxHull += add; hull += add; } else if (b.stat === S.MoveSpeed) statAdd(S.MoveSpeed, add / 6.2); else if (b.stat === S.PickupRadius) statAdd(S.PickupRadius, add / 6); else statAdd(b.stat, add);
    } else if (upgradable.length) {
      const w = upgradable[Math.floor(r() * upgradable.length)], axes = data.weapons[w.id].axes;
      w.level = Math.min(data.weapons[w.id].maxLevel || 12, w.level + P.levelsPerRarity[rarity]);
      for (let i = 0; i < M.linesPerRarity[rarity] && axes.length; i++) {
        const a = pickW(r, axes, x => x.weight), v = a.inc * data.progression.rarityMult[rarity];
        if (a.axis === 0) w.dmg *= 1 + v; else if (a.axis === 1) w.cd *= 1 - Math.min(0.9, v); else if (a.axis === 2) w.qty += Math.max(1, Math.round(v)); else if (a.axis === 4) w.size *= 1 + v;
      }
    }
  };

  const T = M.maxMinutes * 60;
  const density = t => (params.spawnMult || 1) * (t < 300 ? (params.earlyMult ?? 1) : (params.lateMult ?? 1));
  let minuteKills = 0, minuteDamage = 0, minuteSpawns = 0;
  for (let t = 0; t < T; t++) {
    const minute = minuteOf(t), alive = () => groups.reduce((s, g) => s + g.n, 0);
    const arena = focus.some(f => f.arena && !f.boss);
    // Kill band (30 s): next band's rate and elite chance.
    if (D.killBand && t - bandStart >= D.bandSeconds) {
      const ratio = bandSpawns >= 6 ? bandKills / bandSpawns : -1;
      if (ratio < 0) { bandRate = 1; bandElite = 0; } else { const k = Math.min(1, Math.max(0, (ratio - D.low) / (D.high - D.low))); bandRate = D.lowRate + (1 - D.lowRate) * k; bandElite = ratio >= D.high ? D.eliteChance : 0; }
      bandKills = bandSpawns = 0; bandStart = t;
    }
    // Ambient spawns, floor top-up, swarm pulses, hordes (stage time); the Drowned Pursuit after the deadline.
    if (t < tl.deadline) {
      if (!arena) {
        const band = [...tl.bands].reverse().find(b => b.t <= t) || tl.bands[0];
        const [floor, waveRate] = waveAt(t);
        const pulse = tl.pulses.find(p => t >= p.t && t < p.t + p.seconds);
        spawnAcc += band.rate * waveRate * (pulse ? pulse.mult : 1) * bandRate * density(t) * (bossSpawned && !bossDone ? 0.5 : 1);
        while (spawnAcc >= 1) { spawnAcc -= 1; spawnEnemy(pickBandEnemy(band), t, 1, true); }
        const want = floor * (tl.cap - tl.reserved) * bandRate * density(t) * (bossSpawned && !bossDone ? 0.3 : 1);
        if (alive() < want) { topUpAcc += Math.min(M.topUpPerSecond, want - alive()); while (topUpAcc >= 1) { topUpAcc -= 1; spawnEnemy(pickBandEnemy(band), t, 1, true); } }
      }
      for (const h of tl.hordes) if (h[0] === t) for (let i = 0; i < Math.round(h[2] * density(t)); i++) spawnEnemy(h[1], t);
    } else {
      const ot = (t - tl.deadline);
      const interval = Math.max(tl.overtime.floor, tl.overtime.interval * Math.pow(tl.overtime.decay, Math.floor(ot / tl.overtime.step)));
      overtimeAcc += (params.spawnMult || 1) / interval;
      const hpm = 3 * Math.pow(2, ot / 60 * 0.25);
      while (overtimeAcc >= 1) { overtimeAcc -= 1; spawnEnemy(r() < (ot < 120 ? 0.8 : 0.6) ? 'drowned_pursuer' : 'drowned_harrier', t, hpm); }
    }
    // Minibosses, elites and the boss (time-to-kill floors on the build's DPS at spawn).
    for (const f of tl.focus) if (f.t === t && E[f.enemy]) {
      const e = E[f.enemy], hp0 = e.hp * hpCurve(minute) * f.hpMult * (params.enemyHpMult || 1);
      const hp = Math.max(hp0, singleDps() * (f.arena ? D.ttk.arena : D.ttk.focus));
      focus.push({ id: f.id, hp, maxHp: hp, dmg: e.dmg, boss: false, arena: f.arena, xp: e.xp * 5, start: t });
    }
    if (!bossSpawned && t >= 840) {
      bossSpawned = true; const e = E.barnacle_king; const hp = Math.max(e.hp * hpCurve(minute) * (params.enemyHpMult || 1), singleDps() * D.ttk.boss);
      focus.push({ id: 'barnacle_king', hp, maxHp: hp, dmg: e.dmg, boss: true, arena: true, xp: e.xp, start: t });
    }

    // Player damage: single-target DPS splits between the focus target and the crowd; area weapons reach several enemies.
    let crowdDamage = 0, focusDamage = 0, hitWeighted = 0, singleSum = 0;
    const n = alive(), splash = Math.min(1, n / M.crowdForFullSplash);
    const toFocus = focus.length ? (focus.some(f => f.arena) ? 0.85 : M.focusShareOfDps) : 0;
    for (const w of weapons) {
      const x = weaponNumbers(w);
      focusDamage += x.single * toFocus * Math.max(M.accuracy, 0.6); // a big single target is easier to hit
      // Only the single-target share turns to the focus target; splash, pierce, orbits and auras keep hitting the crowd.
      crowdDamage += x.single * M.accuracy * ((1 - toFocus) + (x.targets - 1) * splash);
      hitWeighted += x.single * x.perHit; singleSum += x.single;
    }
    const perHit = singleSum > 0 ? hitWeighted / singleSum : 1; // a typical hit (damage-weighted over the weapons)
    if (focus.length) {
      const f = focus[0]; f.hp -= focusDamage * stats[S.EliteDamage];
      if (f.hp <= 0) { focus.shift(); kills++; minuteKills++; xp += f.xp * data.pickups.xpMult * stats[S.XPGain]; focusLog.push([f.id, f.start, t - f.start, Math.round(f.maxHp)]); if (f.boss) { bossDone = true; bossKill = t; } }
    }
    // Crowd: damage spread over the engaged enemies by count; a kill costs its HP rounded up to whole hits (overkill).
    const engagedTotal = groups.reduce((s, g) => s + (g.ready <= t ? g.n : 0), 0) * M.engagement;
    if (engagedTotal > 0 && crowdDamage > 0) {
      for (const g of groups) {
        if (g.ready > t) continue;
        const eng = g.n * M.engagement, share = crowdDamage * eng / engagedTotal;
        const cost = Math.max(1, Math.ceil(g.hp / Math.max(0.01, perHit))) * Math.max(0.01, perHit);
        const k = Math.min(eng, share / cost), killed = Math.min(g.n, Math.floor(k) + (r() < k - Math.floor(k) ? 1 : 0));
        if (!killed) continue;
        g.n -= killed; kills += killed; minuteKills += killed; bandKills += killed;
        killsBy[g.id] = (killsBy[g.id] ?? 0) + killed;
        xp += killed * g.xp * data.pickups.xpMult * M.lootShare * stats[S.XPGain];
      }
      for (let i = groups.length - 1; i >= 0; i--) if (groups[i].n <= 0) groups.splice(i, 1);
    }
    while (xp >= xpToNext(level)) { xp -= xpToNext(level); levelUp(); }

    // Incoming damage.
    const dodge = Math.min(0.95, M.dodge * (0.85 + 0.15 * stats[S.MoveSpeed]));
    let melee = 0, hits = 0;
    for (const g of groups) {
      if (g.ready > t) continue;
      if (g.ranged) { const shots = g.n * M.engagement / M.shotInterval * M.shotHitChance; const d = shots * g.dmg; hits += d; damageBy[g.id] = (damageBy[g.id] ?? 0) + d; }
      else melee += g.n * M.engagement;
    }
    if (melee > 0) {
      const contacts = M.contactRate * Math.pow(melee, 0.6) * (1 - dodge);
      for (const g of groups) if (!g.ranged && g.ready <= t) { const d = contacts * (g.n * M.engagement / melee) * g.dmg; hits += d; damageBy[g.id] = (damageBy[g.id] ?? 0) + d; }
    }
    for (const f of focus) { const d = f.dmg * (f.boss ? M.bossHitsPerSecond : M.focusHitsPerSecond) * (1 - dodge * 0.6); hits += d; damageBy[f.id] = (damageBy[f.id] ?? 0) + d; }
    const taken = hits * dmgCurve(minute) * (params.enemyDamageMult || 1);
    hull -= taken; damageTaken += taken; minuteDamage += taken;
    hull = Math.min(maxHull, hull + (stats[S.HullRegen] || 0));
    if (t >= healAt) { hull = Math.min(maxHull, hull + data.pickups.healValue); lastHeal = t; healAt = t + (data.pickups.healMin + r() * (data.pickups.healMax - data.pickups.healMin)) * (hull / maxHull < 0.35 ? 0.5 : 1); }
    if (t >= chestAt) {
      chestAt = t + M.chestEverySeconds / M.lootShare * 0.8;
      const pool = Object.values(data.items).filter(i => i.inChest);
      const want = weapons.filter(w => w.level >= P.evolutionLevel).map(w => Object.values(data.evolutions).find(e => e.weapon === w.id)).filter(e => e && !items.includes(e.item));
      const item = want.length && r() < 0.4 ? want[0].item : pickW(r, pool, i => M.rarityWeights[i.rarity] ?? 1).id;
      items.push(item);
      for (const [stat, add, mul] of data.items[item].mods) { if (stat === S.Hull) { maxHull += add; hull += add; } else statAdd(stat, add, mul); }
    }
    minHull = Math.min(minHull, hull / maxHull);
    minuteSpawns = spawns;
    if ((t + 1) % 60 === 0 || hull <= 0) {
      minutes.push({ m: Math.round((t + 1) / 60), kills: minuteKills, level, dps: Math.round(singleDps() * 10) / 10, alive: alive() + focus.length, hull: Math.max(0, hull / maxHull), dmg: Math.round(minuteDamage), weapons: weapons.length });
      minuteKills = 0; minuteDamage = 0;
    }
    if (hull <= 0) { died = true; return finish(t); }
  }
  return finish(T);

  function finish(t) {
    return { captain: cap.id, end: t / 60, died, bossKill: bossKill >= 0 ? bossKill / 60 : null, kills, level, dps: singleDps(), damageTaken, minHull,
      weapons: weapons.map(w => w.id), evolved, logbooks: Object.keys(logbooks), items, minutes, focus: focusLog, damageBy, killsBy, spawns };
  }
}

/** Summary of many runs. */
export function summarise(runs) {
  const med = a => { const s = [...a].sort((x, y) => x - y); return s.length ? s[Math.floor(s.length / 2)] : null; };
  const pct = (a, p) => { const s = [...a].sort((x, y) => x - y); return s.length ? s[Math.min(s.length - 1, Math.floor(s.length * p))] : null; };
  const byMinute = [];
  for (let m = 1; m <= 20; m++) {
    const rows = runs.map(r => r.minutes.find(x => x.m === m)).filter(Boolean);
    if (rows.length < Math.max(3, runs.length * 0.1)) break;
    byMinute.push({ m, alive: rows.length / runs.length, kills: med(rows.map(x => x.kills)), level: med(rows.map(x => x.level)), dps: med(rows.map(x => x.dps)), enemies: med(rows.map(x => x.alive)), hull: med(rows.map(x => x.hull)), dmg: med(rows.map(x => x.dmg)) });
  }
  const tension = runs.map(r => { const live = r.minutes.filter(x => x.hull > 0); return live.length ? live.filter(x => x.hull < 0.5).length / live.length : 0; });
  return {
    runs: runs.length, endMedian: med(runs.map(r => r.end)), endP10: pct(runs.map(r => r.end), 0.1), endP90: pct(runs.map(r => r.end), 0.9),
    bossWin: runs.filter(r => r.bossKill != null).length / runs.length, deathBefore10: runs.filter(r => r.died && r.end < 10).length / runs.length,
    deathBefore14: runs.filter(r => r.died && r.end < 14).length / runs.length, survive20: runs.filter(r => !r.died).length / runs.length,
    tension: tension.reduce((a, b) => a + b, 0) / Math.max(1, tension.length), minHull: med(runs.map(r => r.minHull)), byMinute,
  };
}
