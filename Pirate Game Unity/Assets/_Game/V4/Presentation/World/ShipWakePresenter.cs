using System.Collections.Generic;
using RoguePirates.V4.Views;
using UnityEngine;

namespace RoguePirates.V4.Presentation.World
{
    /// <summary>
    /// The player ship's wake (M14 playtest round 1): the ship model's wake and foam objects ("Boat Wake (Trail based)"
    /// and its "Engine Foam" trail ship inactive in the prefab) switched on and emitting while the ship moves, and their
    /// trails and particles stopped while it is still — the enemy ship models' rule (V3's EnemyShipWakeJuice: names
    /// containing "Wake" or "Foam", moving above 0.35 squared speed). The wake parts are found once per model and only
    /// they are touched (other trails under the ship are left alone).
    /// </summary>
    public sealed class ShipWakePresenter : MonoBehaviour
    {
        const float MoveSpeedSqrThreshold = 0.35f;

        IShipView _ship;
        Transform _model;
        readonly List<GameObject> _roots = new List<GameObject>();
        readonly List<TrailRenderer> _trails = new List<TrailRenderer>();
        readonly List<ParticleSystem> _particles = new List<ParticleSystem>();
        bool? _moving;

        public void Bind(IShipView ship) => _ship = ship;

        void LateUpdate() => Tick();

        public void Tick()
        {
            if (_ship == null || !_ship.Exists) return;
            var model = _ship.VisualModel;
            if (model == null) return;
            if (model != _model) Collect(model);

            var moving = _ship.Velocity.sqrMagnitude > MoveSpeedSqrThreshold;
            if (moving)
                foreach (var root in _roots)
                    if (root != null && !root.activeSelf) root.SetActive(true);
            if (_moving == moving) return;
            _moving = moving;
            foreach (var trail in _trails)
            {
                if (trail == null || !trail.gameObject.activeInHierarchy) continue;
                trail.emitting = moving;
                if (!moving) trail.Clear();
            }
            foreach (var ps in _particles)
            {
                if (ps == null) continue;
                if (moving) { if (!ps.isPlaying) ps.Play(true); }
                else if (ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        void Collect(Transform model)
        {
            _model = model;
            _moving = null;
            _roots.Clear();
            _trails.Clear();
            _particles.Clear();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t == model || !IsWake(t.name)) continue;
                _roots.Add(t.gameObject);
                var trail = t.GetComponent<TrailRenderer>();
                if (trail != null) _trails.Add(trail);
                var ps = t.GetComponent<ParticleSystem>();
                if (ps != null) _particles.Add(ps);
            }
        }

        static bool IsWake(string name) => !string.IsNullOrEmpty(name) && (name.Contains("Foam") || name.Contains("Wake"));
    }
}
