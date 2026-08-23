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
        [SerializeField] int weaponSlots = 2;
        [SerializeField] int logbookSlots = 2;
        [SerializeField] DemoPacingSettings demoPacing;

        public int Level { get; private set; } = 1;
        public float CurrentXP { get; private set; }
        public float XPToNext { get; private set; }
        public int PendingLevelUps { get; private set; }

        readonly List<string> _ownedWeapons = new();
        readonly List<string> _ownedLogbooks = new();
        readonly HashSet<string> _banished = new();

        public event Action<int> LevelChanged;
        public event Action OffersReady;

        void Awake()
        {
            Instance = this;
            if (database == null || !GameDatabaseLoader.IsValid(database))
                database = GameDatabaseLoader.Load();
            if (demoPacing == null)
                demoPacing = Resources.Load<DemoPacingSettings>("DemoPacingSettings");
            RefreshXpToNext();
        }

        public void Initialize(GameDatabase db, string startingWeapon)
        {
            database = db;
            _ownedWeapons.Clear();
            _ownedLogbooks.Clear();
            _ownedWeapons.Add(startingWeapon);
            Level = 1;
            CurrentXP = 0f;
            PendingLevelUps = 0;
            RefreshXpToNext();
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
            var offers = GenerateOffers(3);
            var modal = LevelUpModal.Instance;
            if (modal != null && modal.TryShow(offers, OnOfferSelected))
            {
                RunManager.Instance?.SetPause(PauseReason.LevelUp);
                RunManager.Instance?.SetState(RunState.LevelUpPaused);
                return;
            }

            Debug.LogWarning("Level-up UI unavailable; auto-selecting first offer.");
            OnOfferSelected(offers[0]);
        }

        public struct LevelOffer
        {
            public string id;
            public string title;
            public string description;
            public Rarity rarity;
            public bool isWeapon;
            public bool isNew;
        }

        List<LevelOffer> GenerateOffers(int count)
        {
            var result = new List<LevelOffer>(count);
            var rng = RunManager.Instance.Rng;
            var luck = RunManager.Instance.Stats.Luck;

            for (var i = 0; i < count; i++)
            {
                var pickWeapon = rng.LootFloat() < 0.55f && _ownedWeapons.Count < weaponSlots;
                if (pickWeapon)
                {
                    var w = database.weapons[rng.LootInt(0, database.weapons.Length)];
                    var owned = _ownedWeapons.Contains(w.id);
                    result.Add(new LevelOffer
                    {
                        id = w.id,
                        title = owned ? $"{w.displayName} +" : w.displayName,
                        description = owned ? "Upgrade weapon" : "New weapon",
                        rarity = rng.RollRarity(luck),
                        isWeapon = true,
                        isNew = !owned
                    });
                }
                else
                {
                    var l = database.logbooks[rng.LootInt(0, database.logbooks.Length)];
                    result.Add(new LevelOffer
                    {
                        id = l.id,
                        title = l.displayName,
                        description = $"+{l.perLevelAdditive * 100f:0}% {l.primaryStat}",
                        rarity = rng.RollRarity(luck),
                        isWeapon = false
                    });
                }
            }
            OffersReady?.Invoke();
            return result;
        }

        void OnOfferSelected(LevelOffer offer)
        {
            PendingLevelUps = Mathf.Max(0, PendingLevelUps - 1);
            ApplyOffer(offer);
            if (PendingLevelUps > 0) ShowLevelUp();
            else
            {
                RunManager.Instance?.SetPause(PauseReason.None);
                RunManager.Instance?.EnterPlaying();
            }
        }

        void ApplyOffer(WeaponController weapons, LevelOffer offer)
        {
            if (offer.isWeapon)
            {
                var idx = _ownedWeapons.IndexOf(offer.id);
                if (idx < 0 && _ownedWeapons.Count < weaponSlots)
                {
                    _ownedWeapons.Add(offer.id);
                    weapons.EquipWeapon(offer.id);
                }
                else
                {
                    idx = FindWeaponIndex(weapons, offer.id);
                    if (idx >= 0) weapons.UpgradeWeapon(idx, offer.rarity);
                }
            }
            else
            {
                if (!_ownedLogbooks.Contains(offer.id) && _ownedLogbooks.Count < logbookSlots)
                    _ownedLogbooks.Add(offer.id);
                ApplyLogbook(offer);
            }
        }

        void ApplyOffer(LevelOffer offer)
        {
            var weapons = FindFirstObjectByType<WeaponController>();
            ApplyOffer(weapons, offer);
        }

        static int FindWeaponIndex(WeaponController wc, string id)
        {
            for (var i = 0; i < wc.Weapons.Count; i++)
                if (wc.Weapons[i].def.id == id) return i;
            return -1;
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
