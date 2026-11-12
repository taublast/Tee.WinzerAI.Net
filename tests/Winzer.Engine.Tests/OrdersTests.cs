using static Winzer.Engine.Tests.TestHelpersB;

namespace Winzer.Engine.Tests;

/// <summary>orders.test.ts – Kundenaufträge</summary>
public class OrdersTests
{
    /// <summary>Jahr 2, Juni, mit zwei geprüften Riesling-Posten im Lager</summary>
    static GameState Stocked()
    {
        var s = Game.NewGame(Solo(2, RegionId.Mosel));
        s.Year = 2;
        s.Month = 6;
        BottleLot MakeLot(string id, double quality, double bottles) =>
            Lot(id, GrapeId.Riesling, 1, 85, QualityLevel.Spaetlese, quality, bottles);
        Current(s).Bottles = [MakeLot("gut", 80, 800), MakeLot("einfach", 72, 600)];
        return s;
    }

    static CustomerOrder Order(GameState s, Action<CustomerOrder>? overrides = null)
    {
        var order = new CustomerOrder
        {
            Id = "o1", Customer = "Grandhotel Sonnenkamm", Icon = "🏨", Grape = GrapeId.Riesling, Color = null, Sparkling = false,
            MinLevel = QualityLevel.Kabinett, MinQuality = 70, BioLabel = null, Bottles = 1000, PricePerBottle = 10, Points = 12, Reputation = 3,
            Deadline = Bio.GameMonth(s) + 3, OfferExpires = Bio.GameMonth(s) + 1,
        };
        overrides?.Invoke(order);
        return order;
    }

    [Fact]
    public void Anfragen_gibt_es_erst_ab_dem_zweiten_Jahr_hoechstens_drei_gleichzeitig()
    {
        var first = Game.NewGame(Solo(1, RegionId.Mosel, randomEvents: null));
        for (var i = 0; i < 50; i++) Orders.MaybeOfferOrder(first, Current(first));
        Assert.Empty(Current(first).OrderOffers);

        var s = Stocked();
        for (var i = 0; i < 50; i++) Orders.MaybeOfferOrder(s, Current(s));
        var offers = Current(s).OrderOffers;
        Assert.Equal(3, offers.Count);
        foreach (var o in offers)
        {
            Assert.True(o.Bottles >= 200);
            Assert.True(o.PricePerBottle > 0);
            Assert.True(o.Deadline > o.OfferExpires);
            Assert.DoesNotMatch("undefined|NaN", Orders.DescribeWine(o));
        }
    }

    [Fact]
    public void Angenommen_und_geliefert_Geld_Punkte_Bekanntheit_die_einfachsten_Flaschen_zuerst()
    {
        var s = Stocked();
        var player = Current(s);
        player.OrderOffers = [Order(s)];
        var money = player.Money;
        var t = Game.ApplyAction(s, new GameAction.AcceptOrder("o1"));
        Assert.Single(Current(t).Orders);
        t = Game.ApplyAction(t, new GameAction.DeliverOrder("o1"));
        var after = Current(t);
        Assert.Empty(after.Orders);
        Assert.Equal(money + 10_000, after.Money);
        Assert.Equal(12.0, after.Points);
        // 600 einfache und 400 gute Flaschen gingen raus
        Assert.Equal(new[] { ("gut", 400.0) }, after.Bottles.Select(l => (l.Id, l.Bottles)));
    }

    [Fact]
    public void Geliefert_wird_nur_mit_genug_passenden_Flaschen()
    {
        var s = Stocked();
        Current(s).Orders = [Order(s, o => o.MinQuality = 75)]; // nur der gute Posten passt (800 Fl.)
        AssertRule(() => Game.ApplyAction(s, new GameAction.DeliverOrder("o1")), "Nur 800 von 1000");
        Current(s).Orders = [Order(s, o => o.Grape = GrapeId.Spaetburgunder)];
        AssertRule(() => Game.ApplyAction(s, new GameAction.DeliverOrder("o1")), "Nur 0 von 1000");
        Current(s).Orders = [Order(s, o => o.BioLabel = BioLabel.Basis)];
        AssertRule(() => Game.ApplyAction(s, new GameAction.DeliverOrder("o1")), "Bio-Siegel");
    }

    [Fact]
    public void Hoechstens_drei_Auftraege_gleichzeitig()
    {
        var s = Stocked();
        var player = Current(s);
        player.Orders = [Order(s, o => o.Id = "a"), Order(s, o => o.Id = "b"), Order(s, o => o.Id = "c")];
        player.OrderOffers = [Order(s, o => o.Id = "d")];
        AssertRule(() => Game.ApplyAction(s, new GameAction.AcceptOrder("d")), "Höchstens 3");
    }

    [Fact]
    public void Wer_den_Termin_verpasst_zahlt_Vertragsstrafe()
    {
        var s = Stocked();
        var o = Order(s, x =>
        {
            x.Grape = GrapeId.Spaetburgunder;
            x.Deadline = Bio.GameMonth(s);
        });
        Current(s).Orders = [o];
        var money = Current(s).Money;
        var t = Game.ApplyAction(s, EndTurn);
        var after = Current(t);
        Assert.Empty(after.Orders);
        Assert.Contains(t.Log, e => e.Text.Contains("Vertragsstrafe") && e.Tone == LogTone.Bad);
        Assert.True(after.Money < money - Js.Round(Orders.OrderValue(o) * 0.15) + 1);
    }

    [Fact]
    public void Anfragen_verfallen_nach_zwei_Monaten()
    {
        var s = Stocked();
        s.RandomEvents = false;
        Current(s).OrderOffers = [Order(s, o => o.OfferExpires = Bio.GameMonth(s) + 1)];
        s = Game.ApplyAction(s, EndTurn);
        Assert.Contains(Current(s).OrderOffers, o => o.Id == "o1");
        s = Game.ApplyAction(s, EndTurn);
        Assert.DoesNotContain(Current(s).OrderOffers, o => o.Id == "o1");
    }

    [Fact]
    public void Computergegner_beliefern_Kunden_wenn_sie_genug_Wein_haben_und_nehmen_nichts_an_was_sie_nicht_liefern_koennen()
    {
        var s = Game.NewGame(new NewGameOptions
        {
            Seed = 1,
            TargetPoints = Js.MaxSafeInteger,
            Players = [new NewPlayer { Name = "KI", RegionId = RegionId.Pfalz, Ai = AiProfileId.Bio }],
        });
        var delivered = 0;
        while (s.Year <= 8)
        {
            // Das Logbuch ist begrenzt – neue Einträge Zug für Zug zählen
            var seen = s.Log.Count;
            var last = s.Log.Count > 0 ? s.Log[^1].Text : null;
            s = Ai.PlayAiTurn(s);
            var start = seen >= 400 ? s.Log.Select(e => e.Text).ToList().LastIndexOf(last!) + 1 : seen;
            delivered += s.Log.Skip(start).Count(e => e.Text.Contains(" geliefert: "));
            Assert.Empty(s.Players[0].Orders);
        }
        Assert.True(delivered > 3, $"geliefert: {delivered}");
    }
}
