//! 256-entry sine table with linear interpolation.

use crate::fix::{Fix, FIX_ONE};

const TABLE_BITS: u16 = 256;

static SIN_TABLE: [i32; 256] = build_sin_table();

const fn build_sin_table() -> [i32; 256] {
    let mut t = [0i32; 256];
    let mut i = 0usize;
    while i < 256 {
        // Approximate sin(i/256 * 2pi) * 65536 using parabola on [0,1]
        let x = i as i32;
        let s = if x <= 64 {
            x * 4
        } else if x <= 128 {
            (128 - x) * 4
        } else if x <= 192 {
            (x - 128) * -4
        } else {
            (256 - x) * -4
        };
        t[i] = s * 256;
        i += 1;
    }
    t
}

#[inline]
pub fn sin(angle: u16) -> Fix {
    let idx = (angle as usize) >> 2;
    let frac = (angle & 3) as i32;
    let a = SIN_TABLE[idx % 256];
    let b = SIN_TABLE[(idx + 1) % 256];
    Fix(a + (b - a) * frac / 4)
}

#[inline]
pub fn cos(angle: u16) -> Fix {
    sin(angle.wrapping_add(64))
}

#[inline]
pub fn sin_cos(angle: u16) -> (Fix, Fix) {
    (sin(angle), cos(angle))
}

pub fn atan2(y: Fix, x: Fix) -> u16 {
    if x.0 == 0 && y.0 == 0 {
        return 0;
    }
    let ax = x.0.unsigned_abs();
    let ay = y.0.unsigned_abs();
    let mut a = if ax == 0 {
        64u32
    } else {
        ((ay as u64) << 8).div_euclid(ax as u64).min(64) as u32
    };
    let angle = if x.0 >= 0 && y.0 >= 0 {
        a
    } else if x.0 < 0 && y.0 >= 0 {
        128 - a
    } else if x.0 < 0 && y.0 < 0 {
        128 + a
    } else {
        256 - a
    };
    (angle % 256) as u16 * 4
}
