using NUnit.Framework;

/// <summary>
/// The demo build's switches (DemoMode: no points of interest, half XP, faster ship, the art HUD, the boss at 10:00,
/// hidden barks...) are off for every test in this assembly: the tests cover the regular game. Namespace-less, so NUnit
/// runs it once around the whole assembly.
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
