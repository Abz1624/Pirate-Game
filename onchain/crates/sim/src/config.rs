//! Game configuration tables — weapons, enemies, tides, party scaling.

use crate::fix::Fix;

pub const MAX_WEAPON_ROWS: usize = 64;
pub const MAX_PASSIVE_ROWS: usize = 32;
pub const MAX_ENEMY_ARCHETYPES: usize = 8;
pub const MAX_TIDE_LEVELS: usize = 12;
pub const MAX_PARTY_SCALES: usize = 4;

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
#[repr(u8)]
pub enum ExecutorKind {
    #[default]
    None = 0,
    Projectile = 1,
    InstantArc = 2,
    AreaAtPoint = 3,
    Orbital = 4,
    Trail = 5,
    Aura = 6,
    Chain = 7,
}

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
#[repr(u8)]
pub enum ArcFamily {
    #[default]
    Broadside = 0,
    Chaser = 1,
    Deck = 2,
    Trailing = 3,
    Orbital = 4,
}

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
#[repr(u8)]
pub enum Element {
    #[default]
    Neutral = 0,
    Fire = 1,
    Water = 2,
    Air = 3,
    Earth = 4,
    Lightning = 5,
}

/// Elemental affinity: attacker vs defender -> damage multiplier (Q16.16, 1.0 = 65536).
pub fn affinity_multiplier(attacker: Element, defender: Element) -> Fix {
    use Element::*;
    let beats = match attacker {
        Water => Some(Fire),
        Fire => Some(Air),
        Air => Some(Earth),
        Earth => Some(Lightning),
        Lightning => Some(Water),
        Neutral => None,
    };
    let beaten_by = match defender {
        Water => Some(Lightning),
        Fire => Some(Water),
        Air => Some(Fire),
        Earth => Some(Air),
        Lightning => Some(Earth),
        Neutral => None,
    };
    if beats == Some(defender) {
        Fix(98304) // 1.5x
    } else if beaten_by == Some(attacker) {
        Fix(42598) // 0.65x
    } else {
        Fix(65536) // 1.0x
    }
}

#[derive(Clone, Copy, Debug, Default)]
pub struct WeaponRow {
    pub id: u8,
    pub executor: ExecutorKind,
    pub arc_family: ArcFamily,
    pub damage: u16,
    pub fire_interval_ticks: u8,
    pub projectile_count: u8,
    pub pierce: u8,
    pub speed: Fix,
    pub range: Fix,
    pub knockback: Fix,
    pub element: Element,
    pub arc_width_deg: u8,
    pub chain_count: u8,
    pub radius: Fix,
    pub blocks_projectiles: bool,
}

#[derive(Clone, Copy, Debug, Default)]
pub struct PassiveRow {
    pub id: u8,
    pub damage_pct: i16,
    pub fire_rate_pct: i16,
    pub turn_rate_pct: i16,
    pub speed_pct: i16,
    pub max_hp_pct: i16,
    pub range_pct: i16,
    pub pickup_pct: i16,
}

#[derive(Clone, Copy, Debug, Default)]
pub struct EnemyArchetypeRow {
    pub id: u8,
    pub base_hp: u16,
    pub base_speed: Fix,
    pub damage: u16,
    pub behaviour: u8, // maps to EnemyBehaviour
    pub element: Element,
    pub spawn_weight: u16,
}

#[derive(Clone, Copy, Debug, Default)]
pub struct TideRow {
    pub enemy_hp_mult: Fix,
    pub density_mult: Fix,
    pub elite_rate_pct: u8,
    pub reward_mult: Fix,
    pub max_enemies: u16,
}

#[derive(Clone, Copy, Debug, Default)]
pub struct PartyScaleRow {
    pub density_mult: Fix,
    pub hp_mult: Fix,
    pub boss_hp_mult: Fix,
    pub reward_per_player_pct: u8,
}

#[derive(Clone, Debug)]
pub struct GameConfig {
    pub lobby_capacity_default: u8,
    pub tick_hz: u8,
    pub convoy_radius: Fix,
    pub convoy_buff_pct: u8,
    pub convoy_buff_cap_pct: u8,
    pub weapons: Vec<WeaponRow>,
    pub passives: Vec<PassiveRow>,
    pub enemies: Vec<EnemyArchetypeRow>,
    pub tides: Vec<TideRow>,
    pub party_scales: Vec<PartyScaleRow>,
    pub xp_base: u32,
    pub xp_exponent_q8: u16, // n^1.35 approximated
}

impl Default for GameConfig {
    fn default() -> Self {
        Self::dev_defaults()
    }
}

impl GameConfig {
    pub fn dev_defaults() -> Self {
        let mut cfg = GameConfig {
            lobby_capacity_default: 4,
            tick_hz: 10,
            convoy_radius: Fix::from_int(15),
            convoy_buff_pct: 8,
            convoy_buff_cap_pct: 24,
            weapons: Vec::new(),
            passives: Vec::new(),
            enemies: Vec::new(),
            tides: Vec::new(),
            party_scales: Vec::new(),
            xp_base: 100,
            xp_exponent_q8: 346, // ~1.35 * 256
        };
        cfg.weapons.push(WeaponRow {
            id: 1,
            executor: ExecutorKind::Projectile,
            arc_family: ArcFamily::Broadside,
            damage: 12,
            fire_interval_ticks: 10,
            projectile_count: 1,
            pierce: 0,
            speed: Fix::from_int(24),
            range: Fix::from_int(30),
            knockback: Fix::from_int(2),
            element: Element::Neutral,
            arc_width_deg: 90,
            ..Default::default()
        });
        cfg.enemies.push(EnemyArchetypeRow {
            id: 1,
            base_hp: 30,
            base_speed: Fix::from_int(8),
            damage: 5,
            behaviour: 0, // Swarmer
            element: Element::Neutral,
            spawn_weight: 100,
        });
        cfg.enemies.push(EnemyArchetypeRow {
            id: 2,
            base_hp: 50,
            base_speed: Fix::from_int(12),
            damage: 8,
            behaviour: 2, // Ranged
            element: Element::Water,
            spawn_weight: 20,
        });
        for t in 1..=8u8 {
            let mult = Fix::from_int(100 + (t as i32 - 1) * 35) / Fix::from_int(100);
            cfg.tides.push(TideRow {
                enemy_hp_mult: mult,
                density_mult: Fix::from_int(100 + (t as i32 - 1) * 25) / Fix::from_int(100),
                elite_rate_pct: 3 + t * 2,
                reward_mult: mult,
                max_enemies: (60 + t as u16 * 20).min(200),
            });
        }
        cfg.party_scales = vec![
            PartyScaleRow {
                density_mult: Fix::ONE,
                hp_mult: Fix::ONE,
                boss_hp_mult: Fix::ONE,
                reward_per_player_pct: 100,
            },
            PartyScaleRow {
                density_mult: Fix::from_int(175) / Fix::from_int(100),
                hp_mult: Fix::from_int(110) / Fix::from_int(100),
                boss_hp_mult: Fix::from_int(190) / Fix::from_int(100),
                reward_per_player_pct: 95,
            },
            PartyScaleRow {
                density_mult: Fix::from_int(245) / Fix::from_int(100),
                hp_mult: Fix::from_int(120) / Fix::from_int(100),
                boss_hp_mult: Fix::from_int(275) / Fix::from_int(100),
                reward_per_player_pct: 92,
            },
            PartyScaleRow {
                density_mult: Fix::from_int(310) / Fix::from_int(100),
                hp_mult: Fix::from_int(130) / Fix::from_int(100),
                boss_hp_mult: Fix::from_int(355) / Fix::from_int(100),
                reward_per_player_pct: 90,
            },
        ];
        cfg
    }

    pub fn xp_for_level(&self, level: u8) -> u32 {
        let n = level.max(1) as u32;
        self.xp_base * n * n / 256 * self.xp_exponent_q8 as u32 / 256
    }
}

