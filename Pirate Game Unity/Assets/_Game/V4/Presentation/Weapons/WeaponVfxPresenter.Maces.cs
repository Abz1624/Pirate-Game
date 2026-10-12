using System.Collections.Generic;
using RoguePirates.V4.Weapons;
using UnityEngine;

namespace RoguePirates.V4.Presentation.Weapons
{
    /// <summary>
    /// Hackathon (12 Oct, owner: "a chained cannonball that is thrown at an enemy ... a mace that hits the enemy and comes
    /// back but has a chain attached"): each chained mace in flight (the simulation's <see cref="IWeaponManager.Maces"/>)
    /// is drawn as a pooled spiked iron ball where the simulation put it, spinning, with a sagging chain (a pooled line
    /// renderer with link-like dashes) from the ship to the ball. Reads the views only; draws no random numbers.
    /// </summary>
    public sealed partial class WeaponVfxPresenter
    {
        const string KindMace = "mace";
        const int ChainPoints = 12;
        const float ChainWidth = 0.26f;
        static readonly Color ChainColor = new Color(0.16f, 0.16f, 0.19f, 1f);
        static Material _chainMaterial;

        readonly List<View> _maceViews = new List<View>();
        readonly List<LineRenderer> _chains = new List<LineRenderer>();

        /// <summary>Maces drawn this frame (tests).</summary>
        public int MaceCount => _maceViews.Count;
        /// <summary>A drawn mace's ball position (tests).</summary>
        public Vector3 MacePosition(int index) => _maceViews[index].Go != null ? _maceViews[index].Go.transform.position : Vector3.zero;
        /// <summary>A drawn chain's end points (tests): the ship end and the ball end.</summary>
        public (Vector3 ship, Vector3 ball) ChainEnds(int index) => (_chains[index].GetPosition(0), _chains[index].GetPosition(ChainPoints - 1));

        /// <summary>Pools the mace model (one per mace an attack can throw, and two spare).</summary>
        void PrewarmMaces(int count)
        {
            if (_pools == null) return;
            EnsurePool(KindMace, WeaponPlaceholderModels.Mace(_pools.Staging), count);
        }

        public void SyncMaces()
        {
            var maces = _weapons != null ? _weapons.Maces : null;
            var count = maces != null && (_ship == null || _ship.Exists) ? maces.Count : 0;
            while (_maceViews.Count > count)
            {
                ReleaseView(_maceViews[_maceViews.Count - 1]);
                _maceViews.RemoveAt(_maceViews.Count - 1);
            }
            if (count > 0)
            {
                var model = WeaponPlaceholderModels.Mace(_pools != null ? _pools.Staging : null);
                while (_maceViews.Count < count)
                {
                    var go = Spawn(KindMace, model, maces[_maceViews.Count].Position, Quaternion.identity, 1.25f, out var pooled);
                    if (go == null) break;
                    var prep = Prepare(go);
                    if (!prep.Done) { StripPhysics(go); prep.Done = true; }
                    _maceViews.Add(new View { Go = go, Pooled = pooled });
                }
            }
            while (_chains.Count < _maceViews.Count) _chains.Add(CreateChain(_chains.Count));

            var shipPos = _ship != null && _ship.Exists ? _ship.Position : Vector3.zero;
            var spin = Quaternion.Euler(Time.time * 720f, Time.time * 260f, 0f);
            for (var i = 0; i < _chains.Count; i++)
            {
                var line = _chains[i];
                var visible = i < _maceViews.Count && _maceViews[i].Go != null;
                if (line.gameObject.activeSelf != visible) line.gameObject.SetActive(visible);
                if (!visible) continue;
                var ball = maces[i].Position;
                _maceViews[i].Go.transform.SetPositionAndRotation(ball, spin);
                DrawChain(line, shipPos + Vector3.up * 1.6f, ball);
            }
        }

        /// <summary>A chain from the ship's rail to the ball, sagging a little toward the water in the middle.</summary>
        static void DrawChain(LineRenderer line, Vector3 from, Vector3 to)
        {
            var length = Vector3.Distance(from, to);
            var sag = Mathf.Min(0.45f, 0.04f * length);
            for (var p = 0; p < ChainPoints; p++)
            {
                var k = p / (ChainPoints - 1f);
                line.SetPosition(p, Vector3.Lerp(from, to, k) + Vector3.down * (sag * 4f * k * (1f - k)));
            }
            // Links: the texture repeats once per 0.35 m of chain.
            line.textureScale = new Vector2(Mathf.Max(1f, length / 0.35f), 1f);
        }

        LineRenderer CreateChain(int index)
        {
            var go = new GameObject("MaceChain" + index);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = ChainPoints;
            line.widthMultiplier = ChainWidth;
            line.textureMode = LineTextureMode.Tile;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.startColor = line.endColor = ChainColor;
            line.sharedMaterial = ChainMaterial();
            go.SetActive(false);
            return line;
        }

        /// <summary>One shared material: dark iron links on a transparent strip (a small generated texture).</summary>
        static Material ChainMaterial()
        {
            if (_chainMaterial != null) return _chainMaterial;
            var tex = new Texture2D(16, 4, TextureFormat.RGBA32, false) { name = "MaceChainLinks", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            for (var x = 0; x < 16; x++)
            for (var y = 0; y < 4; y++)
            {
                // An oval link ring (bright edge, hollow middle) then a short gap to the next.
                var linkX = x < 13;
                var edge = y == 0 || y == 3 || x == 0 || x == 12;
                var a = linkX ? (edge ? 1f : 0.35f) : (y == 1 || y == 2 ? 1f : 0f);
                var shade = edge ? 1f : 0.75f;
                tex.SetPixel(x, y, new Color(shade, shade, shade, a));
            }
            tex.Apply();
            _chainMaterial = new Material(Shader.Find("Sprites/Default")) { name = "MaceChain", hideFlags = HideFlags.DontSave, mainTexture = tex };
            return _chainMaterial;
        }

        void ClearMaces()
        {
            foreach (var view in _maceViews) ReleaseView(view);
            _maceViews.Clear();
            foreach (var line in _chains) if (line != null) line.gameObject.SetActive(false);
        }
    }
}
