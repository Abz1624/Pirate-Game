using System.Collections.Generic;
using RoguePirates.UI;
using RoguePirates.V4.Foundation.UI;
using RoguePirates.V4.Foundation.VFX;
using RoguePirates.V4.Presentation.Effects;
using RoguePirates.V4.Presentation.Hud;
using RoguePirates.V4.Online;
using RoguePirates.V4.Presentation.Menu;
using RoguePirates.V4.Presentation.Online;
using RoguePirates.V4.Pvp;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Pvp
{
    /// <summary>
    /// Playtest (3 Oct, owner): the 1v1 match on screen (placeholder look): the opponent's ship (a copy of the player's ship
    /// model, tinted red, with a nameplate and hull bar), the storm ring, the barrels, the bot's cannonballs, the match HUD
    /// (both hulls and levels, the 5:00 clock, the storm line), the countdown, arrows at the screen edge towards the
    /// opponent and the barrels while off-screen, and the end screen (winner, reason, both players' stats, rating change,
    /// Rematch and Main Menu). Reads <see cref="IPvpMatchView"/> only.
    /// </summary>
    public sealed class PvpMatchPresenter : MonoBehaviour
    {
        static readonly Color Red = new Color(0.9f, 0.18f, 0.14f, 1f);
        static readonly Color XpColor = new Color(1f, 0.78f, 0.2f, 1f);
        static readonly Color HealthColor = new Color(0.3f, 0.9f, 0.4f, 1f);
        static readonly Color StormColor = new Color(0.55f, 0.15f, 0.85f, 0.85f);

        IPvpMatchView _view;
        RectTransform _canvas, _hud, _plate, _end, _arrowRoot;
        TextMeshProUGUI _clock, _storm, _myLine, _oppLine, _countdown, _plateName, _endTitle, _endReason, _endStats, _endRating, _endRematch;
        Image _myBar, _oppBar, _plateBar;
        Button _rematch;
        Transform _opponent, _world;
        LineRenderer _ring;
        Material _ringMaterial;
        readonly Dictionary<int, GameObject> _barrels = new Dictionary<int, GameObject>();
        readonly List<GameObject> _balls = new List<GameObject>();
        readonly List<RectTransform> _arrows = new List<RectTransform>();
        readonly List<GameObject> _hidden = new List<GameObject>();
        bool _endShown, _endHadResult;
        float _endSeenAt = -1f;
        // Playtest (3 Oct, owner, round 7: "in pvp i couldn't see other player's weapons, attacks, and health"): a world
        // health bar over the opponent, its hull in numbers, its weapons as chips, coloured shots with trails.
        EnemyViewPresenter _enemyViews;
        WorldHealthBar _oppWorldBar;
        RectTransform _chips;
        TextMeshProUGUI _plateHp;
        IconCatalog _icons;
        string _chipsKey;
        readonly List<Color> _ballColors = new List<Color>();

        public bool EndShown => _endShown;
        public string ClockText => _clock != null ? _clock.text : null;
        public string EndText => _endShown ? _endTitle.text + " | " + _endReason.text + " | " + _endStats.text.Replace("\n", " / ") + " | " + _endRating.text : null;

        public static PvpMatchPresenter Create(Canvas runCanvas, IPvpMatchView view, EnemyViewPresenter enemyViews = null)
        {
            var go = new GameObject("V4PvpMatch", typeof(RectTransform));
            go.transform.SetParent(runCanvas.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var p = go.AddComponent<PvpMatchPresenter>();
            p._view = view;
            p._enemyViews = enemyViews;
            p._canvas = rt;
            p.Build();
            return p;
        }

        void Build()
        {
            // 3D pieces (ring, barrels, the opponent, cannonballs) live outside the canvas (its scale is not the world's).
            _world = new GameObject("V4PvpWorld").transform;
            HideVoyageHud();
            BuildHud();
            BuildPlate();
            _arrowRoot = OnlineUi.Area(_canvas, "Arrows", Vector2.zero, Vector2.one);
            _countdown = OnlineUi.TextAt(_canvas, "Countdown", string.Empty, 140, TextAlignmentOptions.Center, Color.white, new Vector2(0.3f, 0.45f), new Vector2(0.7f, 0.75f), true);
            _countdown.outlineWidth = 0.2f;
            _countdown.outlineColor = new Color32(0, 0, 0, 255);
            BuildEnd();
            BuildRing();
        }

        /// <summary>The voyage's clock, gold, kills, Rum and quest banner mean nothing in a 1v1: hidden (the hull, level and
        /// weapon row stay).</summary>
        void HideVoyageHud()
        {
            foreach (var name in new[] { "MatchTitle", "TopRight", "MatchTimer", "TImer", "Currency", "Kill_Count", "Quest", "Objective", "ObjectiveBanner" })
                foreach (var t in FindAll(name))
                    if (t.gameObject.activeSelf) { t.gameObject.SetActive(false); _hidden.Add(t.gameObject); }
        }

        static List<Transform> FindAll(string name)
        {
            var found = new List<Transform>();
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                foreach (var t in canvas.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) found.Add(t);
            return found;
        }

        void BuildHud()
        {
            _hud = OnlineUi.Area(_canvas, "PvpHud", new Vector2(0.27f, 1f), new Vector2(0.73f, 1f));
            _hud.pivot = new Vector2(0.5f, 1f);
            _hud.offsetMin = new Vector2(0f, -170f);
            _hud.offsetMax = new Vector2(0f, -12f);
            var bg = _hud.gameObject.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.1f, 0.16f, 0.72f);
            // Hackathon UI (13 Oct, owner): a dark wood plank from the kit (light text stays readable over it).
            UiKit.Reskin(bg, "Header_Wood", 1.6f, new Color(1f, 1f, 1f, 0.94f));
            bg.raycastTarget = false;
            _clock = OnlineUi.TextAt(_hud, "Clock", "5:00", 54, TextAlignmentOptions.Center, Color.white, new Vector2(0.38f, 0.42f), new Vector2(0.62f, 1f), true);
            _myLine = OnlineUi.TextAt(_hud, "Me", string.Empty, 24, TextAlignmentOptions.MidlineLeft, OnlineUi.TextColor, new Vector2(0.02f, 0.62f), new Vector2(0.4f, 0.95f));
            _oppLine = OnlineUi.TextAt(_hud, "Opponent", string.Empty, 24, TextAlignmentOptions.MidlineRight, OnlineUi.TextColor, new Vector2(0.6f, 0.62f), new Vector2(0.98f, 0.95f));
            _myBar = Bar(_hud, new Vector2(0.02f, 0.44f), new Vector2(0.38f, 0.6f), OnlineUi.Good, false);
            _oppBar = Bar(_hud, new Vector2(0.62f, 0.44f), new Vector2(0.98f, 0.6f), Red, true);
            _storm = OnlineUi.TextAt(_hud, "Storm", string.Empty, 24, TextAlignmentOptions.Center, OnlineUi.Dim, new Vector2(0f, 0.04f), new Vector2(1f, 0.4f));
            if (UiKit.Font != null)
                foreach (var t in new[] { _clock, _myLine, _oppLine, _storm }) t.font = UiKit.Font;
            // The opponent's weapons: a row of chips under the HUD's right half.
            _chips = OnlineUi.Area(_hud, "OpponentWeapons", new Vector2(0.5f, 0f), new Vector2(1f, 0f));
            _chips.pivot = new Vector2(1f, 1f);
            _chips.offsetMin = new Vector2(0f, -44f);
            _chips.offsetMax = new Vector2(0f, -6f);
            var row = _chips.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.UpperRight;
            row.spacing = 6f;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            try { _icons = IconCatalog.Load(); } catch { _icons = null; }
        }

        void UpdateChips()
        {
            var weapons = _view.OpponentWeapons;
            var key = string.Empty;
            if (weapons != null) foreach (var w in weapons) key += w.id + ":" + w.level + ";";
            if (key == _chipsKey) return;
            _chipsKey = key;
            for (var i = _chips.childCount - 1; i >= 0; i--) Destroy(_chips.GetChild(i).gameObject);
            if (weapons == null) return;
            foreach (var w in weapons) Chip(w.id, w.name, w.level);
        }

        void Chip(string id, string name, int level)
        {
            var chip = (RectTransform)new GameObject("Chip", typeof(RectTransform)).transform;
            chip.SetParent(_chips, false);
            var bg = chip.gameObject.AddComponent<Image>();
            bg.color = new Color(0.45f, 0.08f, 0.06f, 0.85f);
            UiKit.Reskin(bg, "Banner_Maroon", 2.4f); // Hackathon UI: the kit's maroon banner
            bg.raycastTarget = false;
            var layout = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 3, 3);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var icon = _icons != null ? _icons.Weapon(id) : null;
            if (icon != null)
            {
                var img = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                img.transform.SetParent(chip, false);
                var image = img.GetComponent<Image>();
                image.sprite = icon;
                image.preserveAspect = true;
                image.raycastTarget = false;
                var le = img.GetComponent<LayoutElement>();
                le.preferredWidth = le.preferredHeight = 32f;
            }
            // Hackathon UI (13 Oct, Seeker): six named chips did not fit the half-width row ("Dec...", "Sw..."); with an
            // icon the chip shows the icon and level only.
            var text = OnlineUi.Text(chip, "Name", icon != null ? $"<color=#ffd166>{level}</color>" : $"{name} <color=#ffd166>{level}</color>", icon != null ? 24 : 20, TextAlignmentOptions.Center, Color.white);
            if (UiKit.Font != null) text.font = UiKit.Font;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
        }

        static Image Bar(RectTransform parent, Vector2 min, Vector2 max, Color color, bool fromRight)
        {
            var back = OnlineUi.Area(parent, "Bar", min, max);
            var b = back.gameObject.AddComponent<Image>();
            b.color = new Color(0f, 0f, 0f, 0.6f);
            b.raycastTarget = false;
            var fill = OnlineUi.Area(back, "Fill", Vector2.zero, Vector2.one, new Vector4(2f, 2f, 2f, 2f));
            var f = fill.gameObject.AddComponent<Image>();
            f.color = color;
            f.raycastTarget = false;
            f.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            f.type = Image.Type.Filled;
            f.fillMethod = Image.FillMethod.Horizontal;
            f.fillOrigin = fromRight ? (int)Image.OriginHorizontal.Right : (int)Image.OriginHorizontal.Left;
            // Hackathon UI: the kit's bar track and fill (green for you, a red-tinted gold for the opponent).
            if (UiKit.HudBar(b, f, fromRight ? "Bar_Fill_Gold" : "Bar_Fill_Green", null))
            {
                f.fillOrigin = fromRight ? (int)Image.OriginHorizontal.Right : (int)Image.OriginHorizontal.Left;
                f.color = fromRight ? new Color(1f, 0.42f, 0.36f, 1f) : Color.white;
            }
            return f;
        }

        void BuildPlate()
        {
            _plate = OnlineUi.Area(_canvas, "OpponentPlate", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _plate.sizeDelta = new Vector2(260f, 56f);
            _plateName = OnlineUi.TextAt(_plate, "Name", string.Empty, 22, TextAlignmentOptions.Center, Color.white, new Vector2(0f, 0.45f), Vector2.one);
            _plateName.outlineWidth = 0.25f;
            _plateName.outlineColor = new Color32(0, 0, 0, 255);
            _plateBar = Bar(_plate, new Vector2(0.1f, 0.05f), new Vector2(0.9f, 0.38f), Red, false);
            _plateHp = OnlineUi.TextAt(_plate, "Hp", string.Empty, 16, TextAlignmentOptions.Center, Color.white, new Vector2(0.1f, 0.05f), new Vector2(0.9f, 0.38f));
            _plateHp.outlineWidth = 0.25f;
            _plateHp.outlineColor = new Color32(0, 0, 0, 255);
        }

        void BuildEnd()
        {
            _end = OnlineUi.Panel(_canvas, "PvpEnd", new Vector2(0.24f, 0.12f), new Vector2(0.76f, 0.88f), 240);
            UiKit.Sheet(_end.GetComponent<Image>()); // Hackathon UI: the end card on parchment (its texts turn to ink)
            _endTitle = OnlineUi.TextAt(_end, "Title", string.Empty, 72, TextAlignmentOptions.Center, OnlineUi.Gold, new Vector2(0f, 0.84f), new Vector2(1f, 0.98f), true);
            _endReason = OnlineUi.TextAt(_end, "Reason", string.Empty, 30, TextAlignmentOptions.Center, OnlineUi.TextColor, new Vector2(0f, 0.76f), new Vector2(1f, 0.85f));
            _endStats = OnlineUi.TextAt(_end, "Stats", string.Empty, 28, TextAlignmentOptions.TopLeft, OnlineUi.TextColor, new Vector2(0.06f, 0.3f), new Vector2(0.94f, 0.75f));
            _endStats.textWrappingMode = TextWrappingModes.Normal;
            _endStats.overflowMode = TextOverflowModes.Overflow;
            _endRating = OnlineUi.TextAt(_end, "Rating", string.Empty, 30, TextAlignmentOptions.Center, OnlineUi.Gold, new Vector2(0f, 0.21f), new Vector2(1f, 0.3f));
            _endRematch = OnlineUi.TextAt(_end, "RematchStatus", string.Empty, 26, TextAlignmentOptions.Center, OnlineUi.Dim, new Vector2(0f, 0.15f), new Vector2(1f, 0.21f));
            _rematch = OnlineUi.Button(_end, "Rematch", () => _view.Rematch(), new Vector2(0.06f, 0.03f), new Vector2(0.48f, 0.13f));
            var menu = OnlineUi.Button(_end, "Main Menu", () => _view.MainMenu(), new Vector2(0.52f, 0.03f), new Vector2(0.94f, 0.13f));
            UiKit.ButtonIcon(_rematch, "Icon_Refresh", 30f);
            UiKit.StyleButton(menu, "Btn_Parchment");
            UiKit.ButtonIcon(menu, "Icon_House", 30f);
        }

        void BuildRing()
        {
            var go = new GameObject("PvpStormRing");
            go.transform.SetParent(_world, false);
            _ring = go.AddComponent<LineRenderer>();
            _ring.useWorldSpace = true;
            _ring.loop = true;
            _ring.positionCount = 128;
            _ring.widthMultiplier = 1.2f;
            _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ringMaterial = new Material(Shader.Find("Sprites/Default"));
            _ring.material = _ringMaterial;
            _ring.startColor = _ring.endColor = StormColor;
        }

        // ---- Frame ----

        void LateUpdate()
        {
            if (_view == null || !_view.Ready) return;
            if (_opponent == null) BuildOpponent();
            var now = _view.ServerNowMs;
            UpdateHud(now);
            UpdateRing();
            UpdateBarrels();
            UpdateBalls();
            UpdatePlate();
            UpdateWorldBar();
            UpdateChips();
            UpdateArrows();
            // Playtest (4 Oct): an end seen on a resync waits a moment for its "match_end" (the result) before showing.
            if (_view.Ended && !_endShown)
            {
                if (_endSeenAt < 0f) _endSeenAt = Time.unscaledTime;
                if (_view.End != null || Time.unscaledTime - _endSeenAt > 3f) ShowEnd();
            }
            if (_endShown && !_endHadResult && _view.End != null) ShowEnd(); // Playtest (5 Oct, review): the late result
            if (_endShown)
            {
                _endRematch.text = _view.RematchStatus;
                var left = _view.RematchMsLeft;
                _rematch.interactable = left > 0;
                OnlineUi.SetLabel(_rematch, left > 0 ? $"Rematch ({Mathf.CeilToInt(left / 1000f)} s)" : "Rematch closed");
            }
        }

        void UpdateHud(long now)
        {
            var left = !_view.Started ? _view.EndsAt - _view.StartsAt : _view.EndsAt - now;
            _clock.text = _view.Ended ? "0:00" : PvpRules.Clock(left);
            _clock.color = left < 30000 ? OnlineUi.Bad : Color.white;
            _myLine.text = $"{_view.MyName}  <color=#ffd166>Lv {_view.MyLevel}</color>";
            _oppLine.text = $"<color=#ffd166>Lv {_view.OpponentLevel}</color>  {_view.OpponentName}  <size=80%>HP {Mathf.CeilToInt(Mathf.Max(0f, _view.OpponentHp))}/{Mathf.RoundToInt(_view.OpponentMaxHp)}</size>" + (_view.OpponentConnected ? string.Empty : "  <color=#ff8c73>(away)</color>");
            _myBar.fillAmount = _view.MyMaxHp > 0f ? _view.MyHp / _view.MyMaxHp : 0f;
            _oppBar.fillAmount = _view.OpponentMaxHp > 0f ? _view.OpponentHp / _view.OpponentMaxHp : 0f;
            if (_view.OutsideStorm) { _storm.text = "Outside the storm! Get back inside the ring"; _storm.color = OnlineUi.Bad; }
            else if (_view.StormShrinking) { _storm.text = $"Storm closing: ring {Mathf.RoundToInt(_view.StormRadius)} m"; _storm.color = new Color(0.8f, 0.6f, 1f); }
            else { _storm.text = "Storm closes in " + PvpRules.Clock(_view.StormStartsAt - now); _storm.color = OnlineUi.Dim; }
            if (!_view.OpponentConnected && _view.OpponentForfeitAt > 0)
                _storm.text = "Opponent disconnected: they forfeit in " + PvpRules.Clock(_view.OpponentForfeitAt - now);

            var toStart = _view.StartsAt - now;
            if (!_view.Started && toStart > 0) _countdown.text = Mathf.CeilToInt(toStart / 1000f).ToString();
            else if (_view.Started && now - _view.StartsAt < 1200) _countdown.text = "Fight!";
            else if (_view.MySunk && !_view.Ended) _countdown.text = "Sunk!";
            else _countdown.text = string.Empty;
        }

        void UpdateRing()
        {
            var c = _view.ArenaCentre;
            var r = _view.StormRadius;
            for (var i = 0; i < _ring.positionCount; i++)
            {
                var a = i * Mathf.PI * 2f / _ring.positionCount;
                _ring.SetPosition(i, new Vector3(c.x + Mathf.Sin(a) * r, 0.4f, c.z + Mathf.Cos(a) * r));
            }
        }

        void UpdateBarrels()
        {
            var live = new HashSet<int>();
            foreach (var b in _view.Barrels)
            {
                live.Add(b.Id);
                if (!_barrels.TryGetValue(b.Id, out var go) || go == null)
                {
                    go = BarrelModel(b.Xp);
                    _barrels[b.Id] = go;
                }
                go.transform.position = b.World + Vector3.up * (0.6f + Mathf.Sin(Time.time * 2f + b.Id) * 0.25f);
                go.transform.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);
            }
            List<int> gone = null;
            foreach (var kv in _barrels) if (!live.Contains(kv.Key)) (gone ??= new List<int>()).Add(kv.Key);
            if (gone != null) foreach (var id in gone) { if (_barrels[id] != null) Destroy(_barrels[id]); _barrels.Remove(id); }
        }

        GameObject BarrelModel(bool xp)
        {
            var root = new GameObject(xp ? "PvpXpBarrel" : "PvpHealthBarrel");
            root.transform.SetParent(_world, false);
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(1.6f, 1.1f, 1.6f);
            Tint(body, xp ? XpColor : HealthColor);
            var band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(band.GetComponent<Collider>());
            band.transform.SetParent(root.transform, false);
            band.transform.localScale = new Vector3(1.75f, 0.12f, 1.75f);
            Tint(band, new Color(0.25f, 0.15f, 0.08f));
            // A tall beacon so it is seen from afar.
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(beam.GetComponent<Collider>());
            beam.transform.SetParent(root.transform, false);
            beam.transform.localPosition = new Vector3(0f, 6f, 0f);
            beam.transform.localScale = new Vector3(0.25f, 6f, 0.25f);
            Tint(beam, (xp ? XpColor : HealthColor) * 0.9f);
            return root;
        }

        static void Tint(GameObject go, Color color)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var m = r.material;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            m.color = color;
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * 0.6f);
            }
        }

        void UpdateBalls()
        {
            var shots = _view.Shots;
            while (_balls.Count < shots.Count)
            {
                var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(ball.GetComponent<Collider>());
                ball.transform.SetParent(_world, false);
                ball.transform.localScale = Vector3.one * 0.8f;
                var trail = ball.AddComponent<TrailRenderer>();
                trail.time = 0.25f;
                trail.widthMultiplier = 0.45f;
                trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.material = _ringMaterial;
                ball.SetActive(false);
                _balls.Add(ball);
                _ballColors.Add(Color.clear);
            }
            for (var i = 0; i < _balls.Count; i++)
            {
                var on = i < shots.Count && Time.time <= shots[i].End;
                if (_balls[i].activeSelf != on)
                {
                    _balls[i].SetActive(on);
                    if (on) _balls[i].GetComponent<TrailRenderer>().Clear();
                }
                if (!on) continue;
                var s = shots[i];
                var color = s.Color.a > 0f ? s.Color : new Color(0.12f, 0.12f, 0.12f);
                if (_ballColors[i] != color)
                {
                    _ballColors[i] = color;
                    Tint(_balls[i], color);
                    var trail = _balls[i].GetComponent<TrailRenderer>();
                    var smoke = color.grayscale < 0.3f ? new Color(0.85f, 0.85f, 0.85f) : color; // a dark cannonball leaves pale smoke
                    trail.startColor = new Color(smoke.r, smoke.g, smoke.b, 0.8f);
                    trail.endColor = new Color(smoke.r, smoke.g, smoke.b, 0f);
                }
                var k = Mathf.InverseLerp(s.Start, s.End, Time.time);
                var p = Vector3.Lerp(s.From, s.To, k);
                p.y = 1.5f + Mathf.Sin(k * Mathf.PI) * (s.Arc > 0f ? s.Arc : 5f);
                _balls[i].transform.position = p;
            }
        }

        void UpdatePlate()
        {
            var cam = Camera.main;
            var show = _opponent != null && cam != null && _view.OpponentKnown && !_endShown;
            if (show)
            {
                var vp = cam.WorldToViewportPoint(_view.OpponentPosition + Vector3.up * 8f);
                show = vp.z > 0f && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f;
                if (show)
                {
                    var rect = _canvas.rect;
                    _plate.anchoredPosition = new Vector2((vp.x - 0.5f) * rect.width, (vp.y - 0.5f) * rect.height);
                }
            }
            if (_plate.gameObject.activeSelf != show) _plate.gameObject.SetActive(show);
            if (!show) return;
            _plateName.text = $"{_view.OpponentName}  <color=#ffd166>Lv {_view.OpponentLevel}</color>  <size=80%>{_view.OpponentRating}</size>";
            _plateBar.fillAmount = _view.OpponentMaxHp > 0f ? _view.OpponentHp / _view.OpponentMaxHp : 0f;
            _plateHp.text = $"{Mathf.CeilToInt(Mathf.Max(0f, _view.OpponentHp))} / {Mathf.RoundToInt(_view.OpponentMaxHp)}";
        }

        /// <summary>The enemy health bar over the opponent's ship (until it sinks or the match ends).</summary>
        void UpdateWorldBar()
        {
            if (_worldBarDone) return;
            if (_view.Ended || _endShown || (_view.OpponentKnown && !_view.OpponentAlive))
            {
                ReleaseWorldBar();
                _worldBarDone = true;
                return;
            }
            if (_oppWorldBar == null && _opponent != null && _enemyViews != null) _oppWorldBar = _enemyViews.AttachOpponentHealthBar(_opponent);
            if (_oppWorldBar != null) _oppWorldBar.SetFill(_view.OpponentMaxHp > 0f ? _view.OpponentHp / _view.OpponentMaxHp : 0f);
        }

        bool _worldBarDone;

        void ReleaseWorldBar()
        {
            if (_oppWorldBar == null) return;
            if (_enemyViews != null) _enemyViews.Release(null, null, _oppWorldBar.transform);
            else Destroy(_oppWorldBar.gameObject);
            _oppWorldBar = null;
        }

        /// <summary>Edge arrows: red to the opponent, gold to XP barrels, green to health barrels (while off-screen).</summary>
        void UpdateArrows()
        {
            var cam = Camera.main;
            var n = 0;
            if (cam != null && !_endShown)
            {
                if (_view.OpponentKnown && _view.OpponentAlive) Arrow(cam, _view.OpponentPosition, Red, "FOE", ref n);
                foreach (var b in _view.Barrels) Arrow(cam, b.World, b.Xp ? XpColor : HealthColor, b.Xp ? "XP" : "HP", ref n);
            }
            for (var i = n; i < _arrows.Count; i++) if (_arrows[i].gameObject.activeSelf) _arrows[i].gameObject.SetActive(false);
        }

        void Arrow(Camera cam, Vector3 world, Color color, string label, ref int n)
        {
            var vp = cam.WorldToViewportPoint(world);
            var behind = vp.z < 0f;
            if (!behind && vp.x > 0.03f && vp.x < 0.97f && vp.y > 0.03f && vp.y < 0.97f) return;
            if (n >= _arrows.Count) _arrows.Add(NewArrow());
            var a = _arrows[n++];
            if (!a.gameObject.activeSelf) a.gameObject.SetActive(true);
            var dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
            if (behind) dir = -dir;
            var rect = _arrowRoot.rect;
            dir.x *= rect.width;
            dir.y *= rect.height;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
            dir.Normalize();
            var halfW = Mathf.Max(1f, rect.width * 0.5f - 60f);
            var halfH = Mathf.Max(1f, rect.height * 0.5f - (dir.y > 0f ? 200f : 60f)); // clear of the match HUD at the top
            var t = Mathf.Min(Mathf.Abs(dir.x) > 0.0001f ? halfW / Mathf.Abs(dir.x) : float.MaxValue, Mathf.Abs(dir.y) > 0.0001f ? halfH / Mathf.Abs(dir.y) : float.MaxValue);
            a.anchoredPosition = dir * t;
            var tri = (RectTransform)a.GetChild(0);
            tri.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f);
            tri.GetComponent<Image>().color = color;
            var text = a.GetChild(1).GetComponent<TextMeshProUGUI>();
            text.text = label;
            ((RectTransform)text.transform).anchoredPosition = -dir * 40f;
        }

        RectTransform NewArrow()
        {
            var root = (RectTransform)new GameObject("Arrow", typeof(RectTransform)).transform;
            root.SetParent(_arrowRoot, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.sizeDelta = Vector2.zero;
            var tri = (RectTransform)new GameObject("Triangle", typeof(RectTransform), typeof(Image)).transform;
            tri.SetParent(root, false);
            tri.sizeDelta = new Vector2(46f, 46f);
            var img = tri.GetComponent<Image>();
            img.sprite = MinimapUiSprites.Triangle;
            img.raycastTarget = false;
            var outline = tri.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);
            var label = OnlineUi.Text(root, "Label", string.Empty, 20, TextAlignmentOptions.Center, Color.white);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            label.rectTransform.sizeDelta = new Vector2(70f, 26f);
            label.fontStyle = FontStyles.Bold;
            label.outlineWidth = 0.25f;
            label.outlineColor = new Color32(0, 0, 0, 255);
            return root;
        }

        // ---- The opponent's ship ----

        void BuildOpponent()
        {
            var model = _view.ShipModel;
            if (model == null) return;
            var root = new GameObject("PvpOpponentShip").transform;
            root.SetParent(_world, false);
            var shipRoot = FindShipRoot(model);
            root.SetPositionAndRotation(shipRoot.position, shipRoot.rotation);
            var copy = Instantiate(model.gameObject, model.position, model.rotation, root);
            copy.name = "Model";
            copy.transform.localScale = model.lossyScale;
            foreach (var b in copy.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(b);
            foreach (var c in copy.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var r in copy.GetComponentsInChildren<Rigidbody>(true)) Destroy(r);
            foreach (var r in copy.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                foreach (var m in r.materials)
                {
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.Lerp(m.GetColor("_BaseColor"), Red, 0.7f));
                    else if (m.HasProperty("_Color")) m.color = Color.Lerp(m.color, Red, 0.7f);
                }
            }
            _opponent = root;
            _view.AttachOpponentView(root);
        }

        /// <summary>The ship's own object (the one the ship controller is on) above its model.</summary>
        static Transform FindShipRoot(Transform model)
        {
            for (var t = model; t != null; t = t.parent)
                if (t.GetComponent<RoguePirates.V4.Foundation.Player.PlayerShipController>() != null) return t;
            return model;
        }

        // ---- The end ----

        void ShowEnd()
        {
            _endShown = true;
            var end = _view.End;
            _endHadResult = end != null;
            var json = _view.EndJson;
            var me = _view.MyPlayerId;
            var opp = _view.OpponentPlayerId;
            var winner = end != null ? end.winnerPlayerId : null;
            _endTitle.text = end == null ? "Match over" : string.IsNullOrEmpty(winner) ? "Draw" : winner == me ? "Victory!" : "Defeat";
            _endTitle.color = string.IsNullOrEmpty(winner) ? OnlineUi.TextColor : winner == me ? OnlineUi.Good : OnlineUi.Bad;
            if (UiKit.Ready) _endTitle.color = UiKit.InkFor(_endTitle.color);
            _endReason.text = Reason(end != null ? end.reason : null, winner == me) + (end != null ? $"   ({OnlineRules.RunTime(end.durationSeconds)})" : string.Empty);
            var mine = PvpJson.Entry<EndStats>(json, "stats", me);
            var theirs = PvpJson.Entry<EndStats>(json, "stats", opp);
            _endStats.text =
                $"<color=#ffd166><b>{_view.MyName}</b></color>  vs  <color=#ff8c73><b>{_view.OpponentName}</b></color>\n\n" +
                Line("Damage dealt", mine?.damageDealt, theirs?.damageDealt) +
                Line("Damage taken", mine?.damageTaken, theirs?.damageTaken) +
                Line("Level", mine?.level, theirs?.level) +
                Line("Barrels", mine?.barrelsTaken, theirs?.barrelsTaken) +
                Line("Final hull", mine != null ? mine.finalHp : (float?)null, theirs != null ? theirs.finalHp : (float?)null);
            _endStats.text = UiKit.Ready ? UiKit.InkRich(_endStats.text) : _endStats.text;
            var rating = PvpJson.Entry<RatingChange>(json, "ratings", me);
            if (end != null && end.rated && rating != null && rating.after > 0)
                _endRating.text = $"Rating {rating.before} -> {rating.after}  ({(rating.change >= 0 ? "+" : string.Empty)}{rating.change})";
            else _endRating.text = _view.IsBotMatch ? "Practice match: unrated" : "Unrated match";
            _end.gameObject.SetActive(true);
            _end.SetAsLastSibling();
            _plate.gameObject.SetActive(false);
        }

        static string Line(string label, float? mine, float? theirs) =>
            $"{label}:  <color=#ffd166>{Num(mine)}</color>  vs  <color=#ff8c73>{Num(theirs)}</color>\n";

        static string Line(string label, int? mine, int? theirs) =>
            $"{label}:  <color=#ffd166>{(mine.HasValue ? mine.Value.ToString() : "-")}</color>  vs  <color=#ff8c73>{(theirs.HasValue ? theirs.Value.ToString() : "-")}</color>\n";

        static string Num(float? v) => v.HasValue ? Mathf.RoundToInt(v.Value).ToString() : "-";

        static string Reason(string reason, bool won)
        {
            switch (reason)
            {
                case "sunk": return won ? "The enemy ship sank" : "Your ship sank";
                case "time": return won ? "Time: you dealt more damage" : "Time: they dealt more damage";
                case "forfeit": return won ? "Your opponent left the match" : "You left the match";
                case "draw": return "Time: dead even";
                // V5 M8z (6 Oct, owner): the server forfeited a player whose hits it kept refusing
                case "anticheat": return won ? "Opponent disqualified: invalid hits" : "Disqualified: too many invalid hits";
                default: return string.IsNullOrEmpty(reason) ? "Match over" : reason;
            }
        }

        void OnDestroy()
        {
            ReleaseWorldBar();
            if (_ringMaterial != null) Destroy(_ringMaterial);
            if (_world != null) Destroy(_world.gameObject);
            foreach (var go in _hidden) if (go != null) go.SetActive(true);
        }
    }
}
