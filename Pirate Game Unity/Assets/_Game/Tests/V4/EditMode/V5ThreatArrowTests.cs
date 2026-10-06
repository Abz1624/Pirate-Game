using System.Collections.Generic;
using NUnit.Framework;
using RoguePirates.V4.Presentation.Hud;
using RoguePirates.V4.Views;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M8z (6 Oct, owner): the off-screen threat arrows' layout. On/off screen, the edge point inside the safe box,
    /// arrows spread along the edge when they would overlap, and the label.
    /// </summary>
    [Category("V5")]
    public sealed class V5ThreatArrowTests
    {
        static readonly Rect Canvas = new Rect(-960f, -540f, 1920f, 1080f);
        static readonly Vector2 Screen = new Vector2(1920f, 1080f);
        static readonly Rect Full = new Rect(0f, 0f, 1f, 1f);

        [Test]
        public void OnScreen_InsideTheInsetAndInFront_Only()
        {
            Assert.IsTrue(ThreatArrowPresenter.IsOnScreen(new Vector3(0.5f, 0.5f, 10f), 0.04f));
            Assert.IsFalse(ThreatArrowPresenter.IsOnScreen(new Vector3(0.98f, 0.5f, 10f), 0.04f), "in the edge inset");
            Assert.IsFalse(ThreatArrowPresenter.IsOnScreen(new Vector3(1.4f, 0.5f, 10f), 0.04f), "right of the screen");
            Assert.IsFalse(ThreatArrowPresenter.IsOnScreen(new Vector3(0.5f, -0.2f, 10f), 0.04f), "below the screen");
            Assert.IsFalse(ThreatArrowPresenter.IsOnScreen(new Vector3(0.5f, 0.5f, -10f), 0.04f), "behind the camera");
        }

        [Test]
        public void EdgeBox_IsTheSafePartLessTheMargin()
        {
            var box = ThreatArrowPresenter.EdgeBox(Canvas, Full, 70f, 70f);
            Assert.AreEqual(-890f, box.xMin, 1e-3f);
            Assert.AreEqual(890f, box.xMax, 1e-3f);
            Assert.AreEqual(-470f, box.yMin, 1e-3f);
            Assert.AreEqual(470f, box.yMax, 1e-3f);
            // A phone margin of 5 % on each side moves every edge in.
            var phone = ThreatArrowPresenter.EdgeBox(Canvas, Rect.MinMaxRect(0.05f, 0.05f, 0.95f, 0.95f), 70f, 70f);
            Assert.AreEqual(-890f + 96f, phone.xMin, 1e-3f);
            Assert.AreEqual(470f - 54f, phone.yMax, 1e-3f);
            // The top keeps clear of the HUD strip.
            Assert.AreEqual(540f - 190f, ThreatArrowPresenter.EdgeBox(Canvas, Full, 70f, 190f).yMax, 1e-3f);
        }

        [Test]
        public void EdgePoint_IsOnTheBox_TowardsTheTarget()
        {
            var box = ThreatArrowPresenter.EdgeBox(Canvas, Full, 70f, 70f);
            // Far right on the centre line: the right edge, pointing right.
            var p = ThreatArrowPresenter.EdgePoint(new Vector3(3f, 0.5f, 10f), Screen, box, out var dir);
            Assert.AreEqual(890f, p.x, 1e-2f);
            Assert.AreEqual(0f, p.y, 1e-2f);
            Assert.AreEqual(1f, dir.x, 1e-4f);
            // Far up and to the left: on the top edge (the box is wider than tall), inside the box.
            p = ThreatArrowPresenter.EdgePoint(new Vector3(-0.5f, 4f, 10f), Screen, box, out dir);
            Assert.AreEqual(470f, p.y, 1e-2f);
            Assert.Less(p.x, 0f);
            Assert.GreaterOrEqual(p.x, box.xMin - 1e-3f);
            Assert.Less(dir.x, 0f);
            Assert.Greater(dir.y, 0f);
            // Behind the camera a point projects mirrored: flipped back.
            p = ThreatArrowPresenter.EdgePoint(new Vector3(0.7f, 0.5f, -5f), Screen, box, out dir);
            Assert.AreEqual(-890f, p.x, 1e-2f);
        }

        [Test]
        public void Separate_SpreadsCrowdedArrowsAlongTheEdge_AndLeavesSpreadOnesAlone()
        {
            var box = ThreatArrowPresenter.EdgeBox(Canvas, Full, 70f, 70f);
            // Three threats in nearly the same direction, on the right edge.
            var points = new List<Vector2> { new Vector2(890f, 10f), new Vector2(890f, 20f), new Vector2(890f, 0f) };
            ThreatArrowPresenter.Separate(points, box, 90f, 190f);
            for (var i = 0; i < points.Count; i++)
            {
                Assert.AreEqual(890f, points[i].x, 1e-2f, "still on the right edge");
                Assert.LessOrEqual(Mathf.Abs(points[i].y), box.yMax + 1e-3f, "inside the box");
                for (var j = 0; j < i; j++)
                    Assert.GreaterOrEqual(Vector2.Distance(points[i], points[j]), 90f - 1e-2f, $"{i} and {j} apart");
            }
            // Near the top-right corner, a crowded arrow goes round the corner (still inside the box).
            points = new List<Vector2> { new Vector2(890f, 460f), new Vector2(890f, 465f) };
            ThreatArrowPresenter.Separate(points, box, 90f, 190f);
            Assert.GreaterOrEqual(Vector2.Distance(points[0], points[1]), 60f);
            foreach (var q in points) Assert.IsTrue(box.Contains(q) || Mathf.Approximately(q.y, box.yMax) || Mathf.Approximately(q.x, box.xMax));
            // Already apart: unchanged.
            points = new List<Vector2> { new Vector2(890f, 0f), new Vector2(-890f, 0f), new Vector2(0f, 470f) };
            var before = new List<Vector2>(points);
            ThreatArrowPresenter.Separate(points, box, 90f, 190f);
            CollectionAssert.AreEqual(before, points);
        }

        [Test]
        public void Label_IsBossOrTheName_WithTheDistance()
        {
            Assert.AreEqual("BOSS", ThreatArrowPresenter.Label(new ThreatMarker { isBoss = true, name = "Barnacle King" }, -1f));
            Assert.AreEqual("REEF CRAB\n<size=75%>120 m</size>",
                ThreatArrowPresenter.Label(new ThreatMarker { isMiniboss = true, name = "Reef Crab" }, 118f));
            Assert.AreEqual("ELITE", ThreatArrowPresenter.Label(new ThreatMarker { isFocus = true }, -1f));
            StringAssert.DoesNotContain("—", ThreatArrowPresenter.Label(new ThreatMarker { isFocus = true, name = "Elite Hammerhead" }, 40f));
        }
    }
}
