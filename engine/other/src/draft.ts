import {
  PASSIVES,
  WEAPONS,
  type DraftCard,
  type OwnedPassive,
  type OwnedWeapon,
} from './types';

const MAX_WEAPON_SLOTS = 6;
const MAX_PASSIVE_SLOTS = 6;
const MAX_WEAPON_LEVEL = 5;
const MAX_PASSIVE_LEVEL = 5;

function rollRarity(weight: number): DraftCard['rarity'] {
  const roll = Math.random() / weight;
  if (roll < 0.03) return 'legendary';
  if (roll < 0.15) return 'rare';
  if (roll < 0.40) return 'uncommon';
  return 'common';
}

export class DraftSystem {
  generateOffers(
    weapons: OwnedWeapon[],
    passives: OwnedPassive[],
    level: number,
    rarityWeight: number,
  ): DraftCard[] {
    const pool = this.buildPool(weapons, passives, level);
    const offers: DraftCard[] = [];
    const used = new Set<string>();

    while (offers.length < 3 && pool.length > 0) {
      const idx = Math.floor(Math.random() * pool.length);
      const card = pool[idx];
      if (used.has(card.id)) {
        pool.splice(idx, 1);
        continue;
      }
      card.rarity = rollRarity(rarityWeight);
      offers.push(card);
      used.add(card.id);
      pool.splice(idx, 1);
    }

    return offers;
  }

  private buildPool(
    weapons: OwnedWeapon[],
    passives: OwnedPassive[],
    level: number,
  ): DraftCard[] {
    const pool: DraftCard[] = [];

    if (level === 1) {
      for (const [id, def] of Object.entries(WEAPONS)) {
        if (id === 'iron_cannons') continue;
        if (weapons.length < MAX_WEAPON_SLOTS) {
          pool.push({
            id: `weapon_${id}`,
            name: def.name,
            description: def.description,
            rarity: 'common',
            type: 'weapon',
            weaponId: id,
          });
        }
      }
    }

    for (const [id, def] of Object.entries(WEAPONS)) {
      const owned = weapons.find((w) => w.def.id === id);
      if (owned && owned.level < MAX_WEAPON_LEVEL) {
        pool.push({
          id: `upgrade_weapon_${id}`,
          name: `${def.name} +1`,
          description: `Level up ${def.name} to level ${owned.level + 1}`,
          rarity: 'common',
          type: 'upgrade',
          targetWeaponId: id,
        });
      } else if (!owned && weapons.length < MAX_WEAPON_SLOTS) {
        pool.push({
          id: `weapon_${id}`,
          name: def.name,
          description: def.description,
          rarity: 'common',
          type: 'weapon',
          weaponId: id,
        });
      }
    }

    for (const [id, def] of Object.entries(PASSIVES)) {
      const owned = passives.find((p) => p.def.id === id);
      if (owned && owned.level < MAX_PASSIVE_LEVEL) {
        pool.push({
          id: `upgrade_passive_${id}`,
          name: `${def.name} +1`,
          description: def.description,
          rarity: 'common',
          type: 'upgrade',
          targetPassiveId: id,
        });
      } else if (!owned && passives.length < MAX_PASSIVE_SLOTS) {
        pool.push({
          id: `passive_${id}`,
          name: def.name,
          description: def.description,
          rarity: 'common',
          type: 'passive',
          passiveId: id,
        });
      }
    }

    return pool;
  }

  applyCard(
    card: DraftCard,
    weapons: OwnedWeapon[],
    passives: OwnedPassive[],
    onAddOrbital: () => void,
  ): void {
    if (card.type === 'weapon' && card.weaponId) {
      const def = WEAPONS[card.weaponId];
      weapons.push({ def, level: 1, cooldown: 0 });
      if (def.family === 'orbital') onAddOrbital();
    } else if (card.type === 'passive' && card.passiveId) {
      const def = PASSIVES[card.passiveId];
      passives.push({ def, level: 1 });
    } else if (card.type === 'upgrade' && card.targetWeaponId) {
      const w = weapons.find((w) => w.def.id === card.targetWeaponId);
      if (w) w.level = Math.min(MAX_WEAPON_LEVEL, w.level + 1);
    } else if (card.type === 'upgrade' && card.targetPassiveId) {
      const p = passives.find((p) => p.def.id === card.targetPassiveId);
      if (p) p.level = Math.min(MAX_PASSIVE_LEVEL, p.level + 1);
    }
  }
}

export function recalculateStats(
  stats: import('./types').PlayerStats,
  passives: OwnedPassive[],
  hullBase: { hp: number; speed: number; turnRate: number; broadsideArc: number },
): void {
  const hpRatio = stats.hp / stats.maxHp;
  stats.maxHp = hullBase.hp;
  stats.speed = hullBase.speed;
  stats.turnRate = hullBase.turnRate;
  stats.broadsideArc = hullBase.broadsideArc;
  stats.damageMultiplier = 1;
  stats.fireRateMultiplier = 1;
  stats.projectileCountBonus = 0;
  stats.rangeMultiplier = 1;
  stats.arcWidthBonus = 0;
  stats.pickupRadius = 4;
  stats.hpRegen = 0;
  stats.rarityWeight = 1;

  for (const p of passives) {
    for (let i = 0; i < p.level; i++) {
      p.def.apply(stats);
    }
  }
  stats.hp = Math.min(stats.maxHp, stats.maxHp * hpRatio);
}
