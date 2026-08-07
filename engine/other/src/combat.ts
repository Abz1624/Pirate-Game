import type { Enemy } from './enemies';
import type { ProjectileManager } from './projectiles';
import type { Ship } from './ship';
import {
  findNearestEnemyInArc,
  getElementMultiplier,
  dist2,
  type OwnedWeapon,
  type PlayerStats,
  type Vec2,
} from './types';

export class CombatSystem {
  fireWeapons(
    ship: Ship,
    weapons: OwnedWeapon[],
    enemies: Enemy[],
    projectiles: ProjectileManager,
    stats: PlayerStats,
  ): void {
    const aliveEnemies = enemies.filter((e) => e.alive);

    for (const weapon of weapons) {
      weapon.cooldown -= 1 / 60;
      if (weapon.cooldown > 0) continue;

      const def = weapon.def;
      const interval = def.fireInterval / stats.fireRateMultiplier;
      const damage = def.baseDamage * stats.damageMultiplier * (1 + (weapon.level - 1) * 0.2);
      const range = def.range * stats.rangeMultiplier;
      const count = def.projectileCount + stats.projectileCountBonus;

      switch (def.family) {
        case 'broadside':
          this.fireBroadside(ship, def.id, aliveEnemies, projectiles, stats, damage, range, count, interval, weapon);
          break;
        case 'chaser':
          this.fireChaser(ship, aliveEnemies, projectiles, def, damage, range, count, interval, weapon);
          break;
        case 'deck':
          this.fireDeck(ship, aliveEnemies, projectiles, def, damage, range, count, interval, weapon);
          break;
        case 'orbital':
          break;
      }
    }
  }

  private fireBroadside(
    ship: Ship,
    _weaponId: string,
    enemies: Enemy[],
    projectiles: ProjectileManager,
    stats: PlayerStats,
    damage: number,
    range: number,
    count: number,
    interval: number,
    weapon: OwnedWeapon,
  ): void {
    const arcWidth = stats.broadsideArc + stats.arcWidthBonus;
    const def = weapon.def;

    const portTarget = findNearestEnemyInArc(ship.position, ship.heading, enemies, -Math.PI / 2, arcWidth, range);
    const stbdTarget = findNearestEnemyInArc(ship.position, ship.heading, enemies, Math.PI / 2, arcWidth, range);

    let fired = false;

    if (portTarget) {
      fired = true;
      for (let i = 0; i < count; i++) {
        const spread = (i - (count - 1) / 2) * 0.08;
        const dir = { x: Math.sin(ship.heading - Math.PI / 2 + spread), z: Math.cos(ship.heading - Math.PI / 2 + spread) };
        projectiles.spawn(ship.position, dir, def.projectileSpeed, damage, def.element, def.pierce, def.knockback, range, def.family);
      }
    }

    if (stbdTarget) {
      fired = true;
      for (let i = 0; i < count; i++) {
        const spread = (i - (count - 1) / 2) * 0.08;
        const dir = { x: Math.sin(ship.heading + Math.PI / 2 + spread), z: Math.cos(ship.heading + Math.PI / 2 + spread) };
        projectiles.spawn(ship.position, dir, def.projectileSpeed, damage, def.element, def.pierce, def.knockback, range, def.family);
      }
    }

    if (fired) weapon.cooldown = interval;
  }

  private fireChaser(
    ship: Ship,
    enemies: Enemy[],
    projectiles: ProjectileManager,
    def: OwnedWeapon['def'],
    damage: number,
    range: number,
    count: number,
    interval: number,
    weapon: OwnedWeapon,
  ): void {
    const bowTarget = findNearestEnemyInArc(ship.position, ship.heading, enemies, 0, 40, range);
    const sternTarget = findNearestEnemyInArc(ship.position, ship.heading, enemies, Math.PI, 40, range);

    if (bowTarget) {
      const dir = { x: Math.sin(ship.heading), z: Math.cos(ship.heading) };
      for (let i = 0; i < count; i++) {
        projectiles.spawn(ship.position, dir, def.projectileSpeed, damage, def.element, def.pierce, def.knockback, range, def.family);
      }
      weapon.cooldown = interval;
    } else if (sternTarget) {
      const dir = { x: -Math.sin(ship.heading), z: -Math.cos(ship.heading) };
      projectiles.spawn(ship.position, dir, def.projectileSpeed, damage, def.element, def.pierce, def.knockback, range, def.family);
      weapon.cooldown = interval;
    }
  }

  private fireDeck(
    ship: Ship,
    enemies: Enemy[],
    projectiles: ProjectileManager,
    def: OwnedWeapon['def'],
    damage: number,
    range: number,
    count: number,
    interval: number,
    weapon: OwnedWeapon,
  ): void {
    let nearest: Enemy | null = null;
    let nearestDist = Infinity;
    for (const e of enemies) {
      const d = dist2(ship.position, e.position);
      if (d < range && d < nearestDist) {
        nearestDist = d;
        nearest = e;
      }
    }
    if (!nearest) return;

    const dx = nearest.position.x - ship.position.x;
    const dz = nearest.position.z - ship.position.z;
    const dir = { x: dx, z: dz };
    for (let i = 0; i < count; i++) {
      projectiles.spawn(ship.position, dir, def.projectileSpeed, damage, def.element, def.pierce, def.knockback, range, def.family);
    }
    weapon.cooldown = interval;
  }

  processOrbitalDamage(
    ship: Ship,
    weapons: OwnedWeapon[],
    enemies: Enemy[],
    stats: PlayerStats,
    onHit: (enemy: Enemy, damage: number) => void,
  ): void {
    for (const weapon of weapons) {
      if (weapon.def.family !== 'orbital') continue;
      const damage = weapon.def.baseDamage * stats.damageMultiplier * (1 + (weapon.level - 1) * 0.2);
      const range = weapon.def.range;

      for (const orbital of ship.orbitalMeshes) {
        const orbPos: Vec2 = { x: orbital.position.x, z: orbital.position.z };
        for (const enemy of enemies) {
          if (!enemy.alive) continue;
          if (dist2(orbPos, enemy.position) < range) {
            const mult = getElementMultiplier(weapon.def.element, enemy.element);
            onHit(enemy, damage * mult);
          }
        }
      }
    }
  }

  processProjectileHits(
    projectiles: ProjectileManager,
    enemies: Enemy[],
    onHit: (enemy: Enemy, damage: number, isCrit: boolean) => void,
  ): void {
    for (const proj of projectiles.projectiles) {
      if (!proj.alive) continue;
      for (const enemy of enemies) {
        if (!enemy.alive) continue;
        if (dist2(proj.position, enemy.position) < 1.0) {
          const mult = getElementMultiplier(proj.element, enemy.element);
          const dmg = proj.damage * mult;
          const isCrit = mult >= 1.5;
          onHit(enemy, dmg, isCrit);

          if (proj.pierce > 0) {
            proj.pierce--;
          } else {
            projectiles.kill(proj);
            break;
          }
        }
      }
    }
  }
}
