using System;
using System.Collections.Generic;
using System.Linq;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Config;
using UnityEngine;

namespace RoguePirates.V4.Progression
{
    /// <summary>The run control the level-ups use (V3 RunManager).</summary>
    public interface IProgressionRun
    {
        bool Exists { get; }
        bool IsRunEnded { get; }
        RunState State { get; }
        void SetPause(PauseReason reason);
        void SetState(RunState state);
        /// <summary>V3 RunManager.EnterPlaying (state Playing, no pause).</summary>
        void EnterPlaying();
    }

    /// <summary>The ship as level-ups touch it (V3 found the PlayerShipController each time).</summary>
    public interface IProgressionShip
    {
        bool Exists { get; }
        bool IsAlive { get; }
        float Hull { get; }
        float MaxHull { get; }
        void Heal(float amount);
        /// <summary>Re-read the run's move speed (V3 SyncRunSpeedMultiplier).</summary>
        void SyncSpeed();
        /// <summary>Max hull follows the run stats, then heals (V3 PlayerHullSync.SyncMaxFromRunStats, no ratio kept).</summary>
        void SyncMaxHull(float healFlat);
    }

    /// <summary>Shows level-up cards (the kept V3 modal until M14).</summary>
    public interface IOfferPresenter
    {
        /// <summary>Shows the cards; false when the modal isn't available (the run then does not pause, as V3).</summary>
        bool TryShow(List<Offer> offers, Action<Offer> selected, Action skipped, Action refreshed, int refreshesRemaining);
        /// <summary>Hides the cards (a run start cancels pending level-ups).</summary>
        void Hide();
    }

    /// <summary>
    /// The cards on screen, for tools that play the run (M14.11; the trace recorder and the autopilot reflected on V3's
    /// LevelUpModal): what is showing, and the same pick and skip the player's clicks make.
    /// </summary>
    public interface IOfferChoice
    {
        bool IsShowing { get; }
        IReadOnlyList<Offer> Shown { get; }
        void Pick(int index);
        void Skip();
    }

    /// <summary>
    /// The progression module's card sink (M14.11; was LegacyLevelUpPresenter over V3's LevelUpModal): forwards to the
    /// level-up presenter the presentation root registers. Without one TryShow is false (the run then does not pause,
    /// as V3 with no modal).
    /// </summary>
    public sealed class OfferPresenterForwarder : IOfferPresenter
    {
        readonly Func<IOfferPresenter> _target;

        public OfferPresenterForwarder(Func<IOfferPresenter> target) { _target = target; }

        IOfferPresenter T
        {
            get
            {
                var t = _target?.Invoke();
                if (t == null || ReferenceEquals(t, this)) return null;
                if (t is UnityEngine.Object unityObject && unityObject == null) return null;
                return t;
            }
        }

        public bool TryShow(List<Offer> offers, Action<Offer> selected, Action skipped, Action refreshed, int refreshesRemaining)
        {
            var t = T;
            if (t != null && t.TryShow(offers, selected, skipped, refreshed, refreshesRemaining)) return true;
            UnityEngine.Debug.LogWarning("Level-up UI unavailable; waiting for modal wiring.");
            return false;
        }

        public void Hide() => T?.Hide();
    }

    /// <summary>
    /// Level-ups (M9 step 9.6): port of V3's LevelSystem on its Saltwind path — XP grants on V3's stage curve, pending
    /// level-ups shown one after another (pausing the run), skip and refresh, and applying the chosen card to the V4
    /// loadout, the Logbooks and the stats. The cards come from <see cref="OfferGenerator"/>.
    /// </summary>
    public sealed class ProgressionModule
    {
        readonly Func<ProgressionConfig> _config;
        readonly Func<string, LogbookConfig> _logbook;
        readonly OfferGenerator _offers;
        readonly IOfferRandom _rng;
        readonly IOfferContext _ctx;
        readonly IStatModifierSink _stats;
        readonly IStatReader _statValues;
        readonly IProgressionRun _run;
        readonly IProgressionShip _ship;
        readonly OfferState _state = new OfferState();
        readonly Dictionary<string, string> _evolutions = new Dictionary<string, string>();
        int _completedLevelUps;

        public ProgressionLoadout Loadout { get; }
        public IOfferPresenter Presenter { get; set; }
        /// <summary>
        /// R-84/R-102 fix (off = V3): a level-up during the boss fight resumes to the boss fight. V3's resume always set
        /// the run to Playing, which doubled ambient spawns and stopped skipping elite beats while the boss lived.
        /// </summary>
        public Func<bool> ResumeToBossFight { get; set; }
        RunState _stateBeforeLevelUp = RunState.Playing;
        public int Level { get; private set; }
        public float CurrentXp { get; private set; }
        public int XpToNext { get; private set; }
        public int PendingLevelUps { get; private set; }
        public int RefreshesRemaining { get; private set; }
        public IReadOnlyList<string> OwnedWeapons => _state.Weapons;
        public IReadOnlyList<string> OwnedLogbooks => _state.Logbooks;

        /// <summary>After XP raises the level (once per grant, with the final level, as V3 on the stage path).</summary>
        public event Action<int> LevelChanged;
        /// <summary>Cards were dealt.</summary>
        public event Action OffersReady;
        /// <summary>Weapons, Logbooks or evolutions changed.</summary>
        public event Action LoadoutChanged;
        /// <summary>A weapon evolved (weapon id, evolution id).</summary>
        public event Action<string, string> Evolved;
        /// <summary>A card was taken and applied (V3 recorded its offer telemetry here, before the next level-up shows).</summary>
        public event Action<Offer> OfferChosen;

        public ProgressionModule(Func<ProgressionConfig> config, Func<string, WeaponConfig> weapon, Func<string, LogbookConfig> logbook, OfferGenerator offers,
            IOfferRandom rng, IOfferContext ctx, IStatModifierSink stats, IStatReader statValues, IProgressionRun run, IProgressionShip ship)
        {
            _config = config;
            _logbook = logbook;
            _offers = offers;
            _rng = rng;
            _ctx = ctx;
            _stats = stats;
            _statValues = statValues;
            _run = run;
            _ship = ship;
            Loadout = new ProgressionLoadout(weapon, id => id != null && _evolutions.TryGetValue(id, out var e) ? e : null);
        }

        ProgressionConfig C => _config();

        public int WeaponLevel(string id) => _state.WeaponLevel(id);
        public int LogbookLevel(string id) => _state.LogbookLevel(id);
        public string EvolutionOf(string weaponId) => weaponId != null && _evolutions.TryGetValue(weaponId, out var e) ? e : null;

        /// <summary>V3 PrepareForNewRun + the record's Initialize and slot capacity: a new run with the starting weapon.</summary>
        public void BeginRun(string startingWeapon, int loadoutSlots, float newWeaponDamageBonus)
        {
            var c = C;
            _state.Clear();
            _evolutions.Clear();
            _completedLevelUps = 0;
            _offers.ResetForRun();
            _state.Weapons.Add(startingWeapon);
            _state.WeaponLevels[startingWeapon] = 1;
            _stateBeforeLevelUp = RunState.Playing;
            Level = c.startLevel;
            CurrentXp = 0f;
            PendingLevelUps = 0;
            RefreshesRemaining = c.refreshesPerRun;
            XpToNext = ProgressionMath.XpToNext(c, Level);
            Loadout.Clear();
            Loadout.NewWeaponDamageBonus = newWeaponDamageBonus;
            Loadout.EnsureSlotCapacity(loadoutSlots);
            Loadout.Equip(startingWeapon);
            Loadout.EnsureSlotCapacity(c.weaponSlots);
            EnsureOwnedWeaponsEquipped();
            LoadoutChanged?.Invoke();
        }

        // ---- XP (V3 AddXP, stage path) --------------------------------------------------------------------------------

        public void AddXp(int amount)
        {
            if (amount <= 0) return;
            if (_run.Exists && _run.IsRunEnded) return;
            if (_ship != null && _ship.Exists && !_ship.IsAlive) return;
            var c = C;
            var gained = 0;
            var remaining = (float)amount;
            var level = Level;
            var xp = CurrentXp;
            while (remaining > 0.001f)
            {
                var need = ProgressionMath.XpToNext(c, level);
                var room = need - xp;
                if (remaining < room)
                {
                    xp += remaining;
                    remaining = 0f;
                    break;
                }
                remaining -= room;
                xp = 0f;
                level++;
                gained++;
            }
            Level = level;
            CurrentXp = xp;
            PendingLevelUps += gained;
            if (gained > 0)
            {
                XpToNext = ProgressionMath.XpToNext(c, Level);
                LevelChanged?.Invoke(Level);
            }
            if (PendingLevelUps > 0) ShowLevelUp();
        }

        public void CancelPendingLevelUps() => PendingLevelUps = 0;

        // ---- Showing and choosing ---------------------------------------------------------------------------------------

        void ShowLevelUp()
        {
            if (_run.Exists && _run.IsRunEnded)
            {
                PendingLevelUps = 0;
                return;
            }
            if (AutoLevelUp != null)
            {
                // Playtest (3 Oct, owner): PvP does not pause; each level-up raises the next pick of the player's priority.
                while (PendingLevelUps > 0)
                {
                    PendingLevelUps--;
                    AutoLevelUp();
                    _completedLevelUps++;
                }
                LoadoutChanged?.Invoke();
                return;
            }
            var offers = GenerateOffers();
            var before = _run.Exists ? _run.State : RunState.Playing;
            if (Presenter != null && Presenter.TryShow(offers, Select, Skip, Refresh, RefreshesRemaining))
            {
                if (before != RunState.LevelUpPaused) _stateBeforeLevelUp = before; // chained level-ups keep the first state
                _run.SetPause(PauseReason.LevelUp);
                _run.SetState(RunState.LevelUpPaused);
            }
        }

        /// <summary>V3 GenerateOffers (stage branch): luck read once, hull from the ship, the post-grant level.</summary>
        public List<Offer> GenerateOffers()
        {
            var luck = _statValues.Get((int)StatId.Luck);
            var hull = _ship != null && _ship.Exists ? _ship.Hull : -1f;
            var maxHull = _ship != null && _ship.Exists ? _ship.MaxHull : -1f;
            var player = new OfferPlayer(hull, maxHull, _statValues.Get((int)StatId.Damage), _statValues.Get((int)StatId.MoveSpeed));
            var offers = _offers.Generate(_rng, _ctx, _state, luck, _completedLevelUps == 0, player, Level);
            OffersReady?.Invoke();
            return offers;
        }

        /// <summary>V3 OnOfferSelected.</summary>
        public void Select(Offer offer)
        {
            PendingLevelUps = Mathf.Max(0, PendingLevelUps - 1);
            ApplyOffer(offer);
            _completedLevelUps++;
            OfferChosen?.Invoke(offer);
            LoadoutChanged?.Invoke();
            if (PendingLevelUps > 0) ShowLevelUp();
            else ResumeAfterLevelUp();
        }

        /// <summary>V3 OnLevelUpSkipped (does not count as a completed level-up).</summary>
        public void Skip()
        {
            PendingLevelUps = Mathf.Max(0, PendingLevelUps - 1);
            if (PendingLevelUps > 0) ShowLevelUp();
            else ResumeAfterLevelUp();
        }

        /// <summary>V3 OnLevelUpRefresh: new cards for the same level-up.</summary>
        public void Refresh()
        {
            if (RefreshesRemaining <= 0) return;
            RefreshesRemaining--;
            ShowLevelUp();
        }

        void ResumeAfterLevelUp()
        {
            _run.SetPause(PauseReason.None);
            // Only if the run is still in the level-up pause: the boss may have died (stage content set Playing) or the run
            // ended while the cards were up, and then V3's resume (or the new state) stands.
            var stillPaused = _run.Exists && !_run.IsRunEnded && _run.State == RunState.LevelUpPaused;
            if (_stateBeforeLevelUp == RunState.BossFight && stillPaused && ResumeToBossFight != null && ResumeToBossFight()) _run.SetState(RunState.BossFight);
            else _run.EnterPlaying();
            _stateBeforeLevelUp = RunState.Playing;
        }

        // ---- Applying a card (V3 ApplyOffer) -------------------------------------------------------------------------------

        void ApplyOffer(Offer offer)
        {
            var c = C;
            switch (offer.Kind)
            {
                case OfferKind.WeaponEvolution:
                    _evolutions[offer.Id] = offer.EvolutionId;
                    Evolved?.Invoke(offer.Id, offer.EvolutionId);
                    LoadoutChanged?.Invoke();
                    return;
                case OfferKind.Continue:
                case OfferKind.NewRunItem: // not dealt in Saltwind
                    return;
                case OfferKind.FallbackHeal:
                    if (_ship != null && _ship.Exists && _ship.IsAlive)
                        _ship.Heal(Mathf.Min(_ship.MaxHull * c.fallbackHealFraction, _ship.MaxHull - _ship.Hull));
                    return;
                case OfferKind.FallbackDamage:
                    _stats.Add("fallback_card", (int)StatId.Damage, c.fallbackDamageBonus);
                    return;
                case OfferKind.FallbackMoveSpeed:
                    _stats.Add("fallback_card", (int)StatId.MoveSpeed, 0f, c.fallbackMoveSpeedMultiplier);
                    _ship?.SyncSpeed();
                    return;
            }

            if (!IsOfferAllowed(offer)) return;
            if (offer.IsWeapon)
            {
                var owned = _state.Weapons.Contains(offer.Id);
                if (!owned && _state.Weapons.Count < c.weaponSlots)
                {
                    _state.Weapons.Add(offer.Id);
                    _state.WeaponLevels[offer.Id] = 1;
                    Loadout.Equip(offer.Id);
                }
                else if (owned)
                {
                    if (Loadout.IndexOf(offer.Id) < 0) Loadout.Equip(offer.Id);
                    var index = Loadout.IndexOf(offer.Id);
                    if (index >= 0)
                    {
                        Loadout.ApplyUpgradePackage(index, offer.Lines, Mathf.Max(1, offer.NextLevel - offer.CurrentLevel)); // V5 M3: rarer cards grant more levels
                        _state.WeaponLevels[offer.Id] = Loadout.Weapons[index].Level; // the loadout is the level's source (V3: the record)
                    }
                }
                EnsureOwnedWeaponsEquipped();
            }
            else
            {
                if (!_state.Logbooks.Contains(offer.Id) && _state.Logbooks.Count < c.logbookSlots) _state.Logbooks.Add(offer.Id);
                _state.LogbookLevels.TryGetValue(offer.Id, out var level);
                _state.LogbookLevels[offer.Id] = level + 1;
                ApplyLogbook(offer);
            }
        }

        /// <summary>V3 IsV3OfferAllowed: owned, else unlocked (Logbooks are always unlocked).</summary>
        bool IsOfferAllowed(Offer offer)
        {
            if (offer.Id == null) return false;
            if (!offer.IsWeapon) return true;
            return _state.Weapons.Contains(offer.Id) || _ctx.IsWeaponUnlocked(offer.Id);
        }

        void EnsureOwnedWeaponsEquipped()
        {
            foreach (var id in _state.Weapons)
            {
                if (Loadout.IndexOf(id) >= 0) continue;
                if (Loadout.Count >= C.weaponSlots) break;
                if (Loadout.Equip(id)) _state.WeaponLevels[id] = Mathf.Max(1, _state.WeaponLevel(id));
            }
        }

        void ApplyLogbook(Offer offer)
        {
            var def = _logbook(offer.Id);
            if (def == null) return;
            var mult = C.rarityMultipliers[offer.Rarity];
            _stats.Add("logbook:" + def.logbookId, def.stat, def.perLevelAdditive * mult, def.perLevelMultiplicative);
            if (def.stat == (int)StatId.Hull) _ship?.SyncMaxHull(def.perLevelAdditive * mult);
        }

        // ---- Playtest (3 Oct, owner): the 1v1 build --------------------------------------------------------------------------

        /// <summary>Set (PvP): level-ups apply at once through this instead of showing cards (the run never pauses).</summary>
        public Action AutoLevelUp { get; set; }

        /// <summary>PvP start: the six drafted weapons and six drafted Logbooks, all at level 1, in priority order (the
        /// Logbook slot limit of the voyage does not apply).</summary>
        public void BeginPvp(IReadOnlyList<string> weapons, IReadOnlyList<string> logbooks)
        {
            var ids = new List<string>();
            if (weapons != null) foreach (var w in weapons) if (!string.IsNullOrEmpty(w) && !ids.Contains(w)) ids.Add(w);
            if (ids.Count > 0)
            {
                Loadout.EnsureSlotCapacity(ids.Count);
                Loadout.Rebuild(ids);
                _state.Weapons.Clear();
                foreach (var w in Loadout.Weapons)
                {
                    _state.Weapons.Add(w.Id);
                    _state.WeaponLevels[w.Id] = w.Level;
                }
            }
            if (logbooks != null)
                foreach (var id in logbooks)
                {
                    if (string.IsNullOrEmpty(id) || _state.Logbooks.Contains(id) || _logbook(id) == null) continue;
                    _state.Logbooks.Add(id);
                    _state.LogbookLevels[id] = 1;
                    ApplyLogbook(new Offer { Kind = OfferKind.Logbook, Id = id, Rarity = 0, CurrentLevel = 0, NextLevel = 1 });
                }
            LoadoutChanged?.Invoke();
        }

        /// <summary>The pick cannot rise any more (or is not carried).</summary>
        public bool IsMaxed(bool isWeapon, string id)
        {
            if (isWeapon)
            {
                var index = Loadout.IndexOf(id);
                if (index < 0) return true;
                var w = Loadout.Weapons[index];
                return w.Level >= Mathf.Max(1, w.Config.maxLevel);
            }
            var def = _logbook(id);
            return def == null || !_state.Logbooks.Contains(id) || _state.LogbookLevel(id) >= Mathf.Max(1, def.maxLevel);
        }

        /// <summary>One level on a carried weapon (a Common upgrade package, as a level-up card) or one Logbook rank.</summary>
        public bool RaisePick(bool isWeapon, string id)
        {
            if (IsMaxed(isWeapon, id)) return false;
            if (isWeapon)
            {
                var w = Loadout.Weapons[Loadout.IndexOf(id)];
                ApplyOffer(_offers.WeaponUpgradeCard(w.Config, w.Level, 0, _rng));
            }
            else
            {
                var level = _state.LogbookLevel(id);
                ApplyOffer(new Offer { Kind = OfferKind.Logbook, Id = id, Rarity = 0, CurrentLevel = level, NextLevel = level + 1 });
            }
            return true;
        }

        /// <summary>Exactly one level-up (the XP to the next level).</summary>
        public void GrantLevelUp() => AddXp(Mathf.Max(1, Mathf.CeilToInt(XpToNext - CurrentXp)));

        // ---- Console and Module Lab edits (dev tools; not V3 paths) -------------------------------------------------------

        /// <summary>Replaces the carried weapons (console add, remove, solo): weapons kept keep their level and upgrades.</summary>
        public void DevSetWeapons(IReadOnlyList<string> weaponIds)
        {
            Loadout.EnsureSlotCapacity(weaponIds.Count);
            Loadout.Rebuild(weaponIds);
            _state.Weapons.Clear();
            foreach (var w in Loadout.Weapons)
            {
                _state.Weapons.Add(w.Id);
                _state.WeaponLevels[w.Id] = w.Level;
            }
            LoadoutChanged?.Invoke();
        }

        /// <summary>One level with an Uncommon package on the weapon's most-offered axes (no Loot draws).</summary>
        public void DevRankUp(int index)
        {
            if (index < 0 || index >= Loadout.Count) return;
            var w = Loadout.Weapons[index];
            var c = C;
            var axes = (w.Config.upgradeAxes ?? Array.Empty<WeaponUpgradeAxis>()).OrderByDescending(a => a.offerWeight).Take(c.upgradeLinesPerRarity[1]);
            var lines = axes.Select(a => new UpgradeLine(a.axis, a.axis == ConfigEnums.UpgradeAxis.Quantity
                ? Mathf.Max(1f, Mathf.Round(a.baseIncrement * c.rarityMultipliers[1]))
                : a.baseIncrement * c.rarityMultipliers[1] * c.upgradePowerMultiplier)).ToArray();
            Loadout.ApplyUpgradePackage(index, lines);
            _state.WeaponLevels[w.Id] = w.Level;
            LoadoutChanged?.Invoke();
        }

        /// <summary>One Logbook rank at the given rarity, as taking its card does (a new Logbook needs a free slot).</summary>
        public void DevRankLogbook(string logbookId, int rarity = 0)
        {
            var level = _state.LogbookLevel(logbookId);
            ApplyOffer(new Offer { Kind = OfferKind.Logbook, Id = logbookId, Rarity = rarity, CurrentLevel = level, NextLevel = level + 1, IsNew = level <= 0 });
            LoadoutChanged?.Invoke();
        }

        /// <summary>Evolves a weapon as taking its evolution card does.</summary>
        public void DevEvolve(string weaponId, string evolutionId) =>
            ApplyOffer(new Offer { Kind = OfferKind.WeaponEvolution, Id = weaponId, EvolutionId = evolutionId, IsWeapon = true });

        /// <summary>The owned lists and levels the offers read.</summary>
        sealed class OfferState : IOfferState
        {
            public readonly List<string> Weapons = new List<string>(), Logbooks = new List<string>();
            public readonly Dictionary<string, int> WeaponLevels = new Dictionary<string, int>(), LogbookLevels = new Dictionary<string, int>();
            IReadOnlyList<string> IOfferState.OwnedWeapons => Weapons;
            IReadOnlyList<string> IOfferState.OwnedLogbooks => Logbooks;
            public int WeaponLevel(string id) => id != null && WeaponLevels.TryGetValue(id, out var l) ? l : 0;
            public int LogbookLevel(string id) => id != null && LogbookLevels.TryGetValue(id, out var l) ? l : 0;
            public void Clear() { Weapons.Clear(); Logbooks.Clear(); WeaponLevels.Clear(); LogbookLevels.Clear(); }
        }
    }
}
