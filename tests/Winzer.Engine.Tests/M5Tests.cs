using Winzer.Engine.Data;
using static Winzer.Engine.Tests.TestHelpersA;

namespace Winzer.Engine.Tests;

/// <summary>m5.test.ts</summary>
public class M5Tests
{
    static GameState Start(bool? climateChange = null, RegionId regionId = RegionId.Mosel) =>
        Game.NewGame(new NewGameOptions { Seed = 42, ClimateChange = climateChange, Players = [Human("Anna", regionId)] });

    // --- Anbaugebiete und Rebsorten ---

    [Fact]
    public void Alle_13_Gebiete_bewerten_alle_11_Rebsorten()
    {
        var grapes = Grapes.All.Keys.ToList();
        Assert.Equal(13, Regions.All.Keys.Count);
        Assert.Equal(11, grapes.Count);
        foreach (var region in Regions.All.Values)
        {
            foreach (var grape in grapes) Assert.True(region.Suitability.ContainsKey(grape));
            Assert.True(Grapes.All.ContainsKey(region.StartGrape));
            Assert.True(region.Suitability[region.StartGrape] >= 1);
        }
    }

    [Fact]
    public void PIWI_Sorten_sind_kaum_krankheitsanfaellig_verkaufen_sich_aber_etwas_schlechter()
    {
        Assert.True(Grapes.All[GrapeId.Regent].DiseaseSusceptibility < Grapes.All[GrapeId.Spaetburgunder].DiseaseSusceptibility);
        var state = Start();
        foreach (var trend in state.Trends.Values) trend.Value = 1; // Markttrends ausblenden
        var player = CurrentPlayer(state);
        var lot = Lot("x", GrapeId.Regent, 1, 80, QualityLevel.Spaetlese, 70, 100);
        var lemberger = lot.Clone();
        lemberger.Grape = GrapeId.Lemberger;
        Assert.True(Market.BottlePrice(lot, player, state) < Market.BottlePrice(lemberger, player, state));
    }

    // --- Klimawandel ---

    [Fact]
    public void Klimawandel_laesst_die_Trauben_schneller_reifen_und_macht_Eiswein_seltener()
    {
        static GameState Late(bool climateChange)
        {
            var state = Start(climateChange);
            state.Year = 20; // zwanzig Jahre später
            return state;
        }
        Assert.True(Vineyard.IceWineChance(Late(true), RegionId.Mosel) < Vineyard.IceWineChance(Late(false), RegionId.Mosel));

        static double Ripeness(bool climateChange)
        {
            var state = Late(climateChange);
            return CurrentPlayer(EndMonths(state, 9)).Plots[0].Oechsle;
        }
        Assert.True(Ripeness(true) > Ripeness(false));
    }

    // --- Geldsenken ---

    static GameState Rich()
    {
        var state = Start();
        CurrentPlayer(state).Money = 500_000;
        return state;
    }

    [Fact]
    public void Barrique_bringt_Guete_und_laengere_Reife_braucht_aber_Platz()
    {
        var state = EndMonths(Start(regionId: RegionId.Ahr), 9);
        CurrentPlayer(state).Money = 200_000;
        state = Game.ApplyAction(state, new GameAction.Harvest(CurrentPlayer(state).Plots[0].Id));
        var batch = CurrentPlayer(state).Cellar[0];
        var harvested = state;
        Throws("Eichenfass", () => Game.ApplyAction(harvested, new GameAction.ToBarrique(batch.Id)));
        CurrentPlayer(state).CellarMaster = new CellarMaster { Name = "Test", Talent = 0, Skills = [CellarSkill.Barrique], Salary = 3000, HiredYear = 1 };
        Throws("Platz", () => Game.ApplyAction(harvested, new GameAction.ToBarrique(batch.Id)));

        state = Game.ApplyAction(state, new GameAction.ExpandBarrique(300));
        state = Game.ApplyAction(state, new GameAction.ToBarrique(batch.Id));
        var aged = CurrentPlayer(state).Cellar[0];
        Assert.True(aged.Barrique);
        Assert.Equal(Math.Min(100, batch.Quality + 6), aged.Quality); // Spätburgunder ist rot
        Assert.Equal(12, aged.MonthsUntilReady);
    }

    [Fact]
    public void Die_Vinothek_steigert_den_Ab_Hof_Absatz()
    {
        var state = Rich();
        var built = Game.ApplyAction(state, new GameAction.BuildVinothek());
        Assert.Equal(500_000 - Economy.VinothekCost, CurrentPlayer(built).Money);
        Assert.True(Market.MonthlyDemand(CurrentPlayer(built)) > Market.MonthlyDemand(CurrentPlayer(state)));
    }

    [Fact]
    public void Der_Grosshandel_zahlt_weniger_je_mehr_Flaschen_er_im_Jahr_abnimmt()
    {
        var state = Rich();
        var player = CurrentPlayer(state);
        var lot = Lot("x", GrapeId.Riesling, 1, 80, QualityLevel.Spaetlese, 80, 100);
        var fresh = Market.WholesalePrice(lot, player, state);
        player.WholesaleThisYear = 60_000;
        CloseTo(fresh * 0.7, Market.WholesalePrice(lot, player, state), 1);
    }

    // --- Tages-Challenge ---

    /// <summary>Die Tage ab dem 2026-10-01 – an aufeinanderfolgenden Tagen kommt jedes Szenario einmal dran</summary>
    static readonly List<string> Dates = Challenge.ChallengeScenarios.Select((_, i) => $"2026-10-{i + 1:00}").ToList();

    static string DateFor(string id) => Dates.First(d => Challenge.DailyChallenge(d).Scenario.Id == id);

    [Fact]
    public void Tages_Challenge_ist_fuer_alle_am_selben_Tag_gleich_und_wechselt_taeglich()
    {
        Assert.Equal(JsonOf(Challenge.DailyChallenge("2026-10-09")), JsonOf(Challenge.DailyChallenge("2026-10-09")));
        Assert.NotEqual(Challenge.DailyChallenge("2026-10-09").Options.Seed, Challenge.DailyChallenge("2026-10-10").Options.Seed);
        var scenarios = Dates.Select(d => Challenge.DailyChallenge(d).Scenario.Id).ToList();
        Assert.Equal(Challenge.ChallengeScenarios.Length, scenarios.Distinct().Count());
    }

    [Fact]
    public void Tages_Challenge_stellt_unterschiedliche_Gegner_und_Gebiete_auf()
    {
        var challenges = Enumerable.Range(0, 30).Select(i => Challenge.DailyChallenge($"2026-11-{i + 1:00}")).ToList();
        Assert.True(challenges.Select(c => c.Options.Players[0].RegionId).Distinct().Count() > 5);
        Assert.True(challenges
            .Select(c => string.Join(",", c.Options.Players.Skip(1).Select(p => p.Ai is { } ai ? Json.Name(ai) : "")))
            .Distinct().Count() > 5);
        foreach (var c in challenges)
        {
            var opponents = c.Options.Players.Skip(1).Select(p => p.Ai).ToList();
            Assert.Equal(opponents.Count, opponents.Distinct().Count());
            Assert.Equal(c.Scenario.Opponents, opponents.Count);
        }
    }

    [Fact]
    public void Tages_Challenge_uebernimmt_die_Startbedingungen_des_Szenarios_fuer_alle_Weingueter()
    {
        var fallow = Challenge.DailyChallenge(DateFor("brache"));
        var state = Game.NewGame(fallow.Options);
        foreach (var p in state.Players)
        {
            Assert.Equal(fallow.Scenario.Start!.Money, p.Money);
            Assert.True(p.Plots.All(plot => plot.Grape is null));
        }
        var debt = Game.NewGame(Challenge.DailyChallenge(DateFor("schuldenberg")).Options);
        Assert.Equal(60_000, debt.Players[0].Loan);
        Assert.Equal(30_000, debt.Players[0].Money);
    }

    public static TheoryData<string, string> ScenarioDates()
    {
        var data = new TheoryData<string, string>();
        foreach (var s in Challenge.ChallengeScenarios) data.Add(s.Name, DateFor(s.Id));
        return data;
    }

    [Theory]
    [MemberData(nameof(ScenarioDates))]
    public void Szenario_ist_spielbar_und_endet_puenktlich(string name, string date)
    {
        _ = name;
        var (_, scenario, options) = Challenge.DailyChallenge(date);
        // Ohne Menschen: Den eigenen Platz übernimmt ein Traditionswinzer.
        var state = Game.NewGame(new NewGameOptions
        {
            Seed = options.Seed,
            TargetPoints = options.TargetPoints,
            ClimateChange = options.ClimateChange,
            YearLimit = options.YearLimit,
            Challenge = options.Challenge,
            Difficulty = options.Difficulty,
            RandomEvents = options.RandomEvents,
            Start = options.Start,
            Players = options.Players
                .Select(p => new NewPlayer { Name = p.Name, RegionId = p.RegionId, Ai = p.Ai ?? AiProfileId.Tradition })
                .ToList(),
        });
        while (!state.Finished) state = Ai.PlayAiTurn(state);
        Assert.Equal(scenario.Years, state.Year);
        Assert.NotNull(state.WinnerId);
        Assert.False(state.Players[0].Bankrupt);
        Assert.True(state.Players[0].Points > 0);
    }

    // --- Schwierigkeitsgrad ---

    static readonly BottleLot DifficultyLot = Lot("x", GrapeId.Riesling, 1, 80, QualityLevel.Spaetlese, 80, 100);

    static GameState DifficultyGame(Difficulty difficulty) =>
        Game.NewGame(new NewGameOptions
        {
            Seed = 1, Difficulty = difficulty,
            Players = [Human("Anna", RegionId.Mosel), Computer("KI", RegionId.Mosel, AiProfileId.Tradition)],
        });

    [Fact]
    public void Schwierigkeitsgrad_wirkt_auf_Startkapital_Erloese_und_Kosten_der_Menschen()
    {
        var lot = DifficultyLot;
        var (easy, hard) = (DifficultyGame(Difficulty.Leicht), DifficultyGame(Difficulty.Schwierig));
        Assert.Equal(Economy.Difficulties[Difficulty.Leicht].StartMoney, easy.Players[0].Money);
        Assert.Equal(Economy.Difficulties[Difficulty.Schwierig].StartMoney, hard.Players[0].Money);
        Assert.True(Market.BottlePrice(lot, hard.Players[0], hard) < Market.BottlePrice(lot, easy.Players[0], easy));
        Assert.True(Market.MonthlyCosts(hard.Players[0], 6).Total > Market.MonthlyCosts(easy.Players[0], 6).Total);
        var batch = new WineBatch { Id = lot.Id, Hectoliters = 10, MonthsUntilReady = 0, Barrique = false };
        batch.CopyInfoFrom(lot);
        Assert.True(Market.BulkPricePerHl(batch, hard, hard.Players[0]) < Market.BulkPricePerHl(batch, easy, easy.Players[0]));
    }

    [Fact]
    public void Schwierigkeitsgrad_laesst_die_Computergegner_unveraendert()
    {
        var lot = DifficultyLot;
        var (easyGame, hardGame) = (DifficultyGame(Difficulty.Leicht), DifficultyGame(Difficulty.Schwierig));
        var (easy, hard) = (easyGame.Players[1], hardGame.Players[1]);
        Assert.Null(easy.Difficulty);
        Assert.Equal(hard.Money, easy.Money);
        Assert.Equal(Market.BottlePrice(lot, hard, hardGame), Market.BottlePrice(lot, easy, easyGame));
        Assert.Equal(Market.MonthlyCosts(hard, 6), Market.MonthlyCosts(easy, 6));
    }

    // --- Ratgeber und Vorspulen ---

    [Fact]
    public void Ratgeber_meldet_die_Lese_als_wichtige_Entscheidung()
    {
        var october = EndMonths(Start(), 9);
        var hints = Advisor.MonthHints(october, CurrentPlayer(october));
        Assert.Contains(hints, h => h.Important && h.Text.StartsWith("Lesezeit", StringComparison.Ordinal));
    }

    [Fact]
    public void Ratgeber_erinnert_an_die_Praemierung_nur_solange_sich_noch_ein_Wein_anmelden_laesst()
    {
        var s = Start();
        s.Month = 6;
        var player = CurrentPlayer(s);
        static BottleLot NewLot(string id) => Lot(id, GrapeId.Riesling, 1, 85, QualityLevel.Kabinett, 80, 1000);
        player.Bottles.AddRange([NewLot("a"), NewLot("b")]);
        bool Reminded() => Advisor.MonthHints(s, player).Any(h => h.Text.Contains("Weinprämierung"));
        Assert.True(Reminded());
        // Ein Wein angemeldet, der zweite ginge noch
        player.CompetitionEntries = ["a"];
        Assert.True(Reminded());
        // Beide angemeldet: nichts mehr zu tun
        player.CompetitionEntries = ["a", "b"];
        Assert.False(Reminded());
        // Alle Plätze belegt (höchstens drei Weine), auch wenn noch ein weiterer im Lager liegt
        player.Bottles.AddRange([NewLot("c"), NewLot("d")]);
        player.CompetitionEntries = ["a", "b", "c"];
        Assert.False(Reminded());
    }

    [Fact]
    public void Spult_ueber_ruhige_Monate_bis_zur_naechsten_Entscheidung()
    {
        var may = EndMonths(Start(), 4);
        var state = Advisor.Advance(Game.ApplyAction(may, new GameAction.AutoSkipTurn()));
        Assert.True(Bio.GameMonth(state) - Bio.GameMonth(may) > 1);
        // Angehalten, weil etwas Wichtiges ansteht – das Vorspulen ist damit beendet
        Assert.Null(CurrentPlayer(state).AutoSkip);
        Assert.True(Advisor.ImportantHints(state).Count > 0);
        Assert.Equal(
            JsonOf(Advisor.MonthHints(state, CurrentPlayer(state)).Where(h => h.Important).ToList()),
            JsonOf(Advisor.ImportantHints(state)));
    }

    [Fact]
    public void Spult_hoechstens_ein_Jahr()
    {
        var s = Start();
        var state = Advisor.Advance(Game.ApplyAction(s, new GameAction.AutoSkipTurn()));
        Assert.True(Bio.GameMonth(state) - Bio.GameMonth(s) <= Economy.AutoSkipMonths);
        Assert.Null(CurrentPlayer(state).AutoSkip);
    }

    // --- Endlosspiel ---

    [Fact]
    public void Endlosspiel_kennt_kein_Siegziel_und_laeuft_ueber_viele_Jahre_weiter()
    {
        var state = Simulate.Run(new SimOptions
        {
            Seed = 4,
            Players = [new SimPlayer { RegionId = RegionId.Pfalz, Ai = AiProfileId.Tradition }],
            Years = 20,
        }).State;
        Assert.True(Rules.IsEndless(state));
        Assert.Null(state.WinnerId);
        Assert.False(state.Finished);
        Assert.True(state.Year > 20);
    }
}
