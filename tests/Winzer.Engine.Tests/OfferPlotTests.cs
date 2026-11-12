using Winzer.Engine.Data;

namespace Winzer.Engine.Tests;

/// <summary>Erweiterte Regeln: eine eigene Parzelle zur nächsten Versteigerung geben.</summary>
public class OfferPlotTests
{
    static GameState NewGame(int rules = 2, bool rival = false)
    {
        var players = new List<NewPlayer> { new() { Name = "Anna", RegionId = RegionId.Mosel } };
        if (rival) players.Add(new NewPlayer { Name = "Rebenhain", RegionId = RegionId.Mosel, Ai = AiProfileId.Tradition });
        return Game.NewGame(new NewGameOptions { Seed = 3, RulesVersion = rules, TargetPoints = Js.MaxSafeInteger, Players = players });
    }

    static GameState Step(GameState state) => Ai.RunAiTurns(Game.ApplyAction(state, new GameAction.EndTurn()));

    /// <summary>Spielt weiter, bis die Versteigerung die Phase erreicht; Lose aus dem Umland lässt Anna vorbeiziehen.</summary>
    static GameState Until(GameState state, AuctionPhase phase)
    {
        for (var i = 0; i < 60 && state.Auction?.Phase != phase; i++) state = Step(state);
        Assert.Equal(phase, state.Auction?.Phase);
        return state;
    }

    static GameState PassAll(GameState state)
    {
        for (var i = 0; i < 50 && Auction.AwaitingLot(state) is { } lot; i++) state = Game.ApplyAction(state, new GameAction.AuctionPass(lot.Id));
        return state;
    }

    static AuctionLot OwnLot(GameState state) => Assert.Single(state.Auction!.Lots, l => l.SellerId == state.Players[0].Id);

    [Fact]
    public void Originalregeln_kennen_das_Einliefern_nicht()
    {
        var state = NewGame(rules: 1);
        Assert.NotNull(Game.ValidateAction(state, new GameAction.OfferPlot(state.Players[0].Plots[0].Id)));
    }

    [Fact]
    public void Angemeldete_Parzelle_kommt_als_Los_mit_hoeherem_Ausruf_in_die_naechste_Versteigerung()
    {
        var state = NewGame();
        var plot = state.Players[0].Plots[0];
        state = Game.ApplyAction(state, new GameAction.OfferPlot(plot.Id));
        Assert.True(state.Players[0].Plots[0].ForAuction);
        // Solange nichts angekündigt ist, lässt sich die Anmeldung zurückziehen und wieder setzen
        state = Game.ApplyAction(state, new GameAction.WithdrawPlot(plot.Id));
        Assert.False(state.Players[0].Plots[0].ForAuction);
        state = Game.ApplyAction(state, new GameAction.OfferPlot(plot.Id));

        state = Until(state, AuctionPhase.Announced);
        var lot = OwnLot(state);
        Assert.Equal(plot.Hectares, lot.Hectares);
        Assert.Equal(plot.Grape, lot.Grape);
        Assert.Equal(RegionId.Mosel, lot.RegionId);
        // Ausruf bei 80 % statt 70 % des Schätzwerts
        Assert.Equal(Js.Round(lot.Estimate * Economy.PlotOfferStartShare / 1000) * 1000, lot.StartPrice);
        // Angekündigt ist angekündigt
        Assert.NotNull(Game.ValidateAction(state, new GameAction.WithdrawPlot(plot.Id)));
    }

    [Fact]
    public void Angemeldete_Parzelle_laesst_sich_weder_verkaufen_noch_roden()
    {
        var state = NewGame();
        var plot = state.Players[0].Plots.First(p => p.Grape is not null);
        state.Players[0].Money = 500_000;
        state = Game.ApplyAction(state, new GameAction.OfferPlot(plot.Id));
        Assert.Equal(RulesV2.OfferedText, Game.ValidateAction(state, new GameAction.SellPlot(plot.Id)));
        Assert.Equal(RulesV2.OfferedText, Game.ValidateAction(state, new GameAction.ClearPlot(plot.Id)));
        Assert.Equal(RulesV2.OfferedText, Game.ValidateAction(state, new GameAction.OfferPlot(plot.Id)));
    }

    [Fact]
    public void Eine_Parzelle_bleibt_immer_beim_Gut()
    {
        var state = NewGame();
        var plots = state.Players[0].Plots;
        for (var i = 0; i < plots.Count - 1; i++) state = Game.ApplyAction(state, new GameAction.OfferPlot(plots[i].Id));
        var last = plots[^1].Id;
        Assert.NotNull(Game.ValidateAction(state, new GameAction.OfferPlot(last)));
        // Auch der Direktverkauf darf die letzte nicht angemeldete Parzelle nicht nehmen
        Assert.NotNull(Game.ValidateAction(state, new GameAction.SellPlot(last)));
    }

    [Fact]
    public void Zuschlag_bringt_dem_Einlieferer_den_Erloes_und_er_bietet_selbst_nicht_mit()
    {
        var state = NewGame();
        var plot = state.Players[0].Plots[0];
        var count = state.Players[0].Plots.Count;
        state = Game.ApplyAction(state, new GameAction.OfferPlot(plot.Id));
        state = Until(state, AuctionPhase.Announced);
        // Ein auswärtiger Investor bietet sicher mit, und zwar über dem Schätzwert
        var lot = OwnLot(state);
        lot.Limits = new Dictionary<string, double> { [Auction.Investor] = lot.Estimate * 1.2 };
        var lotId = lot.Id;

        state = Until(state, AuctionPhase.Live);
        // Anna wird für ihr eigenes Los nie gefragt
        for (var i = 0; i < 50 && Auction.AwaitingLot(state) is { } open; i++)
        {
            Assert.NotEqual(lotId, open.Id);
            state = Game.ApplyAction(state, new GameAction.AuctionPass(open.Id));
        }
        var sold = state.Auction!.Lots.Find(l => l.Id == lotId)!;
        Assert.Equal(LotStatus.Sold, sold.Status);
        Assert.Equal(Auction.Investor, sold.Leader);
        // Einziger Bieter: Zuschlag zum Ausrufpreis
        Assert.Equal(sold.StartPrice, sold.Price);
        var anna = state.Players[0];
        Assert.Equal(count - 1, anna.Plots.Count);
        Assert.DoesNotContain(anna.Plots, p => p.Id == plot.Id);
        // Der Erlös steht im eigenen Logbuch (das Konto ändert sich im selben Monat auch durch laufende Kosten)
        Assert.Contains(state.Log, e => e.PlayerId == anna.Id && e.Text.Contains($"versteigert: +{Util.FormatEuro(sold.Price)}"));
    }

    [Fact]
    public void Ohne_Gebot_bleibt_die_Parzelle_und_die_Anmeldung_erlischt()
    {
        var state = NewGame();
        var plot = state.Players[0].Plots[0];
        state = Game.ApplyAction(state, new GameAction.OfferPlot(plot.Id));
        state = Until(state, AuctionPhase.Announced);
        var lot = OwnLot(state);
        lot.Limits = [];
        var lotId = lot.Id;
        state = PassAll(Until(state, AuctionPhase.Live));

        Assert.Equal(LotStatus.Unsold, state.Auction!.Lots.Find(l => l.Id == lotId)!.Status);
        var kept = state.Players[0].Plots.Find(p => p.Id == plot.Id);
        Assert.NotNull(kept);
        Assert.False(kept.ForAuction);
        // Danach geht es normal weiter, die nächste Versteigerung enthält das Los nicht mehr
        state = Until(Step(Step(state)), AuctionPhase.Announced);
        Assert.DoesNotContain(state.Auction!.Lots, l => l.SellerId is not null);
    }

    [Fact]
    public void Zwei_Bieter_treiben_den_Preis_ueber_den_Direktverkauf()
    {
        var state = NewGame(rival: true);
        var plot = state.Players[0].Plots[0];
        var direct = Game.PlotSalePrice(state.Players[0], plot, state.Year);
        state = Game.ApplyAction(state, new GameAction.OfferPlot(plot.Id));
        state = Until(state, AuctionPhase.Announced);
        var lot = OwnLot(state);
        var rival = state.Players[1];
        rival.Money = 2_000_000;
        lot.Limits = new Dictionary<string, double> { [Auction.Investor] = lot.Estimate * 1.1, [rival.Id] = lot.Estimate * 1.3 };
        var lotId = lot.Id;
        var plotsBefore = rival.Plots.Count;

        state = PassAll(Until(state, AuctionPhase.Live));
        var sold = state.Auction?.Lots.Find(l => l.Id == lotId);
        // Der Monat der Versteigerung kann schon vorbei sein – dann steht das Ergebnis im Logbuch
        var entry = state.Log.FindLast(e => e.PlayerId == state.Players[0].Id && e.Text.Contains("versteigert: +"));
        Assert.NotNull(entry);
        if (sold is not null)
        {
            Assert.Equal(rival.Id, sold.Leader);
            Assert.True(sold.Price > direct);
        }
        Assert.Equal(plotsBefore + 1, state.Players[1].Plots.Count);
        Assert.DoesNotContain(state.Players[0].Plots, p => p.Id == plot.Id);
    }
}
