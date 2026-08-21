using System;
using System.Collections.Generic;
using HighSeas.Core;
using HighSeas.Data;
using UnityEngine;

namespace HighSeas.Meta
{
    public sealed class TriggerBus : MonoBehaviour
    {
        public static TriggerBus Instance { get; private set; }

        public event Action<float, bool> Hit;
        public event Action<bool, bool> Kill;
        public event Action<float> DamageTaken;
        public event Action StatusApplied;
        public event Action ChestOpened;

        readonly Dictionary<string, int> _relicStacks = new();

        void Awake() => Instance = this;

        public void RaiseHit(float damage, bool crit) => Hit?.Invoke(damage, crit);
        public void RaiseKill(bool elite, bool boss) => Kill?.Invoke(elite, boss);
        public void RaiseDamageTaken(float amount) => DamageTaken?.Invoke(amount);
        public void RaiseStatusApplied(StatusType type) => StatusApplied?.Invoke();
        public void RaiseChestOpen() => ChestOpened?.Invoke();

        public void AddRelic(RelicDefinition relic)
        {
            if (relic == null) return;
            _relicStacks.TryGetValue(relic.id, out var stacks);
            if (relic.unique && stacks > 0) return;
            stacks++;
            _relicStacks[relic.id] = stacks;
            ApplyPassive(relic);
            ApplyTrigger(relic);
        }

        void ApplyPassive(RelicDefinition relic)
        {
            if (relic.passiveModifiers == null) return;
            for (var i = 0; i < relic.passiveModifiers.Length; i++)
                RunManager.Instance?.Stats.AddModifier(relic.passiveModifiers[i]);
        }

        void ApplyTrigger(RelicDefinition relic)
        {
            switch (relic.effectId)
            {
                case "gold_tax":
                    Hit += (_, __) => { /* handled in king's tax */ };
                    break;
                case "emergency_bilge":
                    DamageTaken += amt =>
                    {
                        if (RunManager.Instance == null) return;
                        var player = UnityEngine.Object.FindFirstObjectByType<Player.PlayerShipController>();
                        if (player != null && player.Damage.CurrentHP / player.Damage.MaxHP < 0.25f)
                            RunManager.Instance.Stats.AddModifier(new StatModifier(StatId.AttackSpeed, 0f, 1.6f));
                    };
                    break;
            }
        }
    }

    public sealed class LootService : MonoBehaviour
    {
        [SerializeField] GameDatabase database;

        void Awake()
        {
            if (database == null || !GameDatabaseLoader.IsValid(database))
                database = GameDatabaseLoader.Load();
        }

        public RelicDefinition RollRelic(RunRng rng, float luck)
        {
            var rarity = rng.RollRarity(luck);
            RelicDefinition best = null;
            for (var i = 0; i < database.relics.Length; i++)
            {
                var r = database.relics[i];
                if (r.rarity == rarity) { best = r; break; }
            }
            return best ?? database.relics[0];
        }
    }

    public sealed class QuestService : MonoBehaviour
    {
        public static QuestService Instance { get; private set; }

        readonly int[] _progress = new int[10];

        void Awake() => Instance = this;

        public void AddProgress(int questIndex, int amount)
        {
            if (questIndex < 0 || questIndex >= _progress.Length) return;
            _progress[questIndex] += amount;
        }

        public int GetProgress(int questIndex) =>
            questIndex >= 0 && questIndex < _progress.Length ? _progress[questIndex] : 0;
    }
}
