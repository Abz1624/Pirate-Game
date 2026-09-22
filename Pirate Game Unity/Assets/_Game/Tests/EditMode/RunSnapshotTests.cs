using HighSeas.Save;
using NUnit.Framework;

namespace HighSeas.Tests
{
    public class RunSnapshotTests
    {
        [Test]
        public void RunSnapshot_RoundTrip_PreservesSchemaVersion()
        {
            RunSnapshotService.Clear();
            var data = new RunSnapshotData
            {
                runSeed = 42,
                experimentProfileId = "D_FULL_COMBAT_V2",
                weaponIds = { "deck_cannons", "broadside_battery" },
                weaponLevels = { 2, 1 }
            };
            RunSnapshotService.Save(data);
            var loaded = RunSnapshotService.Load();
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.schemaVersion, Is.EqualTo(RunSnapshotRestoreLogic.SupportedSchemaVersion));
            Assert.That(loaded.runSeed, Is.EqualTo(42));
            Assert.That(loaded.weaponIds, Is.EqualTo(data.weaponIds));
            RunSnapshotService.Clear();
        }
    }
}
