import * as THREE from 'three';
import type { Element, EnemyArchetype, Vec2 } from './types';
import { ARENA_RADIUS, clamp, dist2 } from './types';

let nextId = 0;

const ELEMENT_COLORS: Record<Element, number> = {
  neutral: 0x88aa88,
  fire: 0xff4422,
  water: 0x3388ff,
  air: 0xccddff,
  earth: 0x886644,
  lightning: 0xffee44,
};

const ARCHETYPE_STATS: Record<EnemyArchetype, { hp: number; speed: number; damage: number; radius: number; xp: number; gold: number }> = {
  swarmer: { hp: 15, speed: 5, damage: 8, radius: 0.6, xp: 3, gold: 2 },
  charger: { hp: 30, speed: 3, damage: 15, radius: 0.8, xp: 8, gold: 5 },
  ranged: { hp: 20, speed: 3.5, damage: 10, radius: 0.7, xp: 6, gold: 4 },
  orbiter: { hp: 25, speed: 4, damage: 12, radius: 0.75, xp: 7, gold: 4 },
  bruiser: { hp: 80, speed: 2, damage: 20, radius: 1.2, xp: 15, gold: 10 },
  elite: { hp: 120, speed: 3.5, damage: 25, radius: 1.0, xp: 30, gold: 20 },
};

export interface Enemy {
  id: number;
  position: Vec2;
  velocity: Vec2;
  hp: number;
  maxHp: number;
  speed: number;
  damage: number;
  element: Element;
  archetype: EnemyArchetype;
  alive: boolean;
  hitFlash: number;
  mesh: THREE.Mesh;
  xpValue: number;
  goldValue: number;
  chargeTimer: number;
  chargeDir: Vec2;
  orbitAngle: number;
  contactCooldown: number;
}

export class EnemyManager {
  group = new THREE.Group();
  enemies: Enemy[] = [];
  private geoCache = new Map<number, THREE.IcosahedronGeometry>();

  private getGeo(radius: number): THREE.IcosahedronGeometry {
    const key = Math.round(radius * 10);
    if (!this.geoCache.has(key)) {
      this.geoCache.set(key, new THREE.IcosahedronGeometry(radius, 1));
    }
    return this.geoCache.get(key)!;
  }

  spawn(
    archetype: EnemyArchetype,
    element: Element,
    hpMult: number,
    spawnPos?: Vec2,
  ): Enemy {
    const stats = ARCHETYPE_STATS[archetype];
    const pos = spawnPos ?? this.getSpawnPos();
    const color = ELEMENT_COLORS[element];
    const mat = new THREE.MeshStandardMaterial({
      color,
      emissive: color,
      emissiveIntensity: archetype === 'elite' ? 0.3 : 0.1,
    });
    const mesh = new THREE.Mesh(this.getGeo(stats.radius), mat);
    mesh.position.set(pos.x, stats.radius, pos.z);
    mesh.castShadow = true;
    this.group.add(mesh);

    const enemy: Enemy = {
      id: nextId++,
      position: { ...pos },
      velocity: { x: 0, z: 0 },
      hp: stats.hp * hpMult,
      maxHp: stats.hp * hpMult,
      speed: stats.speed,
      damage: stats.damage,
      element,
      archetype,
      alive: true,
      hitFlash: 0,
      mesh,
      xpValue: stats.xp,
      goldValue: stats.gold,
      chargeTimer: 2 + Math.random() * 2,
      chargeDir: { x: 0, z: 0 },
      orbitAngle: Math.random() * Math.PI * 2,
      contactCooldown: 0,
    };
    this.enemies.push(enemy);
    return enemy;
  }

  private getSpawnPos(): Vec2 {
    const angle = Math.random() * Math.PI * 2;
    const dist = ARENA_RADIUS * 0.7 + Math.random() * ARENA_RADIUS * 0.25;
    return { x: Math.cos(angle) * dist, z: Math.sin(angle) * dist };
  }

  spawnWave(count: number, hpMult: number, eliteRate: number): void {
    const elements: Element[] = ['neutral', 'fire', 'water', 'air', 'earth', 'lightning'];
    for (let i = 0; i < count; i++) {
      const roll = Math.random();
      let archetype: EnemyArchetype;
      if (roll < eliteRate) archetype = 'elite';
      else if (roll < eliteRate + 0.15) archetype = 'charger';
      else if (roll < eliteRate + 0.25) archetype = 'bruiser';
      else archetype = 'swarmer';
      const element = elements[Math.floor(Math.random() * elements.length)];
      this.spawn(archetype, element, hpMult);
    }
  }

  update(dt: number, playerPos: Vec2): void {
    for (const e of this.enemies) {
      if (!e.alive) continue;

      if (e.hitFlash > 0) {
        e.hitFlash -= dt;
        (e.mesh.material as THREE.MeshStandardMaterial).emissiveIntensity =
          e.archetype === 'elite' ? 0.8 : 0.6;
      } else {
        (e.mesh.material as THREE.MeshStandardMaterial).emissiveIntensity =
          e.archetype === 'elite' ? 0.3 : 0.1;
      }

      if (e.contactCooldown > 0) e.contactCooldown -= dt;

      switch (e.archetype) {
        case 'swarmer':
          this.moveToward(e, playerPos, dt);
          break;
        case 'charger':
          e.chargeTimer -= dt;
          if (e.chargeTimer <= 0 && e.chargeTimer > -0.5) {
            const dx = playerPos.x - e.position.x;
            const dz = playerPos.z - e.position.z;
            const len = Math.sqrt(dx * dx + dz * dz) || 1;
            e.chargeDir = { x: dx / len, z: dz / len };
            e.position.x += e.chargeDir.x * e.speed * 4 * dt;
            e.position.z += e.chargeDir.z * e.speed * 4 * dt;
          } else if (e.chargeTimer <= -0.5) {
            e.chargeTimer = 2 + Math.random() * 2;
            this.moveToward(e, playerPos, dt, 0.5);
          } else {
            this.moveToward(e, playerPos, dt, 0.5);
          }
          break;
        case 'orbiter': {
          e.orbitAngle += dt * 1.5;
          const orbitDist = 12;
          const targetX = playerPos.x + Math.cos(e.orbitAngle) * orbitDist;
          const targetZ = playerPos.z + Math.sin(e.orbitAngle) * orbitDist;
          this.moveToward(e, { x: targetX, z: targetZ }, dt);
          break;
        }
        default:
          this.moveToward(e, playerPos, dt);
      }

      e.position.x = clamp(e.position.x, -ARENA_RADIUS, ARENA_RADIUS);
      e.position.z = clamp(e.position.z, -ARENA_RADIUS, ARENA_RADIUS);
      const stats = ARCHETYPE_STATS[e.archetype];
      e.mesh.position.set(e.position.x, stats.radius, e.position.z);
    }
    this.enemies = this.enemies.filter((e) => e.alive);
  }

  private moveToward(e: Enemy, target: Vec2, dt: number, speedMult = 1): void {
    const dx = target.x - e.position.x;
    const dz = target.z - e.position.z;
    const len = Math.sqrt(dx * dx + dz * dz) || 1;
    e.position.x += (dx / len) * e.speed * speedMult * dt;
    e.position.z += (dz / len) * e.speed * speedMult * dt;
  }

  damage(enemy: Enemy, amount: number): void {
    enemy.hp -= amount;
    enemy.hitFlash = 0.1;
    if (enemy.hp <= 0) {
      this.kill(enemy);
    }
  }

  kill(enemy: Enemy): void {
    enemy.alive = false;
    this.group.remove(enemy.mesh);
    (enemy.mesh.material as THREE.Material).dispose();
  }

  getAliveEnemies(): Enemy[] {
    return this.enemies.filter((e) => e.alive);
  }

  clear(): void {
    for (const e of this.enemies) {
      this.group.remove(e.mesh);
      (e.mesh.material as THREE.Material).dispose();
    }
    this.enemies = [];
  }

  getEnemyCount(): number {
    return this.enemies.filter((e) => e.alive).length;
  }
}

export function getContactDamage(enemies: Enemy[], playerPos: Vec2, playerRadius: number): number {
  let total = 0;
  for (const e of enemies) {
    if (!e.alive || e.contactCooldown > 0) continue;
    const stats = ARCHETYPE_STATS[e.archetype];
    if (dist2(e.position, playerPos) < playerRadius + stats.radius) {
      total += e.damage;
      e.contactCooldown = 0.5;
    }
  }
  return total;
}
