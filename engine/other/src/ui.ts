import type { DraftCard, GamePhase, OwnedPassive, OwnedWeapon, TideConfig } from './types';
import { TIDES } from './types';

export class UIManager {
  private hudEl: HTMLElement;
  private menuEl: HTMLElement;
  private draftEl: HTMLElement;
  private gameoverEl: HTMLElement;
  private selectedTide = 1;
  private onStart?: (tide: number) => void;
  private onDraftPick?: (card: DraftCard) => void;
  private onRestart?: () => void;

  constructor() {
    this.hudEl = document.getElementById('hud')!;
    this.menuEl = document.getElementById('menu')!;
    this.draftEl = document.getElementById('draft')!;
    this.gameoverEl = document.getElementById('gameover')!;
    this.renderMenu();
  }

  setCallbacks(cb: {
    onStart: (tide: number) => void;
    onDraftPick: (card: DraftCard) => void;
    onRestart: () => void;
  }): void {
    this.onStart = cb.onStart;
    this.onDraftPick = cb.onDraftPick;
    this.onRestart = cb.onRestart;
  }

  setPhase(phase: GamePhase): void {
    this.menuEl.classList.toggle('hidden', phase !== 'menu');
    this.draftEl.classList.toggle('hidden', phase !== 'draft');
    this.gameoverEl.classList.toggle('hidden', phase !== 'gameover');
    this.hudEl.style.display = phase === 'menu' ? 'none' : 'block';
  }

  private renderMenu(): void {
    this.menuEl.innerHTML = `
      <div class="menu-panel">
        <h1>Tides of Plunder</h1>
        <p class="subtitle">A pirate survivor-like — steer to aim your broadsides</p>
        <p class="pillar">"Movement is aiming."</p>
        <div class="tide-select">
          ${TIDES.map((t) => `
            <button class="tide-btn ${t.level === this.selectedTide ? 'selected' : ''}" data-tide="${t.level}">
              Tide ${t.level}<br><small>${t.biome}</small>
            </button>
          `).join('')}
        </div>
        <button class="btn-primary" id="start-btn">Set Sail</button>
        <p class="controls-hint">
          WASD / Arrow Keys to steer<br>
          Cannons fire from port &amp; starboard — turn to aim<br>
          Survive, level up, draft power-ups
        </p>
      </div>
    `;

    this.menuEl.querySelectorAll('.tide-btn').forEach((btn) => {
      btn.addEventListener('click', () => {
        this.selectedTide = parseInt((btn as HTMLElement).dataset.tide!);
        this.menuEl.querySelectorAll('.tide-btn').forEach((b) => b.classList.remove('selected'));
        btn.classList.add('selected');
      });
    });

    this.menuEl.querySelector('#start-btn')!.addEventListener('click', () => {
      this.onStart?.(this.selectedTide);
    });
  }

  showDraft(cards: DraftCard[]): void {
    this.draftEl.innerHTML = `
      <div class="draft-panel">
        <h2>Level Up!</h2>
        <div class="draft-cards">
          ${cards.map((c, i) => `
            <div class="draft-card ${c.rarity}" data-index="${i}">
              <div class="card-rarity">${c.rarity}</div>
              <div class="card-name">${c.name}</div>
              <div class="card-desc">${c.description}</div>
              <div class="card-type">${c.type}</div>
            </div>
          `).join('')}
        </div>
      </div>
    `;

    this.draftEl.querySelectorAll('.draft-card').forEach((el) => {
      el.addEventListener('click', () => {
        const idx = parseInt((el as HTMLElement).dataset.index!);
        this.onDraftPick?.(cards[idx]);
      });
    });
  }

  showGameOver(stats: { kills: number; gold: number; time: number; level: number; tide: TideConfig }): void {
    const mins = Math.floor(stats.time / 60);
    const secs = Math.floor(stats.time % 60);
    this.gameoverEl.innerHTML = `
      <div class="gameover-panel">
        <h2>Ship Sunk</h2>
        <p>But every run pays — you keep your gold.</p>
        <div class="gameover-stats">
          <div>Time survived: ${mins}:${secs.toString().padStart(2, '0')}</div>
          <div>Level reached: ${stats.level}</div>
          <div>Enemies sunk: ${stats.kills}</div>
          <div class="gold">Gold earned: ${stats.gold}</div>
        </div>
        <button class="btn-primary" id="restart-btn">Return to Port</button>
      </div>
    `;
    this.gameoverEl.querySelector('#restart-btn')!.addEventListener('click', () => {
      this.onRestart?.();
    });
  }

  updateHUD(data: {
    hp: number;
    maxHp: number;
    xp: number;
    xpNeeded: number;
    level: number;
    kills: number;
    gold: number;
    time: number;
    enemyCount: number;
    tide: TideConfig;
    weapons: OwnedWeapon[];
    passives: OwnedPassive[];
  }): void {
    const hpPct = (data.hp / data.maxHp) * 100;
    const xpPct = (data.xp / data.xpNeeded) * 100;
    const mins = Math.floor(data.time / 60);
    const secs = Math.floor(data.time % 60);

    this.hudEl.innerHTML = `
      <div class="hud-top">
        <div class="hud-bar-group">
          <div class="bar-label">Hull</div>
          <div class="bar-track"><div class="bar-fill hp" style="width:${hpPct}%"></div></div>
          <div class="bar-label">Experience</div>
          <div class="bar-track"><div class="bar-fill xp" style="width:${xpPct}%"></div></div>
        </div>
        <div class="hud-stats">
          <div>Tide ${data.tide.level} — ${data.tide.biome}</div>
          <div>Level ${data.level}</div>
          <div>${mins}:${secs.toString().padStart(2, '0')}</div>
          <div>Kills: ${data.kills} | Gold: ${data.gold}</div>
          <div>Enemies: ${data.enemyCount}</div>
        </div>
      </div>
      <div class="hud-weapons">
        ${data.weapons.map((w) => `
          <div class="weapon-slot">
            <div class="name">${w.def.name}</div>
            <div class="level">Lv.${w.level} · ${w.def.family}</div>
          </div>
        `).join('')}
        ${data.passives.map((p) => `
          <div class="weapon-slot">
            <div class="name">${p.def.name}</div>
            <div class="level">Passive Lv.${p.level}</div>
          </div>
        `).join('')}
      </div>
      <div class="hud-arcs">
        Port ◀ Broadside ▶ Starboard<br>
        Steer to present your sides
      </div>
    `;
  }
}
