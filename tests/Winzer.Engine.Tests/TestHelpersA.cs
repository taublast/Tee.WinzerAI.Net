using System.Text.Json;

namespace Winzer.Engine.Tests;

/// <summary>Gemeinsame Hilfen der aus Vitest übertragenen Tests (Game, Ai, Auction, M4, M5, Regions, Sabotage, Bio).</summary>
public static class TestHelpersA
{
    public static readonly GameAction EndTurn = new GameAction.EndTurn();

    public static Player CurrentPlayer(GameState state) => Rules.CurrentPlayer(state);

    public static NewPlayer Human(string name, RegionId regionId) => new() { Name = name, RegionId = regionId };

    public static NewPlayer Computer(string name, RegionId regionId, AiProfileId ai) => new() { Name = name, RegionId = regionId, Ai = ai };

    public static GameState EndMonths(GameState state, int count)
    {
        for (var i = 0; i < count; i++) state = Game.ApplyAction(state, EndTurn);
        return state;
    }

    /// <summary><c>expect(fn).toThrow('text')</c>: Regelverstoß, dessen Meldung den Text enthält.</summary>
    public static void Throws(string text, Action action)
    {
        var error = Assert.Throws<RuleException>(action);
        Assert.Contains(text, error.Message);
    }

    /// <summary><c>toBeCloseTo(expected, digits)</c> mit der Rechenregel von Vitest: |Differenz| &lt; 10^-digits / 2 (auch für negative Stellen).</summary>
    public static void CloseTo(double expected, double actual, int digits = 2)
    {
        var diff = Math.Abs(expected - actual);
        Assert.True(diff < Math.Pow(10, -digits) / 2, $"erwartet {expected:R} (±{Math.Pow(10, -digits) / 2:R}), ist {actual:R}");
    }

    /// <summary>Für <c>toEqual</c>: Vergleich über die Serialisierung im Format des Originals.</summary>
    public static string JsonOf(object? value) => JsonSerializer.Serialize(value, Json.Options);

    public static BottleLot Lot(string id, GrapeId grape, int vintage, double oechsle, QualityLevel level, double quality, double bottles) => new()
    {
        Id = id, Grape = grape, Vintage = vintage, Oechsle = oechsle, Level = level, Quality = quality, Adulterated = null,
        Bottles = bottles, Sparkling = false, MonthsUntilReady = 0, Registered = true, Awards = [],
    };
}
