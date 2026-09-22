using System;
using System.Collections.Generic;
using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.V3;
using HighSeas.V3.MultiShip;
using HighSeas.Weapons;
using HighSeas.Save;
using UnityEngine;

namespace HighSeas.UI
{
    public sealed class LevelSystem : MonoBehaviour
    {
        public static LevelSystem Instance { get; private set; }

        [SerializeField] GameDatabase database;
        [SerializeField] LevelUpSettings levelUpSettings;
        [SerializeField] int weaponSlots = GameConstants.DefaultWeaponSlots;
        [SerializeField] int logbookSlots = 2;
        [SerializeField] DemoPacingSettings demoPacing;

        public int Level { get; private set; } = 1;
        public float CurrentXP { get; private set; }
        public float XPToNext { get; private set; }
        public int PendingLevelUps { get; private set; }
        public IReadOnlyList<string> OwnedWeapons => _ownedWeapons;
        public IReadOnlyList<string> OwnedLogbooks => _ownedLogbooks;

        readonly List<string> _ownedWeapons = new();
        readonly List<string> _ownedLogbooks = new();
        readonly Dictionary<string, int> _weaponLevels = new();
        readonly Dictionary<string, int> _logbookLevels = new();
        int _completedLevelUps;
        bool _useV3Rules;
        bool _useCombatExperimentOffers;
        bool _useStageBalanceProgression;
        bool _openingDraftShown;
        CombatExperimentProfile _experimentProfile;

        public event Action<int> LevelChanged;
        public event Action OffersReady;
        public event Action LoadoutChanged;

        void Awake()
        {
            Instance = this;
            weaponSlots = GameConstants.DefaultWeaponSlots;
            if (database == null || !GameDatabaseLoader.IsValid(database))
                database = GameDatabaseLoader.Load();
            if (demoPacing == null)
                demoPacing = Resources.Load<DemoPacingSettings>("DemoPacingSettings");
            if (levelUpSettings == null)
                levelUpSettings = Resources.Load<LevelUpSettings>("LevelUpSettings");
            RefreshXpToNext();
        }

        public void Initialize(GameDatabase db, string startingWeapon)
        {
            database = db;
            _ownedWeapons.Clear();
            _ownedLogbooks.Clear();
            _weaponLevels.Clear();
            _logbookLevels.Clear();
            _completedLevelUps = 0;
            _ownedWeapons.Add(startingWeapon);
            _weaponLevels[startingWeapon] = 1;
            Level = _useV3Rules ? 0 : 1;
            CurrentXP = 0f;
            PendingLevelUps = 0;
            RefreshXpToNext();
        }

        public void PrepareForNewRun(GameDatabase db, string startingWeapon)
        {
            _openingDraftShown = false;
            Initialize(db, startingWeapon);
        }

        public void RestoreSuspendSnapshot(RunSnapshotData data)
        {
            if (data == null) return;
            Level = Mathf.Max(0, data.level);
            CurrentXP = data.currentXp;
            PendingLevelUps = 0;
            _ownedWeapons.Clear();
            _ownedLogbooks.Clear();
            _weaponLevels.Clear();
            _logbookLevels.Clear();
            for (var i = 0; i < data.weaponIds.Count; i++)
            {
                var id = data.weaponIds[i];
                if (string.IsNullOrEmpty(id)) continue;
                _ownedWeapons.Add(id);
                var lvl = i < data.weaponLevels.Count ? data.weaponLevels[i] : 1;
                _weaponLevels[id] = Mathf.Max(1, lvl);
            }

            for (var i = 0; i < data.logbookIds.Count; i++)
            {
                var id = data.logbookIds[i];
                if (string.IsNullOrEmpty(id)) continue;
                _ownedLogbooks.Add(id);
                var lvl = i < data.logbookLevels.Count ? data.logbookLevels[i] : 1;
                _logbookLevels[id] = Mathf.Max(1, lvl);
            }

            RefreshXpToNext();
            LoadoutChanged?.Invoke();
        }

        public int GetWeaponLevel(string weaponId)
        {
            return _weaponLevels.TryGetValue(weaponId, out var level) ? level : 0;
        }

        public int GetLogbookLevel(string logbookId)
        {
            return _logbookLevels.TryGetValue(logbookId, out var level) ? level : 0;
        }

        public void ConfigureV3()
        {
            _useV3Rules = true;
            logbookSlots = GameConstants.DefaultLogbookSlots;
        }

        public void ApplyStageBalanceProgression()
        {
            _useStageBalanceProgression = StageBalanceProfileContext.IsActive;
            if (!_useStageBalanceProgression) return;

            var export = StageBalanceProfileContext.Export;
            if (export?.Progression == null) return;

            weaponSlots = export.Progression.WeaponSlotCap;
            logbookSlots = export.Progression.LogbookSlotCap;
            _useCombatExperimentOffers = false;
        }

        public void SetPlaytestSoloWeapon(string weaponId)
        {
            SetPlaytestWeapon(weaponId, 1);
        }

        public void SetPlaytestWeapon(string weaponId, int level)
        {
            if (string.IsNullOrEmpty(weaponId)) return;
            _ownedWeapons.Clear();
            _weaponLevels.Clear();
            _ownedWeapons.Add(weaponId);
            _weaponLevels[weaponId] = Mathf.Max(1, level);
            LoadoutChanged?.Invoke();
        }

        public void SetPlaytestWeaponLoadout(IReadOnlyList<string> weaponIds)
        {
            if (weaponIds == null) return;
            _ownedWeapons.Clear();
            _weaponLevels.Clear();
            for (var i = 0; i < weaponIds.Count; i++)
            {
                var id = weaponIds[i];
                if (string.IsNullOrEmpty(id) || _ownedWeapons.Contains(id)) continue;
                _ownedWeapons.Add(id);
                _weaponLevels[id] = 1;
            }
            LoadoutChanged?.Invoke();
        }

        public void GrantPlaytestLogbookRank(string logbookId)
        {
            if (string.IsNullOrEmpty(logbookId) || database == null) return;
            var def = database.GetLogbook(logbookId);
            if (def == null) return;
            if (!_ownedLogbooks.Contains(logbookId) && _ownedLogbooks.Count < logbookSlots)
                _ownedLogbooks.Add(logbookId);
            _logbookLevels.TryGetValue(logbookId, out var level);
            if (level >= def.maxLevel)
            {
                V3ToastHud.Instance?.Show($"{V3LogbookCatalog.GetDisplayName(logbookId)} already max rank", 2f);
                return;
            }

            _logbookLevels[logbookId] = level + 1;
            var offer = LevelOfferGenerator.BuildLogbookOffer(def, level, Rarity.Uncommon);
            ApplyLogbook(offer);
            LoadoutChanged?.Invoke();
            V3ToastHud.Instance?.Show(
                $"Logbook +1: {V3LogbookCatalog.GetDisplayName(logbookId)} (Lv {level + 1})",
                2.5f);
        }

        public void ApplyCombatExperiment(CombatExperimentProfile profile)
        {
            _experimentProfile = profile ?? CombatExperimentContext.Active;
            weaponSlots = _experimentProfile.weaponSlotCap;
            _useCombatExperimentOffers = _experimentProfile.weaponSlotCap > 2;
        }

        public bool TryShowOpeningWeaponDraft()
        {
            if (_openingDraftShown) return false;
            if (_experimentProfile == null || !_experimentProfile.openingWeaponDraftEnabled) return false;
            if (_ownedWeapons.Count >= 2 || _ownedWeapons.Count >= weaponSlots) return false;

            _openingDraftShown = true;
            var offers = LevelOfferGenerator.GenerateOpeningWeaponDraft(
                database,
                levelUpSettings,
                RunManager.Instance.Rng,
                _ownedWeapons,
                weaponSlots);
            if (offers.Count == 0) return false;

            var modal = LevelUpModal.Instance;
            if (modal != null && modal.TryShow(offers, OnOpeningDraftSelected))
            {
                RunManager.Instance?.SetPause(PauseReason.LevelUp);
                RunManager.Instance?.SetState(RunState.LevelUpPaused);
                return true;
            }

            OnOpeningDraftSelected(offers[0]);
            return true;
        }

        void OnOpeningDraftSelected(LevelOffer offer)
        {
            ApplyOffer(offer);
            if (offer.kind == LevelOfferKind.NewWeapon)
                RunCombatTelemetry.Instance?.RecordNewWeaponOfferAccepted();
            RunCombatTelemetry.Instance?.RecordOfferChoice(offer.kind.ToString(), offer.id);
            LoadoutChanged?.Invoke();
            RunManager.Instance?.SetPause(PauseReason.None);
            RunManager.Instance?.EnterPlaying();
        }

        void RefreshXpToNext()
        {
            if (_useStageBalanceProgression && StageBalanceProfileContext.Export != null)
                XPToNext = SaltwindStageProgressionLogic.XpToNext(StageBalanceProfileContext.Export, Level);
            else
                XPToNext = _useV3Rules
                    ? V3GameMath.XPToNext(Level, demoPacing)
                    : GameMath.XPToNext(Level, demoPacing);
        }

        public void AddXP(int amount)
        {
            if (amount <= 0) return;
            if (RunManager.Instance != null && RunManager.Instance.IsRunEnded) return;
            var player = UnityEngine.Object.FindFirstObjectByType<Player.PlayerShipController>();
            if (player?.Damage != null && !player.Damage.IsAlive) return;
            var scaled = _useStageBalanceProgression
                ? amount
                : Mathf.Max(1, Mathf.RoundToInt(amount * GameConstants.XpGainGlobalMultiplier));

            if (_useStageBalanceProgression && StageBalanceProfileContext.Export != null)
            {
                var advance = SaltwindStageProgressionLogic.ApplyXpGrant(
                    StageBalanceProfileContext.Export,
                    Level,
                    CurrentXP,
                    scaled);
                Level = advance.Level;
                CurrentXP = advance.CurrentXp;
                PendingLevelUps += advance.PendingLevelUps;
                if (advance.PendingLevelUps > 0)
                {
                    RefreshXpToNext();
                    LevelChanged?.Invoke(Level);
                }
            }
            else
            {
                CurrentXP += scaled;
                while (CurrentXP >= XPToNext)
                {
                    CurrentXP -= XPToNext;
                    Level++;
                    PendingLevelUps++;
                    RefreshXpToNext();
                    LevelChanged?.Invoke(Level);
                }
            }
            if (PendingLevelUps > 0) ShowLevelUp();
        }

        public void CancelPendingLevelUps()
        {
            PendingLevelUps = 0;
            LevelUpModal.Instance?.Hide();
            RunManager.Instance?.SetPause(PauseReason.None);
        }

        void ShowLevelUp()
        {
            if (RunManager.Instance != null && RunManager.Instance.IsRunEnded)
            {
                PendingLevelUps = 0;
                return;
            }

            var offers = GenerateOffers();
            var modal = LevelUpModal.Instance;
            if (modal != null && modal.TryShow(offers, OnOfferSelected))
            {
                Debug.Log($"Level-up modal shown with {offers.Count} offers.");
                RunManager.Instance?.SetPause(PauseReason.LevelUp);
                RunManager.Instance?.SetState(RunState.LevelUpPaused);
                return;
            }

            Debug.LogWarning("Level-up UI unavailable; auto-selecting first offer.");
            if (offers.Count > 0)
                OnOfferSelected(offers[0]);
            else
                ResumeAfterLevelUp();
        }

        List<LevelOffer> GenerateOffers()
        {
            var rng = RunManager.Instance.Rng;
            var luck = RunManager.Instance.Stats.Luck;
            var player = UnityEngine.Object.FindFirstObjectByType<Player.PlayerShipController>();
            var hull = player?.Damage;
            if (_useStageBalanceProgression)
            {
                var stageOffers = LevelOfferGenerator.Generate(
                    database,
                    levelUpSettings,
                    demoPacing,
                    rng,
                    luck,
                    _ownedWeapons,
                    _weaponLevels,
                    _ownedLogbooks,
                    _logbookLevels,
                    weaponSlots,
                    logbookSlots,
                    isFirstLevelUp: false,
                    RunManager.Instance?.Stats,
                    hull?.CurrentHP ?? -1f,
                    hull?.MaxHP ?? -1f,
                    Level,
                    useFillStateWeighting: true);
                OffersReady?.Invoke();
                return stageOffers;
            }

            if (_useV3Rules && !_useCombatExperimentOffers && weaponSlots < 6)
                return GenerateV3Offers(rng, luck, hull);
            var offers = LevelOfferGenerator.Generate(
                database,
                levelUpSettings,
                demoPacing,
                rng,
                luck,
                _ownedWeapons,
                _weaponLevels,
                _ownedLogbooks,
                _logbookLevels,
                weaponSlots,
                logbookSlots,
                _completedLevelUps == 0 && !_useCombatExperimentOffers,
                RunManager.Instance?.Stats,
                hull?.CurrentHP ?? -1f,
                hull?.MaxHP ?? -1f,
                Level,
                _useCombatExperimentOffers);
            OffersReady?.Invoke();
            return offers;
        }

        List<LevelOffer> GenerateV3Offers(RunRng rng, float luck, Combat.DamageReceiver hull)
        {
            var offers = new List<LevelOffer>(3);
            var used = new HashSet<string>();

            TryAddV3Offer(offers, used, RollV3LogbookOffer(rng, luck));
            TryAddV3Offer(offers, used, RollV3RunItemOffer(rng, luck));
            TryAddV3Offer(offers, used, RollV3WeaponOffer(rng, luck));

            var attempts = 0;
            while (offers.Count < 3 && attempts < 64)
            {
                attempts++;
                var kind = attempts % 3;
                var candidate = kind switch
                {
                    0 => RollV3LogbookOffer(rng, luck),
                    1 => RollV3WeaponOffer(rng, luck),
                    _ => RollSafeV3Offer(rng, luck)
                };
                TryAddV3Offer(offers, used, candidate);
            }

            OffersReady?.Invoke();
            return LevelOfferGenerator.FinalizeOffers(
                offers,
                3,
                rng,
                RunManager.Instance?.Stats,
                hull?.CurrentHP ?? -1f,
                hull?.MaxHP ?? -1f);
        }

        void TryAddV3Offer(List<LevelOffer> offers, HashSet<string> used, LevelOffer offer)
        {
            if (offer.id == null || !IsV3OfferAllowed(offer)) return;
            var key = $"{offer.kind}:{offer.id}";
            if (used.Contains(key)) return;
            used.Add(key);
            offers.Add(offer);
        }

        LevelOffer RollV3LogbookOffer(RunRng rng, float luck)
        {
            if (database.logbooks == null || database.logbooks.Length == 0)
                return default;

            var start = rng.MapInt(0, database.logbooks.Length);
            for (var i = 0; i < database.logbooks.Length; i++)
            {
                var lb = database.logbooks[(start + i) % database.logbooks.Length];
                if (lb == null || !V3ContentUnlocks.IsLogbookUnlocked(lb.id)) continue;
                var owned = _ownedLogbooks.Contains(lb.id);
                if (!owned && _ownedLogbooks.Count >= logbookSlots) continue;
                _logbookLevels.TryGetValue(lb.id, out var lbLevel);
                if (owned && lbLevel >= lb.maxLevel) continue;
                return LevelOfferGenerator.BuildLogbookOffer(lb, lbLevel, rng.RollRarity(luck));
            }

            return default;
        }

        LevelOffer RollV3RunItemOffer(RunRng rng, float luck)
        {
            if (rng.LootFloat() > 0.35f) return default;
            var pool = V3ItemCatalog.InteractionItemIds;
            if (pool.Length == 0) return default;
            var id = pool[rng.LootInt(0, pool.Length)];
            if (!V3ContentUnlocks.IsItemUnlocked(id)) return default;
            return LevelOfferGenerator.BuildRunItemOffer(id, rng.RollRarity(luck));
        }

        LevelOffer RollV3WeaponOffer(RunRng rng, float luck)
        {
            if (_ownedWeapons.Count < weaponSlots)
            {
                for (var i = 0; i < database.weapons.Length; i++)
                {
                    var weapon = database.weapons[i];
                    if (weapon == null || _ownedWeapons.Contains(weapon.id)) continue;
                    if (!V3ContentUnlocks.IsWeaponUnlocked(weapon.id)) continue;
                    if (rng.LootFloat() < 0.55f)
                        return LevelOfferGenerator.BuildNewWeaponOffer(weapon);
                    break;
                }
            }

            for (var i = 0; i < _ownedWeapons.Count; i++)
            {
                var weaponId = _ownedWeapons[i];
                var def = database.GetWeapon(weaponId);
                if (def == null) continue;
                _weaponLevels.TryGetValue(weaponId, out var level);
                if (level <= 0) level = 1;
                if (level >= def.maxLevel) continue;
                return LevelOfferGenerator.BuildWeaponUpgradeOffer(
                    def, level, rng.RollRarity(luck), levelUpSettings, demoPacing, rng);
            }

            return default;
        }

        bool IsV3OfferAllowed(LevelOffer offer)
        {
            if (offer.id == null) return false;
            if (offer.kind == LevelOfferKind.NewRunItem)
                return V3ContentUnlocks.IsItemUnlocked(offer.id);
            if (offer.isWeapon)
            {
                if (_ownedWeapons.Contains(offer.id))
                    return true;
                return V3ContentUnlocks.IsWeaponUnlocked(offer.id);
            }

            if (_ownedLogbooks.Contains(offer.id))
                return true;
            return V3ContentUnlocks.IsLogbookUnlocked(offer.id);
        }

        LevelOffer RollSafeV3Offer(RunRng rng, float luck)
        {
            for (var i = 0; i < _ownedWeapons.Count; i++)
            {
                var weaponId = _ownedWeapons[i];
                var def = database.GetWeapon(weaponId);
                if (def == null) continue;
                _weaponLevels.TryGetValue(weaponId, out var level);
                if (level <= 0) level = 1;
                if (level >= def.maxLevel) continue;
                return LevelOfferGenerator.BuildWeaponUpgradeOffer(
                    def, level, rng.RollRarity(luck), levelUpSettings, demoPacing, rng);
            }

            for (var i = 0; i < database.logbooks.Length; i++)
            {
                var lb = database.logbooks[i];
                if (lb == null || !V3ContentUnlocks.IsLogbookUnlocked(lb.id)) continue;
                var owned = _ownedLogbooks.Contains(lb.id);
                if (!owned && _ownedLogbooks.Count >= logbookSlots) continue;
                _logbookLevels.TryGetValue(lb.id, out var lbLevel);
                if (owned && lbLevel >= lb.maxLevel) continue;
                return LevelOfferGenerator.BuildLogbookOffer(lb, lbLevel, rng.RollRarity(luck));
            }

            for (var i = 0; i < database.weapons.Length; i++)
            {
                var weapon = database.weapons[i];
                if (weapon == null || _ownedWeapons.Contains(weapon.id)) continue;
                if (!V3ContentUnlocks.IsWeaponUnlocked(weapon.id)) continue;
                if (_ownedWeapons.Count >= weaponSlots) break;
                return LevelOfferGenerator.BuildNewWeaponOffer(weapon);
            }

            return default;
        }

        void OnOfferSelected(LevelOffer offer)
        {
            PendingLevelUps = Mathf.Max(0, PendingLevelUps - 1);
            ApplyOffer(offer);
            if (offer.kind == LevelOfferKind.NewWeapon)
                RunCombatTelemetry.Instance?.RecordNewWeaponOfferAccepted();
            _completedLevelUps++;
            RecordWeaponTelemetry();
            LoadoutChanged?.Invoke();
            if (PendingLevelUps > 0) ShowLevelUp();
            else ResumeAfterLevelUp();
        }

        void RecordWeaponTelemetry()
        {
            RunCombatTelemetry.Instance?.RecordWeaponCount(
                _ownedWeapons.Count,
                Level,
                RunManager.Instance != null ? RunManager.Instance.ElapsedSeconds : 0f);
        }

        void ResumeAfterLevelUp()
        {
            RunManager.Instance?.SetPause(PauseReason.None);
            RunManager.Instance?.EnterPlaying();
        }

        void ApplyOffer(LevelOffer offer)
        {
            if (offer.kind == LevelOfferKind.WeaponEvolution)
            {
                WeaponEvolutionRuntime.Apply(offer.id, offer.evolutionId);
                LoadoutChanged?.Invoke();
                return;
            }

            if (offer.kind == LevelOfferKind.Continue)
                return;

            if (offer.kind == LevelOfferKind.NewRunItem)
            {
                if (!V3ContentUnlocks.IsItemUnlocked(offer.id)) return;
                V3RunItemInventory.Instance?.TryAddItem(offer.id);
                LoadoutChanged?.Invoke();
                return;
            }

            if (offer.kind == LevelOfferKind.FallbackHeal)
            {
                var player = UnityEngine.Object.FindFirstObjectByType<Player.PlayerShipController>();
                if (player?.Damage != null && player.Damage.IsAlive)
                    player.Damage.Heal(player.Damage.MaxHP * GameConstants.LevelOfferFallbackHealFraction);
                return;
            }

            if (offer.kind == LevelOfferKind.FallbackDamage)
            {
                RunManager.Instance?.Stats.AddModifier(
                    new StatModifier(StatId.Damage, GameConstants.LevelOfferFallbackDamageBonus));
                return;
            }

            if (offer.kind == LevelOfferKind.FallbackMoveSpeed)
            {
                RunManager.Instance?.Stats.AddModifier(
                    new StatModifier(StatId.MoveSpeed, 0f, GameConstants.LevelOfferFallbackMoveSpeedMultiplier));
                UnityEngine.Object.FindFirstObjectByType<Player.PlayerShipController>()?.SyncRunSpeedMultiplier();
                return;
            }

            if (_useV3Rules && !IsV3OfferAllowed(offer))
            {
                Debug.LogWarning($"Blocked locked V3 offer: {offer.id}");
                return;
            }

            var weapons = ResolveHumanWeaponController();
            if (offer.isWeapon)
            {
                var owned = _ownedWeapons.Contains(offer.id);
                if (!owned && _ownedWeapons.Count < weaponSlots)
                {
                    _ownedWeapons.Add(offer.id);
                    _weaponLevels[offer.id] = 1;
                    weapons?.EquipWeapon(offer.id);
                }
                else if (owned)
                {
                    var idx = weapons != null ? weapons.FindWeaponIndex(offer.id) : -1;
                    if (idx < 0)
                        weapons?.EquipWeapon(offer.id);

                    idx = weapons != null ? weapons.FindWeaponIndex(offer.id) : -1;
                    if (idx >= 0)
                    {
                        weapons.ApplyUpgradePackage(idx, offer.upgradeLines);
                        _weaponLevels[offer.id] = weapons.Weapons[idx].level;
                    }
                }

                EnsureOwnedWeaponsOnController(weapons);
            }
            else
            {
                if (!_ownedLogbooks.Contains(offer.id) && _ownedLogbooks.Count < logbookSlots)
                    _ownedLogbooks.Add(offer.id);
                _logbookLevels.TryGetValue(offer.id, out var level);
                _logbookLevels[offer.id] = level + 1;
                ApplyLogbook(offer);
            }
        }

        static WeaponController ResolveHumanWeaponController() =>
            V3MultiShipWeaponLoadouts.FindLocalHumanWeaponController();

        void EnsureOwnedWeaponsOnController(WeaponController weapons)
        {
            if (weapons == null) return;
            for (var i = 0; i < _ownedWeapons.Count; i++)
            {
                var id = _ownedWeapons[i];
                if (weapons.FindWeaponIndex(id) >= 0) continue;
                if (weapons.Weapons.Count >= weaponSlots) break;
                if (weapons.EquipWeapon(id))
                    _weaponLevels[id] = Mathf.Max(1, GetWeaponLevel(id));
            }
        }

        void ApplyLogbook(LevelOffer offer)
        {
            var def = database.GetLogbook(offer.id);
            if (def == null) return;
            var mult = GameConstants.RarityMultipliers[(int)offer.rarity];
            RunManager.Instance.Stats.AddModifier(new StatModifier(
                def.primaryStat,
                def.perLevelAdditive * mult,
                def.perLevelMultiplicative));
        }
    }
}
