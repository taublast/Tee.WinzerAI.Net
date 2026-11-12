namespace Winzer.Engine.Tests;

/// <summary>Gemeinsame Hilfen der übertragenen Vitest-Tests (Kellermeister, Entscheidungen, Abwehr, Ereignisse, …)</summary>
public static class TestHelpersB
{
    public static readonly GameAction EndTurn = new GameAction.EndTurn();

    public static Player Current(GameState state) => Rules.CurrentPlayer(state);

    /// <summary>Beendet <paramref name="count"/> Monate hintereinander.</summary>
    public static GameState EndMonths(GameState state, int count)
    {
        for (var i = 0; i < count; i++) state = Game.ApplyAction(state, EndTurn);
        return state;
    }

    /// <summary><c>expect(() => …).toThrow('text')</c>: Regelverstoß, dessen Meldung den Text enthält</summary>
    public static void AssertRule(Action action, string text)
    {
        var ex = Assert.Throws<RuleException>(action);
        Assert.Contains(text, ex.Message);
    }

    /// <summary>Geprüfter, fertiger Flaschenposten ohne Auszeichnungen</summary>
    public static BottleLot Lot(string id, GrapeId grape, int vintage, double oechsle, QualityLevel level, double quality, double bottles,
        bool rose = false, bool sparkling = false) => new()
    {
        Id = id, Grape = grape, Vintage = vintage, Oechsle = oechsle, Level = level, Quality = quality, Adulterated = null, Rose = rose,
        Bottles = bottles, Sparkling = sparkling, MonthsUntilReady = 0, Registered = true, Awards = [],
    };

    public static NewGameOptions Solo(int seed, RegionId regionId, bool? randomEvents = false, StartSetup? start = null) => new()
    {
        Seed = seed,
        RandomEvents = randomEvents,
        Start = start,
        Players = [new NewPlayer { Name = "Anna", RegionId = regionId }],
    };

    public static SimResult SimulateSolo(int seed, int years, RegionId regionId, AiProfileId ai) =>
        Simulate.Run(new SimOptions { Seed = seed, Years = years, Players = [new SimPlayer { RegionId = regionId, Ai = ai }] });
}
