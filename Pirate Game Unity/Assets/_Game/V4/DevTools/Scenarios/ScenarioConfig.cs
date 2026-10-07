using System;
using System.Collections.Generic;
using RoguePirates.V4.Config;
using RoguePirates.V4.Foundation.Core;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Scenarios
{
    /// <summary>
    /// V5 M8z (7 Oct, I8z.4): one Scenario Lab scenario, as data: the run (seed, Captain, playtest switches), the build
    /// (weapons and levels, evolutions, Logbooks and ranks, items, level), the clock (the timeline, kill band and director
    /// follow from it as in the game), what is placed around the ship (enemies, points of interest, the boss), and for a
    /// 1v1 the draft against the practice bot over an in-process relay. <see cref="Scenario.Load(ScenarioConfig)"/> loads
    /// it into the real run scene; the F1 console's Scenarios section and PlayMode tests use the same assets. Dev tools only.
    /// Ids are the configs' own ids (weaponId, logbookId, itemId, enemyId or variantId); unknown ids are reported by the
    /// runner, never guessed.
    /// </summary>
    [CreateAssetMenu(menuName = "Rogue Pirates/V5 Scenario", fileName = "scenario")]
    public sealed class ScenarioConfig : ScriptableObject
    {
        [TextArea(2, 6)] public string description;

        [Header("Run")]
        [Tooltip("The run seed (stage layout, islands, every seeded stream). The same seed and scenario give the same start.")]
        public int seed = 1;
        [Tooltip("The Captain (empty: the menu's saved choice). A locked Captain is allowed (dev tools).")]
        public CaptainConfig captain;
        [Tooltip("Off: the regular game (as tests run). Shipped: the playtest switches the owner plays (Resources/DemoJuiceSettings: Beacon or timed boss, chests-only, whirlpool...), as [UseShippedPlaytestSettings].")]
        public PlaytestSwitches playtest = PlaytestSwitches.Off;
        [Tooltip("The ship takes no damage.")]
        public bool invulnerable = true;
        [Tooltip("Level-ups during the scenario apply without cards or a pause (no pick); off: the cards show as in the game.")]
        public bool skipLevelUpCards = true;

        [Header("Clock")]
        [Tooltip("Run time to start at, seconds (0: the start). Forward jump through the run clock, as the console's Jump: ambient bands, overtime, the storm, the curves and the director read it.")]
        public float clockSeconds;
        [Tooltip("Scripted timeline phases (focus elites and minibosses, swarm pulses, overtime, the storm): as the game would run them at that time, skip the focus/pulse phases already due (a jump would fire them all at once), or skip them all (a quiet sea).")]
        public ScriptedPhaseMode scriptedPhases = ScriptedPhaseMode.SkipPassed;
        [Tooltip("Ambient spawning (the timeline's bands, minute waves and hordes). Off writes ExperimentConfig.encounterSpawnEnabled = false for this run.")]
        public bool ambientSpawning;
        [Tooltip("Timeline phases started at once, as the console's Trigger now (e.g. a focus phase with its arena).")]
        public List<string> triggerPhases = new List<string>();

        [Header("Build")]
        public ScenarioLoadout loadout = new ScenarioLoadout();

        [Header("Around the ship")]
        [Tooltip("Where the ship starts: where the stage put it, or driven into the nearest island shore at 12 m/s.")]
        public ShipStart shipStart = ShipStart.AsTheStage;
        [Tooltip("With IntoShore: keep driving into the shore for the whole scenario (a stick held into it).")]
        public bool holdIntoShore;
        public List<ScenarioEnemy> enemies = new List<ScenarioEnemy>();
        public List<ScenarioPoi> pointsOfInterest = new List<ScenarioPoi>();
        [Tooltip("The boss: not summoned, summoned now (the console's summon, any time), or at the Treasury Beacon (the ship is moved to it and uses it).")]
        public BossSummon boss = BossSummon.None;
        [Tooltip("Every enemy that is not the scenario's own (ambient, scripted) is sunk the moment it appears, through the weapon hit path: a player who kills everything at once (kills count for the kill band).")]
        public bool sinkOtherEnemies;

        [Header("Config edits (this run only, as the console's Config section)")]
        public List<ScenarioConfigEdit> configEdits = new List<ScenarioConfigEdit>();

        [Header("1v1 against the practice bot (no server)")]
        public ScenarioPvp pvp = new ScenarioPvp();

        public enum PlaytestSwitches { Off, Shipped }
        public enum ScriptedPhaseMode { AsTheGame, SkipPassed, SkipAll }
        public enum ShipStart { AsTheStage, IntoShore }
        public enum BossSummon { None, Summon, AtBeacon }
    }

    [Serializable]
    public sealed class ScenarioLoadout
    {
        [Tooltip("On: the carried weapons are exactly the list (empty: none). Off: the starting weapon stays and listed weapons are only raised.")]
        public bool replaceWeapons = true;
        public List<ScenarioWeapon> weapons = new List<ScenarioWeapon>();
        public List<ScenarioLogbook> logbooks = new List<ScenarioLogbook>();
        [Tooltip("Item ids added to the build (as a chest would).")]
        public List<string> items = new List<string>();
        [Tooltip("Player level (no cards: the level only, the build is the lists above). 0 or 1: as the run starts.")]
        public int playerLevel;
        [Range(0f, 0.99f)] [Tooltip("XP into the level, as a fraction of the XP to the next.")]
        public float xpIntoLevel;
    }

    [Serializable]
    public sealed class ScenarioWeapon
    {
        public string weaponId;
        [Tooltip("Weapon level (each level an Uncommon package on its most-offered axes, as the console's Rank up).")]
        public int level = 1;
        [Tooltip("Evolution id to evolve it into (empty: none).")]
        public string evolutionId;
    }

    [Serializable]
    public sealed class ScenarioLogbook
    {
        public string logbookId;
        public int rank = 1;
    }

    [Serializable]
    public sealed class ScenarioEnemy
    {
        [Tooltip("An enemy id or a variant id.")]
        public string enemyId;
        public int count = 1;
        [Tooltip("Metres from the ship.")]
        public float distance = 14f;
        [Tooltip("Degrees clockwise from the top of the screen (90 = right of the ship on screen).")]
        public float bearing;
        [Tooltip("Degrees between units of a group.")]
        public float spread = 15f;
        public ConfigEnums.EliteAffix eliteAffix = ConfigEnums.EliteAffix.None;
        [Tooltip("Marked as a focus unit (a scheduled elite: the HUD's off-screen arrow points at it). An arena (waves paused until it dies) comes from a timeline focus phase: Trigger phases.")]
        public bool focus;
        [Tooltip("HP multiplier (a sturdier unit for a test that must not see it sink).")]
        public float hpMultiplier = 1f;
    }

    [Serializable]
    public sealed class ScenarioPoi
    {
        public POIType type;
        [Tooltip("Metres from the ship.")]
        public float distance = 12f;
        [Tooltip("Degrees clockwise from the top of the screen.")]
        public float bearing;
    }

    [Serializable]
    public sealed class ScenarioConfigEdit
    {
        [Tooltip("The config's id (e.g. director.saltwind, experiment, run).")]
        public string configId;
        [Tooltip("Field path inside it (e.g. killBandSeconds, phases[2].ratePerSecond).")]
        public string path;
        public string value;
    }

    [Serializable]
    public sealed class ScenarioPvp
    {
        public bool enabled;
        [Tooltip("The player's draft in priority order (weapon ids).")]
        public List<string> weapons = new List<string>();
        [Tooltip("The player's drafted Logbooks in priority order.")]
        public List<string> logbooks = new List<string>();
        [Tooltip("Countdown before the fight, seconds (from when the run is up).")]
        public float countdownSeconds = 1f;
        [Tooltip("The match's length from the start of the fight, seconds (the relay ends it on time, as the server does).")]
        public float matchSeconds = 6f;
        [Tooltip("The storm starts shrinking this many seconds into the fight (0: no shrink).")]
        public float stormStartSeconds;
    }
}
