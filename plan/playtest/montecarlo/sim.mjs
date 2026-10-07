// Rogue Pirates: numbers-only Monte Carlo of a Saltwind voyage. Written from scratch (playtest round 5, 3 Oct 2026); the
// one balance simulator since V5 M8z (I8z.7, owner 7 Oct 2026: the in-engine simulator is retired). One second per step;
// enemies are counted in groups, not simulated as bodies. The same file runs in Node (batch.mjs, compare_session.mjs,
// fit_sessions.mjs) and in the report page.
//
// The data (data.json) is the game's real config, exported by the Unity Editor (MonteCarloExport: the config set the game
// runs on, resolved the game's way); export_data.py is the YAML fallback and check_export.mjs compares the two. Every
// assumption that is not in the game's data is a named constant in MODEL, fitted on the owner's recorded voyages
// (plan/playtest/sessions) with fit_sessions.mjs.

// ---- Model assumptions (not in the game data) --------------------------------------------------------------------
export const MODEL = {
  // Per weapon at level 1: single-target hits per cycle (x base damage), cycle seconds, one hit's share of base damage,
  // and how many enemies a firing reaches in a dense crowd (pierce, fan, beam, area, orbit). From the code review of
  // every behaviour (2 Oct). This is realistic damage; the game's own theoretical DPS (the fight-length floors) is
  // computed from the data the way PlayerPowerIndex does it.
  weapons: {
    deck_cannons: [1, 1.05, 1, 1], swivel_guns: [1.1, 0.78, 0.55, 1], long_nine: [1, 2.4, 1, 3], harpoon_gun: [1, 1.2, 1, 1],
    chainshot: [1, 1.5, 1, 2], broadside_battery: [1.8, 1.5, 0.6, 1.8], flying_cutlasses: [0.875, 1, 0.35, 3],
    anchor_flail: [1, 3.6, 1, 3], lighthouse_lantern: [1, 6, 1, 4], flamethrower: [9.4, 3.3, 0.28, 3],
    powder_kegs: [0.72, 2.2, 0.72, 3], lightning_mast: [1, 1.8, 1, 3], cursed_doubloons: [1, 2, 1, 3], ramming_prow: [0.3, 0.3, 1, 3],
    siren_bell: [1, 3, 1, 5], mortar: [1, 1.8, 1, 3], maelstrom_charge: [3.7, 5, 0.2, 4], monkey_gunners: [13, 6, 1, 1], parrot_squadron: [22, 4, 1, 1],
  },
  // The cooldowns and orbit spins the weapon table above was measured at (2 Oct); the model scales its cycles by the data's.
  refCooldown: { deck_cannons: 1.05, swivel_guns: 0.6, long_nine: 2.4, harpoon_gun: 1.2, chainshot: 1.5, broadside_battery: 1.5, flying_cutlasses: 1, anchor_flail: 0.75,
    lighthouse_lantern: 0.5, flamethrower: 0.7, powder_kegs: 2.2, lightning_mast: 1.8, cursed_doubloons: 2, ramming_prow: 0.3, siren_bell: 3, mortar: 1.8, maelstrom_charge: 5,
    monkey_gunners: 6, parrot_squadron: 4 },
  refSpin: { flying_cutlasses: 120, anchor_flail: 100, lighthouse_lantern: 60 },
  refBlades: { flying_cutlasses: 2, anchor_flail: 1, lighthouse_lantern: 1 },
  crowdForFullSplash: 25,      // area weapons reach their full target count at this many enemies alive
  // ---- Fitted on the owner's three recorded voyages (fit_sessions.mjs, 7 Oct 2026) ----
  accuracy: 0.4,               // share of the build's realistic damage that lands on an enemy (misses, travel, overkill)
  engagement: 0.9,             // share of the enemies the player fights (the rest wander off)
  arrivalSeconds: 1.5,         // a new enemy spawns off-screen and needs this long to come into range
  lootShare: 0.95,            // share of XP and health barrels the player sails over (barrels must be touched)
  barrelsPerMinute: 140,       // the most XP barrels the ship sails over in a minute: past it, barrels are left behind
                               // (sessions: ~2.3 XP a kill at 90-150 kills a minute, ~1.6 at 150-190)
  vfxDensity: 0.6,             // the quality tier's effects density: a kill's XP barrel bursts into max(3, round(6..10 x this))
                               // pieces of at least 1 XP each (PickupModule.Burst). 0.45 Low, 0.75 Mid, 1 High (fitted 0.6).
  arenaShareOfDps: 0.3,        // share of the single-target damage spent on an arena miniboss or elite (the waves are paused)
  focusShareOfDps: 0.1,        // ... on a scheduled miniboss or elite that fights alongside the waves
  eliteShareOfDps: 0.06,       // ... on each kill-band elite (they mingle with the crowd; nobody turns on them)
  bossShareOfDps: 0.6,         // ... on the Barnacle King
  // Incoming damage: contacts/s = contactRate x (melee enemies in range)^0.6 x (1 - dodge); shots/s = shooters x
  // shotHitChance / shotInterval; a focus enemy hits focusHitsPerSecond (the boss bossHitsPerSecond) x (1 - 0.6 dodge).
  contactRate: 0.08, dodge: 0.7, shotHitChance: 0.02, shotInterval: 2.5,
  bossHitsPerSecond: 0.6, focusHitsPerSecond: 0.05, // scheduled minibosses and elites; a kill-band elite hits like one more enemy of its type
  islandHitsPerMinute: 1.45,   // measured: sessions 2-3 island damage / 5 hull per impact; island impacts that cost hull (data: run.islandImpactDamage, at most one per islandDamageCooldown)
  logbookPickChance: 0.5,      // with a Logbook card and a weapon upgrade on offer, the player takes the Logbook this often
  chestEverySeconds: 105,      // a salvage chest opened this often (sessions: 6-7 chests in 11-14 min)
  evolutionMultiplier: 1.6,    // an evolved weapon's realistic damage, x (its Empowered numbers come from the data)
  linesPerRarity: [1, 1, 2, 2, 3], // fallback when the data has no progression.linesPerRarity
  maxMinutes: 20,
  // The boss is summoned at the Treasury Beacon (no timed boss; its exact marker shows at the data's
  // stage.beaconMarkerSeconds, 13:00). The owner's sessions never summoned it, so 14:00 is assumed.
  // params.bossAt overrides it (seconds; null = never summoned): compare_session.mjs passes the session's summon time.
  bossAtSeconds: 840,
};

// Stat ids (Foundation/Core/GameEnums.cs StatId).
const S = { Hull: 0, HullRegen: 1, Shield: 2, Armor: 3, Evasion: 4, Damage: 5, AttackSpeed: 6, CritChance: 7, CritDamage: 8, Quantity: 9, Size: 10,
  Duration: 11, ProjectileSpeed: 12, Knockback: 13, PickupRadius: 14, Luck: 15, XPGain: 16, GoldGain: 17, DoubloonGain: 18, EliteDamage: 19, MoveSpeed: 20 };
// Upgrade axes (ConfigEnums.UpgradeAxis).
const AX = { Damage: 0, Cooldown: 1, Quantity: 2, Speed: 3, Size: 4, Duration: 5 };

export function rng(seed) { let s = seed >>> 0 || 1; return () => { s ^= s << 13; s >>>= 0; s ^= s >>> 17; s ^= s << 5; s >>>= 0; return s / 4294967296; }; }
const pickW = (r, items, w) => { let t = 0; for (const x of items) t += Math.max(0, w(x)); let v = r() * t; for (const x of items) { v -= Math.max(0, w(x)); if (v <= 0) return x; } return items[items.length - 1]; };
const poly = (c, x) => (c[0] ?? 1) + (c[1] ?? 0) * x + (c[2] ?? 0) * x * x;

/** XP the game pays for a kill worth `value` (PickupModule: the barrel bursts into pieces, each at least 1), before the pace. */
export function xpPaidTable(data, density) {
  const p = data.pickups, lo = p.burstMin ?? 6, hi = Math.max(lo, p.burstMax ?? 10), minimum = p.burstMinimum ?? 3, table = [];
  for (let v = 0; v <= 400; v++) {
    let sum = 0;
    for (let k = lo; k <= hi; k++) {
      const count = Math.max(minimum, Math.round(k * density)), per = Math.max(1, Math.floor(v / count)), rem = v - per * count;
      sum += Math.max(1, per + rem) + Math.max(1, per) * (count - 1);
    }
    table.push(v > 0 ? sum / (hi - lo + 1) : 0);
  }
  return table;
}

/** One voyage. params: { captain, startWeapon, weaponOrder, pickPlan, spawnMult, enemyDamageMult, enemyHpMult, earlyMult, lateMult, seed, bossAt, model } -> run record. */
export function simulateRun(data, params) {
  const r = rng(params.seed || 1), M = { ...MODEL, ...(params.model || {}) };
  const tl = data.timeline, P = data.progression, E = data.enemies, D = data.director, C = data.curve, PK = data.pickups;
  const flow = tl.flow || { enabled: true, smooth: false, waves: tl.waves.map(w => ({ t: w[0], share: w[1], rate: w[2], topUpInterval: 1, maxTopUp: 4 })), hordes: tl.hordes.map(h => ({ id: String(h[0]), t: h[0], enemy: h[1], count: h[2], hpMult: 1, dmgMult: 1 })), release: null };
  const release = flow.release || { rate: 1, floor: 1, afterArena: 0, afterBoss: 0, hordeDelay: 0, afterHorde: 0, bossFightFloor: 0.3, preHordeLull: 0 };
  const cap = data.captains[params.captain] || Object.values(data.captains)[0];
  // The Captain's passive damage per player level (CaptainModule.AdditiveDamageBonus), capped.
  const captainDamage = () => Math.min(cap.damageCap ?? 0, (cap.damagePerLevel ?? 0) * level);
  const stats = { [S.Damage]: 1, [S.AttackSpeed]: 1, [S.CritChance]: 0.05, [S.CritDamage]: 1.5, [S.Quantity]: 0, [S.Size]: 1, [S.XPGain]: 1, [S.EliteDamage]: 1, [S.HullRegen]: 0, [S.MoveSpeed]: 1, [S.PickupRadius]: 1, [S.Duration]: 1 };
  const statAdd = (id, add, mul = 1) => { stats[id] = ((stats[id] ?? 0) + add) * mul; };
  const ambientCap = tl.cap - tl.reserved, spawnGap = Math.max(0.05, tl.spawnGap ?? 0.35), grace = tl.grace ?? 0;
  const xpPaid = xpPaidTable(data, M.vfxDensity), xpPace = PK.xpMult ?? 1;
  const newBonus = P.newWeaponDamageBonus ?? 1, coverage = D.coverage || [], evoOffense = D.evolutionOffense ?? 1.6;
  const disabled = new Set((data.unlocks && data.unlocks.disabled) || []);
  const offerable = Object.keys(data.weapons).filter(id => !disabled.has(id) && (data.unlocks?.unlockEverything !== false || !data.starterWeapons?.length || data.starterWeapons.includes(id)));
  const xpToNext = lv => {
    if (P.xpToNext && lv < P.xpToNext.length) return P.xpToNext[lv];
    const t = P.xpTable, n = t.length; if (lv < n) return t[lv];
    const k = Math.min(4, n - 1), step = Math.max(1, Math.round((t[n - 1] - t[n - 1 - k]) / k)); return t[n - 1] + step * (lv - n + 1);
  };
  let maxHull = cap.hull, hull = maxHull, level = P.startLevel ?? 0, xp = 0, xpCarry = 0, kills = 0, spawns = 0, damageTaken = 0, healed = 0, islandDamage = 0;
  const weapons = [], logbooks = {}, items = [], evolved = [];
  const addWeapon = id => weapons.push({ id, level: 1, dmg: newBonus, cd: 1, qty: 0, size: 1, acc: {}, evo: null });
  addWeapon(params.startWeapon || cap.weapon);
  const groups = []; // { id, hp, n, ranged, dmg, xp, ready }
  const focus = [];  // single targets: { id, kind: 'arena'|'focus'|'elite'|'boss', hp, maxHp, dmg, xp, start, phase }
  let healAt = -1, lastHeal = 0, chestAt = M.chestEverySeconds, bandKills = 0, bandSpawns = 0, bandStart = 0, bandRate = 1, bandElite = 0;
  let spawnAcc = 0, topUpAcc = 0, overtimeAcc = 0, bossDone = false, bossSpawned = false, bossKill = -1, died = false, minHull = 1;
  let releaseUntil = -1, islandReadyAt = 0, arenaSince = -1;
  const pendingReleases = [], hordesDone = new Set(), focusDone = new Set();
  const minutes = [], focusLog = [], damageBy = {}, killsBy = {}, levelAt = []; // levelAt[i] = second level i + 1 was reached
  const bossAt = params.bossAt !== undefined ? params.bossAt : (data.demo && data.demo.autoBossSeconds > 0 ? data.demo.autoBossSeconds : M.bossAtSeconds);
  let now = 0;
  const minuteOf = t => t / 60;
  const ordinary = (t, c) => t < (C.ordinaryDelay ?? 1e9) ? 1 : poly(c, (t - C.ordinaryDelay) / 60);
  const hpScale = t => poly(C.hp, minuteOf(t)) * (C.global_hp ?? 1) * ordinary(t, C.ordinaryHp || [1]) * (data.experiment?.enemyHp ?? 1) * (params.enemyHpMult || 1);
  const dmgScale = t => poly(C.dmg, minuteOf(t)) * ordinary(t, C.ordinaryDmg || [1]) * (data.experiment?.enemyDamage ?? 1);

  // Realistic damage (the MODEL weapon table) and the game's theoretical DPS (PlayerPowerIndex.Offense: the floors).
  const weaponNumbers = w => {
    const m = M.weapons[w.id] || [1, 1, 1, 1], base = data.weapons[w.id];
    const lvl = base.levelDamageFactor === false ? w.dmg : w.dmg * w.level * 0.1 + 1;
    const crit = 1 + Math.min(1, stats[S.CritChance]) * Math.max(0, stats[S.CritDamage] - 1);
    const emp = w.evo && w.evo.empower;
    const dmgMult = lvl * (stats[S.Damage] + captainDamage()) * crit * (w.evo ? (emp ? emp.damage : M.evolutionMultiplier) : 1);
    const qty = Math.max(1, base.quantity + w.qty + (emp ? emp.quantity : 0) + Math.floor(stats[S.Quantity]));
    const cd = w.cd * (emp ? emp.cooldown : 1);
    // Orbits: passes per second follow the spin (their cooldown is unused); everything else follows the cooldown.
    const cycleScale = M.refSpin[w.id] ? M.refSpin[w.id] / Math.max(1, base.spin || M.refSpin[w.id]) : (M.refCooldown[w.id] ? base.cooldown / M.refCooldown[w.id] : 1);
    const blades = M.refBlades[w.id] ? (base.blades || M.refBlades[w.id]) / M.refBlades[w.id] : 1;
    const single = base.damage * m[0] * (qty / base.quantity) * dmgMult * blades / (m[1] * cycleScale * cd / stats[S.AttackSpeed]);
    const game = base.damage * lvl * (stats[S.Damage] + captainDamage()) * crit * (w.evo ? Math.max(1, evoOffense) * (emp ? emp.damage : 1) : 1)
      / Math.max(0.05, base.cooldown * cd / stats[S.AttackSpeed]) * qty * (base.coverage ?? coverage[base.behavior] ?? 1) * (base.hitsPerFiring ?? 1);
    return { single, game, perHit: base.damage * m[2] * dmgMult, targets: m[3] * (1 + 0.5 * (w.size * stats[S.Size] - 1)) };
  };
  const singleDps = () => weapons.reduce((s, w) => s + weaponNumbers(w).single, 0);
  const gameDps = () => weapons.reduce((s, w) => s + weaponNumbers(w).game, 0);
  const alive = () => groups.reduce((s, g) => s + g.n, 0) + focus.length;
  const arenaActive = () => focus.some(f => f.kind === 'arena');

  const spawnEnemy = (id, t, opts = {}) => {
    let e = E[id]; if (!e) return false;
    const rangedCap = (tl.tagCaps && tl.tagCaps.ranged_gunner) || 99;
    if (e.ranged && opts.fallbackId && groups.reduce((s, g) => s + (g.ranged ? g.n : 0), 0) >= rangedCap) { if (!E[opts.fallbackId]) return false; id = opts.fallbackId; e = E[id]; }
    if (alive() >= (opts.cap ?? ambientCap)) return false;
    const hp = e.hp * hpScale(t) * (opts.hpMult ?? 1), dmg = e.dmg * (opts.dmgMult ?? 1);
    spawns++; bandSpawns++;
    if (opts.eliteRoll && bandElite > 0 && r() < bandElite) {
      // A kill-band elite (adaptive): the elite multiplier and its affix, floored on the build's theoretical DPS.
      const ehp = Math.max(hp * (D.eliteHp ?? 1) * (D.eliteAffixHp ?? 1.25), gameDps() * D.ttk.elite);
      focus.push({ id: 'adaptive:' + id, kind: 'elite', hp: ehp, maxHp: ehp, dmg: dmg * (D.eliteDamage ?? 1.1), xp: e.xp, start: t });
      return true;
    }
    groups.push({ id, hp, n: 1, ranged: e.ranged, dmg, xp: e.xp, ready: t + M.arrivalSeconds });
    return true;
  };
  const mainOf = band => pickW(r, band.pools[0].entries, x => x[1])[0];
  const pickBandEnemy = band => {
    const pools = band.pools.filter(p => p.entries.length); let roll = r(), pool = pools[0];
    if (pools.length > 1 && roll > pools[0].share) { let th = pools[0].share; pool = pools[pools.length - 1]; for (let i = 1; i < pools.length; i++) { th += pools[i].share; if (roll <= th) { pool = pools[i]; break; } } }
    return pickW(r, pool.entries, x => x[1])[0];
  };
  // The current minute wave (FlowSettings.Sample: between keyframes when smoothWaves is on).
  const waveAt = t => {
    const w = flow.waves; let i = 0; while (i + 1 < w.length && w[i + 1].t <= t) i++;
    const a = w[i], b = w[i + 1];
    if (!flow.smooth || !b || t < a.t) return { share: a.share, rate: a.rate, wave: a };
    const k = (t - a.t) / (b.t - a.t); return { share: a.share + (b.share - a.share) * k, rate: a.rate + (b.rate - a.rate) * k, wave: a };
  };
  const inLull = t => (release.preHordeLull ?? 0) > 0 && flow.hordes.some(h => !hordesDone.has(h.id) && t >= h.t - release.preHordeLull && t < h.t);

  // params.pickPlan (a recorded session's level-up picks, in order: { kind, id }) replays that build: each level-up takes
  // the session's next pick when it is possible, so the model's combat and XP constants are measured on the same build.
  let pickIndex = 0;
  const applyLogbook = (b, rarity) => {
    const add = b.add * P.rarityMult[rarity];
    logbooks[b.id] = (logbooks[b.id] ?? 0) + 1;
    if (b.stat === S.Hull) { maxHull += add; hull += add; } else if (b.stat === S.MoveSpeed) statAdd(S.MoveSpeed, add / 6.2); else if (b.stat === S.PickupRadius) statAdd(S.PickupRadius, add / 6); else statAdd(b.stat, add);
  };
  const upgradeWeapon = (w, rarity) => {
    const base = data.weapons[w.id], axes = base.axes || [];
    w.level = Math.min(base.maxLevel || P.maxWeaponLevel, w.level + (P.levelsPerRarity[rarity] ?? 1));
    // OfferGenerator.RollUpgradePackage: distinct axes, each base increment x rarity power x upgradePowerMultiplier
    // (volley: rounded, at least 1), capped by the axis's maxAccumulated (ProgressionLoadout).
    const used = new Set(), lines = (P.linesPerRarity || M.linesPerRarity)[rarity] ?? 1;
    for (let i = 0; i < lines; i++) {
      const free = axes.filter(x => !used.has(x.axis)); if (!free.length) break;
      const a = pickW(r, free, x => Math.max(0.01, x.weight)); used.add(a.axis);
      const rolled = a.axis === AX.Quantity ? Math.max(1, Math.round(a.inc * P.rarityMult[rarity])) : a.inc * P.rarityMult[rarity] * (P.upgradePowerMultiplier ?? 1);
      const room = a.cap > 0 ? a.cap - (w.acc[a.axis] ?? 0) : Infinity;
      const v = Math.min(room, rolled); if (!(v > 0)) continue;
      w.acc[a.axis] = (w.acc[a.axis] ?? 0) + v;
      if (a.axis === AX.Damage) w.dmg *= 1 + v; else if (a.axis === AX.Cooldown) w.cd *= 1 - Math.min(1, v); else if (a.axis === AX.Quantity) w.qty += Math.max(1, Math.round(v)); else if (a.axis === AX.Size) w.size *= 1 + v;
    }
  };
  const replay = rarity => {
    const plan = params.pickPlan && params.pickPlan[pickIndex++];
    if (!plan) return false;
    const owned = weapons.find(w => w.id === plan.id);
    if (plan.kind === 'NewWeapon' && !owned && data.weapons[plan.id] && weapons.length < P.weaponSlots) { addWeapon(plan.id); return true; }
    if (plan.kind === 'WeaponUpgrade' && owned && owned.level < (data.weapons[owned.id].maxLevel || P.maxWeaponLevel)) { upgradeWeapon(owned, rarity); return true; }
    if (plan.kind === 'WeaponEvolution' && owned && !owned.evo) { const e = Object.values(data.evolutions).find(x => x.weapon === owned.id); if (e) { owned.evo = e; evolved.push(owned.id); return true; } }
    if (plan.kind === 'Logbook' && data.logbooks[plan.id] && (logbooks[plan.id] ?? 0) < data.logbooks[plan.id].max) { applyLogbook(data.logbooks[plan.id], rarity); return true; }
    return plan.kind.startsWith('Fallback'); // a fallback card (heal or damage) is not modelled
  };

  const levelUp = () => {
    level++; levelAt.push(now);
    if (params.pickPlan) { const rarity = pickW(r, [0, 1, 2, 3, 4], i => (P.rarityWeights || [60, 26, 10, 3.2, 0.8])[i]); if (replay(rarity)) return; }
    const evoReady = weapons.find(w => !w.evo && w.level >= (P.evolutionLevel || P.maxWeaponLevel) && Object.values(data.evolutions).some(e => e.weapon === w.id && items.includes(e.item)));
    if (evoReady) { evoReady.evo = Object.values(data.evolutions).find(e => e.weapon === evoReady.id); evolved.push(evoReady.id); return; }
    const rarity = pickW(r, [0, 1, 2, 3, 4], i => (P.rarityWeights || [60, 26, 10, 3.2, 0.8])[i]);
    const upgradable = weapons.filter(w => w.level < (data.weapons[w.id].maxLevel || P.maxWeaponLevel));
    const newPool = offerable.filter(id => data.weapons[id] && !weapons.some(w => w.id === id));
    const ownedBooks = Object.keys(logbooks), bookUp = ownedBooks.filter(k => logbooks[k] < data.logbooks[k].max);
    const bookNew = ownedBooks.length < P.logbookSlots ? Object.values(data.logbooks).filter(b => b.inPool !== false && !(b.id in logbooks)) : [];
    // The cards: each rolls a category from the row for the weapons carried (categories that cannot be offered drop out).
    const rows = data.offerWeights || [], row = rows.find(x => weapons.length <= x.maxWeapons) || rows[rows.length - 1] || { weaponUpgrade: 50, newWeapon: 30, logbookUpgrade: 10, newLogbook: 10 };
    const cats = [['weaponUpgrade', upgradable.length > 0], ['newWeapon', !params.solo && weapons.length < P.weaponSlots && newPool.length > 0],
      ['logbookUpgrade', bookUp.length > 0], ['newLogbook', bookNew.length > 0]].filter(([, ok]) => ok).map(([k]) => k);
    if (!cats.length) return; // a fallback card (heal or damage): not modelled
    const dealt = new Set(); for (let i = 0; i < (P.cardsPerLevel || 3); i++) dealt.add(pickW(r, cats, k => row[k] ?? 0));
    // The owner's picks (sessions): a new weapon whenever one is offered, then weapon upgrades; Logbooks sometimes.
    let take = dealt.has('newWeapon') ? 'newWeapon' : null;
    const book = dealt.has('logbookUpgrade') ? 'logbookUpgrade' : dealt.has('newLogbook') ? 'newLogbook' : null;
    if (!take) take = dealt.has('weaponUpgrade') && !(book && r() < M.logbookPickChance) ? 'weaponUpgrade' : book || [...dealt][0];
    if (take === 'newWeapon') {
      // params.weaponOrder (a recorded session's new-weapon picks, in order) replays that build; otherwise a random offer.
      const next = (params.weaponOrder || []).find(id => newPool.includes(id));
      addWeapon(next || newPool[Math.floor(r() * newPool.length)]); return;
    }
    if (take === 'logbookUpgrade' || take === 'newLogbook') {
      applyLogbook(take === 'newLogbook' ? bookNew[Math.floor(r() * bookNew.length)] : data.logbooks[bookUp[Math.floor(r() * bookUp.length)]], rarity);
      return;
    }
    upgradeWeapon(upgradable[Math.floor(r() * upgradable.length)], rarity);
  };

  const T = M.maxMinutes * 60;
  const density = t => (params.spawnMult || 1) * (data.experiment?.spawnDensity ?? 1) * (t < 300 ? (params.earlyMult ?? 1) : (params.lateMult ?? 1));
  let minuteKills = 0, minuteDamage = 0;
  const killWindow = new Array(60).fill(0); let killWindowSum = 0; // kills over the last 60 s (barrels left behind)
  for (let t = 0; t < T; t++) {
    now = t;
    const minute = minuteOf(t);
    const bossFight = bossSpawned && !bossDone;
    // Kill band (fixed bands): next band's rate and elite chance (DifficultyDirector.JudgeKillBand).
    if (D.killBand && t - bandStart >= D.bandSeconds) {
      if (bandSpawns < (D.minSpawns ?? 6)) { bandRate = 1; bandElite = 0; }
      else { const ratio = bandKills / bandSpawns, k = Math.min(1, Math.max(0, (ratio - D.low) / (D.high - D.low))); bandRate = D.lowRate + (1 - D.lowRate) * k; bandElite = ratio >= D.high ? D.eliteChance : 0; }
      bandKills = bandSpawns = 0; bandStart += D.bandSeconds;
    }
    // Release windows: after an arena, after the boss, a while after each horde.
    for (let i = pendingReleases.length - 1; i >= 0; i--) if (t >= pendingReleases[i][0]) { releaseUntil = Math.max(releaseUntil, t + pendingReleases[i][1]); pendingReleases.splice(i, 1); }
    const arena = arenaActive();
    // V5 M8z: an arena fight pauses the waves until it ends (or for at most arenaMaxPauseSeconds).
    if (arena && D.arenaMaxPauseSeconds > 0 && arenaSince >= 0 && t - arenaSince > D.arenaMaxPauseSeconds) for (const f of focus) if (f.kind === 'arena') f.kind = 'focus';
    if (t < tl.deadline) {
      if (!arenaActive() && t >= grace) {
        const band = [...tl.bands].reverse().find(b => b.t <= t) || tl.bands[0];
        const { share, rate, wave } = waveAt(t);
        const relaxed = t < releaseUntil || inLull(t);
        const pulse = tl.pulses.find(p => t >= p.t && t < p.t + p.seconds);
        // Ambient: the band's rate x the wave's x pulse x kill band x release x the boss fight; one spawn per gap at most.
        const perSecond = band.rate * rate * (pulse && !bossFight ? pulse.mult : 1) * bandRate * density(t) * (relaxed ? release.rate : 1) * (bossFight ? (tl.bossAmbientRate ?? 0.5) : 1);
        spawnAcc += Math.min(1 / spawnGap, perSecond);
        while (spawnAcc >= 1) { spawnAcc -= 1; spawnEnemy(pickBandEnemy(band), t, { eliteRoll: true, fallbackId: mainOf(band) }); }
        // The floor: while fewer are alive, one per spawn gap, at most maxTopUp per top-up interval.
        if (flow.enabled !== false) {
          const floor = Math.min(ambientCap, Math.round(share * (relaxed ? release.floor : 1) * (bossFight ? release.bossFightFloor : 1) * bandRate * density(t) * ambientCap));
          const perSec = Math.min(1 / spawnGap, (wave.maxTopUp ?? 4) / Math.max(0.05, wave.topUpInterval ?? 1));
          if (alive() < floor) { topUpAcc += perSec; while (topUpAcc >= 1 && alive() < floor) { topUpAcc -= 1; spawnEnemy(pickBandEnemy(band), t, { eliteRoll: true, fallbackId: mainOf(band) }); } }
          topUpAcc = Math.min(topUpAcc, 1);
          // Hordes: held during a release window, skipped during the boss fight or when 30 s late.
          for (const h of flow.hordes) {
            if (hordesDone.has(h.id) || t < h.t) continue;
            if (bossFight || t - h.t > 30) { hordesDone.add(h.id); continue; }
            if (t < releaseUntil) continue;
            hordesDone.add(h.id);
            let n = 0; for (let i = 0; i < Math.round(h.count); i++) if (spawnEnemy(h.enemy, t, { hpMult: (h.hpMult ?? 1), dmgMult: h.dmgMult ?? 1, cap: Math.min(tl.cap, ambientCap + Math.floor(tl.reserved / 2)) })) n++;
            if (n > 0 && release.afterHorde > 0) pendingReleases.push([t + release.hordeDelay, release.afterHorde]);
          }
        }
      }
    } else {
      // Overtime: the Drowned Pursuit (OvertimePhase).
      const o = tl.overtime, ot = t - tl.deadline;
      const interval = Math.max(o.floor, o.interval * Math.pow(o.decay, Math.floor(ot / o.step)));
      overtimeAcc += (params.spawnMult || 1) / interval;
      const ramp = (o.rampStart ?? 1) + ((o.rampEnd ?? 1) - (o.rampStart ?? 1)) * Math.min(1, o.rampSeconds > 0 ? ot / o.rampSeconds : 1);
      const hpm = (C.overtimeHpBase ?? 3) * Math.pow(2, ot / 60 * (C.overtimeHpExponent ?? 0.25)) * ramp, dm = (1 + ot / 60 * (C.overtimeDamagePerMinute ?? 0.12)) * ramp;
      while (overtimeAcc >= 1) { overtimeAcc -= 1; spawnEnemy(r() < (ot < 120 ? (o.pursuerFirst ?? 0.8) : (o.pursuerAfter ?? 0.6)) ? 'drowned_pursuer' : 'drowned_harrier', t, { hpMult: hpm, dmgMult: dm, cap: tl.cap }); }
    }
    // Minibosses and scheduled elites (time-to-kill floors on the build's theoretical DPS at spawn).
    for (const f of tl.focus) {
      if (focusDone.has(f.id) || t < f.t || !E[f.enemy] || bossFight) continue;
      if (f.waitForOtherArena && arenaActive()) continue;
      focusDone.add(f.id);
      const e = E[f.enemy], hp = Math.max(e.hp * hpScale(t) * f.hpMult, gameDps() * (f.arena ? D.ttk.arena : D.ttk.focus));
      focus.push({ id: f.id, phase: f.id, kind: f.arena ? 'arena' : 'focus', hp, maxHp: hp, dmg: e.dmg * (f.dmgMult ?? 1), xp: e.xp, start: t });
      if (f.arena && arenaSince < 0) arenaSince = t;
    }
    if (!bossSpawned && bossAt != null && t >= bossAt) {
      const id = (tl.boss && tl.boss.enemy) || 'barnacle_king', e = E[id];
      if (e) {
        bossSpawned = true;
        const asIf = Math.max(t, D.bossHpAsIfAtSeconds || 0);
        const hp = Math.max(e.hp * hpScale(asIf), gameDps() * D.ttk.boss);
        focus.push({ id, kind: 'boss', hp, maxHp: hp, dmg: e.dmg, xp: e.xp, start: t });
      }
    }

    // Player damage: shares of the single-target damage go to the focus targets; the rest, splash, pierce, orbits and
    // auras hit the crowd.
    const killsBefore = kills;
    const barrelShare = () => M.lootShare * Math.min(1, M.barrelsPerMinute / Math.max(1, killWindowSum));
    const n = groups.reduce((s, g) => s + (g.ready <= t ? g.n : 0), 0), splash = Math.min(1, n / M.crowdForFullSplash);
    const share = f => f.kind === 'arena' ? M.arenaShareOfDps : f.kind === 'boss' ? M.bossShareOfDps : f.kind === 'focus' ? M.focusShareOfDps : M.eliteShareOfDps;
    let toFocus = focus.reduce((s, f) => s + share(f), 0); const focusScale = toFocus > 0.95 ? 0.95 / toFocus : 1; toFocus = Math.min(0.95, toFocus);
    let crowdDamage = 0, hitWeighted = 0, singleSum = 0;
    for (const w of weapons) {
      const x = weaponNumbers(w);
      crowdDamage += x.single * M.accuracy * ((1 - toFocus) + (x.targets - 1) * splash);
      hitWeighted += x.single * x.perHit; singleSum += x.single;
    }
    const single = singleSum;
    for (let i = focus.length - 1; i >= 0; i--) {
      const f = focus[i]; f.hp -= single * M.accuracy * share(f) * focusScale * stats[S.EliteDamage];
      if (f.hp > 0) continue;
      focus.splice(i, 1); kills++; minuteKills++; bandKills++;
      xpCarry += (xpPaid[Math.min(400, Math.round(f.xp))] || 1) * barrelShare() * stats[S.XPGain] * xpPace;
      focusLog.push([f.id, f.start, t - f.start, Math.round(f.maxHp), f.kind]);
      if (f.kind === 'boss') { bossDone = true; bossKill = t; if (release.afterBoss > 0) releaseUntil = Math.max(releaseUntil, t + release.afterBoss); }
      if (f.kind === 'arena') { if (!arenaActive()) { arenaSince = -1; if (release.afterArena > 0) releaseUntil = Math.max(releaseUntil, t + release.afterArena); } }
    }
    const perHit = singleSum > 0 ? hitWeighted / singleSum : 1; // a typical hit (damage-weighted over the weapons)
    // Crowd: damage spread over the engaged enemies by count; a kill costs its HP rounded up to whole hits (overkill).
    const engagedTotal = n * M.engagement;
    if (engagedTotal > 0 && crowdDamage > 0) {
      for (const g of groups) {
        if (g.ready > t) continue;
        const eng = g.n * M.engagement, part = crowdDamage * eng / engagedTotal;
        const cost = Math.max(1, Math.ceil(g.hp / Math.max(0.01, perHit))) * Math.max(0.01, perHit);
        const k = Math.min(eng, part / cost), killed = Math.min(g.n, Math.floor(k) + (r() < k - Math.floor(k) ? 1 : 0));
        if (!killed) continue;
        g.n -= killed; kills += killed; minuteKills += killed; bandKills += killed;
        killsBy[g.id] = (killsBy[g.id] ?? 0) + killed;
        xpCarry += killed * (xpPaid[Math.min(400, Math.round(g.xp))] || 1) * barrelShare() * stats[S.XPGain] * xpPace;
        // Timed health drops (Survivor.io): the first kill after a random wait drops a health barrel.
        if (PK.healMax > 0) {
          if (healAt < 0) healAt = lastHeal + PK.healMin + r() * (PK.healMax - PK.healMin);
          const due = hull / maxHull < (PK.lowHull ?? 0.35) ? lastHeal + (healAt - lastHeal) * (PK.lowWait ?? 1) : healAt;
          if (t >= due) {
            lastHeal = t; healAt = t + PK.healMin + r() * (PK.healMax - PK.healMin);
            if (r() < M.lootShare) { const heal = Math.min(maxHull - hull, PK.healFraction > 0 ? Math.max(1, Math.round(PK.healFraction * maxHull)) : PK.healValue); hull += heal; healed += heal; }
          }
        }
      }
      for (let i = groups.length - 1; i >= 0; i--) if (groups[i].n <= 0) groups.splice(i, 1);
    }
    const slot = t % 60; killWindowSum += (kills - killsBefore) - killWindow[slot]; killWindow[slot] = kills - killsBefore;
    const whole = Math.floor(xpCarry); xp += whole; xpCarry -= whole;
    while (xp >= xpToNext(level)) { xp -= xpToNext(level); levelUp(); }

    // Incoming damage.
    const dodge = Math.min(0.95, M.dodge * (0.85 + 0.15 * stats[S.MoveSpeed]));
    let melee = 0, hits = 0;
    for (const g of groups) {
      if (g.ready > t) continue;
      if (g.ranged) { const d = g.n * M.engagement / M.shotInterval * M.shotHitChance * g.dmg; hits += d; damageBy[g.id] = (damageBy[g.id] ?? 0) + d; }
      else melee += g.n * M.engagement;
    }
    if (melee > 0) {
      const contacts = M.contactRate * Math.pow(melee, 0.6) * (1 - dodge);
      for (const g of groups) if (!g.ranged && g.ready <= t) { const d = contacts * (g.n * M.engagement / melee) * g.dmg; hits += d; damageBy[g.id] = (damageBy[g.id] ?? 0) + d; }
    }
    for (const f of focus) {
      let d;
      if (f.kind === 'elite') { const e = E[f.id.slice(9)] || {}; d = e.ranged ? M.engagement / M.shotInterval * M.shotHitChance * f.dmg : M.contactRate * (1 - dodge) * M.engagement * f.dmg; }
      else d = f.dmg * (f.kind === 'boss' ? M.bossHitsPerSecond : M.focusHitsPerSecond) * (1 - dodge * 0.6);
      hits += d; const k = f.id.startsWith('adaptive:') ? f.id.slice(9) : (tl.focus.find(x => x.id === f.id) || {}).enemy || f.id; damageBy[k] = (damageBy[k] ?? 0) + d; }
    const armor = data.armorWorks ? Math.max(0, stats[S.Armor] || 0) : 0, armorFactor = 20 / (20 + armor);
    let taken = hits * dmgScale(t) * (params.enemyDamageMult || 1) * armorFactor;
    // Island impacts (V5 M8z: islandImpactDamage hull, at most one per islandDamageCooldown s), at a fitted rate.
    const islandHit = (data.run?.islandImpactDamage ?? 0) * armorFactor;
    if (islandHit > 0 && t >= islandReadyAt && r() < M.islandHitsPerMinute / 60) { taken += islandHit; islandDamage += islandHit; damageBy.island = (damageBy.island ?? 0) + islandHit / armorFactor; islandReadyAt = t + (data.run.islandDamageCooldown ?? 0); }
    hull -= taken; damageTaken += taken; minuteDamage += taken;
    hull = Math.min(maxHull, hull + (stats[S.HullRegen] || 0));
    if (t >= chestAt) {
      chestAt = t + M.chestEverySeconds;
      const pool = Object.values(data.items).filter(i => i.inChest);
      const want = weapons.filter(w => !w.evo && w.level >= P.evolutionLevel).map(w => Object.values(data.evolutions).find(e => e.weapon === w.id)).filter(e => e && !items.includes(e.item));
      const item = want.length && r() < (data.stage?.evolutionItemChance ?? 0.4) ? want[0].item : pickW(r, pool, i => (P.rarityWeights || [60, 26, 10, 3.2, 0.8])[i.rarity] ?? 1).id;
      items.push(item);
      for (const [stat, add, mul] of data.items[item].mods) { if (stat === S.Hull) { maxHull += add; hull += add; } else statAdd(stat, add, mul); }
    }
    minHull = Math.min(minHull, hull / maxHull);
    if ((t + 1) % 60 === 0 || hull <= 0) {
      minutes.push({ m: Math.round((t + 1) / 60), kills: minuteKills, level, dps: Math.round(singleDps() * 10) / 10, gameDps: Math.round(gameDps()), alive: alive(), hull: Math.max(0, hull / maxHull), dmg: Math.round(minuteDamage), weapons: weapons.length });
      minuteKills = 0; minuteDamage = 0;
    }
    if (hull <= 0) { died = true; return finish(t); }
  }
  return finish(T);

  function finish(t) {
    return { captain: cap.id, end: t / 60, died, bossKill: bossKill >= 0 ? bossKill / 60 : null, kills, level, dps: singleDps(), gameDps: gameDps(), damageTaken, healed, islandDamage, minHull,
      weapons: weapons.map(w => w.id), evolved, logbooks: Object.keys(logbooks), items, minutes, focus: focusLog, damageBy, killsBy, spawns, levelAt };
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
    byMinute.push({ m, alive: rows.length / runs.length, kills: med(rows.map(x => x.kills)), level: med(rows.map(x => x.level)), dps: med(rows.map(x => x.dps)), gameDps: med(rows.map(x => x.gameDps)), enemies: med(rows.map(x => x.alive)), hull: med(rows.map(x => x.hull)), dmg: med(rows.map(x => x.dmg)) });
  }
  const tension = runs.map(r => { const live = r.minutes.filter(x => x.hull > 0); return live.length ? live.filter(x => x.hull < 0.5).length / live.length : 0; });
  return {
    runs: runs.length, endMedian: med(runs.map(r => r.end)), endP10: pct(runs.map(r => r.end), 0.1), endP90: pct(runs.map(r => r.end), 0.9),
    bossWin: runs.filter(r => r.bossKill != null).length / runs.length, deathBefore10: runs.filter(r => r.died && r.end < 10).length / runs.length,
    deathBefore14: runs.filter(r => r.died && r.end < 14).length / runs.length, survive20: runs.filter(r => !r.died).length / runs.length,
    tension: tension.reduce((a, b) => a + b, 0) / Math.max(1, tension.length), minHull: med(runs.map(r => r.minHull)), byMinute,
  };
}
