using Winzer.Engine.Data;
using static Winzer.Engine.Tests.TestHelpersA;

namespace Winzer.Engine.Tests;

/// <summary>regions.test.ts</summary>
public class RegionsTests
{
    /// <summary>Ein Weingut im Gebiet, im Oktober des dritten Jahres, alle Parzellen mit gleichen alten Riesling-Reben</summary>
    static GameState Estate(RegionId regionId)
    {
        var s = Game.NewGame(new NewGameOptions
        {
            Seed = 1, RandomEvents = false, Start = new StartSetup { Plots = StartPlots.Planted }, Players = [Human("Anna", regionId)],
        });
        s.Year = 3;
        s.Month = 10;
        var player = CurrentPlayer(s);
        foreach (var plot in player.Plots)
        {
            plot.Grape = GrapeId.Riesling;
            plot.Oechsle = 80;
            plot.Site = 0;
            plot.Health = 1;
            plot.CropFactor = 1;
        }
        foreach (var trend in s.Trends.Values) trend.Value = 1;
        return s;
    }

    static readonly BottleLot TestLot = Lot("x", GrapeId.Riesling, 2, 85, QualityLevel.Kabinett, 75, 100);

    [Fact]
    public void Jedes_Gebiet_hat_einen_Charakter_in_sinnvollen_Grenzen()
    {
        foreach (var region in Regions.All.Values)
        {
            var c = region.Character;
            Assert.True(c.Slope >= 0);
            Assert.True(c.Slope <= 1);
            Assert.True(c.Prestige >= -1);
            Assert.True(c.Prestige <= 2);
            Assert.True(c.Tourism >= 0);
            Assert.True(c.Tourism <= 2);
            Assert.True(c.YieldFactor > 0.7);
            Assert.True(c.LandPrice > 0.7);
            Assert.True(c.Profile.Length > 20);
        }
    }

    [Fact]
    public void Steillagen_teurere_Lese_und_Pflege_aber_mehr_Guete()
    {
        var (steep, flat) = (Estate(RegionId.Mittelrhein), Estate(RegionId.Rheinhessen));
        var (ps, pf) = (CurrentPlayer(steep), CurrentPlayer(flat));
        var (hs, hf) = (Game.HarvestPreview(steep, ps, ps.Plots[0]), Game.HarvestPreview(flat, pf, pf.Plots[0]));
        Assert.True(hs.Cost > hf.Cost);
        Assert.True(Market.MonthlyCosts(ps, 3).Upkeep > Market.MonthlyCosts(pf, 3).Upkeep);
        // Die Eignung für Riesling unterscheidet sich (Mittelrhein +2, Rheinhessen +1) – die Steillage legt noch etwas drauf
        Assert.True(hs.Quality - hf.Quality > 6);
        // Rheinhessen trägt deutlich mehr
        Assert.True(hf.Hectoliters > hs.Hectoliters * 1.2);
    }

    [Fact]
    public void Renommee_hebt_die_Preise_Tourismus_die_Nachfrage()
    {
        var (famous, hidden) = (Estate(RegionId.Mosel), Estate(RegionId.SaaleUnstrut));
        Assert.True(Market.BottlePrice(TestLot, CurrentPlayer(famous), famous) > Market.BottlePrice(TestLot, CurrentPlayer(hidden), hidden) * 1.1);
        var (busy, quiet) = (Estate(RegionId.Pfalz), Estate(RegionId.Nahe));
        CloseTo(Market.MonthlyDemand(CurrentPlayer(quiet)) * 1.2, Market.MonthlyDemand(CurrentPlayer(busy)), -1);
    }

    [Fact]
    public void Der_Bodenpreis_bestimmt_Weinbergswerte_und_damit_den_Kreditrahmen()
    {
        var (dear, cheap) = (Estate(RegionId.Rheingau), Estate(RegionId.Sachsen));
        Assert.True(Market.AssetValue(CurrentPlayer(dear)) > Market.AssetValue(CurrentPlayer(cheap)));
        static double Parcel(RegionId regionId) => Auction.ParcelValue(3, null, null, 0, regionId, 3);
        Assert.True(Parcel(RegionId.Rheingau) > Parcel(RegionId.Sachsen) * 1.3);
    }
}
