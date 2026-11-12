using static Winzer.Engine.Tests.TestHelpersB;

namespace Winzer.Engine.Tests;

/// <summary>milestones.test.ts – Meilensteine</summary>
public class MilestonesTests
{
    static GameState Start() => Game.NewGame(Solo(1, RegionId.Mosel));
    static MilestoneDef Def(string id) => Milestones.All.First(m => m.Id == id);

    [Fact]
    public void Jeder_Meilenstein_hat_eine_eindeutige_ID_und_einen_sinnvollen_Fortschritt()
    {
        Assert.Equal(Milestones.All.Count, Milestones.All.Select(m => m.Id).Distinct().Count());
        var s = Start();
        foreach (var m in Milestones.All)
        {
            var (value, target) = m.Progress(s, Current(s));
            Assert.True(double.IsFinite(value), m.Id);
            Assert.True(target > 0, m.Id);
        }
    }

    [Fact]
    public void Die_erste_Lese_wird_belohnt_und_mit_Datum_in_der_Chronik_festgehalten()
    {
        var s = Start();
        for (var i = 0; i < 9; i++) s = Game.ApplyAction(s, EndTurn); // Oktober
        s = Game.ApplyAction(s, new GameAction.Harvest(Current(s).Plots[0].Id));
        Assert.Equal(1, Current(s).Stats.Harvests);
        var points = Current(s).Points;
        var month = Bio.GameMonth(s);
        s = Game.ApplyAction(s, EndTurn);
        var player = Current(s);
        Assert.Equal(month, player.Milestones["erste-lese"]);
        Assert.Equal(points + Milestones.TierRewards[Tier.Bronze].Points, player.Points);
        Assert.Contains(s.Log, e => e.Text.Contains("„Erste Lese“") && e.Tone == LogTone.Good);
    }

    [Fact]
    public void Wird_nur_einmal_vergeben()
    {
        var s = Start();
        var player = Current(s);
        player.HasVinothek = true;
        Milestones.CheckMilestones(s);
        var points = player.Points;
        Milestones.CheckMilestones(s);
        Assert.Equal(points, player.Points);
        Assert.Equal(new[] { "vinothek" }, player.Milestones.Keys);
    }

    [Fact]
    public void Beim_Uebernehmen_alter_Spielstaende_ohne_Belohnung_eingetragen()
    {
        var s = Start();
        var player = Current(s);
        player.HasOnlineShop = true;
        Milestones.CheckMilestones(s, true);
        Assert.True(player.Milestones.ContainsKey("onlineshop"));
        Assert.Equal(0.0, player.Points);
    }

    [Fact]
    public void Fortschritt_bei_Zaehlern_und_Schwellen()
    {
        var s = Start();
        var player = Current(s);
        player.Stats.BottlesSold = 4000;
        Assert.Equal((4000.0, 10_000.0), Def("flaschen-10k").Progress(s, player));
        player.Reputation = 55;
        Assert.True(Milestones.IsReached(s, player, Def("bekanntheit-50")));
        Assert.False(Milestones.IsReached(s, player, Def("bekanntheit-80")));
    }

    [Fact]
    public void In_einer_laengeren_Partie_kommen_viele_Meilensteine_zusammen()
    {
        var state = SimulateSolo(2, 12, RegionId.Rheingau, AiProfileId.Tradition).State;
        var reached = state.Players[0].Milestones.Keys.ToList();
        Assert.True(reached.Count > 8, $"erreicht: {reached.Count}");
        Assert.True(reached.Count < Milestones.All.Count);
        Assert.Contains("erste-lese", reached);
    }
}
