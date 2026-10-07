using System.Collections.Generic;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Config;
using UnityEngine;

namespace RoguePirates.V4.Views
{
    // Read-only snapshots presentation reads (M6). Presenters see only these interfaces, the event reader and the
    // small command interfaces below (a button press is input, not a state change the presenter makes itself).
    // M6 implemented them over the legacy hubs; since M14 V4 modules and the ports in Runtime/Ports implement them,
    // without the presenters changing.

    public interface IRunStatusView
    {
        bool Exists { get; }
        float ElapsedSeconds { get; }
        bool IsOvertime { get; }
        int Gold { get; }
        int Kills { get; }
        bool IsRunEnded { get; }
        bool IsPaused { get; }
        bool IsRunComplete { get; }
        bool ObjectiveSecured { get; }
        bool EndedByDeath { get; }
        /// <summary>The weapon/enemy playtest scene drives the run (backslash does not toggle the aim overlay there, as V3).</summary>
        bool WeaponPlaytestActive { get; }
    }

    /// <summary>What the aim overlay reads about weapon targeting (the weapon module's targeting, read-only).</summary>
    public interface IWeaponAimView
    {
        /// <summary>The weapon's current target, as the weapon module would pick it now (no side effects).</summary>
        bool TryResolveTarget(WeaponConfig weapon, out Vector3 targetPosition, out Vector3 aimDirection);
        /// <summary>The predicted-area point (densest cluster) within range.</summary>
        Vector3 PredictClusterGround(float range);
        /// <summary>The experiment's directional-targeting switch.</summary>
        bool DirectionalTargeting { get; }
        /// <summary>The weapon's targeting arc in degrees (as the weapon module resolves it).</summary>
        float ArcDegrees(WeaponConfig weapon);
        /// <summary>The player's Size stat (weapon areas scale with it).</summary>
        float SizeMultiplier { get; }
    }

    public interface IShipView
    {
        bool Exists { get; }
        Vector3 Position { get; }
        Vector3 Velocity { get; }
        Vector3 MovementForward { get; }
        float Hp { get; }
        float MaxHp { get; }
        /// <summary>Boost meter fill, 0..1 (0 when the ship has no boost).</summary>
        float BoostNormalized { get; }
        /// <summary>M14: the ship's visual root, for effects drawn on it (damage flash, highlight); null without a ship.</summary>
        Transform Anchor { get; }
        /// <summary>M14.9b: the ship's model, where its health bar sits (V3 PlayerHealthBarBinder: VisualModel, else the ship).</summary>
        Transform VisualModel => Anchor;
    }

    /// <summary>
    /// The play area as the world presenters draw it (M14.9b; V3's V3PlayBoundsWallVisual and V3SaltwindStormRingVisual
    /// read the shared PlayBounds): the bounds rectangle for the edge walls and the storm's safe rectangle for its ring.
    /// </summary>
    public interface IWorldView
    {
        Vector3 BoundsCenter { get; }
        float BoundsHalfX { get; }
        float BoundsHalfZ { get; }
        /// <summary>The storm ring is up (from the storm's start until the next run).</summary>
        bool StormRingVisible { get; }
        /// <summary>The storm's safe rectangle, in ring order.</summary>
        void StormRingCorners(out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d);
    }

    public interface ICaptainView
    {
        /// <summary>The selected captain as its shared id (Foundation CaptainId value), for portrait lookup.</summary>
        int CaptainId { get; }
        string DisplayTitle { get; }
        /// <summary>M9 playtest: the Captain's active is on (actives switched on and the Captain has one).</summary>
        bool ActiveAvailable { get; }
        string ActiveName { get; }
        float ActiveCooldownRemaining { get; }
        float ActiveCooldownDuration { get; }
        /// <summary>The key that uses it: SPACE, or the gamepad button while a gamepad is connected.</summary>
        string ActiveKeyHint { get; }
        /// <summary>M9 playtest: times the active was used (a change shows the effect banner), what it does, and how long its effect lasts.</summary>
        int ActiveUses { get; }
        string ActiveSummary { get; }
        float ActiveEffectRemaining { get; }
        float ActiveEffectDuration { get; }
        Color ThemeColor { get; }
    }

    public interface IProgressionView
    {
        bool Exists { get; }
        int Level { get; }
        float CurrentXp { get; }
        float XpToNext { get; }
        int LogbookCount { get; }
        string LogbookId(int index);
        string LogbookName(int index);
        int LogbookRank(int index);
        /// <summary>V5 M8z (7 Oct): the player's stat now and its base (the level-up cards' "current → next" lines; was
        /// read from the run manager by the card texts). 0 without stats.</summary>
        float StatValue(StatId stat) => 0f;
        float StatBase(StatId stat) => 0f;
    }

    /// <summary>V5 M8z (7 Oct): an owned weapon's level and upgrade multipliers (the level-up cards' "current → next"
    /// lines; was read from the game's progression loadout by the card texts).</summary>
    public struct WeaponModifiersView
    {
        public int Level, QuantityBonus;
        public float DamageBonus, CooldownMult, SpeedMult, SizeMult, DurationMult;
    }

    public interface ILoadoutView
    {
        /// <summary>Weapon slots shown on the HUD (the experiment's slot cap).</summary>
        int WeaponSlotCap { get; }
        int WeaponCount { get; }
        string WeaponId(int index);
        /// <summary>Display name, evolution-aware (the evolved name once a weapon has evolved).</summary>
        string WeaponName(int index);
        /// <summary>The name the icon files use (the base weapon's display name).</summary>
        string WeaponIconName(int index);
        int WeaponLevel(int index);
        int ItemCount { get; }
        string ItemId(int index);
        string ItemName(int index);
        int ItemStacks(int index);
        /// <summary>V5 M8z (7 Oct): the owned weapon's level and upgrade multipliers; false when it is not owned.</summary>
        bool TryWeaponModifiers(string weaponId, out WeaponModifiersView modifiers) { modifiers = default; return false; }
    }

    public enum BeaconReveal { Hidden, DirectionSector, ExactMarker, Discovered }

    public struct PoiMarker
    {
        public POIType type;
        public Vector3 position;
        public bool used;
    }

    public interface IStageView
    {
        bool Exists { get; }
        /// <summary>Changes when the stage layout is replaced (a new run); markers are rebuilt then.</summary>
        int LayoutVersion { get; }
        int PoiCount { get; }
        PoiMarker Poi(int index);
        bool HasDiscovery { get; }
        BeaconReveal Beacon { get; }
        Vector3? BeaconPosition { get; }
        bool IsPoiVisible(int index);
        bool IsCellExplored(Vector3 world);
        /// <summary>Changes whenever more of the map is explored (the fog mask is rebuilt then).</summary>
        int ExplorationRevision { get; }
        float ExploreCellSize { get; }
        float DiscoveryRadius { get; }
        bool BossSummoned { get; }
        bool BossDefeated { get; }
    }

    public struct ThreatMarker
    {
        public Vector3 position;
        public bool isBoss;
        /// <summary>V5 M8z (6 Oct, owner): the off-screen arrows tell minibosses from scheduled elites (focus units).</summary>
        public bool isMiniboss;
        public bool isFocus;
        public int unitId;
        /// <summary>The definition's display name (may be empty).</summary>
        public string name;
    }

    public interface IEnemyView
    {
        /// <summary>Fills the live boss, miniboss and focus-unit (scheduled elite) positions (the minimap's threat glyphs, the
        /// off-screen arrows).</summary>
        void CollectThreats(List<ThreatMarker> into);
        /// <summary>Tier of an enemy definition (Spawned events carry the definition index).</summary>
        EnemyTier TierOf(int definitionIndex);
        bool IsKnownDefinition(int definitionIndex);
    }

    public struct NamedValue
    {
        public string name;
        public float value;
    }

    public interface IResultsView
    {
        bool ObjectiveSecured { get; }
        bool EndedByDeath { get; }
        /// <summary>The profile line (stage, seed, run time).</summary>
        string ProfileFooter { get; }
        int GoldEarned { get; }
        int GoldSpent { get; }
        int GoldRemaining { get; }
        /// <summary>Whether a reward settlement exists (a profile run); the section text below is empty otherwise.</summary>
        bool HasRewardSection { get; }
        /// <summary>The Rum breakdown as the player reads it (legacy formatting until M12); null when nothing was settled.</summary>
        string RewardSection { get; }
        string GoalProgress { get; }
        /// <summary>Owned weapons, name and level (value).</summary>
        void CollectFinalBuild(List<NamedValue> into);
        /// <summary>Damage share per weapon, percent (value).</summary>
        void CollectContributions(List<NamedValue> into);
    }

    /// <summary>Everything a presenter may read, gathered once at bind time.</summary>
    public interface IRunViews
    {
        IRunStatusView Run { get; }
        IShipView Ship { get; }
        ICaptainView Captain { get; }
        IProgressionView Progression { get; }
        ILoadoutView Loadout { get; }
        IStageView Stage { get; }
        IEnemyView Enemies { get; }
        IResultsView Results { get; }
        IWeaponAimView WeaponAim { get; }
        IToastSink Toasts { get; }
        IHudCommands Commands { get; }
    }

    /// <summary>On-screen messages (the kept toast view until M14).</summary>
    public interface IToastSink
    {
        void Show(string message, float seconds);
    }

    /// <summary>Player input the HUD and results screen forward. Commands, not state: the run decides what happens.</summary>
    public interface IHudCommands
    {
        void RequestBoost();
        void Retry();
        void GoToMenu();
        /// <summary>Picks the next reward unlock goal; returns the message to show.</summary>
        string SelectNextUnlockGoal();
        /// <summary>Buys the selected goal; returns the message to show.</summary>
        string PurchaseUnlockGoal();
        /// <summary>V5 M7: pauses a running run or resumes one the player paused; true when it is now paused by the player.</summary>
        bool TogglePause() => false;
    }

    public sealed class RunViews : IRunViews
    {
        public IRunStatusView Run { get; set; }
        public IShipView Ship { get; set; }
        public ICaptainView Captain { get; set; }
        public IProgressionView Progression { get; set; }
        public ILoadoutView Loadout { get; set; }
        public IStageView Stage { get; set; }
        public IEnemyView Enemies { get; set; }
        public IResultsView Results { get; set; }
        public IWeaponAimView WeaponAim { get; set; }
        public IToastSink Toasts { get; set; }
        public IHudCommands Commands { get; set; }
    }
}
