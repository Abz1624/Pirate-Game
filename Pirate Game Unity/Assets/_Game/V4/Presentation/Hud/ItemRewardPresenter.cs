using System;
using System.Collections.Generic;
using RoguePirates.V4.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Hud
{
    /// <summary>
    /// Playtest (3 Oct 2026, owner): items are shown when they are won (Survivor.io's chest reveal): a panel near the top of
    /// the screen with the item's icon, name and effect for a few seconds. It watches the loadout view for a new item or a
    /// new stack (chests, the Powder Cache's treasure chest); several in a row queue up. Reads only the loadout view.
    /// </summary>
    public sealed class ItemRewardPresenter : MonoBehaviour
    {
        const float Width = 520f, Height = 132f, IconSize = 104f;
        const float ShowSeconds = 3.2f, FadeSeconds = 0.25f;

        IRunViews _views;
        IconCatalog _icons;
        Func<string, string> _describe;
        CanvasGroup _group;
        Image _icon;
        TextMeshProUGUI _header, _name, _description;
        readonly Dictionary<string, int> _known = new Dictionary<string, int>();
        readonly Queue<(string id, string name, int stacks)> _queue = new Queue<(string, string, int)>();
        bool _baselined;
        float _shownFor = -1f;
        static Sprite _white;

        /// <summary>The item on screen now (null when hidden), for checks.</summary>
        public string ShowingItemId { get; private set; }

        public static ItemRewardPresenter Create(Transform hudCanvas)
        {
            var existing = hudCanvas.GetComponentInChildren<ItemRewardPresenter>(true);
            if (existing != null) return existing;
            var go = new GameObject("V4ItemReward", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(hudCanvas, false);
            var presenter = go.AddComponent<ItemRewardPresenter>();
            presenter.Build((RectTransform)go.transform);
            return presenter;
        }

        /// <param name="describe">The item's effect text by id (null: name only).</param>
        public void Bind(IRunViews views, IconCatalog icons, Func<string, string> describe)
        {
            _views = views;
            _icons = icons;
            _describe = describe;
            _known.Clear();
            _queue.Clear();
            _baselined = false;
            Hide();
        }

        void Build(RectTransform rt)
        {
            _group = rt.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(Width, Height);
            rt.anchoredPosition = new Vector2(0f, -150f);
            var bg = rt.GetComponent<Image>();
            bg.sprite = White();
            bg.color = new Color(0.06f, 0.09f, 0.14f, 0.97f);
            bg.raycastTarget = false;

            // A gold border: the outline draws the panel again, offset, behind it.
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.95f, 0.72f, 0.25f, 1f);
            outline.effectDistance = new Vector2(3f, -3f);
            outline.useGraphicAlpha = false;

            var iconGo = Child(rt, "Icon");
            var irt = (RectTransform)iconGo.transform;
            irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0f, 0.5f);
            irt.sizeDelta = new Vector2(IconSize, IconSize);
            irt.anchoredPosition = new Vector2(14f, 0f);
            _icon = iconGo.AddComponent<Image>();
            _icon.preserveAspect = true;
            _icon.raycastTarget = false;

            const float textLeft = 14f + IconSize + 14f;
            _header = Text(rt, "Header", textLeft, -10f, 22f, 15f, FontStyles.Bold, new Color(0.95f, 0.72f, 0.25f, 1f));
            _name = Text(rt, "Name", textLeft, -32f, 34f, 26f, FontStyles.Bold, Color.white);
            _description = Text(rt, "Description", textLeft, -68f, 56f, 16f, FontStyles.Normal, new Color(0.85f, 0.88f, 0.92f, 1f));
            _description.enableWordWrapping = true;
            Hide();
        }

        static GameObject Child(Transform parent, string name, params Type[] extra)
        {
            var types = new List<Type> { typeof(RectTransform) };
            types.AddRange(extra);
            var go = new GameObject(name, types.ToArray());
            go.transform.SetParent(parent, false);
            return go;
        }

        static TextMeshProUGUI Text(RectTransform parent, string name, float left, float top, float height, float size, FontStyles style, Color color)
        {
            var go = Child(parent, name);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(left, top - height);
            rt.offsetMax = new Vector2(-14f, top);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            return t;
        }

        static Sprite White()
        {
            if (_white != null) return _white;
            var tex = Texture2D.whiteTexture;
            _white = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            return _white;
        }

        void Update()
        {
            Watch();
            if (_shownFor >= 0f)
            {
                _shownFor += Time.unscaledDeltaTime;
                var fadeIn = Mathf.Clamp01(_shownFor / FadeSeconds);
                var fadeOut = Mathf.Clamp01((ShowSeconds - _shownFor) / FadeSeconds);
                _group.alpha = Mathf.Min(fadeIn, fadeOut);
                if (_shownFor >= ShowSeconds) Hide();
            }
            if (_shownFor < 0f && _queue.Count > 0) Show(_queue.Dequeue());
        }

        /// <summary>A new item or a new stack since the last frame queues a panel; the loadout at the start of a run is the baseline.</summary>
        void Watch()
        {
            var loadout = _views?.Loadout;
            if (loadout == null) return;
            var total = 0;
            for (var i = 0; i < loadout.ItemCount; i++) total += loadout.ItemStacks(i);
            if (total < KnownTotal()) { _known.Clear(); _baselined = false; } // a new run
            for (var i = 0; i < loadout.ItemCount; i++)
            {
                var id = loadout.ItemId(i);
                if (string.IsNullOrEmpty(id)) continue;
                var stacks = loadout.ItemStacks(i);
                _known.TryGetValue(id, out var before);
                if (stacks > before && _baselined) _queue.Enqueue((id, loadout.ItemName(i), stacks));
                _known[id] = stacks;
            }
            _baselined = true;
        }

        int KnownTotal()
        {
            var n = 0;
            foreach (var kv in _known) n += kv.Value;
            return n;
        }

        void Show((string id, string name, int stacks) item)
        {
            ShowingItemId = item.id;
            var sprite = _icons != null ? _icons.Item(item.id) : null;
            _icon.sprite = sprite;
            _icon.enabled = sprite != null;
            _header.text = item.stacks > 1 ? $"ITEM UPGRADED (x{item.stacks})" : "NEW ITEM";
            _name.text = RoguePirates.V4.Foundation.Core.DemoMode.Text(item.name);
            var description = _describe?.Invoke(item.id);
            _description.text = string.IsNullOrEmpty(description) ? string.Empty : RoguePirates.V4.Foundation.Core.DemoMode.Text(description);
            _shownFor = 0f;
            _group.alpha = 0f;
            transform.SetAsLastSibling();
        }

        void Hide()
        {
            ShowingItemId = null;
            _shownFor = -1f;
            if (_group != null) _group.alpha = 0f;
        }
    }
}
