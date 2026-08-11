//! Quantised snapshot and event ring for client broadcast.

use crate::config::Element;
use crate::pools::EntityId;

pub const MAX_SNAPSHOT_PLAYERS: usize = 12;
pub const MAX_SNAPSHOT_ENEMIES: usize = 256;
pub const MAX_SNAPSHOT_ENEMY_PROJECTILES: usize = 64;
pub const EVENT_RING_SIZE: usize = 32;

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
#[repr(u8)]
pub enum GameEventKind {
    #[default]
    None = 0,
    Fire = 1,
    Hit = 2,
    Death = 3,
    LevelUp = 4,
    Evolution = 5,
    Revive = 6,
    SpawnEnemy = 7,
    BossTelegraph = 8,
}

#[derive(Clone, Copy, Debug, Default)]
pub struct GameEvent {
    pub kind: GameEventKind,
    pub tick: u32,
    pub actor: EntityId,
    pub target: EntityId,
    pub x: i16,
    pub z: i16,
    pub value: u16,
}

#[derive(Clone, Copy, Debug, Default)]
pub struct SnapshotPlayer {
    pub slot: u8,
    pub x: i16,
    pub z: i16,
    pub heading: u16,
    pub hp_pct: u8,
    pub level: u8,
    pub downed: bool,
}

#[derive(Clone, Copy, Debug, Default)]
pub struct SnapshotEnemy {
    pub id: EntityId,
    pub archetype: u8,
    pub x: i16,
    pub z: i16,
    pub hp_pct: u8,
    pub element: Element,
}

#[derive(Clone, Copy, Debug, Default)]
pub struct SnapshotProjectile {
    pub id: EntityId,
    pub x: i16,
    pub z: i16,
    pub vx: i16,
    pub vz: i16,
}

#[derive(Clone, Debug, Default)]
pub struct Snapshot {
    pub tick: u32,
    pub players: Vec<SnapshotPlayer>,
    pub enemies: Vec<SnapshotEnemy>,
    pub enemy_projectiles: Vec<SnapshotProjectile>,
    pub events: Vec<GameEvent>,
    pub event_head: u8,
}

impl Snapshot {
    pub fn with_capacity(players: usize, enemies: usize) -> Self {
        Self {
            players: vec![SnapshotPlayer::default(); players],
            enemies: Vec::with_capacity(enemies),
            enemy_projectiles: Vec::with_capacity(MAX_SNAPSHOT_ENEMY_PROJECTILES),
            events: vec![GameEvent::default(); EVENT_RING_SIZE],
            ..Default::default()
        }
    }

    pub fn push_event(&mut self, event: GameEvent) {
        let idx = self.event_head as usize % EVENT_RING_SIZE;
        self.events[idx] = event;
        self.event_head = self.event_head.wrapping_add(1);
    }
}

pub fn quantize_pos(v: crate::fix::Fix) -> i16 {
    v.to_int().clamp(-32768, 32767) as i16
}

pub fn hp_pct(current: u16, max: u16) -> u8 {
    if max == 0 {
        0
    } else {
        ((current as u32 * 255) / max as u32).min(255) as u8
    }
}
