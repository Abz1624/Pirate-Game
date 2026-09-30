using System.Collections;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M3 in the real run scene: the evolution chain through the game's own wiring — a weapon raised to level 8, a
    /// chest (the stage's economy, with the build's items wired in) giving its evolution item, then the evolution card
    /// dealt by the build's progression, taken, and the evolved weapon levelling on.
    /// </summary>
    [Category("V5")]
    public sealed class V5BuildPlayModeTests
    {
        V4TestSaveIsolation _isolation;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _isolation = new V4TestSaveIsolation();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SceneManager.LoadSceneAsync(V4SceneNames.Boot, LoadSceneMode.Single);
            yield return null;
            StageBalanceProfileContext.ResetForTests();
            _isolation?.Dispose();
            _isolation = null;
        }

        static IEnumerator WaitForPlaying()
        {
            var deadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (RunManager.Instance != null && RunManager.Instance.State == RunState.Playing && StageBalanceProfileContext.IsActive) yield break;
                yield return null;
            }
            Assert.Fail("V4 run did not reach Playing.");
        }

        [UnityTest]
        public IEnumerator EvolutionChain_Level8_ChestItem_Card_ThenLevellingOn()
        {
            SceneManager.LoadScene(V4SceneNames.SaltwindRun, LoadSceneMode.Single);
            yield return null;
            yield return WaitForPlaying();
            var game = Object.FindFirstObjectByType<V4Game>();
            Object.FindFirstObjectByType<PlayerShipController>().Damage.SetPlaytestInvulnerable(true);
            var progression = game.Build.Progression;
            var evo = game.Config.All<WeaponEvolutionConfig>().First(e => e.baseWeapon != null && game.Config.All<ItemConfig>().Any(i => i.itemId == e.requiredItemId && i.inChestPool));
            var weaponId = evo.baseWeapon.weaponId;
            progression.DevSetWeapons(new[] { weaponId });
            var index = progression.Loadout.IndexOf(weaponId);
            while (progression.WeaponLevel(weaponId) < 8) progression.DevRankUp(index);
            yield return null; // the weapon module mirrors the loadout
            yield return null;

            // A chest with the chance forced to 1 gives the evolution item (the build's items are wired as HasItem).
            Assert.IsTrue(game.Config.TryApplyEdit(game.Config.Single<StageContentConfig>().configId, "evolutionItemChance", "1", 0f, out var error), error);
            RunManager.Instance.AddGold(10000);
            var plan = game.Stage.Economy.PlanChest(new[] { weaponId });
            Assert.IsTrue(plan.Valid, plan.Error);
            Assert.AreEqual(evo.requiredItemId, plan.ItemId, "the chest gives the level-8 weapon's evolution item");
            Assert.IsTrue(game.Build.Items.TryAddItem(plan.ItemId));
            Assert.IsTrue(game.Build.Items.HasItem(evo.requiredItemId), "the build carries it (the chest skips carried items: EditMode test)");

            // The build's progression now deals the evolution card (after the first level-up, whose deal is fixed);
            // taking it evolves the weapon, which keeps levelling to 12.
            var firstDeal = progression.GenerateOffers();
            progression.Select(firstDeal.First(o => o.Kind != OfferKind.NewWeapon));
            Offer card = default;
            for (var i = 0; i < 40 && card.Kind != OfferKind.WeaponEvolution; i++)
            {
                var deal = progression.GenerateOffers();
                if (deal.Any(o => o.Kind == OfferKind.WeaponEvolution && o.Id == weaponId)) card = deal.First(o => o.Kind == OfferKind.WeaponEvolution && o.Id == weaponId);
            }
            Assert.AreEqual(OfferKind.WeaponEvolution, card.Kind, "the evolution card is dealt at level 8");
            progression.Select(card);
            Assert.AreEqual(evo.evolutionId, progression.EvolutionOf(weaponId));
            while (progression.WeaponLevel(weaponId) < 12) progression.DevRankUp(index);
            Assert.AreEqual(12, progression.WeaponLevel(weaponId));
            Assert.AreEqual(evo.evolutionId, progression.EvolutionOf(weaponId), "still evolved at level 12");
            yield return null;
        }
    }
}
