using System.Collections.Generic;
using RoguePirates.V4.Weapons;
using UnityEngine;
using UnityEngine.Rendering;

namespace RoguePirates.V4.Presentation.Weapons
{
    /// <summary>
    /// Hackathon (12 Oct, owner: "a chained cannonball that is thrown at an enemy ... a mace that hits the enemy and comes
    /// back but has a chain attached"): each chained mace in flight (the simulation's <see cref="IWeaponManager.Maces"/>)
    /// is drawn as a pooled spiked iron ball where the simulation put it, spinning, with a sagging chain (a pooled line
    /// renderer with link-like dashes) from the ship to the ball. Reads the views only; draws no random numbers.
    /// <para>Hackathon art pass (12 Oct): with the art team's anchor and chain link set in the juice settings
    /// (<c>maceArtModel</c>, <c>maceChainLinkModel</c>), the mace is their anchor (flukes first, rolling about the chain)
    /// and the chain is their iron links, drawn instanced along the same sagging curve (no object per link); the line
    /// renderer then only holds the curve.</para>
    /// </summary>
    public sealed partial class WeaponVfxPresenter
    {
        const string KindMace = "mace";
        const int ChainPoints = 12;
        const float ChainWidth = 0.26f;
        const int MaxLinksPerChain = 80;
        const float LinkOverlap = 1.3f;
        static readonly Color ChainColor = new Color(0.16f, 0.16f, 0.19f, 1f);
        static Material _chainMaterial;

        readonly List<View> _maceViews = new List<View>();
        readonly List<Transform> _maceAttach = new List<Transform>();
        readonly List<LineRenderer> _chains = new List<LineRenderer>();

        // The art team's chain links (instanced): one mesh, one instancing copy of its material made at bind.
        Mesh _linkMesh;
        Material _linkMaterial;
        Matrix4x4 _linkLocal = Matrix4x4.identity;
        Matrix4x4[] _linkMatrices;
        GameObject _linkSource;

        /// <summary>Maces drawn this frame (tests).</summary>
        public int MaceCount => _maceViews.Count;
        /// <summary>A drawn mace's ball position (tests).</summary>
        public Vector3 MacePosition(int index) => _maceViews[index].Go != null ? _maceViews[index].Go.transform.position : Vector3.zero;
        /// <summary>Chains made so far (tests: prewarmed at bind).</summary>
        public int ChainCount => _chains.Count;
        /// <summary>A drawn chain's end points (tests): the ship end and the ball end.</summary>
        public (Vector3 ship, Vector3 ball) ChainEnds(int index) => (_chains[index].GetPosition(0), _chains[index].GetPosition(ChainPoints - 1));
        /// <summary>Chain links drawn last frame (tests; 0 without the art team's links).</summary>
        public int ChainLinksDrawn { get; private set; }

        // Hackathon (13 Oct): the mace weapon whose prefabs (anchor, chain link, chain line) are drawn.
        string _maceWeapon;

        GameObject MaceModel()
        {
            var weapon = _maceWeapon ?? (Visuals != null ? Equipped(Config.ConfigEnums.WeaponBehavior.ChainedMace) : null);
            return Pick(weapon, WeaponVisualSlot.Mace, WeaponPlaceholderModels.Mace(_pools != null ? _pools.Staging : null, _settings));
        }

        /// <summary>The art team's anchor (or a copy of it): it has the ChainAttach child the chain meets.</summary>
        static bool IsArtMace(GameObject model) => model != null && model.transform.Find("ChainAttach") != null;

        /// <summary>Pools the mace model and makes the chains (one per mace an attack can throw, and two spare) at bind,
        /// so a throw creates no object while the run plays.</summary>
        void PrewarmMaces(string weaponId, int count)
        {
            _maceWeapon = weaponId;
            while (_chains.Count < count) _chains.Add(CreateChain(_chains.Count));
            PrepareLinks(count);
            if (_pools == null) return;
            EnsurePool(KindMace, MaceModel(), count);
        }

        /// <summary>The art team's link mesh and an instancing copy of its material (once per link model).</summary>
        void PrepareLinks(int chains)
        {
            var source = Pick(_maceWeapon, WeaponVisualSlot.ChainLink, _settings != null ? _settings.maceChainLinkModel : null);
            if (source == null || !SystemInfo.supportsInstancing) { _linkMesh = null; return; }
            if (_linkSource != source)
            {
                _linkSource = source;
                var filter = source.GetComponentInChildren<MeshFilter>(true);
                var renderer = filter != null ? filter.GetComponent<MeshRenderer>() : null;
                _linkMesh = filter != null ? filter.sharedMesh : null;
                if (_linkMaterial != null) Kill(_linkMaterial);
                _linkMaterial = null;
                if (_linkMesh == null || renderer == null || renderer.sharedMaterial == null) { _linkMesh = null; return; }
                _linkMaterial = new Material(renderer.sharedMaterial) { name = renderer.sharedMaterial.name + " (instanced)", hideFlags = HideFlags.DontSave, enableInstancing = true };
                _linkLocal = source.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            }
            var need = Mathf.Max(1, chains) * MaxLinksPerChain;
            if (_linkMatrices == null || _linkMatrices.Length < need) _linkMatrices = new Matrix4x4[Mathf.Min(need, 1023)];
        }

        public void SyncMaces()
        {
            var maces = _weapons != null ? _weapons.Maces : null;
            var count = maces != null && (_ship == null || _ship.Exists) ? maces.Count : 0;
            while (_maceViews.Count > count)
            {
                ReleaseView(_maceViews[_maceViews.Count - 1]);
                _maceViews.RemoveAt(_maceViews.Count - 1);
                _maceAttach.RemoveAt(_maceAttach.Count - 1);
            }
            var model = MaceModel();
            if (count > 0)
            {
                var art = IsArtMace(model);
                while (_maceViews.Count < count)
                {
                    var go = Spawn(KindMace, model, maces[_maceViews.Count].Position, Quaternion.identity, art ? 1f : 1.25f, out var pooled);
                    if (go == null) break;
                    var prep = Prepare(go);
                    if (!prep.Done) { StripPhysics(go); prep.Done = true; }
                    _maceViews.Add(new View { Go = go, Pooled = pooled });
                    _maceAttach.Add(art ? go.transform.Find("ChainAttach") : null);
                }
            }
            while (_chains.Count < _maceViews.Count) _chains.Add(CreateChain(_chains.Count)); // only past the prewarmed ones

            var shipPos = _ship != null && _ship.Exists ? _ship.Position : Vector3.zero;
            var rail = shipPos + Vector3.up * 1.6f;
            var links = _linkMesh != null && _linkMaterial != null && _linkMatrices != null;
            var spinMult = WeaponVisualTuning.Spin(model);
            var spin = Quaternion.Euler(Time.time * 720f * spinMult, Time.time * 260f * spinMult, 0f);
            var roll = Quaternion.Euler(0f, 0f, Time.time * 420f * spinMult);
            var drawn = 0;
            for (var i = 0; i < _chains.Count; i++)
            {
                var line = _chains[i];
                var visible = i < _maceViews.Count && _maceViews[i].Go != null;
                if (line.gameObject.activeSelf != visible) line.gameObject.SetActive(visible);
                if (!visible) continue;
                var ball = maces[i].Position;
                var view = _maceViews[i].Go.transform;
                if (_maceAttach[i] != null)
                {
                    // The anchor flies flukes first, away from the ship, rolling about the chain.
                    var away = ball - rail;
                    away.y *= 0.35f;
                    var heading = away.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(away.normalized, Vector3.up) : Quaternion.identity;
                    view.SetPositionAndRotation(ball, heading * roll);
                    DrawChain(line, rail, _maceAttach[i].position);
                }
                else
                {
                    view.SetPositionAndRotation(ball, spin);
                    DrawChain(line, rail, ball);
                }
                line.enabled = !links;
                if (links) drawn = LayLinks(line, drawn);
            }
            ChainLinksDrawn = drawn;
            if (drawn > 0)
            {
                var rp = new RenderParams(_linkMaterial) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false, layer = gameObject.layer };
                Graphics.RenderMeshInstanced(rp, _linkMesh, 0, _linkMatrices, drawn);
            }
        }

        /// <summary>Links along a drawn chain's curve, every <c>maceChainLinkSpacing</c> metres, each turned a quarter
        /// about the chain from the last (as real links hang); returns the running link count.</summary>
        int LayLinks(LineRenderer line, int drawn)
        {
            var spacing = Mathf.Max(0.05f, _settings.maceChainLinkSpacing);
            var linkScale = spacing * LinkOverlap * WeaponVisualTuning.Scale(_linkSource);
            var next = spacing * 0.5f; // where the next link goes, along the current segment
            var n = 0;
            for (var p = 1; p < ChainPoints && drawn < _linkMatrices.Length && n < MaxLinksPerChain; p++)
            {
                var a = line.GetPosition(p - 1);
                var b = line.GetPosition(p);
                var segment = b - a;
                var length = segment.magnitude;
                if (length < 0.0001f) continue;
                var dir = segment / length;
                var look = Quaternion.LookRotation(dir, Vector3.up);
                var d = next;
                for (; d < length && drawn < _linkMatrices.Length && n < MaxLinksPerChain; d += spacing)
                {
                    var twist = (n & 1) == 0 ? look : look * Quaternion.Euler(0f, 0f, 90f);
                    _linkMatrices[drawn++] = Matrix4x4.TRS(a + dir * d, twist, Vector3.one * linkScale) * _linkLocal;
                    n++;
                }
                next = d - length;
            }
            return drawn;
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
            // Hackathon (13 Oct): the mace weapon's chain prefab (a LineRenderer at its root), else code-built.
            var prefab = Pick(_maceWeapon, WeaponVisualSlot.Chain, null);
            if (prefab != null && prefab.GetComponent<LineRenderer>() != null)
            {
                var own = Instantiate(prefab, transform, false);
                own.name = "MaceChain" + index;
                var ownLine = own.GetComponent<LineRenderer>();
                ownLine.useWorldSpace = true;
                ownLine.positionCount = ChainPoints;
                own.SetActive(false);
                return ownLine;
            }
            var go = new GameObject("MaceChain" + index);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            ConfigureChainLine(line);
            line.sharedMaterial = ChainMaterial();
            go.SetActive(false);
            return line;
        }

        /// <summary>The code-built chain line's settings (the chain prefab is built with these, HackathonWeaponPrefabs).</summary>
        public static void ConfigureChainLine(LineRenderer line)
        {
            line.useWorldSpace = true;
            line.positionCount = ChainPoints;
            line.widthMultiplier = ChainWidth;
            line.textureMode = LineTextureMode.Tile;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.startColor = line.endColor = ChainColor;
        }

        /// <summary>One shared material: dark iron links on a transparent strip (a small generated texture).</summary>
        static Material ChainMaterial()
        {
            if (_chainMaterial != null) return _chainMaterial;
            _chainMaterial = new Material(Shader.Find("Sprites/Default")) { name = "MaceChain", hideFlags = HideFlags.DontSave, mainTexture = ChainLinkTexture() };
            return _chainMaterial;
        }

        /// <summary>The chain's link texture (16 x 4: an oval link ring, then a short gap to the next).</summary>
        public static Texture2D ChainLinkTexture()
        {
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
            return tex;
        }

        void ClearMaces()
        {
            foreach (var view in _maceViews) ReleaseView(view);
            _maceViews.Clear();
            _maceAttach.Clear();
            foreach (var line in _chains) if (line != null) line.gameObject.SetActive(false);
            ChainLinksDrawn = 0;
        }
    }
}
