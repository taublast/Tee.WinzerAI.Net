namespace Winzer.Engine;

/// <summary>
/// Seed-basierter Zufall (mulberry32), bitgleich mit rng.ts. Der Zustand ist eine einzelne
/// 32-Bit-Zahl im Spielstand – so bleiben Partien reproduzierbar.
/// </summary>
public sealed class Rng
{
    public int State { get; set; }

    public static double NextFloat(Rng rng)
    {
        unchecked
        {
            rng.State += 0x6d2b79f5;
            var t = (uint)rng.State;
            t = (t ^ (t >> 15)) * (t | 1);
            t ^= t + (t ^ (t >> 7)) * (t | 61);
            return (t ^ (t >> 14)) / 4294967296.0;
        }
    }

    public static double NextRange(Rng rng, double min, double max) => min + NextFloat(rng) * (max - min);

    public static int SeedFromString(string text)
    {
        unchecked
        {
            var h = 2166136261u;
            foreach (var ch in text) h = (h ^ ch) * 16777619u;
            return (int)h;
        }
    }
}
