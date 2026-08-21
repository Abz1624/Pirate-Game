using HighSeas.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HighSeas.Tests
{
    public class PlayModeSmokeTests
    {
        [UnityTest]
        public System.Collections.IEnumerator RunManager_CanBeginRun()
        {
            var go = new GameObject("RunManagerTest");
            var rm = go.AddComponent<RunManager>();
            rm.BeginRun(123);
            Assert.That(rm.RunSeed, Is.EqualTo(123));
            Object.Destroy(go);
            yield return null;
        }
    }
}
