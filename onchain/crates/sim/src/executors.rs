//! Weapon executors — data-driven combat resolution.

use crate::config::{ArcFamily, ExecutorKind, GameConfig, WeaponRow};
use crate::fix::Fix;
use crate::pools::{EntityId, EntityKind, Vec2};
use crate::prng::Prng;
use crate::snapshot::{GameEvent, GameEventKind};
use crate::status::knockback_velocity;
use crate::trig::{atan2, sin_cos};
use crate::world::{EnemyState, MatchWorld, PlayerState, ProjectileState};

pub struct ExecutorContext<'a> {
    pub world: &'a mut MatchWorld,
    pub config: &'a GameConfig,
    pub prng: &'a mut Prng,
}

pub fn tick_weapons(ctx: &mut ExecutorContext) {
    let player_count = ctx.world.player_count as usize;
    for slot in 0..player_count {
        let weapons: Vec<(u8, u8)> = {
            let p = &ctx.world.players[slot];
            p.weapon_slots
                .iter()
                .enumerate()
                .filter(|(_, w)| w.weapon_id > 0)
                .map(|(i, w)| (i as u8, w.weapon_id))
                .collect()
        };
        for (weapon_slot, weapon_id) in weapons {
            let row = ctx
                .config
                .weapons
                .iter()
                .find(|w| w.id == weapon_id)
                .cloned()
                .unwrap_or_default();
            if row.executor == ExecutorKind::None {
                continue;
            }
            let mut fire_cd = ctx.world.players[slot].weapon_cooldowns[weapon_slot as usize];
            if fire_cd > 0 {
                ctx.world.players[slot].weapon_cooldowns[weapon_slot as usize] = fire_cd - 1;
                continue;
            }
            if try_fire_weapon(ctx, slot as u8, weapon_slot, &row) {
                ctx.world.players[slot].weapon_cooldowns[weapon_slot as usize] =
                    row.fire_interval_ticks.max(1);
            }
        }
    }
    integrate_projectiles(ctx);
}

fn try_fire_weapon(
    ctx: &mut ExecutorContext,
    player_slot: u8,
    _weapon_slot: u8,
    row: &WeaponRow,
) -> bool {
    let slot = player_slot as usize;
    let heading = ctx.world.players[slot].heading;
    let pos = ctx.world.players[slot].position;
    match row.executor {
        ExecutorKind::Projectile | ExecutorKind::Chain => fire_projectile(ctx, slot, row, heading, pos),
        ExecutorKind::InstantArc => fire_instant_arc(ctx, slot, row, heading, pos),
        ExecutorKind::AreaAtPoint => fire_area(ctx, slot, row, pos),
        ExecutorKind::Orbital => true, // passive, handled in phase 1
        ExecutorKind::Trail => fire_trail(ctx, slot, row, pos),
        ExecutorKind::Aura => fire_aura(ctx, slot, row, pos),
        ExecutorKind::None => false,
    }
}

fn fire_projectile(
    ctx: &mut ExecutorContext,
    slot: usize,
    row: &WeaponRow,
    heading: u16,
    pos: Vec2,
) -> bool {
    let (dir_x, dir_z) = arc_direction(row.arc_family, heading, row.arc_width_deg);
    if dir_x.0 == 0 && dir_z.0 == 0 {
        return false;
    }
    for _ in 0..row.projectile_count.max(1) {
        if let Some(id) = ctx.world.projectiles.spawn(ctx.world.tick) {
            let p = ProjectileState {
                owner_slot: slot as u8,
                weapon_id: row.id,
                position: pos,
                velocity: Vec2::new(dir_x.mul_fix(row.speed), dir_z.mul_fix(row.speed)),
                damage: row.damage,
                pierce_left: row.pierce,
                range_left: row.range,
                element: row.element,
                from_player: true,
            };
            ctx.world.projectiles.slots[id as usize] = p;
            ctx.world.snapshot.push_event(GameEvent {
                kind: GameEventKind::Fire,
                tick: ctx.world.tick,
                actor: slot as u16,
                x: crate::snapshot::quantize_pos(pos.x),
                z: crate::snapshot::quantize_pos(pos.z),
                ..Default::default()
            });
        }
    }
    true
}

fn fire_instant_arc(
    ctx: &mut ExecutorContext,
    slot: usize,
    row: &WeaponRow,
    heading: u16,
    pos: Vec2,
) -> bool {
    let (dir_x, dir_z) = arc_direction(row.arc_family, heading, row.arc_width_deg);
    let mut hit_any = false;
    let mut hits: Vec<EntityId> = Vec::new();
    ctx.world.enemies.for_each_alive(|eid, enemy| {
        let to_enemy = Vec2::new(enemy.position.x - pos.x, enemy.position.z - pos.z);
        if in_arc(dir_x, dir_z, to_enemy, row.range) {
            hits.push(eid);
            hit_any = true;
        }
    });
    for eid in hits {
        apply_damage_to_enemy(ctx, slot, eid, row.damage, row.element, row.knockback, pos);
    }
    hit_any
}

fn fire_area(ctx: &mut ExecutorContext, slot: usize, row: &WeaponRow, pos: Vec2) -> bool {
    if let Some(aid) = ctx.world.areas.spawn(ctx.world.tick) {
        ctx.world.areas.slots[aid as usize] = crate::world::AreaState {
            position: pos,
            radius: row.radius,
            damage: row.damage,
            element: row.element,
            ttl_ticks: 30,
            owner_slot: slot as u8,
        };
    }
    true
}

fn fire_trail(ctx: &mut ExecutorContext, slot: usize, row: &WeaponRow, pos: Vec2) -> bool {
    fire_area(ctx, slot, row, pos)
}

fn fire_aura(ctx: &mut ExecutorContext, slot: usize, row: &WeaponRow, pos: Vec2) -> bool {
    let radius_sq = row.radius.mul_fix(row.radius).0 as i64;
    let mut hits: Vec<EntityId> = Vec::new();
    ctx.world.enemies.for_each_alive(|eid, enemy| {
        if enemy.position.dist_sq(pos) <= radius_sq {
            hits.push(eid);
        }
    });
    for eid in hits {
        apply_damage_to_enemy(ctx, slot, eid, row.damage, row.element, row.knockback, pos);
    }
    true
}

fn integrate_projectiles(ctx: &mut ExecutorContext) {
    let tick = ctx.world.tick;
    let dt = Fix::from_int(1) / Fix::from_int(ctx.config.tick_hz as i32);
    let mut to_despawn = Vec::new();
    let mut hits: Vec<(EntityId, EntityId, u16, Vec2)> = Vec::new();
    let mut updates: Vec<(EntityId, ProjectileState)> = Vec::new();

    ctx.world.projectiles.for_each_alive(|pid, proj| {
        let mut p = *proj;
        p.position.x = p.position.x + p.velocity.x.mul_fix(dt);
        p.position.z = p.position.z + p.velocity.z.mul_fix(dt);
        p.range_left = p.range_left - p.velocity.x.mul_fix(dt).abs() - p.velocity.z.mul_fix(dt).abs();
        if p.range_left.0 <= 0 {
            to_despawn.push(pid);
            return;
        }
        if p.from_player {
            ctx.world.grid.query_radius(p.position.x, p.position.z, 1, |entry| {
                if entry.kind == EntityKind::Enemy {
                    hits.push((pid, entry.id, p.damage, p.position));
                }
            });
        }
        updates.push((pid, p));
    });

    for (pid, p) in updates {
        ctx.world.projectiles.slots[pid as usize] = p;
    }

    for (pid, eid, damage, hit_pos) in hits {
        let owner = ctx.world.projectiles.get(pid).map(|p| p.owner_slot).unwrap_or(0);
        let element = ctx.world.projectiles.get(pid).map(|p| p.element).unwrap_or_default();
        if let Some(enemy) = ctx.world.enemies.get_mut(eid) {
            let mult = crate::config::affinity_multiplier(element, enemy.element);
            let dmg = ((damage as u32 * mult.0 as u32) >> 16) as u16;
            let actual = enemy.status.apply_damage(dmg);
            enemy.hp = enemy.hp.saturating_sub(actual);
            ctx.world.players[owner as usize].damage_dealt += actual as u32;
            if enemy.hp == 0 {
                ctx.world.players[owner as usize].kills += 1;
                ctx.world.snapshot.push_event(GameEvent {
                    kind: GameEventKind::Death,
                    tick: ctx.world.tick,
                    actor: owner as u16,
                    target: eid,
                    value: actual,
                    ..Default::default()
                });
                ctx.world.enemies.despawn(eid);
            } else {
                ctx.world.snapshot.push_event(GameEvent {
                    kind: GameEventKind::Hit,
                    tick: ctx.world.tick,
                    actor: owner as u16,
                    target: eid,
                    value: actual,
                    ..Default::default()
                });
            }
        }
        if let Some(proj) = ctx.world.projectiles.get_mut(pid) {
            if proj.pierce_left == 0 {
                to_despawn.push(pid);
            } else {
                proj.pierce_left -= 1;
            }
        }
        let _ = hit_pos;
    }

    for pid in to_despawn {
        ctx.world.projectiles.despawn(pid);
    }
    let _ = tick;
}

fn apply_damage_to_enemy(
    ctx: &mut ExecutorContext,
    player_slot: usize,
    eid: EntityId,
    base_damage: u16,
    element: crate::config::Element,
    knockback: Fix,
    from_pos: Vec2,
) {
    let (actual, killed) = {
        let Some(enemy) = ctx.world.enemies.get_mut(eid) else {
            return;
        };
        let mult = crate::config::affinity_multiplier(element, enemy.element);
        let dmg = ((base_damage as u32 * mult.0 as u32) >> 16) as u16;
        let actual = enemy.status.apply_damage(dmg);
        enemy.hp = enemy.hp.saturating_sub(actual);
        let (kx, kz) =
            knockback_velocity(from_pos.x, from_pos.z, enemy.position.x, enemy.position.z, knockback);
        enemy.velocity.x = enemy.velocity.x + kx;
        enemy.velocity.z = enemy.velocity.z + kz;
        (actual, enemy.hp == 0)
    };
    ctx.world.players[player_slot].damage_dealt += actual as u32;
    if killed {
        ctx.world.players[player_slot].kills += 1;
    }
    ctx.world.snapshot.push_event(GameEvent {
        kind: if killed {
            GameEventKind::Death
        } else {
            GameEventKind::Hit
        },
        tick: ctx.world.tick,
        actor: player_slot as u16,
        target: eid,
        value: actual,
        ..Default::default()
    });
    if killed {
        ctx.world.enemies.despawn(eid);
    }
}

fn arc_direction(family: ArcFamily, heading: u16, arc_width: u8) -> (Fix, Fix) {
    let (fx, fz) = sin_cos(heading);
    match family {
        ArcFamily::Broadside => (fz, -fx), // port
        ArcFamily::Chaser => (fx, fz),
        ArcFamily::Deck => (fx, fz),
        ArcFamily::Trailing => (-fx, -fz),
        ArcFamily::Orbital => (fx, fz),
    }
}

fn in_arc(dir_x: Fix, dir_z: Fix, to_target: Vec2, range: Fix) -> bool {
    let dist_sq = to_target.x.0 as i64 * to_target.x.0 as i64 + to_target.z.0 as i64 * to_target.z.0 as i64;
    if dist_sq > (range.0 as i64) * (range.0 as i64) {
        return false;
    }
    let dot = dir_x.mul_fix(to_target.x) + dir_z.mul_fix(to_target.z);
    dot.0 > 0
}
