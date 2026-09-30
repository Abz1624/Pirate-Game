using System;
using System.Collections.Generic;
using System.Linq;
using RoguePirates.V4.Config;
using RoguePirates.V4.Difficulty;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Progression;
using RoguePirates.V4.Weapons;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Balance
{
    /// <summary>
    /// V5 M2: a simulated player's build, made of the real V4 build modules — <see cref="ProgressionModule"/> (XP, the
    /// level-up cards from <see cref="OfferGenerator"/>, the loadout and Logbooks), <see cref="ItemModule"/>,
    /// <see cref="CaptainModule"/>, <see cref="StatResolver"/> over a <see cref="PlayerStats"/> and
    /// <see cref="BuildWeaponModifiers"/> — behind small ports, so the simulator levels, picks cards and resolves weapon
    /// numbers exactly as a run does. Cards are chosen by the personality's card policy.
    /// </summary>
    public sealed class SimBuild : IPowerInputs
    {
        readonly SimContent _content;
        readonly PlayerPersonality _p;
        readonly System.Random _rng;
        readonly PlayerStats _stats = new PlayerStats();
        readonly SimPorts _ports;
        readonly List<WeaponSlot> _slots = new List<WeaponSlot>();
        readonly Dictionary<string, WeaponSlot> _slotById = new Dictionary<string, WeaponSlot>();
        List<Offer> _shown;
        Action<Offer> _select;
        Action _skip, _refresh;
        int _refreshesLeft;

        public StatResolver Stats { get; }
        public ProgressionModule Progression { get; }
        public ItemModule Items { get; }
        public CaptainModule Captain { get; }
        public BuildWeaponModifiers Modifiers { get; }
        public float Hull { get; private set; }
        public float MaxHull => _stats.MaxHull;
        public float RunTime { get; set; }
        public int Gold { get; set; }
        /// <summary>Times the card policy found nothing it wanted and spent a refresh.</summary>
        public int Refreshes { get; private set; }
        public readonly List<float> LevelUpTimes = new List<float>();
        public readonly List<(string weapon, float time)> Evolutions = new List<(string, float)>();

        public SimBuild(SimContent content, PlayerPersonality personality, System.Random rng, CaptainConfig captain)
        {
            _content = content;
            _p = personality;
            _rng = rng;
            _ports = new SimPorts(this);
            Stats = new StatResolver(() => _stats);
            var offers = new OfferGenerator(() => content.Progression, () => content.OfferWeights,
                () => content.WeaponsInOrder, () => content.LogbooksInOrder, () => content.Evolutions, () => content.EvolutionsEnabled);
            Progression = new ProgressionModule(() => content.Progression, content.Weapon, content.Logbook, offers, _ports, _ports, Stats, Stats, _ports, _ports)
            {
                Presenter = _ports,
                ResumeToBossFight = () => false,
            };
            Items = new ItemModule(content.Item, Stats, _ports);
            Captain = new CaptainModule(Stats, _ports, _ports, () => content.CaptainActivesEnabled);
            Modifiers = new BuildWeaponModifiers(() => _stats, Captain, Items, content.IsLightningWeapon);
            Progression.LevelChanged += _ => Captain.OnLevelChanged();
            Progression.Evolved += (weapon, _) => Evolutions.Add((weapon, RunTime));

            Captain.BeginRun(captain);
            Hull = _stats.MaxHull;
            var start = content.StartingWeapon(captain);
            Progression.BeginRun(start, content.WeaponSlotCap, content.Progression.newWeaponDamageBonus);
            SyncSlots();
        }

        // ---- IPowerInputs (the director's offense index reads these) ---------------------------------------------------

        public IReadOnlyList<WeaponSlot> Weapons => _slots;
        IWeaponModifiers IPowerInputs.Modifiers => Modifiers;
        float IPowerInputs.Hull => Hull;
        public float IncomingDamageMultiplier => Captain.IncomingDamageMultiplier * Items.IncomingDamageMultiplier();

        public bool Alive => Hull > 0f;
        public float HullShare => MaxHull > 0f ? Hull / MaxHull : 0f;

        public void Damage(float amount)
        {
            if (amount <= 0f || Hull <= 0f) return;
            Hull = Mathf.Max(0f, Hull - amount);
            Items.NotifyPlayerTookDamage();
        }

        public void Heal(float amount) { if (Hull > 0f) Hull = Mathf.Min(MaxHull, Hull + Mathf.Max(0f, amount)); }

        public float Stat(StatId id) => _stats.Get(id);
        /// <summary>The stat a Logbook raises (−1 when unknown).</summary>
        public int LogbookStat(string logbookId) => _content.Logbook(logbookId)?.stat ?? -1;
        public float PickupRadius => _stats.PickupRadius * Captain.PickupRadiusMultiplier;

        /// <summary>XP from a pickup (× the XP gain stat, as the pickup module grants it), then the cards it earns.</summary>
        public void GrantXp(int amount)
        {
            if (amount <= 0 || !Alive) return;
            Progression.AddXp(Mathf.RoundToInt(amount * _stats.XPGain));
            ResolveCards();
        }

        public bool AddItem(string itemId)
        {
            var added = Items.TryAddItem(itemId);
            SyncSlots();
            return added;
        }

        /// <summary>The weapon slots the combat reads, mirrored from the loadout (as the weapon module does each frame).</summary>
        public void SyncSlots()
        {
            _slots.Clear();
            foreach (var w in Progression.Loadout.Weapons)
            {
                if (w?.Config == null) continue;
                if (!_slotById.TryGetValue(w.Id, out var slot)) _slotById[w.Id] = slot = new WeaponSlot { Key = w };
                slot.Config = w.Config;
                var evolution = Progression.EvolutionOf(w.Id);
                slot.Evolution = evolution != null ? _content.Evolution(evolution) : null;
                slot.Level = w.Level;
                slot.DamageBonus = w.DamageBonus;
                slot.QuantityBonus = w.QuantityBonus;
                slot.CooldownMult = w.CooldownMult;
                slot.SpeedMult = w.SpeedMult;
                slot.SizeMult = w.SizeMult;
                slot.DurationMult = w.DurationMult;
                slot.KnockbackMult = w.KnockbackMult;
                _slots.Add(slot);
            }
        }

        // ---- Cards -------------------------------------------------------------------------------------------------------

        void ResolveCards()
        {
            var guard = 0;
            while (_shown != null && guard++ < 200)
            {
                var shown = _shown;
                var select = _select;
                var skip = _skip;
                var refresh = _refresh;
                _shown = null;
                var pick = SimCardPolicy.Choose(_p, shown, this, _rng, out var wanted);
                if (!wanted && _refreshesLeft > 0 && _rng.NextDouble() < _p.rerollUse)
                {
                    Refreshes++;
                    refresh();
                    continue;
                }
                if (pick < 0) skip();
                else
                {
                    LevelUpTimes.Add(RunTime);
                    select(shown[pick]);
                }
            }
            SyncSlots();
        }

        /// <summary>The weapon of the loadout with this id, and whether it has reached its last level.</summary>
        public int WeaponLevel(string id) => Progression.WeaponLevel(id);

        /// <summary>A carried weapon at its top level still waits for its evolution's item.</summary>
        public bool WantsEvolutionItem(out string itemId)
        {
            itemId = null;
            foreach (var w in Progression.Loadout.Weapons)
            {
                if (w?.Config == null || Progression.EvolutionOf(w.Id) != null) continue;
                var evolution = _content.EvolutionFor(w.Id);
                if (evolution == null || string.IsNullOrEmpty(evolution.requiredItemId) || Items.HasItem(evolution.requiredItemId)) continue;
                var unlock = _content.Progression.evolutionUnlockLevel > 0 ? _content.Progression.evolutionUnlockLevel : _content.Progression.maxWeaponLevel;
                if (w.Level < Mathf.Max(1, unlock - 2)) continue; // close to the level its evolution card needs
                itemId = evolution.requiredItemId;
                return true;
            }
            return false;
        }

        // ---- Ports -------------------------------------------------------------------------------------------------------

        sealed class SimPorts : IOfferRandom, IOfferContext, IProgressionRun, IProgressionShip, IOfferPresenter, ICaptainShip, ICaptainRun, IItemWorld
        {
            readonly SimBuild _b;
            public SimPorts(SimBuild b) => _b = b;

            public float LootFloat() => (float)_b._rng.NextDouble();

            public bool IsWeaponOfferable(string weaponId) => _b._content.IsWeaponAvailable(weaponId);
            public bool IsWeaponUnlocked(string weaponId) => _b._content.IsWeaponAvailable(weaponId);
            public bool HasItem(string itemId) => _b.Items != null && _b.Items.HasItem(itemId);
            public bool IsEvolved(string weaponId) => _b.Progression.EvolutionOf(weaponId) != null;

            public bool Exists => true;
            public bool IsRunEnded => !_b.Alive;
            public RunState State => RunState.Playing;
            public void SetPause(PauseReason reason) { }
            public void SetState(RunState state) { }
            public void EnterPlaying() { }

            public bool IsAlive => _b.Alive;
            float IProgressionShip.Hull => _b.Hull;
            public float MaxHull => _b.MaxHull;
            public void Heal(float amount) => _b.Heal(amount);
            public void SyncSpeed() { }
            public void SyncMaxHull(float healFlat)
            {
                // PlayerHullSync.SyncMaxFromRunStats(preserveRatio: false): max follows the stats, then the flat heal.
                _b.Hull = Mathf.Min(_b.Hull, _b.MaxHull);
                _b.Heal(healFlat);
            }

            public bool TryShow(List<Offer> offers, Action<Offer> selected, Action skipped, Action refreshed, int refreshesRemaining)
            {
                _b._shown = offers;
                _b._select = selected;
                _b._skip = skipped;
                _b._refresh = refreshed;
                _b._refreshesLeft = refreshesRemaining;
                return true;
            }
            public void Hide() => _b._shown = null;

            public float Speed => _b.Stat(StatId.MoveSpeed) * 0.7f;
            public void InitializeHull(float baseHull) => _b.Hull = _b.MaxHull;
            public void SyncMaxHullKeepRatio()
            {
                var share = _b.HullShare;
                _b.Hull = _b.MaxHull * share;
            }
            public void ApplySlow(float multiplier, float seconds) { }
            public void ApplyMoveEffect(float multiplier, float seconds) { }

            bool ICaptainRun.Exists => true;
            public bool IsPaused => false;
            public int Gold => _b.Gold;
            public int PlayerLevel => _b.Progression.Level;

            public bool RunActive => _b.Alive;
            public bool ShipExists => true;
            public Vector3 ShipForward => new Vector3(Mathf.Cos(_b.RunTime * 0.5f), 0f, Mathf.Sin(_b.RunTime * 0.5f)); // a slow circle (turn-based items)
            public Vector3 ShipPosition => ShipForward * 20f;
            public Vector3 ShipVelocity => ShipForward * Speed;
            int IItemWorld.Gold => _b.Gold;
            IEnumerable<WeaponConfig> IItemWorld.Weapons => _b.Progression.Loadout.Weapons.Select(w => w.Config);
        }
    }

    /// <summary>V5 M2: how a personality picks a level-up card (plan/v5/PLAYER_PERSONALITIES.md, parameter 9).</summary>
    public static class SimCardPolicy
    {
        /// <summary>The card to take (−1 = skip), and whether the policy wanted it (false = a refresh may be spent).</summary>
        public static int Choose(PlayerPersonality p, IReadOnlyList<Offer> offers, SimBuild build, System.Random rng, out bool wanted)
        {
            wanted = true;
            if (offers == null || offers.Count == 0) return -1;
            var best = -1;
            var bestScore = float.NegativeInfinity;
            for (var i = 0; i < offers.Count; i++)
            {
                var o = offers[i];
                if (o.IsEmpty && o.Kind != OfferKind.FallbackHeal && o.Kind != OfferKind.FallbackDamage && o.Kind != OfferKind.FallbackMoveSpeed) continue;
                var score = Score(p, o, build) + (float)rng.NextDouble() * 0.01f;
                if (p.cards == PersonalityCards.Random) score = (float)rng.NextDouble();
                if (score > bestScore) { bestScore = score; best = i; }
            }
            wanted = p.cards == PersonalityCards.Random || bestScore >= 1f;
            return best;
        }

        static bool Is(Offer o, StatId stat, SimBuild b) => !o.IsWeapon && o.Kind == OfferKind.Logbook && b.LogbookStat(o.Id) == (int)stat;

        public static float Score(PlayerPersonality p, Offer o, SimBuild b)
        {
            if (o.Kind == OfferKind.WeaponEvolution) return 10f; // every policy takes an evolution
            var owned = b.Progression.OwnedWeapons.Count;
            var lowHull = b.HullShare < 0.5f;
            var rarity = o.Rarity * 0.15f;
            switch (p.cards)
            {
                case PersonalityCards.Shiny:
                    if (o.Kind == OfferKind.NewWeapon) return 3f + rarity;
                    return 0.6f + o.Rarity * 0.4f;
                case PersonalityCards.Balanced:
                    if (lowHull && (Is(o, StatId.Hull, b) || o.Kind == OfferKind.FallbackHeal)) return 4f;
                    if (o.Kind == OfferKind.NewWeapon) return owned < 4 ? 3f + rarity : 0.8f + rarity;
                    if (o.Kind == OfferKind.WeaponUpgrade) return 2.5f - o.CurrentLevel * 0.08f + rarity;
                    if (o.Kind == OfferKind.Logbook) return 1.6f + rarity;
                    return 0.5f;
                case PersonalityCards.Damage:
                    if (o.Kind == OfferKind.NewWeapon) return owned < 5 ? 2.6f + rarity : 1f;
                    if (o.Kind == OfferKind.WeaponUpgrade) return (HasAxis(o, ConfigEnums.UpgradeAxis.Damage) ? 2.8f : 2f) + rarity;
                    if (Is(o, StatId.Damage, b) || Is(o, StatId.AttackSpeed, b) || Is(o, StatId.CritChance, b) || Is(o, StatId.Quantity, b)) return 2.4f + rarity;
                    return 0.4f;
                case PersonalityCards.Defense:
                    if (Is(o, StatId.Hull, b) || Is(o, StatId.HullRegen, b) || Is(o, StatId.Armor, b) || Is(o, StatId.Shield, b) || o.Kind == OfferKind.FallbackHeal) return 3f + rarity;
                    if (o.Kind == OfferKind.NewWeapon) return owned < 3 ? 2.5f : 1f;
                    if (o.Kind == OfferKind.WeaponUpgrade) return 1.8f + rarity;
                    return 1.1f;
                case PersonalityCards.Focused:
                {
                    var top = TopWeapon(b);
                    if (o.Kind == OfferKind.WeaponUpgrade && o.Id == top) return 3.5f + rarity;
                    if (o.Kind == OfferKind.NewWeapon) return owned < 3 ? 2.8f + rarity : 0.7f;
                    if (o.Kind == OfferKind.WeaponUpgrade) return 1.9f - o.CurrentLevel * 0.05f + rarity;
                    if (o.Kind == OfferKind.Logbook) return 1.5f + rarity;
                    return 0.5f;
                }
                case PersonalityCards.Economy:
                    if (Is(o, StatId.XPGain, b) || Is(o, StatId.GoldGain, b) || Is(o, StatId.PickupRadius, b) || Is(o, StatId.Luck, b)) return 3f + rarity;
                    if (o.Kind == OfferKind.NewWeapon) return owned < 4 ? 2.4f : 0.9f;
                    if (o.Kind == OfferKind.WeaponUpgrade) return 1.8f + rarity;
                    return 1f;
                default:
                    return 1f;
            }
        }

        static bool HasAxis(Offer o, ConfigEnums.UpgradeAxis axis)
        {
            if (o.Lines == null) return false;
            foreach (var l in o.Lines) if (l.Axis == axis) return true;
            return false;
        }

        static string TopWeapon(SimBuild b)
        {
            string top = null;
            var level = -1;
            foreach (var id in b.Progression.OwnedWeapons)
            {
                if (b.Progression.EvolutionOf(id) != null) continue;
                var l = b.WeaponLevel(id);
                if (l > level) { level = l; top = id; }
            }
            return top;
        }
    }
}
