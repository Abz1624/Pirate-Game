using System.Collections.Generic;
using MoreMountains.Feedbacks;
using RoguePirates.V4;
using RoguePirates.V4.Events;
using RoguePirates.V4.Presentation;
using UnityEngine;

namespace RoguePirates.FeelIntegration
{
    /// <summary>
    /// Makes hitting and sinking enemies feel crunchy, on top of the game's own sparks, flashes and kill explosions:
    ///  - every hit squashes the enemy's model with a quick spring (a bigger squash on crits), and crits add a tiny
    ///    camera tick;
    ///  - every kill gives a small camera kick, elites a firmer one with a little roll, bosses a heavy rumble.
    /// Listens to the run's existing combat event stream (CombatEvents.TickPublished: Hit / Killed) and finds the hit
    /// enemy's model with the game's own EnemyViewPresenter.NearestView; no game code changed. Visual only. Throttled
    /// so big waves never turn into constant shaking or a pile of lookups (mobile friendly).
    /// </summary>
    [AddComponentMenu("Rogue Pirates/Feel/Enemy Hit Feel")]
    public sealed class EnemyHitFeel : MonoBehaviour
    {
        [Header("Enemy squash on hit")]
        [Tooltip("Squash on a normal hit (0.08 = 8% flatter and wider at the peak).")]
        [Range(0f, 0.25f)] public float HitSquash = 0.08f;
        [Tooltip("Squash on a critical hit.")]
        [Range(0f, 0.25f)] public float CritSquash = 0.15f;
        [Tooltip("How long the squash takes to settle (seconds).")]
        public float SquashDuration = 0.24f;
        [Tooltip("Wobbles per second (higher = snappier).")]
        public float SquashFrequency = 4.5f;
        [Tooltip("How far from the hit point to look for the enemy's model (m).")]
        public float FindRadius = 3f;
        [Tooltip("Most enemies squashed per frame (big volleys).")]
        public int MaxSquashesPerFrame = 8;

        [Header("Camera (Feel players)")]
        [Tooltip("Played on a critical hit.")]
        public MMF_Player CritFeedback;
        [Tooltip("Played when a normal enemy sinks.")]
        public MMF_Player KillFeedback;
        [Tooltip("Played when an elite sinks.")]
        public MMF_Player EliteKillFeedback;
        [Tooltip("Played when a boss sinks.")]
        public MMF_Player BossKillFeedback;
        [Tooltip("Shortest time between two crit or normal-kill camera ticks (seconds). Elites and bosses always play.")]
        public float SmallKickInterval = 0.12f;

        V4Game _game;
        CombatEvents _events;
        V4PresentationRoot _presentation;
        EnemyPunchSystem _punch;
        float _nextSmallKick, _nextSearch;
        int _frame, _squashesThisFrame;

        void Awake()
        {
            _punch = GetComponent<EnemyPunchSystem>();
            if (_punch == null) _punch = gameObject.AddComponent<EnemyPunchSystem>();
            V4Game.RunStarted += OnRunStarted;
        }

        void OnDestroy()
        {
            V4Game.RunStarted -= OnRunStarted;
            Unbind();
        }

        void OnDisable() => Unbind();

        void OnRunStarted(V4Game game) => _game = game;

        void LateUpdate()
        {
            if (_game == null && Time.unscaledTime >= _nextSearch)
            {
                _nextSearch = Time.unscaledTime + 0.5f;
                _game = FindFirstObjectByType<V4Game>();
            }
            var events = _game != null ? _game.Events : null;
            if (events == _events) return;
            Unbind();
            _events = events;
            if (_events != null) _events.TickPublished += OnEvents;
        }

        void Unbind()
        {
            if (_events != null) _events.TickPublished -= OnEvents;
            _events = null;
        }

        void OnEvents(IReadOnlyList<CombatEvent> batch)
        {
            if (batch == null) return;
            if (_frame != Time.frameCount) { _frame = Time.frameCount; _squashesThisFrame = 0; }
            var now = Time.unscaledTime;
            for (var i = 0; i < batch.Count; i++)
            {
                var e = batch[i];
                if (e.Kind == CombatEventKind.Hit)
                {
                    var position = new Vector3(e.X, 0f, e.Y);
                    var crit = e.A == 1;
                    if (_squashesThisFrame < MaxSquashesPerFrame)
                    {
                        var view = NearestEnemyView(position);
                        if (view != null)
                        {
                            _punch.Punch(view, crit ? CritSquash : HitSquash, SquashDuration, SquashFrequency);
                            _squashesThisFrame++;
                        }
                    }
                    if (crit && CritFeedback != null && now >= _nextSmallKick)
                    {
                        _nextSmallKick = now + SmallKickInterval;
                        CritFeedback.PlayFeedbacks(position, 1f);
                    }
                }
                else if (e.Kind == CombatEventKind.Killed)
                {
                    var position = new Vector3(e.X, 0f, e.Y);
                    var boss = (e.B & 2) != 0;
                    var elite = (e.B & 1) != 0 && (e.B & 4) == 0; // the game's own elite test for kills
                    if (boss) { if (BossKillFeedback != null) BossKillFeedback.PlayFeedbacks(position, 1f); }
                    else if (elite) { if (EliteKillFeedback != null) EliteKillFeedback.PlayFeedbacks(position, 1f); }
                    else if (KillFeedback != null && now >= _nextSmallKick)
                    {
                        _nextSmallKick = now + SmallKickInterval;
                        KillFeedback.PlayFeedbacks(position, 1f);
                    }
                }
            }
        }

        Transform NearestEnemyView(Vector3 position)
        {
            if (_presentation == null) _presentation = FindFirstObjectByType<V4PresentationRoot>();
            var views = _presentation != null ? _presentation.EnemyViews : null;
            return views != null ? views.NearestView(position, FindRadius) : null;
        }
    }
}
