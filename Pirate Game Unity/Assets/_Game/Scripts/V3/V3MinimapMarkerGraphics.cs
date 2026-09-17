using HighSeas.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HighSeas.V3
{
    /// <summary>Abstract Megabonk-style minimap glyphs (shape + color, not world meshes).</summary>
    public static class V3MinimapMarkerGraphics
    {
        public enum GlyphKind
        {
            Player,
            SalvageChest,
            BossBeacon,
            GildedWreck,
            MerchantSkiff,
            TrialBuoy,
            CursedIdol,
            LighthouseOfFortune,
            Miniboss,
            Boss,
            GenericPoi
        }

        public static GlyphKind FromPoi(POIType type) =>
            type switch
            {
                POIType.SalvageChest => GlyphKind.SalvageChest,
                POIType.BossBeacon => GlyphKind.BossBeacon,
                POIType.GildedWreck => GlyphKind.GildedWreck,
                POIType.MerchantSkiff => GlyphKind.MerchantSkiff,
                POIType.TrialBuoy => GlyphKind.TrialBuoy,
                POIType.CursedIdol => GlyphKind.CursedIdol,
                POIType.LighthouseOfFortune => GlyphKind.LighthouseOfFortune,
                _ => GlyphKind.GenericPoi
            };

        public static void Apply(RectTransform root, GlyphKind kind)
        {
            ClearChildren(root);
            switch (kind)
            {
                case GlyphKind.Player:
                    AddTriangle(root, 11f, 15f, Color.white, 0f);
                    AddCircle(root, 5f, new Color(1f, 1f, 1f, 0.35f));
                    break;
                case GlyphKind.SalvageChest:
                    AddSquare(root, 9f, new Color(1f, 0.88f, 0.22f), 12f);
                    AddOutlineSquare(root, 11f, new Color(0.72f, 0.52f, 0.08f, 0.95f));
                    break;
                case GlyphKind.BossBeacon:
                    AddDiamond(root, 14f, new Color(0.92f, 0.35f, 1f), 0f);
                    AddDiamond(root, 18f, new Color(0.92f, 0.35f, 1f, 0.35f));
                    break;
                case GlyphKind.GildedWreck:
                    AddBar(root, new Vector2(14f, 6f), new Color(0.62f, 0.45f, 0.32f));
                    break;
                case GlyphKind.MerchantSkiff:
                    AddTriangle(root, 10f, 10f, new Color(1f, 0.58f, 0.18f), 180f);
                    break;
                case GlyphKind.TrialBuoy:
                    AddCircle(root, 8f, new Color(0.25f, 0.82f, 1f));
                    AddCircle(root, 11f, new Color(0.25f, 0.82f, 1f, 0.35f));
                    break;
                case GlyphKind.CursedIdol:
                    AddDiamond(root, 10f, new Color(0.72f, 0.28f, 0.98f), 0f);
                    AddBar(root, new Vector2(3f, 12f), new Color(0.95f, 0.9f, 1f, 0.85f));
                    break;
                case GlyphKind.LighthouseOfFortune:
                    AddDiamond(root, 11f, new Color(0.95f, 0.92f, 0.55f, 0.2f), 0f);
                    AddOutlineDiamond(root, 11f, new Color(0.95f, 0.92f, 0.55f, 0.95f));
                    break;
                case GlyphKind.Miniboss:
                    AddCircle(root, 12f, new Color(1f, 0.48f, 0.12f, 0.25f));
                    AddCircle(root, 8f, new Color(1f, 0.48f, 0.12f));
                    break;
                case GlyphKind.Boss:
                    AddDiamond(root, 12f, new Color(1f, 0.22f, 0.18f), 0f);
                    AddCircle(root, 4f, Color.white);
                    break;
                default:
                    AddCircle(root, 7f, new Color(0.75f, 0.78f, 0.85f));
                    break;
            }
        }

        static void ClearChildren(RectTransform root)
        {
            for (var i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                if (Application.isPlaying) UnityEngine.Object.Destroy(child.gameObject);
                else UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        static Image CreateImage(Transform parent, string name, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.sprite = V3MinimapUiSprites.White;
            return img;
        }

        static void AddSquare(RectTransform parent, float size, Color color, float corner)
        {
            var img = CreateImage(parent, "Square", new Vector2(size, size));
            img.color = color;
            if (corner > 0f)
            {
                var outline = img.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.55f);
                outline.effectDistance = new Vector2(1f, -1f);
            }
        }

        static void AddOutlineSquare(RectTransform parent, float size, Color color)
        {
            var img = CreateImage(parent, "SquareOutline", new Vector2(size, size));
            img.color = color;
            var outline = img.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        static void AddCircle(RectTransform parent, float diameter, Color color)
        {
            var img = CreateImage(parent, "Circle", new Vector2(diameter, diameter));
            img.sprite = V3MinimapUiSprites.Circle;
            img.color = color;
        }

        static void AddDiamond(RectTransform parent, float size, Color color, float zRotation = 0f)
        {
            var img = CreateImage(parent, "Diamond", new Vector2(size, size));
            img.sprite = V3MinimapUiSprites.Diamond;
            img.color = color;
            img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, zRotation);
        }

        static void AddOutlineDiamond(RectTransform parent, float size, Color color)
        {
            var img = CreateImage(parent, "DiamondOutline", new Vector2(size, size));
            img.sprite = V3MinimapUiSprites.Diamond;
            img.color = new Color(color.r, color.g, color.b, 0.15f);
            var outline = img.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        static void AddTriangle(RectTransform parent, float width, float height, Color color, float zRotation)
        {
            var img = CreateImage(parent, "Triangle", new Vector2(width, height));
            img.sprite = V3MinimapUiSprites.Triangle;
            img.color = color;
            img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, zRotation);
        }

        static void AddBar(RectTransform parent, Vector2 size, Color color)
        {
            var img = CreateImage(parent, "Bar", size);
            img.color = color;
        }
    }
}
