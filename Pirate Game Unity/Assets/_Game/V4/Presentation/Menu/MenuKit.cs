using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Menu
{
    /// <summary>
    /// Hackathon UI (13 Oct, owner): the parchment kit for V4's own menus and panels (settings, pause, the hub's Ledger and
    /// profile, the captain profile, the 1v1 draft, results, the item popup). V4 does not reference the hackathon assembly,
    /// so the kit's sprites and font are read by name from the same Resources asset (Resources/HackSkin: its public
    /// "sprites" and "font" fields). Without the asset every call falls back to the old flat colours, so nothing breaks.
    /// </summary>
    internal static class MenuKit
    {
        public static readonly Color Ink = new Color(0.24f, 0.14f, 0.06f, 1f);
        public static readonly Color InkDim = new Color(0.48f, 0.35f, 0.23f, 1f);
        public static readonly Color InkGold = new Color(0.55f, 0.3f, 0.02f, 1f);
        public static readonly Color InkGood = new Color(0.17f, 0.45f, 0.14f, 1f);
        public static readonly Color InkBad = new Color(0.66f, 0.16f, 0.09f, 1f);
        public static readonly Color OnWood = new Color(1f, 0.94f, 0.8f, 1f);
        public const string InkHex = "#3d240f", DimHex = "#7a5a3a", GoldHex = "#8c4d05", GoodHex = "#2b7324", BadHex = "#a8291a";

        static Dictionary<string, Sprite> _sprites;
        static TMP_FontAsset _font;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _sprites = null;
            _font = null;
        }

        static void Load()
        {
            if (_sprites != null) return;
            _sprites = new Dictionary<string, Sprite>();
            var skin = Resources.Load<ScriptableObject>("HackSkin");
            if (skin == null) return;
            var type = skin.GetType();
            if (type.GetField("sprites")?.GetValue(skin) is Sprite[] all)
                foreach (var sp in all)
                    if (sp != null) _sprites[sp.name] = sp;
            _font = type.GetField("font")?.GetValue(skin) as TMP_FontAsset;
        }

        /// <summary>The kit sprite called <paramref name="name"/> (null without the kit).</summary>
        public static Sprite S(string name)
        {
            Load();
            return !string.IsNullOrEmpty(name) && _sprites.TryGetValue(name, out var sp) ? sp : null;
        }

        /// <summary>True when the kit is present (its parchment card at least).</summary>
        public static bool Ready => S("Panel_Card") != null;

        public static TMP_FontAsset Font
        {
            get { Load(); return _font; }
        }

        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Fill(Transform parent, string name, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            var rt = Node(parent, name);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>A strip across the top of <paramref name="parent"/>, <paramref name="y"/> down and <paramref name="h"/> tall.</summary>
        public static RectTransform Strip(Transform parent, string name, float left, float y, float right, float h)
        {
            var rt = Node(parent, name);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(left, -y - h);
            rt.offsetMax = new Vector2(-right, -y);
            return rt;
        }

        /// <summary>Kit sprite on <paramref name="target"/>'s Image (added when missing; 9-sliced when the sprite has borders);
        /// without the kit, <paramref name="fallback"/> (or the tint) as a flat colour.</summary>
        public static Image Paint(Component target, string sprite, float thin = 1f, Color? tint = null, Color? fallback = null)
        {
            var img = target.GetComponent<Image>();
            if (img == null) img = target.gameObject.AddComponent<Image>();
            var s = S(sprite);
            if (s != null)
            {
                img.sprite = s;
                img.type = s.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
                img.pixelsPerUnitMultiplier = thin;
                img.preserveAspect = img.type == Image.Type.Simple;
                img.color = tint ?? Color.white;
            }
            else if (fallback.HasValue || tint.HasValue) img.color = fallback ?? tint.Value;
            return img;
        }

        /// <summary>A kit picture (aspect kept) on a new child.</summary>
        public static Image Picture(RectTransform rt, string sprite, Color? tint = null)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            var s = S(sprite);
            img.sprite = s;
            img.preserveAspect = true;
            img.color = s != null ? tint ?? Color.white : new Color(0f, 0f, 0f, 0f);
            return img;
        }

        /// <summary>Ink text in the kit font.</summary>
        public static void InkText(TMP_Text t, Color color, float? size = null)
        {
            if (t == null) return;
            if (Font != null) t.font = Font;
            t.color = color;
            if (size.HasValue) t.fontSize = size.Value;
        }

        public static TextMeshProUGUI Label(RectTransform rt, string text, float size, Color color, TextAlignmentOptions align, bool bold = false, float minSize = 0f)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null) t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            if (minSize > 0f)
            {
                t.enableAutoSizing = true;
                t.fontSizeMin = minSize;
                t.fontSizeMax = size;
            }
            return t;
        }

        /// <summary>
        /// Re-skins a button in the kit: <paramref name="style"/> (Btn_Gold, Btn_Parchment, Btn_Green, Btn_Red,
        /// Btn_Square_Gold, Btn_Square_Parchment) on its target graphic, the pressed and disabled art, and its label in ink
        /// and the kit font. Without the kit the button keeps its look.
        /// </summary>
        public static void StyleButton(Button b, string style = "Btn_Gold", float thin = 1f)
        {
            if (b == null) return;
            var img = b.targetGraphic as Image ?? b.GetComponent<Image>();
            var s = S(style);
            if (img == null || s == null) return;
            b.targetGraphic = img;
            // A legacy UI Text label (the old placeholder buttons) becomes a TMP label in the kit font.
            foreach (var legacy in b.GetComponentsInChildren<Text>(true))
            {
                var go = legacy.gameObject;
                var text = legacy.text;
                var size = Mathf.Max(24f, legacy.resizeTextForBestFit ? legacy.resizeTextMaxSize : legacy.fontSize);
                UnityEngine.Object.DestroyImmediate(legacy);
                var t = go.AddComponent<TextMeshProUGUI>();
                t.text = text;
                t.fontSize = size;
                t.alignment = TextAlignmentOptions.Center;
                t.raycastTarget = false;
            }
            img.sprite = s;
            img.type = s.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            img.pixelsPerUnitMultiplier = thin;
            img.color = Color.white;
            var pressed = S(style == "Btn_Gold" ? "Btn_Gold_Pressed" : null);
            var disabled = S(style == "Btn_Gold" ? "Btn_Gold_Disabled" : style.StartsWith("Btn_Square") ? "Btn_Square_Disabled" : null);
            if (pressed != null || disabled != null)
            {
                b.transition = Selectable.Transition.SpriteSwap;
                b.colors = ColorBlock.defaultColorBlock;
                img.canvasRenderer.SetColor(Color.white); // an earlier colour tint stays on the renderer otherwise
                b.spriteState = new SpriteState { pressedSprite = pressed != null ? pressed : s, highlightedSprite = s, selectedSprite = s, disabledSprite = disabled != null ? disabled : s };
            }
            else
            {
                b.transition = Selectable.Transition.ColorTint;
                var c = ColorBlock.defaultColorBlock;
                c.highlightedColor = new Color(1f, 0.97f, 0.9f, 1f);
                c.pressedColor = new Color(0.85f, 0.8f, 0.7f, 1f);
                c.disabledColor = new Color(1f, 1f, 1f, 0.55f);
                b.colors = c;
            }
            foreach (var t in b.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (Font != null) t.font = Font;
                t.color = Ink;
                t.fontStyle |= FontStyles.Bold;
                if (!t.enableAutoSizing)
                {
                    t.enableAutoSizing = true;
                    t.fontSizeMax = t.fontSize;
                    t.fontSizeMin = Mathf.Max(14f, t.fontSize * 0.6f);
                }
                t.margin = new Vector4(10f, 0f, 10f, 0f);
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        /// <summary>A kit icon left of a button's label (the label keeps the rest of the width).</summary>
        public static void ButtonIcon(Button b, string icon, float size = 34f)
        {
            if (b == null || S(icon) == null) return;
            var label = b.GetComponentInChildren<TextMeshProUGUI>(true);
            var ic = Node(b.transform, "Icon");
            ic.anchorMin = ic.anchorMax = new Vector2(0f, 0.5f);
            ic.pivot = new Vector2(0f, 0.5f);
            ic.sizeDelta = new Vector2(size, size);
            ic.anchoredPosition = new Vector2(18f, 0f);
            Picture(ic, icon, Ink);
            if (label != null) label.margin = new Vector4(size + 26f, 0f, 12f, 0f);
        }

        /// <summary>The parchment sheet on a panel's own Image (torn-edge card), keeping its raycast.</summary>
        public static void Sheet(Image panel, float thin = 1f)
        {
            if (panel == null || S("Panel_Card") == null) return;
            Paint(panel, "Panel_Card", thin);
        }

        /// <summary>
        /// The wood header across the top of a panel: the skull, the title (on wood) and room on the right for its buttons.
        /// Returns the header; <paramref name="title"/> gets the title text.
        /// </summary>
        public static RectTransform WoodHeader(RectTransform panel, string text, float height, out TextMeshProUGUI title, float rightRoom = 300f)
        {
            var head = Strip(panel, "KitHeader", -10f, -8f, -10f, height);
            head.gameObject.AddComponent<LayoutElement>().ignoreLayout = true; // panels with a layout group keep it out of the flow
            Paint(head, "Header_Wood", 1.4f, null, new Color(0.32f, 0.2f, 0.1f, 0.97f)).raycastTarget = false;
            var skullSize = height * 0.72f;
            var skull = Node(head, "Skull");
            skull.anchorMin = skull.anchorMax = new Vector2(0f, 0.5f);
            skull.pivot = new Vector2(0f, 0.5f);
            skull.sizeDelta = new Vector2(skullSize * 1.1f, skullSize);
            skull.anchoredPosition = new Vector2(26f, 0f);
            Picture(skull, "Icon_Skull_Crossbones", OnWood);
            var left = 26f + skullSize * 1.1f + 18f;
            var tr = Node(head, "Title");
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(left, 0f);
            tr.offsetMax = new Vector2(-rightRoom, 0f);
            title = Label(tr, text, height * 0.5f, OnWood, TextAlignmentOptions.MidlineLeft, true, height * 0.3f);
            head.SetAsFirstSibling();
            // The panel's click blocker stays the back-most child: in front of the header it ate the header's buttons
            // (the hub's Close on the Profile and Collection screens did nothing).
            var blocker = panel.Find("Blocker");
            if (blocker != null) blocker.SetAsFirstSibling();
            return head;
        }

        /// <summary>Puts a button into the right end of a kit header (fixed width, centred vertically).</summary>
        public static void HeaderButton(RectTransform head, Button b, float right, float width, float height)
        {
            if (b == null) return;
            var rt = (RectTransform)b.transform;
            rt.SetParent(head, false);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(-right, 0f);
            var le = b.GetComponent<LayoutElement>();
            if (le != null) le.ignoreLayout = true;
        }

        /// <summary>The kit's scroll bar on a ScrollRect (thin track, gold thumb, auto-hidden when everything fits).</summary>
        public static void StyleScroll(ScrollRect sr, float width = 14f)
        {
            if (sr == null || S("Scroll_Thumb") == null || sr.verticalScrollbar != null) return;
            var host = (RectTransform)sr.transform;
            var bar = Node(host, "Scrollbar");
            bar.anchorMin = new Vector2(1f, 0f);
            bar.anchorMax = Vector2.one;
            bar.pivot = new Vector2(1f, 0.5f);
            bar.sizeDelta = new Vector2(width, -8f);
            bar.anchoredPosition = new Vector2(-2f, 0f);
            Paint(bar, "Scroll_Track", 1f, new Color(0.55f, 0.4f, 0.25f, 0.55f)).raycastTarget = true;
            var handle = Fill(Fill(bar, "Sliding Area"), "Handle");
            var himg = Paint(handle, "Scroll_Thumb", 1f);
            himg.raycastTarget = true;
            var sb = bar.gameObject.AddComponent<Scrollbar>();
            sb.handleRect = handle;
            sb.targetGraphic = himg;
            sb.direction = Scrollbar.Direction.BottomToTop;
            sr.verticalScrollbar = sb;
            sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        /// <summary>A slider in the kit: dark bar track, gold fill, a gold square handle.</summary>
        public static void StyleSlider(Slider s)
        {
            if (s == null || S("Bar_Track") == null) return;
            var bg = s.transform.Find("Background");
            if (bg != null) Paint(bg, "Bar_Track", 1f).raycastTarget = true;
            if (s.fillRect != null) Paint(s.fillRect, "Bar_Fill_Gold", 1f);
            if (s.handleRect != null)
            {
                var h = Paint(s.handleRect, "Btn_Square_Gold", 2f);
                h.preserveAspect = false;
                s.handleRect.sizeDelta = new Vector2(44f, -12f);
                s.handleRect.anchorMin = new Vector2(s.handleRect.anchorMin.x, 0f);
                s.handleRect.anchorMax = new Vector2(s.handleRect.anchorMax.x, 1f);
            }
            s.transition = Selectable.Transition.ColorTint;
            var c = ColorBlock.defaultColorBlock;
            c.pressedColor = new Color(0.85f, 0.8f, 0.7f, 1f);
            s.colors = c;
        }

        /// <summary>
        /// A HUD meter in the kit (boost, Captain active): the dark bar track behind, the kit's bar fill (still a filled
        /// image, so the fill amount works), the label in the kit font with a dark outline so it reads over the fill.
        /// Returns false without the kit.
        /// </summary>
        public static bool HudBar(Image track, Image fill, string fillSprite, TextMeshProUGUI label)
        {
            var t = S("Bar_Track");
            var f = S(fillSprite);
            if (t == null || f == null) return false;
            var raycast = track.raycastTarget;
            Paint(track, "Bar_Track", 1f);
            track.raycastTarget = raycast;
            track.preserveAspect = false;
            fill.sprite = f;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.preserveAspect = false;
            var frt = fill.rectTransform;
            frt.offsetMin = new Vector2(3f, 3f);
            frt.offsetMax = new Vector2(-3f, -3f);
            if (label != null)
            {
                if (Font != null) label.font = Font;
                label.color = Color.white;
                label.outlineWidth = 0.22f;
                label.outlineColor = new Color32(40, 22, 8, 255);
            }
            return true;
        }

        /// <summary>A text field in the kit (parchment field, ink text).</summary>
        public static void StyleInput(TMP_InputField f)
        {
            if (f == null || S("Input_Field") == null) return;
            Paint(f, "Input_Field", 1.4f).raycastTarget = true;
            if (f.textComponent != null) InkText(f.textComponent, Ink);
            if (f.placeholder is TextMeshProUGUI ph) InkText(ph, new Color(InkDim.r, InkDim.g, InkDim.b, 0.75f));
            if (Font != null) f.fontAsset = Font;
            f.caretColor = Ink;
            f.customCaretColor = true;
            f.selectionColor = new Color(0.95f, 0.72f, 0.25f, 0.45f);
        }

        /// <summary>An old cream/grey dark-panel text colour mapped to the ink for parchment.</summary>
        public static Color InkFor(Color c)
        {
            if (c.g > 0.75f && c.r < 0.6f) return InkGood; // green
            if (c.r > 0.9f && c.g < 0.65f) return InkBad;  // red
            if (c.r > 0.9f && c.g > 0.7f && c.b < 0.55f) return InkGold; // gold
            if (c.r < 0.8f && Mathf.Abs(c.r - c.b) < 0.1f) return InkDim; // grey
            return Ink;
        }

        /// <summary>Rich-text colours written for a dark panel, swapped for parchment ones.</summary>
        public static string InkRich(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("#9aa4ad", DimHex).Replace("#ffd166", GoldHex).Replace("#FFD166", GoldHex)
                .Replace("#73d973", GoodHex).Replace("#ff8c73", BadHex);
        }

        /// <summary>Every text under <paramref name="root"/> in the kit font and ink (colours mapped by <see cref="InkFor"/>).</summary>
        public static void InkAll(Transform root, Func<TextMeshProUGUI, bool> skip = null)
        {
            if (root == null || !Ready) return;
            foreach (var t in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (skip != null && skip(t)) continue;
                if (t.GetComponentInParent<Button>() != null) continue;
                InkText(t, InkFor(t.color));
            }
        }
    }
}
