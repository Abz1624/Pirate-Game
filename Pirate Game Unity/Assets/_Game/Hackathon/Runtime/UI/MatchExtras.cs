using System;
using RoguePirates.V4;
using RoguePirates.V4.Online;
using RoguePirates.V4.Presentation.Online;
using RoguePirates.V4.Pvp;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.Hackathon.UI
{
    /// <summary>
    /// Hackathon (12 Oct): the 1v1 run's extras, added when the run starts (V4's <see cref="V4Game.RunStarted"/>): Captain
    /// Marrow's barks in a match against the AI captain (read from the match view's hull and state; the bot's play and the
    /// PvP rules are unchanged), and the wager strip (stake and pot during the match; the result and the payout link with
    /// a devnet explorer button after it).
    /// </summary>
    public sealed class MatchExtras : MonoBehaviour
    {
        static readonly string[] Idle =
        {
            "Ye sail like a drunk gull!",
            "My cannons are hungry, whelp.",
            "The reef is full of captains like ye.",
            "Strike yer colours and I may let ye swim.",
        };

        IPvpMatchView _match;
        bool _vsCaptain;
        RectTransform _barkBox, _wagerBox;
        TextMeshProUGUI _bark, _wagerText;
        Button _explorer;
        string _explorerUrl;
        float _barkUntil, _nextIdle, _nextPoll;
        bool _saidStart, _saidHalf, _saidLow, _saidMyLow, _saidEnd;
        int _idle;
        float _pollUntil;

        public string LastBark { get; private set; }
        public string WagerText => _wagerText != null ? _wagerText.text : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            V4Game.RunStarted -= OnRun;
            V4Game.RunStarted += OnRun;
            // U-3: the match notices name the AI captain ("Captain Marrow took an XP barrel").
            PvpMatchController.BotDisplayName = HackathonRules.CaptainName;
        }

        static void OnRun(V4Game game)
        {
            if (game == null || !game.IsPvp || game.PvpMatch == null) return;
            if (game.GetComponent<MatchExtras>() != null) return;
            var extras = game.gameObject.AddComponent<MatchExtras>();
            extras._match = game.PvpMatch;
            extras.Build();
        }

        void Build()
        {
            var go = new GameObject("HackathonMatchCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 520;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Hackathon UI (13 Oct): the parchment kit. The bark is a maroon banner, the wager a parchment card.
            _barkBox = Box(go.transform, "Bark", new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(940f, 96f), "Banner_Maroon", 1.6f, new Color(0.35f, 0.07f, 0.06f, 0.92f));
            PvpKit.RowIcon(_barkBox, "Icon_Skull_Crossbones", 26f, 56f, PvpKit.OnWood);
            _bark = PvpKit.Label(PvpKit.Fill(_barkBox, "Text", 96f, 6f, 24f, 6f), string.Empty, 28f, PvpKit.OnWood, TextAlignmentOptions.MidlineLeft, false, 16f);
            _bark.textWrappingMode = TextWrappingModes.Normal;
            _barkBox.gameObject.SetActive(false);

            _wagerBox = Box(go.transform, "Wager", new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(1100f, 84f), "Panel_Card", 2.2f, new Color(0.96f, 0.89f, 0.74f, 0.97f));
            PvpKit.RowIcon(_wagerBox, "Chest_2", 18f, 60f);
            _wagerText = PvpKit.Label(PvpKit.Fill(_wagerBox, "Text", 92f, 4f, 280f, 4f), string.Empty, 25f, PvpKit.Ink, TextAlignmentOptions.MidlineLeft, false, 15f);
            _wagerText.textWrappingMode = TextWrappingModes.Normal;
            _explorer = PvpKit.KitButton(PvpKit.TopRight(_wagerBox, "ViewPayout", 16f, 12f, 250f, 60f), "View payout", "Icon_Link",
                () => { if (_explorerUrl != null) Application.OpenURL(_explorerUrl); }, "Btn_Gold", 25f, 28f);
            _explorer.gameObject.SetActive(false);
            _wagerBox.gameObject.SetActive(false);
            _nextIdle = Time.unscaledTime + 28f;
        }

        static RectTransform Box(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, string sprite, float thin, Color fallback)
        {
            var rt = PvpKit.Node(parent, name);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, anchor.y);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            PvpKit.Paint(rt, sprite, thin, null, fallback);
            return rt;
        }

        void Update()
        {
            if (_match == null || !_match.Ready) return;
            _vsCaptain = _match.IsBotMatch;
            if (_vsCaptain) Barks();
            if (_barkBox.gameObject.activeSelf && Time.unscaledTime > _barkUntil) _barkBox.gameObject.SetActive(false);
            WagerStrip();
        }

        void Barks()
        {
            if (!_saidStart && _match.StartsAt > 0 && _match.ServerNowMs >= _match.StartsAt - 2500)
            {
                _saidStart = true;
                Say("Wrong waters, whelp! Run out the guns!");
                return;
            }
            if (!_match.Started) return;
            if (_match.Ended)
            {
                if (_saidEnd) return;
                _saidEnd = true;
                var end = _match.End;
                var captainWon = end != null && !string.IsNullOrEmpty(end.winnerPlayerId) && end.winnerPlayerId != _match.MyPlayerId;
                var draw = end != null && string.IsNullOrEmpty(end.winnerPlayerId);
                Say(draw ? "Bah, the sea takes us both. Another tide, then." :
                    captainWon ? "Another hull for the reef! Come back when ye can sail." : "Curse ye... this isn't over. Marrow never forgets.", 6f);
                return;
            }
            var oppFrac = _match.OpponentMaxHp > 0f ? _match.OpponentHp / _match.OpponentMaxHp : 1f;
            var myFrac = _match.MyMaxHp > 0f ? _match.MyHp / _match.MyMaxHp : 1f;
            if (!_saidHalf && oppFrac < 0.5f) { _saidHalf = true; Say("A scratch! My hull has seen worse storms than ye."); return; }
            if (!_saidLow && oppFrac < 0.2f) { _saidLow = true; Say("Belay that! No quarter, no mercy!"); return; }
            if (!_saidMyLow && myFrac < 0.35f) { _saidMyLow = true; Say("Hah! I can smell yer bilge from here!"); return; }
            if (Time.unscaledTime >= _nextIdle)
            {
                _nextIdle = Time.unscaledTime + 30f;
                Say(Idle[_idle++ % Idle.Length]);
            }
        }

        void Say(string line, float seconds = 4f)
        {
            LastBark = line;
            _bark.text = $"<color=#ffd166><b>{HackathonRules.CaptainName}:</b></color> <i>\"{line}\"</i>";
            _barkBox.gameObject.SetActive(true);
            _barkUntil = Time.unscaledTime + seconds;
        }

        void WagerStrip()
        {
            var session = PvpSession.Exists ? PvpSession.Instance : null;
            var lobbyId = session?.Lobby?.lobbyId ?? HackathonPvp.WagerLobbyId;
            var w = HackathonPvp.For(lobbyId);
            if (w == null) { _wagerBox.gameObject.SetActive(false); return; }
            _wagerBox.gameObject.SetActive(true);
            if (!_match.Ended)
            {
                _wagerText.text = $"<color=#8c4d05><b>Wager {HackathonRules.Sol(w.stakeLamports)}</b></color>   Pot {HackathonRules.Sol(w.potLamports)}   " +
                                  (w.vsHouse ? $"<color=#7a5a3b>beat {HackathonRules.CaptainName} to win it</color>" : "<color=#7a5a3b>winner takes all</color>");
                _explorer.gameObject.SetActive(false);
                return;
            }
            // After the end: read the wager until it is settled and its payout is sent (about a minute at most).
            if (_pollUntil <= 0f) _pollUntil = Time.unscaledTime + 120f;
            var settled = w.Closed && !HackathonRules.PayoutPending(w);
            if (!settled && Time.unscaledTime >= _nextPoll && Time.unscaledTime < _pollUntil)
            {
                _nextPoll = Time.unscaledTime + 3f;
                _ = HackathonPvp.Refresh(lobbyId);
            }
            var result = HackathonRules.ResultLine(w, _match.MyPlayerId);
            _explorerUrl = HackathonRules.ExplorerTx(HackathonRules.PayoutSignature(w));
            _explorer.gameObject.SetActive(_explorerUrl != null);
            _wagerText.text = result == null
                ? "<color=#8c4d05><b>Wager</b></color>  The server is settling the wager..."
                : $"<color=#8c4d05><b>Wager</b></color>  {result}" + (HackathonRules.PayoutPending(w) ? "  <color=#7a5a3b>(payout on its way)</color>" : string.Empty);
        }
    }
}
