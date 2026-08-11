//! Uniform spatial grid for O(1) proximity queries.

use crate::fix::Fix;
use crate::pools::{EntityId, EntityKind};
use crate::GRID_CELLS;

const MAX_PER_CELL: usize = 16;

#[derive(Clone, Copy, Debug, Default)]
pub struct GridEntry {
    pub id: EntityId,
    pub kind: EntityKind,
}

/// Account-resident grid: flat array of cell heads + linked slots.
#[derive(Clone, Debug)]
pub struct SpatialGrid {
    pub cell_size: Fix,
    pub origin: Fix,
    /// Per-cell count of entries.
    counts: [u8; GRID_CELLS * GRID_CELLS],
    /// Flat storage: cell * MAX_PER_CELL + slot.
    entries: [[GridEntry; MAX_PER_CELL]; GRID_CELLS * GRID_CELLS],
}

impl Default for SpatialGrid {
    fn default() -> Self {
        Self::new(Fix::from_int(200) / Fix::from_int(GRID_CELLS as i32))
    }
}

impl SpatialGrid {
    pub fn new(cell_size: Fix) -> Self {
        Self {
            cell_size,
            origin: Fix::from_int(-100),
            counts: [0; GRID_CELLS * GRID_CELLS],
            entries: [[GridEntry::default(); MAX_PER_CELL]; GRID_CELLS * GRID_CELLS],
        }
    }

    pub fn clear(&mut self) {
        self.counts = [0; GRID_CELLS * GRID_CELLS];
    }

    fn cell_index(&self, x: Fix, z: Fix) -> Option<usize> {
        let cx = ((x - self.origin) / self.cell_size).to_int();
        let cz = ((z - self.origin) / self.cell_size).to_int();
        if cx < 0 || cz < 0 || cx >= GRID_CELLS as i32 || cz >= GRID_CELLS as i32 {
            return None;
        }
        Some(cz as usize * GRID_CELLS + cx as usize)
    }

    pub fn insert(&mut self, x: Fix, z: Fix, id: EntityId, kind: EntityKind) {
        let Some(ci) = self.cell_index(x, z) else {
            return;
        };
        let count = self.counts[ci] as usize;
        if count >= MAX_PER_CELL {
            return;
        }
        self.entries[ci][count] = GridEntry { id, kind };
        self.counts[ci] += 1;
    }

    pub fn query_cell<F>(&self, x: Fix, z: Fix, mut f: F)
    where
        F: FnMut(GridEntry),
    {
        let Some(ci) = self.cell_index(x, z) else {
            return;
        };
        for i in 0..self.counts[ci] as usize {
            f(self.entries[ci][i]);
        }
    }

    pub fn query_radius<F>(&self, x: Fix, z: Fix, radius_cells: i32, mut f: F)
    where
        F: FnMut(GridEntry),
    {
        let cx = ((x - self.origin) / self.cell_size).to_int();
        let cz = ((z - self.origin) / self.cell_size).to_int();
        for dz in -radius_cells..=radius_cells {
            for dx in -radius_cells..=radius_cells {
                let nx = cx + dx;
                let nz = cz + dz;
                if nx < 0 || nz < 0 || nx >= GRID_CELLS as i32 || nz >= GRID_CELLS as i32 {
                    continue;
                }
                let ci = nz as usize * GRID_CELLS + nx as usize;
                for i in 0..self.counts[ci] as usize {
                    f(self.entries[ci][i]);
                }
            }
        }
    }
}
