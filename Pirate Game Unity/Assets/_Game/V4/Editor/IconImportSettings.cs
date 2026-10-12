using UnityEditor;

namespace RoguePirates.V4.Editor
{
    /// <summary>
    /// Hackathon (13 Oct): the art team's UI icons under <see cref="Folder"/> import as UI sprites: alpha is transparency, no
    /// mipmaps, at most 512 px and compressed, so the WebGL download stays small (the source PNGs are about 1000 px).
    /// The files are named by content id; Tools > Rogue Pirates > Demo > Wire Icons puts them in V4IconCatalog.
    /// </summary>
    public sealed class IconImportSettings : AssetPostprocessor
    {
        public const string Folder = "Assets/_Game/V4/Demo/Art/Icons/";
        public const int MaxSize = 512;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.maxTextureSize = MaxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = false;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.filterMode = UnityEngine.FilterMode.Bilinear;
        }
    }
}
