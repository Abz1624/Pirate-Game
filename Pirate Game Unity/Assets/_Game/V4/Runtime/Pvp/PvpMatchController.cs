using System;
using System.Collections.Generic;
using RoguePirates.V4.Composition;
using RoguePirates.V4.Config;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Foundation.World;
using RoguePirates.V4.Online;
using RoguePirates.V4.Ports;
using RoguePirates.V4.Spawning;
using RoguePirates.V4.Weapons;
using UnityEngine;

namespace RoguePirates.V4.Pvp
{
    /// <summary>A barrel on the water (server id, kind, where, when it goes).</summary>
    public sealed class PvpBarrel
    {
        public int Id;
        public bool Xp;
        public Vector3 World;
        public long ExpiresAt;
        internal float NextClaimAt;
    }

    /// <summary>A bot volley or an opponent's mirrored shot in flight (for the presenter's ball; its colour and arc
    /// height).</summary>
    public struct PvpShot
    {
        public Vector3 From, To;
        public float Start, End;
        public Color Color;
        public float Arc;
    }

    /// <summary>What the match HUD and the world presenter read (and the buttons they press).</summary>
    public interface IPvpMatchView
    {
        bool Ready { get; }
        bool IsBotMatch { get; }
        string MyName { get; }
        string OpponentName { get; }
        int MyRating { get; }
        int OpponentRating { get; }
        long ServerNowMs { get; }
        long StartsAt { get; }
        long EndsAt { get; }
        bool Started { get; }
        Vector3 ArenaCentre { get; }
        float StormRadius { get; }
        bool StormShrinking { get; }
        long StormStartsAt { get; }
        bool OutsideStorm { get; }
        int MyLevel { get; }
        float MyHp { get; }
        float MyMaxHp { get; }
        bool MySunk { get; }
        Vector3 OpponentPosition { get; }
        Quaternion OpponentRotation { get; }
        bool OpponentKnown { get; }
        float OpponentHp { get; }
        float OpponentMaxHp { get; }
        int OpponentLevel { get; }
        bool OpponentAlive { get; }
        bool OpponentConnected { get; }
        long OpponentForfeitAt { get; }
        float DamageDealt { get; }
        float DamageTaken { get; }
        int BarrelsTaken { get; }
        IReadOnlyCollection<PvpBarrel> Barrels { get; }
        IReadOnlyList<PvpShot> Shots { get; }
        /// <summary>Playtest (3 Oct, owner, round 7): the opponent's weapons (id, name, level), from its "fx" loadout (the
        /// bot: its cannons).</summary>
        IReadOnlyList<(string id, string name, int level)> OpponentWeapons { get; }
        bool Ended { get; }
        MatchEndMsg End { get; }
        string EndJson { get; }
        string MyPlayerId { get; }
        string OpponentPlayerId { get; }
        /// <summary>"waiting for opponent", "opponent wants a rematch", ... (empty: none).</summary>
        string RematchStatus { get; }
        long RematchMsLeft { get; }
        /// <summary>A short line for the HUD (barrels, level-ups); the frame it changed.</summary>
        string LastNotice { get; }
        int NoticeSerial { get; }
        /// <summary>The model to copy for the opponent (the player's ship model) and whether models face -Z.</summary>
        Transform ShipModel { get; }
        bool ModelFacesNegativeZ { get; }
        void AttachOpponentView(Transform view);
        void Rematch();
        void MainMenu();
    }

    /// <summary>
    /// Playtest (3 Oct, owner): the 1v1 match inside the run. The run is the Saltwind run with everything PvE off (one switch:
    /// <see cref="RunRequest.Mode"/>); this controller adds the match: the seat's spawn, the opponent as a remote enemy unit
    /// every weapon targets (its position from the network, interpolated; its hull from its own reports), local weapon
    /// damage on it sent as "hit" (one batch per 0.1 s; never applied here), incoming hits through the PvP rules
    /// (<see cref="PvpRules"/>), barrels (claims; one level-up per XP barrel, 20% hull per health barrel), the storm ring
    /// and its damage, the automatic level-ups by draft priority (<see cref="PvpLevelOrder"/>), the end, and, for a bot
    /// match, the bot (<see cref="PvpBot"/>) the host's client speaks for.
    /// </summary>
    public sealed class PvpMatchController : MonoBehaviour, IPvpMatchView
    {
        /// <summary>Hackathon (12 Oct, U-3): how the match notices name the practice bot ("Captain Marrow" in the hackathon
        /// build; display only).</summary>
        public static string BotDisplayName = "The bot";

        RoguePirates.V4.Foundation.Core.SessionClock _clock;
        /// <summary>V5 M9c S1: the run's session clock (V4Game sets it; Unity's frame without one).</summary>
        public RoguePirates.V4.Foundation.Core.SessionClock Clock { get => _clock ??= new RoguePirates.V4.Foundation.Core.SessionClock(); set => _clock = value; }

        PvpSession _session;
        MatchStartMsg _match;
        ShipPort _ship;
        PlayerDamageService _damage;
        BuildModule _build;
        EnemySimulation _sim;
        Views.IToastSink _toasts;
        PvpLevelOrder _order;
        readonly PvpInterpolator _interp = new PvpInterpolator();
        readonly PvpHitWindow _hits = new PvpHitWindow();
        readonly Dictionary<int, PvpBarrel> _barrels = new Dictionary<int, PvpBarrel>();
        readonly List<PvpShot> _shots = new List<PvpShot>();
        // Hackathon playtest (12 Oct, owner): the bot's guns are the game's weapons (PvpBotArsenal); their hits on the
        // ship are batched like a player's and sent as the bot's.
        PvpBotArsenal _arsenal;
        float _botBatch, _botBatchAt;
        readonly Dictionary<string, float> _botBatchByWeapon = new Dictionary<string, float>();
        bool _botTagFallback;
        readonly Dictionary<string, float> _batchByWeapon = new Dictionary<string, float>();
        HashSet<string> _draftWeapons;
        Dictionary<string, string> _evolutionBase;
        Vector3 _centre;
        int _mySeat, _oppSeat;
        string _myId, _oppId;
        int _oppSlot = -1;
        float _oppHp = 1f, _oppMaxHp = 1f;
        int _oppLevel = 1;
        bool _oppAlive = true, _oppConnected = true, _oppKnown;
        long _oppForfeitAt;
        Vector3 _oppPosition;
        Quaternion _oppRotation = Quaternion.identity;
        Transform _oppView;
        PvpBot _bot;
        bool _botDeadSent;
        float _batch, _batchAt, _stateAt, _outsideSeconds, _botOutsideSeconds, _speedSeen = 10f;
        bool _ready, _began, _started, _sunk, _ended;
        float _dealt, _taken;
        int _barrelsTaken;
        string _rematchStatus = string.Empty;
        string _notice = string.Empty;
        int _noticeSerial;
        WeaponEffectsForwarder _mirror;
        Func<IWeaponEffects> _vfx;
        ConfigSetRuntime _config;
        readonly List<PvpFx> _fxOut = new List<PvpFx>();
        readonly Dictionary<string, float> _fxLastByWeapon = new Dictionary<string, float>();
        readonly List<(string id, string name, int level)> _oppWeapons = new List<(string, string, int)>();
        Dictionary<string, WeaponConfig> _weaponConfigs;
        float _fxAt, _loadoutAt;

        // ---- Setup (V4Game: Prepare before the run starts, Begin after) ----

        /// <summary>The match this run plays, or false (no match: the scene was opened without one).</summary>
        public bool Prepare(PvpSession session)
        {
            _session = session;
            _match = session != null ? session.Match : null;
            if (_match == null || string.IsNullOrEmpty(_match.matchId))
            {
                Debug.LogError("PvpMatchController: no match (open PvP from the main menu).");
                return false;
            }
            _myId = _match.youPlayerId;
            _mySeat = _match.you;
            _oppSeat = 1 - _mySeat;
            _oppId = _match.Player(_oppSeat)?.playerId;
            var mine = _match.Player(_mySeat)?.draft;
            _order = new PvpLevelOrder(mine?.weapons ?? Array.Empty<string>(), mine?.logbooks ?? Array.Empty<string>());
            _draftWeapons = new HashSet<string>(mine?.weapons ?? Array.Empty<string>());
            _session.MatchMessage += OnMessage;
            _session.MatchStarted += OnResync;
            return true;
        }

        /// <summary>The seat's draft (priority order) for the run's starting build.</summary>
        public PvpDraft MyDraft => _match?.Player(_mySeat)?.draft;

        /// <summary>The run's ship placement: the seat's spawn on the arena (centred on the play area), facing the centre.</summary>
        public void PlaceShip(PlayerShipController ship)
        {
            _centre = PlayBounds.Center;
            var spawn = _match.Spawn(_mySeat);
            var p = spawn != null ? PvpRules.ToWorld(_centre, spawn.x, spawn.z) : _centre;
            ship.SetWorldPosition(p);
            ship.SetHeading(PvpRules.Forward(spawn != null ? spawn.yaw : 0f));
        }

        /// <summary>After the run started: the opponent unit, the barrels already out (a rejoin), the countdown.</summary>
        public void Begin(ShipPort ship, PlayerDamageService damage, BuildModule build, EnemySimulation sim, Views.IToastSink toasts)
        {
            _ship = ship;
            _damage = damage;
            _build = build;
            _sim = sim;
            _toasts = toasts;
            _sim.RemoteDamaged += OnRemoteDamaged;
            _startLevel = build.Progression.Level;
            var spawn = _match.Spawn(_oppSeat);
            _oppPosition = spawn != null ? PvpRules.ToWorld(_centre, spawn.x, spawn.z) : _centre;
            _oppRotation = Quaternion.LookRotation(PvpRules.Forward(spawn != null ? spawn.yaw : 0f));
            _oppMaxHp = _oppHp = Mathf.Max(1f, ship.MaxHp);
            var radius = Mathf.Max(1.6f, ship.ContactRadius * 1.4f);
            _oppSlot = sim.SpawnRemote(OpponentDefinition(sim), _oppPosition, radius);
            if (_oppSlot < 0) Debug.LogError("PvpMatchController: could not place the opponent unit.");
            if (_match.HasBot && _match.bot.simulatedBy == _myId)
                _bot = new PvpBot(_match.seed, _oppPosition, spawn != null ? spawn.yaw : 270f, _oppMaxHp);
            AdoptLiveBarrels(_match);
            _ship.SetMovementEnabled(false);
            _began = true;
            _ready = true;
            if (_match.live != null && _match.live.ended) _ended = true;
            // Playtest (4 Oct): a match that ended while this scene loaded (an early forfeit) shows its result at once.
            if (_session.LastMatchEnd != null && _session.LastMatchEnd.matchId == _match.matchId) _ended = true;
        }

        /// <summary>An ordinary ship definition for the opponent unit (its own model replaces the enemy's).</summary>
        static int OpponentDefinition(EnemySimulation sim)
        {
            var defs = sim.Definitions;
            if (defs == null) return 0;
            for (var i = 0; i < defs.Length; i++)
                if (defs[i] != null && defs[i].category == RoguePirates.V4.Foundation.Core.EnemyCategory.Ship && !defs[i].isBoss && !defs[i].isMiniboss) return i;
            return 0;
        }

        /// <summary>The level-up the progression applies (no cards, no pause): the next pick by priority, skipping maxed ones.</summary>
        public void AutoLevel()
        {
            var progression = _build.Progression;
            var pick = _order.Next(p => progression.IsMaxed(p.IsWeapon, p.Id));
            if (pick == null) return;
            progression.RaisePick(pick.Value.IsWeapon, pick.Value.Id);
            Notice($"Level {MyLevel}: {Name(pick.Value)} up");
        }

        string Name(PvpPick pick)
        {
            if (pick.IsWeapon)
            {
                var i = _build.Progression.Loadout.IndexOf(pick.Id);
                return i >= 0 && !string.IsNullOrEmpty(_build.Progression.Loadout.Weapons[i].Config.displayName) ? _build.Progression.Loadout.Weapons[i].Config.displayName : pick.Id;
            }
            var l = _build.Logbook(pick.Id);
            return l != null && !string.IsNullOrEmpty(l.displayName) ? l.displayName : pick.Id;
        }

        /// <summary>The ship sank (the run's death flow hands it here): tell the server; the match ends there.</summary>
        public void OnSunk()
        {
            if (_sunk) return;
            _sunk = true;
            SendMyState(true);
            _session.SendRaw("{\"t\":\"dead\"}");
            Notice("Your ship sank!");
        }

        void OnDestroy()
        {
            if (_session != null)
            {
                _session.MatchMessage -= OnMessage;
                _session.MatchStarted -= OnResync;
            }
            if (_sim != null) _sim.RemoteDamaged -= OnRemoteDamaged;
            if (_mirror != null) _mirror.Mirror -= OnMyWeaponFx;
            _arsenal?.Dispose();
        }

        // ---- Frame ----

        void Update()
        {
            if (!_began) return;
            var dt = Clock.DeltaTime;
            var now = ServerNowMs;
            if (!_started && now >= _match.startsAt)
            {
                _started = true;
                if (!_sunk && !_ended) _ship.SetMovementEnabled(true);
                Notice("Fight!");
            }
            if (!_started) _ship.SetMovementEnabled(false); // the countdown (the run's own start enables the ship)
            var speed = new Vector2(_ship.Velocity.x, _ship.Velocity.z).magnitude;
            if (speed > _speedSeen && !_ship.IsBoosting) _speedSeen = Mathf.Lerp(_speedSeen, speed, 0.05f);

            TickOpponent(dt, now);
            TickPooledHits();
            TickStorm(dt, now);
            TickBarrels(now);
            // V5 M8z (6 Oct, audit): the position goes out before the hits, so after a stall (a background tab, a hitch)
            // the server reads the hits against where the ship is now
            if (Clock.TimeSingle >= _stateAt)
            {
                _stateAt = Clock.TimeSingle + 1f / PvpRules.StateHz;
                if (!_ended) SendMyState(false);
                if (_bot != null && !_ended) SendBotState();
            }
            FlushHits();
            FlushBotHits();
            FlushFx();
            for (var i = _shots.Count - 1; i >= 0; i--) if (Clock.TimeSingle > _shots[i].End + 0.3f) _shots.RemoveAt(i);
        }

        void TickOpponent(float dt, long now)
        {
            if (_bot != null)
            {
                _bot.Speed = Mathf.Max(8f, _speedSeen * 0.85f);
                _bot.Tick(dt, _ship.Position, _ship.IsAlive && !_sunk, _centre, StormRadius, _barrels.Values, _started && !_ended);
                _oppPosition = _bot.Position;
                _oppRotation = Quaternion.LookRotation(PvpRules.Forward(_bot.Yaw));
                _oppHp = _bot.Hp;
                _oppMaxHp = _bot.MaxHp;
                _oppLevel = _bot.Level;
                _oppAlive = _bot.Alive;
                _oppKnown = true;
                // Hackathon playtest (12 Oct, owner): the bot fires its drafted weapons through the real weapon systems
                // (was a made-up cannon volley drawn as plain spheres).
                if (_arsenal != null)
                {
                    _arsenal.ShipPosition = _bot.Position;
                    _arsenal.ShipVelocity = _bot.Velocity;
                    _arsenal.ShipYaw = _bot.Yaw;
                    _arsenal.ShipAlive = _bot.Alive;
                    _arsenal.TargetPosition = _ship.Position;
                    _arsenal.TargetVelocity = _ship.Velocity;
                    _arsenal.TargetRadius = Mathf.Max(1.2f, _ship.ContactRadius);
                    _arsenal.TargetAlive = _ship.IsAlive && !_sunk;
                    _arsenal.Active = _started && !_ended && _bot.Alive;
                    _arsenal.Tick(dt);
                }
            }
            else if (_interp.Sample(Clock.TimeSingle, out var p, out var yaw))
            {
                _oppPosition = p;
                _oppRotation = Quaternion.LookRotation(PvpRules.Forward(yaw));
            }
            var velocity = _bot != null ? _bot.Velocity : _interp.HasData ? _interp.Latest.Velocity : Vector3.zero;
            var modelRotation = ModelFacesNegativeZ ? _oppRotation * Quaternion.Euler(0f, 180f, 0f) : _oppRotation;
            if (_oppSlot >= 0) _sim.SetRemoteState(_oppSlot, _oppPosition, modelRotation, velocity, _oppHp, _oppMaxHp);
        }

        void TickStorm(float dt, long now)
        {
            if (!_started || _ended) return;
            var radius = StormRadius;
            if (!_sunk && _ship.IsAlive && Flat(_ship.Position - _centre) > radius)
            {
                _outsideSeconds += dt;
                var amount = PvpRules.StormDps(_match.storm, _outsideSeconds) * dt;
                _damage.ExactOverTime(amount, _ship.Position);
            }
            else _outsideSeconds = 0f;

            if (_bot != null && _bot.Alive)
            {
                if (Flat(_bot.Position - _centre) > radius)
                {
                    _botOutsideSeconds += dt;
                    _bot.Hp -= PvpRules.StormDps(_match.storm, _botOutsideSeconds) * dt;
                    CheckBotSunk();
                }
                else _botOutsideSeconds = 0f;
            }
        }

        void TickBarrels(long now)
        {
            if (_barrels.Count == 0) return;
            List<int> gone = null;
            foreach (var b in _barrels.Values)
            {
                if (now > b.ExpiresAt + 1000) { (gone ??= new List<int>()).Add(b.Id); continue; }
                if (!_started || _ended) continue;
                var reach = (_match.barrels != null && _match.barrels.claimRadius > 0f ? _match.barrels.claimRadius : 6f) - 0.75f;
                if (Clock.TimeSingle < b.NextClaimAt) continue;
                if (!_sunk && _ship.IsAlive && Flat(_ship.Position - b.World) <= reach)
                {
                    b.NextClaimAt = Clock.TimeSingle + 0.5f;
                    SendMyState(false); // the claim is checked against the last reported position
                    _session.Send(new PickupMsg { id = b.Id });
                }
                else if (_bot != null && _bot.Alive && Flat(_bot.Position - b.World) <= reach)
                {
                    b.NextClaimAt = Clock.TimeSingle + 0.5f;
                    SendBotState();
                    _session.Send(new PickupMsg { id = b.Id, asBot = true });
                }
            }
            if (gone != null) foreach (var id in gone) _barrels.Remove(id);
        }

        /// <summary>The largest batch the server takes (announced in match_start; 500 from an older server).</summary>
        float HitLimit => PvpRules.HitLimit(_match?.limits != null ? _match.limits.maxHitDamage : 0f);

        void FlushHits()
        {
            if (_batch <= 0f || Clock.TimeSingle < _batchAt) return;
            var weapon = PvpRules.GenericHitTag;
            var best = 0f;
            foreach (var kv in _batchByWeapon) if (kv.Value > best) { best = kv.Value; weapon = kv.Key; }
            var dmg = PvpRules.ClampHit(_batch, HitLimit);
            var rest = _batch - dmg;
            _batch = 0f;
            _batchByWeapon.Clear();
            if (dmg <= 0f || _ended || _sunk) return;
            _session.Send(new HitMsg { dmg = dmg, weaponId = weapon.Length > 64 ? weapon.Substring(0, 64) : weapon });
            _dealt += dmg;
            // V5 M8z (6 Oct, audit): a batch over the limit (a keg field going off at once) is not cut: the rest goes next
            if (rest > 0.05f)
            {
                _batch = rest;
                _batchByWeapon[weapon] = rest;
                _batchAt = Clock.TimeSingle + PvpRules.HitBatchSeconds;
            }
        }

        void OnRemoteDamaged(int slot, float damage, string weaponId)
        {
            if (slot != _oppSlot || !_started || _ended || _sunk || !_oppAlive || damage <= 0f) return;
            if (_batch <= 0f) _batchAt = Clock.TimeSingle + PvpRules.HitBatchSeconds;
            _batch += damage;
            // V5 M8z (6 Oct, audit): only drafted weapon ids or "weapon" reach the server (it refuses anything else)
            var key = PvpRules.HitTag(weaponId, _draftWeapons, EvolutionBase);
            _batchByWeapon.TryGetValue(key, out var sum);
            _batchByWeapon[key] = sum + damage;
        }

        /// <summary>The base weapon of an evolution id (null for anything else).</summary>
        string EvolutionBase(string id)
        {
            if (_config == null || string.IsNullOrEmpty(id)) return null;
            if (_evolutionBase == null)
            {
                _evolutionBase = new Dictionary<string, string>();
                foreach (var e in _config.All<WeaponEvolutionConfig>())
                    if (e != null && e.baseWeapon != null && !string.IsNullOrEmpty(e.evolutionId)) _evolutionBase[e.evolutionId] = e.baseWeapon.weaponId;
            }
            return _evolutionBase.TryGetValue(id, out var b) ? b : null;
        }

        // ---- Sending ----

        void SendMyState(bool sunk)
        {
            var a = PvpRules.ToArena(_centre, _ship.Position);
            var v = _ship.Velocity;
            _session.Send(new StateMsg
            {
                x = Round(a.x), z = Round(a.y), yaw = Round(PvpRules.YawOf(_ship.MovementForward)), vx = Round(v.x), vz = Round(v.z),
                hp = Round(sunk ? 0f : _ship.Hp), maxHp = Round(_ship.MaxHp), level = MyLevel, alive = !sunk && !_sunk && _ship.IsAlive,
                boosting = _ship.IsBoosting,
            });
        }

        void SendBotState()
        {
            var a = PvpRules.ToArena(_centre, _bot.Position);
            _session.Send(new StateMsg
            {
                x = Round(a.x), z = Round(a.y), yaw = Round(_bot.Yaw), vx = Round(_bot.Velocity.x), vz = Round(_bot.Velocity.z),
                hp = Round(Mathf.Max(0f, _bot.Hp)), maxHp = Round(_bot.MaxHp), level = _bot.Level, alive = _bot.Alive, boosting = _bot.Boosting, asBot = true,
            });
        }

        static float Round(float v) => Mathf.Round(v * 100f) / 100f;
        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        void CheckBotSunk()
        {
            if (_bot == null || !_bot.Alive || _bot.Hp > 0f) return;
            _bot.Hp = 0f;
            _bot.Alive = false;
            if (_botDeadSent) return;
            _botDeadSent = true;
            SendBotState();
            _session.SendRaw("{\"t\":\"dead\",\"asBot\":true}");
            Notice("The bot sank!");
        }

        // ---- Playtest (3 Oct, owner, round 7: "in pvp i couldn't see other player's weapons, attacks"): weapon fx ----

        const int FxPerMessage = 12;
        const int FxMaxChars = 1800; // the relay refuses fx over 2048 bytes
        const float FxSeconds = 0.1f, LoadoutSeconds = 2f, FiredGapSeconds = 0.08f;

        /// <summary>After Begin: my weapon effects are mirrored to the opponent (cosmetic; damage stays on "hit"); the
        /// opponent's are played here through the weapon presenter (never back through the mirror). In a bot match the
        /// bot's arsenal is built here (its draft's weapons and Logbooks, drawn through the same presenter).</summary>
        public void AttachWeapons(WeaponEffectsForwarder mirror, Func<IWeaponEffects> vfx, ConfigSetRuntime config, Func<IProjectileViews> projectileViews = null)
        {
            _vfx = vfx;
            _config = config;
            _mirror = mirror;
            if (_mirror != null && !IsBotMatch) _mirror.Mirror += OnMyWeaponFx;
            if (_bot != null && _arsenal == null && config != null)
            {
                var draft = _match?.Player(_oppSeat)?.draft;
                _arsenal = new PvpBotArsenal(config, _match.seed, draft?.weapons, draft?.logbooks, new PvpBotEffects(vfx), projectileViews);
                _arsenal.Hit = OnBotWeaponHit;
                AddBotHull(_arsenal.HullGained);
                _arsenal.HullAdded = AddBotHull;
            }
        }

        /// <summary>The bot's arsenal (tests).</summary>
        public PvpBotArsenal BotArsenal => _arsenal;

        void AddBotHull(float amount)
        {
            if (_bot == null || amount <= 0f) return;
            _bot.MaxHp += amount;
            _bot.Hp += amount;
        }

        void OnBotWeaponHit(float damage, string weaponId)
        {
            if (_bot == null || !_bot.Alive || !_started || _ended || _sunk || damage <= 0f) return;
            if (_botBatch <= 0f) _botBatchAt = Clock.TimeSingle + PvpRules.HitBatchSeconds;
            _botBatch += damage;
            var key = string.IsNullOrEmpty(weaponId) ? PvpBot.WeaponId : weaponId;
            _botBatchByWeapon.TryGetValue(key, out var sum);
            _botBatchByWeapon[key] = sum + damage;
        }

        /// <summary>The bot's weapon hits on my ship, one batch per 0.1 s, tagged with the weapon that dealt the most. A
        /// server that only knows the bot's cannons refuses that tag once; the bot's hits then carry the cannons tag.</summary>
        void FlushBotHits()
        {
            if (_botBatch <= 0f || Clock.TimeSingle < _botBatchAt) return;
            var weapon = PvpBot.WeaponId;
            var best = 0f;
            foreach (var kv in _botBatchByWeapon) if (kv.Value > best) { best = kv.Value; weapon = kv.Key; }
            if (_botTagFallback) weapon = PvpBot.WeaponId;
            var dmg = PvpRules.ClampHit(_botBatch, HitLimit);
            var rest = _botBatch - dmg;
            _botBatch = 0f;
            _botBatchByWeapon.Clear();
            if (dmg <= 0f || _ended || _sunk || _bot == null || !_bot.Alive) return;
            _session.Send(new HitMsg { dmg = dmg, weaponId = weapon.Length > 64 ? weapon.Substring(0, 64) : weapon, asBot = true });
            if (rest > 0.05f)
            {
                _botBatch = rest;
                _botBatchByWeapon[weapon] = rest;
                _botBatchAt = Clock.TimeSingle + PvpRules.HitBatchSeconds;
            }
        }

        void OnMyWeaponFx(char kind, string weaponId, Vector3 point, Vector3 direction, float radius)
        {
            if (!_started || _ended || _sunk || _ship == null || _fxOut.Count >= FxPerMessage * 2) return;
            if (kind == 'f' || kind == 's' || kind == 'b' || kind == 'p')
            {
                // a burst fires many shots in a frame (an aura pulses often): one mirrored per weapon per gap
                var key = (weaponId ?? string.Empty) + kind;
                if (_fxLastByWeapon.TryGetValue(key, out var last) && Clock.TimeSingle - last < FiredGapSeconds) return;
                _fxLastByWeapon[key] = Clock.TimeSingle;
            }
            var fx = new PvpFx { k = kind.ToString(), w = weaponId != null && weaponId.Length > 40 ? weaponId.Substring(0, 40) : weaponId, r = Round(radius) };
            if (kind == 'f' || kind == 's' || kind == 'b')
            {
                var offset = point - _ship.Position;
                fx.x = Round(offset.x);
                fx.z = Round(offset.z);
                var d = new Vector2(direction.x, direction.z).normalized;
                fx.dx = Round(d.x);
                fx.dz = Round(d.y);
            }
            else
            {
                var a = PvpRules.ToArena(_centre, point);
                fx.x = Round(a.x);
                fx.z = Round(a.y);
            }
            _fxOut.Add(fx);
        }

        /// <summary>At most one fx message per 0.1 s (the relay allows 30/s): the effects since the last, and every 2 s
        /// the loadout.</summary>
        void FlushFx()
        {
            if (Clock.TimeSingle < _fxAt || _session == null || IsBotMatch) return;
            var loadout = Clock.TimeSingle >= _loadoutAt && _build != null;
            if (_fxOut.Count == 0 && !loadout) return;
            _fxAt = Clock.TimeSingle + FxSeconds;
            var msg = new FxMsg { from = _myId };
            if (loadout)
            {
                _loadoutAt = Clock.TimeSingle + LoadoutSeconds;
                var weapons = _build.Progression.Loadout.Weapons;
                msg.lo = new string[weapons.Count];
                msg.lv = new int[weapons.Count];
                for (var i = 0; i < weapons.Count; i++) { msg.lo[i] = weapons[i].Id; msg.lv[i] = weapons[i].Level; }
            }
            msg.e = _fxOut.GetRange(0, Mathf.Min(FxPerMessage, _fxOut.Count)).ToArray();
            _fxOut.Clear();
            var json = JsonUtility.ToJson(msg);
            while (json.Length > FxMaxChars && msg.e.Length > 0)
            {
                Array.Resize(ref msg.e, msg.e.Length / 2);
                json = JsonUtility.ToJson(msg);
            }
            if (json.Length <= FxMaxChars) _session.SendRaw(json);
        }

        void OnOpponentFx(FxMsg m)
        {
            if (m == null || _bot != null || (!string.IsNullOrEmpty(m.from) && m.from != _oppId)) return;
            if (m.lo != null)
            {
                _oppWeapons.Clear();
                for (var i = 0; i < m.lo.Length && i < 8; i++)
                {
                    if (string.IsNullOrEmpty(m.lo[i])) continue;
                    var config = WeaponConfigOf(m.lo[i]);
                    var name = config != null && !string.IsNullOrEmpty(config.displayName) ? config.displayName : m.lo[i];
                    _oppWeapons.Add((m.lo[i], name, m.lv != null && i < m.lv.Length ? Mathf.Max(1, m.lv[i]) : 1));
                }
            }
            if (m.e == null || _ended || !_oppAlive || !_began) return;
            var vfx = _vfx?.Invoke();
            if (vfx is UnityEngine.Object unityObject && unityObject == null) vfx = null;
            foreach (var fx in m.e)
            {
                if (fx == null || string.IsNullOrEmpty(fx.k)) continue;
                try { PlayOpponentFx(vfx, fx); }
                catch (Exception e) { Debug.LogWarning("PvpMatchController: fx " + fx.k + ": " + e.Message); }
            }
        }

        void PlayOpponentFx(IWeaponEffects vfx, PvpFx fx)
        {
            var origin = _oppPosition + new Vector3(fx.x, 1.2f, fx.z);
            var dir = new Vector3(fx.dx, 0f, fx.dz);
            if (dir.sqrMagnitude < 0.0001f) dir = _oppRotation * Vector3.forward;
            dir.Normalize();
            var point = PvpRules.ToWorld(_centre, fx.x, fx.z);
            var radius = Mathf.Clamp(fx.r, 0.5f, 30f);
            switch (fx.k[0])
            {
                case 'f':
                {
                    vfx?.Fired(fx.w, origin, dir);
                    // the shot flies at my ship when it was aimed my way, else 18 m along its line
                    var toMe = _ship.Position - origin;
                    toMe.y = 0f;
                    var target = toMe.magnitude < 60f && Vector3.Dot(toMe.normalized, dir) > 0.8f ? _ship.Position : origin + dir * 18f;
                    AddShot(origin, target, WeaponColour(fx.w), 1.2f);
                    return;
                }
                case 's':
                    vfx?.SideSalvo(fx.w, _oppPosition, dir);
                    AddShot(_oppPosition + dir * 1.1f, _oppPosition + dir * 16f, WeaponColour(fx.w), 2f);
                    return;
                case 'b': vfx?.Beam(origin, dir, Mathf.Clamp(fx.r, 1f, 60f), 0.6f); return;
                case 'e': vfx?.Explosion(point, radius); return;
                case 'l': vfx?.LightningStrike(point, radius); return;
                case 'a': vfx?.AreaImpact(point, radius); return;
                case 'm': vfx?.AreaTelegraph(point, radius); return;
                // V5 M8z (7 Oct, D21): an aura pulses on the rival's ship; a pulse elsewhere (the Squid Ink Pots' slick) where it was.
                case 'p': vfx?.WeaponPulse(fx.w, WeaponConfigOf(fx.w)?.behavior == RoguePirates.V4.Config.ConfigEnums.WeaponBehavior.Aura ? _oppPosition : point, radius); return;
            }
        }

        void AddShot(Vector3 from, Vector3 to, Color color, float arc)
        {
            if (_shots.Count >= 48) return;
            var flight = Mathf.Clamp(Flat(to - from) / 45f, 0.12f, 0.8f);
            _shots.Add(new PvpShot { From = from, To = to, Start = Clock.TimeSingle, End = Clock.TimeSingle + flight, Color = color, Arc = arc });
        }

        WeaponConfig WeaponConfigOf(string id)
        {
            if (string.IsNullOrEmpty(id) || _config == null) return null;
            if (_weaponConfigs == null)
            {
                _weaponConfigs = new Dictionary<string, WeaponConfig>();
                foreach (var c in _config.All<WeaponConfig>()) if (c != null && !string.IsNullOrEmpty(c.weaponId)) _weaponConfigs[c.weaponId] = c;
            }
            // V5 M8z (8 Oct, close): an opponent on an older build may still send a removed id (its replacement's config).
            return _weaponConfigs.TryGetValue(RoguePirates.V4.Foundation.Core.ContentIdAliases.Resolve(id), out var config) ? config : null;
        }

        Color WeaponColour(string id)
        {
            var config = WeaponConfigOf(id);
            var c = config != null ? config.aimOverlayColor : new Color(1f, 0.55f, 0.2f);
            c.a = 1f;
            return c;
        }

        // ---- Receiving ----

        void OnResync(MatchStartMsg start)
        {
            if (start == null || start.matchId != _match.matchId) return;
            _match = start;
            AdoptLiveBarrels(start);
            if (start.live != null && start.live.ended)
            {
                _ended = true;
                _ship?.SetMovementEnabled(false);
            }
        }

        void AdoptLiveBarrels(MatchStartMsg start)
        {
            if (start.live?.barrels == null) return;
            foreach (var b in start.live.barrels) AddBarrel(b);
        }

        void AddBarrel(BarrelMsg b)
        {
            if (b == null) return;
            _barrels[b.id] = new PvpBarrel { Id = b.id, Xp = b.IsXp, World = PvpRules.ToWorld(_centre, b.x, b.z), ExpiresAt = b.expiresAt };
        }

        void OnMessage(string t, string json)
        {
            try { Handle(t, json); }
            catch (Exception e) { Debug.LogWarning("PvpMatchController: " + t + ": " + e.Message); }
        }

        void Handle(string t, string json)
        {
            switch (t)
            {
                case "state":
                {
                    var s = JsonUtility.FromJson<StateMsg>(json);
                    if (s == null || s.from != _oppId || _bot != null) return;
                    _interp.Add(new PvpSnapshot { Time = Clock.TimeSingle, Position = PvpRules.ToWorld(_centre, s.x, s.z), Velocity = new Vector3(s.vx, 0f, s.vz), Yaw = s.yaw });
                    _oppHp = s.hp;
                    _oppMaxHp = Mathf.Max(1f, s.maxHp);
                    _oppLevel = Mathf.Max(1, s.level);
                    _oppAlive = s.alive;
                    _oppKnown = true;
                    return;
                }
                case "hit":
                {
                    var h = JsonUtility.FromJson<HitMsg>(json);
                    if (h == null) return;
                    if (h.to == _myId) TakeHit(h.dmg);
                    else if (_bot != null && h.to == _match.bot.playerId) BotTakesHit(h.dmg);
                    return;
                }
                case "fx":
                    OnOpponentFx(JsonUtility.FromJson<FxMsg>(json));
                    return;
                case "barrel_spawn":
                    AddBarrel(JsonUtility.FromJson<BarrelMsg>(json));
                    return;
                case "barrel_gone":
                {
                    var b = JsonUtility.FromJson<BarrelMsg>(json);
                    if (b != null) _barrels.Remove(b.id);
                    return;
                }
                case "barrel_taken":
                {
                    var b = JsonUtility.FromJson<BarrelMsg>(json);
                    if (b == null) return;
                    _barrels.Remove(b.id);
                    if (b.by == _myId) TakeBarrel(b.IsXp);
                    else if (_bot != null && b.by == _match.bot.playerId)
                    {
                        if (b.IsXp) { _bot.LevelUp(); _arsenal?.LevelUp(); }
                        else _bot.Heal(HealFraction);
                        Notice(BotDisplayName + (b.IsXp ? " took an XP barrel" : " took a health barrel"));
                    }
                    else Notice(b.IsXp ? "Opponent took an XP barrel" : "Opponent took a health barrel");
                    return;
                }
                case "presence":
                {
                    var p = JsonUtility.FromJson<PresenceMsg>(json);
                    if (p == null || p.playerId != _oppId) return;
                    _oppConnected = p.connected;
                    _oppForfeitAt = p.connected ? 0 : p.forfeitAt;
                    Notice(p.connected ? "Opponent reconnected" : "Opponent disconnected");
                    return;
                }
                case "match_end":
                    _ended = true;
                    _ship.SetMovementEnabled(false);
                    _rematchStatus = string.Empty;
                    return;
                case "rematch":
                {
                    var m = JsonUtility.FromJson<RematchMsg>(json);
                    if (m == null) return;
                    if (m.status == "requested") _rematchStatus = m.by == _myId ? "Waiting for opponent..." : "Opponent wants a rematch!";
                    else if (m.status == "created") _rematchStatus = "Rematch ready: back to the lobby";
                    else if (m.status == "failed") _rematchStatus = RematchFailed; // V5 M8z (6 Oct, owner)
                    return;
                }
                case "reject":
                {
                    var r = JsonUtility.FromJson<RejectMsg>(json);
                    if (r == null) return;
                    if (r.of == "hit")
                    {
                        OnHitRefused(r);
                        return;
                    }
                    if (r.of != "pickup" && r.of != "state") Debug.Log($"PvP: server refused {r.of}: {r.reason}");
                    if (r.of == "rematch") _rematchStatus = r.reason == "rematch_failed" ? RematchFailed : "Rematch not possible (" + r.reason + ")";
                    return;
                }
            }
        }

        const string RematchFailed = "Rematch failed on the server: press Rematch to try again";

        /// <summary>V5 M8z (6 Oct, owner): the server refused all or part of a hit batch (not in the draft, out of range, over
        /// the level's damage cap...): "damage dealt" counts only what it accepted. Logged once per reason, not per batch.</summary>
        void OnHitRefused(RejectMsg r)
        {
            if (!r.asBot && r.dropped > 0f) _dealt = Mathf.Max(0f, _dealt - r.dropped);
            if (r.asBot && r.reason == "not_in_draft") _botTagFallback = true; // a server that only knows the bot's cannons
            var reason = string.IsNullOrEmpty(r.reason) ? "unknown" : r.reason;
            _hitRefusals.TryGetValue(reason, out var n);
            _hitRefusals[reason] = n + 1;
            if (n == 0) Debug.Log($"PvP: the server refused hit damage ({reason}); further refusals for this reason are counted, not logged");
        }

        readonly Dictionary<string, int> _hitRefusals = new Dictionary<string, int>();

        float HealFraction => _match.barrels != null && _match.barrels.healthHealFraction > 0f ? _match.barrels.healthHealFraction : 0.2f;

        void TakeHit(float raw)
        {
            if (_sunk || _ended || !_ship.IsAlive) return;
            ApplyToMe(_hits.Hit(Clock.TimeSingle, raw, _build.IncomingDamageMultiplier(), _ship.MaxHp));
        }

        void ApplyToMe(float amount)
        {
            if (amount <= 0f || _sunk || _ended || !_ship.IsAlive) return;
            _taken += amount;
            _damage.ExactHit(amount, _oppPosition);
        }

        void BotTakesHit(float raw)
        {
            if (!_bot.Alive) return;
            ApplyToBot(_bot.Hits.Hit(Clock.TimeSingle, raw, 1f, _bot.MaxHp));
        }

        void ApplyToBot(float amount)
        {
            if (amount <= 0f || !_bot.Alive) return;
            _bot.Hp -= amount;
            CheckBotSunk();
        }

        /// <summary>Hits pooled during an invulnerability window land when it ends.</summary>
        void TickPooledHits()
        {
            if (_ended) return;
            ApplyToMe(_hits.Tick(Clock.TimeSingle, _ship.MaxHp));
            if (_bot != null) ApplyToBot(_bot.Hits.Tick(Clock.TimeSingle, _bot.MaxHp));
        }

        void TakeBarrel(bool xp)
        {
            _barrelsTaken++;
            if (xp)
            {
                _build.Progression.GrantLevelUp();
                return; // the level-up's own line says what rose
            }
            var heal = _ship.MaxHp * HealFraction;
            _ship.Heal(Mathf.Min(heal, _ship.MaxHp - _ship.Hp));
            Notice($"Health barrel: +{Mathf.RoundToInt(heal)} hull");
        }

        void Notice(string text)
        {
            _notice = text;
            _noticeSerial++;
            _toasts?.Show(text, 2f);
        }

        // ---- IPvpMatchView ----

        public bool Ready => _ready;
        public bool IsBotMatch => _match != null && _match.HasBot;
        public string MyName => _match?.Player(_mySeat)?.Display ?? "You";
        public string OpponentName => _match?.Player(_oppSeat)?.Display ?? "Opponent";
        public int MyRating => _match?.Player(_mySeat)?.rating ?? 0;
        public int OpponentRating => _match?.Player(_oppSeat)?.rating ?? 0;
        public long ServerNowMs => _session != null ? _session.ServerNowMs : PvpSession.UtcNowMs;
        public long StartsAt => _match != null ? _match.startsAt : 0;
        public long EndsAt => _match != null ? _match.endsAt : 0;
        public bool Started => _started;
        public Vector3 ArenaCentre => _centre;
        public float StormRadius => PvpRules.StormRadius(_match?.storm, ServerNowMs);
        public bool StormShrinking => _match?.storm != null && ServerNowMs >= _match.storm.shrinkStartMs;
        public long StormStartsAt => _match?.storm != null ? _match.storm.shrinkStartMs : 0;
        public bool OutsideStorm => _outsideSeconds > 0f;
        /// <summary>Level 1 at the start, +1 per level-up.</summary>
        public int MyLevel => _build != null ? 1 + _build.Progression.Level - _startLevel : 1;
        int _startLevel;
        public float MyHp => _ship != null ? _ship.Hp : 0f;
        public float MyMaxHp => _ship != null ? _ship.MaxHp : 1f;
        public bool MySunk => _sunk;
        public Vector3 OpponentPosition => _oppPosition;
        public Quaternion OpponentRotation => _oppRotation;
        public bool OpponentKnown => _oppKnown || _bot != null;
        public float OpponentHp => _oppHp;
        public float OpponentMaxHp => _oppMaxHp;
        public int OpponentLevel => _oppLevel;
        public bool OpponentAlive => _oppAlive;
        public bool OpponentConnected => _oppConnected;
        public long OpponentForfeitAt => _oppForfeitAt;
        public float DamageDealt => _dealt;
        public float DamageTaken => _taken;
        public int BarrelsTaken => _barrelsTaken;
        public IReadOnlyCollection<PvpBarrel> Barrels => _barrels.Values;
        public IReadOnlyList<PvpShot> Shots => _shots;
        public IReadOnlyList<(string id, string name, int level)> OpponentWeapons
        {
            get
            {
                if (_bot == null) return _oppWeapons;
                _oppWeapons.Clear();
                if (_arsenal != null)
                    foreach (var w in _arsenal.Loadout)
                        _oppWeapons.Add((w.Id, !string.IsNullOrEmpty(w.Config.displayName) ? w.Config.displayName : w.Id, w.Level));
                return _oppWeapons;
            }
        }
        public bool Ended => _ended;
        public MatchEndMsg End => _session?.LastMatchEnd;
        public string EndJson => _session?.LastMatchEndJson;
        public string MyPlayerId => _myId;
        public string OpponentPlayerId => _oppId;
        public string RematchStatus => _rematchStatus;
        public string LastNotice => _notice;
        public int NoticeSerial => _noticeSerial;
        public Transform ShipModel => _ship?.VisualModel;
        public bool ModelFacesNegativeZ => _ship?.Ship != null && _ship.Ship.ModelForwardIsNegativeZ;

        public void AttachOpponentView(Transform view)
        {
            _oppView = view;
            if (_oppSlot >= 0) _sim.SetRemoteView(_oppSlot, view);
        }

        public void Rematch()
        {
            var error = _session.RequestRematch();
            _rematchStatus = error ?? (IsBotMatch ? "Asking the bot..." : "Waiting for opponent...");
        }

        /// <summary>Milliseconds left to ask for a rematch (0 when closed).</summary>
        public long RematchMsLeft => End != null ? System.Math.Max(0, End.rematchUntil - ServerNowMs) : 0;

        /// <summary>Back to the menu; before the end this leaves the match (a forfeit).</summary>
        public void MainMenu()
        {
            if (!_ended) _ = _session.Leave();
            _session.ReturnToMenu(_ended);
        }
    }
}
