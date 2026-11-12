using System.Text.Json;
using Winzer.Engine.Data;

namespace Winzer.Engine.Tests;

/// <summary>
/// Spielt die KI-Partien des Originals nach (Protokolle aus <c>npm run trace</c> im alten Repository)
/// und vergleicht den Zustand nach jedem Zug.
/// </summary>
public class TraceTests
{
    static readonly string TraceDir = Path.Combine(AppContext.BaseDirectory, "Traces");

    static readonly string[] Fields = ["money", "points", "reputation", "loan", "hectares", "netWorth", "cellarHl", "bottles", "bankrupt"];

    public static TheoryData<string> Scenarios()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(TraceDir, "*.json").Order()) data.Add(Path.GetFileNameWithoutExtension(file));
        return data;
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Partie_verlaeuft_wie_im_Original(string name)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(TraceDir, name + ".json")));
        var scenario = doc.RootElement.GetProperty("scenario");
        var rows = doc.RootElement.GetProperty("rows");

        var options = new NewGameOptions
        {
            Seed = scenario.GetProperty("seed").GetInt32(),
            TargetPoints = Js.MaxSafeInteger,
            RandomEvents = scenario.TryGetProperty("randomEvents", out var re) ? re.GetBoolean() : null,
            ClimateChange = scenario.TryGetProperty("climateChange", out var cc) ? cc.GetBoolean() : null,
        };
        var index = 0;
        foreach (var p in scenario.GetProperty("players").EnumerateArray())
        {
            options.Players.Add(new NewPlayer
            {
                Name = $"KI {++index}",
                RegionId = Json.Enum<RegionId>(p.GetProperty("regionId").GetString()!),
                Ai = Json.Enum<AiProfileId>(p.GetProperty("ai").GetString()!),
            });
        }
        var state = Game.NewGame(options);
        if (scenario.TryGetProperty("difficulty", out var d))
        {
            var difficulty = Json.Enum<Difficulty>(d.GetString()!);
            foreach (var p in state.Players)
            {
                p.Difficulty = difficulty;
                p.Money = Economy.Difficulties[difficulty].StartMoney;
            }
        }

        var turn = 0;
        foreach (var row in rows.EnumerateArray())
        {
            if (turn > 0) state = Ai.PlayAiTurn(state);
            Compare(name, turn, row, state);
            turn++;
        }
    }

    internal static void Compare(string name, int turn, JsonElement row, GameState state)
    {
        var where = $"{name}, Zug {turn} ({row.GetProperty("m").GetInt32()}/{row.GetProperty("y").GetInt32()})";
        Assert.True(row.GetProperty("y").GetInt32() == state.Year && row.GetProperty("m").GetInt32() == state.Month,
            $"{where}: Datum {state.Month}/{state.Year}");
        Assert.True(row.GetProperty("cp").GetInt32() == state.CurrentPlayer, $"{where}: Spieler am Zug {state.CurrentPlayer}");
        var players = row.GetProperty("p");
        for (var i = 0; i < state.Players.Count; i++)
        {
            var p = state.Players[i];
            double[] actual =
            [
                p.Money, p.Points, p.Reputation, p.Loan, Market.TotalHectares(p), Market.NetWorth(p),
                p.Cellar.Sum(b => b.Hectoliters), p.Bottles.Sum(l => l.Bottles), p.Bankrupt ? 1 : 0,
            ];
            var expected = players[i];
            for (var f = 0; f < actual.Length; f++)
            {
                var want = expected[f].GetDouble();
                var tolerance = 1e-9 * Math.Max(1, Math.Abs(want));
                Assert.True(Math.Abs(want - actual[f]) <= tolerance,
                    $"{where}: Spieler {i + 1} {Fields[f]} erwartet {want:R}, ist {actual[f]:R}");
            }
        }
        Assert.True(row.GetProperty("rng").GetInt32() == state.Rng.State, $"{where}: Zufallszustand weicht ab");
        Assert.True(row.GetProperty("id").GetInt32() == state.NextId, $"{where}: nextId {state.NextId}");
    }
}
