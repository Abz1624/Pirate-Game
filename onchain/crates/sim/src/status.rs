//! Status effects bitmask and magnitudes.

use crate::fix::Fix;

bitflags::bitflags! {
    #[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
    pub struct StatusFlags: u16 {
        const BURNING   = 0b0000_0001;
        const SLOWED    = 0b0000_0010;
        const ROOTED    = 0b0000_0100;
        const SHIELDED  = 0b0000_1000;
        const INVULN    = 0b0001_0000;
        const DOWNED    = 0b0010_0000;
    }
}

#[derive(Clone, Copy, Debug, Default)]
pub struct StatusState {
    pub flags: StatusFlags,
    pub burn_dps: u16,
    pub slow_pct: u8,
    pub shield_hp: u16,
    pub expiry_tick: u32,
}

impl StatusState {
    pub fn tick(&mut self, current_tick: u32) {
        if self.expiry_tick > 0 && current_tick >= self.expiry_tick {
            self.flags = StatusFlags::empty();
            self.burn_dps = 0;
            self.slow_pct = 0;
            self.shield_hp = 0;
            self.expiry_tick = 0;
        }
    }

    pub fn apply_damage(&mut self, amount: u16) -> u16 {
        if self.flags.contains(StatusFlags::INVULN) {
            return 0;
        }
        if self.shield_hp > 0 {
            let absorbed = amount.min(self.shield_hp);
            self.shield_hp -= absorbed;
            let remaining = amount - absorbed;
            if self.shield_hp == 0 {
                self.flags.remove(StatusFlags::SHIELDED);
            }
            return remaining;
        }
        amount
    }
}

pub fn knockback_velocity(
    from_x: Fix,
    from_z: Fix,
    to_x: Fix,
    to_z: Fix,
    strength: Fix,
) -> (Fix, Fix) {
    let dx = to_x - from_x;
    let dz = to_z - from_z;
    let len_sq = dx.mul_fix(dx) + dz.mul_fix(dz);
    if len_sq.0 == 0 {
        return (Fix::ZERO, Fix::ZERO);
    }
    let len = len_sq.sqrt().max(Fix::from_int(1));
    (dx.div_fix(len).mul_fix(strength), dz.div_fix(len).mul_fix(strength))
}
