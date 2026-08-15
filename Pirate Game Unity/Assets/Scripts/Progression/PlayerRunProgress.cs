using System;
using PirateGame.Combat;
using PirateGame.Ship;
using UnityEngine;

namespace PirateGame.Progression
{
    /// <summary>
    /// Solo run XP, leveling, and card effect application.
    /// </summary>
    public class PlayerRunProgress : MonoBehaviour
    {
        [SerializeField] UpgradeCardDatabase cardDatabase;
        [SerializeField] int startLevel = 1;
        [SerializeField] float baseXpToLevel = 25f;
        [SerializeField] float xpGrowth = 1.35f;
        [SerializeField] float pickupRadius = 3f;

        CannonAutoFire cannons;
        ShipMovement movement;
        Health health;

        float xp;
        float xpToNext;
        int level;
        float healthRegenPerSecond;
        float pickupRadiusMultiplier = 1f;

        int score;
        int killCount;
        string lastAppliedCardSummary = string.Empty;
        float lastAppliedCardTime;

        public int Level => level;
        public float Xp => xp;
        public float XpToNext => xpToNext;
        public int Score => score;
        public int KillCount => killCount;
        public float PickupRadius => pickupRadius * pickupRadiusMultiplier;
        public bool DraftOpen { get; private set; }
        public string LastAppliedCardSummary => lastAppliedCardSummary;

        public event Action<int> OnLevelUp;
        public event Action<string> OnCardApplied;

        void Awake()
        {
            ResolveComponents();
            if (cardDatabase == null)
                cardDatabase = Resources.Load<UpgradeCardDatabase>("UpgradeCardDatabase");

            level = Mathf.Max(1, startLevel);
            xpToNext = baseXpToLevel;
        }

        void Start()
        {
            ResolveComponents();
            cannons?.EnsureMinimumMounts(8);
            if (cannons != null && cannons.ActiveCannonCount < 2)
                cannons.ActiveCannonCount = 2;
        }

        void Update()
        {
            if (healthRegenPerSecond > 0f && health != null && health.IsAlive)
                health.Heal(healthRegenPerSecond * Time.deltaTime);
        }

        public void AddXp(float amount)
        {
            if (DraftOpen || amount <= 0f) return;

            score += Mathf.RoundToInt(amount);
            xp += amount;
            while (xp >= xpToNext)
            {
                xp -= xpToNext;
                level++;
                xpToNext = baseXpToLevel * Mathf.Pow(xpGrowth, level - 1);
                DraftOpen = true;
                Time.timeScale = 0f;
                OnLevelUp?.Invoke(level);
            }
        }

        public void CloseDraft()
        {
            DraftOpen = false;
            Time.timeScale = 1f;
        }

        public UpgradeCardData[] RollDraftOffers(int count, int seed)
        {
            if (cardDatabase == null || cardDatabase.Cards.Count == 0)
                return Array.Empty<UpgradeCardData>();

            var pool = cardDatabase.Cards;
            var offers = new UpgradeCardData[Mathf.Min(count, pool.Count)];
            var used = new System.Collections.Generic.HashSet<int>();
            var rng = new System.Random(seed ^ level * 7919);

            for (var i = 0; i < offers.Length; i++)
            {
                var attempts = 0;
                int idx;
                do
                {
                    idx = rng.Next(pool.Count);
                    attempts++;
                } while (used.Contains(idx) && attempts < 32);

                used.Add(idx);
                offers[i] = pool[idx];
            }

            return offers;
        }

        public void ApplyCard(UpgradeCardData card)
        {
            ResolveComponents();
            cannons?.EnsureMinimumMounts(8);

            var summary = ApplyCardEffect(card);
            lastAppliedCardSummary = summary;
            lastAppliedCardTime = Time.unscaledTime;
            OnCardApplied?.Invoke(summary);

            CloseDraft();
            Debug.Log($"[Run] Applied {card.displayName}: {summary}");
        }

        string ApplyCardEffect(UpgradeCardData card)
        {
            switch (card.effect)
            {
                case UpgradeEffect.DamagePercent:
                    if (cannons == null) return $"{card.displayName} (no cannons found)";
                    cannons.DamageMultiplier += card.magnitude;
                    return $"{card.displayName}: damage x{cannons.DamageMultiplier:0.00}";

                case UpgradeEffect.FireRatePercent:
                    if (cannons == null) return $"{card.displayName} (no cannons found)";
                    cannons.FireRateMultiplier += card.magnitude;
                    return $"{card.displayName}: fire rate x{cannons.FireRateMultiplier:0.00} ({cannons.EffectiveFireRate:0.##}/s)";

                case UpgradeEffect.CannonCount:
                    if (cannons == null) return $"{card.displayName} (no cannons found)";
                    cannons.ActiveCannonCount += Mathf.RoundToInt(card.magnitude);
                    return $"{card.displayName}: {cannons.ActiveCannonCount} cannons active";

                case UpgradeEffect.Pierce:
                    if (cannons == null) return $"{card.displayName} (no cannons found)";
                    cannons.PierceBonus += Mathf.RoundToInt(card.magnitude);
                    return $"{card.displayName}: pierce +{cannons.PierceBonus}";

                case UpgradeEffect.RangePercent:
                    if (cannons == null) return $"{card.displayName} (no cannons found)";
                    cannons.RangeMultiplier += card.magnitude;
                    return $"{card.displayName}: range x{cannons.RangeMultiplier:0.00} ({cannons.EffectiveRange:0.#}m)";

                case UpgradeEffect.MaxHealthPercent:
                    if (health == null) return $"{card.displayName} (no health found)";
                    health.MaxHealthMultiplier += card.magnitude;
                    return $"{card.displayName}: max hull x{health.MaxHealthMultiplier:0.00}";

                case UpgradeEffect.MoveSpeedPercent:
                    if (movement == null) return $"{card.displayName} (no movement found)";
                    movement.MoveSpeedMultiplier += card.magnitude;
                    return $"{card.displayName}: speed x{movement.MoveSpeedMultiplier:0.00}";

                case UpgradeEffect.TurnRatePercent:
                    if (movement == null) return $"{card.displayName} (no movement found)";
                    movement.TurnRateMultiplier += card.magnitude;
                    return $"{card.displayName}: turn x{movement.TurnRateMultiplier:0.00}";

                case UpgradeEffect.HealthRegen:
                    healthRegenPerSecond += card.magnitude;
                    return $"{card.displayName}: regen {healthRegenPerSecond:0.#}/s";

                case UpgradeEffect.PickupRadiusPercent:
                    pickupRadiusMultiplier += card.magnitude;
                    return $"{card.displayName}: pickup radius x{pickupRadiusMultiplier:0.00}";

                default:
                    return card.displayName;
            }
        }

        void ResolveComponents()
        {
            cannons ??= GetComponent<CannonAutoFire>();
            movement ??= GetComponent<ShipMovement>();
            health ??= GetComponent<Health>();
            if (health == null)
            {
                health = gameObject.AddComponent<Health>();
                health.Initialize(100f);
            }
        }

        public void RegisterKill(bool isBoss)
        {
            killCount++;
            score += isBoss ? 150 : 15;
        }

        public bool TryGetRecentCardToast(out string message)
        {
            message = lastAppliedCardSummary;
            return !string.IsNullOrEmpty(message) && Time.unscaledTime - lastAppliedCardTime < 3f;
        }
    }
}
