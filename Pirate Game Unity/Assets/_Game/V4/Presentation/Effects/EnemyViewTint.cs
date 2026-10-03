using System.Collections.Generic;
using UnityEngine;

namespace RoguePirates.V4.Presentation.Effects
{
    /// <summary>
    /// V5 M3d (owner, 1 Oct 2026: an enemy that looks the same behaves the same): tints a pooled enemy view for its enemy
    /// id. Views of one model are shared by the ids that use it, so each acquire sets the renderers' shared materials to
    /// the model's own (white) or to one tinted copy per (material, tint): every view of a tint shares a material, which
    /// keeps the SRP Batcher's batches (a property block or a per-view material would not). Owned by
    /// <see cref="EnemyViewPresenter"/>: a view's renderers and their own materials are recorded when it is first taken
    /// from its pool, before anything (the burn flash, an effect parented to it) can change them, and only those renderers
    /// are tinted later; a view keeps its materials while its tint is unchanged, and a per-view instance left on it is
    /// destroyed when the tint changes. Nothing allocates per acquire once every (renderer, tint) pair has been seen.
    /// <para>Playtest (3 Oct, owner: "most of the ships were visually the same"): a look has two colours. The tint
    /// multiplies the model's lit materials (the hull, as before); the accent colours what reads from above: the flags
    /// (their colour × the accent) and the sails, whose shader has no colour of its own (the accent goes into their
    /// emission, so a sail glows in its ship type's colour).</para>
    /// </summary>
    public sealed class EnemyViewTint
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColor = Shader.PropertyToID("_Color");
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int SailTexture = Shader.PropertyToID("_BaseTexture");
        /// <summary>How strongly a sail glows in its accent colour (added to its lit cloth).</summary>
        public const float SailGlow = 0.42f;

        readonly Dictionary<Transform, Renderer[]> _views = new Dictionary<Transform, Renderer[]>();
        readonly Dictionary<Renderer, Material[]> _originals = new Dictionary<Renderer, Material[]>();
        readonly List<Material> _shared = new List<Material>();
        readonly Dictionary<Renderer, (Color, Color)> _applied = new Dictionary<Renderer, (Color, Color)>();
        readonly Dictionary<(Renderer, Color, Color), Material[]> _arrays = new Dictionary<(Renderer, Color, Color), Material[]>();
        readonly Dictionary<(Material, Color, Color), Material> _copies = new Dictionary<(Material, Color, Color), Material>();
        readonly HashSet<Material> _owned = new HashSet<Material>();
        readonly List<Renderer> _renderers = new List<Renderer>();

        /// <summary>Tinted material copies made so far (tests).</summary>
        public int CopyCount => _copies.Count;

        /// <summary>Records a view's renderers and their own materials the first time it is seen (later calls do nothing).</summary>
        public void Record(Transform view)
        {
            if (view == null || _views.ContainsKey(view)) return;
            view.GetComponentsInChildren(true, _renderers);
            _renderers.RemoveAll(r => !Tintable(r));
            _views[view] = _renderers.ToArray();
            for (var i = 0; i < _renderers.Count; i++)
            {
                var r = _renderers[i];
                if (_originals.ContainsKey(r)) continue;
                var own = r.sharedMaterials;
                _originals[r] = own;
                _applied[r] = (Color.white, Color.white);
                _arrays[(r, Color.white, Color.white)] = own;
                foreach (var m in own) if (m != null) _owned.Add(m);
            }
            _renderers.Clear();
        }

        /// <summary>The tint on everything (the hull and the flags); the sails as authored.</summary>
        public void Apply(Transform view, Color tint) => Apply(view, tint, tint);

        /// <summary>Playtest (3 Oct, owner): the hull × <paramref name="tint"/>, the flags × <paramref name="accent"/> and
        /// the sails glowing in <paramref name="accent"/> (white = as authored).</summary>
        public void Apply(Transform view, Color tint, Color accent)
        {
            if (view == null || !_views.TryGetValue(view, out var renderers)) return;
            var look = (tint, accent);
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !_originals.TryGetValue(r, out var own)) continue;
                if (_applied.TryGetValue(r, out var current) && current == look) continue;
                DestroyForeign(r);
                if (!_arrays.TryGetValue((r, tint, accent), out var materials))
                {
                    materials = new Material[own.Length];
                    for (var m = 0; m < own.Length; m++) materials[m] = Copy(own[m], tint, accent);
                    _arrays[(r, tint, accent)] = materials;
                }
                r.sharedMaterials = materials;
                _applied[r] = look;
            }
        }

        /// <summary>A per-view instance someone else put on the renderer (the burn flash's <c>renderer.material</c>).</summary>
        void DestroyForeign(Renderer r)
        {
            r.GetSharedMaterials(_shared);
            for (var i = 0; i < _shared.Count; i++)
            {
                var m = _shared[i];
                if (m == null || _owned.Contains(m)) continue;
                if (Application.isPlaying) Object.Destroy(m); else Object.DestroyImmediate(m);
            }
        }

        Material Copy(Material source, Color tint, Color accent)
        {
            if (source == null) return source;
            var sail = IsSail(source);
            var hull = source.HasProperty(BaseColor);
            // The colour this material takes: the hull's tint, else the accent (flags and sails).
            var colour = hull ? tint : accent;
            if (colour == Color.white) return source;
            if (_copies.TryGetValue((source, tint, accent), out var copy) && copy != null) return copy;
            copy = new Material(source) { name = source.name + " (tint)", hideFlags = HideFlags.DontSave };
            if (sail) copy.SetColor(EmissionColor, source.GetColor(EmissionColor) + accent * SailGlow);
            else if (hull) copy.SetColor(BaseColor, source.GetColor(BaseColor) * tint);
            else if (copy.HasProperty(LegacyColor)) copy.SetColor(LegacyColor, source.GetColor(LegacyColor) * accent);
            _copies[(source, tint, accent)] = copy;
            _owned.Add(copy);
            return copy;
        }

        /// <summary>The enemy sail shader: a cloth texture and an emission colour, no base colour.</summary>
        static bool IsSail(Material m) => !m.HasProperty(BaseColor) && !m.HasProperty(LegacyColor) && m.HasProperty(SailTexture) && m.HasProperty(EmissionColor);

        static bool Tintable(Renderer r) => r != null && !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer);

        /// <summary>Destroys the tinted copies and forgets every view (the presenter's end; the pooled views go with the scene).</summary>
        public void Clear()
        {
            foreach (var m in _copies.Values)
                if (m != null) { if (Application.isPlaying) Object.Destroy(m); else Object.DestroyImmediate(m); }
            _copies.Clear();
            _views.Clear();
            _originals.Clear();
            _applied.Clear();
            _arrays.Clear();
            _owned.Clear();
        }
    }
}
