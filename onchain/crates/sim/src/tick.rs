//! Four-phase tick pipeline.

use crate::config::GameConfig;
use crate::enemies::{rebuild_grid, tick_enemies};
use crate::executors::{ExecutorContext, tick_weapons};
use crate::fix::Fix;
use crate::pools::Vec2;
use crate::snapshot::{hp_pct, quantize_pos, SnapshotEnemy, SnapshotPlayer, SnapshotProjectile};
use crate::status::StatusFlags;
use crate::trig::sin_cos;
use crate::world::{MatchPhase, MatchWorld, ShipInput};

#[derive(Clone, Copy, Debug, Default)]
pub struct TickResult {
    pub cu_estimate: u32,
    pub state_hash: u64,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum TickPhase {
    InputsAndShips = 1,
    Enemies = 2,
    Weapons = 3,
    Resolve = 4,
}

pub fn run_full_tick(world: &mut MatchWorld, config: &GameConfig) -> TickResult {
    phase_inputs_and_ships(world, config);
    phase_enemies(world, config);
    phase_weapons(world, config);
    phase_resolve(world, config);
    world.tick += 1;
    TickResult {
        cu_estimate: estimate_cu(world),
        state_hash: world.state_hash(),
    }
}

pub fn run_phase(world: &mut MatchWorld, config: &GameConfig, phase: TickPhase) {
    match phase {
        TickPhase::InputsAndShips => phase_inputs_and_ships(world, config),
        TickPhase::Enemies => phase_enemies(world, config),
        TickPhase::Weapons => phase_weapons(world, config),
        TickPhase::Resolve => {
            phase_resolve(world, config);
            world.tick += 1;
        }
    }
}

fn phase_inputs_and_ships(world: &mut MatchWorld, config: &GameConfig) {
    if world.phase != MatchPhase::Running {
        return;
    }
    let dt = Fix::from_int(1) / Fix::from_int(config.tick_hz as i32);
    for (i, input) in world.pending_inputs.iter().enumerate() {
        if !world.players[i].active {
            continue;
        }
        integrate_ship(&mut world.players[i], *input, dt);
        crate::world::clamp_position_to_arena(&mut world.players[i].position);
    }
    rebuild_grid(world);
}

pub fn integrate_ship(player: &mut crate::world::PlayerState, input: ShipInput, dt: Fix) {
    if player.status.flags.contains(StatusFlags::DOWNED) {
        return;
    }
    let forward_speed = player.velocity.x; // simplified
    let turn_eff = if forward_speed.0.abs() > 0 {
        Fix::ONE
    } else {
        Fix::ZERO
    };
    if input.steer != 0 && turn_eff.0 > 0 {
        let steer = Fix::from_int(input.steer as i32);
        player.heading = player
            .heading
            .wrapping_add((steer.mul_fix(player.turn_rate).mul_fix(dt).to_int() * 1024 / 360) as u16);
    }
    let (fx, fz) = sin_cos(player.heading);
    let forward = Vec2::new(fx, fz);
    if input.throttle != 0 {
        let thrust = if input.throttle > 0 {
            player.accel
        } else {
            player.accel / Fix::from_int(4)
        };
        let t = Fix::from_int(input.throttle as i32);
        player.velocity.x = player.velocity.x + forward.x.mul_fix(t).mul_fix(thrust).mul_fix(dt);
        player.velocity.z = player.velocity.z + forward.z.mul_fix(t).mul_fix(thrust).mul_fix(dt);
    }
    let drag = Fix::ONE - player.water_drag.mul_fix(dt) / Fix::from_int(10);
    player.velocity.x = player.velocity.x.mul_fix(drag);
    player.velocity.z = player.velocity.z.mul_fix(drag);
    let speed = Fix(player.velocity.x.0 * player.velocity.x.0 + player.velocity.z.0 * player.velocity.z.0).sqrt();
    if speed.0 > player.max_speed.0 {
        let scale = player.max_speed.div_fix(speed);
        player.velocity.x = player.velocity.x.mul_fix(scale);
        player.velocity.z = player.velocity.z.mul_fix(scale);
    }
    player.position.x = player.position.x + player.velocity.x.mul_fix(dt);
    player.position.z = player.position.z + player.velocity.z.mul_fix(dt);
}

fn phase_enemies(world: &mut MatchWorld, config: &GameConfig) {
    let mut prng = world.prng;
    let mut ctx = ExecutorContext {
        world,
        config,
        prng: &mut prng,
    };
    tick_enemies(&mut ctx);
    world.prng = prng;
}

fn phase_weapons(world: &mut MatchWorld, config: &GameConfig) {
    let mut prng = world.prng;
    let mut ctx = ExecutorContext {
        world,
        config,
        prng: &mut prng,
    };
    tick_weapons(&mut ctx);
    world.prng = prng;
}

fn phase_resolve(world: &mut MatchWorld, config: &GameConfig) {
    // Status ticks, area effects, XP
    for p in &mut world.players {
        p.status.tick(world.tick);
    }
    let mut to_despawn = Vec::new();
    world.areas.for_each_alive(|aid, area| {
        if area.ttl_ticks <= 1 {
            to_despawn.push(aid);
        }
    });
    for aid in to_despawn {
        world.areas.despawn(aid);
    }
    world.areas.for_each_alive_mut(|_, area| {
        area.ttl_ticks = area.ttl_ticks.saturating_sub(1);
    });
    write_snapshot(world);
    let _ = config;
}

fn write_snapshot(world: &mut MatchWorld) {
    world.snapshot.tick = world.tick;
    world.snapshot.enemies.clear();
    world.snapshot.enemy_projectiles.clear();
    for (i, p) in world.players.iter().enumerate() {
        if i < world.snapshot.players.len() {
            world.snapshot.players[i] = SnapshotPlayer {
                slot: i as u8,
                x: quantize_pos(p.position.x),
                z: quantize_pos(p.position.z),
                heading: p.heading,
                hp_pct: hp_pct(p.hp, p.max_hp),
                level: p.level,
                downed: p.status.flags.contains(StatusFlags::DOWNED),
            };
        }
    }
    world.enemies.for_each_alive(|eid, e| {
        world.snapshot.enemies.push(SnapshotEnemy {
            id: eid,
            archetype: e.archetype_id,
            x: quantize_pos(e.position.x),
            z: quantize_pos(e.position.z),
            hp_pct: hp_pct(e.hp, e.max_hp),
            element: e.element,
        });
    });
    world.projectiles.for_each_alive(|pid, proj| {
        if !proj.from_player {
            world.snapshot.enemy_projectiles.push(SnapshotProjectile {
                id: pid,
                x: quantize_pos(proj.position.x),
                z: quantize_pos(proj.position.z),
                vx: (proj.velocity.x.to_int() * 10) as i16,
                vz: (proj.velocity.z.to_int() * 10) as i16,
            });
        }
    });
}

/// Rough CU estimate for budget testing (calibrated by spike).
pub fn estimate_cu(world: &MatchWorld) -> u32 {
    let players = world.player_count as u32;
    let mut enemies = 0u32;
    world.enemies.for_each_alive(|_, _| enemies += 1);
    let mut projectiles = 0u32;
    world.projectiles.for_each_alive(|_, _| projectiles += 1);
    5000
        + players * 800
        + enemies * 350
        + projectiles * 120
        + enemies * players * 40
}

/// Worst-case stress tick for CU spike / CI gate.
pub fn stress_tick(capacity: u8, enemy_count: usize, projectile_count: usize) -> u32 {
    let mut config = GameConfig::default();
    config.tick_hz = 10;
    let mut world = MatchWorld::new(42, 8, capacity, &config);
    world.phase = MatchPhase::Running;
    for i in 0..capacity {
        world.join_player(i);
    }
    for _ in 0..enemy_count {
        if let Some(eid) = world.enemies.spawn(0) {
            world.enemies.slots[eid as usize] = crate::world::EnemyState {
                archetype_id: 1,
                hp: 100,
                max_hp: 100,
                position: Vec2::new(
                    Fix::from_int((eid % 20) as i32 * 3),
                    Fix::from_int((eid / 20) as i32 * 3),
                ),
                ..Default::default()
            };
        }
    }
    for _ in 0..projectile_count {
        if let Some(pid) = world.projectiles.spawn(0) {
            world.projectiles.slots[pid as usize] = crate::world::ProjectileState {
                from_player: true,
                position: Vec2::new(Fix::from_int(1), Fix::from_int(1)),
                velocity: Vec2::new(Fix::from_int(10), Fix::ZERO),
                range_left: Fix::from_int(30),
                damage: 10,
                ..Default::default()
            };
        }
    }
    let r = run_full_tick(&mut world, &config);
    r.cu_estimate
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn golden_tick_hash_stable() {
        let config = GameConfig::default();
        let mut world = MatchWorld::new(12345, 1, 4, &config);
        world.phase = MatchPhase::Running;
        for i in 0..4 {
            world.join_player(i);
        }
        let h0 = run_full_tick(&mut world, &config).state_hash;
        let mut world2 = MatchWorld::new(12345, 1, 4, &config);
        world2.phase = MatchPhase::Running;
        for i in 0..4 {
            world2.join_player(i);
        }
        let h1 = run_full_tick(&mut world2, &config).state_hash;
        assert_eq!(h0, h1);
    }

    #[test]
    fn stress_4_players_under_budget() {
        let cu = stress_tick(4, 140, 240);
        assert!(cu < 1_400_000, "CU {cu} exceeds 1.4M");
    }

    #[test]
    fn stress_12_players_under_budget() {
        let cu = stress_tick(12, 200, 720);
        assert!(cu < 1_400_000, "CU {cu} exceeds 1.4M");
    }
}
