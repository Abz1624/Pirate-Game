"""Exports the Saltwind numbers the Monte Carlo model needs straight from the Unity assets (no Editor).

Playtest round 5 (3 Oct 2026). Written fresh for the numbers-only simulation (sim.mjs); it does not use the V5
simulator (DevTools/Balance). Output: data.json next to this file.
"""
import glob, json, os, re, struct
import yaml

ROOT = os.environ.get('RP_GAME_ROOT') or os.path.join(os.path.dirname(__file__), '..', '..', '..', 'Pirate Game Unity', 'Assets', '_Game')
V4 = os.path.join(ROOT, 'V4', 'Data')


def load(path):
    text = open(path, encoding='utf-8').read()
    text = '\n'.join(l for l in text.splitlines() if not l.startswith('%'))
    text = re.sub(r'^--- !u!\d+ &\d+.*$', '---', text, flags=re.M)
    docs = [d for d in yaml.safe_load_all(text) if d]
    return docs[0]['MonoBehaviour']


def guid(path):
    m = re.search(r'guid: ([0-9a-f]+)', open(path + '.meta', encoding='utf-8').read())
    return m.group(1)


def raw_hex(path, field):
    m = re.search(r'^\s*' + field + r': ([0-9a-fA-F]*)\s*$', open(path, encoding='utf-8').read(), flags=re.M)
    return m.group(1) if m else ''


def ints(hexstr):
    b = bytes.fromhex(hexstr)
    return list(struct.unpack('<%di' % (len(b) // 4), b))


def ref(r):
    return r.get('guid') if isinstance(r, dict) else None


by_guid = {}
enemies = {}
for p in glob.glob(os.path.join(V4, 'Enemies', '*.asset')):
    a = load(p)
    eid = os.path.basename(p)[:-6]
    s = a['stats']
    enemies[eid] = dict(id=eid, name=a.get('displayName', eid), hp=float(s['baseHP']), dmg=float(s['baseDamage']),
                        speed=float(s['moveSpeed']), xp=float(a.get('rewards', {}).get('xpValue', 1)),
                        boss=bool(a.get('isBoss', 0)), miniboss=bool(a.get('isMiniboss', 0)))
    by_guid[guid(p)] = eid

for p in glob.glob(os.path.join(V4, 'Variants', '*.asset')):
    a = load(p)
    vid = os.path.basename(p)[:-6]
    base = by_guid.get(ref(a.get('baseArchetype')))
    b = enemies.get(base, {})
    hp = a['baseHP']['value'] if a['baseHP']['enabled'] else b.get('hp', 1)
    dmg = a['baseDamage']['value'] if a['baseDamage']['enabled'] else b.get('dmg', 1)
    spd = a['moveSpeed']['value'] if a['moveSpeed']['enabled'] else b.get('speed', 4)
    enemies[vid] = dict(id=vid, name=a.get('displayName', vid), hp=float(hp), dmg=float(dmg), speed=float(spd), xp=b.get('xp', 1),
                        boss=False, miniboss=False, base=base, ranged=False)
    by_guid[guid(p)] = vid

RANGED = {'cannonboat', 'gunner_cog', 'needle_sloop', 'net_cutter', 'shield_frigate', 'drowned_harrier'}
for e in enemies.values():
    e['ranged'] = e['id'] in RANGED

# Spawners: the elite multipliers of the scheduled elites.
spawners = {}
for p in glob.glob(os.path.join(V4, 'Spawners', '*.asset')):
    a = load(p)
    el = a.get('elite', {})
    spawners[guid(p)] = dict(id=os.path.basename(p)[:-6], anchor=by_guid.get(ref(a.get('anchor'))),
                             eliteHp=float(el.get('hpMultiplier', 1)) if el.get('isElite') else 1.0, elite=bool(el.get('isElite', 0)))

t = load(os.path.join(V4, 'Timelines', 'SaltwindTimeline.asset'))
bands, focus, pulses = [], [], []
for ph in t['phases']:
    pid = ph['phaseId']
    if pid.startswith('band_'):
        pools = []
        for pool in ph['pools']:
            entries = []
            for e in pool['entries']:
                eid = by_guid.get(ref(e.get('variant'))) or by_guid.get(ref(e.get('archetype'))) or e['enemyId']
                entries.append([eid, float(e['weight'])])
            pools.append(dict(share=float(pool['share']), entries=entries))
        bands.append(dict(t=float(ph['trigger']['timeSeconds']), rate=float(ph['ratePerSecond']), pools=pools))
    elif ph['focus']['enabled']:
        f = ph['focus']
        sp = spawners.get(ref(ph.get('spawner')), {})
        eid = by_guid.get(ref(f.get('variant'))) or by_guid.get(ref(f.get('archetype'))) or sp.get('anchor')
        focus.append(dict(id=pid, t=float(ph['trigger']['timeSeconds']), enemy=eid, hpMult=float(f['hpMultiplier']) * sp.get('eliteHp', 1.0),
                          arena=bool(ph['pauseAmbient']), elite=sp.get('elite', False)))
    elif pid.startswith('swarm_pulse'):
        pulses.append(dict(t=float(ph['trigger']['timeSeconds']), seconds=float(ph['duration']['seconds']), mult=float(ph['rateMultiplier'])))
bands.sort(key=lambda b: b['t'])
flow = t['flow']
timeline = dict(deadline=float(t['stageDeadlineSeconds']), cap=int(t['maxActiveEnemies']), reserved=int(t['reservedEncounterSlots']),
                bands=bands, focus=focus, pulses=pulses,
                waves=[[float(w['startSeconds']), float(w['minAliveShare']), float(w['rateMultiplier'])] for w in flow['waves']],
                hordes=[[float(h['timeSeconds']), by_guid.get(ref(h['archetype'])), int(h['count'])] for h in flow['hordes']],
                overtime=dict(interval=float(t['overtime']['initialSpawnIntervalSeconds']), floor=float(t['overtime']['intervalFloorSeconds']),
                              decay=float(t['overtime']['intervalDecayPerStep']), step=float(t['overtime']['pressureStepSeconds'])),
                tagCaps={c['tag']: int(c['max']) for c in t['tagCaps']})

weapons = {}
for p in glob.glob(os.path.join(V4, 'Weapons', '*.asset')):
    a = load(p)
    if 'weaponId' not in a: continue  # WeaponRules
    wid = a['weaponId']
    weapons[wid] = dict(id=wid, name=a['displayName'], damage=float(a['baseDamage']), cooldown=float(a['baseCooldown']),
                        quantity=int(a['baseQuantity']), behavior=int(a['behavior']), maxLevel=int(a['maxLevel']),
                        pierce=int(a['projectile'].get('pierce', 0)), lane=bool(a['lane'].get('enabled', 0)),
                        axes=[dict(axis=int(x['axis']), inc=float(x['baseIncrement']), weight=float(x['offerWeight']), cap=float(x['maxAccumulated']))
                              for x in (a.get('upgradeAxes') or [])])

PROG = os.path.join(V4, 'Build', 'Progression_Saltwind.asset')
prog = load(PROG)
pick = load(os.path.join(V4, 'Build', 'Pickups_Saltwind.asset'))
logbooks = {}
for p in glob.glob(os.path.join(V4, 'Build', 'Logbooks', '*.asset')):
    a = load(p)
    logbooks[a['logbookId']] = dict(id=a['logbookId'], name=a['displayName'], stat=int(a['stat']), add=float(a['perLevelAdditive']), max=int(a['maxLevel']))
items = {}
for p in glob.glob(os.path.join(V4, 'Build', 'Items', '*.asset')):
    a = load(p)
    items[a['itemId']] = dict(id=a['itemId'], name=a['displayName'], rarity=int(a['rarity']), inChest=bool(a.get('inChestPool', 1)),
                              mods=[[int(m['stat']), float(m.get('additive', 0)), float(m.get('multiplicative', 1))] for m in (a.get('statModifiers') or [])])
evolutions = {}
for p in glob.glob(os.path.join(V4, 'Evolutions', '*.asset')):
    a = load(p)
    base = [w for w in weapons.values() if guid(os.path.join(V4, 'Weapons', w['id'] + '.asset')) == ref(a.get('baseWeapon'))]
    evolutions[a['evolutionId']] = dict(id=a['evolutionId'], name=a['displayName'], weapon=base[0]['id'] if base else None, item=a['requiredItemId'])
captains = {}
for p in glob.glob(os.path.join(V4, 'Build', 'Captains', '*.asset')):
    a = load(p)
    captains[a['captainKey']] = dict(id=a['captainKey'], name=a.get('displayName', a['captainKey']), weapon=a['startingWeaponId'], hull=float(a['baseHull']))
curve = load(os.path.join(V4, 'Difficulty', 'SaltwindDifficulty.asset'))
director = load(os.path.join(V4, 'Difficulty', 'SaltwindDirector.asset'))

data = dict(enemies=enemies, timeline=timeline, weapons=weapons, logbooks=logbooks, items=items, evolutions=evolutions, captains=captains,
            progression=dict(xpTable=ints(raw_hex(PROG, 'xpToNextTable')), weaponSlots=int(prog['weaponSlots']), logbookSlots=int(prog['logbookSlots']),
                             levelsPerRarity=ints(raw_hex(PROG, 'levelsPerRarity')), rarityMult=[float(x) for x in prog['rarityMultipliers']],
                             evolutionLevel=int(prog['evolutionUnlockLevel']), maxWeaponLevel=int(prog['maxWeaponLevel'])),
            pickups=dict(xpMult=float(pick['xpPickupMultiplier']), healValue=float(pick['healthDropValue']),
                         healMin=float(pick.get('healthDropIntervalMin', 0)), healMax=float(pick.get('healthDropIntervalMax', 0))),
            curve=dict(hp=[float(curve['enemyHpPerMinute'][k]) for k in ('c0', 'c1', 'c2')], dmg=[float(curve['enemyDamagePerMinute'][k]) for k in ('c0', 'c1', 'c2')],
                       global_hp=float(curve['globalHpMultiplier'])),
            director=dict(killBand=bool(director.get('killBandLever', 0)), bandSeconds=float(director.get('killBandSeconds', 30)),
                          high=float(director.get('killBandHighRatio', 0.85)), low=float(director.get('killBandLowRatio', 0.3)),
                          lowRate=float(director.get('killBandLowRateMultiplier', 0.6)), eliteChance=float(director.get('killBandEliteChance', 0.06)),
                          eliteHp=float(director.get('eliteHpMultiplier', 8)),
                          ttk=dict(boss=float(director.get('bossMinSeconds', 0)), arena=float(director.get('arenaFocusMinSeconds', 0)),
                                   focus=float(director.get('focusMinSeconds', 0)), elite=float(director.get('adaptiveEliteMinSeconds', 0)))),
            starterWeapons=list(load(os.path.join(V4, 'Build', 'Unlocks_Saltwind.asset')).get('starterWeaponIds') or []))
out = os.environ.get('RP_OUT') or os.path.join(os.path.dirname(__file__), 'data.json')
json.dump(data, open(out, 'w', encoding='utf-8'), indent=1)
print('wrote', out, len(enemies), 'enemies', len(weapons), 'weapons', len(bands), 'bands', len(focus), 'focus', len(timeline['hordes']), 'hordes')
