//! Headless match runner — full 15-minute run in milliseconds.

use clap::{Parser, Subcommand};
use pirate_sim::config::GameConfig;
use pirate_sim::tick::{run_full_tick, stress_tick};
use pirate_sim::world::{MatchPhase, MatchWorld, ShipInput};
use std::time::Instant;

#[derive(Parser)]
#[command(name = "simcli")]
struct Cli {
    #[command(subcommand)]
    command: Commands,
}

#[derive(Subcommand)]
enum Commands {
    /// Run a single match with bot inputs.
    Run {
        #[arg(long, default_value = "42")]
        seed: u64,
        #[arg(long, default_value = "4")]
        players: u8,
        #[arg(long, default_value = "1")]
        tide: u8,
        #[arg(long, default_value = "9000")]
        ticks: u32,
    },
    /// Sweep balance parameters, emit CSV.
    Sweep {
        #[arg(long, default_value = "100")]
        runs: u32,
        #[arg(long, default_value = "sweep.csv")]
        output: String,
    },
    /// CU stress test at 4 or 12 player capacity.
    StressCu {
        #[arg(long, default_value = "4")]
        capacity: u8,
    },
    /// Golden hash for regression tests.
    GoldenHash {
        #[arg(long, default_value = "100")]
        ticks: u32,
    },
}

fn main() {
    let cli = Cli::parse();
    match cli.command {
        Commands::Run {
            seed,
            players,
            tide,
            ticks,
        } => run_match(seed, players, tide, ticks),
        Commands::Sweep { runs, output } => sweep(runs, &output),
        Commands::StressCu { capacity } => stress_cu(capacity),
        Commands::GoldenHash { ticks } => golden_hash(ticks),
    }
}

fn run_match(seed: u64, players: u8, tide: u8, ticks: u32) {
    let config = GameConfig::default();
    let mut world = MatchWorld::new(seed, tide, players, &config);
    world.phase = MatchPhase::Running;
    for i in 0..players {
        world.join_player(i);
    }
    let start = Instant::now();
    for t in 0..ticks {
        for (i, input) in bot_input(t, i as u8) {
            if (i as usize) < world.pending_inputs.len() {
                world.pending_inputs[i as usize] = input;
            }
        }
        run_full_tick(&mut world, &config);
    }
    let elapsed = start.elapsed();
    let total_gold: u32 = world.players.iter().map(|p| p.gold).sum();
    let total_kills: u32 = world.players.iter().map(|p| p.kills).sum();
    println!(
        "ticks={ticks} elapsed={:?} gold={total_gold} kills={total_kills} hash={:#x}",
        elapsed,
        world.state_hash()
    );
}

fn bot_input(tick: u32, slot: u8) -> Vec<(u8, ShipInput)> {
    let phase = (tick + slot as u32 * 100) % 200;
    let throttle = if phase < 100 { 1 } else { -1 };
    let steer = if phase % 50 < 25 { 1 } else { -1 };
    vec![(slot, ShipInput { throttle, steer, buttons: 0 })]
}

fn sweep(runs: u32, output: &str) {
    let mut wtr = csv::Writer::from_path(output).expect("create csv");
    wtr.write_record(["run", "seed", "gold", "kills", "ticks", "hash"])
        .unwrap();
    for run in 0..runs {
        let seed = 1000 + run as u64;
        let config = GameConfig::default();
        let mut world = MatchWorld::new(seed, 4, 4, &config);
        world.phase = MatchPhase::Running;
        for i in 0..4 {
            world.join_player(i);
        }
        let ticks = 6000u32; // ~10 min at 10Hz
        for t in 0..ticks {
            for (i, input) in bot_input(t, i) {
                world.pending_inputs[i as usize] = input;
            }
            run_full_tick(&mut world, &config);
        }
        let gold: u32 = world.players.iter().map(|p| p.gold).sum();
        let kills: u32 = world.players.iter().map(|p| p.kills).sum();
        wtr.write_record([
            run.to_string(),
            seed.to_string(),
            gold.to_string(),
            kills.to_string(),
            ticks.to_string(),
            format!("{:#x}", world.state_hash()),
        ])
        .unwrap();
    }
    wtr.flush().unwrap();
    println!("Wrote {output}");
}

fn stress_cu(capacity: u8) {
    let (enemies, projectiles) = if capacity <= 4 {
        (140, 240)
    } else {
        (200, 720)
    };
    let cu = stress_tick(capacity, enemies, projectiles);
    let cu_per_player_sec = cu as u64 * 10; // 10Hz
    println!("capacity={capacity} enemies={enemies} projectiles={projectiles}");
    println!("cu_per_tick={cu}");
    println!("cu_per_player_second={cu_per_player_sec}");
    if cu > 1_400_000 {
        std::process::exit(1);
    }
}

fn golden_hash(ticks: u32) {
    let config = GameConfig::default();
    let mut world = MatchWorld::new(999, 2, 4, &config);
    world.phase = MatchPhase::Running;
    for i in 0..4 {
        world.join_player(i);
    }
    for t in 0..ticks {
        for i in 0..4 {
            world.pending_inputs[i] = ShipInput {
                throttle: 1,
                steer: if t % 20 < 10 { 1 } else { -1 },
                buttons: 0,
            };
        }
        run_full_tick(&mut world, &config);
    }
    println!("{{\"ticks\":{ticks},\"hash\":\"{:#x}\"}}", world.state_hash());
}
