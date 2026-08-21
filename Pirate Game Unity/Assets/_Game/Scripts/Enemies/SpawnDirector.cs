using HighSeas.Core;
using HighSeas.Enemies;
using UnityEngine;

namespace HighSeas.Enemies
{
    public sealed class SpawnDirector : MonoBehaviour
    {
        [SerializeField] EnemyWorld enemyWorld;
        [SerializeField] Transform player;

        float _budget;
        float _eliteChance;
        int _paidChestIndex = 1;
        bool _miniboss30;
        bool _miniboss72;

        void Awake()
        {
            if (enemyWorld == null) enemyWorld = FindFirstObjectByType<EnemyWorld>();
            if (player == null)
            {
                var ship = FindFirstObjectByType<Player.PlayerShipController>();
                if (ship != null) player = ship.transform;
            }
        }

        void Update()
        {
            if (RunManager.Instance == null || RunManager.Instance.IsPaused) return;
            if (RunManager.Instance.State != RunState.Playing && RunManager.Instance.State != RunState.BossFight) return;
            Tick(Time.deltaTime);
        }

        public void Tick(float dt)
        {
            if (enemyWorld == null || player == null) return;
            var threatPerSec = RunManager.Instance.ThreatPerSecond;
            _budget += threatPerSec * dt;
            var cap = threatPerSec * 3.5f;
            if (_budget > cap) _budget = cap;

            var minute = RunManager.Instance.ElapsedSeconds / 60f;
            _eliteChance = Mathf.Min(0.18f, 0.02f + 0.008f * minute);

            TrySpawnMinibosses();

            while (_budget > 0f && enemyWorld.ActiveCount < Platform.QualityProfile.Current.enemyCap)
            {
                var defIndex = PickEnemyDefinition();
                if (defIndex < 0) break;
                var def = enemyWorld.Definitions[defIndex];
                if (_budget < def.threatCost) break;
                _budget -= def.threatCost;

                var affix = EliteAffix.None;
                if (RunManager.Instance.Rng.DirectorFloat() < _eliteChance)
                    affix = (EliteAffix)RunManager.Instance.Rng.DirectorInt(1, 5);

                var pos = PickSpawnPosition();
                enemyWorld.Spawn(defIndex, pos, affix);
            }
        }

        int PickEnemyDefinition()
        {
            var defs = enemyWorld.Definitions;
            if (defs == null || defs.Length == 0) return -1;
            var weights = new float[defs.Length];
            for (var i = 0; i < defs.Length; i++)
            {
                if (defs[i].isBoss || defs[i].isMiniboss) { weights[i] = 0f; continue; }
                weights[i] = 1f / Mathf.Max(1, defs[i].threatCost);
            }
            return RunManager.Instance.Rng.WeightedPick(weights);
        }

        void TrySpawnMinibosses()
        {
            var elapsed = RunManager.Instance.ElapsedSeconds;
            var stageDuration = GameConstants.StageDurationSeconds;
            if (!_miniboss30 && elapsed >= stageDuration * 0.30f)
            {
                _miniboss30 = true;
                SpawnMiniboss();
            }
            if (!_miniboss72 && elapsed >= stageDuration * 0.72f)
            {
                _miniboss72 = true;
                SpawnMiniboss();
            }
        }

        void SpawnMiniboss()
        {
            var defs = enemyWorld.Definitions;
            for (var i = 0; i < defs.Length; i++)
            {
                if (!defs[i].isMiniboss) continue;
                enemyWorld.Spawn(i, PickSpawnPosition(), EliteAffix.Rich);
                break;
            }
        }

        Vector3 PickSpawnPosition()
        {
            var rng = RunManager.Instance.Rng;
            var angle = rng.DirectorFloat() * Mathf.PI * 2f;
            var dist = Mathf.Lerp(GameConstants.SpawnAnnulusMin, GameConstants.SpawnAnnulusMax, rng.DirectorFloat());
            var offset = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
            return player.position + offset;
        }

        public int GetChestPrice()
        {
            var cost = GameMath.ChestCost(_paidChestIndex);
            _paidChestIndex++;
            return Mathf.RoundToInt(cost);
        }
    }
}
