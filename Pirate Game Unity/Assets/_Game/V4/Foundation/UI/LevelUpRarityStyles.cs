using RoguePirates.V4.Foundation.Core;
using UnityEngine;

namespace RoguePirates.V4.Foundation.UI
{
    public readonly struct LevelUpRarityStyle
    {
        public readonly Color panelFill;
        public readonly Color panelBorder;
        public readonly Color accent;
        public readonly Color glow;
        public readonly Color title;
        public readonly Color rarityText;

        public LevelUpRarityStyle(Color panelFill, Color panelBorder, Color accent, Color glow, Color title, Color rarityText)
        {
            this.panelFill = panelFill;
            this.panelBorder = panelBorder;
            this.accent = accent;
            this.glow = glow;
            this.title = title;
            this.rarityText = rarityText;
        }
    }

    public static class LevelUpRarityStyles
    {
        public static LevelUpRarityStyle For(Rarity rarity) => rarity switch
        {
            Rarity.Uncommon => new LevelUpRarityStyle(
                new Color(0.1f, 0.18f, 0.14f, 0.96f),
                new Color(0.22f, 0.62f, 0.38f, 1f),
                new Color(0.28f, 0.78f, 0.48f, 1f),
                new Color(0.2f, 0.55f, 0.35f, 0.45f),
                new Color(0.92f, 1f, 0.95f, 1f),
                new Color(0.75f, 1f, 0.85f, 1f)),
            Rarity.Rare => new LevelUpRarityStyle(
                new Color(0.08f, 0.12f, 0.2f, 0.96f),
                new Color(0.22f, 0.45f, 0.82f, 1f),
                new Color(0.35f, 0.62f, 1f, 1f),
                new Color(0.2f, 0.45f, 0.85f, 0.45f),
                new Color(0.92f, 0.96f, 1f, 1f),
                new Color(0.8f, 0.9f, 1f, 1f)),
            Rarity.Epic => new LevelUpRarityStyle(
                new Color(0.14f, 0.08f, 0.2f, 0.96f),
                new Color(0.52f, 0.28f, 0.82f, 1f),
                new Color(0.72f, 0.38f, 1f, 1f),
                new Color(0.45f, 0.2f, 0.75f, 0.5f),
                new Color(0.97f, 0.92f, 1f, 1f),
                new Color(0.9f, 0.78f, 1f, 1f)),
            Rarity.Legendary => new LevelUpRarityStyle(
                new Color(0.2f, 0.12f, 0.04f, 0.96f),
                new Color(0.92f, 0.62f, 0.12f, 1f),
                new Color(1f, 0.78f, 0.22f, 1f),
                new Color(0.9f, 0.55f, 0.1f, 0.55f),
                new Color(1f, 0.96f, 0.86f, 1f),
                new Color(1f, 0.88f, 0.55f, 1f)),
            _ => new LevelUpRarityStyle(
                new Color(0.1f, 0.12f, 0.16f, 0.96f),
                new Color(0.35f, 0.4f, 0.48f, 1f),
                new Color(0.55f, 0.62f, 0.72f, 1f),
                new Color(0.3f, 0.35f, 0.42f, 0.35f),
                new Color(0.95f, 0.97f, 1f, 1f),
                new Color(0.82f, 0.86f, 0.92f, 1f))
        };

        public static Color FrameColor(Rarity rarity) => For(rarity).panelBorder;
    }

    public static class LevelUpRarityColors
    {
        public static Color ForRarity(Rarity rarity) => LevelUpRarityStyles.FrameColor(rarity);
    }
}
