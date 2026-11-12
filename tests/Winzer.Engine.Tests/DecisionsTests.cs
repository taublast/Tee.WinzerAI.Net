using static Winzer.Engine.Tests.TestHelpersB;

namespace Winzer.Engine.Tests;

/// <summary>decisions.test.ts – Entscheidungsereignisse; Weinberge verkaufen und roden</summary>
public class DecisionsTests
{
    static readonly Lazy<GameState> Established = new(() => SimulateSolo(7, 4, RegionId.Pfalz, AiProfileId.Tradition).State);

    /// <summary>Ein eingespieltes Weingut mit Menschen am Zug: tragende Reben, volles Flaschenlager, Kellermeister</summary>
    static GameState Prepared(int[]? months = null)
    {
        var s = Established.Value.Clone();
        var player = Current(s);
        player.Ai = null;
        player.Money = 500_000;
        player.CellarMaster = new CellarMaster
        {
            Name = "Test", Talent = 5, Skills = [CellarSkill.Sekt, CellarSkill.Barrique], Salary = 4000, HiredYear = 1,
        };
        s.Month = months is { Length: > 0 } ? months[0] : 6;
        foreach (var plot in player.Plots) plot.HarvestedThisYear = false;
        player.Bottles.Add(Lot("lot-x", GrapeId.Riesling, s.Year - 1, 85, QualityLevel.Kabinett, 75, 3000));
        // Fertiger Wein im Tank und ein befreundeter Computer-Nachbar für die Nachbarschaftsangebote
        player.Cellar.Add(new WineBatch
        {
            Id = "wine-x", Grape = GrapeId.Riesling, Vintage = s.Year - 1, Oechsle = 80, Level = QualityLevel.Kabinett, Quality = 70,
            Adulterated = null, Hectoliters = 60, MonthsUntilReady = 0, Barrique = false,
        });
        var neighbour = player.Clone();
        neighbour.Id = "p2";
        neighbour.Name = "Weingut Rebenhain";
        neighbour.Ai = AiProfileId.Tradition;
        neighbour.Relations = new Dictionary<string, double> { [player.Id] = 30 };
        s.Players.Add(neighbour);
        return s;
    }

    static DecisionDef Def(string id) => Decisions.DecisionEvents.First(d => d.Id == id);

    public static TheoryData<string> DecisionIds()
    {
        var data = new TheoryData<string>();
        foreach (var d in Decisions.DecisionEvents) data.Add(d.Id);
        return data;
    }

    [Theory]
    [MemberData(nameof(DecisionIds))]
    public void Entscheidung_bietet_eine_Wahl_und_jede_Moeglichkeit_laesst_sich_waehlen(string id)
    {
        var def = Def(id);
        var @base = Prepared(def.Months);
        Assert.Contains(def, Decisions.PossibleDecisions(@base, Current(@base)));
        Decisions.OfferDecision(@base, Current(@base), def);
        var decision = Current(@base).Decision!;
        Assert.NotNull(decision);
        Assert.True(decision.Options.Count >= 2);
        Assert.InRange(decision.Fallback, 0, decision.Options.Count - 1);
        Assert.DoesNotMatch("NaN|undefined", decision.Text);

        for (var choice = 0; choice < decision.Options.Count; choice++)
        {
            var after = Game.ApplyAction(@base, new GameAction.Decide(choice));
            Assert.Null(Current(after).Decision);
            Assert.DoesNotMatch("NaN|undefined", after.Log[^1].Text);
        }
    }

    [Fact]
    public void Kostspielige_Wahl_nur_wenn_sie_finanzierbar_ist()
    {
        var s = Prepared();
        var player = Current(s);
        Decisions.OfferDecision(s, player, Def("weinmesse-einladung"));
        player.Money = 0;
        player.Loan = 10_000_000;
        AssertRule(() => Game.ApplyAction(s, new GameAction.Decide(0)), "Nicht finanzierbar");
        Game.ApplyAction(s, new GameAction.Decide(1)); // darf nicht werfen
    }

    [Fact]
    public void Ohne_Antwort_gilt_am_Monatsende_die_vorsichtige_Wahl()
    {
        var s = Prepared();
        s.RandomEvents = false;
        Decisions.OfferDecision(s, Current(s), Def("gebrauchte-tanks"));
        var tanks = Current(s).TankCapacityHl;
        var after = Ai.RunAiTurns(Game.ApplyAction(s, EndTurn)); // der Nachbar zieht mit, dann endet der Monat
        Assert.Null(after.Players[0].Decision);
        Assert.Equal(tanks, after.Players[0].TankCapacityHl);
        Assert.Contains(after.Log, e => e.Text.StartsWith("Keine Antwort auf „Gebrauchte Tanks günstig“", StringComparison.Ordinal));
    }

    [Fact]
    public void Computergegner_entscheiden_sofort()
    {
        var s = Prepared();
        var player = Current(s);
        player.Ai = AiProfileId.Tradition;
        var before = s.Log.Count;
        Decisions.OfferDecision(s, player, Def("vereinsfest"));
        Assert.Null(player.Decision);
        Assert.True(s.Log.Count > before);
    }

    [Fact]
    public void Kommen_im_Spielverlauf_regelmaessig_vor()
    {
        var offered = 0;
        for (var seed = 0; seed < 300; seed++)
        {
            var s = Prepared();
            s.Rng.State = seed * 104_729;
            Decisions.MonthlyEvent(s, Current(s));
            if (Current(s).Decision is not null) offered++;
        }
        Assert.True(offered > 10, $"angeboten: {offered}");
        Assert.True(offered < 80, $"angeboten: {offered}");
    }

    // Weinberge verkaufen und roden

    static GameState Start() => Game.NewGame(Solo(1, RegionId.Mosel));

    [Fact]
    public void Ein_Weinberg_laesst_sich_verkaufen_die_letzte_Parzelle_nicht()
    {
        var s = Start();
        var plots = Current(s).Plots;
        var (first, second, third) = (plots[0], plots[1], plots[2]);
        var money = Current(s).Money;
        s = Game.ApplyAction(s, new GameAction.SellPlot(second.Id));
        Assert.Equal(new[] { first.Id, third.Id }, Current(s).Plots.Select(p => p.Id));
        Assert.True(Current(s).Money > money + 50_000); // 3 ha Brache, sehr gute Lage
        s = Game.ApplyAction(s, new GameAction.SellPlot(third.Id));
        AssertRule(() => Game.ApplyAction(s, new GameAction.SellPlot(first.Id)), "letzte Parzelle");
    }

    [Fact]
    public void Gerodete_Reben_machen_Platz_fuer_eine_neue_Sorte()
    {
        var s = Start();
        var plot = Current(s).Plots[0];
        var money = Current(s).Money;
        s = Game.ApplyAction(s, new GameAction.ClearPlot(plot.Id));
        var cleared = Current(s).Plots[0];
        Assert.Null(cleared.Grape);
        Assert.Equal(money - plot.Hectares * 2000, Current(s).Money);
        AssertRule(() => Game.ApplyAction(s, new GameAction.ClearPlot(plot.Id)), "brach");

        for (var i = 0; i < 2; i++) s = Game.ApplyAction(s, EndTurn); // März
        Current(s).Money = 200_000; // Spätburgunder-Pflanzgut für 4 ha ist teuer
        s = Game.ApplyAction(s, new GameAction.Plant(plot.Id, GrapeId.Spaetburgunder));
        Assert.Equal(GrapeId.Spaetburgunder, Current(s).Plots[0].Grape);
        Assert.Equal(1, Current(s).Plots[0].PlantedYear);
    }
}
