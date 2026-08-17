//! Fixed-size entity pools — no heap allocation (BPF-safe).

use crate::pools::{EntityId, INVALID_ID};

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub struct SlotMeta {
    pub alive: bool,
    pub birth_tick: u32,
}

/// Stack/account-resident entity pool with stable IDs.
#[derive(Clone, Debug)]
pub struct FixedPool<T: Copy + Default, const N: usize> {
    pub slots: [T; N],
    meta: [SlotMeta; N],
    free_list: [u16; N],
    free_count: u16,
}

impl<T: Copy + Default, const N: usize> Default for FixedPool<T, N> {
    fn default() -> Self {
        Self::new()
    }
}

impl<T: Copy + Default, const N: usize> FixedPool<T, N> {
    pub fn new() -> Self {
        let mut free_list = [0u16; N];
        for i in 0..N {
            free_list[i] = i as u16;
        }
        Self {
            slots: [T::default(); N],
            meta: [SlotMeta::default(); N],
            free_list,
            free_count: N as u16,
        }
    }

    pub fn spawn(&mut self, tick: u32) -> Option<EntityId> {
        if self.free_count > 0 {
            self.free_count -= 1;
            let idx = self.free_list[self.free_count as usize] as usize;
            self.meta[idx].alive = true;
            self.meta[idx].birth_tick = tick;
            return Some(idx as EntityId);
        }
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
            Some(idx as EntityId)
        } else {
            None
        }
    }

    pub fn despawn(&mut self, id: EntityId) {
        let idx = id as usize;
        if idx < N && self.meta[idx].alive {
            self.meta[idx].alive = false;
            self.free_list[self.free_count as usize] = id;
            self.free_count += 1;
        }
    }

    pub fn get(&self, id: EntityId) -> Option<&T> {
        let idx = id as usize;
        if idx < N && self.meta[idx].alive {
            Some(&self.slots[idx])
        } else {
            None
        }
    }

    pub fn get_mut(&mut self, id: EntityId) -> Option<&mut T> {
        let idx = id as usize;
        if idx < N && self.meta[idx].alive {
            Some(&mut self.slots[idx])
        } else {
            None
        }
    }

    pub fn is_alive(&self, id: EntityId) -> bool {
        let idx = id as usize;
        idx < N && self.meta[idx].alive
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

    pub fn alive_count(&self) -> usize {
        self.meta.iter().filter(|m| m.alive).count()
    }
}

pub fn isqrt64(n: i64) -> i64 {
    if n <= 0 {
        return 0;
    }
    let mut x = n;
    let mut y = (x + 1) / 2;
    while y < x {
        x = y;
        y = (x + n / x) / 2;
    }
    x
}
