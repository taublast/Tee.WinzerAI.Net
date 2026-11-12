using static Winzer.Engine.Tests.TestHelpersB;

namespace Winzer.Engine.Tests;

/// <summary>products.test.ts – Rosé, Federweißer, Straußwirtschaft</summary>
public class ProductsTests
{
    /// <summary>Ahr: Spätburgunder als Startsorte, also rote Trauben – im September des ersten Jahres</summary>
    static GameState September() =>
        EndMonths(Game.NewGame(Solo(1, RegionId.Ahr, false, new StartSetup { Money = 300_000 })), 8);

    static BottleLot RoseLot() => Lot("r", GrapeId.Spaetburgunder, 1, 80, QualityLevel.Kabinett, 70, 1000, rose: true);

    // Rosé

    [Fact]
    public void Rote_Trauben_lassen_sich_als_Rose_lesen_er_ist_nach_drei_Monaten_fertig()
    {
        var s = EndMonths(September(), 1); // Oktober
        var plotId = Current(s).Plots[0].Id;
        s = Game.ApplyAction(s, new GameAction.Harvest(plotId, Rose: true));
        var batch = Current(s).Cellar[0];
        Assert.True(batch.Rose);
        Assert.Equal(3, batch.MonthsUntilReady);
        Assert.Contains("Spätburgunder Rosé", Rules.WineLabel(batch));
        AssertRule(() => Game.ApplyAction(s, new GameAction.ToBarrique(batch.Id)), "Barrique");
    }

    [Fact]
    public void Rose_nur_aus_roten_Trauben()
    {
        var s = Game.NewGame(Solo(1, RegionId.Mosel));
        var october = EndMonths(s, 9);
        AssertRule(() => Game.ApplyAction(october, new GameAction.Harvest(Current(october).Plots[0].Id, Rose: true)), "roten Trauben");
    }

    [Fact]
    public void Rose_folgt_seinem_eigenen_Trend_und_ist_im_Sommer_teurer()
    {
        var s = September();
        var player = Current(s);
        var roseLot = RoseLot();
        s.Trends[TrendKey.Rose].Value = 1;
        s.Month = 3;
        var spring = Market.BottlePrice(roseLot, player, s);
        s.Month = 7;
        Assert.Equal(spring * 1.15, Market.BottlePrice(roseLot, player, s), 1);
        s.Trends[TrendKey.Rose].Value = 1.2;
        s.Trends[TrendKey.Spaetburgunder].Value = 0.8;
        Assert.Equal(spring * 1.15 * 1.2, Market.BottlePrice(roseLot, player, s), 1);
    }

    [Fact]
    public void Rose_zaehlt_nicht_fuer_Kundenauftraege_ueber_Rotwein()
    {
        var s = September();
        var player = Current(s);
        player.Bottles = [RoseLot()];
        var order = new CustomerOrder
        {
            Id = "o", Customer = "Test", Icon = "", Grape = null, Color = GrapeColor.Rot, Sparkling = false,
            MinLevel = QualityLevel.Qualitaetswein, MinQuality = 50, BioLabel = null, Bottles = 100, PricePerBottle = 5, Points = 3,
            Reputation = 1, Deadline = 99, OfferExpires = 99,
        };
        Assert.Empty(Orders.OrderEligible(player, order));
    }

    // Federweißer

    [Fact]
    public void Frischer_Most_wird_im_Herbst_direkt_verkauft_so_viel_wie_die_Kundschaft_trinkt()
    {
        var s = September();
        s = Game.ApplyAction(s, new GameAction.Harvest(Current(s).Plots[0].Id));
        var batch = Current(s).Cellar[0];
        var capacity = Products.FederweisserCapacity(Current(s));
        Assert.True(capacity > 5);
        AssertRule(() => Game.ApplyAction(s, new GameAction.SellFederweisser(batch.Id, capacity + 1)), "nur noch");
        var money = Current(s).Money;
        s = Game.ApplyAction(s, new GameAction.SellFederweisser(batch.Id, capacity));
        Assert.Equal(money + capacity * 250 * 0.95, Current(s).Money); // Normal: −5 % auf Erlöse
        Assert.Equal(0.0, Products.FederweisserCapacity(Current(s)));
        Assert.Contains("Roter Rauscher", s.Log[^1].Text);
        s = EndMonths(s, 1);
        Assert.Equal(capacity, Products.FederweisserCapacity(Current(s))); // neuer Monat, neue Kundschaft
    }

    [Fact]
    public void Federweisser_nur_im_September_und_Oktober_und_nur_aus_frischem_Most()
    {
        var s = September();
        s = Game.ApplyAction(s, new GameAction.Harvest(Current(s).Plots[0].Id));
        var batchId = Current(s).Cellar[0].Id;
        var later = EndMonths(s, 2); // November
        AssertRule(() => Game.ApplyAction(later, new GameAction.SellFederweisser(batchId, 1)), "September und Oktober");
    }

    // Straußwirtschaft

    [Fact]
    public void Strausswirtschaft_oeffnet_einmal_im_Jahr_fuer_vier_Monate_und_bringt_mehr_Gaeste()
    {
        var s = EndMonths(Game.NewGame(Solo(1, RegionId.Pfalz, false, new StartSetup { Money = 300_000 })), 4); // Mai
        var demand = Market.MonthlyDemand(Current(s));
        s = Game.ApplyAction(s, new GameAction.OpenStrausswirtschaft());
        Assert.True(Market.MonthlyDemand(Current(s)) > demand * 1.3);
        AssertRule(() => Game.ApplyAction(s, new GameAction.OpenStrausswirtschaft()), "schon geöffnet");
        s = EndMonths(s, 4);
        Assert.DoesNotContain(Current(s).Effects, e => e.Label.StartsWith("Straußwirtschaft", StringComparison.Ordinal));
    }

    [Fact]
    public void Strausswirtschaft_nicht_im_Winter()
    {
        var s = Game.NewGame(Solo(1, RegionId.Pfalz));
        AssertRule(() => Game.ApplyAction(s, new GameAction.OpenStrausswirtschaft()), "März und Oktober");
    }
}
