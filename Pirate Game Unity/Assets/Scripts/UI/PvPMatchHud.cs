using System.Collections.Generic;
using UnityEngine;
using PirateGame.Net;
using PirateGame.Net.Generated;

namespace PirateGame.UI
{
    /// <summary>PvPvE HUD: timer, bank scoreboard, kill feed, respawn countdown.</summary>
    public class PvPMatchHud : MonoBehaviour
    {
        [SerializeField] ErClient erClient;
        [SerializeField] LobbyManager lobbyManager;

        readonly List<string> killFeed = new();
        GUIStyle titleStyle;
        GUIStyle rowStyle;
        GUIStyle feedStyle;
        bool stylesReady;

        void Awake()
        {
            if (erClient == null) erClient = FindFirstObjectByType<ErClient>();
            if (lobbyManager == null) lobbyManager = FindFirstObjectByType<LobbyManager>();
        }

        void OnEnable()
        {
            if (erClient != null)
                erClient.OnSnapshotReceived += OnSnapshot;
        }

        void OnDisable()
        {
            if (erClient != null)
                erClient.OnSnapshotReceived -= OnSnapshot;
        }

        void OnSnapshot(MatchSnapshot snap)
        {
            if (snap?.events == null) return;
            foreach (var ev in snap.events)
            {
                if (ev.kind == GameEventKind.PlayerKill)
                    killFeed.Add($"P{ev.actor} sank P{ev.target}");
                else if (ev.kind == GameEventKind.Death)
                    killFeed.Add($"Enemy #{ev.target} destroyed");
                else if (ev.kind == GameEventKind.LevelUp)
                    killFeed.Add($"P{ev.actor} reached level {ev.value}");
            }
            while (killFeed.Count > 6)
                killFeed.RemoveAt(0);
        }

        void OnGUI()
        {
            if (erClient == null || !erClient.MatchLive) return;
            var snap = erClient.LatestSnapshot;
            if (snap == null) return;

            EnsureStyles();
            var w = Screen.width;

            var mins = snap.matchTimerSec / 60;
            var secs = snap.matchTimerSec % 60;
            GUI.Label(new Rect(16, 12, 300, 28), $"Bounty Waters — {mins:00}:{secs:00}", titleStyle);
            GUI.Label(new Rect(16, 36, 300, 22), $"Storm {snap.stormRadiusPct}%  |  Tick {snap.tick}", rowStyle);

            var y = 72f;
            GUI.Label(new Rect(16, y, 200, 22), "Scoreboard (bank)", titleStyle);
            y += 26f;
            foreach (var p in snap.players)
            {
                if (!SnapshotDecoder.IsPlayerSlotPopulated(p)) continue;
                var local = lobbyManager != null && p.slot == lobbyManager.LocalPlayerSlot ? " (you)" : "";
                var status = p.downed ? " [respawning]" : "";
                GUI.Label(new Rect(16, y, w - 32, 20),
                    $"P{p.slot}{local}  Lv{p.level}  Bank {p.bank}  Kills {p.kills}{status}", rowStyle);
                y += 20f;
            }

            y = Screen.height - 140f;
            GUI.Label(new Rect(16, y, 200, 22), "Kill feed", titleStyle);
            y += 22f;
            for (var i = 0; i < killFeed.Count; i++)
                GUI.Label(new Rect(16, y + i * 18, w - 32, 18), killFeed[i], feedStyle);
        }

        void EnsureStyles()
        {
            if (stylesReady) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            rowStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            feedStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            stylesReady = true;
        }
    }
}
