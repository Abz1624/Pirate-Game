using NUnit.Framework;
using RoguePirates.V4.DevTools.Bench;
using RoguePirates.V4.Editor;
using UnityEditor;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 performance tools (2 Oct): the Android texture cap's rules and the benchmark's arguments (URL and Android command
    /// line). The measurements themselves are in plan/v5/perf/.
    /// </summary>
    public sealed class V5BenchToolsTests
    {
        const TextureImporterCompression Compressed = TextureImporterCompression.Compressed;

        [Test]
        public void AndroidCap_WorldAt1024_UiAt2048_NeverGrows_SkipsUncompressedAndTextAtlases()
        {
            Assert.AreEqual(1024, V5AndroidProfiles.CapFor("Assets/Models/hull.png", TextureImporterType.Default, Compressed, 2048, 2048, 1024, 2048));
            Assert.AreEqual(1024, V5AndroidProfiles.CapFor("Assets/Models/hull_n.png", TextureImporterType.NormalMap, Compressed, 4096, 4949, 1024, 2048), "normal maps are world textures");
            Assert.AreEqual(2048, V5AndroidProfiles.CapFor("Assets/UI/portrait.png", TextureImporterType.Sprite, Compressed, 4096, 3333, 1024, 2048), "UI sprites keep 2048");
            Assert.IsNull(V5AndroidProfiles.CapFor("Assets/UI/icon.png", TextureImporterType.Sprite, Compressed, 2048, 1254, 1024, 2048), "a 1254 sprite is within its cap");
            Assert.IsNull(V5AndroidProfiles.CapFor("Assets/Models/small.png", TextureImporterType.Default, Compressed, 2048, 512, 1024, 2048), "the source is already small");
            Assert.IsNull(V5AndroidProfiles.CapFor("Assets/Models/set.png", TextureImporterType.Default, Compressed, 512, 2048, 1024, 2048), "already capped lower: never raised");
            Assert.IsNull(V5AndroidProfiles.CapFor("Assets/Fx/mask.png", TextureImporterType.Default, TextureImporterCompression.Uncompressed, 2048, 2048, 1024, 2048), "deliberately uncompressed");
            Assert.IsNull(V5AndroidProfiles.CapFor("Assets/Texture2D/PirataOne-Regular SDF Atlas.png", TextureImporterType.Default, Compressed, 4096, 4096, 1024, 2048), "text atlases keep their size");
        }

        [Test]
        public void BenchArguments_FromTheUrl_AndTheAndroidCommandLine()
        {
            var url = PhoneBench.Parse("http://localhost:8080/WebGL-Mobile/RoguePirates/index.html?bench=APK%20smoke&q=mid&crowd=80#top");
            Assert.AreEqual("APK smoke", url["bench"]);
            Assert.AreEqual("80", url["crowd"]);
            Assert.AreEqual(3, url.Count, "the #fragment is not an argument");

            var line = PhoneBench.ParseCommandLine(new[] { "app", "-v5bench", "bench=check&short=1&shadows=0" });
            Assert.AreEqual("check", line["bench"]);
            Assert.AreEqual("0", line["shadows"]);
            Assert.AreEqual(0, PhoneBench.ParseCommandLine(new[] { "app", "-v5bench" }).Count, "-v5bench last, with nothing after it");
            Assert.AreEqual(0, PhoneBench.ParseCommandLine(new[] { "app" }).Count);
            Assert.AreEqual(0, PhoneBench.ParseCommandLine(null).Count);
        }
    }
}
