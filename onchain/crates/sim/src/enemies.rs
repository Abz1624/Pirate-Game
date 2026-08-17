//! Enemy AI archetypes and spawning.

use crate::config::GameConfig;
use crate::executors::ExecutorContext;
use crate::fix::Fix;
use crate::pools::{EntityId, EntityKind, Vec2};
use crate::trig::atan2;
use crate::world::{EnemyBehaviour, EnemyState, MatchPhase, MatchWorld};

pub fn tick_enemies(ctx: &mut ExecutorContext) {
    if ctx.world.phase() != MatchPhase::Running {
        return;
    }
    spawn_enemies(ctx);
    let player_count = ctx.world.player_count() as usize;
    let tick_hz = ctx.config.tick_hz as i32;
    let dt = Fix::from_int(1) / Fix::from_int(tick_hz);

    let mut updates: Vec<(EntityId, Vec2, Vec2, u16)> = Vec::new();
    let mut ranged_shots: Vec<(EntityId, Vec2, Vec2)> = Vec::new();
    let players = ctx.world.players().clone();
    ctx.world.enemies_mut().for_each_alive(|eid, enemy| {
        let target_pos = nearest_player_pos(&players, enemy.position);
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
            EnemyBehaviour::Boss => {
                steer_toward(&mut vel, pos, target_pos, 6, dt);
                pos.x += vel.x.mul_fix(dt);
                pos.z += vel.z.mul_fix(dt);
            }
        }
        crate::world::clamp_position_to_arena(&mut pos);
        let heading = atan2(target_pos.z - pos.z, target_pos.x - pos.x);
        updates.push((eid, pos, vel, heading));
    });

    for (eid, pos, vel, heading) in updates {
        if let Some(e) = ctx.world.enemies_mut().get_mut(eid) {
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
    let tide_idx = ctx.world.header().tide.saturating_sub(1) as usize;
    let tide = ctx.config.tides.get(tide_idx).cloned().unwrap_or_default();
    let party_idx = (ctx.world.player_count().saturating_sub(1)).min(3) as usize;
    let party = ctx.config.party_scales.get(party_idx).cloned().unwrap_or_default();
    let target_count = ((tide.max_enemies as u32 * party.density_mult.0 as u32) >> 16) as usize;
    let mut alive = count_alive_enemies(ctx.world);
    if alive >= target_count {
        return;
    }
    let rate = Fix::from_int(2).mul_fix(tide.density_mult);
    let mut acc = ctx.world.inner.spawn_accumulator();
    acc = acc + rate / Fix::from_int(ctx.config.tick_hz as i32);
    while acc >= Fix::ONE {
        let alive_now = count_alive_enemies(ctx.world);
        if alive_now >= target_count {
            break;
        }
        acc = acc - Fix::ONE;
        spawn_one(ctx, &tide, party.hp_mult);
        alive = alive_now + 1;
    }
    ctx.world.inner.set_spawn_accumulator(acc);
}

fn count_alive_enemies(world: &MatchWorld) -> usize {
    let mut n = 0;
    world.enemies().for_each_alive(|_, _| n += 1);
    n
}

fn spawn_one(ctx: &mut ExecutorContext, tide: &crate::config::TideRow, hp_mult: Fix) {
    let arch = ctx.config.enemies.first().cloned().unwrap_or_default();
    let tick = ctx.world.tick();
    if let Some(eid) = ctx.world.enemies_mut().spawn(tick) {
        let angle = ctx.prng.next_u32() % 1024;
        let (sx, sz) = crate::trig::sin_cos(angle as u16);
        let dist = Fix::from_int(ctx.prng.range_i32(40, 70));
        let pos = Vec2::new(sx.mul_fix(dist), sz.mul_fix(dist));
        let max_hp = ((arch.base_hp as u64 * tide.enemy_hp_mult.0 as u64 * hp_mult.0 as u64) >> 32) as u16;
        ctx.world.enemies_mut().slots[eid as usize] = EnemyState {
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

fn nearest_player_pos(players: &[crate::world::PlayerState; crate::modes::PVP_MAX_PLAYERS], from: Vec2) -> Vec2 {
    let mut best = Vec2::default();
    let mut best_d = i64::MAX;
    for p in players.iter() {
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
    let len = (dx.mul_fix(dx) + dz.mul_fix(dz))
        .sqrt()
        .max(Fix::from_int(1));
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
    let tick = ctx.world.tick();
    if tick % 30 != 0 {
        return;
    }
    if let Some(pid) = ctx.world.projectiles_mut().spawn(tick) {
        let dx = to.x - from.x;
        let dz = to.z - from.z;
        let len = (dx.mul_fix(dx) + dz.mul_fix(dz))
            .sqrt()
            .max(Fix::from_int(1));
        let speed = Fix::from_int(18);
        ctx.world.projectiles_mut().slots[pid as usize] = crate::world::ProjectileState {
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
    let player_cells: Vec<(Fix, Fix, u16)> = world
        .players()
        .iter()
        .enumerate()
        .filter(|(_, p)| p.active)
        .map(|(i, p)| (p.position.x, p.position.z, i as u16))
        .collect();
    let mut enemy_cells: Vec<(Fix, Fix, u16)> = Vec::new();
    world.enemies().for_each_alive(|eid, e| {
        enemy_cells.push((e.position.x, e.position.z, eid));
    });
    world.grid.clear();
    for (x, z, id) in player_cells {
        world.grid.insert(x, z, id, EntityKind::Player);
    }
    for (x, z, eid) in enemy_cells {
        world.grid.insert(x, z, eid, EntityKind::Enemy);
    }
}
