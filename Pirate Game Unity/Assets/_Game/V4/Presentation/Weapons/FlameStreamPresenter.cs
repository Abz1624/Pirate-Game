using HighSeas.Data;
using HighSeas.Player;
using HighSeas.VFX;
using RoguePirates.V4.Config;
using RoguePirates.V4.Weapons;
using UnityEngine;

namespace RoguePirates.V4.Presentation.Weapons
{
    /// <summary>
    /// The flamethrower's stream (M5 playtest fix): the existing Flamethrower_Stream_VFX effect (the juice settings'
    /// stream prefab, its flame particle system as V3's juice controller picked it), shown while a V4 stream weapon is
    /// streaming and posed ahead of the ship. V3's juice controller reset that particle system's emission to 1 particle a
    /// second (authored 60), so the stream was nearly invisible; this keeps the authored emission.
    /// </summary>
    public sealed class FlameStreamPresenter : MonoBehaviour
    {
        const float VisualScaleMult = 1.35f; // V3 WeaponJuiceController.FlamethrowerVisualScaleMult
        const float Height = 0.45f;

        IWeaponManager _weapons;
        DemoJuiceSettings _settings;
        PlayerShipController _ship;
        Transform _mount;
        ParticleSystem[] _particles;
        bool _emitting;
        bool _unavailable;

        public void Bind(IWeaponManager weapons, DemoJuiceSettings settings)
        {
            _weapons = weapons;
            _settings = settings;
        }

        public bool IsEmitting => _emitting;

        void LateUpdate()
        {
            var stream = StreamingSlot(out var any);
            if (!any)
            {
                SetEmitting(false);
                return;
            }
            if (!EnsureMount()) return;
            if (_ship == null) _ship = FindFirstObjectByType<PlayerShipController>();
            if (_ship != null)
            {
                var forward = _ship.MovementForward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.0001f) forward = _ship.transform.forward;
                forward.Normalize();
                var offset = stream != null ? stream.Config.stream.forwardOffset : 1.65f;
                _mount.SetPositionAndRotation(_ship.Position + forward * offset + Vector3.up * Height, Quaternion.LookRotation(forward, Vector3.up));
            }
            SetEmitting(stream != null);
        }

        /// <summary>The first streaming stream weapon; <paramref name="any"/> tells whether a stream weapon is equipped.</summary>
        WeaponSlot StreamingSlot(out bool any)
        {
            any = false;
            if (_weapons == null) return null;
            var slots = _weapons.Slots;
            for (var i = 0; i < slots.Count; i++)
            {
                var w = slots[i];
                if (w.Config == null || w.Config.behavior != ConfigEnums.WeaponBehavior.ConeStream) continue;
                any = true;
                if (w.StreamRemaining > 0f) return w;
            }
            return null;
        }

        bool EnsureMount()
        {
            if (_mount != null) return true;
            if (_unavailable) return false;
            var prefab = _settings != null ? (_settings.flamethrowerStreamPrefab != null ? _settings.flamethrowerStreamPrefab : _settings.flamethrowerPrefab) : null;
            if (prefab == null) { _unavailable = true; return false; }

            // As V3: take the prefab's flame particle system (its name has "Flame", else the first), not the whole gun.
            var template = Instantiate(prefab);
            template.SetActive(false);
            Transform source = null;
            foreach (var ps in template.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps.name.IndexOf("Flame", System.StringComparison.OrdinalIgnoreCase) >= 0) { source = ps.transform; break; }
                if (source == null) source = ps.transform;
            }
            if (source == null) { Destroy(template); _unavailable = true; return false; }

            _mount = new GameObject("V4FlameStream").transform;
            _mount.SetParent(transform, false);
            var visual = Instantiate(source.gameObject, _mount);
            visual.name = "FlamethrowerStream";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = source.localRotation;
            visual.transform.localScale = Vector3.one * ((_settings != null ? _settings.flamethrowerScale : 1f) * VisualScaleMult);
            visual.SetActive(true);
            Destroy(template);
            foreach (var c in visual.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var b in visual.GetComponentsInChildren<Rigidbody>(true)) Destroy(b);
            VfxParticleUtil.SetRenderersEnabled(visual, true);

            _particles = visual.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in _particles)
            {
                var main = ps.main;
                main.loop = true;
                main.playOnAwake = false;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                var emission = ps.emission;
                emission.enabled = false; // authored rate kept; only switched on and off
                if (emission.burstCount > 0) emission.SetBursts(System.Array.Empty<ParticleSystem.Burst>());
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            _emitting = false;
            return true;
        }

        void SetEmitting(bool on)
        {
            if (_particles == null || on == _emitting) return;
            _emitting = on;
            foreach (var ps in _particles)
            {
                if (ps == null) continue;
                var emission = ps.emission;
                emission.enabled = on;
                if (on) { ps.Clear(true); ps.Play(true); }
                else ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }
}
