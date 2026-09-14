using System;
using System.Collections.Generic;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.V3;
using HighSeas.Weapons;
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
            weaponSlots = 2;
            logbookSlots = GameConstants.DefaultLogbookSlots;
        }

        void RefreshXpToNext() =>
            XPToNext = _useV3Rules ? V3GameMath.XPToNext(Level) : GameMath.XPToNext(Level, demoPacing);

        public void AddXP(int amount)
        {
            if (amount <= 0) return;
            if (RunManager.Instance != null && RunManager.Instance.IsRunEnded) return;
            var player = UnityEngine.Object.FindFirstObjectByType<Player.PlayerShipController>();
            if (player?.Damage != null && !player.Damage.IsAlive) return;
            CurrentXP += amount;
            while (CurrentXP >= XPToNext)
            {
                CurrentXP -= XPToNext;
                Level++;
                PendingLevelUps++;
                RefreshXpToNext();
                LevelChanged?.Invoke(Level);
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
            if (_useV3Rules)
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
                _completedLevelUps == 0,
                RunManager.Instance?.Stats,
                hull?.CurrentHP ?? -1f,
                hull?.MaxHP ?? -1f);
            OffersReady?.Invoke();
            return offers;
        }

        List<LevelOffer> GenerateV3Offers(RunRng rng, float luck, Combat.DamageReceiver hull)
        {
            var offers = new List<LevelOffer>(3);
            var used = new HashSet<string>();

            TryAddV3Offer(offers, used, RollV3LogbookOffer(rng, luck));
            TryAddV3Offer(offers, used, RollV3WeaponOffer(rng, luck));
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
            _completedLevelUps++;
            LoadoutChanged?.Invoke();
            if (PendingLevelUps > 0) ShowLevelUp();
            else ResumeAfterLevelUp();
        }

        void ResumeAfterLevelUp()
        {
            RunManager.Instance?.SetPause(PauseReason.None);
            RunManager.Instance?.EnterPlaying();
        }

        void ApplyOffer(LevelOffer offer)
        {
            if (offer.kind == LevelOfferKind.Continue)
                return;

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

            var weapons = FindFirstObjectByType<WeaponController>();
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
                    if (idx >= 0)
                    {
                        weapons.ApplyUpgradePackage(idx, offer.upgradeLines);
                        _weaponLevels[offer.id] = weapons.Weapons[idx].level;
                    }
                }
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
