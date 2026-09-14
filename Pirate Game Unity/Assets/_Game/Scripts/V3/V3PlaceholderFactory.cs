using HighSeas.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HighSeas.V3
{
    public static class V3PlaceholderFactory
    {
        public static readonly Color UiPanel = new(0.08f, 0.12f, 0.18f, 0.92f);
        public static readonly Color UiAccent = new(0.85f, 0.65f, 0.2f, 1f);
        public static readonly Color UiText = new(0.95f, 0.93f, 0.88f, 1f);

        public static GameObject CreatePoiMarker(POIType type, Vector3 position, Transform parent)
        {
            var (id, color, scale, hint) = type switch
            {
                POIType.SalvageChest => ("poi_salvage_chest", new Color(0.75f, 0.55f, 0.15f), new Vector3(1.8f, 1.2f, 1.8f), "Paid run chest — replace with treasure chest model."),
                POIType.LighthouseOfFortune => ("poi_smugglers_oath", new Color(0.9f, 0.85f, 0.5f), new Vector3(1.4f, 2.4f, 1.4f), "Greed shrine — replace with lighthouse/oath shrine."),
                POIType.TrialBuoy => ("poi_signal_buoy", new Color(0.2f, 0.7f, 0.95f), new Vector3(1.2f, 1.6f, 1.2f), "Charge shrine buoy — replace with signal buoy."),
                POIType.BossBeacon => ("poi_treasury_beacon", new Color(0.75f, 0.2f, 0.85f), new Vector3(2.4f, 1.4f, 2.4f), "Treasury Beacon / boss summon — replace with vault beacon."),
                POIType.GildedWreck => ("poi_repair_wreck", new Color(0.55f, 0.4f, 0.3f), new Vector3(2.2f, 1f, 2.2f), "Repair wreck POI."),
                _ => ($"poi_{type.ToString().ToLowerInvariant()}", Color.gray, new Vector3(1.6f, 1.2f, 1.6f), $"POI placeholder for {type}.")
            };

            var marker = CreatePrimitiveMarker("poi", id, position, parent, PrimitiveType.Cylinder, color, scale, hint);
            V3WorldBillboardLabel.Attach(marker, GetPoiDisplayName(type), hint);
            return marker;
        }

        public static GameObject CreateFloorPowerup(V3FloorPowerupKind kind, Vector3 position, Transform parent)
        {
            var (id, color, scale, hint, primitive) = kind switch
            {
                V3FloorPowerupKind.RepairSupplies => (
                    "floor_powerup_repair_supplies",
                    new Color(0.2f, 0.85f, 0.45f),
                    new Vector3(0.9f, 0.5f, 0.9f),
                    "Repair Supplies kit — replace with green supply crate/VFX.",
                    PrimitiveType.Cube),
                V3FloorPowerupKind.BattleSignal => (
                    "floor_powerup_battle_signal",
                    new Color(0.95f, 0.35f, 0.2f),
                    new Vector3(0.75f, 1.1f, 0.75f),
                    "Battle Signal flare — replace with red signal flare mesh/VFX.",
                    PrimitiveType.Cylinder),
                V3FloorPowerupKind.Tailwind => (
                    "floor_powerup_tailwind",
                    new Color(0.35f, 0.75f, 1f),
                    new Vector3(1.1f, 0.35f, 1.1f),
                    "Tailwind gust pickup — replace with wind swirl mesh/VFX.",
                    PrimitiveType.Sphere),
                V3FloorPowerupKind.SalvageChime => (
                    "floor_powerup_salvage_chime",
                    new Color(0.95f, 0.85f, 0.25f),
                    new Vector3(0.7f, 0.9f, 0.7f),
                    "Salvage Chime bell — replace with chime/bell pickup mesh/SFX.",
                    PrimitiveType.Capsule),
                _ => ("floor_powerup_unknown", Color.gray, Vector3.one, "Unknown floor powerup.", PrimitiveType.Sphere)
            };

            var marker = CreatePrimitiveMarker("pickup", id, position, parent, primitive, color, scale, hint);
            V3WorldBillboardLabel.Attach(marker, V3FloorPowerupWorld.GetDisplayName(kind), hint);
            return marker;
        }

        public static string GetPoiDisplayName(POIType type) =>
            type switch
            {
                POIType.SalvageChest => "Salvage Chest",
                POIType.GildedWreck => "Repair Wreck",
                POIType.MerchantSkiff => "Powder Cache",
                POIType.TrialBuoy => "Signal Buoy",
                POIType.CursedIdol => "Treasure Chart",
                POIType.BossBeacon => "Treasury Beacon",
                _ => type.ToString()
            };

        public static GameObject CreateUiPanel(Transform parent, string id, Vector2 size, string hint)
        {
            var go = new GameObject($"PLACEHOLDER_v3_ui_{id}");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            var image = go.AddComponent<Image>();
            image.color = UiPanel;
            var marker = go.AddComponent<PlaceholderMarker>();
            marker.Configure("ui", id, hint);
            return go;
        }

        public static Button CreateUiButton(Transform parent, string id, string label, Vector2 size, string hint)
        {
            var root = CreateUiPanel(parent, id, size, hint);
            var button = root.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = UiAccent;
            colors.highlightedColor = UiAccent * 1.1f;
            colors.pressedColor = UiAccent * 0.85f;
            button.colors = colors;

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(root.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = UiText;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 12;
            text.resizeTextMaxSize = 28;
            return button;
        }

        public static Text CreateUiText(Transform parent, string id, string content, int fontSize, string hint)
        {
            var go = new GameObject($"PLACEHOLDER_v3_ui_{id}");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(600f, 80f);
            var text = go.AddComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = UiText;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var marker = go.AddComponent<PlaceholderMarker>();
            marker.Configure("ui", id, hint);
            return text;
        }

        static GameObject CreatePrimitiveMarker(
            string category,
            string id,
            Vector3 position,
            Transform parent,
            PrimitiveType primitive,
            Color color,
            Vector3 scale,
            string hint)
        {
            var go = GameObject.CreatePrimitive(primitive);
            go.name = $"PLACEHOLDER_v3_{category}_{id}";
            go.transform.SetParent(parent, false);
            go.transform.position = position + Vector3.up * scale.y * 0.5f;
            go.transform.localScale = scale;
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                rend.material.color = color;
            }

            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            var marker = go.AddComponent<PlaceholderMarker>();
            marker.Configure(category, id, hint);
            return go;
        }
    }
}
