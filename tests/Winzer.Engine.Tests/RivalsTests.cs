using static Winzer.Engine.Tests.TestHelpersB;

namespace Winzer.Engine.Tests;

/// <summary>rivals.test.ts – Verhältnis der Computergegner</summary>
public class RivalsTests
{
    /// <summary>Anna (Mensch) und ein Computer-Nachbar im selben Gebiet</summary>
    static GameState Neighbours(AiProfileId ai = AiProfileId.Tradition, int seed = 3) => Game.NewGame(new NewGameOptions
    {
        Seed = seed,
        Start = new StartSetup { Money = 500_000, Plots = StartPlots.Planted },
        Players =
        [
            new NewPlayer { Name = "Anna", RegionId = RegionId.Pfalz },
            new NewPlayer { Name = "Nachbar", RegionId = RegionId.Pfalz, Ai = ai },
        ],
    });

    [Fact]
    public void Bleibt_zwischen_minus_100_und_plus_100_und_betrifft_nur_Computergegner()
    {
        var s = Neighbours();
        var (anna, ai) = (s.Players[0], s.Players[1]);
        Rivals.AdjustRelation(ai, anna.Id, -250);
        Assert.Equal(-100.0, Rivals.Relation(ai, anna.Id));
        Rivals.AdjustRelation(anna, ai.Id, 50);
        Assert.Empty(anna.Relations);
        Assert.Equal("verfeindet", Rivals.RelationLabel(-100).Text);
        Assert.Equal("befreundet", Rivals.RelationLabel(50).Text);
    }

    [Fact]
    public void Wer_bei_einer_Sabotage_erwischt_wird_macht_sich_einen_Feind()
    {
        for (var seed = 1; seed < 60; seed++)
        {
            var s = Neighbours(AiProfileId.Tradition, seed);
            var after = Game.ApplyAction(s, new GameAction.Sabotage("p2", SabotageKind.Geruecht));
            if (after.Log.Any(e => e.Text.Contains("erwischt")))
            {
                Assert.Equal(-60.0, Rivals.Relation(after.Players[1], "p1"));
                return;
            }
        }
        Assert.Fail("Keine Sabotage wurde entdeckt");
    }

    [Fact]
    public void Groll_verblasst_mit_der_Zeit()
    {
        var s = Neighbours();
        Rivals.AdjustRelation(s.Players[1], "p1", -60);
        for (var i = 0; i < 14; i++) Rivals.MonthlyRivalry(s);
        Assert.True(Rivals.Relation(s.Players[1], "p1") > -31);
        Assert.True(Rivals.Relation(s.Players[1], "p1") < -20);
    }

    [Fact]
    public void Verstimmte_Nachbarn_starten_Rabattaktionen_verfeindete_beschweren_sich_oeffentlich()
    {
        var priceWars = 0;
        var complaints = 0;
        for (var seed = 0; seed < 200; seed++)
        {
            var s = Neighbours();
            s.Rng.State = seed * 7919;
            Rivals.AdjustRelation(s.Players[1], "p1", -80);
            Rivals.MonthlyRivalry(s);
            if (s.Players[0].Effects.Any(e => e.Label.StartsWith("Rabattaktion", StringComparison.Ordinal))) priceWars++;
            if (s.Log.Any(e => e.Text.Contains("beschwert sich öffentlich"))) complaints++;
        }
        Assert.True(priceWars > 3, $"Rabattaktionen: {priceWars}");
        Assert.True(complaints > 3, $"Beschwerden: {complaints}");
    }

    [Fact]
    public void Freundliche_Nachbarn_starten_keine_Feindseligkeiten()
    {
        for (var seed = 0; seed < 100; seed++)
        {
            var s = Neighbours();
            s.Rng.State = seed * 7919;
            Rivals.AdjustRelation(s.Players[1], "p1", 40);
            Rivals.MonthlyRivalry(s);
            Assert.Empty(s.Players[0].Effects);
        }
    }

    [Fact]
    public void Das_Schlitzohr_raecht_sich_im_Sommer_am_Weinberg_seines_Erzfeinds()
    {
        var s = Neighbours(AiProfileId.Schlitzohr);
        s.Year = 3;
        s.Month = 6;
        Rivals.AdjustRelation(s.Players[1], "p1", -70);
        Assert.Equal("p1", Rivals.ArchEnemy(s, s.Players[1])?.Id);
        s = Game.ApplyAction(s, EndTurn); // jetzt zieht das Schlitzohr
        Assert.Equal("p2", Current(s).Id);
        var after = Ai.PlayAiTurn(s);
        Assert.Contains(after.Log, e => e.Text.Contains("Sabotage gegen Anna") || e.Text.Contains("beschädigt"));
    }

    [Fact]
    public void Computergegner_roden_wenn_ihre_Sorte_aus_der_Mode_kommt_und_eine_andere_gefragt_ist()
    {
        var s = Neighbours(AiProfileId.Tradition);
        s.Year = 4;
        s.Month = 1;
        var ai = s.Players[1];
        var grape = ai.Plots[0].Grape!.Value;
        s.Trends[(TrendKey)(int)grape] = new Trend { Value = 0.85, Target = 0.82 };
        s.Trends[TrendKey.Spaetburgunder] = new Trend { Value = 1.1, Target = 1.25 };
        s = Game.ApplyAction(s, EndTurn);
        var after = Ai.PlayAiTurn(s);
        Assert.Contains(after.Players[1].Plots, p => p.Grape is null);
        Assert.Contains(after.Log, e => e.PlayerId is null && e.Text.StartsWith("Nachbar rodet", StringComparison.Ordinal));
    }
}
