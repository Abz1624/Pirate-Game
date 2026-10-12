using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Foundation.Data;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// Hackathon art pass (12 Oct): the art team's models set in the shipped juice settings (HackathonArtWiring) are
    /// draw-only prefabs (no missing scripts, colliders, rigidbodies or NavMesh parts; root at scale 1, as the pools and
    /// presenters set the scale) and every enemy entry names an enemy or variant the configs have.
    /// </summary>
    [Category("V4")]
    public sealed class HackathonArtModelsTests
    {
        static DemoJuiceSettings Settings() => AssetDatabase.LoadAssetAtPath<DemoJuiceSettings>("Assets/_Game/Resources/DemoJuiceSettings.asset");

        static IEnumerable<GameObject> ArtModels(DemoJuiceSettings s)
        {
            foreach (var e in s.enemyArtModels) yield return e.model;
            foreach (var w in s.weaponArtModels) { if (w.projectileModel != null) yield return w.projectileModel; if (w.lobModel != null) yield return w.lobModel; }
            yield return s.maceArtModel;
            yield return s.maceChainLinkModel;
            yield return s.lanternArtModel;
            yield return s.parrotArtModel;
            yield return s.monkeyArtModel;
        }

        [Test]
        public void EveryArtModel_IsDrawOnly()
        {
            var s = Settings();
            Assert.IsNotNull(s);
            Assert.GreaterOrEqual(s.enemyArtModels.Length, 15, "13 ships and the two crowned elites");
            foreach (var model in ArtModels(s))
            {
                Assert.IsNotNull(model);
                var parts = model.GetComponentsInChildren<Transform>(true);
                Assert.AreEqual(0, parts.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)), model.name + ": missing scripts");
                Assert.IsEmpty(model.GetComponentsInChildren<Collider>(true), model.name + ": colliders");
                Assert.IsEmpty(model.GetComponentsInChildren<Rigidbody>(true), model.name + ": rigidbodies");
                Assert.AreEqual(Vector3.one, model.transform.localScale, model.name + ": root scale");
                Assert.IsTrue(model.GetComponentsInChildren<Renderer>(true).Length > 0, model.name + ": draws something");
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                    {
                        Assert.IsNotNull(m, model.name + "/" + r.name + ": a missing material");
                        Assert.IsFalse(ShaderUtil.ShaderHasError(m.shader), model.name + "/" + r.name + ": shader " + m.shader.name + " has errors");
                    }
            }
        }

        [Test]
        public void EveryEnemyArtEntry_NamesAnEnemyOrVariant()
        {
            var ids = new HashSet<string>(AssetDatabase.FindAssets("t:EnemyArchetypeConfig").Select(g => AssetDatabase.LoadAssetAtPath<EnemyArchetypeConfig>(AssetDatabase.GUIDToAssetPath(g)).enemyId)
                .Concat(AssetDatabase.FindAssets("t:EnemyVariantConfig").Select(g => AssetDatabase.LoadAssetAtPath<EnemyVariantConfig>(AssetDatabase.GUIDToAssetPath(g)).variantId)));
            var weapons = new HashSet<string>(AssetDatabase.FindAssets("t:WeaponConfig").Select(g => AssetDatabase.LoadAssetAtPath<WeaponConfig>(AssetDatabase.GUIDToAssetPath(g)).weaponId));
            var s = Settings();
            foreach (var e in s.enemyArtModels) Assert.IsTrue(ids.Contains(e.enemyId), e.enemyId);
            foreach (var w in s.weaponArtModels) Assert.IsTrue(weapons.Contains(w.weaponId), w.weaponId);
        }
    }
}
