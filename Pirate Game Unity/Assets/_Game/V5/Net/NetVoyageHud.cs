using System.Collections.Generic;
using RoguePirates.V4.Progression;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RoguePirates.V5.Net
{
    /// <summary>
    /// V5 M9b part 2b: the server voyage's HUD (the test client's own, on its own canvas): hull, level, kills, gold and
    /// run time from the server's snapshots, the boost, the connection; the end screen with its way back to the menu; plain
    /// card buttons when the stage has no card presenter. It reads the net client only. The run's own HUD shows the
    /// client's stage, whose run never ticks, so its counters are hidden (<see cref="HideRunHud"/>).
    /// </summary>
    public sealed class NetVoyageHud : MonoBehaviour
    {
        NetVoyageClient _voyage;
        Canvas _canvas;
        Text _title, _stats, _detail, _status, _endTitle, _endDetail;
        Image _hullFill;
        GameObject _end, _cards;
        readonly List<Button> _cardButtons = new List<Button>();
        float _nextText;
        int _frames;
        float _fpsSince;
        float _fps;

        static Font _font;
        static Font UiFont => _font != null ? _font : _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public void Bind(NetVoyageClient voyage)
        {
            _voyage = voyage;
            Build();
        }

        void Build()
        {
            var go = new GameObject("NetVoyageHud");
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 900;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            var panel = Box(go.transform, "Stats", new Vector2(0, 1), new Vector2(24, -24), new Vector2(760, 150), new Color(0f, 0f, 0f, 0.55f));
            _title = Label(panel, "Title", new Vector2(16, -10), new Vector2(728, 26), 20, FontStyle.Bold, new Color(1f, 0.85f, 0.45f));
            _title.text = "SERVER VOYAGE (TEST)";
            var barBack = Box(panel, "HullBar", new Vector2(0, 1), new Vector2(16, -42), new Vector2(728, 18), new Color(0.25f, 0.05f, 0.05f, 0.9f));
            _hullFill = Box(barBack, "Fill", new Vector2(0, 1), Vector2.zero, new Vector2(728, 18), new Color(0.2f, 0.85f, 0.35f, 1f)).GetComponent<Image>();
            _hullFill.rectTransform.pivot = new Vector2(0, 1);
            _stats = Label(panel, "Stats", new Vector2(16, -64), new Vector2(728, 34), 28, FontStyle.Bold, Color.white);
            _detail = Label(panel, "Detail", new Vector2(16, -104), new Vector2(728, 36), 19, FontStyle.Normal, new Color(0.85f, 0.9f, 1f));

            _status = Label(go.transform, "Status", Vector2.zero, new Vector2(1200, 48), 30, FontStyle.Bold, Color.white);
            var rt = _status.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.82f);
            rt.anchoredPosition = Vector2.zero;
            _status.alignment = TextAnchor.MiddleCenter;
            var shadow = _status.gameObject.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(2, -2);

            _end = Box(go.transform, "End", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 380), new Color(0.04f, 0.07f, 0.12f, 0.92f)).gameObject;
            var endRt = (RectTransform)_end.transform;
            endRt.pivot = new Vector2(0.5f, 0.5f);
            endRt.anchoredPosition = Vector2.zero;
            _endTitle = Label(_end.transform, "Title", new Vector2(30, -30), new Vector2(840, 60), 46, FontStyle.Bold, new Color(1f, 0.85f, 0.45f));
            _endTitle.alignment = TextAnchor.MiddleCenter;
            _endDetail = Label(_end.transform, "Detail", new Vector2(30, -105), new Vector2(840, 150), 26, FontStyle.Normal, Color.white);
            _endDetail.alignment = TextAnchor.UpperCenter;
            var back = MakeButton(_end.transform, "BackToMenu", "Back to menu", new Vector2(300, -280), new Vector2(300, 70));
            back.onClick.AddListener(() => _voyage.BackToMenu());
            _end.SetActive(false);

            _cards = Box(go.transform, "Cards", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 120), new Color(0f, 0f, 0f, 0.7f)).gameObject;
            ((RectTransform)_cards.transform).pivot = new Vector2(0.5f, 0.5f);
            ((RectTransform)_cards.transform).anchoredPosition = new Vector2(0, -200);
            _cards.SetActive(false);
            EnsureEventSystem();
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("NetVoyageEventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            DontDestroyOnLoad(es);
        }

        static RectTransform Box(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return rt;
        }

        static Text Label(Transform parent, string name, Vector2 position, Vector2 size, int fontSize, FontStyle style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.font = UiFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        static Button MakeButton(Transform parent, string name, string caption, Vector2 position, Vector2 size)
        {
            var rt = Box(parent, name, new Vector2(0, 1), position, size, new Color(0.85f, 0.62f, 0.2f, 1f));
            var button = rt.gameObject.AddComponent<Button>();
            var label = Label(rt, "Label", Vector2.zero, size, 26, FontStyle.Bold, new Color(0.1f, 0.06f, 0.02f));
            label.alignment = TextAnchor.MiddleCenter;
            label.text = caption;
            return button;
        }

        /// <summary>The run's own HUD parts that show the client's stage's numbers (its run never ticks: zeros, a local
        /// clock, a loadout the server does not use) or controls the server does not take (the Captain's ability). The
        /// run's canvases stay: the level-up cards and the boost meter (the predicted boost) live there. Applied again
        /// every second, in case a presenter turns them back on.</summary>
        static readonly string[] RunHudParts = { "HUD_SaltWind/SafeArea", "V4CaptainActive", "V4CaptainAbilityButton", "V4CaptainActiveEffect", "V4ItemReward", "V5TouchControls" };
        bool _hideRunHud;
        float _nextHide;

        public void HideRunHud()
        {
            _hideRunHud = true;
            _nextHide = 0f;
        }

        void ApplyRunHudHiding()
        {
            foreach (var path in RunHudParts)
            {
                var slash = path.IndexOf('/');
                var go = GameObject.Find(slash < 0 ? path : path.Substring(0, slash));
                if (go == null) continue;
                var target = slash < 0 ? go.transform : go.transform.Find(path.Substring(slash + 1));
                if (target != null && target.gameObject.activeSelf) target.gameObject.SetActive(false);
            }
        }

        public void ShowCards(OfferMessage m)
        {
            foreach (var b in _cardButtons) Destroy(b.gameObject);
            _cardButtons.Clear();
            var n = m.offers.Count;
            var width = 1000f / Mathf.Max(1, n);
            for (var i = 0; i < n; i++)
            {
                var index = i;
                var o = m.offers[i];
                var b = MakeButton(_cards.transform, "Card" + i, CardCaption(o), new Vector2(10 + i * width, -10), new Vector2(width - 20, 100));
                b.onClick.AddListener(() => { _voyage.Client.SendPick(NetProtocol.PickAction.Pick, index); HideCards(); });
                _cardButtons.Add(b);
            }
            _cards.SetActive(true);
        }

        static string CardCaption(Offer o)
        {
            var name = string.IsNullOrEmpty(o.Id) ? o.Kind.ToString() : o.Id.Replace('_', ' ');
            return o.IsNew ? "New: " + name : o.NextLevel > 0 ? $"{name} {o.CurrentLevel} to {o.NextLevel}" : name;
        }

        public void HideCards()
        {
            if (_cards != null) _cards.SetActive(false);
        }

        public void ShowEnd(string title, string detail)
        {
            HideCards();
            _endTitle.text = title;
            _endDetail.text = detail;
            _end.SetActive(true);
            EnsureEventSystem();
        }

        void Update()
        {
            _frames++;
            var now = Time.realtimeSinceStartup;
            if (_hideRunHud && now >= _nextHide)
            {
                _nextHide = now + 1f;
                ApplyRunHudHiding();
            }
            if (now - _fpsSince >= 1f)
            {
                _fps = _frames / (now - _fpsSince);
                _frames = 0;
                _fpsSince = now;
            }
            if (now < _nextText || _voyage == null) return;
            _nextText = now + 0.1f;
            var client = _voyage.Client;
            switch (_voyage.Current)
            {
                case NetVoyageClient.State.Ticket: _status.text = "Asking for a voyage ticket..."; break;
                case NetVoyageClient.State.Joining: _status.text = "Joining the match server..."; break;
                case NetVoyageClient.State.LoadingStage: _status.text = "Building the stage..."; break;
                case NetVoyageClient.State.Sailing:
                    _status.text = client != null && !client.Connected ? "Connection lost, reconnecting..." :
                        client != null && client.Last.Paused ? "Choose your upgrade" : "";
                    break;
                default: _status.text = ""; break;
            }
            if (client == null) { _stats.text = ""; _detail.text = _voyage.Status; return; }
            var s = client.Last;
            _hullFill.rectTransform.sizeDelta = new Vector2(728f * (s.maxHull > 0f ? Mathf.Clamp01(s.hull / s.maxHull) : 0f), 18f);
            _stats.text = $"Hull {s.hull:0}/{s.maxHull:0}    Level {s.level}    Kills {s.kills}    Gold {s.gold}    {NetVoyageClient.Clock(s.runTime)}";
            var boost = s.Boosting ? "Boosting" : s.boost.cooldownTimer > 0f ? $"Boost in {Mathf.CeilToInt(s.boost.cooldownTimer)} s" : "Boost ready (Shift)";
            var lag = NetMetrics.Percentile(client.Metrics.InputLagMs, 50f);
            _detail.text = $"{boost}    Input delay {lag:0} ms    {_fps:0} fps    {client.Enemies.Count} enemies near";
        }
    }
}
