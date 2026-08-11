//! Enemy AI archetypes and spawning.

use crate::config::GameConfig;
use crate::executors::ExecutorContext;
use crate::fix::Fix;
use crate::pools::{EntityId, EntityKind, Vec2};
use crate::trig::atan2;
use crate::world::{EnemyBehaviour, EnemyState, MatchPhase, MatchWorld};

pub fn tick_enemies(ctx: &mut ExecutorContext) {
    if ctx.world.phase != MatchPhase::Running {
        return;
    }
    spawn_enemies(ctx);
    let player_count = ctx.world.player_count as usize;
    let tick_hz = ctx.config.tick_hz as i32;
    let dt = Fix::from_int(1) / Fix::from_int(tick_hz);

    let mut updates: Vec<(EntityId, Vec2, Vec2, u16)> = Vec::new();
    let mut ranged_shots: Vec<(EntityId, Vec2, Vec2)> = Vec::new();
    ctx.world.enemies.for_each_alive(|eid, enemy| {
        let target_pos = nearest_player_pos(ctx.world, enemy.position);
        let mut vel = enemy.velocity;
        let mut pos = enemy.position;
        match enemy.behaviour {
            EnemyBehaviour::Swarmer => {
                steer_toward(&mut vel, pos, target_pos, enemy.archetype_id as i32 + 8, dt);
                pos.x += vel.x.mul_fix(dt);
                pos.z += vel.z.mul_fix(dt);
            }
            EnemyBehaviour::Charger => {
                // simplified wind-up dash
                steer_toward(&mut vel, pos, target_pos, 16, dt);
                pos.x += vel.x.mul_fix(dt * 2);
                pos.z += vel.z.mul_fix(dt * 2);
            }
            EnemyBehaviour::Ranged => {
                let dist = pos.dist_sq(target_pos);
                if dist < Fix::from_int(20).mul_fix(Fix::from_int(20)).0 as i64 {
                    flee(&mut vel, pos, target_pos, 10, dt);
                } else {
                    steer_toward(&mut vel, pos, target_pos, 6, dt);
                }
                pos.x += vel.x.mul_fix(dt);
                pos.z += vel.z.mul_fix(dt);
                ranged_shots.push((eid, pos, target_pos));
            }
            EnemyBehaviour::Orbiter => {
                orbit(&mut pos, target_pos, enemy.state_timer, dt);
            }
            EnemyBehaviour::Bruiser => {
                steer_toward(&mut vel, pos, target_pos, 4, dt);
                pos.x += vel.x.mul_fix(dt);
                pos.z += vel.z.mul_fix(dt);
            }
            EnemyBehaviour::Elite => {
                steer_toward(&mut vel, pos, target_pos, 10, dt);
                pos.x += vel.x.mul_fix(dt);
                pos.z += vel.z.mul_fix(dt);
            }
        }
        crate::world::clamp_position_to_arena(&mut pos);
        let heading = atan2(target_pos.z - pos.z, target_pos.x - pos.x);
        updates.push((eid, pos, vel, heading));
    });

    for (eid, pos, vel, heading) in updates {
        if let Some(e) = ctx.world.enemies.get_mut(eid) {
            e.position = pos;
            e.velocity = vel;
            e.heading = heading;
            e.state_timer = e.state_timer.saturating_add(1);
        }
    }
    for (eid, pos, target) in ranged_shots {
        try_enemy_shot(ctx, eid, pos, target);
    }
}

fn spawn_enemies(ctx: &mut ExecutorContext) {
    let tide_idx = ctx.world.tide.saturating_sub(1) as usize;
    let tide = ctx.config.tides.get(tide_idx).cloned().unwrap_or_default();
    let party_idx = (ctx.world.player_count.saturating_sub(1)).min(3) as usize;
    let party = ctx.config.party_scales.get(party_idx).cloned().unwrap_or_default();
    let target_count = ((tide.max_enemies as u32 * party.density_mult.0 as u32) >> 16) as usize;
    let mut alive = count_alive_enemies(ctx.world);
    if alive >= target_count {
        return;
    }
    let rate = Fix::from_int(2).mul_fix(tide.density_mult);
    ctx.world.spawn_accumulator = ctx.world.spawn_accumulator + rate / Fix::from_int(ctx.config.tick_hz as i32);
    while ctx.world.spawn_accumulator >= Fix::ONE {
        let alive_now = count_alive_enemies(ctx.world);
        if alive_now >= target_count {
            break;
        }
        ctx.world.spawn_accumulator = ctx.world.spawn_accumulator - Fix::ONE;
        spawn_one(ctx, &tide, party.hp_mult);
        alive = alive_now + 1;
    }
}

fn count_alive_enemies(world: &MatchWorld) -> usize {
    let mut n = 0;
    world.enemies.for_each_alive(|_, _| n += 1);
    n
}

fn spawn_one(ctx: &mut ExecutorContext, tide: &crate::config::TideRow, hp_mult: Fix) {
    let arch = ctx.config.enemies.first().cloned().unwrap_or_default();
    if let Some(eid) = ctx.world.enemies.spawn(ctx.world.tick) {
        let angle = ctx.world.prng.next_u32() % 1024;
        let (sx, sz) = crate::trig::sin_cos(angle as u16);
        let dist = Fix::from_int(ctx.world.prng.range_i32(40, 70));
        let pos = Vec2::new(sx.mul_fix(dist), sz.mul_fix(dist));
        let max_hp = ((arch.base_hp as u64 * tide.enemy_hp_mult.0 as u64 * hp_mult.0 as u64) >> 32) as u16;
        ctx.world.enemies.slots[eid as usize] = EnemyState {
            archetype_id: arch.id,
            behaviour: EnemyBehaviour::Swarmer,
            position: pos,
            hp: max_hp,
            max_hp,
            element: arch.element,
            ..Default::default()
        };
    }
}

fn nearest_player_pos(world: &MatchWorld, from: Vec2) -> Vec2 {
    let mut best = Vec2::default();
    let mut best_d = i64::MAX;
    for p in &world.players {
        if !p.active || p.status.flags.contains(crate::status::StatusFlags::DOWNED) {
            continue;
        }
        let d = from.dist_sq(p.position);
        if d < best_d {
            best_d = d;
            best = p.position;
        }
    }
    best
}

fn steer_toward(vel: &mut Vec2, from: Vec2, to: Vec2, speed: i32, dt: Fix) {
    let dx = to.x - from.x;
    let dz = to.z - from.z;
    let len = Fix(dx.0 * dx.0 + dz.0 * dz.0).sqrt().max(Fix::from_int(1));
    let spd = Fix::from_int(speed);
    vel.x = dx.div_fix(len).mul_fix(spd);
    vel.z = dz.div_fix(len).mul_fix(spd);
}

fn flee(vel: &mut Vec2, from: Vec2, threat: Vec2, speed: i32, _dt: Fix) {
    steer_toward(vel, threat, from, speed, Fix::ONE);
}

fn orbit(pos: &mut Vec2, center: Vec2, timer: u16, dt: Fix) {
    let angle = (timer as u32 * 8) % 1024;
    let (sx, sz) = crate::trig::sin_cos(angle as u16);
    let r = Fix::from_int(25);
    pos.x = center.x + sx.mul_fix(r);
    pos.z = center.z + sz.mul_fix(r);
    let _ = dt;
}

fn try_enemy_shot(ctx: &mut ExecutorContext, owner: EntityId, from: Vec2, to: Vec2) {
    if ctx.world.tick % 30 != 0 {
        return;
    }
    if let Some(pid) = ctx.world.projectiles.spawn(ctx.world.tick) {
        let dx = to.x - from.x;
        let dz = to.z - from.z;
        let len = Fix(dx.0 * dx.0 + dz.0 * dz.0).sqrt().max(Fix::from_int(1));
        let speed = Fix::from_int(18);
        ctx.world.projectiles.slots[pid as usize] = crate::world::ProjectileState {
            position: from,
            velocity: Vec2::new(dx.div_fix(len).mul_fix(speed), dz.div_fix(len).mul_fix(speed)),
            damage: 8,
            range_left: Fix::from_int(40),
            from_player: false,
            ..Default::default()
        };
        let _ = owner;
    }
}

pub fn rebuild_grid(world: &mut MatchWorld) {
    world.grid.clear();
    for (i, p) in world.players.iter().enumerate() {
        if p.active {
            world.grid.insert(
                p.position.x,
                p.position.z,
                i as u16,
                EntityKind::Player,
            );
        }
    }
    world.enemies.for_each_alive(|eid, e| {
        world
            .grid
            .insert(e.position.x, e.position.z, eid, EntityKind::Enemy);
    });
}
