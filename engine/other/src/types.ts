export const WORLD_SIZE = 200;
export const ARENA_RADIUS = 90;

export type GamePhase = 'menu' | 'playing' | 'draft' | 'gameover';

export type Element = 'neutral' | 'fire' | 'water' | 'air' | 'earth' | 'lightning';

export type ArcFamily = 'broadside' | 'chaser' | 'deck' | 'trailing' | 'orbital';

export type EnemyArchetype = 'swarmer' | 'charger' | 'ranged' | 'orbiter' | 'bruiser' | 'elite';

export interface Vec2 {
  x: number;
  z: number;
}

export interface HullDef {
  id: string;
  name: string;
  hp: number;
  speed: number;
  turnRate: number;
  broadsideArc: number;
  color: number;
  accentColor: number;
}

export interface WeaponDef {
  id: string;
  name: string;
  family: ArcFamily;
  element: Element;
  baseDamage: number;
  fireInterval: number;
  projectileCount: number;
  pierce: number;
  projectileSpeed: number;
  range: number;
  knockback: number;
  description: string;
}

export interface PassiveDef {
  id: string;
  name: string;
  description: string;
  apply: (stats: PlayerStats) => void;
}

export interface PlayerStats {
  maxHp: number;
  hp: number;
  speed: number;
  turnRate: number;
  broadsideArc: number;
  damageMultiplier: number;
  fireRateMultiplier: number;
  projectileCountBonus: number;
  rangeMultiplier: number;
  arcWidthBonus: number;
  pickupRadius: number;
  hpRegen: number;
  rarityWeight: number;
}

export interface OwnedWeapon {
  def: WeaponDef;
  level: number;
  cooldown: number;
}

export interface OwnedPassive {
  def: PassiveDef;
  level: number;
}

export interface DraftCard {
  id: string;
  name: string;
  description: string;
  rarity: 'common' | 'uncommon' | 'rare' | 'legendary';
  type: 'weapon' | 'passive' | 'upgrade';
  weaponId?: string;
  passiveId?: string;
  targetWeaponId?: string;
  targetPassiveId?: string;
}

export interface TideConfig {
  level: number;
  duration: number;
  enemyHpMult: number;
  enemyDensityMult: number;
  eliteRate: number;
  rewardMult: number;
  biome: string;
  biomeColor: number;
}

export const HULLS: Record<string, HullDef> = {
  skiff: { id: 'skiff', name: 'Skiff', hp: 60, speed: 13, turnRate: 4.8, broadsideArc: 70, color: 0x8b6914, accentColor: 0xf0e6d2 },
  sloop: { id: 'sloop', name: 'Sloop', hp: 100, speed: 10, turnRate: 3.0, broadsideArc: 90, color: 0x5c3a1e, accentColor: 0xd4a850 },
  frigate: { id: 'frigate', name: 'Frigate', hp: 130, speed: 8.5, turnRate: 2.25, broadsideArc: 110, color: 0x3a2a1a, accentColor: 0xc0a060 },
  galleon: { id: 'galleon', name: 'Galleon', hp: 180, speed: 7, turnRate: 1.65, broadsideArc: 120, color: 0x2a1a0a, accentColor: 0xb09050 },
  marauder: { id: 'marauder', name: 'Marauder', hp: 75, speed: 11.5, turnRate: 3.6, broadsideArc: 60, color: 0x4a1010, accentColor: 0xff6040 },
};

export const WEAPONS: Record<string, WeaponDef> = {
  iron_cannons: {
    id: 'iron_cannons', name: 'Iron Cannons', family: 'broadside', element: 'neutral',
    baseDamage: 12, fireInterval: 1.2, projectileCount: 2, pierce: 0,
    projectileSpeed: 35, range: 28, knockback: 2,
    description: 'Standard port and starboard battery.',
  },
  long_nines: {
    id: 'long_nines', name: 'Long Nines', family: 'broadside', element: 'neutral',
    baseDamage: 18, fireInterval: 1.8, projectileCount: 1, pierce: 1,
    projectileSpeed: 45, range: 40, knockback: 3,
    description: 'Long-range piercing broadside.',
  },
  carronades: {
    id: 'carronades', name: 'Carronades', family: 'broadside', element: 'fire',
    baseDamage: 8, fireInterval: 0.7, projectileCount: 4, pierce: 0,
    projectileSpeed: 25, range: 18, knockback: 4,
    description: 'Short-range scatter broadside.',
  },
  musket_shot: {
    id: 'musket_shot', name: 'Musket Shot', family: 'chaser', element: 'neutral',
    baseDamage: 6, fireInterval: 0.5, projectileCount: 1, pierce: 0,
    projectileSpeed: 50, range: 35, knockback: 1,
    description: 'Bow and stern chaser covering blind spots.',
  },
  bombardment: {
    id: 'bombardment', name: 'Bombardment', family: 'deck', element: 'fire',
    baseDamage: 5, fireInterval: 0.4, projectileCount: 1, pierce: 0,
    projectileSpeed: 20, range: 12, knockback: 2,
    description: '360° deck mortar, short range.',
  },
  iron_fangs: {
    id: 'iron_fangs', name: 'Iron Fangs', family: 'orbital', element: 'neutral',
    baseDamage: 4, fireInterval: 0.3, projectileCount: 3, pierce: 0,
    projectileSpeed: 0, range: 4, knockback: 1,
    description: 'Orbiting spikes deal contact damage.',
  },
  scatter_shot: {
    id: 'scatter_shot', name: 'Scatter Shot', family: 'broadside', element: 'neutral',
    baseDamage: 5, fireInterval: 0.9, projectileCount: 6, pierce: 0,
    projectileSpeed: 22, range: 16, knockback: 2,
    description: 'Wide shotgun broadside.',
  },
};

export const PASSIVES: Record<string, PassiveDef> = {
  powder_charge: {
    id: 'powder_charge', name: 'Powder Charge',
    description: '+15% damage per level',
    apply: (s) => { s.damageMultiplier += 0.15; },
  },
  ramrod: {
    id: 'ramrod', name: 'Ramrod',
    description: '+12% fire rate per level',
    apply: (s) => { s.fireRateMultiplier += 0.12; },
  },
  rigging: {
    id: 'rigging', name: 'Rigging',
    description: '+15% turn rate per level',
    apply: (s) => { s.turnRate *= 1.15; },
  },
  sailcloth: {
    id: 'sailcloth', name: 'Sailcloth',
    description: '+10% speed per level',
    apply: (s) => { s.speed *= 1.1; },
  },
  hull_plating: {
    id: 'hull_plating', name: 'Hull Plating',
    description: '+20 max HP per level',
    apply: (s) => { s.maxHp += 20; s.hp += 20; },
  },
  spyglass: {
    id: 'spyglass', name: 'Spyglass',
    description: '+10% range and arc width per level',
    apply: (s) => { s.rangeMultiplier += 0.1; s.arcWidthBonus += 8; },
  },
  lodestone: {
    id: 'lodestone', name: 'Lodestone',
    description: '+25% pickup radius per level',
    apply: (s) => { s.pickupRadius *= 1.25; },
  },
  ships_cook: {
    id: 'ships_cook', name: "Ship's Cook",
    description: '+1 HP/s regeneration per level',
    apply: (s) => { s.hpRegen += 1; },
  },
};

export const TIDES: TideConfig[] = [
  { level: 1, duration: 480, enemyHpMult: 1.0, enemyDensityMult: 1.0, eliteRate: 0.03, rewardMult: 1.0, biome: 'Shallows', biomeColor: 0xc2a86a },
  { level: 2, duration: 600, enemyHpMult: 1.4, enemyDensityMult: 1.2, eliteRate: 0.05, rewardMult: 1.4, biome: 'Shallows', biomeColor: 0xb89a5a },
  { level: 3, duration: 720, enemyHpMult: 1.9, enemyDensityMult: 1.45, eliteRate: 0.07, rewardMult: 1.9, biome: 'Reef', biomeColor: 0x6a8a7a },
  { level: 4, duration: 900, enemyHpMult: 2.6, enemyDensityMult: 1.75, eliteRate: 0.10, rewardMult: 2.6, biome: 'Reef', biomeColor: 0x5a7a6a },
];

export const ELEMENT_AFFINITY: Record<Element, { strong: Element; weak: Element }> = {
  neutral: { strong: 'neutral', weak: 'neutral' },
  water: { strong: 'fire', weak: 'lightning' },
  fire: { strong: 'air', weak: 'water' },
  air: { strong: 'earth', weak: 'fire' },
  earth: { strong: 'lightning', weak: 'air' },
  lightning: { strong: 'water', weak: 'earth' },
};

export function getElementMultiplier(attack: Element, defense: Element): number {
  if (attack === 'neutral' || defense === 'neutral') return 1.0;
  const aff = ELEMENT_AFFINITY[attack];
  if (aff.strong === defense) return 1.5;
  if (aff.weak === defense) return 0.65;
  return 1.0;
}

export function xpForLevel(level: number): number {
  return Math.floor(30 * Math.pow(level, 1.35));
}

export function clamp(v: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, v));
}

export function dist2(a: Vec2, b: Vec2): number {
  const dx = a.x - b.x;
  const dz = a.z - b.z;
  return Math.sqrt(dx * dx + dz * dz);
}

export function angleDiff(a: number, b: number): number {
  let d = a - b;
  while (d > Math.PI) d -= Math.PI * 2;
  while (d < -Math.PI) d += Math.PI * 2;
  return d;
}

export function lerpAngle(a: number, b: number, t: number): number {
  return a + angleDiff(b, a) * t;
}

export function randomInCircle(radius: number): Vec2 {
  const angle = Math.random() * Math.PI * 2;
  const r = Math.sqrt(Math.random()) * radius;
  return { x: Math.cos(angle) * r, z: Math.sin(angle) * r };
}

export function createBaseStats(hull: HullDef): PlayerStats {
  return {
    maxHp: hull.hp,
    hp: hull.hp,
    speed: hull.speed,
    turnRate: hull.turnRate,
    broadsideArc: hull.broadsideArc,
    damageMultiplier: 1,
    fireRateMultiplier: 1,
    projectileCountBonus: 0,
    rangeMultiplier: 1,
    arcWidthBonus: 0,
    pickupRadius: 4,
    hpRegen: 0,
    rarityWeight: 1,
  };
}

export function isInArc(
  shipPos: Vec2,
  shipHeading: number,
  targetPos: Vec2,
  arcCenter: number,
  arcWidth: number,
  range: number,
): boolean {
  const dx = targetPos.x - shipPos.x;
  const dz = targetPos.z - shipPos.z;
  const dist = Math.sqrt(dx * dx + dz * dz);
  if (dist > range) return false;
  const angleToTarget = Math.atan2(dx, dz);
  const diff = Math.abs(angleDiff(angleToTarget, shipHeading + arcCenter));
  return diff <= (arcWidth * Math.PI / 180) / 2;
}

export function findNearestEnemyInArc(
  shipPos: Vec2,
  shipHeading: number,
  enemies: { position: Vec2; alive: boolean }[],
  arcCenter: number,
  arcWidth: number,
  range: number,
): { position: Vec2 } | null {
  let nearest: { position: Vec2 } | null = null;
  let nearestDist = Infinity;
  for (const e of enemies) {
    if (!e.alive) continue;
    if (!isInArc(shipPos, shipHeading, e.position, arcCenter, arcWidth, range)) continue;
    const d = dist2(shipPos, e.position);
    if (d < nearestDist) {
      nearestDist = d;
      nearest = e;
    }
  }
  return nearest;
}
