using System.Collections.Generic;
using RoguePirates.V4.Config;
using RoguePirates.V4.Weapons;
using UnityEngine;

namespace RoguePirates.V4.Presentation.Weapons
{
    /// <summary>
    /// Hackathon (13 Oct, owner: every weapon visual as an editable prefab): the weapon prefab catalog
    /// (<see cref="WeaponVisualCatalog"/>) for the weapon effects. Each effect picks its firing weapon's prefab for that
    /// visual (else the shared juice settings prefab, as before), and the prefab's <see cref="WeaponVisualTuning"/> scales
    /// what the code sets. Several effect calls carry no weapon id (the simulation's contract is left as it is), so the
    /// weapon is found the way the effect was caused: the weapon that aimed or fired this frame, the shell whose landing
    /// ring was drawn at the same spot, the keg or anchor at that spot, else the one equipped weapon with that behaviour.
    /// Also the art team's new pieces wired here: the Tidal Aura around the ship (Siren Bell) and the Maelstrom's whirlpool.
    /// Every prefab is pooled and prewarmed in <see cref="PrewarmModels"/>; nothing is created mid-run.
    /// </summary>
    public sealed partial class WeaponVfxPresenter
    {
        const string KindPulse = "pulse";
        const string KindAura = "aura";
        const string KindWhirlpool = "whirlpool";
        // Per-weapon prewarm (each weapon has its own copies now; the pools grow past it if a run ever needs more).
        const int PrewarmWeaponMuzzle = 4, PrewarmWeaponSplash = 3, PrewarmWeaponPulse = 6, PrewarmSlickPulse = 24, PrewarmWeaponRings = 8, PrewarmLaunch = 3, PrewarmWhirlpool = 3, PrewarmWeaponImpact = 12;
        const float AuraSpinDegrees = 15f, WhirlpoolSpinDegrees = 90f;

        /// <summary>The weapon prefab catalog (null: every effect keeps the shared juice settings look). Set before <see cref="Bind"/>.</summary>
        public WeaponVisualCatalog Visuals { get; set; }
        /// <summary>The projectile views (which shot just ended where a splash is played).</summary>
        public ProjectileViewPresenter Projectiles { get; set; }

        GameObject Pick(string weaponId, WeaponVisualSlot slot, GameObject fallback) =>
            Visuals != null && weaponId != null ? Visuals.Pick(weaponId, slot, fallback) : fallback;

        // ---- Which weapon an effect is ----------------------------------------------------------------------------

        string _firedWeapon;
        int _firedFrame = -1;

        WeaponConfig ConfigOf(string weaponId)
        {
            var slots = _weapons != null ? _weapons.Slots : null;
            if (slots == null || weaponId == null) return null;
            for (var i = 0; i < slots.Count; i++)
                if (slots[i]?.Config != null && slots[i].Config.weaponId == weaponId) return slots[i].Config;
            return null;
        }

        /// <summary>The first equipped weapon with <paramref name="behavior"/> (null: none).</summary>
        string Equipped(ConfigEnums.WeaponBehavior behavior, System.Func<WeaponConfig, bool> also = null)
        {
            var slots = _weapons != null ? _weapons.Slots : null;
            if (slots == null) return null;
            for (var i = 0; i < slots.Count; i++)
            {
                var c = slots[i]?.Config;
                if (c != null && c.behavior == behavior && (also == null || also(c))) return c.weaponId;
            }
            return null;
        }

        /// <summary>The weapon that aimed this frame if it has <paramref name="behavior"/>, else the equipped one.</summary>
        string AimedOrEquipped(ConfigEnums.WeaponBehavior behavior, System.Func<WeaponConfig, bool> also = null)
        {
            if (_aimedFrame == Time.frameCount)
            {
                var c = ConfigOf(_aimedWeapon);
                if (c != null && c.behavior == behavior && (also == null || also(c))) return _aimedWeapon;
            }
            return Equipped(behavior, also);
        }

        // Where each predicted-area shell lands and whose it is (the landing ring is drawn with the aim; the impact, the
        // splash and the whirlpool come later at the same spot). A fixed ring of marks: no allocation while playing.
        const int ShellSlots = 64;
        readonly Vector3[] _shellAt = new Vector3[ShellSlots];
        readonly string[] _shellWeapon = new string[ShellSlots];
        int _nextShell;

        void MarkShell(Vector3 at, string weaponId)
        {
            if (weaponId == null) return;
            for (var i = 0; i < ShellSlots; i++)
                if (_shellWeapon[i] == weaponId && FlatNear(_shellAt[i], at, 0.05f)) return;
            _shellAt[_nextShell] = at;
            _shellWeapon[_nextShell] = weaponId;
            _nextShell = (_nextShell + 1) % ShellSlots;
        }

        string ShellAt(Vector3 at)
        {
            // Newest first: a spot reused by a later shell is that shell's.
            for (var k = 1; k <= ShellSlots; k++)
            {
                var i = (_nextShell - k + ShellSlots) % ShellSlots;
                if (_shellWeapon[i] != null && FlatNear(_shellAt[i], at, 0.25f)) return _shellWeapon[i];
            }
            return null;
        }

        // The spots where a shell landed this frame (its whirlpool or slick ring follows in the same frame).
        const int LandingSlots = 8;
        readonly Vector3[] _landedAt = new Vector3[LandingSlots];
        readonly int[] _landedFrame = { -1, -1, -1, -1, -1, -1, -1, -1 };
        int _nextLanding;

        void NoteLanding(Vector3 at)
        {
            _landedAt[_nextLanding] = at;
            _landedFrame[_nextLanding] = Time.frameCount;
            _nextLanding = (_nextLanding + 1) % LandingSlots;
        }

        bool LandedHere(Vector3 at)
        {
            for (var i = 0; i < LandingSlots; i++)
                if (_landedFrame[i] == Time.frameCount && FlatNear(_landedAt[i], at, 0.05f)) return true;
            return false;
        }

        static bool FlatNear(Vector3 a, Vector3 b, float metres)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return dx * dx + dz * dz <= metres * metres;
        }

        /// <summary>The keg weapon whose keg is at <paramref name="at"/> (it blows where it floats).</summary>
        string KegAt(Vector3 at)
        {
            foreach (var view in _handles.Values)
                if (view.WeaponId != null && view.Go != null && FlatNear(view.Go.transform.position, at, 0.6f)) return view.WeaponId;
            return null;
        }

        /// <summary>The mace weapon when one of its maces is at <paramref name="at"/>.</summary>
        string MaceAt(Vector3 at)
        {
            var maces = _weapons != null ? _weapons.Maces : null;
            if (maces == null) return null;
            for (var i = 0; i < maces.Count; i++)
                if (FlatNear(maces[i].Position, at, 1f)) return Equipped(ConfigEnums.WeaponBehavior.ChainedMace);
            return null;
        }

        /// <summary>Whose splash: the shot that just ended there, the shell that landed there, an anchor there, or a summon
        /// weapon that aimed this frame (null: the shared splash, as enemy shots use).</summary>
        string SplashWeapon(Vector3 at)
        {
            if (Visuals == null) return null;
            var shot = Projectiles != null ? Projectiles.EndedNear(at) : null;
            if (shot != null) return shot;
            var shell = ShellAt(at);
            if (shell != null) return shell;
            var mace = MaceAt(at);
            if (mace != null) return mace;
            if (_aimedFrame == Time.frameCount && ConfigOf(_aimedWeapon)?.behavior == ConfigEnums.WeaponBehavior.Summon) return _aimedWeapon;
            return null;
        }

        // ---- Aura (the art team's Tidal Aura for the Siren Bell) ------------------------------------------------------

        readonly Dictionary<string, float> _auraRadius = new Dictionary<string, float>();
        readonly Dictionary<string, View> _auras = new Dictionary<string, View>();
        readonly List<string> _auraGone = new List<string>();
        public int AuraCount => _auras.Count;

        void NoteAura(string weaponId, float radius)
        {
            if (Pick(weaponId, WeaponVisualSlot.Aura, null) != null) _auraRadius[weaponId] = radius;
        }

        /// <summary>Each equipped weapon with an aura prefab shows it around the ship, sized to its last pulse's radius.</summary>
        void SyncAuras()
        {
            if (Visuals == null) return;
            var shipGone = _ship == null || !_ship.Exists;
            _auraGone.Clear();
            foreach (var pair in _auras)
                if (shipGone || ConfigOf(pair.Key) == null) _auraGone.Add(pair.Key);
            foreach (var id in _auraGone)
            {
                ReleaseView(_auras[id]);
                _auras.Remove(id);
                _auraRadius.Remove(id);
            }
            if (shipGone) return;
            foreach (var pair in _auraRadius)
            {
                var prefab = Pick(pair.Key, WeaponVisualSlot.Aura, null);
                if (prefab == null) continue;
                if (!_auras.TryGetValue(pair.Key, out var view) || view.Go == null)
                {
                    var go = Spawn(KindAura, prefab, _ship.Position, Quaternion.identity, null, out var pooled);
                    if (go == null) continue;
                    var prep = Prepare(go);
                    if (!prep.Done) { StripPhysics(go); prep.Done = true; }
                    _auras[pair.Key] = view = new View { Go = go, Pooled = pooled };
                }
                var t = view.Go.transform;
                var tuning = WeaponVisualTuning.Of(prefab);
                var baseRadius = tuning != null ? tuning.baseRadius : 1f;
                t.position = _ship.Position + Vector3.up * (0.05f + WeaponVisualTuning.Height(prefab));
                t.rotation = Quaternion.Euler(0f, Time.time * AuraSpinDegrees * WeaponVisualTuning.Spin(prefab), 0f);
                t.localScale = Vector3.one * (pair.Value / Mathf.Max(0.01f, baseRadius) * WeaponVisualTuning.Scale(prefab));
            }
        }

        // ---- Whirlpool (the art team's Maelstrom whirlpool) ------------------------------------------------------------

        struct Whirl
        {
            public View View;
            public GameObject Prefab;
            public float Left, Total;
        }

        static readonly int SpinOffsetId = Shader.PropertyToID("_SpinOffset");
        static readonly int FadeId = Shader.PropertyToID("_Fade");
        readonly List<Whirl> _whirls = new List<Whirl>();
        MaterialPropertyBlock _whirlBlock;
        public int WhirlpoolCount => _whirls.Count;

        void ShowWhirlpool(string weaponId, Vector3 at, float radius, float seconds)
        {
            var prefab = Pick(weaponId, WeaponVisualSlot.Whirlpool, null);
            if (prefab == null || seconds <= 0f) return;
            var tuning = WeaponVisualTuning.Of(prefab);
            var baseRadius = tuning != null ? tuning.baseRadius : 1f;
            at.y = 0.12f + WeaponVisualTuning.Height(prefab);
            var go = Spawn(KindWhirlpool, prefab, at, Quaternion.identity, radius / Mathf.Max(0.01f, baseRadius), out var pooled);
            if (go == null) return;
            var prep = Prepare(go);
            if (prep.Renderers == null) prep.Renderers = go.GetComponentsInChildren<Renderer>(true);
            Restart(prep.Particles);
            _whirls.Add(new Whirl { View = new View { Go = go, Pooled = pooled }, Prefab = prefab, Left = seconds, Total = seconds });
        }

        void TickWhirlpools(float dt)
        {
            if (_whirls.Count == 0) return;
            if (_whirlBlock == null) _whirlBlock = new MaterialPropertyBlock();
            for (var i = _whirls.Count - 1; i >= 0; i--)
            {
                var w = _whirls[i];
                w.Left -= dt;
                if (w.Left <= 0f || w.View.Go == null)
                {
                    ReleaseView(w.View);
                    _whirls.RemoveAt(i);
                    continue;
                }
                _whirls[i] = w;
                var age = w.Total - w.Left;
                var fade = Mathf.Clamp01(age / 0.25f) * Mathf.Clamp01(w.Left / 0.4f);
                var spin = age * WhirlpoolSpinDegrees * WeaponVisualTuning.Spin(w.Prefab) / 360f;
                var renderers = Prepare(w.View.Go).Renderers;
                for (var r = 0; r < renderers.Length; r++)
                {
                    var renderer = renderers[r];
                    if (renderer == null || renderer is ParticleSystemRenderer) continue;
                    renderer.GetPropertyBlock(_whirlBlock);
                    _whirlBlock.SetFloat(SpinOffsetId, spin);
                    _whirlBlock.SetFloat(FadeId, fade);
                    renderer.SetPropertyBlock(_whirlBlock);
                }
            }
        }

        // ---- Launch (the burst at the ship when a shell is thrown) ----------------------------------------------------

        void PlayLaunch(string weaponId, Vector3 from, Vector3 to)
        {
            var prefab = Pick(weaponId, WeaponVisualSlot.Launch, null);
            if (prefab == null) return;
            var dir = to - from;
            dir.y = 0f;
            var rotation = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : Quaternion.identity;
            PlayBurst(prefab, from, rotation, 1f, 1.2f);
        }

        // ---- Prewarm ----------------------------------------------------------------------------------------------

        readonly List<(GameObject prefab, int count)> _ringPrewarm = new List<(GameObject, int)>();
        GameObject _patchRing;

        /// <summary>Pools and prewarms a weapon's catalog prefabs (the ones the shared pools do not already cover).</summary>
        void PrewarmVisuals(WeaponConfig w)
        {
            var set = Visuals != null ? Visuals.Find(w.weaponId) : null;
            if (set == null || _pools == null) return;
            if (set.muzzle != null) EnsurePool(set.muzzle.transform.Find(GunSmokeChild) != null ? KindMuzzle : KindBurst, set.muzzle, PrewarmWeaponMuzzle);
            if (set.beam != null) EnsurePool(KindBeam, set.beam, PrewarmBeam);
            if (set.waterSplash != null) EnsurePool(KindBurst, set.waterSplash, PrewarmWeaponSplash);
            if (set.mine != null) EnsurePool(KindKeg, set.mine, PrewarmKeg);
            if (set.blast != null) EnsurePool(HasNamedExplosionChild(set.blast.transform) ? KindBlast : KindBurst, set.blast, PrewarmBlast);
            if (set.strike != null) EnsurePool(KindLightning, set.strike, PrewarmLightning);
            if (set.impact != null) EnsurePool(KindBurst, set.impact, PrewarmWeaponImpact);
            if (set.launch != null) EnsurePool(KindBurst, set.launch, PrewarmLaunch);
            if (set.whirlpool != null) EnsurePool(KindWhirlpool, set.whirlpool, PrewarmWhirlpool);
            if (set.aura != null) EnsurePool(KindAura, set.aura, 1);
            if (set.pulseRing != null) EnsurePool(KindPulse, set.pulseRing, w.visual.pulseImpact ? PrewarmSlickPulse : PrewarmWeaponPulse); // an ink slick pulses every tick
            if (set.orbit != null && w.behavior == ConfigEnums.WeaponBehavior.Orbit && w.visual.orbitModel == null) EnsurePool(KindCutlass, set.orbit, PrewarmCutlass);
            if (set.landingRing != null) _ringPrewarm.Add((set.landingRing, PrewarmWeaponRings));
            if (set.patchRing != null) _patchRing = set.patchRing;
            PrewarmRings();
        }

        /// <summary>The landing and patch rings live in the hazard ring presenter (set after or before the prewarm).</summary>
        void PrewarmRings()
        {
            if (HazardRings == null) return;
            foreach (var (prefab, count) in _ringPrewarm) HazardRings.PrewarmTimed(prefab, count);
            _ringPrewarm.Clear();
            if (_patchRing != null) HazardRings.UsePatchRing(_patchRing);
        }
    }
}
