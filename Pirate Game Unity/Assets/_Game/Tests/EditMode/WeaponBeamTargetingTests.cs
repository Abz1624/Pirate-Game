using HighSeas.Combat;
using NUnit.Framework;
using UnityEngine;

namespace HighSeas.Tests.EditMode
{
    public sealed class WeaponBeamTargetingTests
    {
        [Test]
        public void IsInsideBeam_RespectsHalfWidthAndRange()
        {
            var origin = Vector3.zero;
            var forward = Vector3.forward;
            const float range = 20f;
            const float halfWidth = 0.35f;

            Assert.IsTrue(WeaponBeamTargeting.IsInsideBeam(origin, forward, range, halfWidth, new Vector3(0f, 0f, 10f), 0.5f));
            Assert.IsFalse(WeaponBeamTargeting.IsInsideBeam(origin, forward, range, halfWidth, new Vector3(2f, 0f, 10f), 0.5f));
            Assert.IsFalse(WeaponBeamTargeting.IsInsideBeam(origin, forward, range, halfWidth, new Vector3(0f, 0f, -1f), 0.5f));
            Assert.IsFalse(WeaponBeamTargeting.IsInsideBeam(origin, forward, range, halfWidth, new Vector3(0f, 0f, 25f), 0.5f));
        }
    }
}
