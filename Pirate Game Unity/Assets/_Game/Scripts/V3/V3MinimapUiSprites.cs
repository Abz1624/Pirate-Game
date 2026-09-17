using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>Procedural UI sprites for minimap glyphs and circular masking.</summary>
    public static class V3MinimapUiSprites
    {
        static Sprite _white;
        static Sprite _circle;
        static Sprite _diamond;
        static Sprite _triangle;
        static Sprite _softFog;

        public static Sprite White => _white ??= CreateSolid(4, 4);
        public static Sprite Circle => _circle ??= CreateCircle(64);
        public static Sprite Diamond => _diamond ??= CreateDiamond(64);
        public static Sprite Triangle => _triangle ??= CreateTriangle(64, 64);
        public static Sprite SoftFog => _softFog ??= CreateSoftFogDisc(64);

        static Sprite CreateSolid(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite CreateCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = size * 0.5f;
            var radius = size * 0.5f - 1f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    var a = dist <= radius ? 1f : 0f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite CreateDiamond(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = size * 0.5f;
            var half = size * 0.42f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = Mathf.Abs(x - center);
                    var dy = Mathf.Abs(y - center);
                    var inside = (dx / half) + (dy / half) <= 1f;
                    tex.SetPixel(x, y, inside ? Color.white : new Color(0f, 0f, 0f, 0f));
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite CreateTriangle(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var nx = x / (float)(width - 1);
                    var ny = y / (float)(height - 1);
                    var leftEdge = 0.5f - ny * 0.5f;
                    var rightEdge = 0.5f + ny * 0.5f;
                    var inside = ny <= 0.98f && nx >= leftEdge && nx <= rightEdge;
                    tex.SetPixel(x, y, inside ? Color.white : new Color(0f, 0f, 0f, 0f));
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite CreateSoftFogDisc(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = size * 0.5f;
            var radius = size * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / radius;
                    var a = Mathf.Clamp01(1f - dist);
                    a = a * a * (3f - 2f * a);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
