using Winzer.Engine.Data;

namespace Winzer.Engine;

public sealed class SimPlayer
{
    public RegionId RegionId { get; set; }
    public AiProfileId Ai { get; set; }
    public string? Name { get; set; }
}

public sealed class SimOptions
{
    public int Seed { get; set; }
    public List<SimPlayer> Players { get; set; } = [];
    public int Years { get; set; }
    /// <summary>Ohne Angabe praktisch unerreichbar, damit die volle Laufzeit gemessen wird</summary>
    public double? TargetPoints { get; set; }
    /// <summary>Lässt die KI unter den Bedingungen eines Schwierigkeitsgrads spielen, um dessen Wirkung zu messen</summary>
    public Difficulty? Difficulty { get; set; }
    /// <summary>Standard: an</summary>
    public bool? RandomEvents { get; set; }
    /// <summary>1 = Regeln des Originals, 2 = erweiterte Regeln; ohne Angabe gilt <see cref="Simulate.DefaultRulesVersion"/></summary>
    public int? RulesVersion { get; set; }
}

public sealed class PlayerSnapshot
{
    public string Name { get; set; } = "";
    public AiProfileId Ai { get; set; }
    public RegionId RegionId { get; set; }
    public double Points { get; set; }
    public double Money { get; set; }
    public double Loan { get; set; }
    public double Reputation { get; set; }
    public double Hectares { get; set; }
    public double NetWorth { get; set; }
    public bool Bankrupt { get; set; }
}

public sealed class YearSnapshot
{
    public int Year { get; set; }
    public List<PlayerSnapshot> Players { get; set; } = [];
}

public sealed class SimResult
{
    public GameState State { get; set; } = new();
    /// <summary>Stand jeweils am Jahresende</summary>
    public List<YearSnapshot> Snapshots { get; set; } = [];
}

/// <summary>simulate.ts</summary>
public static class Simulate
{
    /// <summary>Regelversion für Partien ohne eigene Angabe – der Balancing-Report stellt sie per <c>--rules</c> um.</summary>
    public static int DefaultRulesVersion { get; set; } = 1;

    /// <summary>
    /// Spielt eine Partie nur mit Computergegnern – Grundlage für Balancing und KI-Tests.
    /// Heißt im Original <c>simulate</c>; in C# darf eine Methode nicht wie ihre Klasse heißen.
    /// </summary>
    public static SimResult Run(SimOptions options)
    {
        var newPlayers = new List<NewPlayer>();
        for (var i = 0; i < options.Players.Count; i++)
        {
            var p = options.Players[i];
            newPlayers.Add(new NewPlayer { Name = p.Name ?? $"KI {i + 1}", RegionId = p.RegionId, Ai = p.Ai });
        }
        var state = Game.NewGame(new NewGameOptions
        {
            Seed = options.Seed,
            TargetPoints = options.TargetPoints ?? Js.MaxSafeInteger,
            RandomEvents = options.RandomEvents,
            RulesVersion = options.RulesVersion ?? DefaultRulesVersion,
            Players = newPlayers,
        });
        if (options.Difficulty is { } difficulty)
        {
            foreach (var p in state.Players)
            {
                p.Difficulty = difficulty;
                p.Money = Economy.Difficulties[difficulty].StartMoney;
            }
        }
        var snapshots = new List<YearSnapshot>();
        while (string.IsNullOrEmpty(state.WinnerId) && !state.Finished && state.Year <= options.Years)
        {
            var year = state.Year;
            state = Ai.PlayAiTurn(state);
            if (state.Year != year) snapshots.Add(Snapshot(state, year));
        }
        return new SimResult { State = state, Snapshots = snapshots };
    }

    static YearSnapshot Snapshot(GameState state, int year)
    {
        return new YearSnapshot
        {
            Year = year,
            Players = state.Players.Select(p => new PlayerSnapshot
            {
                Name = p.Name,
                Ai = p.Ai!.Value,
                RegionId = p.RegionId,
                Points = p.Points,
                Money = Js.Round(p.Money),
                Loan = p.Loan,
                Reputation = Js.Round(p.Reputation),
                Hectares = Market.TotalHectares(p),
                NetWorth = Js.Round(Market.NetWorth(p)),
                Bankrupt = p.Bankrupt,
            }).ToList(),
        };
    }
}
