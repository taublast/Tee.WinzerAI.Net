using Winzer.Engine.Data;
using static Winzer.Engine.Tests.TestHelpersA;

namespace Winzer.Engine.Tests;

/// <summary>auction.test.ts</summary>
public class AuctionTests
{
    static NewPlayer Anna => Human("Anna", RegionId.Mosel);
    static NewPlayer Rebenhain => Computer("Rebenhain", RegionId.Mosel, AiProfileId.Tradition);

    static GameState NewGameWith(List<NewPlayer> players, int seed = 3, StartSetup? start = null) =>
        Ai.RunAiTurns(Game.NewGame(new NewGameOptions { Seed = seed, Players = players, Start = start }));

    /// <summary>Beendet Monate, bis die Versteigerung die gewünschte Phase erreicht hat</summary>
    static GameState Until(GameState state, AuctionPhase phase)
    {
        var s = state;
        // Bei mehreren Menschen beendet ein endTurn nur den Zug – daher großzügig viele Durchläufe
        for (var i = 0; i < 100 && s.Auction?.Phase != phase; i++) s = Ai.RunAiTurns(Game.ApplyAction(s, EndTurn));
        Assert.Equal(phase, s.Auction?.Phase);
        return s;
    }

    /// <summary>Live-Versteigerung, bei der Anna am Zug ist, mit selbst gesetzten Limits der Gegner</summary>
    static (GameState S, AuctionLot Lot) LiveFor(Dictionary<string, double>? limits = null, List<NewPlayer>? extraPlayers = null)
    {
        var s = Until(NewGameWith([Anna, .. extraPlayers ?? []]), AuctionPhase.Live);
        var lot = Auction.AwaitingLot(s)!;
        lot.Limits = new Dictionary<string, double>(limits ?? []);
        s.Players[0].Money = Math.Max(s.Players[0].Money, 1_000_000); // genug Spielraum für jedes Testgebot
        return (s, lot);
    }

    static AuctionLot? LotOf(GameState s, string id) => s.Auction?.Lots.Find(l => l.Id == id);

    /// <summary>Der Weinberg, wie er nach dem Zuschlag im Logbuch steht</summary>
    static string? SoldTo(GameState s, AuctionLot lot) =>
        s.Log.Find(e => e.Text.StartsWith($"🔨 Zuschlag: {lot.Name}", StringComparison.Ordinal))?.Text;

    static void SoldToContains(string text, GameState s, AuctionLot lot)
    {
        var entry = SoldTo(s, lot);
        Assert.NotNull(entry);
        Assert.Contains(text, entry);
    }

    [Fact]
    public void Findet_in_zufaelligen_Abstaenden_von_8_bis_16_Monaten_statt()
    {
        var s = Game.NewGame(new NewGameOptions { Seed = 11, TargetPoints = Js.MaxSafeInteger, Players = [Rebenhain] });
        var months = new List<int>();
        while (s.Year <= 12 && !s.Finished)
        {
            var before = s.Auction?.Phase;
            s = Ai.PlayAiTurn(s);
            if (s.Auction?.Phase == AuctionPhase.Live && before != AuctionPhase.Live) months.Add((s.Year - 1) * 12 + s.Month);
        }
        var gaps = new List<int> { months[0] - 1 };
        for (var i = 1; i < months.Count; i++) gaps.Add(months[i] - months[i - 1]);
        Assert.True(gaps.Count > 6);
        foreach (var gap in gaps)
        {
            Assert.True(gap >= Economy.AuctionInterval.Min);
            Assert.True(gap <= Economy.AuctionInterval.Max);
        }
        Assert.True(gaps.Distinct().Count() > 2);
    }

    [Fact]
    public void Wird_einen_Monat_vorher_mit_1_bis_3_Lagen_je_Gebiet_angekuendigt()
    {
        var s = Until(NewGameWith([Anna, Human("Cleo", RegionId.Pfalz)]), AuctionPhase.Announced);
        var auction = s.Auction!;
        Assert.Equal((s.Month % 12) + 1, auction.Month);
        foreach (var regionId in new[] { RegionId.Mosel, RegionId.Pfalz })
        {
            var count = auction.Lots.Count(l => l.RegionId == regionId);
            Assert.True(count >= 1);
            Assert.True(count <= 3);
        }
        foreach (var lot in auction.Lots)
        {
            Assert.True(lot.StartPrice < lot.Estimate);
            Assert.Equal(LotStatus.Pending, lot.Status);
        }
        var live = Until(s, AuctionPhase.Live);
        Assert.Equal("p1", Auction.AwaitingLot(live)?.Awaiting);
    }

    [Fact]
    public void Wer_am_meisten_bietet_bekommt_den_Zuschlag_auch_weit_ueber_dem_Schaetzwert()
    {
        var (s, lot) = LiveFor();
        lot.Limits = new Dictionary<string, double> { [Auction.Investor] = Js.Round((lot.Estimate * 1.3) / 1000) * 1000 };
        s.Players[0].Money = 1_000_000;
        var offer = Js.Round((lot.Estimate * 1.6) / 1000) * 1000;
        var after = Game.ApplyAction(s, new GameAction.AuctionBid(lot.Id, offer));

        Assert.Equal(LotStatus.Sold, LotOf(after, lot.Id)?.Status);
        SoldToContains("an Anna", after, lot);
        Assert.Contains(after.Players[0].Plots, p => p.Name == lot.Name);
        Assert.Equal(1_000_000 - offer, after.Players[0].Money);
    }

    [Fact]
    public void Gegner_ueberbieten_Schritt_fuer_Schritt_bis_zu_ihrem_Limit()
    {
        var (s, lot) = LiveFor();
        var limit = lot.StartPrice + 3 * lot.Increment;
        s.Auction!.Lots.Find(l => l.Id == lot.Id)!.Limits = new Dictionary<string, double> { [Auction.Investor] = limit };
        var t = Game.ApplyAction(s, new GameAction.AuctionBid(lot.Id, lot.StartPrice));
        var current = LotOf(t, lot.Id)!;
        Assert.Equal(Auction.Investor, current.Leader);
        Assert.Equal(lot.StartPrice + lot.Increment, current.Price);
        Assert.Equal("p1", current.Awaiting);

        // Ein Schritt über das Limit genügt
        t = Game.ApplyAction(t, new GameAction.AuctionBid(lot.Id, Auction.NextMinimum(current)));
        current = LotOf(t, lot.Id)!;
        Assert.Equal(Auction.Investor, current.Leader);
        Assert.Equal(limit, current.Price);
        t = Game.ApplyAction(t, new GameAction.AuctionBid(lot.Id, Auction.NextMinimum(current)));
        SoldToContains("an Anna", t, lot);
    }

    [Fact]
    public void Steigt_man_aus_entscheidet_das_zweithoechste_Limit_ueber_den_Preis()
    {
        var (s, lot) = LiveFor();
        var (high, low) = (lot.Estimate + 20_000, lot.Estimate);
        s.Auction!.Lots.Find(l => l.Id == lot.Id)!.Limits = new Dictionary<string, double> { [Auction.Investor] = high, ["p2"] = low };
        var fuchs = s.Players[0].Clone();
        fuchs.Id = "p2";
        fuchs.Name = "Fuchs";
        fuchs.Ai = AiProfileId.Schlitzohr;
        fuchs.Money = 1_000_000;
        s.Players.Add(fuchs);
        var after = Game.ApplyAction(s, new GameAction.AuctionPass(lot.Id));
        SoldToContains("an auswärtige Investoren", after, lot);
        SoldToContains(Util.FormatNumber(low + lot.Increment), after, lot);
        Assert.DoesNotContain(after.Players[0].Plots, p => p.Name == lot.Name);
    }

    [Fact]
    public void Ohne_Gebote_bleibt_ein_Weinberg_unverkauft()
    {
        var (s, lot) = LiveFor();
        var after = Game.ApplyAction(s, new GameAction.AuctionPass(lot.Id));
        Assert.Equal(LotStatus.Unsold, LotOf(after, lot.Id)?.Status ?? LotStatus.Unsold);
    }

    [Fact]
    public void Prueft_Mindestgebot_und_Finanzierbarkeit()
    {
        var (s, lot) = LiveFor();
        Action Bid(double amount) => () => Game.ApplyAction(s, new GameAction.AuctionBid(lot.Id, amount));
        Throws("mindestens", Bid(lot.StartPrice - 1000));
        Throws("nicht finanzierbar", Bid(10_000_000));
        Bid(lot.StartPrice)(); // wirft nicht
    }

    [Fact]
    public void Am_Monatsende_wird_ohne_weitere_Gebote_zugeschlagen()
    {
        var (s, lot) = LiveFor();
        var t = Game.ApplyAction(s, new GameAction.AuctionBid(lot.Id, lot.StartPrice));
        t = Ai.RunAiTurns(Game.ApplyAction(t, EndTurn));
        Assert.Null(t.Auction);
        SoldToContains("an Anna", t, lot);
    }

    [Fact]
    public void Mehrere_Menschen_im_selben_Gebiet_bieten_abwechselnd()
    {
        var (s, lot) = LiveFor([], [Human("Ben", RegionId.Mosel)]);
        Assert.Equal("p1", lot.Awaiting);
        var t = Game.ApplyAction(s, new GameAction.AuctionBid(lot.Id, lot.StartPrice));
        Assert.Equal("p2", LotOf(t, lot.Id)?.Awaiting);
        t = Game.ApplyAction(t, new GameAction.AuctionBid(lot.Id, lot.StartPrice + lot.Increment));
        Assert.Equal("p1", LotOf(t, lot.Id)?.Awaiting);
        t = Game.ApplyAction(t, new GameAction.AuctionPass(lot.Id));
        SoldToContains("an Ben", t, lot);
    }

    [Fact]
    public void Computergegner_hinterlegen_im_Ankuendigungsmonat_ein_Hoechstgebot_Menschen_nicht()
    {
        var s = Until(
            NewGameWith([Anna, Rebenhain], 5, new StartSetup { Plots = StartPlots.Planted, Money = 400_000 }),
            AuctionPhase.Announced);
        var lot = s.Auction!.Lots[0];
        // Anna ist am Zug; die KI zieht erst nach ihr
        Throws("live", () => Game.ApplyAction(s, new GameAction.AuctionLimit(lot.Id, lot.StartPrice)));
        var aiTurn = Game.ApplyAction(s, EndTurn);
        Assert.Equal("p2", CurrentPlayer(aiTurn).Id);
        CurrentPlayer(aiTurn).Money = 2_000_000; // genug Geld, damit die KI sicher mitbietet
        var afterAi = Ai.PlayAiTurn(aiTurn);
        foreach (var l in afterAi.Auction!.Lots)
        {
            Assert.True(l.Limits.ContainsKey("p2"), $"kein Höchstgebot von p2 für {l.Name}");
            Assert.Equal(Js.Round((l.Estimate * 1.3) / 1000) * 1000, l.Limits["p2"]); // Traditionswinzer: 130 % des Schätzwerts
        }
    }
}
