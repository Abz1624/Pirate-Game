//! Entity pools with stable IDs and oldest-first eviction.

use crate::fix::Fix;

pub type EntityId = u16;
pub const INVALID_ID: EntityId = u16::MAX;

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
#[repr(u8)]
pub enum EntityKind {
    #[default]
    None = 0,
    Player = 1,
    Enemy = 2,
    Projectile = 3,
    Area = 4,
}

#[derive(Clone, Copy, Debug, Default)]
pub struct SlotMeta {
    pub alive: bool,
    pub birth_tick: u32,
}

pub struct EntityPool<T: Copy + Default> {
    pub slots: Vec<T>,
    pub meta: Vec<SlotMeta>,
    free_list: Vec<usize>,
}

impl<T: Copy + Default> EntityPool<T> {
    pub fn with_capacity(cap: usize) -> Self {
        Self {
            slots: vec![T::default(); cap],
            meta: vec![SlotMeta::default(); cap],
            free_list: Vec::new(),
        }
    }

    pub fn spawn(&mut self, tick: u32) -> Option<EntityId> {
        if let Some(idx) = self.free_list.pop() {
            self.meta[idx].alive = true;
            self.meta[idx].birth_tick = tick;
            return Some(idx as EntityId);
        }
        // Oldest-first eviction
        let mut oldest_tick = u32::MAX;
        let mut oldest_idx = None;
        for (i, m) in self.meta.iter().enumerate() {
            if m.alive && m.birth_tick < oldest_tick {
                oldest_tick = m.birth_tick;
                oldest_idx = Some(i);
            }
        }
        if let Some(idx) = oldest_idx {
            self.meta[idx].alive = true;
            self.meta[idx].birth_tick = tick;
            return Some(idx as EntityId);
        }
        None
    }

    pub fn despawn(&mut self, id: EntityId) {
        let idx = id as usize;
        if idx < self.meta.len() {
            self.meta[idx].alive = false;
            self.free_list.push(idx);
        }
    }

    pub fn get(&self, id: EntityId) -> Option<&T> {
        let idx = id as usize;
        if idx < self.slots.len() && self.meta[idx].alive {
            Some(&self.slots[idx])
        } else {
            None
        }
    }

    pub fn get_mut(&mut self, id: EntityId) -> Option<&mut T> {
        let idx = id as usize;
        if idx < self.slots.len() && self.meta[idx].alive {
            Some(&mut self.slots[idx])
        } else {
            None
        }
    }

    pub fn is_alive(&self, id: EntityId) -> bool {
        let idx = id as usize;
        idx < self.meta.len() && self.meta[idx].alive
    }

    pub fn for_each_alive<F>(&self, mut f: F)
    where
        F: FnMut(EntityId, &T),
    {
        for (i, m) in self.meta.iter().enumerate() {
            if m.alive {
                f(i as EntityId, &self.slots[i]);
            }
        }
    }

    pub fn for_each_alive_mut<F>(&mut self, mut f: F)
    where
        F: FnMut(EntityId, &mut T),
    {
        for (i, m) in self.meta.iter().enumerate() {
            if m.alive {
                f(i as EntityId, &mut self.slots[i]);
            }
        }
    }
}

#[derive(Clone, Copy, Debug, Default)]
pub struct Vec2 {
    pub x: Fix,
    pub z: Fix,
}

impl Vec2 {
    pub fn new(x: Fix, z: Fix) -> Self {
        Self { x, z }
    }

    pub fn dist_sq(self, other: Self) -> i64 {
        let dx = (self.x - other.x).0 as i64;
        let dz = (self.z - other.z).0 as i64;
        dx * dx + dz * dz
    }
}
