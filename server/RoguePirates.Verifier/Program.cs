// Standalone .NET verifier runner for cross-platform fixture parity (M0-004).
// Unity Simulation sources should be mirrored here for full parity; this runner validates harness wiring.

if (args.Length < 2 || !int.TryParse(args[0], out var seed) || !int.TryParse(args[1], out var ticks))
{
    Console.WriteLine("Usage: RoguePirates.Verifier <seed> <ticks>");
    return 1;
}

var hash = ComputePlaceholderHash(seed, ticks);
Console.WriteLine($"seed={seed} ticks={ticks} hash={hash}");
return 0;

static string ComputePlaceholderHash(int seed, int ticks)
{
    unchecked
    {
        var state = (uint)seed;
        for (var t = 0; t < ticks; t++)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            state += (uint)t;
        }
        return state.ToString("X8");
    }
}
