using Winzer.Engine.Data;
using static Winzer.Engine.Tests.TestHelpersB;

namespace Winzer.Engine.Tests;

/// <summary>defense.test.ts – Wetterabwehr</summary>
public class DefenseTests
{
    /// <summary>Jahr 3, alle drei Parzellen tragen, viel Geld – im gewünschten Monat</summary>
    static GameState BearingEstate(int month, int seed = 1)
    {
        var s = Game.NewGame(Solo(seed, RegionId.Franken, false, new StartSetup { Plots = StartPlots.Planted, Money = 500_000 }));
        s.Year = 3;
        s.Month = month;
        return s;
    }

    /// <summary>Wendet das Monatswetter auf viele Zufallsfolgen an und mittelt den Ertragsfaktor je Parzelle</summary>
    static double[] AverageCrop(Action<GameState> prepare, int month, WeatherKind weather, int runs = 400)
    {
        var totals = new double[] { 0, 0, 0 };
        for (var i = 0; i < runs; i++)
        {
            var s = BearingEstate(month);
            prepare(s);
            s.Rng.State = i * 7919 + 13;
            Vineyard.VineyardEvents(s, Current(s), weather);
            var plots = Current(s).Plots;
            for (var j = 0; j < plots.Count; j++) totals[j] += plots[j].CropFactor;
        }
        return totals.Select(t => t / runs).ToArray();
    }

    [Fact]
    public void Frostkerzen_verhindern_Spaetfrostschaeden()
    {
        Assert.True(Defense.FrostChance(BearingEstate(4), Current(BearingEstate(4))) > 0);
        Assert.Equal(0.0, Defense.FrostChance(BearingEstate(6), Current(BearingEstate(6))));
        var unprotected = AverageCrop(_ => { }, 4, WeatherKind.Wechselhaft);
        var protectedCrop = AverageCrop(s => Current(s).FrostCandles = true, 4, WeatherKind.Wechselhaft);
        Assert.True(unprotected[0] < 0.99);
        Assert.Equal(1.0, protectedCrop[0]);
    }

    [Fact]
    public void Ein_Hagelnetz_haelt_den_Grossteil_des_Hagelschadens_ab()
    {
        var crop = AverageCrop(s => Current(s).Plots[0].HailNet = true, 7, WeatherKind.Wechselhaft, 800);
        var lossNet = 1 - crop[0];
        var lossOpen = 1 - (crop[1] + crop[2]) / 2;
        Assert.True(lossOpen > 0.005);
        Assert.True(lossNet < lossOpen * 0.3);
    }

    [Fact]
    public void Bewaesserung_schuetzt_vor_Hitze_und_Duerre()
    {
        var crop = AverageCrop(s => Current(s).Plots[0].Irrigation = true, 8, WeatherKind.Sonnig, 800);
        var lossWatered = 1 - crop[0];
        var lossDry = 1 - (crop[1] + crop[2]) / 2;
        Assert.True(lossDry > lossWatered + 0.01);
    }

    [Fact]
    public void Die_Ernteversicherung_zahlt_80_Prozent_des_verlorenen_Werts()
    {
        var s = BearingEstate(1);
        var player = Current(s);
        var premium = Defense.InsurancePremium(s, player);
        Assert.True(premium > 0);
        var insured = Game.ApplyAction(s, new GameAction.InsureHarvest());
        player = Current(insured);
        Assert.Equal(500_000 - premium, player.Money);
        AssertRule(() => Game.ApplyAction(insured, new GameAction.InsureHarvest()), "bereits");

        var plot = player.Plots[0];
        var payout = Defense.CropLoss(insured, player, [plot], 0.5, "Hagel");
        Assert.Equal(Js.Round((plot.Hectares * 0.5 * Economy.InsuredValuePerHa * 0.8) / 100) * 100, payout);
        Assert.Equal(500_000 - premium + payout, player.Money);
    }

    [Fact]
    public void Ohne_Versicherung_zahlt_niemand()
    {
        var s = BearingEstate(6);
        var player = Current(s);
        Assert.Equal(0.0, Defense.CropLoss(s, player, [player.Plots[0]], 0.5, "Hagel"));
        Assert.Equal(500_000.0, player.Money);
    }

    [Fact]
    public void Prueft_Zeitpunkt_und_doppelte_Installation()
    {
        AssertRule(() => Game.ApplyAction(BearingEstate(6), new GameAction.InsureHarvest()), "Januar bis April");
        AssertRule(() => Game.ApplyAction(BearingEstate(6), new GameAction.FrostCandles()), "April und Mai");
        var s = BearingEstate(6);
        var plotId = Current(s).Plots[0].Id;
        var netted = Game.ApplyAction(s, new GameAction.InstallHailNet(plotId));
        Assert.True(Current(netted).Plots[0].HailNet);
        AssertRule(() => Game.ApplyAction(netted, new GameAction.InstallHailNet(plotId)), "bereits");
    }

    [Fact]
    public void Frostkerzen_gelten_nur_fuer_den_laufenden_Monat()
    {
        var s = Game.ApplyAction(BearingEstate(4), new GameAction.FrostCandles());
        Assert.True(Current(s).FrostCandles);
        s = Game.ApplyAction(s, EndTurn);
        Assert.False(Current(s).FrostCandles);
    }

    [Fact]
    public void Vorsichtige_Computergegner_versichern_ihre_Ernte()
    {
        var state = SimulateSolo(3, 4, RegionId.Franken, AiProfileId.Tradition).State;
        Assert.Contains(state.Log, e => e.Text.StartsWith("Ernteversicherung für Jahr", StringComparison.Ordinal));
    }
}
