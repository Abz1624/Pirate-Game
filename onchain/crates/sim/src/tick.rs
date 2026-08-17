//! Four-phase tick pipeline.

use crate::config::GameConfig;
use crate::enemies::{rebuild_grid, tick_enemies};
use crate::executors::{ExecutorContext, tick_weapons};
use crate::fix::Fix;
use crate::gameplay;
use crate::modes::PVP_MAX_PLAYERS;
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
    world.set_tick(world.tick() + 1);
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
            world.set_tick(world.tick() + 1);
        }
    }
}

fn phase_inputs_and_ships(world: &mut MatchWorld, config: &GameConfig) {
    if world.phase() != MatchPhase::Running {
        return;
    }
    let dt = Fix::from_int(1) / Fix::from_int(config.tick_hz as i32);
    for i in 0..PVP_MAX_PLAYERS {
        let input = world.inner.pending_inputs[i];
        if !world.players()[i].active {
            continue;
        }
        integrate_ship(&mut world.players_mut()[i], input, dt);
        crate::world::clamp_position_to_arena(&mut world.players_mut()[i].position);
    }
    rebuild_grid(world);
}

pub fn integrate_ship(player: &mut crate::world::PlayerState, input: ShipInput, dt: Fix) {
    if player.status.flags.contains(StatusFlags::DOWNED) {
        return;
    }
    let (fx, fz) = sin_cos(player.heading);
    // The model faces local -Z. Positive Unity yaw rotates that nose toward -X.
    let forward = Vec2::new(-fx, -fz);
    let forward_speed =
        player.velocity.x.mul_fix(forward.x) + player.velocity.z.mul_fix(forward.z);
    let reverse_max = Fix::from_int(15);
    let turn_reference = if forward_speed.0 >= 0 {
        player.max_speed
    } else {
        reverse_max
    };
    let mut turn_eff = Fix::ZERO;
    if turn_reference.0 > 0 {
        turn_eff = forward_speed.abs().div_fix(turn_reference);
        if turn_eff.0 > Fix::ONE.0 {
            turn_eff = Fix::ONE;
        }
    }
    if input.steer != 0 && turn_eff.0 > 0 {
        let steer = Fix::from_int(input.steer as i32).div_fix(Fix::from_int(127));
        let heading_delta = steer
            .mul_fix(player.turn_rate)
            .mul_fix(turn_eff)
            .mul_fix(dt)
            .mul_fix(Fix::from_int(1024))
            .div_fix(Fix::from_int(360));
        player.heading =
            (player.heading as i32 + heading_delta.to_int()).rem_euclid(1024) as u16;
    }
    if input.throttle != 0 {
        let (thrust_fx, thrust_fz) = sin_cos(player.heading);
        let thrust_forward = Vec2::new(-thrust_fx, -thrust_fz);
        let thrust = if input.throttle > 0 {
            player.accel
        } else {
            Fix::from_int(4)
        };
        let t = Fix::from_int(input.throttle as i32).div_fix(Fix::from_int(127));
        player.velocity.x =
            player.velocity.x + thrust_forward.x.mul_fix(t).mul_fix(thrust).mul_fix(dt);
        player.velocity.z =
            player.velocity.z + thrust_forward.z.mul_fix(t).mul_fix(thrust).mul_fix(dt);

        let fwd_speed = player.velocity.x.mul_fix(thrust_forward.x)
            + player.velocity.z.mul_fix(thrust_forward.z);
        let opposing = (input.throttle > 0 && fwd_speed.0 < 0)
            || (input.throttle < 0 && fwd_speed.0 > 0);
        if opposing && fwd_speed.0.abs() > 0 {
            let brake = Fix::ONE - Fix::from_int(6).mul_fix(dt);
            player.velocity.x = player.velocity.x.mul_fix(brake);
            player.velocity.z = player.velocity.z.mul_fix(brake);
        }
    }
    let drag = (Fix::ONE - player.water_drag.mul_fix(dt)).clamp(Fix::ZERO, Fix::ONE);
    player.velocity.x = player.velocity.x.mul_fix(drag);
    player.velocity.z = player.velocity.z.mul_fix(drag);
    let speed = (player.velocity.x.mul_fix(player.velocity.x)
        + player.velocity.z.mul_fix(player.velocity.z))
    .sqrt();
    if speed.0 > player.max_speed.0 {
        let scale = player.max_speed.div_fix(speed);
        player.velocity.x = player.velocity.x.mul_fix(scale);
        player.velocity.z = player.velocity.z.mul_fix(scale);
    }
    player.position.x = player.position.x + player.velocity.x.mul_fix(dt);
    player.position.z = player.position.z + player.velocity.z.mul_fix(dt);
}

fn phase_enemies(world: &mut MatchWorld, config: &GameConfig) {
    let mut prng = world.inner.prng();
    let mut ctx = ExecutorContext {
        world,
        config,
        prng: &mut prng,
    };
    tick_enemies(&mut ctx);
    world.inner.set_prng(prng);
}

fn phase_weapons(world: &mut MatchWorld, config: &GameConfig) {
    let mut prng = world.inner.prng();
    let mut ctx = ExecutorContext {
        world,
        config,
        prng: &mut prng,
    };
    tick_weapons(&mut ctx);
    world.inner.set_prng(prng);
}

fn phase_resolve(world: &mut MatchWorld, config: &GameConfig) {
    gameplay::tick_match_timer(world, config);

    let tick = world.tick();
    for p in world.players_mut().iter_mut() {
        p.status.tick(tick);
    }
    let mut to_despawn = [0u16; 16];
    let mut despawn_n = 0usize;
    world.areas().for_each_alive(|aid, area| {
        if area.ttl_ticks <= 1 && despawn_n < to_despawn.len() {
            to_despawn[despawn_n] = aid;
            despawn_n += 1;
        }
    });
    for i in 0..despawn_n {
        world.areas_mut().despawn(to_despawn[i]);
    }
    world.areas_mut().for_each_alive_mut(|_, area| {
        area.ttl_ticks = area.ttl_ticks.saturating_sub(1);
    });
    write_snapshot(world);
}

fn write_snapshot(world: &mut MatchWorld) {
    world.snapshot.tick = world.tick();
    world.snapshot.enemies.clear();
    world.snapshot.enemy_projectiles.clear();

    let player_rows: Vec<SnapshotPlayer> = world
        .players()
        .iter()
        .enumerate()
        .filter(|(i, _)| *i < world.snapshot.players.len())
        .map(|(i, p)| SnapshotPlayer {
            slot: i as u8,
            x: quantize_pos(p.position.x),
            z: quantize_pos(p.position.z),
            heading: p.heading,
            hp_pct: hp_pct(p.hp, p.max_hp),
            level: p.level,
            downed: p.status.flags.contains(StatusFlags::DOWNED),
            bank: p.bank,
            kills: p.player_kills,
        })
        .collect();
    for (i, row) in player_rows.into_iter().enumerate() {
        if i < world.snapshot.players.len() {
            world.snapshot.players[i] = row;
        }
    }

    let mut enemy_rows = Vec::new();
    world.enemies().for_each_alive(|eid, e| {
        enemy_rows.push(SnapshotEnemy {
            id: eid,
            archetype: e.archetype_id,
            x: quantize_pos(e.position.x),
            z: quantize_pos(e.position.z),
            hp_pct: hp_pct(e.hp, e.max_hp),
            element: e.element,
        });
    });
    for row in enemy_rows {
        world.snapshot.enemies.push(row);
    }

    let mut projectile_rows = Vec::new();
    world.projectiles().for_each_alive(|pid, proj| {
        if !proj.from_player {
            projectile_rows.push(SnapshotProjectile {
                id: pid,
                x: quantize_pos(proj.position.x),
                z: quantize_pos(proj.position.z),
                vx: (proj.velocity.x.to_int() * 10) as i16,
                vz: (proj.velocity.z.to_int() * 10) as i16,
            });
        }
    });
    for row in projectile_rows {
        world.snapshot.enemy_projectiles.push(row);
    }
}

/// Rough CU estimate for budget testing (calibrated by spike).
pub fn estimate_cu(world: &MatchWorld) -> u32 {
    let players = world.player_count() as u32;
    let mut enemies = 0u32;
    world.enemies().for_each_alive(|_, _| enemies += 1);
    let mut projectiles = 0u32;
    world.projectiles().for_each_alive(|_, _| projectiles += 1);
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
    world.set_phase(MatchPhase::Running);
    for i in 0..capacity {
        world.join_player(i);
    }
    for _ in 0..enemy_count {
        if let Some(eid) = world.enemies_mut().spawn(0) {
            world.enemies_mut().slots[eid as usize] = crate::world::EnemyState {
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
        if let Some(pid) = world.projectiles_mut().spawn(0) {
            world.projectiles_mut().slots[pid as usize] = crate::world::ProjectileState {
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
        world.set_phase(MatchPhase::Running);
        for i in 0..4 {
            world.join_player(i);
        }
        let h0 = run_full_tick(&mut world, &config).state_hash;
        let mut world2 = MatchWorld::new(12345, 1, 4, &config);
        world2.set_phase(MatchPhase::Running);
        for i in 0..4 {
            world2.join_player(i);
        }
        let h1 = run_full_tick(&mut world2, &config).state_hash;
        assert_eq!(h0, h1);
    }

    #[test]
    fn steering_changes_heading_in_one_tick() {
        let mut player = crate::world::PlayerState::default();
        player.velocity.z = Fix::from_int(-5);
        let start_heading = player.heading;
        integrate_ship(
            &mut player,
            ShipInput {
                throttle: 127,
                steer: 127,
                buttons: 0,
            },
            Fix::from_int(1).div_fix(Fix::from_int(10)),
        );

        assert_ne!(
            player.heading, start_heading,
            "full steering must produce a non-zero quantized heading delta"
        );
        assert!(
            player.velocity.x.0 < 0,
            "positive Unity yaw must turn the model's -Z nose toward -X"
        );
    }

    #[test]
    fn releasing_throttle_damps_and_reverse_eventually_moves_backward() {
        let dt = Fix::from_int(1).div_fix(Fix::from_int(10));
        let forward = ShipInput {
            throttle: 127,
            steer: 0,
            buttons: 0,
        };
        let reverse = ShipInput {
            throttle: -127,
            steer: 0,
            buttons: 0,
        };
        let idle = ShipInput::default();

        let mut coasting = crate::world::PlayerState::default();
        for _ in 0..10 {
            integrate_ship(&mut coasting, forward, dt);
        }
        let powered_speed = coasting.velocity.z.abs();
        for _ in 0..10 {
            integrate_ship(&mut coasting, idle, dt);
        }
        assert!(
            coasting.velocity.z.abs().0 < powered_speed.0 / 10,
            "solo-style water drag must stop coasting promptly"
        );

        let mut reversing = crate::world::PlayerState::default();
        for _ in 0..10 {
            integrate_ship(&mut reversing, forward, dt);
        }
        for _ in 0..10 {
            integrate_ship(&mut reversing, reverse, dt);
        }
        assert!(
            reversing.velocity.z.0 > 0,
            "holding reverse must brake and then move backward"
        );
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
