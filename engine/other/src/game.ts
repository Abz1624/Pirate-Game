import * as THREE from 'three';
import { CombatSystem } from './combat';
import { DraftSystem, recalculateStats } from './draft';
import { EnemyManager, getContactDamage } from './enemies';
import { InputSystem } from './input';
import { ProjectileManager, XpOrbManager } from './projectiles';
import { EffectsManager, SceneManager } from './scene';
import { Ship } from './ship';
import { UIManager } from './ui';
import {
  HULLS,
  TIDES,
  WEAPONS,
  createBaseStats,
  getElementMultiplier,
  xpForLevel,
  type DraftCard,
  type GamePhase,
  type OwnedPassive,
  type OwnedWeapon,
  type TideConfig,
} from './types';

const MAX_ENEMIES = 200;

export class Game {
  private phase: GamePhase = 'menu';
  private sceneManager: SceneManager;
  private input: InputSystem;
  private ui: UIManager;
  private effects: EffectsManager;

  private ship!: Ship;
  private enemies = new EnemyManager();
  private projectiles = new ProjectileManager();
  private xpOrbs = new XpOrbManager();
  private combat = new CombatSystem();
  private draft = new DraftSystem();

  private weapons: OwnedWeapon[] = [];
  private passives: OwnedPassive[] = [];
  private tide: TideConfig = TIDES[0];

  private level = 1;
  private xp = 0;
  private kills = 0;
  private gold = 0;
  private time = 0;
  private spawnTimer = 0;
  private spawnInterval = 2;
  private pendingDraftCards: DraftCard[] = [];
  private lastTime = 0;
  private contactDamageTimer = 0;

  constructor(canvas: HTMLCanvasElement) {
    this.sceneManager = new SceneManager(canvas);
    this.input = new InputSystem(canvas);
    this.ui = new UIManager();
    this.effects = new EffectsManager(document.getElementById('app')!);

    this.ui.setCallbacks({
      onStart: (tide) => this.startRun(tide),
      onDraftPick: (card) => this.pickDraft(card),
      onRestart: () => this.returnToMenu(),
    });

    this.sceneManager.scene.add(this.enemies.group);
    this.sceneManager.scene.add(this.projectiles.group);
    this.sceneManager.scene.add(this.xpOrbs.group);

    requestAnimationFrame((t) => this.loop(t));
  }

  private startRun(tideLevel: number): void {
    this.tide = TIDES.find((t) => t.level === tideLevel) ?? TIDES[0];
    this.phase = 'playing';
    this.ui.setPhase('playing');

    const hull = HULLS.sloop;
    const stats = createBaseStats(hull);
    this.ship = new Ship(hull, stats);
    this.sceneManager.scene.add(this.ship.mesh);
    this.sceneManager.setBiomeColor(this.tide.biomeColor);

    this.weapons = [{
      def: WEAPONS.iron_cannons,
      level: 1,
      cooldown: 0,
    }];
    this.passives = [];
    this.level = 1;
    this.xp = 0;
    this.kills = 0;
    this.gold = 0;
    this.time = 0;
    this.spawnTimer = 0;
    this.spawnInterval = 2.5 / this.tide.enemyDensityMult;
    this.contactDamageTimer = 0;

    this.enemies.clear();
    this.projectiles.clear();
    this.xpOrbs.clear();

    this.enemies.spawnWave(8, this.tide.enemyHpMult, this.tide.eliteRate);
  }

  private returnToMenu(): void {
    this.phase = 'menu';
    this.ui.setPhase('menu');
    if (this.ship) {
      this.ship.clearOrbitals(this.sceneManager.scene);
      this.sceneManager.scene.remove(this.ship.mesh);
    }
    this.enemies.clear();
    this.projectiles.clear();
    this.xpOrbs.clear();
  }

  private pickDraft(card: DraftCard): void {
    this.draft.applyCard(card, this.weapons, this.passives, () => {
      this.ship.addOrbital(this.sceneManager.scene);
    });
    recalculateStats(this.ship.stats, this.passives, {
      hp: HULLS.sloop.hp,
      speed: HULLS.sloop.speed,
      turnRate: HULLS.sloop.turnRate,
      broadsideArc: HULLS.sloop.broadsideArc,
    });
    this.phase = 'playing';
    this.ui.setPhase('playing');
  }

  private triggerLevelUp(): void {
    this.level++;
    this.xp = 0;
    this.effects.showLevelFlash();
    this.pendingDraftCards = this.draft.generateOffers(
      this.weapons,
      this.passives,
      this.level,
      this.ship.stats.rarityWeight,
    );
    this.phase = 'draft';
    this.ui.setPhase('draft');
    this.ui.showDraft(this.pendingDraftCards);
  }

  private loop(timestamp: number): void {
    const dt = Math.min((timestamp - this.lastTime) / 1000, 0.05);
    this.lastTime = timestamp;

    if (this.phase === 'playing') {
      this.update(dt);
    }

    if (this.phase === 'playing' || this.phase === 'draft') {
      this.sceneManager.updateCamera(
        this.ship?.position.x ?? 0,
        this.ship?.position.z ?? 0,
        dt,
      );
    }

    this.sceneManager.render();
    requestAnimationFrame((t) => this.loop(t));
  }

  private update(dt: number): void {
    this.time += dt;

    const moveDir = this.input.getMoveDirection();
    this.ship.update(dt, moveDir);
    this.ship.updateOrbitals(dt);

    this.combat.fireWeapons(
      this.ship,
      this.weapons,
      this.enemies.enemies,
      this.projectiles,
      this.ship.stats,
    );

    this.combat.processOrbitalDamage(
      this.ship,
      this.weapons,
      this.enemies.enemies,
      this.ship.stats,
      (enemy, damage) => this.hitEnemy(enemy, damage, false),
    );

    this.projectiles.update(dt);
    this.combat.processProjectileHits(
      this.projectiles,
      this.enemies.enemies,
      (enemy, damage, isCrit) => this.hitEnemy(enemy, damage, isCrit),
    );

    this.enemies.update(dt, this.ship.position);

    const xpGained = this.xpOrbs.update(dt, this.ship.position, this.ship.stats.pickupRadius);
    if (xpGained > 0) {
      this.xp += xpGained;
      if (this.xp >= xpForLevel(this.level)) {
        this.triggerLevelUp();
      }
    }

    this.contactDamageTimer -= dt;
    if (this.contactDamageTimer <= 0) {
      const dmg = getContactDamage(this.enemies.enemies, this.ship.position, 1.5);
      if (dmg > 0) {
        this.ship.stats.hp -= dmg * dt * 2;
        this.sceneManager.shake(0.3);
        this.contactDamageTimer = 0.1;
      }
    }

    if (this.ship.stats.hp <= 0) {
      this.endRun();
      return;
    }

    this.spawnTimer -= dt;
    if (this.spawnTimer <= 0 && this.enemies.getEnemyCount() < MAX_ENEMIES) {
      const waveSize = Math.min(
        Math.floor(3 + this.time * 0.15 * this.tide.enemyDensityMult),
        15,
      );
      this.enemies.spawnWave(waveSize, this.tide.enemyHpMult, this.tide.eliteRate);
      this.spawnInterval = Math.max(0.5, 2.5 / this.tide.enemyDensityMult - this.time * 0.01);
      this.spawnTimer = this.spawnInterval;
    }

    if (this.time >= this.tide.duration) {
      this.endRun(true);
      return;
    }

    this.ui.updateHUD({
      hp: this.ship.stats.hp,
      maxHp: this.ship.stats.maxHp,
      xp: this.xp,
      xpNeeded: xpForLevel(this.level),
      level: this.level,
      kills: this.kills,
      gold: this.gold,
      time: this.time,
      enemyCount: this.enemies.getEnemyCount(),
      tide: this.tide,
      weapons: this.weapons,
      passives: this.passives,
    });
  }

  private hitEnemy(enemy: import('./enemies').Enemy, damage: number, isCrit: boolean): void {
    const mult = getElementMultiplier(
      this.weapons[0]?.def.element ?? 'neutral',
      enemy.element,
    );
    const isWeak = mult < 1;
    this.enemies.damage(enemy, damage);

    const worldPos = new THREE.Vector3(enemy.position.x, 1.5, enemy.position.z);
    const screen = this.effects.worldToScreen(
      worldPos,
      this.sceneManager.camera,
      this.sceneManager.renderer,
    );
    this.effects.showDamageNumber(screen.x, screen.y, damage, isCrit, isWeak);

    if (!enemy.alive) {
      this.kills++;
      this.gold += Math.floor(enemy.goldValue * this.tide.rewardMult);
      this.xpOrbs.spawn(enemy.position, enemy.xpValue);
      this.sceneManager.shake(0.1);
    }
  }

  private endRun(extracted = false): void {
    if (extracted) {
      this.gold = Math.floor(this.gold * 1.25);
    }
    this.phase = 'gameover';
    this.ui.setPhase('gameover');
    this.ui.showGameOver({
      kills: this.kills,
      gold: this.gold,
      time: this.time,
      level: this.level,
      tide: this.tide,
    });
  }
}
