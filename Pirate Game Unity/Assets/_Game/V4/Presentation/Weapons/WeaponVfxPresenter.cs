using System;
using System.Collections.Generic;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.VFX;
using RoguePirates.V4.Config;
using RoguePirates.V4.Presentation.Pooling;
using RoguePirates.V4.Views;
using RoguePirates.V4.Weapons;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Presentation.Weapons
{
    /// <summary>
    /// Weapon effects as the player sees them (M6), replacing the M5 LegacyWeaponEffects adapter one for one. Port of the visuals in V3's
    /// WeaponJuiceController: muzzle smoke, the Long Nine beam, the broadside salvo, powder kegs (drop and blast),
    /// lightning strikes, the mortar reticle and shell bursts, burning oil, boarding skiffs and the flying cutlasses.
    /// Timings, scales, colours and prefabs are V3's (the juice settings asset, read only).
    /// <para>What V4 changes: every effect V3 instantiated and destroyed per use (muzzle guns, keg blasts, reticles,
    /// kegs, blades, skiffs) or pooled in the juice broker's own pool comes from the V4 <see cref="PoolService"/>, keys
    /// "vfx.&lt;kind&gt;/&lt;prefab&gt;", registered and prewarmed in <see cref="Bind"/>. Game state is read only from
    /// <see cref="IWeaponManager"/> (orbit slot, blade positions) and <see cref="IShipView"/> (ship heading); no legacy
    /// hub is looked up, and no random number is drawn (the mortar bursts use V3's fallback spread, as M5 did: V4
    /// already made V3's combat draws in the simulation, register R-70).</para>
    /// <para>M14.9b: also the water splash (V3 JuiceVfxBroker.PlayWaterSplash, with its merge window shared by every
    /// splash in the run, enemy shots included), the enemy world's blasts (V3 SaltwindCombatEffects: the keg blast at sea
    /// level) and, through the hazard ring presenter, the timed mortar ring. Not here: the flame stream
    /// (FlameStreamPresenter).</para>
    /// </summary>
    public sealed partial class WeaponVfxPresenter : MonoBehaviour, IWeaponEffects, RoguePirates.V4.Enemies.IEnemyEffects
    {
        // V3 JuiceVfxBroker: splashes within 1.35 m and 0.14 s of the last one merge into it.
        const float SplashMergeRadius = 1.35f;
        const float SplashMergeSeconds = 0.14f;
        const int PrewarmSplash = 12;
        public const string KeyPrefix = "vfx.";

        // Pool kinds: one pool per (kind, prefab), because each kind prepares its instances differently.
        const string KindMuzzle = "muzzle";       // a gun prefab showing only its CannonSmoke child
        const string KindBurst = "burst";         // V3 JuiceVfxBroker.Play: whole prefab, particles cleared and replayed
        const string KindBlast = "blast";         // powder keg blast: only the named explosion children
        const string KindLightning = "lightning";
        const string KindBeam = "beam";
        const string KindReticle = "reticle";     // the mortar reticle child of the telegraph prefab, tinted
        const string KindKeg = "keg";
        const string KindCutlass = "cutlass";
        const string KindSkiff = "skiff";
        const string KindOrbitModel = "orbit-model"; // V5 M4: an Orbit weapon's own model (the Anchor Flail)
        const string KindCoin = "coin";               // Playtest (3 Oct, owner): the Cursed Doubloons' coins

        // V3 WeaponJuiceController constants.
        const float MortarReticleDiameterPerScale = 12.5f;
        const float MortarWindupSeconds = 1.1f;
        static readonly Color CutlassAuraTint = new Color(1f, 0.9f, 0.32f, 1f);
        static readonly string[] PowderKegExplosionChildNames = { "Impact small", "Ring", "Smoke" };
        const string GunSmokeChild = "CannonSmoke";
        const string ReticleChild = "Visuals/WaterReticle";
        // V3 CreateBoardingSkiffVisual (an M5 adapter until M6); the model and scale are weapon config
        // (WeaponConfig.visual.summonModel / summonModelScale).
        static readonly Vector3 SkiffBoxScale = new Vector3(1.3f, 0.35f, 2.2f);
        static readonly Color SkiffBoxColor = new Color(0.55f, 0.3f, 0.15f);

        // V3's juice controller picked muzzle prefabs, scale tweaks and impact bindings by weapon id. Those choices are
        // weapon and rule config now (WeaponConfig.visual, WeaponRulesConfig blast/impact bindings, M6); the juice
        // settings' own bindings stay keyed by the firing weapon's id.

        // Prewarm per kind (enough for the usual overlap: 4 mortar bursts, a 5-strike lightning chain, a keg trail).
        // M14.15: raised to the peaks of a six-weapon run at ~70 enemies (keg blasts peaked at 9).
        const int PrewarmMuzzle = 6, PrewarmBurst = 6, PrewarmMortarBurst = 8, PrewarmBlast = 12, PrewarmLightning = 8;
        const int PrewarmBeam = 2, PrewarmReticle = 2, PrewarmKeg = 32, PrewarmCutlass = 8, PrewarmSkiff = 3;

        sealed class Prepared
        {
            public ParticleSystem[] Particles;
            public Renderer[] Renderers;
            public Transform LiveChild;
            public ParticleSystem[] ChildParticles;
            public ParticleSystem.MinMaxGradient[] BaseColors;
            public LongNineBeamVisual Beam;
            public bool Done;
        }

        struct Timed
        {
            public GameObject Go;
            public float Left;
            public int Frame;
            public bool Pooled;
        }

        sealed class View
        {
            public GameObject Go;
            public bool Pooled;
            public Material OwnedMaterial;
            public ParticleSystem[] Particles;
            /// <summary>Hackathon: a keg's weapon (its blast is that weapon's).</summary>
            public string WeaponId;
        }

        PoolService _pools;
        IWeaponManager _weapons;
        IShipView _ship;
        DemoJuiceSettings _settings;
        GameObject _skiffModel;
        float _skiffScale = 0.42f;
        Func<WeaponRulesConfig> _rules;
        WeaponAimOverlay _overlay;
        Func<IReadOnlyList<OrbitBehavior.HitSphere>> _bladeHits;
        float _lastSplashTime = -10f;
        Vector3 _lastSplashPos;

        /// <summary>M14.9b: draws the timed landing rings (set by the presentation root).</summary>
        public Effects.HazardRingPresenter HazardRings
        {
            get => _hazardRings;
            set { _hazardRings = value; PrewarmRings(); } // Hackathon: the weapons' own landing and patch rings
        }
        Effects.HazardRingPresenter _hazardRings;
        public int SplashesPlayed { get; private set; }
        /// <summary>Demo: the artists' per-weapon VFX profiles (muzzle).</summary>
        public Effects.VfxProfilePlayer Profiles { get; set; }

        readonly Dictionary<(string kind, int prefab), string> _keys = new Dictionary<(string, int), string>();
        readonly Dictionary<int, Prepared> _prepared = new Dictionary<int, Prepared>();
        readonly List<Timed> _timed = new List<Timed>();
        readonly Dictionary<int, View> _handles = new Dictionary<int, View>();
        readonly List<View> _blades = new List<View>();
        readonly List<Vector3> _bladePositions = new List<Vector3>();
        int _nextHandle;
        Transform _looseRoot;
        Vector3 _lastForward = Vector3.forward;

        /// <summary>
        /// Binds the presenter. <paramref name="bladeHits"/> is where the simulation put the orbit blades this frame
        /// (default: <see cref="WeaponManager.Orbit"/>'s hit spheres when <paramref name="weapons"/> is the V4 manager);
        /// without it the blades are placed with the simulation's formula from the slot's orbit config.
        /// <paramref name="skiffModel"/> is the boarding skiff model (V3: the cannonboat's visual prefab); null draws V3's
        /// brown box. Registers and prewarms the "vfx." pools the settings' prefabs need.
        /// </summary>
        public void Bind(PoolService pools, IWeaponManager weapons, IShipView ship, DemoJuiceSettings settings,
            GameObject skiffModel = null, WeaponAimOverlay overlay = null, Func<IReadOnlyList<OrbitBehavior.HitSphere>> bladeHits = null,
            Func<WeaponRulesConfig> rules = null, float skiffScale = 0.42f)
        {
            Clear();
            if (!ReferenceEquals(pools, _pools))
            {
                _keys.Clear();
                _prepared.Clear();
            }
            _pools = pools;
            _weapons = weapons;
            _ship = ship;
            _settings = settings;
            _skiffModel = skiffModel;
            _skiffScale = skiffScale;
            _rules = rules;
            _overlay = overlay;
            _bladeHits = bladeHits != null ? bladeHits : DefaultBladeHits(weapons);
            RegisterPools();
        }

        /// <summary>The summon view model and scale from the weapon configs (V3: the Cannonboat model at 0.42).</summary>
        public static (GameObject model, float scale) FindSummonModel(ConfigSetRuntime config)
        {
            if (config == null) return (null, 0.42f);
            foreach (var w in config.All<WeaponConfig>())
                if (w != null && w.behavior == ConfigEnums.WeaponBehavior.Summon && w.visual != null && w.visual.summonModel != null
                    && !w.summon.perQuantity && !w.summon.retarget && w.summon.shotSpeed <= 0f) // V5 M4b summons draw their own models
                    return (w.visual.summonModel, w.visual.summonModelScale);
            return (null, 0.42f);
        }

        Func<IReadOnlyList<OrbitBehavior.HitSphere>> DefaultBladeHits(IWeaponManager weapons)
        {
            if (weapons == null) return null;
            return () => weapons.OrbitHitsOf(OrbitSlot()); // the cutlass-style weapon's blades (V5 M4: not just the first Orbit)
        }

        public int LiveHandleCount => _handles.Count;
        public int LiveTimedCount => _timed.Count;
        public int BladeCount => _blades.Count;
        public Vector3 BladePosition(int index) => _blades[index].Go != null ? _blades[index].Go.transform.position : Vector3.zero;

        // ---- Frame ------------------------------------------------------------------------------------------------

        void Update() => Tick(Time.deltaTime);

        void LateUpdate()
        {
            SyncBlades();
            SyncOrbitModels();
            SyncMaces(); // Hackathon (12 Oct): the Anchor Flail's chained maces (WeaponVfxPresenter.Maces.cs)
            SyncAuras(); // Hackathon (13 Oct): a weapon's aura prefab round the ship (WeaponVfxPresenter.Visuals.cs)
        }

        /// <summary>Ends timed effects (V3 VfxAutoReturn / Destroy(go, t)): scaled time, starting the frame after spawn.</summary>
        public void Tick(float dt)
        {
            TickSinking(dt);
            TickCoins(dt);
            TickPulses(dt);
            TickLobs(dt); // Hackathon art pass: the ink pots in flight (WeaponVfxPresenter.Lobs.cs)
            TickWhirlpools(dt); // Hackathon (13 Oct): the Maelstrom's whirlpool art (WeaponVfxPresenter.Visuals.cs)
            for (var i = _timed.Count - 1; i >= 0; i--)
            {
                var t = _timed[i];
                if (t.Go != null && Application.isPlaying && t.Frame == Time.frameCount) continue;
                t.Left -= dt;
                if (t.Left > 0f && t.Go != null)
                {
                    _timed[i] = t;
                    continue;
                }
                _timed.RemoveAt(i);
                Recycle(t.Go, t.Pooled);
            }
        }

        // ---- IWeaponEffects -------------------------------------------------------------------------------------

        /// <summary>V3 OnWeaponFired: the weapon's muzzle prefab (its CannonSmoke child when it has one) for 0.35 s.</summary>
        public void Fired(string weaponId, Vector3 origin, Vector3 direction)
        {
            _firedWeapon = weaponId;
            _firedFrame = Time.frameCount;
            var rotation = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction.normalized, Vector3.up) : Quaternion.identity;
            if (ProfileMuzzle(weaponId, origin, rotation, out var profileScale)) return;
            var prefab = Pick(weaponId, WeaponVisualSlot.Muzzle, ResolveMuzzlePrefab(weaponId)); // Hackathon: the weapon's muzzle prefab
            if (prefab == null) return;
            var scale = _settings.shootingVfxScale * profileScale;
            if (_settings.TryGetWeaponVfx(weaponId, out var binding) && binding.muzzleScaleMult > 0f)
                scale *= binding.muzzleScaleMult;
            else
                scale *= VisualOf(weaponId)?.fallbackMuzzleScale ?? 1f;
            if (PlayGunChildBurst(prefab, origin, rotation, scale, 0.35f)) return;
            PlayBurst(prefab, origin, rotation, scale, 0.35f);
        }

        /// <summary>V3 WeaponTargetingVisualizer.NotifyWeaponFired (the aim overlay's fire pulse).</summary>
        public void Aimed(string weaponId, Vector3 aimDirection, Vector3 targetPosition, bool hasTarget)
        {
            if (_overlay != null) _overlay.NotifyAimed(weaponId);
            _aimedWeapon = weaponId;
            _aimedFrame = Time.frameCount;
        }

        // Playtest (3 Oct, owner: no cannonball or shared placeholder for weapons that are not cannons). The strike a
        // weapon fires right after it aims (same frame) is that weapon's: the Cursed Doubloons' strikes fling gold coins
        // instead of the Lightning Mast's bolt.
        string _aimedWeapon;
        int _aimedFrame = -1;

        // Hackathon (12 Oct, owner: "a few gold coins (that we use in keg pickup) fly from the ship and hit the enemies"):
        // each strike throws a few of the gold barrel's coins (the juice settings' gold pickup model at its flying scale)
        // from the ship in a low arc to the struck enemy; there they glance off, fall and go back to the pool.
        struct Coin
        {
            public View View;
            public Vector3 Velocity;
            public float Left;
            public Vector3 From, To;
            /// <summary>Flight time so far (negative = still waiting at the ship), the flight's length and arc height.</summary>
            public float T, Flight, Arc;
            public bool Landed, Splash, Billboard;
            public float Scale, Spin, SpinMult;
            public string WeaponId;
        }

        // Hackathon (13 Oct, owner: "the enemies are dying before the coins hit them"): the sim's damage is instant at the
        // strike, so the coins now cross to the enemy in a fast, flat throw (at most ~0.1 s, all three nearly together)
        // and reach it while its death is still starting. Presentation only.
        const int CoinsPerStrike = 3;
        const float CoinGravity = 24f;
        const float CoinFlightSpeed = 220f;
        const float CoinMinFlight = 0.04f, CoinMaxFlight = 0.1f;
        const float CoinStagger = 0.015f;
        const float CoinSpriteSize = 1.1f;
        Material _coinSpriteMaterial;
        bool _coinSpriteLooked;
        Camera _coinCamera;
        readonly List<Coin> _coins = new List<Coin>();
        public int CoinCount => _coins.Count;

        /// <summary>Coins still on their way from the ship (tests).</summary>
        public int CoinsInFlight
        {
            get { var n = 0; foreach (var c in _coins) if (!c.Landed) n++; return n; }
        }

        /// <summary>A coin's position (tests).</summary>
        public Vector3 CoinPosition(int index) => _coins[index].View.Go != null ? _coins[index].View.Go.transform.position : Vector3.zero;

        /// <summary>The gold barrel's collect-particle coin (hackathon, 13 Oct, owner) on a camera-facing quad; else the
        /// keg pickup's gold coin (the juice settings' gold pickup model), else the coloured placeholder.</summary>
        GameObject CoinModel(out float scale, out bool billboard, string weaponId = null)
        {
            billboard = false;
            var own = Pick(weaponId, WeaponVisualSlot.Coin, null); // Hackathon: the weapon's coin prefab
            if (own != null)
            {
                billboard = WeaponVisualTuning.Of(own) is { faceCamera: true };
                scale = billboard ? CoinSpriteSize : _settings != null ? _settings.goldFlyingCoinScale : 1f;
                return own;
            }
            var sprite = CoinSpriteMaterial();
            if (sprite != null)
            {
                billboard = true;
                scale = CoinSpriteSize;
                return WeaponPlaceholderModels.CoinSprite(_pools != null ? _pools.Staging : null, sprite);
            }
            if (_settings != null && _settings.goldPickupPrefab != null)
            {
                scale = _settings.goldFlyingCoinScale;
                return _settings.goldPickupPrefab;
            }
            scale = 1f;
            return WeaponPlaceholderModels.Coin(_pools != null ? _pools.Staging : null);
        }

        /// <summary>The material of the gold barrel's coin particles (the one named for coins), looked up once.</summary>
        Material CoinSpriteMaterial()
        {
            if (_coinSpriteLooked) return _coinSpriteMaterial;
            _coinSpriteLooked = true;
            var barrel = _settings != null ? _settings.goldBarrelPickupPrefab : null;
            if (barrel == null) return null;
            foreach (var r in barrel.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                var m = r.sharedMaterial;
                if (m != null && m.name.IndexOf("Coin", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _coinSpriteMaterial = m;
                    break;
                }
            }
            return _coinSpriteMaterial;
        }

        Quaternion CoinFacing(Quaternion fallback)
        {
            if (_coinCamera == null || !_coinCamera.isActiveAndEnabled) _coinCamera = Camera.main;
            return _coinCamera != null ? _coinCamera.transform.rotation : fallback;
        }

        // V5 M8z (7 Oct): the coins are the weapon's data (visual.placeholderLook), not its id.
        bool StrikeIsDoubloons() => _aimedFrame == Time.frameCount && VisualOf(_aimedWeapon)?.placeholderLook == WeaponPlaceholderLook.Doubloons;

        void CoinBurst(string weaponId, Vector3 position, float radius)
        {
            var coin = CoinModel(out var scale, out var billboard, weaponId);
            var arcMult = WeaponVisualTuning.Arc(coin);
            var spinMult = WeaponVisualTuning.Spin(coin);
            var tunedScale = scale * WeaponVisualTuning.Scale(coin);
            var target = position;
            target.y = 0.7f;
            var from = _ship != null && _ship.Exists ? _ship.Position + Vector3.up * 1.1f : target + Vector3.up * 4f;
            var flat = target - from;
            flat.y = 0f;
            var dist = flat.magnitude;
            var dir = dist > 0.01f ? flat / dist : Vector3.forward;
            var side = Vector3.Cross(Vector3.up, dir);
            var flight = Mathf.Clamp(dist / CoinFlightSpeed, CoinMinFlight, CoinMaxFlight);
            var arc = (0.4f + dist * 0.03f) * arcMult;
            for (var i = 0; i < CoinsPerStrike; i++)
            {
                // A small fan, no random draw (the presenter draws none): middle, left, right.
                var lateral = (i == 0 ? 0f : i % 2 == 1 ? -1f : 1f) * 0.45f * ((i + 1) / 2);
                var go = Spawn(KindCoin, coin, from, Quaternion.LookRotation(dir, Vector3.up), scale, out var pooled);
                if (go == null) break;
                var prep = Prepare(go);
                if (!prep.Done) { StripPhysics(go); prep.Done = true; }
                _coins.Add(new Coin
                {
                    View = new View { Go = go, Pooled = pooled }, From = from + side * lateral * 0.3f, To = target + side * lateral,
                    T = -i * CoinStagger, Flight = flight, Arc = arc, Left = 0.45f * WeaponVisualTuning.Lifetime(coin), Splash = i == 0,
                    Billboard = billboard, Scale = tunedScale, Spin = i * 1.3f, SpinMult = spinMult, WeaponId = weaponId,
                });
            }
        }

        void TickCoins(float dt)
        {
            for (var i = _coins.Count - 1; i >= 0; i--)
            {
                var c = _coins[i];
                var go = c.View.Go;
                if (go == null) { _coins.RemoveAt(i); ReleaseView(c.View); continue; }
                var t = go.transform;
                if (!c.Landed)
                {
                    // In flight: from the ship to the enemy in a low arc, spinning.
                    c.T += dt;
                    var k = Mathf.Clamp01(c.T / Mathf.Max(0.01f, c.Flight));
                    t.position = Vector3.Lerp(c.From, c.To, k) + Vector3.up * (c.Arc * 4f * k * (1f - k));
                    SpinCoin(ref c, t, dt, 1080f * c.SpinMult);
                    if (k >= 1f)
                    {
                        // The hit: it glances off the enemy and falls.
                        c.Landed = true;
                        var back = c.From - c.To;
                        back.y = 0f;
                        c.Velocity = (back.sqrMagnitude > 0.0001f ? back.normalized : Vector3.zero) * 2.5f + Vector3.up * 5f;
                        if (c.Splash) WaterSplashFor(c.WeaponId, c.To);
                    }
                    _coins[i] = c;
                    continue;
                }
                c.Left -= dt;
                if (c.Left <= 0f)
                {
                    _coins.RemoveAt(i);
                    ReleaseView(c.View);
                    continue;
                }
                c.Velocity.y -= CoinGravity * dt;
                var p = t.position + c.Velocity * dt;
                if (p.y < 0.05f) { p.y = 0.05f; c.Velocity = Vector3.zero; }
                t.position = p;
                SpinCoin(ref c, t, dt, 900f * c.SpinMult);
                _coins[i] = c;
            }
        }

        /// <summary>A model coin spins about the vertical; a sprite coin faces the camera and flips (its width follows
        /// the spin, like the barrel's coin particles turning).</summary>
        void SpinCoin(ref Coin c, Transform t, float dt, float degreesPerSecond)
        {
            if (!c.Billboard)
            {
                t.rotation = Quaternion.Euler(0f, degreesPerSecond * dt, 0f) * t.rotation;
                return;
            }
            c.Spin += degreesPerSecond * Mathf.Deg2Rad * dt;
            t.rotation = CoinFacing(t.rotation);
            t.localScale = new Vector3(c.Scale * Mathf.Max(0.15f, Mathf.Abs(Mathf.Cos(c.Spin))), c.Scale, c.Scale);
        }

        /// <summary>V3 OnLongNineBeam: the laser beam prefab's line from the muzzle for laserBeamDuration.</summary>
        public void Beam(Vector3 origin, Vector3 direction, float range, float halfWidth)
        {
            // Hackathon: the beam of the weapon that fired this frame (the lane weapon fires, then draws its beam).
            var weapon = _firedFrame == Time.frameCount ? _firedWeapon : Equipped(ConfigEnums.WeaponBehavior.Projectile, c => c.lane.enabled && c.lane.beam);
            var prefab = Pick(weapon, WeaponVisualSlot.Beam, _settings != null ? _settings.laserBeamPrefab : null);
            if (_settings == null || prefab == null) return;
            // V3's broker pool left the instance at the prefab's own pose (the line is in world space).
            var go = Spawn(KindBeam, prefab, prefab.transform.position, prefab.transform.rotation, null, out var pooled);
            if (go == null) return;
            var prep = Prepare(go);
            if (prep.Beam == null)
            {
                prep.Beam = go.GetComponent<LongNineBeamVisual>();
                if (prep.Beam == null) prep.Beam = go.AddComponent<LongNineBeamVisual>();
            }
            prep.Beam.Configure(origin, direction, range, halfWidth);
            AddTimed(go, pooled, _settings.laserBeamDuration * WeaponVisualTuning.Lifetime(prefab));
        }

        /// <summary>
        /// V3 OnBroadsideSalvo: one muzzle burst 1.1 m out on the firing side. V3 always used the Broadside Battery's
        /// muzzle binding; the firing weapon's is used now, which is the same weapon (only Broadside fires side salvos).
        /// </summary>
        public void SideSalvo(string weaponId, Vector3 shipPosition, Vector3 sideDirection)
        {
            _firedWeapon = weaponId;
            _firedFrame = Time.frameCount;
            var prefab = Pick(weaponId, WeaponVisualSlot.Muzzle, ResolveMuzzlePrefab(weaponId)); // Hackathon: the weapon's muzzle prefab
            if (prefab == null) return;
            sideDirection.y = 0f;
            if (sideDirection.sqrMagnitude < 0.0001f) return;
            sideDirection.Normalize();
            var origin = shipPosition + sideDirection * 1.1f + Vector3.up * 0.35f;
            var rotation = Quaternion.LookRotation(sideDirection, Vector3.up);
            if (ProfileMuzzle(weaponId, origin, rotation, out var profileScale)) return;
            var scale = _settings.shootingVfxScale * 1.15f * profileScale;
            if (_settings.TryGetWeaponVfx(weaponId, out var binding) && binding.muzzleScaleMult > 0f)
                scale *= binding.muzzleScaleMult;
            if (!PlayGunChildBurst(prefab, origin, rotation, scale, 0.4f))
                PlayBurst(prefab, origin, rotation, scale, 0.4f);
        }

        /// <summary>V3 OnMineDropped: the barrel model, physics stripped, until the keg blows. -1 without a model.</summary>
        public int MineDropped(Vector3 position)
        {
            if (_settings == null) return -1;
            var weapon = Visuals != null ? Equipped(ConfigEnums.WeaponBehavior.Mine) : null; // Hackathon: the keg weapon's keg prefab
            var prefab = Pick(weapon, WeaponVisualSlot.Mine, _settings.mineBarrelPrefab);
            if (prefab == null) return -1;
            var go = Spawn(KindKeg, prefab, position, Quaternion.identity, _settings.mineBarrelScale, out var pooled);
            if (go == null) return -1;
            var prep = Prepare(go);
            if (!prep.Done)
            {
                StripPhysics(go);
                prep.Done = true;
            }
            return Track(new View { Go = go, Pooled = pooled, WeaponId = weapon });
        }

        public void MineRemoved(int handle) => ReleaseHandle(handle);

        /// <summary>V5: an unblown keg sinks (down and tilting) over <paramref name="seconds"/>, then goes back to its pool.</summary>
        public void MineSunk(int handle, float seconds)
        {
            if (!_handles.TryGetValue(handle, out var view)) return;
            _handles.Remove(handle);
            if (view.Go == null || seconds <= 0f) { ReleaseView(view); return; }
            var t = view.Go.transform;
            _sinking.Add(new Sinking { View = view, Left = seconds, Total = seconds, StartY = t.position.y, StartRotation = t.rotation });
        }

        /// <summary>Depth a sinking keg goes below its floating height before it is recycled.</summary>
        public const float KegSinkDepth = 1.4f;

        struct Sinking
        {
            public View View;
            public float Left, Total, StartY;
            public Quaternion StartRotation;
        }

        readonly List<Sinking> _sinking = new List<Sinking>();
        public int SinkingCount => _sinking.Count;

        void TickSinking(float dt)
        {
            for (var i = _sinking.Count - 1; i >= 0; i--)
            {
                var s = _sinking[i];
                s.Left -= dt;
                var go = s.View.Go;
                if (s.Left <= 0f || go == null)
                {
                    _sinking.RemoveAt(i);
                    ReleaseView(s.View);
                    continue;
                }
                var k = 1f - s.Left / s.Total; // 0 → 1
                var t = go.transform;
                var p = t.position;
                p.y = s.StartY - KegSinkDepth * k * k;
                t.position = p;
                t.rotation = s.StartRotation * Quaternion.Euler(0f, 0f, 35f * k);
                _sinking[i] = s;
            }
        }

        /// <summary>V3 OnMineExploded: the powder keg blast (only its impact, ring and smoke children), else a burst.</summary>
        public void Explosion(Vector3 position, float radius) => ExplosionFor(Visuals != null ? KegAt(position) : null, position, radius);

        /// <summary>The keg blast of <paramref name="weaponId"/> (its blast prefab; Hackathon), else the shared one.</summary>
        void ExplosionFor(string weaponId, Vector3 position, float radius)
        {
            if (_settings == null) return;
            var prefab = _settings.powderKegExplosionPrefab != null ? _settings.powderKegExplosionPrefab : _settings.weaponExplosionPrefab;
            prefab = Pick(weaponId, WeaponVisualSlot.Blast, prefab);
            if (prefab == null) return;

            var scaleMult = 1f;
            if (_settings.TryGetWeaponVfx(_rules?.Invoke()?.blastVfxBinding, out var binding) && binding.impactScaleMult > 0f)
                scaleMult = binding.impactScaleMult;
            var scale = Mathf.Max(1.15f, _settings.powderKegExplosionScale * 2.35f * scaleMult * Mathf.Clamp(radius / 2.8f, 0.9f, 1.35f));
            var burstPos = position + Vector3.up * 0.45f;
            if (PlayFilteredExplosion(prefab, burstPos, scale, 1.5f * WeaponVisualTuning.Lifetime(prefab))) return;
            PlayBurst(prefab, burstPos, Quaternion.identity, scale * 0.65f, 1.35f);
        }

        /// <summary>V3 OnLightningStrike: the strike prefab on the water, sized by the strike radius, for 1.15 s.</summary>
        public void LightningStrike(Vector3 position, float radius)
        {
            if (StrikeIsDoubloons()) { CoinBurst(_aimedWeapon, position, radius); return; }
            // Hackathon: the strike prefab of the strike weapon that aimed this frame (else the equipped one).
            var weapon = Visuals != null ? AimedOrEquipped(ConfigEnums.WeaponBehavior.AreaStrike, c => c.visual.placeholderLook != WeaponPlaceholderLook.Doubloons) : null;
            var prefab = Pick(weapon, WeaponVisualSlot.Strike, _settings != null ? _settings.lightningStrikePrefab : null);
            if (_settings == null || prefab == null) return;
            var strikePos = position;
            strikePos.y = 0.05f;
            var scale = _settings.lightningStrikeScale * Mathf.Clamp(radius / 3.5f, 0.85f, 1.35f);
            var go = Spawn(KindLightning, prefab, strikePos, Quaternion.identity, scale, out var pooled);
            if (go == null) return;
            Restart(Prepare(go).Particles);
            AddTimed(go, pooled, 1.15f * WeaponVisualTuning.Lifetime(prefab));
        }

        /// <summary>
        /// V3 OnFlamethrowerBurst only re-posed the juice controller's flame mount, which V4 keeps idle (the flame stream
        /// is FlameStreamPresenter's, register R-89), so it showed nothing; nothing to draw.
        /// </summary>
        public void FlameBurst(Vector3 origin, Vector3 aim) { }

        /// <summary>V3 OnMortarTelegraph: the tinted water reticle for the 1.1 s windup (+0.12 s), else a ring or trail.</summary>
        public void AreaTelegraph(Vector3 position, float radius)
        {
            if (_settings == null) return;
            TryLob(position, MortarWindupSeconds); // Hackathon art pass: the aimed weapon's lob model, if it has one
            position.y = 0.15f;
            var scale = MortarTelegraphScaleForRadius(radius, _settings.mortarTelegraphScale);
            var lifetime = MortarWindupSeconds + 0.12f;
            var template = _settings.mortarTelegraphPrefab != null ? _settings.mortarTelegraphPrefab : _settings.lightningStrikePrefab;
            if (template != null && PlayMortarReticle(template, position, scale, lifetime)) return;

            if (_settings.bossTelegraphRingPrefab != null)
            {
                PlayBurst(_settings.bossTelegraphRingPrefab, position, Quaternion.identity, scale * 0.55f, lifetime);
                return;
            }

            if (_settings.burningTrailPrefab == null) return;
            scale = _settings.burningTrailScale * Mathf.Clamp(radius / 8f, 0.55f, 0.95f);
            PlayBurst(_settings.burningTrailPrefab, position, Quaternion.identity, scale, lifetime);
        }

        /// <summary>The timed landing ring (R-27 fix: it fills over the shell's real delay), drawn by the hazard ring presenter.</summary>
        public void TimedRing(Vector3 position, float radius, float seconds)
        {
            TryLob(position, seconds); // Hackathon art pass: the aimed weapon's lob model (the ink pot), landing with the shell
            // Hackathon (13 Oct): whose ring: the predicted-area weapon that aimed this frame (a shell), else the shell that
            // landed here (its whirlpool or ink slick opening); none for an item's blast ring (the shared ring).
            string weapon = null;
            if (Visuals != null)
            {
                // A shell landed here this frame: this ring is its whirlpool or slick (opened right after the impact).
                var opening = LandedHere(position) && (weapon = ShellAt(position)) != null;
                if (!opening && _aimedFrame == Time.frameCount && ConfigOf(_aimedWeapon)?.behavior == ConfigEnums.WeaponBehavior.PredictedArea) weapon = _aimedWeapon;
                MarkShell(position, weapon);
                if (opening) ShowWhirlpool(weapon, position, radius, seconds);
            }
            if (HazardRings != null) HazardRings.ShowTimedRing(position, radius, seconds, Pick(weapon, WeaponVisualSlot.LandingRing, null));
        }

        /// <summary>
        /// V3 OnMortarImpact with no random stream (as M5 called it): 4 small bursts spread by V3's fallback pattern inside
        /// the reticle ring, else the keg blast.
        /// </summary>
        public void AreaImpact(Vector3 position, float radius)
        {
            if (_settings == null) return;
            var burstPrefab = _settings.mortarExplosionPrefab != null ? _settings.mortarExplosionPrefab : _settings.weaponExplosionPrefab;
            // Hackathon: the impact prefab of the shell that landed here (else the equipped shell weapon's).
            NoteLanding(position);
            if (Visuals != null)
                burstPrefab = Pick(ShellAt(position) ?? Equipped(ConfigEnums.WeaponBehavior.PredictedArea, c => !c.visual.pulseImpact), WeaponVisualSlot.Impact, burstPrefab);
            if (burstPrefab == null)
            {
                Explosion(position, radius);
                return;
            }

            const int burstCount = 4;
            var mortarScaleMult = _settings.mortarExplosionScaleMult;
            if (_settings.TryGetWeaponVfx(_rules?.Invoke()?.impactVfxBinding, out var binding) && binding.impactScaleMult > 0f)
                mortarScaleMult *= binding.impactScaleMult;
            var smallScale = _settings.weaponExplosionScale * mortarScaleMult * Mathf.Clamp(radius / 11f, 0.18f, 0.34f);
            // Keep bursts inside the visible telegraph ring (and away from the edge by VFX size).
            var ringRadius = MortarTelegraphScaleForRadius(radius, _settings.mortarTelegraphScale) * MortarReticleDiameterPerScale * 0.5f;
            var vfxPad = smallScale * 1.6f;
            var maxSpawnRadius = Mathf.Max(0.2f, ringRadius * 0.72f - vfxPad);
            for (var i = 0; i < burstCount; i++)
            {
                var angle = i * 1.05f;
                var dist = maxSpawnRadius * ((i + 1f) / (burstCount + 1f));
                var offset = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                PlayBurst(burstPrefab, position + offset + Vector3.up * 0.3f, Quaternion.identity, smallScale, 1.05f);
            }
        }

        // ---- Aura pulses (Playtest, 3 Oct, owner, round 7: "Siren Bell may have no visible effect") ---------------------
        // An aura weapon's pulse was the mortar's four small bursts round the ship, lost under the hull. It is now a ring
        // on the water in the weapon's own colour that sweeps out to the pulse's exact radius and fades, with a second
        // ring just behind it. V5 M8z (7 Oct, D21): the boost-only bow pulse went with the Ramming Prow; the Squid Ink Pots'
        // shell and slick pulse the same way (WeaponEffectsVisual.pulseImpact).
        // Pooled line renderers, one shared material.

        struct Pulse
        {
            public Vector3 Center;
            public float Radius, Left, Total, Width, StartFraction;
            public Color Color;
            public View View;
            public LineRenderer Line;
        }

        const int PulseSegments = 48;
        const float PulseHeight = 0.2f;
        static readonly Color AuraPulseColor = new Color(1f, 0.82f, 0.3f, 0.95f);
        readonly List<Pulse> _pulses = new List<Pulse>();
        GameObject _pulseTemplate;
        Material _pulseMaterial;
        public int PulseCount => _pulses.Count;

        /// <summary>An aura pulse without its weapon: a gold ring.</summary>
        public void AuraPulse(Vector3 center, float radius) => AddPulse(center, radius, AuraPulseColor, 0.5f, 0.3f, 0.45f, null);

        public void WeaponPulse(string weaponId, Vector3 center, float radius)
        {
            var config = ConfigOf(weaponId);
            var color = config != null ? config.aimOverlayColor : AuraPulseColor;
            color.a = 0.95f;
            // Hackathon (13 Oct): the weapon's pulse ring prefab (its tuning: width, colour, sweep time); its aura prefab
            // follows the ship at this radius (WeaponVfxPresenter.Visuals.cs).
            var prefab = Pick(weaponId, WeaponVisualSlot.PulseRing, null);
            NoteAura(weaponId, radius);
            NoteLanding(center); // an ink shell landing (its slick opens next)
            AddPulse(center, radius, color, 0.6f, 0.15f, 0.5f, prefab);
            AddPulse(center, radius * 0.8f, new Color(color.r, color.g, color.b, 0.6f), 0.75f, 0.05f, 0.3f, prefab);
        }

        void AddPulse(Vector3 center, float radius, Color color, float seconds, float startFraction, float width, GameObject prefab)
        {
            if (radius <= 0.05f) return;
            var go = Spawn(KindPulse, prefab != null ? prefab : PulseTemplate(), Vector3.zero, Quaternion.identity, null, out var pooled);
            if (go == null) return;
            var line = go.GetComponent<LineRenderer>();
            if (line == null) { Recycle(go, pooled); return; }
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = PulseSegments;
            seconds *= WeaponVisualTuning.Lifetime(prefab);
            _pulses.Add(new Pulse
            {
                Center = center + Vector3.up * WeaponVisualTuning.Height(prefab), Radius = radius, Left = seconds, Total = seconds,
                Width = width * WeaponVisualTuning.Width(prefab), StartFraction = startFraction, Color = WeaponVisualTuning.Colour(prefab, color),
                View = new View { Go = go, Pooled = pooled }, Line = line,
            });
        }

        void TickPulses(float dt)
        {
            for (var i = _pulses.Count - 1; i >= 0; i--)
            {
                var p = _pulses[i];
                p.Left -= dt;
                if (p.Left <= 0f || p.Line == null) { ReleaseView(p.View); _pulses.RemoveAt(i); continue; }
                _pulses[i] = p;
            }
            for (var i = 0; i < _pulses.Count; i++)
            {
                var p = _pulses[i];
                var line = p.Line;
                var t = 1f - p.Left / p.Total;                 // 0 → 1
                var eased = 1f - (1f - t) * (1f - t);          // fast out, slow at the edge
                var r = p.Radius * Mathf.Lerp(p.StartFraction, 1f, eased);
                var c = p.Color;
                c.a *= t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f; // holds at the full radius, then fades
                line.startColor = line.endColor = c;
                line.widthMultiplier = p.Width * Mathf.Lerp(1.2f, 0.7f, t);
                for (var s = 0; s < PulseSegments; s++)
                {
                    var a = s * Mathf.PI * 2f / PulseSegments;
                    line.SetPosition(s, new Vector3(p.Center.x + Mathf.Cos(a) * r, PulseHeight + p.Center.y, p.Center.z + Mathf.Sin(a) * r));
                }
            }
        }

        /// <summary>The code-built pulse line (a weapon without a pulse ring prefab), kept inactive as a pool template.</summary>
        GameObject PulseTemplate()
        {
            if (_pulseTemplate != null) return _pulseTemplate;
            if (_pulseMaterial == null) _pulseMaterial = new Material(Shader.Find("Sprites/Default")) { name = "WeaponPulse", hideFlags = HideFlags.DontSave };
            _pulseTemplate = new GameObject("WeaponPulse");
            _pulseTemplate.transform.SetParent(_pools != null && _pools.Staging != null ? _pools.Staging : transform, false);
            var line = _pulseTemplate.AddComponent<LineRenderer>();
            ConfigurePulseLine(line);
            line.sharedMaterial = _pulseMaterial;
            _pulseTemplate.SetActive(false);
            return _pulseTemplate;
        }

        /// <summary>The code-built pulse line's settings (the weapon pulse ring prefabs are built with these, HackathonWeaponPrefabs).</summary>
        public static void ConfigurePulseLine(LineRenderer line)
        {
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = PulseSegments;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        /// <summary>V3 OnBurningOilDropped: the burning trail prefab sized by the patch radius, for 1.6 s.</summary>
        public void BurningPatch(Vector3 position, float radius)
        {
            if (_settings == null || _settings.burningTrailPrefab == null) return;
            var scale = _settings.burningTrailScale * Mathf.Clamp(radius / 2.8f, 0.75f, 1.6f);
            PlayBurst(_settings.burningTrailPrefab, position, Quaternion.identity, scale, 1.6f);
        }

        /// <summary>V3 JuiceVfxBroker.PlayWaterSplash: the splash prefab at y 0.35 for 0.55 s, merged with a recent nearby one.</summary>
        public void WaterSplash(Vector3 position) => WaterSplashFor(SplashWeapon(position), position);

        /// <summary>The splash of <paramref name="weaponId"/> (its splash prefab; Hackathon), else the shared one.</summary>
        void WaterSplashFor(string weaponId, Vector3 position)
        {
            var prefab = Pick(weaponId, WeaponVisualSlot.WaterSplash, _settings != null ? _settings.waterImpactSplashPrefab : null);
            if (_settings == null || prefab == null) return;
            if (Time.time - _lastSplashTime < SplashMergeSeconds
                && (position - _lastSplashPos).sqrMagnitude < SplashMergeRadius * SplashMergeRadius)
                return;
            _lastSplashTime = Time.time;
            _lastSplashPos = position;
            position.y = 0.35f;
            PlayBurst(prefab, position, Quaternion.identity, _settings.waterImpactSplashScale, 0.55f);
            SplashesPlayed++;
        }

        /// <summary>V3 SaltwindCombatEffects.PlayExplosion: the keg blast at sea level (bomb boats, mines, traps, the boss).</summary>
        public void PlayExplosion(Vector3 position, float blastRadius)
        {
            position.y = 0f;
            ExplosionFor(null, position, blastRadius); // the enemies' blasts keep the shared keg blast
        }

        readonly Dictionary<string, string> _summonKinds = new Dictionary<string, string>();
        string SummonKind(string weaponId)
        {
            if (!_summonKinds.TryGetValue(weaponId, out var kind)) _summonKinds[weaponId] = kind = KindSkiff + ":" + weaponId;
            return kind;
        }

        /// <summary>V5 M4b: a summon with its weapon's own model (parrots, the monkey turret), pooled like the skiffs.</summary>
        public int SummonSpawned(string weaponId, Vector3 position, Vector3 forward)
        {
            WeaponConfig config = null;
            var slots = _weapons != null ? _weapons.Slots : null;
            if (slots != null)
                for (var i = 0; i < slots.Count; i++)
                    if (slots[i]?.Config != null && slots[i].Config.weaponId == weaponId) { config = slots[i].Config; break; }
            // Playtest (3 Oct, owner): a cannon asset standing in for a summon (the parrots' cannonball, the monkeys'
            // cannon barrel) is drawn as the weapon's coloured placeholder instead.
            var model = config != null ? WeaponPlaceholderModels.Summon(config.visual.placeholderLook, config.visual.summonModel, _pools != null ? _pools.Staging : null, _settings) : null;
            if (model != null) model = Pick(weaponId, WeaponVisualSlot.Summon, model); // Hackathon: the weapon's summon prefab
            if (model == null) return SkiffSpawned(position, forward);
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            var go = Spawn(SummonKind(weaponId), model, position + Vector3.up * 0.2f, Quaternion.LookRotation(forward.normalized, Vector3.up), config.visual.summonModelScale, out var pooled);
            if (go == null) return SkiffSpawned(position, forward);
            var prep = Prepare(go);
            if (!prep.Done) { StripPhysics(go); prep.Done = true; }
            return Track(new View { Go = go, Pooled = pooled });
        }

        /// <summary>V3 CreateBoardingSkiffVisual: the skiff model at 0.42 scale, colliders removed, or a brown box.</summary>
        public int SkiffSpawned(Vector3 position, Vector3 forward)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();
            var pose = position + Vector3.up * 0.2f;
            var rotation = Quaternion.LookRotation(forward, Vector3.up);

            if (_skiffModel != null)
            {
                var go = Spawn(KindSkiff, _skiffModel, pose, rotation, _skiffScale, out var pooled);
                if (go != null)
                {
                    var prep = Prepare(go);
                    if (!prep.Done)
                    {
                        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Kill(c);
                        prep.Done = true;
                    }
                    return Track(new View { Go = go, Pooled = pooled });
                }
            }

            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "BoardingSkiff";
            box.transform.SetParent(LooseRoot, false);
            box.transform.localScale = SkiffBoxScale;
            var col = box.GetComponent<Collider>();
            if (col != null) Kill(col);
            var view = new View { Go = box };
            var renderer = box.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                // V3 set renderer.material.color; an owned copy gives the same look without leaking an instance per box.
                view.OwnedMaterial = new Material(renderer.sharedMaterial) { color = SkiffBoxColor };
                renderer.sharedMaterial = view.OwnedMaterial;
            }
            box.transform.SetPositionAndRotation(pose, rotation);
            return Track(view);
        }

        public void SkiffMoved(int handle, Vector3 position, Vector3 facing)
        {
            if (!_handles.TryGetValue(handle, out var view) || view.Go == null) return;
            var t = view.Go.transform;
            t.position = position;
            if (facing.sqrMagnitude > 0.0001f) t.rotation = Quaternion.LookRotation(facing, Vector3.up);
        }

        public void SkiffRemoved(int handle) => ReleaseHandle(handle);

        /// <summary>Returns every live effect, keg, skiff and blade (run end, loadout replaced, rebind).</summary>
        public void Clear()
        {
            for (var i = _timed.Count - 1; i >= 0; i--) Recycle(_timed[i].Go, _timed[i].Pooled);
            _timed.Clear();
            foreach (var view in _handles.Values) ReleaseView(view);
            _handles.Clear();
            foreach (var s in _sinking) ReleaseView(s.View);
            _sinking.Clear();
            foreach (var c in _coins) ReleaseView(c.View);
            _coins.Clear();
            foreach (var p in _pulses) ReleaseView(p.View);
            _pulses.Clear();
            foreach (var w in _whirls) ReleaseView(w.View);
            _whirls.Clear();
            foreach (var a in _auras.Values) ReleaseView(a);
            _auras.Clear();
            _auraRadius.Clear();
            ReleaseBlades(0);
            foreach (var list in _orbitModels.Values) foreach (var view in list) ReleaseView(view);
            _orbitModels.Clear();
            ClearMaces();
            ClearLobs();
        }

        void OnDestroy()
        {
            Clear();
            if (_pulseMaterial != null) Destroy(_pulseMaterial);
            if (_pulseTemplate != null) Kill(_pulseTemplate);
            if (_linkMaterial != null) Destroy(_linkMaterial); // Hackathon art pass: the chain links' instancing copy
        }

        // ---- Orbit blades ---------------------------------------------------------------------------------------

        /// <summary>
        /// V3 SyncCutlasses, drawn where the simulation says the blades are (V3 computed both from its own constants; V4's
        /// hit spheres follow the slot's orbit config, so the drawn blades now follow it too). The blades lie flat, pointing
        /// along the ship's heading, with V3's yellow aura re-tinted every frame. With no orbit weapon or no ship they go;
        /// while the simulation has no new positions (paused) they stay where they are, as V3's did.
        /// </summary>
        public void SyncBlades()
        {
            var slot = OrbitSlot();
            // V3's blades were children of the ship, so they went with it.
            if (slot == null || (_ship != null && !_ship.Exists))
            {
                ReleaseBlades(0);
                return;
            }

            _bladePositions.Clear();
            var hits = _bladeHits != null ? _bladeHits() : null;
            if (hits != null)
            {
                for (var i = 0; i < hits.Count; i++) _bladePositions.Add(hits[i].Position);
            }
            else if (_ship != null && _ship.Exists)
            {
                ComputeBladePositions(slot, Time.time, _ship.Position, Quaternion.LookRotation(Heading(), Vector3.up), _bladePositions);
            }
            if (_bladePositions.Count == 0) return;

            ReleaseBlades(_bladePositions.Count);
            var bladePrefab = Pick(slot.Config.weaponId, WeaponVisualSlot.Orbit, _settings != null ? _settings.cutlassPrefab : null); // Hackathon
            while (_blades.Count < _bladePositions.Count) _blades.Add(CreateBlade(bladePrefab, _bladePositions[_blades.Count]));

            var rotation = Quaternion.LookRotation(Heading(), Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
            for (var i = 0; i < _blades.Count; i++)
            {
                var blade = _blades[i];
                if (blade.Go == null) continue;
                blade.Go.transform.SetPositionAndRotation(_bladePositions[i], rotation);
                TintCumulative(blade.Particles, CutlassAuraTint, 0.55f);
            }
        }

        /// <summary>
        /// The simulation's blade formula (OrbitBehavior.ComputeHits) for a ship at <paramref name="shipPosition"/> and
        /// <paramref name="shipRotation"/>; used only when the simulation's positions are not available.
        /// </summary>
        public static void ComputeBladePositions(WeaponSlot w, float time, Vector3 shipPosition, Quaternion shipRotation, List<Vector3> into)
        {
            if (w == null || w.Config == null || into == null) return;
            var o = w.Config.orbit;
            var count = o.baseBlades + w.QuantityBonus;
            var orbitRadius = o.orbitRadius * w.SizeMult;
            if (w.EvolutionEffect == ConfigEnums.EvolutionEffect.PulsingOrbit)
            {
                var pulse = w.Evolution.orbitPulse;
                orbitRadius *= pulse.minimum + pulse.amplitude * (0.5f + 0.5f * Mathf.Sin(time * pulse.frequency));
            }
            for (var index = 0; index < count; index++)
            {
                // V5 M8z (7 Oct, Q20): with a milestone's spin (this fallback does not carry the simulation's angle offset).
                // Hackathon playtest (12 Oct): a world angle, as the simulation's (the ship's yaw is taken off).
                var angle = (time * o.angularSpeedDegrees * w.SpinMult + index * (360f / Mathf.Max(1, count)) - shipRotation.eulerAngles.y) * Mathf.Deg2Rad;
                var localOffset = new Vector3(Mathf.Sin(angle), o.heightOffset, Mathf.Cos(angle)) * orbitRadius;
                into.Add(shipPosition + shipRotation * localOffset);
            }
        }

        /// <summary>The first Orbit weapon drawn as cutlass blades (one without its own orbit model).</summary>
        WeaponSlot OrbitSlot()
        {
            if (_weapons == null) return null;
            var slots = _weapons.Slots;
            if (slots == null) return null;
            for (var i = 0; i < slots.Count; i++)
                if (slots[i] != null && slots[i].Config != null && slots[i].Config.behavior == ConfigEnums.WeaponBehavior.Orbit
                    && slots[i].Config.visual.orbitModel == null) return slots[i];
            return null;
        }

        // ---- V5 M4: Orbit weapons with their own model (the Anchor Flail) -------------------------------------------

        readonly Dictionary<WeaponSlot, List<View>> _orbitModels = new Dictionary<WeaponSlot, List<View>>();
        readonly List<WeaponSlot> _orbitModelGone = new List<WeaponSlot>();

        /// <summary>Models drawn for an Orbit weapon (tests).</summary>
        public int OrbitModelCount(WeaponSlot slot) => slot != null && _orbitModels.TryGetValue(slot, out var list) ? list.Count : 0;

        /// <summary>Each Orbit weapon with an orbit model gets one pooled model per blade, where the simulation's blade is,
        /// spinning about its own axis; models of weapons that left go back to their pool.</summary>
        void SyncOrbitModels()
        {
            var slots = _weapons != null ? _weapons.Slots : null;
            var shipGone = _ship != null && !_ship.Exists;
            _orbitModelGone.Clear();
            foreach (var key in _orbitModels.Keys)
            {
                var keep = !shipGone && slots != null;
                if (keep)
                {
                    keep = false;
                    for (var i = 0; i < slots.Count; i++) if (ReferenceEquals(slots[i], key)) { keep = true; break; }
                }
                if (!keep) _orbitModelGone.Add(key);
            }
            foreach (var key in _orbitModelGone)
            {
                foreach (var view in _orbitModels[key]) ReleaseView(view);
                _orbitModels.Remove(key);
            }
            if (slots == null || shipGone) return;

            for (var s = 0; s < slots.Count; s++)
            {
                var slot = slots[s];
                if (slot == null || slot.Config == null || slot.Config.behavior != ConfigEnums.WeaponBehavior.Orbit) continue;
                var model = WeaponPlaceholderModels.Orbit(slot.Config.visual.placeholderLook, slot.Config.visual.orbitModel, _pools != null ? _pools.Staging : null, _settings); // Playtest (3 Oct, owner)
                if (model == null) continue;
                model = Pick(slot.Config.weaponId, WeaponVisualSlot.Orbit, model); // Hackathon: the weapon's orbit prefab
                var hits = _weapons.OrbitHitsOf(slot);
                if (hits == null || hits.Count == 0) continue; // paused: they stay where they are
                if (!_orbitModels.TryGetValue(slot, out var views)) _orbitModels[slot] = views = new List<View>();
                while (views.Count > hits.Count) { ReleaseView(views[views.Count - 1]); views.RemoveAt(views.Count - 1); }
                while (views.Count < hits.Count)
                {
                    var go = Spawn(KindOrbitModel, model, hits[views.Count].Position, Quaternion.identity, slot.Config.visual.orbitModelScale, out var pooled);
                    if (go == null) break;
                    var prep = Prepare(go);
                    if (!prep.Done) { StripPhysics(go); prep.Done = true; }
                    views.Add(new View { Go = go, Pooled = pooled, Particles = Array.Empty<ParticleSystem>() });
                }
                var spin = Quaternion.Euler(0f, Time.time * 540f * WeaponVisualTuning.Spin(model), 0f);
                for (var i = 0; i < views.Count && i < hits.Count; i++)
                    if (views[i].Go != null) views[i].Go.transform.SetPositionAndRotation(hits[i].Position, spin);
            }
        }

        /// <summary>The ship's flat heading (V3: MovementForward, else the ship transform's forward; V4 keeps the last one).</summary>
        Vector3 Heading()
        {
            if (_ship != null && _ship.Exists)
            {
                var forward = _ship.MovementForward;
                forward.y = 0f;
                if (forward.sqrMagnitude >= 0.0001f) _lastForward = forward.normalized;
            }
            return _lastForward;
        }

        View CreateBlade(GameObject prefab, Vector3 position)
        {
            if (prefab != null && _settings != null)
            {
                var go = Spawn(KindCutlass, prefab, position, Quaternion.identity, _settings.cutlassScale, out var pooled);
                if (go != null)
                {
                    var prep = Prepare(go);
                    if (!prep.Done)
                    {
                        ActivateCutlassVisuals(go);
                        HideCutlassRootMesh(go);
                        VfxParticleUtil.ApplyTint(go, CutlassAuraTint, 0.72f);
                        StripPhysics(go);
                        // V3 then added a trigger sphere for its hit test; V4's hits are the simulation's, so no collider.
                        prep.Particles = go.GetComponentsInChildren<ParticleSystem>(true);
                        prep.Done = true;
                    }
                    foreach (var trail in go.GetComponentsInChildren<TrailRenderer>(true)) trail.Clear();
                    return new View { Go = go, Pooled = pooled, Particles = prep.Particles };
                }
            }

            // V3's placeholder: a 0.3 m cube.
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "CutlassPlaceholder";
            cube.transform.SetParent(LooseRoot, false);
            cube.transform.localScale = Vector3.one * 0.3f;
            var col = cube.GetComponent<Collider>();
            if (col != null) Kill(col);
            cube.transform.position = position;
            return new View { Go = cube, Particles = Array.Empty<ParticleSystem>() };
        }

        /// <summary>Releases blades beyond <paramref name="keep"/> (V3 removed the last ones first).</summary>
        void ReleaseBlades(int keep)
        {
            while (_blades.Count > keep)
            {
                var last = _blades.Count - 1;
                ReleaseView(_blades[last]);
                _blades.RemoveAt(last);
            }
        }

        static void ActivateCutlassVisuals(GameObject root)
        {
            var blade = root.transform.Find("Blade");
            if (blade != null) blade.gameObject.SetActive(true);
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(true);
        }

        static void HideCutlassRootMesh(GameObject root)
        {
            var mesh = root.GetComponent<MeshRenderer>();
            if (mesh != null) mesh.enabled = false;
        }

        // ---- Effect kinds ---------------------------------------------------------------------------------------

        /// <summary>The equipped weapon's effect data (null for a weapon not in the loadout).</summary>
        WeaponEffectsVisual VisualOf(string weaponId)
        {
            var slots = _weapons != null ? _weapons.Slots : null;
            if (slots == null || weaponId == null) return null;
            for (var i = 0; i < slots.Count; i++)
                if (slots[i].Config != null && slots[i].Config.weaponId == weaponId) return slots[i].Config.visual;
            return null;
        }

        /// <summary>Demo: the weapon's muzzle profile. Plays its prefab; true when that replaces the default muzzle.
        /// <paramref name="defaultScale"/> is the slot's scale for the default when the slot has no prefab.</summary>
        bool ProfileMuzzle(string weaponId, Vector3 origin, Quaternion rotation, out float defaultScale)
        {
            defaultScale = 1f;
            var slot = Profiles != null ? Profiles.Slot(VfxSubject.Weapon, weaponId, VfxSlotKind.Muzzle) : null;
            if (slot == null) return false;
            if (!slot.HasPrefab) { defaultScale = slot.ScaleOrOne; return false; }
            Profiles.Play(slot, origin, rotation, _settings != null ? _settings.shootingVfxScale : 1f);
            return slot.SuppressesDefault;
        }

        GameObject ResolveMuzzlePrefab(string weaponId)
        {
            if (_settings == null) return null;
            if (_settings.TryGetWeaponVfx(weaponId, out var binding) && binding.muzzleVfxPrefab != null) return binding.muzzleVfxPrefab;
            if (_settings.cannonMuzzleVfxPrefab != null && (VisualOf(weaponId)?.cannonMuzzle ?? false)) return _settings.cannonMuzzleVfxPrefab;
            return _settings.shootingVfxPrefab;
        }

        /// <summary>V3 TryPlayGunChildBurst: the whole gun, but only its CannonSmoke child shows and plays.</summary>
        bool PlayGunChildBurst(GameObject gunPrefab, Vector3 position, Quaternion rotation, float scale, float lifetime)
        {
            if (gunPrefab == null || gunPrefab.transform.Find(GunSmokeChild) == null) return false;
            lifetime *= WeaponVisualTuning.Lifetime(gunPrefab);
            var go = Spawn(KindMuzzle, gunPrefab, position, rotation, scale, out var pooled);
            if (go == null) return false;
            var prep = Prepare(go);
            if (prep.LiveChild == null)
            {
                prep.LiveChild = go.transform.Find(GunSmokeChild);
                if (prep.LiveChild == null)
                {
                    Recycle(go, pooled);
                    return false;
                }
                DisableAllExcept(go.transform, prep.LiveChild);
                prep.ChildParticles = prep.LiveChild.GetComponentsInChildren<ParticleSystem>(true);
            }
            Restart(prep.ChildParticles);
            AddTimed(go, pooled, lifetime);
            return true;
        }

        /// <summary>V3 JuiceVfxBroker.Play (VfxPool.Play): the prefab, every particle system cleared and replayed.</summary>
        void PlayBurst(GameObject prefab, Vector3 position, Quaternion rotation, float scale, float lifetime)
        {
            position.y += WeaponVisualTuning.Height(prefab);
            lifetime *= WeaponVisualTuning.Lifetime(prefab);
            var go = Spawn(KindBurst, prefab, position, rotation, scale, out var pooled);
            if (go == null) return;
            var particles = Prepare(go).Particles;
            for (var i = 0; i < particles.Length; i++)
            {
                if (particles[i] == null) continue;
                particles[i].Clear(true);
                particles[i].Play(true);
            }
            AddTimed(go, pooled, lifetime);
        }

        /// <summary>V3 PlayFilteredExplosion: only the direct children named like the powder keg's burst parts.</summary>
        bool PlayFilteredExplosion(GameObject prefab, Vector3 position, float scale, float lifetime)
        {
            if (prefab == null || !HasNamedExplosionChild(prefab.transform)) return false;
            var go = Spawn(KindBlast, prefab, position, Quaternion.identity, scale, out var pooled);
            if (go == null) return false;
            var prep = Prepare(go);
            if (!prep.Done)
            {
                EnableNamedExplosionChildren(go.transform);
                prep.Done = true;
            }
            Restart(prep.Particles);
            AddTimed(go, pooled, lifetime);
            return true;
        }

        /// <summary>V3 TryPlayMortarReticle: the telegraph prefab's WaterReticle child, tinted with the mortar colour.</summary>
        bool PlayMortarReticle(GameObject template, Vector3 position, float scale, float lifetime)
        {
            var reticle = template.transform.Find(ReticleChild);
            if (reticle == null) return false;
            var go = Spawn(KindReticle, reticle.gameObject, position, Quaternion.identity, scale, out var pooled);
            if (go == null) return false;
            var prep = Prepare(go);
            if (prep.Renderers == null) prep.Renderers = go.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < prep.Renderers.Length; i++)
                if (prep.Renderers[i] != null) prep.Renderers[i].enabled = true;
            // V3 tinted a fresh copy each time; a pooled copy is tinted from its original colours so it never drifts.
            TintFromBase(prep, _settings.mortarTelegraphColor, _settings.mortarTelegraphTintSaturation, _settings.mortarTelegraphTintAlpha);
            Restart(prep.Particles);
            AddTimed(go, pooled, lifetime);
            return true;
        }

        public static float MortarTelegraphScaleForRadius(float blastRadius, float telegraphScaleSetting) =>
            telegraphScaleSetting * (blastRadius * 2f / MortarReticleDiameterPerScale);

        // ---- Pools and instances --------------------------------------------------------------------------------

        void RegisterPools()
        {
            if (_pools == null || _settings == null) return;
            var s = _settings;
            // Hackathon (13 Oct): with the weapon prefab catalog, each weapon prewarms its own copies (PrewarmModels), so
            // the shared prefabs only the weapons used are registered here without a prewarm (still there for a weapon
            // with an empty slot). The keg blast and the splash stay prewarmed: the enemies' blasts and shots use them.
            var shared = Visuals != null ? 0 : 1;
            if (s.weaponVfxBindings != null)
                foreach (var b in s.weaponVfxBindings)
                    if (b != null) RegisterMuzzle(b.muzzleVfxPrefab, PrewarmMuzzle * shared);
            RegisterMuzzle(s.cannonMuzzleVfxPrefab, PrewarmMuzzle * shared);
            RegisterMuzzle(s.shootingVfxPrefab, PrewarmMuzzle * shared);

            var kegBlast = s.powderKegExplosionPrefab != null ? s.powderKegExplosionPrefab : s.weaponExplosionPrefab;
            if (kegBlast != null) EnsurePool(HasNamedExplosionChild(kegBlast.transform) ? KindBlast : KindBurst, kegBlast, PrewarmBlast);
            var mortarBurst = s.mortarExplosionPrefab != null ? s.mortarExplosionPrefab : s.weaponExplosionPrefab;
            if (mortarBurst != null) EnsurePool(KindBurst, mortarBurst, PrewarmMortarBurst * shared);
            if (s.lightningStrikePrefab != null) EnsurePool(KindLightning, s.lightningStrikePrefab, PrewarmLightning * shared);
            if (s.laserBeamPrefab != null) EnsurePool(KindBeam, s.laserBeamPrefab, PrewarmBeam * shared);
            var telegraph = s.mortarTelegraphPrefab != null ? s.mortarTelegraphPrefab : s.lightningStrikePrefab;
            var reticle = telegraph != null ? telegraph.transform.Find(ReticleChild) : null;
            if (reticle != null) EnsurePool(KindReticle, reticle.gameObject, PrewarmReticle);
            if (s.burningTrailPrefab != null) EnsurePool(KindBurst, s.burningTrailPrefab, PrewarmBurst);
            if (s.waterImpactSplashPrefab != null) EnsurePool(KindBurst, s.waterImpactSplashPrefab, PrewarmSplash);
            if (s.mineBarrelPrefab != null) EnsurePool(KindKeg, s.mineBarrelPrefab, PrewarmKeg * shared);
            if (s.cutlassPrefab != null) EnsurePool(KindCutlass, s.cutlassPrefab, PrewarmCutlass * shared);
            if (_skiffModel != null) EnsurePool(KindSkiff, _skiffModel, PrewarmSkiff);
        }

        /// <summary>V5 M4: pools and prewarms the weapons' own models (orbit models: one per blade and two spare), so the
        /// first use mid-run creates nothing.</summary>
        public void PrewarmModels(IEnumerable<WeaponConfig> weapons)
        {
            if (_pools == null || weapons == null) return;
            // One pool per orbit model, sized to the most any weapon draws (an evolution may double the blades).
            var orbit = new Dictionary<GameObject, int>();
            foreach (var w in weapons)
            {
                if (w?.visual == null) continue;
                var staging = _pools.Staging;
                var orbitModel = WeaponPlaceholderModels.Orbit(w.visual.placeholderLook, w.visual.orbitModel, staging, _settings); // Playtest (3 Oct, owner)
                if (orbitModel != null) orbitModel = Pick(w.weaponId, WeaponVisualSlot.Orbit, orbitModel); // Hackathon: the weapon's orbit prefab
                if (w.behavior == ConfigEnums.WeaponBehavior.Orbit && orbitModel != null)
                {
                    var need = 2 * Mathf.Max(1, w.orbit.baseBlades) * Mathf.Max(1, w.orbit.beamSegments) + 2;
                    orbit[orbitModel] = orbit.TryGetValue(orbitModel, out var n) ? Mathf.Max(n, need) : need;
                }
                if (w.behavior == ConfigEnums.WeaponBehavior.Summon && w.visual.summonModel != null)
                    EnsurePool(SummonKind(w.weaponId), Pick(w.weaponId, WeaponVisualSlot.Summon, WeaponPlaceholderModels.Summon(w.visual.placeholderLook, w.visual.summonModel, staging, _settings)), w.summon.maxAlive > 0 ? w.summon.maxAlive : 4); // V5 M4b: parrots, the turret
                if (w.visual.placeholderLook == WeaponPlaceholderLook.Doubloons) EnsurePool(KindCoin, CoinModel(out _, out _, w.weaponId), CoinsPerStrike * 8); // Hackathon: a chain of strikes
                if (w.behavior == ConfigEnums.WeaponBehavior.ChainedMace) PrewarmMaces(w.weaponId, Mathf.Max(1, w.mace.maxPerAttack) + 2); // Hackathon (12 Oct)
                PrewarmLob(w.weaponId); // Hackathon art pass: a lob model (the ink pot) is pooled before the run
                PrewarmVisuals(w); // Hackathon (13 Oct): the weapon's own prefabs (WeaponVfxPresenter.Visuals.cs)
                if (w.behavior == ConfigEnums.WeaponBehavior.Aura || w.visual.pulseImpact)
                    EnsurePool(KindPulse, Pick(w.weaponId, WeaponVisualSlot.PulseRing, null) ?? PulseTemplate(), w.visual.pulseImpact ? PrewarmSlickPulse : PrewarmWeaponPulse);
            }
            foreach (var pair in orbit) EnsurePool(KindOrbitModel, pair.Key, pair.Value);
        }

        void RegisterMuzzle(GameObject prefab, int prewarm)
        {
            if (prefab == null) return;
            EnsurePool(prefab.transform.Find(GunSmokeChild) != null ? KindMuzzle : KindBurst, prefab, prewarm);
        }

        /// <summary>The pool key for a (kind, prefab); registered on first use (and prewarmed when asked).</summary>
        string EnsurePool(string kind, GameObject prefab, int prewarm)
        {
            var id = (kind, prefab.GetInstanceID());
            if (!_keys.TryGetValue(id, out var key))
            {
                var baseKey = KeyPrefix + kind + "/" + prefab.name;
                key = baseKey;
                for (var n = 2; _pools.Has(key); n++) key = baseKey + "#" + n;
                _pools.Register(new PoolDefinition(key, prefab, prewarm, 0, ConfigEnums.PoolOverflowPolicy.Grow,
                    prewarm > 0 ? "weapon effects" : "weapon effects (on demand)"));
                _keys[id] = key;
            }
            if (prewarm > 0) _pools.Prewarm(key);
            return key;
        }

        /// <summary>An instance at the pose (pooled when bound to a pool service; otherwise a plain copy, as V3's fallback).</summary>
        GameObject Spawn(string kind, GameObject prefab, Vector3 position, Quaternion rotation, float? scale, out bool pooled)
        {
            pooled = false;
            if (prefab == null) return null;
            GameObject go = null;
            if (_pools != null)
            {
                go = _pools.Acquire(EnsurePool(kind, prefab, 0), position, rotation);
                pooled = go != null;
            }
            if (go == null)
            {
                go = Instantiate(prefab, position, rotation, LooseRoot);
                go.SetActive(true);
            }
            // Hackathon (13 Oct): the prefab's tuning scales what the code sets (1 without one).
            if (scale.HasValue) go.transform.localScale = Vector3.one * (scale.Value * WeaponVisualTuning.Scale(prefab));
            return go;
        }

        Prepared Prepare(GameObject go)
        {
            var id = go.GetInstanceID();
            if (!_prepared.TryGetValue(id, out var prep))
            {
                prep = new Prepared { Particles = go.GetComponentsInChildren<ParticleSystem>(true) };
                _prepared[id] = prep;
            }
            return prep;
        }

        void AddTimed(GameObject go, bool pooled, float lifetime) =>
            _timed.Add(new Timed { Go = go, Pooled = pooled, Left = Mathf.Max(0.05f, lifetime), Frame = Time.frameCount });

        int Track(View view)
        {
            var handle = _nextHandle++;
            _handles[handle] = view;
            return handle;
        }

        void ReleaseHandle(int handle)
        {
            if (!_handles.TryGetValue(handle, out var view)) return;
            _handles.Remove(handle);
            ReleaseView(view);
        }

        void ReleaseView(View view)
        {
            Recycle(view.Go, view.Pooled);
            if (view.OwnedMaterial != null) Kill(view.OwnedMaterial);
            view.OwnedMaterial = null;
            view.Go = null;
        }

        /// <summary>Back to its pool (only if the pool still owns it), or destroyed when it was a plain copy.</summary>
        void Recycle(GameObject go, bool pooled)
        {
            if (ReferenceEquals(go, null)) return;
            if (pooled)
            {
                if (_pools != null && _pools.IsPooled(go)) _pools.Release(go);
                return;
            }
            _prepared.Remove(go.GetInstanceID());
            if (go != null) Kill(go);
        }

        Transform LooseRoot
        {
            get
            {
                if (_looseRoot == null)
                {
                    _looseRoot = new GameObject("WeaponVfx").transform;
                    _looseRoot.SetParent(transform, false);
                }
                return _looseRoot;
            }
        }

        // ---- Helpers (V3 WeaponJuiceController / VfxParticleUtil) -----------------------------------------------

        /// <summary>VfxParticleUtil.RestartParticles over a cached list.</summary>
        static void Restart(ParticleSystem[] particles)
        {
            if (particles == null) return;
            for (var i = 0; i < particles.Length; i++)
            {
                var ps = particles[i];
                if (ps == null) continue;
                var main = ps.main;
                main.startDelay = 0f;
                ps.Clear(true);
                ps.Play(true);
            }
        }

        /// <summary>VfxParticleUtil.ApplyTint applied to the instance's original start colours (not compounding).</summary>
        static void TintFromBase(Prepared prep, Color tint, float saturation, float alphaMultiplier)
        {
            var particles = prep.Particles;
            if (prep.BaseColors == null)
            {
                prep.BaseColors = new ParticleSystem.MinMaxGradient[particles.Length];
                for (var i = 0; i < particles.Length; i++)
                    if (particles[i] != null) prep.BaseColors[i] = particles[i].main.startColor;
            }
            for (var i = 0; i < particles.Length; i++)
            {
                if (particles[i] == null) continue;
                var main = particles[i].main;
                var baseColor = prep.BaseColors[i].color;
                var blended = Color.Lerp(baseColor, tint, saturation);
                blended.a = baseColor.a * alphaMultiplier;
                main.startColor = blended;
            }
        }

        /// <summary>VfxParticleUtil.ApplyTint as V3 re-applied it to the blades every frame (compounding toward the tint).</summary>
        static void TintCumulative(ParticleSystem[] particles, Color tint, float saturation)
        {
            if (particles == null) return;
            for (var i = 0; i < particles.Length; i++)
            {
                if (particles[i] == null) continue;
                var main = particles[i].main;
                var baseColor = main.startColor.color;
                var blended = Color.Lerp(baseColor, tint, saturation);
                blended.a = baseColor.a;
                main.startColor = blended;
            }
        }

        static bool HasNamedExplosionChild(Transform root)
        {
            for (var i = 0; i < root.childCount; i++)
                if (Array.IndexOf(PowderKegExplosionChildNames, root.GetChild(i).name) >= 0) return true;
            return false;
        }

        static void EnableNamedExplosionChildren(Transform root)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                var keep = Array.IndexOf(PowderKegExplosionChildNames, child.name) >= 0;
                child.gameObject.SetActive(keep);
                if (!keep) continue;
                foreach (var r in child.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            }
        }

        static void DisableAllExcept(Transform root, Transform keep)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                if (r != null) r.enabled = r.transform == keep || r.transform.IsChildOf(keep);
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps == null) continue;
                var t = ps.transform;
                if (t == keep || t.IsChildOf(keep)) continue;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                t.gameObject.SetActive(false);
            }
        }

        static void StripPhysics(GameObject root)
        {
            foreach (var body in root.GetComponentsInChildren<Rigidbody>(true)) Kill(body);
            foreach (var c in root.GetComponentsInChildren<Collider>(true)) Kill(c);
            foreach (var obstacle in root.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true)) Kill(obstacle);
        }

        static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }
    }
}
