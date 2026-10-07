using System.Collections;
using NUnit.Framework;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Online;
using UnityEngine;
using UnityEngine.TestTools;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M8z (7 Oct, I8z.2): the online services and the 1v1 session are owned by the app root (composed once,
    /// registered on it); the callers' static entry points reach those instances.
    /// </summary>
    [Category("V5")]
    public sealed class V5ArchitectureM8zPlayModeTests
    {
        [UnityTest]
        public IEnumerator OnlineServices_AndThePvpSession_AreOwnedByTheAppRoot()
        {
            AppRoot.EnsureInitialized();
            yield return null;
            var online = OnlineComposition.Online;
            Assert.IsTrue(AppRoot.Instance.TryGet<OnlineServices>(out var registered) && ReferenceEquals(online, registered), "registered on the app root");
            Assert.AreSame(online, OnlineComposition.Online, "composed once");
            Assert.AreSame(online.Profiles, OnlineService.Profiles, "the static facade reads the owned instance");

            var session = PvpSession.Instance;
            Assert.IsNotNull(session);
            Assert.AreSame(AppRoot.Instance.gameObject, session.gameObject, "the session lives on the app root's object (no object of its own)");
            Assert.AreSame(session, PvpSession.Instance, "composed once");
            Assert.IsTrue(PvpSession.Exists);
            Assert.IsNull(GameObject.Find("PvpSession"), "no lazily created session object");
        }
    }
}
