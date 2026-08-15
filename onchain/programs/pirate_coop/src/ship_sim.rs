//! Minimal fixed-point ship integration for on-chain ticks (10 Hz).
//! Matches Unity snapshot heading units: 1024 = full turn.

const FIX_ONE: i32 = 65536;
const MAX_SPEED_CENTI: i32 = 5000; // 50 world units/s
const ACCEL_CENTI: i32 = 1500; // 15 units/s^2 forward
const REVERSE_ACCEL_CENTI: i32 = 400; // 4 units/s^2 reverse
const TURN_DEG_PER_SEC: i32 = 800;
const HEADING_UNITS: i32 = 1024;
const DRAG_PER_SEC: i32 = 3;

static SIN_TABLE: [i32; 256] = build_sin_table();

const fn build_sin_table() -> [i32; 256] {
    let mut t = [0i32; 256];
    let mut i = 0usize;
    while i < 256 {
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

fn sin_cos(heading: u16) -> (i32, i32) {
    let idx = (heading as usize) >> 2;
    let frac = (heading & 3) as i32;
    let sin0 = SIN_TABLE[idx & 255];
    let sin1 = SIN_TABLE[(idx + 1) & 255];
    let sin_v = sin0 + (sin1 - sin0) * frac / 4;
    let cos0 = SIN_TABLE[(idx + 64) & 255];
    let cos1 = SIN_TABLE[(idx + 65) & 255];
    let cos_v = cos0 + (cos1 - cos0) * frac / 4;
    (sin_v, cos_v)
}

fn isqrt64(n: i64) -> i64 {
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

/// Integrate one ship for a single tick. Velocity is centi-units per second.
pub fn integrate_ship(
    pos_x: i16,
    pos_z: i16,
    heading: u16,
    vel_x: &mut i32,
    vel_z: &mut i32,
    throttle: i8,
    steer: i8,
    tick_hz: u8,
) -> (i16, i16, u16) {
    let hz = tick_hz.max(1) as i32;
    let mut heading = heading;

    if steer != 0 {
        let deg_per_tick = TURN_DEG_PER_SEC * (steer as i32) / (127 * hz);
        let heading_delta = deg_per_tick * HEADING_UNITS / 360;
        heading = heading.wrapping_add(heading_delta as u16);
    }

    let (sin_h, cos_h) = sin_cos(heading);
    // Unity ships face -Z at heading 0.
    let forward_x = sin_h;
    let forward_z = -cos_h;

    if throttle != 0 {
        let accel = if throttle > 0 {
            ACCEL_CENTI
        } else {
            REVERSE_ACCEL_CENTI
        };
        let t = throttle as i64;
        let denom = 127i64 * FIX_ONE as i64 * hz as i64;
        *vel_x += ((forward_x as i64) * t * accel as i64 / denom) as i32;
        *vel_z += ((forward_z as i64) * t * accel as i64 / denom) as i32;
    }

    let drag_num = 1000 - (DRAG_PER_SEC * 100) / hz;
    *vel_x = ((*vel_x as i64) * drag_num as i64 / 1000) as i32;
    *vel_z = ((*vel_z as i64) * drag_num as i64 / 1000) as i32;

    let speed_sq = (*vel_x as i64) * (*vel_x as i64) + (*vel_z as i64) * (*vel_z as i64);
    let max_sq = (MAX_SPEED_CENTI as i64) * (MAX_SPEED_CENTI as i64);
    if speed_sq > max_sq {
        let speed = isqrt64(speed_sq);
        if speed > 0 {
            *vel_x = ((*vel_x as i64) * MAX_SPEED_CENTI as i64 / speed) as i32;
            *vel_z = ((*vel_z as i64) * MAX_SPEED_CENTI as i64 / speed) as i32;
        }
    }

    let new_x = (pos_x as i64) + (*vel_x as i64) / (100 * hz as i64);
    let new_z = (pos_z as i64) + (*vel_z as i64) / (100 * hz as i64);

    (
        new_x.clamp(-32768, 32767) as i16,
        new_z.clamp(-32768, 32767) as i16,
        heading,
    )
}
