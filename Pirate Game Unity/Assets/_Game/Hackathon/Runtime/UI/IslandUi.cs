using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace RoguePirates.Hackathon.UI
{
    /// <summary>
    /// Hackathon (12 Oct, U-3): shared bits of the islands and market screens: a round marker sprite and the NFT picture
    /// cache. (13 Oct: the old navy rows, amber buttons and RUM bar went; the screens draw with the parchment kit,
    /// <see cref="RoguePirates.UI.UiKit"/>.)
    /// </summary>
    static class IslandUi
    {
        static Sprite _circle;

        /// <summary>A soft-edged white disc (marker and dot art, tinted per use).</summary>
        public static Sprite Circle
        {
            get
            {
                if (_circle != null) return _circle;
                const int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[size * size];
                var r = size * 0.5f - 1f;
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var dx = x + 0.5f - size * 0.5f;
                    var dy = y + 0.5f - size * 0.5f;
                    var a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
                tex.SetPixels32(px);
                tex.Apply();
                _circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
                return _circle;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _circle = null; Pictures.Clear(); }

        // ---------------------------------------------------------------- pictures

        /// <summary>A picture (<paramref name="size"/> square) loaded from <paramref name="url"/> into a row.</summary>
        public static RawImage Picture(Transform row, string url, float size)
        {
            var go = new GameObject("Picture", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement));
            go.transform.SetParent(row, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = size;
            le.preferredHeight = le.minHeight = size;
            le.flexibleWidth = 0f;
            var img = go.GetComponent<RawImage>();
            img.color = new Color(1f, 1f, 1f, 0.08f);
            img.raycastTarget = false;
            _ = LoadInto(img, url);
            return img;
        }

        /// <summary>A picture loaded from <paramref name="url"/> filling <paramref name="rt"/> (hidden until it loads).</summary>
        public static RawImage PictureIn(RectTransform rt, string url)
        {
            var img = rt.gameObject.AddComponent<RawImage>();
            img.color = new Color(1f, 1f, 1f, 0f);
            img.raycastTarget = false;
            _ = LoadInto(img, url);
            return img;
        }

        static readonly Dictionary<string, Texture2D> Pictures = new Dictionary<string, Texture2D>();

        static async Task LoadInto(RawImage img, string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            if (!Pictures.TryGetValue(url, out var tex) || tex == null)
            {
                using (var req = UnityWebRequestTexture.GetTexture(url))
                {
                    try { await req.SendWebRequest(); }
                    catch (Exception) { /* read below */ }
                    if (req.result != UnityWebRequest.Result.Success) { Debug.LogWarning("IslandUi picture " + url + ": " + req.error); return; }
                    tex = DownloadHandlerTexture.GetContent(req);
                    Pictures[url] = tex;
                }
            }
            if (img == null) return;
            img.texture = tex;
            img.color = Color.white;
        }

        /// <summary>An address shortened for a label ("5SBg...jH9A").</summary>
        public static string Short(string address) => string.IsNullOrEmpty(address) ? "-" : Web3Rules.ShortAddress(address);
    }
}
