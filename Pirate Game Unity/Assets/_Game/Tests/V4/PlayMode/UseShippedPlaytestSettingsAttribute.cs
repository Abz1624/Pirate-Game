using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M8z (7 Oct, I8z.3): runs a test with the playtest switches the owner plays (<see cref="DemoMode"/> on, reading
    /// the shipped Resources/DemoJuiceSettings asset: unlock everything, chests-only or all points of interest, the Beacon
    /// or the timed boss, the whirlpool arrival, the hidden voyage toast...), so tests cover what ships. Every other test
    /// keeps DemoMode off (DemoOffForTests); this puts it back after the test.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class UseShippedPlaytestSettingsAttribute : NUnitAttribute, ITestAction
    {
        bool _was;

        public ActionTargets Targets => ActionTargets.Test;

        public void BeforeTest(ITest test)
        {
            _was = DemoMode.Off;
            DemoMode.Off = false;
            Assert.IsNotNull(ShippedPlaytestSettings.Asset, "Resources/DemoJuiceSettings.asset (the shipped playtest switches)");
        }

        public void AfterTest(ITest test) => DemoMode.Off = _was;
    }

    /// <summary>V5 M8z (7 Oct): the shipped playtest switches the attribute above turns on.</summary>
    public static class ShippedPlaytestSettings
    {
        public static DemoJuiceSettings Asset => Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
    }
}
