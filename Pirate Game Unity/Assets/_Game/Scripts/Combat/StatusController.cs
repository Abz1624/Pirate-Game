using HighSeas.Core;
using HighSeas.Enemies;
using HighSeas.Meta;
using UnityEngine;

namespace HighSeas.Combat
{
    public sealed class StatusController : MonoBehaviour
    {
        public static StatusController Instance { get; private set; }

        readonly float[] _burnStacks = new float[512];
        readonly float[] _tarred = new float[512];
        readonly float[] _breach = new float[512];
        readonly float[] _curse = new float[512];

        void Awake()
        {
            Instance = this;
        }

        public void ApplyStatus(int enemyIndex, StatusType type, float strength)
        {
            if (enemyIndex < 0 || enemyIndex >= _burnStacks.Length) return;
            switch (type)
            {
                case StatusType.Burn:
                    _burnStacks[enemyIndex] = Mathf.Min(20f, _burnStacks[enemyIndex] + 1f);
                    break;
                case StatusType.Tarred:
                    _tarred[enemyIndex] = Mathf.Max(_tarred[enemyIndex], strength);
                    break;
                case StatusType.Breach:
                    _breach[enemyIndex] = Mathf.Min(5f, _breach[enemyIndex] + 1f);
                    break;
                case StatusType.Shock:
                    break;
                case StatusType.Curse:
                    _curse[enemyIndex] = Mathf.Min(0.6f, _curse[enemyIndex] + strength * 0.1f);
                    break;
            }
            Meta.TriggerBus.Instance?.RaiseStatusApplied(type);
        }

        public float GetVulnerability(int enemyIndex) => 1f + _curse[enemyIndex];

        public float GetArmorReduction(int enemyIndex) => _breach[enemyIndex] * 4f;

        public void Tick(float dt, EnemyWorld world)
        {
            var agents = world.Agents;
            for (var i = 0; i < agents.Length; i++)
            {
                if (!agents[i].active)
                {
                    if (_burnStacks[i] > 0f || _tarred[i] > 0f || _breach[i] > 0f || _curse[i] > 0f)
                        ClearIndex(i);
                    continue;
                }

                if (_burnStacks[i] > 0f)
                {
                    world.ApplyDamage(i, _burnStacks[i] * 0.5f * dt, false);
                    _burnStacks[i] = Mathf.Max(0f, _burnStacks[i] - dt * 0.5f);
                }

                _tarred[i] = Mathf.Max(0f, _tarred[i] - dt);
                _breach[i] = Mathf.Max(0f, _breach[i] - dt * 0.25f);
                _curse[i] = Mathf.Max(0f, _curse[i] - dt * 0.05f);
            }
        }

        public void ClearIndex(int index)
        {
            if (index < 0 || index >= _burnStacks.Length) return;
            _burnStacks[index] = 0f;
            _tarred[index] = 0f;
            _breach[index] = 0f;
            _curse[index] = 0f;
        }
    }
}
