using System.Collections.Generic;
using NUnit.Framework;
using RoguePirates.V4.Presentation.Effects;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M3d review #10: the enemy view tint shares one copy per (material, tint), takes a reused view back to its own
    /// materials, is not fooled by a per-view instance (the burn flash), and destroys its copies when cleared.
    /// </summary>
    public sealed class V5EnemyViewTintTests
    {
        readonly List<Object> _made = new List<Object>();
        Material _material;

        [SetUp]
        public void SetUp()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Color");
            _material = new Material(shader) { name = "hull" };
            if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", Color.white);
            else _material.color = Color.white;
            _made.Add(_material);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        Transform View()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = _material;
            _made.Add(go);
            return go.transform;
        }

        static Color ColorOf(Transform view)
        {
            var m = view.GetComponent<Renderer>().sharedMaterial;
            return m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
        }

        static void AreClose(Color expected, Color actual)
        {
            Assert.AreEqual(expected.r, actual.r, 1e-3f);
            Assert.AreEqual(expected.g, actual.g, 1e-3f);
            Assert.AreEqual(expected.b, actual.b, 1e-3f);
        }

        [Test]
        public void ViewsOfATint_ShareOneCopy_AndAReusedViewGoesBackToItsOwnMaterial()
        {
            var tint = new EnemyViewTint();
            var red = new Color(1f, 0.4f, 0.4f, 1f);
            var a = View();
            var b = View();
            foreach (var v in new[] { a, b }) { tint.Record(v); tint.Apply(v, red); }
            Assert.AreSame(a.GetComponent<Renderer>().sharedMaterial, b.GetComponent<Renderer>().sharedMaterial, "one shared copy per tint");
            Assert.AreNotSame(_material, a.GetComponent<Renderer>().sharedMaterial);
            AreClose(red, ColorOf(a));
            Assert.AreEqual(1, tint.CopyCount);

            tint.Record(a); // a reused view: recorded once only
            tint.Apply(a, Color.white);
            Assert.AreSame(_material, a.GetComponent<Renderer>().sharedMaterial, "an untinted id gets the model's own material back");
            tint.Apply(a, red);
            Assert.AreEqual(1, tint.CopyCount, "no new copy for a tint seen before");
            tint.Clear();
        }

        [Test]
        public void APerViewInstance_IsNotTakenForTheOriginal_AndIsDestroyedWhenTheTintChanges()
        {
            var tint = new EnemyViewTint();
            var view = View();
            tint.Record(view);
            tint.Apply(view, Color.white);
            var renderer = view.GetComponent<Renderer>();
            var instance = new Material(_material) { name = "burn instance" }; // what the burn flash's renderer.material leaves
            renderer.sharedMaterial = instance;
            var blue = new Color(0.5f, 0.6f, 1f, 1f);
            tint.Apply(view, blue);
            Assert.IsTrue(instance == null, "the per-view instance is destroyed, not kept or copied");
            AreClose(blue, ColorOf(view));
            Assert.AreEqual(1, tint.CopyCount, "the copy is of the model's own material");
            tint.Apply(view, Color.white);
            Assert.AreSame(_material, renderer.sharedMaterial);
        }

        [Test]
        public void AnUnchangedTint_LeavesTheViewsMaterialsAlone()
        {
            var tint = new EnemyViewTint();
            var view = View();
            var red = new Color(1f, 0.4f, 0.4f, 1f);
            tint.Record(view);
            tint.Apply(view, red);
            var renderer = view.GetComponent<Renderer>();
            var burning = new Material(renderer.sharedMaterial) { name = "burn instance" };
            _made.Add(burning);
            renderer.sharedMaterial = burning;
            tint.Apply(view, red); // the same id again: the burn flash restores its own colours
            Assert.AreSame(burning, renderer.sharedMaterial);
        }

        [Test]
        public void Clear_DestroysTheCopies()
        {
            var tint = new EnemyViewTint();
            var view = View();
            tint.Record(view);
            tint.Apply(view, new Color(0.5f, 1f, 0.5f, 1f));
            var copy = view.GetComponent<Renderer>().sharedMaterial;
            tint.Clear();
            Assert.IsTrue(copy == null);
            Assert.AreEqual(0, tint.CopyCount);
        }
    }
}
