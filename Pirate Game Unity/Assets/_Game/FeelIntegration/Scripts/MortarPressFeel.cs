using System.Collections.Generic;
using MoreMountains.Feedbacks;
using RoguePirates.V4;
using RoguePirates.V4.Weapons;
using UnityEngine;

namespace RoguePirates.FeelIntegration
{
    /// <summary>
    /// Mortar anticipation: just before the mortar is ready, the ship presses itself down into the water and flattens;
    /// when the shell is lobbed it pops back up with a springy overshoot (and plays the optional launch Feel player).
    /// Reads the mortar slot's existing reload timer (WeaponSlot.CooldownTimer): the press starts as it nears zero, and
    /// the launch is the moment it jumps back up (mortar shells don't send a 'fired' effect). No game code changed; the
    /// shot's timing is untouched. Only presses when an enemy is in the mortar's range (it won't fire otherwise), and
    /// lets go gently if the shot doesn't come. Visual only; mobile friendly (one short list scan per frame).
    /// </summary>
    [AddComponentMenu("Rogue Pirates/Feel/Mortar Press Feel")]
    public sealed class MortarPressFeel : MonoBehaviour
    {
        [Tooltip("Weapon ids that get the press-before-firing feel.")]
        public string[] WeaponIds = { "mortar" };

        [Header("Press (before the shot)")]
        [Tooltip("Start pressing this long before the weapon is ready (seconds).")]
        public float LeadTime = 0.25f;
        [Tooltip("How far the ship sinks (m).")]
        public float Depth = 0.28f;
        [Tooltip("How much the ship flattens and widens while pressed (0.1 = 10%).")]
        [Range(0f, 0.3f)] public float Squash = 0.08f;
        [Tooltip("If the shot hasn't come this long after the press finished, let go without a pop (seconds).")]
        public float GiveUpAfter = 0.35f;

        [Header("Release (the shot)")]
        [Tooltip("How far past the water line the ship pops back up (m).")]
        public float Pop = 0.18f;
        [Tooltip("Bobs per second as it settles.")]
        public float PopFrequency = 2.4f;
        [Tooltip("Feel player to play at launch, and its strength. Empty = the scene's cannon-fire Feel player, if any.")]
        public MMF_Player LaunchFeedback;
        [Range(0f, 1f)] public float LaunchIntensity = 0.35f;

        [Header("While moving")]
        [Tooltip("How much of the press, pop and launch kick to take away at speed (0.6 = 60% less).")]
        [Range(0f, 1f)] public float MovingReduction = 0.6f;
        [Tooltip("Ship speed (m/s) at which the full reduction applies.")]
        public float SpeedForFullReduction = 12f;

        readonly HashSet<string> _ids = new HashSet<string>();
        readonly Dictionary<object, float> _lastTimer = new Dictionary<object, float>();
        V4Game _game;
        ShipPunchOffset _offset;
        float _pressStarted = -1f, _nextSearch;

        void Awake()
        {
            RebuildIds();
            V4Game.RunStarted += OnRunStarted;
        }

        void OnValidate() => RebuildIds();

        void OnDestroy() => V4Game.RunStarted -= OnRunStarted;

        void OnDisable()
        {
            if (_offset != null) _offset.CancelPress();
            _pressStarted = -1f;
            _lastTimer.Clear();
        }

        void OnRunStarted(V4Game game)
        {
            _game = game;
            _lastTimer.Clear();
        }

        void RebuildIds()
        {
            _ids.Clear();
            if (WeaponIds == null) return;
            foreach (var id in WeaponIds)
                if (!string.IsNullOrEmpty(id)) _ids.Add(id.Trim());
        }

        void LateUpdate()
        {
            if (_game == null && Time.unscaledTime >= _nextSearch)
            {
                _nextSearch = Time.unscaledTime + 0.5f;
                _game = FindFirstObjectByType<V4Game>();
            }
            var shell = _game != null ? _game.Shell : null;
            var weapons = shell != null ? shell.Weapons : null;
            if (weapons == null || weapons.Manager == null || shell.Player == null) return;

            var pivot = shell.Player.VisualModel != null ? shell.Player.VisualModel : shell.Player.transform;
            if (_offset == null || _offset.transform != pivot)
            {
                _offset = pivot.GetComponent<ShipPunchOffset>();
                if (_offset == null) _offset = pivot.gameObject.AddComponent<ShipPunchOffset>();
            }

            var slots = weapons.Manager.Slots;
            if (slots == null) return;
            var now = Time.unscaledTime;
            // Softer while sailing: a moving ship already reads busy, and a big dip fights the motion.
            var speed = shell.Player.Velocity.magnitude;
            var k = 1f - MovingReduction * Mathf.Clamp01(speed / Mathf.Max(0.1f, SpeedForFullReduction));
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null || slot.Config == null || !_ids.Contains(slot.Id)) continue;
                var key = slot.Key ?? slot;
                var timer = slot.CooldownTimer;
                float last;
                var launched = _lastTimer.TryGetValue(key, out last) && timer > last + 0.15f; // reload restarted = shell lobbed
                _lastTimer[key] = timer;

                if (launched)
                {
                    if (_offset.IsPressing) _offset.ReleasePress(Pop * k, PopFrequency);
                    else _offset.Punch(0.04f * k, 0.3f, 5f, true); // fired without a press (e.g. level-up reset): small kick
                    _pressStarted = -1f;
                    if (LaunchFeedback == null)
                    {
                        // Standalone prefab: borrow the cannon-fire Feel player if one is in the scene.
                        var fire = FindFirstObjectByType<CannonFireFeel>();
                        if (fire != null) LaunchFeedback = fire.FireFeedback != null ? fire.FireFeedback : fire.GetComponent<MMF_Player>();
                    }
                    if (LaunchFeedback != null && !FeelPriority.HitWithin(0.3f))
                        LaunchFeedback.PlayFeedbacks(shell.Player.transform.position, LaunchIntensity * k);
                    continue;
                }

                if (_offset.IsPressing)
                {
                    if (_pressStarted >= 0f && now - _pressStarted > LeadTime + GiveUpAfter)
                    {
                        _offset.CancelPress();
                        _pressStarted = -1f;
                    }
                    continue;
                }

                if (timer > LeadTime || timer <= 0f) continue;
                if (!EnemyInRange(shell, shell.Player.transform.position, slot.Config.range)) continue;
                _offset.BeginPress(Depth * k, Squash * k, Mathf.Max(0.05f, timer * 0.85f)); // fully pressed just before the shot
                _pressStarted = now;
            }
        }

        static bool EnemyInRange(RoguePirates.V4.Composition.RunBootstrapSequence shell, Vector3 from, float range)
        {
            var enemies = shell.Enemies != null ? shell.Enemies.Contracts as IEnemyQuery : null;
            int index;
            return enemies != null && enemies.Exists && enemies.TryGetNearest(from, Mathf.Max(1f, range), out index, false);
        }
    }
}
