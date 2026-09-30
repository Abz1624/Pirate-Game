using NUnit.Framework;

/// <summary>
/// The demo build's switches (DemoMode: no points of interest, the art HUD, the boss at 10:00,
/// hidden barks...) are off for every test in this assembly: the tests cover the regular game. Namespace-less, so NUnit
/// runs it once around the whole assembly. The XP pace (×0.5) and the ship speed (×1.625) are
/// config since 1 Oct 2026 (PickupConfig, RunConfig), so tests run at the V5 baseline unless they set them.
/// </summary>
[SetUpFixture]
public sealed class DemoOffForTests
{
    bool _was;

    [OneTimeSetUp]
    public void TurnDemoOff()
    {
        _was = RoguePirates.V4.Foundation.Core.DemoMode.Off;
        RoguePirates.V4.Foundation.Core.DemoMode.Off = true;
    }

    [OneTimeTearDown]
    public void Restore() => RoguePirates.V4.Foundation.Core.DemoMode.Off = _was;
}
