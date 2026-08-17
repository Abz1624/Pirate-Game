//! Q16.16 fixed-point arithmetic.

use core::ops::{Add, AddAssign, Div, Mul, Neg, Sub, SubAssign};

/// Signed 16.16 fixed-point number.
#[derive(Copy, Clone, Debug, Default, PartialEq, Eq, PartialOrd, Ord, Hash)]
#[repr(transparent)]
pub struct Fix(pub i32);

pub const FIX_ONE: Fix = Fix(1 << 16);
pub const FIX_HALF: Fix = Fix(1 << 15);

impl Fix {
    pub const ZERO: Fix = Fix(0);
    pub const ONE: Fix = FIX_ONE;

    #[inline]
    pub const fn from_int(n: i32) -> Self {
        Fix(n << 16)
    }

    #[inline]
    pub const fn raw(self) -> i32 {
        self.0
    }

    #[inline]
    pub fn to_int(self) -> i32 {
        self.0 >> 16
    }

    #[inline]
    pub fn abs(self) -> Self {
        Fix(if self.0 == i32::MIN { i32::MAX } else { self.0.abs() })
    }

    #[inline]
    pub fn clamp(self, min: Fix, max: Fix) -> Self {
        if self < min {
            min
        } else if self > max {
            max
        } else {
            self
        }
    }

    /// Multiply two Fix values using i64 intermediate.
    #[inline]
    pub fn mul_fix(self, rhs: Fix) -> Fix {
        Fix(((self.0 as i64 * rhs.0 as i64) >> 16) as i32)
    }

    /// Divide two Fix values.
    #[inline]
    pub fn div_fix(self, rhs: Fix) -> Fix {
        if rhs.0 == 0 {
            return Fix::ZERO;
        }
        Fix(((self.0 as i64) << 16).div_euclid(rhs.0 as i64) as i32)
    }

    #[inline]
    pub fn sqrt(self) -> Fix {
        if self.0 <= 0 {
            return Fix::ZERO;
        }
        // Q16.16 sqrt: sqrt(raw / 2^16) * 2^16 = sqrt(raw * 2^16).
        let n = (self.0 as u64) << 16;
        let mut x = n;
        let mut y = (x + 1) >> 1;
        while y < x {
            x = y;
            y = (x + n / x) >> 1;
        }
        Fix(x as i32)
    }

    pub fn sin_cos(angle: u16) -> (Fix, Fix) {
        crate::trig::sin_cos(angle)
    }
}

impl Add for Fix {
    type Output = Self;
    fn add(self, rhs: Self) -> Self {
        Fix(self.0.saturating_add(rhs.0))
    }
}

impl Sub for Fix {
    type Output = Self;
    fn sub(self, rhs: Self) -> Self {
        Fix(self.0.saturating_sub(rhs.0))
    }
}

impl Mul<i32> for Fix {
    type Output = Self;
    fn mul(self, rhs: i32) -> Self {
        Fix(self.0.saturating_mul(rhs))
    }
}

impl Div<Fix> for Fix {
    type Output = Self;
    fn div(self, rhs: Fix) -> Self {
        self.div_fix(rhs)
    }
}

impl Div<i32> for Fix {
    type Output = Self;
    fn div(self, rhs: i32) -> Self {
        if rhs == 0 {
            Fix::ZERO
        } else {
            Fix(self.0 / rhs)
        }
    }
}

impl Neg for Fix {
    type Output = Self;
    fn neg(self) -> Self {
        Fix(-self.0)
    }
}

impl AddAssign for Fix {
    fn add_assign(&mut self, rhs: Self) {
        *self = *self + rhs;
    }
}

impl SubAssign for Fix {
    fn sub_assign(&mut self, rhs: Self) {
        *self = *self - rhs;
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn mul_div_roundtrip() {
        let a = Fix::from_int(3);
        let b = Fix::from_int(7);
        assert_eq!(a.mul_fix(b), Fix::from_int(21));
    }
}
