using UnityEngine;

namespace RoguePirates.V4.Presentation.Menu
{
    /// <summary>
    /// The art team's menu (demo, 30 Sep 2026): the main menu and Captain select panels and their 3D backdrops. When
    /// Resources/V4ArtMenuSettings has both panels, the V4 menu shows them instead of the generated Captain carousel.
    /// Built by Tools ▸ Rogue Pirates ▸ Demo ▸ Wire Art Team Assets.
    /// </summary>
    [CreateAssetMenu(fileName = "V4ArtMenuSettings", menuName = "RoguePirates/V4 Art Menu Settings")]
    public sealed class ArtMenuSettings : ScriptableObject
    {
        public const string ResourcePath = "V4ArtMenuSettings";

        public GameObject mainMenuPanel;
        public GameObject characterSelectionPanel;
        [Tooltip("3D scene behind the main menu (islands, cursed brig); its camera replaces the menu camera.")]
        public GameObject mainMenuBackdrop;
        [Tooltip("3D scene behind Captain select (the Captains' ships).")]
        public GameObject characterSelectBackdrop;
        [Tooltip("The Set Sail button art without its baked-in text (used for UNLOCK and LOCKED).")]
        public Sprite setSailNoText;
        public TMPro.TMP_FontAsset font;

        public bool Usable => mainMenuPanel != null && characterSelectionPanel != null;

        public static ArtMenuSettings Load() => Resources.Load<ArtMenuSettings>(ResourcePath);
    }
}
