//! Xorshift128 PRNG for deterministic spawn jitter.

#[derive(Clone, Copy, Debug, Default)]
pub struct Prng {
    state: [u32; 4],
}

impl Prng {
    pub fn new(seed: u64) -> Self {
        let mut s = [
            (seed as u32) ^ 0x9E37_79B9,
            ((seed >> 32) as u32) ^ 0x85EB_CA6B,
            (seed as u32).wrapping_mul(0xC2B2_AE35),
            ((seed >> 32) as u32).wrapping_mul(0x27D4_EB2F),
        ];
        for _ in 0..8 {
            let _ = Self { state: s }.next_u32();
        }
        Self { state: s }
    }

    pub fn next_u32(&mut self) -> u32 {
        let mut t = self.state[3];
        let s = self.state[0];
        self.state[3] = self.state[2];
        self.state[2] = self.state[1];
        self.state[1] = s;
        t ^= t << 11;
        t ^= t >> 8;
        self.state[0] = t ^ s ^ (s >> 19);
        self.state[0]
    }

    pub fn next_u64(&mut self) -> u64 {
        let hi = self.next_u32() as u64;
        let lo = self.next_u32() as u64;
        (hi << 32) | lo
    }

    /// Inclusive range [min, max].
    pub fn range_i32(&mut self, min: i32, max: i32) -> i32 {
        if min >= max {
            return min;
        }
        let span = (max - min + 1) as u32;
        min + (self.next_u32() % span) as i32
    }

    pub fn range_fix(&mut self, min: crate::fix::Fix, max: crate::fix::Fix) -> crate::fix::Fix {
        crate::fix::Fix(self.range_i32(min.0, max.0))
    }
}
