using System.Collections.Generic;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Enemies
{
    /// <summary>Named, timed spawn rate modifiers composed multiplicatively.</summary>
    public sealed class SpawnModifierStack
    {
        struct Entry
        {
            public string Key;
            public float RateMultiplier;
            public float ExpiresAt;
        }

        readonly List<Entry> _entries = new(8);
        float _baseRateMultiplier = 1f;

        public void Reset(float baseRateMultiplier = 1f)
        {
            _baseRateMultiplier = Mathf.Max(0.1f, baseRateMultiplier);
            _entries.Clear();
        }

        public void SetBaseRateMultiplier(float multiplier) =>
            _baseRateMultiplier = Mathf.Max(0.1f, multiplier);

        public void SetTimedModifier(string key, float rateMultiplier, float startTime, float durationSeconds)
        {
            if (string.IsNullOrEmpty(key)) return;
            Remove(key);
            _entries.Add(new Entry
            {
                Key = key,
                RateMultiplier = Mathf.Max(0.05f, rateMultiplier),
                ExpiresAt = durationSeconds > 0f ? startTime + durationSeconds : float.PositiveInfinity
            });
        }

        public void Remove(string key)
        {
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entries[i].Key == key)
                    _entries.RemoveAt(i);
            }
        }

        public float GetRateMultiplier(float elapsedSeconds)
        {
            Prune(elapsedSeconds);
            var product = _baseRateMultiplier;
            for (var i = 0; i < _entries.Count; i++)
                product *= _entries[i].RateMultiplier;
            return product;
        }

        public bool HasActive(string key, float elapsedSeconds)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Key != key) continue;
                if (elapsedSeconds < _entries[i].ExpiresAt)
                    return true;
            }

            return false;
        }

        void Prune(float elapsedSeconds)
        {
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                if (elapsedSeconds >= _entries[i].ExpiresAt)
                    _entries.RemoveAt(i);
            }
        }
    }
}
