using System.Collections.Generic;
using System.Linq;
using RoguePirates.V4.Config;
using RoguePirates.V5.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Menu
{
    /// <summary>
    /// V5 M8: the menu hub's screens over the main menu (mobile first, 72-unit targets): mode select (Voyage now, the
    /// rest "coming"), the shop (Weapons, Logbooks, Items, Captains for RUM), the collection and the profile. Reads and
    /// buys through the meta services only.
    /// </summary>
    public sealed class MenuHubPresenter : MonoBehaviour
    {
        public enum Screen { None, Modes, Shop, Collection, Profile }

        static readonly Color Panel = new Color(0.06f, 0.1f, 0.16f, 0.97f);
        static readonly Color Text = new Color(0.95f, 0.93f, 0.88f, 1f);
        static readonly Color[] RarityColors =
        {
            new Color(0.8f, 0.8f, 0.8f), new Color(0.45f, 0.85f, 0.45f), new Color(0.4f, 0.65f, 1f), new Color(0.75f, 0.5f, 1f), new Color(1f, 0.8f, 0.3f),
        };
        static readonly (StoreCategory cat, string name)[] Tabs =
        {
            (StoreCategory.Weapon, "Weapons"), (StoreCategory.Logbook, "Logbooks"), (StoreCategory.Item, "Items"), (StoreCategory.Captain, "Captains"),
        };

        LocalMetaServices _meta;
        System.Action _voyage;
        RectTransform _root, _list;
        Button _close;
        TextMeshProUGUI _title, _balance, _message;
        GameObject _tabs;
        StoreCategory _category;
        readonly List<GameObject> _rows = new List<GameObject>();

        public Screen Showing { get; private set; }
        public LocalMetaServices Meta => _meta;
        public int RowCount => _rows.Count;
        public string LastMessage => _message != null ? _message.text : string.Empty;

        public static MenuHubPresenter Create(Transform canvas, LocalMetaServices meta, System.Action voyage)
        {
            var go = new GameObject("V5MenuHub", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvas, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.06f, 0.06f);
            rt.anchorMax = new Vector2(0.94f, 0.94f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = Panel;
            SettingsPanel.DrawOnTop(go, 200);
            var hub = go.AddComponent<MenuHubPresenter>();
            hub._meta = meta;
            hub._voyage = voyage;
            hub.Build(rt);
            go.SetActive(false);
            return hub;
        }

        void Build(RectTransform rt)
        {
            _root = rt;
            // Clicks in the margins stop here, not on the art menu behind (invisible; first child, so the content is above).
            var blocker = new GameObject("Blocker", typeof(RectTransform), typeof(Image));
            blocker.transform.SetParent(rt, false);
            var brt = (RectTransform)blocker.transform;
            brt.anchorMin = new Vector2(-0.1f, -0.1f);
            brt.anchorMax = new Vector2(1.1f, 1.1f);
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            blocker.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            var header = Strip("Header", rt, true, 96f, 12f);
            _title = Label(header, "Title", 44, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 0f), new Vector2(0.5f, 1f));
            _balance = Label(header, "Balance", 32, TextAlignmentOptions.MidlineRight, new Vector2(0.5f, 0f), new Vector2(0.85f, 1f));
            var close = SettingsPanel.CreateButton(header, "Close", Close);
            _close = close;
            Place(close.transform, new Vector2(0.86f, 0.1f), new Vector2(1f, 0.9f));

            _tabs = Strip("Tabs", rt, true, 88f, 120f).gameObject;
            var tl = _tabs.AddComponent<HorizontalLayoutGroup>();
            tl.spacing = 12f;
            tl.childControlWidth = tl.childControlHeight = true;
            tl.childForceExpandWidth = true;
            foreach (var (cat, name) in Tabs)
            {
                var c = cat;
                SettingsPanel.CreateButton(_tabs.transform, name, () => ShowShop(c));
            }

            var scroll = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask));
            scroll.transform.SetParent(rt, false);
            var srt = (RectTransform)scroll.transform;
            srt.anchorMin = new Vector2(0f, 0f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.offsetMin = new Vector2(24f, 88f);
            srt.offsetMax = new Vector2(-24f, -296f);
            scroll.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.15f);
            scroll.GetComponent<Mask>().showMaskGraphic = true;
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(scroll.transform, false);
            _list = (RectTransform)content.transform;
            _list.anchorMin = new Vector2(0f, 1f);
            _list.anchorMax = new Vector2(1f, 1f);
            _list.pivot = new Vector2(0.5f, 1f);
            _list.sizeDelta = Vector2.zero; // stretched: exactly the view's width (the default 100 spilled 50 each side)
            var v = content.GetComponent<VerticalLayoutGroup>();
            v.spacing = 10f;
            v.padding = new RectOffset(12, 12, 12, 12);
            v.childControlHeight = v.childControlWidth = true;
            v.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sr = scroll.GetComponent<ScrollRect>();
            sr.content = _list;
            sr.horizontal = false;

            var footer = Strip("Footer", rt, false, 72f, 8f);
            _message = Label(footer, "Message", 28, TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
        }

        /// <summary>A full-width strip <paramref name="height"/> tall, <paramref name="fromEdge"/> in from the top (positive)
        /// or the bottom (negative edge distance as a positive number with top = false).</summary>
        static RectTransform Strip(string name, RectTransform parent, bool top, float height, float fromEdge)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
            rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
            rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
            rt.sizeDelta = new Vector2(-48f, height);
            rt.anchoredPosition = new Vector2(0f, top ? -fromEdge : fromEdge);
            return rt;
        }

        static void Place(Transform t, Vector2 min, Vector2 max)
        {
            var rt = (RectTransform)t;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var le = t.GetComponent<LayoutElement>();
            if (le != null) le.ignoreLayout = true;
        }

        static TextMeshProUGUI Label(Transform parent, string name, float size, TextAlignmentOptions align, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.alignment = align;
            t.color = Text;
            t.raycastTarget = false;
            return t;
        }

        void Open(Screen screen, string title, bool tabs)
        {
            Showing = screen;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _title.text = title;
            _tabs.SetActive(tabs);
            _message.text = string.Empty;
            RefreshBalance();
            // Gamepad: start on Close; automatic navigation reaches the tabs and rows.
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(_close.gameObject);
        }

        void RefreshBalance() => _balance.text = $"{_meta?.Balance ?? 0} RUM";

        public void Close()
        {
            Showing = Screen.None;
            gameObject.SetActive(false);
        }

        void ClearRows()
        {
            foreach (var r in _rows)
            {
                if (r == null) continue;
                r.SetActive(false); // out of the layout this frame
                if (Application.isPlaying) Destroy(r); else DestroyImmediate(r);
            }
            _rows.Clear();
        }

        Hud.IconCatalog _icons;
        bool _iconsLoaded;

        /// <summary>Hackathon (13 Oct): the art team's icon for a shop or collection entry (V4IconCatalog), or null.</summary>
        Sprite IconOf(StoreCategory category, string contentId)
        {
            if (!_iconsLoaded) { _icons = Hud.IconCatalog.Load(); _iconsLoaded = true; }
            if (_icons == null) return null;
            switch (category)
            {
                case StoreCategory.Weapon: return _icons.Weapon(contentId);
                case StoreCategory.Logbook: return _icons.Logbook(contentId);
                case StoreCategory.Item: return _icons.Item(contentId);
                default: return null;
            }
        }

        TextMeshProUGUI Row(string text, Color color, string buttonText = null, System.Action click = null, bool interactable = true, Sprite icon = null)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(_list, false);
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.05f);
            go.GetComponent<LayoutElement>().preferredHeight = 88f;
            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(20, 12, 8, 8);
            h.spacing = 16f;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            if (icon != null)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                iconGo.transform.SetParent(go.transform, false);
                var image = iconGo.GetComponent<Image>();
                image.sprite = icon;
                image.preserveAspect = true;
                image.raycastTarget = false;
                var le = iconGo.GetComponent<LayoutElement>();
                le.minWidth = le.preferredWidth = 72f;
                le.minHeight = le.preferredHeight = 72f;
            }
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            labelGo.transform.SetParent(go.transform, false);
            labelGo.GetComponent<LayoutElement>().flexibleWidth = 1f;
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 30;
            label.color = color;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            if (buttonText != null)
            {
                var b = SettingsPanel.CreateButton(go.transform, buttonText, () => click?.Invoke());
                b.GetComponent<LayoutElement>().minWidth = 260f;
                b.interactable = interactable;
            }
            _rows.Add(go);
            return label;
        }

        // ---- Screens --------------------------------------------------------------------------------------------------

        public void ShowModes()
        {
            Open(Screen.Modes, "Choose a mode", false);
            ClearRows();
            Row("Voyage: sail Saltwind, fight the fleet, beat the Barnacle King", Text, "Sail", () => { Close(); _voyage?.Invoke(); });
            foreach (var mode in new[] { "Island Raids", "Island Race", "1v1 Arena", "Hunt (4 players)" })
                Row(mode, new Color(0.6f, 0.6f, 0.6f), "Coming", null, false);
        }

        public void ShowShop(StoreCategory category = StoreCategory.Weapon)
        {
            _category = category;
            Open(Screen.Shop, "Shop", true);
            RefreshShop();
        }

        void RefreshShop()
        {
            ClearRows();
            RefreshBalance();
            if (_meta == null) return;
            foreach (var e in _meta.Entries(_category))
            {
                var owned = _meta.Owns(e.ContentId);
                var tags = (e.IsNew ? "  NEW" : string.Empty) + (e.OnSale ? $"  SALE (was {e.BasePrice})" : string.Empty);
                var color = RarityColors[Mathf.Clamp(e.Rarity, 0, 4)];
                var icon = IconOf(_category, e.ContentId);
                if (owned) { Row(e.DisplayName + tags, color, "Owned", null, false, icon); continue; }
                var id = e.ContentId;
                Row(e.DisplayName + tags, color, $"Buy {e.Price} RUM", () => Buy(id), _meta.CanAfford(e.Price), icon);
            }
            if (_rows.Count == 0) Row("Nothing for sale here yet.", Text);
        }

        /// <summary>Buys through the store and shows its message (tests and the menu check call it).</summary>
        public PurchaseResult Buy(string contentId)
        {
            var result = _meta.Buy(contentId);
            RefreshShop();
            _message.text = _meta.Message(contentId, result);
            return result;
        }

        public void ShowCollection()
        {
            Open(Screen.Collection, "Collection", false);
            ClearRows();
            foreach (var (cat, name) in Tabs)
            {
                var owned = _meta.Owned(cat);
                var total = _meta.All.Count(e => e.Category == cat);
                Row($"{name}: {owned.Count} of {total}", Text);
                foreach (var e in owned) Row("   " + e.DisplayName, RarityColors[Mathf.Clamp(e.Rarity, 0, 4)], icon: IconOf(cat, e.ContentId));
            }
        }

        public void ShowProfile()
        {
            Open(Screen.Profile, "Profile", false);
            ClearRows();
            Row("Your collection", Text, "Open", ShowCollection);
            Row($"{_meta.DisplayName} (guest, this device)", Text);
            Row($"RUM: {_meta.Balance}", Text);
            var records = new RoguePirates.V4.Difficulty.PlayerPrefsRunRecordStore().Records;
            Row($"Runs recorded: {records.Count}", Text);
            if (records.Count > 0)
            {
                var best = records.OrderByDescending(r => r.kills).First();
                var longest = records.Max(r => r.runSeconds);
                Row($"Most kills in a run: {best.kills}", Text);
                Row($"Longest run: {Mathf.FloorToInt(longest / 60f)}:{Mathf.FloorToInt(longest % 60f):00}", Text);
            }
            foreach (var (cat, name) in Tabs) Row($"{name} owned: {_meta.Owned(cat).Count}", Text);
        }
    }
}
