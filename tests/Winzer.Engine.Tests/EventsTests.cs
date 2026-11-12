using static Winzer.Engine.Tests.TestHelpersB;

namespace Winzer.Engine.Tests;

/// <summary>events.test.ts – Zufallsereignisse</summary>
public class EventsTests
{
    /// <summary>Ein gut laufendes Weingut nach vier Jahren: Wein im Tank, Flaschen im Lager, Reben im Ertrag</summary>
    static readonly Lazy<GameState> Base = new(() => SimulateSolo(7, 4, RegionId.Pfalz, AiProfileId.Tradition).State);

    /// <summary>Bereitet den Spielstand so vor, dass das Ereignis eintreten kann</summary>
    static GameState Prepared(GameState @base, int[]? months = null)
    {
        var s = @base.Clone();
        s.Month = months is { Length: > 0 } ? months[0] : 6;
        var player = Current(s);
        foreach (var plot in player.Plots) plot.HarvestedThisYear = false;
        // Eine Junganlage für Ereignisse, die nur junge Reben betreffen
        var young = player.Plots[0].Clone();
        young.Id = "young";
        young.Name = "Junganlage";
        young.PlantedYear = s.Year;
        player.Plots.Add(young);
        // Ein großer, geprüfter Flaschenposten für Ereignisse rund ums Lager
        player.Bottles.Add(Lot("lot-x", GrapeId.Riesling, s.Year - 1, 85, QualityLevel.Kabinett, 75, 2000));
        return s;
    }

    static EstateEventDef Event(string id) => Events.EstateEventList.First(e => e.Id == id);

    /// <summary>Flache Kopie wie <c>{ ...s, rng, log, players }</c>: alles Übrige (Trends, Vorhersage …) bleibt geteilt</summary>
    static GameState ShallowCopy(GameState s, Rng rng, List<LogEntry> log, List<Player> players) => new()
    {
        Version = s.Version,
        RulesVersion = s.RulesVersion,
        Rng = rng,
        Year = s.Year,
        Month = s.Month,
        Players = players,
        CurrentPlayer = s.CurrentPlayer,
        Forecast = s.Forecast,
        LastWeather = s.LastWeather,
        TargetPoints = s.TargetPoints,
        WinnerId = s.WinnerId,
        Finished = s.Finished,
        Auction = s.Auction,
        MonthsToAuction = s.MonthsToAuction,
        RandomEvents = s.RandomEvents,
        CellarMasterCandidates = s.CellarMasterCandidates,
        Trends = s.Trends,
        BulkCrisisMonths = s.BulkCrisisMonths,
        ClimateChange = s.ClimateChange,
        YearLimit = s.YearLimit,
        Challenge = s.Challenge,
        Difficulty = s.Difficulty,
        NextId = s.NextId,
        Log = log,
    };

    [Fact]
    public void Hat_Ereignisse_in_allen_Seltenheitsstufen_gute_wie_schlechte()
    {
        foreach (var rarity in Events.RarityWeight.Keys)
        {
            var events = Events.EstateEventList.Where(e => e.Rarity == rarity).ToList();
            Assert.Contains(events, e => e.Tone == LogTone.Good);
            Assert.Contains(events, e => e.Tone == LogTone.Bad);
        }
        Assert.True(Events.EstateEventList.Count >= 40);
        Assert.Equal(Events.EstateEventList.Count, Events.EstateEventList.Select(e => e.Id).Distinct().Count());
    }

    public static TheoryData<string> EventIds()
    {
        var data = new TheoryData<string>();
        foreach (var e in Events.EstateEventList) data.Add(e.Id);
        return data;
    }

    [Theory]
    [MemberData(nameof(EventIds))]
    public void Ereignis_laesst_sich_ausloesen_und_meldet_sich_im_Logbuch(string id)
    {
        var @event = Event(id);
        var s = Prepared(Base.Value, @event.Months);
        var player = Current(s);
        Assert.Contains(@event, Events.PossibleEvents(s, player));
        var before = s.Log.Count;
        Events.TriggerEvent(s, player, @event);
        var entry = s.Log[before];
        Assert.Equal(@event.Tone, entry.Tone);
        Assert.True(entry.Text.Length > 20);
        Assert.DoesNotMatch("NaN|undefined", entry.Text);
    }

    [Fact]
    public void Tritt_in_rund_einem_Drittel_der_Monate_ein_drastische_Ereignisse_sehr_selten()
    {
        var s = Prepared(Base.Value);
        const int months = 4000;
        var events = 0;
        for (var i = 0; i < months; i++)
        {
            var copy = ShallowCopy(s, new Rng { State = i * 7919 }, [], [Current(s).Clone()]);
            Events.EstateEvents(copy, copy.Players[0]);
            if (copy.Log.Count > 0) events++;
        }
        Assert.True((double)events / months > Events.EventChance - 0.03, $"Anteil: {(double)events / months}");
        Assert.True((double)events / months < Events.EventChance + 0.03, $"Anteil: {(double)events / months}");

        // Anteil je Stufe an allen Ereignissen, die im Juni möglich sind
        var weights = Events.PossibleEvents(s, Current(s)).Select(e => (e.Rarity, W: Events.RarityWeight[e.Rarity])).ToList();
        var total = weights.Aggregate(0.0, (sum, e) => sum + e.W);
        double Share(Rarity rarity) => weights.Where(e => e.Rarity == rarity).Aggregate(0.0, (sum, e) => sum + e.W) / total;
        Assert.True(Share(Rarity.Alltag) > 0.6);
        Assert.True(Share(Rarity.SehrSelten) < 0.01);
        // Ein bestimmtes drastisches Ereignis: weniger als einmal in 100 Spieljahren
        Assert.True((Events.EventChance * Events.RarityWeight[Rarity.SehrSelten]) / total < 1.0 / 1200);
    }

    [Fact]
    public void Effekte_wirken_befristet_auf_Nachfrage_und_Kosten()
    {
        var s = Prepared(Base.Value);
        var player = Current(s);
        var demand = Market.MonthlyDemand(player);
        var costs = Market.MonthlyCosts(player, 6).Total;
        Events.TriggerEvent(s, player, Event("handelskette"));
        Events.TriggerEvent(s, player, Event("strom"));
        Assert.True(Math.Abs(Market.MonthlyDemand(player) - demand * 1.5) <= 1); // Nachfrage wird gerundet
        Assert.True(Market.MonthlyCosts(player, 6).Total > costs);

        for (var i = 0; i < 6; i++) s = Game.ApplyAction(s, EndTurn);
        var later = Current(s);
        Assert.DoesNotContain(later.Effects, e => e.Label.Contains("Handelskette"));
        Assert.Contains(s.Log, e => e.Text.Contains("Vorbei: Listung bei einer Handelskette"));
    }

    [Fact]
    public void Seltene_Ereignisse_erfahren_alle_am_Tisch()
    {
        var s = Prepared(Base.Value);
        Events.TriggerEvent(s, Current(s), Event("sturm"));
        Assert.Null(s.Log[^1].PlayerId);
        Events.TriggerEvent(s, Current(s), Event("pumpe"));
        Assert.Equal(Current(s).Id, s.Log[^1].PlayerId);
    }
}
