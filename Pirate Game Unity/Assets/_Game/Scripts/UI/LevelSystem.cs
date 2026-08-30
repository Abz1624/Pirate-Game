using System;
using System.Collections.Generic;
using HighSeas.Core;
using HighSeas.Data;
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
            Level = 1;
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

        void RefreshXpToNext() => XPToNext = GameMath.XPToNext(Level, demoPacing);

        public void AddXP(int amount)
        {
            if (amount <= 0) return;
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

        void ShowLevelUp()
        {
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
                _completedLevelUps == 0);
            OffersReady?.Invoke();
            return offers;
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
