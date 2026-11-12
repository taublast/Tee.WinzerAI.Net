using System.Text.Json;

namespace Winzer.Engine.Tests;

/// <summary>
/// Spielstände des alten Spiels: Ein mitten in der Partie gesicherter Stand (Format der Spielstandsdateien,
/// erzeugt von <c>npm run trace</c>) wird geladen und muss von dort an dieselben Züge liefern wie das Original.
/// </summary>
public class SaveCompatTests
{
    static readonly string SaveDir = Path.Combine(AppContext.BaseDirectory, "Saves");
    static readonly string TraceDir = Path.Combine(AppContext.BaseDirectory, "Traces");

    public static TheoryData<string> Saves()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(SaveDir, "*.json").Order()) data.Add(Path.GetFileNameWithoutExtension(file));
        return data;
    }

    [Fact]
    public void Es_gibt_Spielstaende_zum_Pruefen() => Assert.NotEmpty(Directory.GetFiles(SaveDir, "*.json"));

    [Theory]
    [MemberData(nameof(Saves))]
    public void Alter_Spielstand_laedt_und_spielt_wie_im_Original_weiter(string name)
    {
        using var save = JsonDocument.Parse(File.ReadAllText(Path.Combine(SaveDir, name + ".json")));
        var turn = save.RootElement.GetProperty("turn").GetInt32();
        var state = Json.DeserializeState(save.RootElement.GetProperty("state").GetRawText());
        Assert.NotNull(state);
        Assert.Equal(17, state.Version);
        Assert.Equal(1, state.RulesVersion);

        using var trace = JsonDocument.Parse(File.ReadAllText(Path.Combine(TraceDir, name + ".json")));
        var rows = trace.RootElement.GetProperty("rows");
        TraceTests.Compare(name, turn, rows[turn], state);
        var compared = 0;
        for (var i = turn + 1; i < rows.GetArrayLength(); i++)
        {
            state = Ai.PlayAiTurn(state);
            TraceTests.Compare(name, i, rows[i], state);
            compared++;
        }
        Assert.True(compared > 100, $"nur {compared} Züge nach dem Laden verglichen");
    }

    [Theory]
    [MemberData(nameof(Saves))]
    public void Geladener_Stand_uebersteht_erneutes_Speichern(string name)
    {
        using var save = JsonDocument.Parse(File.ReadAllText(Path.Combine(SaveDir, name + ".json")));
        var state = Json.DeserializeState(save.RootElement.GetProperty("state").GetRawText())!;
        var again = Json.DeserializeState(Json.Serialize(state))!;
        Assert.Equal(Json.Serialize(state), Json.Serialize(again));
        // Was das Original gespeichert hat, ist angekommen: Spieler, Parzellen, Keller, Trends, Logbuch
        var original = save.RootElement.GetProperty("state");
        Assert.Equal(original.GetProperty("players").GetArrayLength(), state.Players.Count);
        Assert.Equal(original.GetProperty("log").GetArrayLength(), state.Log.Count);
        Assert.Equal(original.GetProperty("trends").EnumerateObject().Count(), state.Trends.Count);
        for (var i = 0; i < state.Players.Count; i++)
        {
            var p = original.GetProperty("players")[i];
            Assert.Equal(p.GetProperty("plots").GetArrayLength(), state.Players[i].Plots.Count);
            Assert.Equal(p.GetProperty("cellar").GetArrayLength(), state.Players[i].Cellar.Count);
            Assert.Equal(p.GetProperty("bottles").GetArrayLength(), state.Players[i].Bottles.Count);
            Assert.Equal(p.GetProperty("name").GetString(), state.Players[i].Name);
        }
    }
}
