import * as THREE from 'three';
import type { HullDef, PlayerStats, Vec2 } from './types';
import { ARENA_RADIUS, clamp, lerpAngle } from './types';

export class Ship {
  position: Vec2 = { x: 0, z: 0 };
  heading = 0;
  velocity: Vec2 = { x: 0, z: 0 };
  mesh: THREE.Group;
  arcIndicators: THREE.Group;
  orbitalMeshes: THREE.Mesh[] = [];
  orbitalAngle = 0;

  constructor(public hull: HullDef, public stats: PlayerStats) {
    this.mesh = this.createShipMesh(hull);
    this.arcIndicators = this.createArcIndicators();
    this.mesh.add(this.arcIndicators);
  }

  private createShipMesh(hull: HullDef): THREE.Group {
    const group = new THREE.Group();

    const hullGeo = new THREE.BoxGeometry(1.2, 0.6, 3.0);
    const hullMat = new THREE.MeshStandardMaterial({ color: hull.color, roughness: 0.8 });
    const hullMesh = new THREE.Mesh(hullGeo, hullMat);
    hullMesh.position.y = 0.3;
    hullMesh.castShadow = true;
    group.add(hullMesh);

    const deckGeo = new THREE.BoxGeometry(1.0, 0.15, 2.0);
    const deckMat = new THREE.MeshStandardMaterial({ color: hull.accentColor, roughness: 0.6 });
    const deckMesh = new THREE.Mesh(deckGeo, deckMat);
    deckMesh.position.y = 0.65;
    group.add(deckMesh);

    const mastGeo = new THREE.CylinderGeometry(0.06, 0.08, 2.0, 6);
    const mastMat = new THREE.MeshStandardMaterial({ color: 0x6a5030 });
    const mast = new THREE.Mesh(mastGeo, mastMat);
    mast.position.set(0, 1.5, 0);
    group.add(mast);

    const sailGeo = new THREE.PlaneGeometry(1.2, 1.6);
    const sailMat = new THREE.MeshStandardMaterial({
      color: 0xf0e8d8, side: THREE.DoubleSide, transparent: true, opacity: 0.9,
    });
    const sail = new THREE.Mesh(sailGeo, sailMat);
    sail.position.set(0, 1.6, 0);
    group.add(sail);

    const bowGeo = new THREE.ConeGeometry(0.4, 0.8, 4);
    const bowMat = new THREE.MeshStandardMaterial({ color: hull.color });
    const bow = new THREE.Mesh(bowGeo, bowMat);
    bow.rotation.x = Math.PI / 2;
    bow.position.set(0, 0.4, 1.6);
    group.add(bow);

    return group;
  }

  private createArcIndicators(): THREE.Group {
    const group = new THREE.Group();
    const arcGeo = new THREE.RingGeometry(4, 5, 32, 1, -Math.PI / 4, Math.PI / 2);
    const arcMat = new THREE.MeshBasicMaterial({
      color: 0x44ff88, transparent: true, opacity: 0.15, side: THREE.DoubleSide,
    });

    const portArc = new THREE.Mesh(arcGeo, arcMat);
    portArc.rotation.x = -Math.PI / 2;
    portArc.position.y = 0.1;
    group.add(portArc);

    const starboardArc = portArc.clone();
    starboardArc.rotation.z = Math.PI;
    group.add(starboardArc);

    return group;
  }

  update(dt: number, moveDir: Vec2 | null): void {
    if (moveDir) {
      const targetHeading = Math.atan2(moveDir.x, moveDir.z);
      this.heading = lerpAngle(this.heading, targetHeading, this.stats.turnRate * dt);

      const accel = this.stats.speed * 2;
      const friction = 3;
      this.velocity.x += Math.sin(this.heading) * accel * dt;
      this.velocity.z += Math.cos(this.heading) * accel * dt;
      this.velocity.x *= Math.max(0, 1 - friction * dt);
      this.velocity.z *= Math.max(0, 1 - friction * dt);

      const maxSpeed = this.stats.speed;
      const speed = Math.sqrt(this.velocity.x ** 2 + this.velocity.z ** 2);
      if (speed > maxSpeed) {
        this.velocity.x = (this.velocity.x / speed) * maxSpeed;
        this.velocity.z = (this.velocity.z / speed) * maxSpeed;
      }
    } else {
      const friction = 4;
      this.velocity.x *= Math.max(0, 1 - friction * dt);
      this.velocity.z *= Math.max(0, 1 - friction * dt);
    }

    this.position.x += this.velocity.x * dt;
    this.position.z += this.velocity.z * dt;
    this.position.x = clamp(this.position.x, -ARENA_RADIUS + 2, ARENA_RADIUS - 2);
    this.position.z = clamp(this.position.z, -ARENA_RADIUS + 2, ARENA_RADIUS - 2);

    if (this.stats.hpRegen > 0) {
      this.stats.hp = Math.min(this.stats.maxHp, this.stats.hp + this.stats.hpRegen * dt);
    }

    this.mesh.position.set(this.position.x, 0, this.position.z);
    this.mesh.rotation.y = this.heading;
  }

  updateOrbitals(dt: number): void {
    this.orbitalAngle += dt * 3;
    const count = this.orbitalMeshes.length;
    for (let i = 0; i < count; i++) {
      const angle = this.orbitalAngle + (i / count) * Math.PI * 2;
      const radius = 3;
      const mesh = this.orbitalMeshes[i];
      mesh.position.set(
        this.position.x + Math.sin(angle) * radius,
        1.0,
        this.position.z + Math.cos(angle) * radius,
      );
    }
  }

  addOrbital(scene: THREE.Object3D): THREE.Mesh {
    const geo = new THREE.ConeGeometry(0.2, 0.6, 4);
    const mat = new THREE.MeshStandardMaterial({ color: 0x888888, emissive: 0x444444 });
    const mesh = new THREE.Mesh(geo, mat);
    scene.add(mesh);
    this.orbitalMeshes.push(mesh);
    return mesh;
  }

  clearOrbitals(scene: THREE.Object3D): void {
    for (const m of this.orbitalMeshes) {
      scene.remove(m);
      m.geometry.dispose();
      (m.material as THREE.Material).dispose();
    }
    this.orbitalMeshes = [];
  }
}
