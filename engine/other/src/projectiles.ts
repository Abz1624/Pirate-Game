import * as THREE from 'three';
import type { ArcFamily, Element, Vec2 } from './types';
import { dist2 } from './types';

let nextId = 0;

export interface Projectile {
  id: number;
  position: Vec2;
  velocity: Vec2;
  damage: number;
  element: Element;
  pierce: number;
  knockback: number;
  range: number;
  traveled: number;
  alive: boolean;
  mesh: THREE.Mesh;
  family: ArcFamily;
}

export class ProjectileManager {
  group = new THREE.Group();
  projectiles: Projectile[] = [];
  private geo = new THREE.SphereGeometry(0.25, 6, 4);
  private matCache = new Map<number, THREE.MeshStandardMaterial>();

  private getMat(color: number): THREE.MeshStandardMaterial {
    if (!this.matCache.has(color)) {
      this.matCache.set(color, new THREE.MeshStandardMaterial({
        color, emissive: color, emissiveIntensity: 0.4,
      }));
    }
    return this.matCache.get(color)!;
  }

  spawn(
    pos: Vec2,
    dir: Vec2,
    speed: number,
    damage: number,
    element: Element,
    pierce: number,
    knockback: number,
    range: number,
    family: ArcFamily,
  ): void {
    const len = Math.sqrt(dir.x * dir.x + dir.z * dir.z) || 1;
    const color = element === 'fire' ? 0xff6622
      : element === 'water' ? 0x4488ff
      : element === 'lightning' ? 0xffff44
      : element === 'earth' ? 0x886644
      : element === 'air' ? 0xaaddff
      : 0xccccaa;

    const mesh = new THREE.Mesh(this.geo, this.getMat(color));
    mesh.position.set(pos.x, 0.8, pos.z);
    mesh.castShadow = true;
    this.group.add(mesh);

    this.projectiles.push({
      id: nextId++,
      position: { ...pos },
      velocity: { x: (dir.x / len) * speed, z: (dir.z / len) * speed },
      damage,
      element,
      pierce,
      knockback,
      range,
      traveled: 0,
      alive: true,
      mesh,
      family,
    });
  }

  update(dt: number): void {
    for (const p of this.projectiles) {
      if (!p.alive) continue;
      p.position.x += p.velocity.x * dt;
      p.position.z += p.velocity.z * dt;
      p.traveled += Math.sqrt(p.velocity.x ** 2 + p.velocity.z ** 2) * dt;
      p.mesh.position.set(p.position.x, 0.8, p.position.z);

      if (p.traveled >= p.range) {
        this.kill(p);
      }
    }
    this.projectiles = this.projectiles.filter((p) => p.alive);
  }

  kill(p: Projectile): void {
    p.alive = false;
    this.group.remove(p.mesh);
  }

  clear(): void {
    for (const p of this.projectiles) {
      this.group.remove(p.mesh);
    }
    this.projectiles = [];
  }
}

export interface XpOrb {
  id: number;
  position: Vec2;
  value: number;
  alive: boolean;
  mesh: THREE.Mesh;
  magnetized: boolean;
}

export class XpOrbManager {
  group = new THREE.Group();
  orbs: XpOrb[] = [];
  private geo = new THREE.OctahedronGeometry(0.35, 0);
  private mat = new THREE.MeshStandardMaterial({
    color: 0x44aaff, emissive: 0x2288ff, emissiveIntensity: 0.6,
  });

  spawn(pos: Vec2, value: number): void {
    const mesh = new THREE.Mesh(this.geo, this.mat);
    mesh.position.set(pos.x, 0.5, pos.z);
    this.group.add(mesh);
    this.orbs.push({
      id: nextId++,
      position: { ...pos },
      value,
      alive: true,
      mesh,
      magnetized: false,
    });
  }

  update(dt: number, playerPos: Vec2, pickupRadius: number): number {
    let xpGained = 0;
    for (const orb of this.orbs) {
      if (!orb.alive) continue;
      const d = dist2(orb.position, playerPos);
      if (d < pickupRadius) orb.magnetized = true;

      if (orb.magnetized) {
        const speed = 18 + (pickupRadius - d) * 2;
        const dx = playerPos.x - orb.position.x;
        const dz = playerPos.z - orb.position.z;
        const len = Math.sqrt(dx * dx + dz * dz) || 1;
        orb.position.x += (dx / len) * speed * dt;
        orb.position.z += (dz / len) * speed * dt;
        orb.mesh.position.set(orb.position.x, 0.5 + Math.sin(Date.now() * 0.005) * 0.2, orb.position.z);
      } else {
        orb.mesh.rotation.y += dt * 2;
        orb.mesh.position.y = 0.5 + Math.sin(Date.now() * 0.003 + orb.id) * 0.15;
      }

      if (d < 1.2) {
        xpGained += orb.value;
        this.kill(orb);
      }
    }
    this.orbs = this.orbs.filter((o) => o.alive);
    return xpGained;
  }

  kill(orb: XpOrb): void {
    orb.alive = false;
    this.group.remove(orb.mesh);
  }

  clear(): void {
    for (const o of this.orbs) this.group.remove(o.mesh);
    this.orbs = [];
  }
}
