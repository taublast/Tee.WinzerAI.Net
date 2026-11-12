using Winzer.Engine.Data;

namespace Winzer.Engine.Tests;

/// <summary>Erweiterte Regeln (Regelversion 2). Die Originalregeln sichern TraceTests und die übertragenen Regeltests.</summary>
public class RulesV2Tests
{
    static GameState NewGame(int seed = 11, int rules = 2, AiProfileId? ai = null, RegionId region = RegionId.Pfalz) =>
        Game.NewGame(new NewGameOptions
        {
            Seed = seed,
            RulesVersion = rules,
            TargetPoints = Js.MaxSafeInteger,
            Players = [new NewPlayer { Name = "Test", RegionId = region, Ai = ai }],
        });

    static GameState EndTurn(GameState state) => Game.ApplyAction(state, new GameAction.EndTurn());

    [Fact]
    public void Originalregeln_kennen_die_neuen_Zuege_nicht()
    {
        var state = NewGame(rules: 1);
        Assert.NotNull(Game.ValidateAction(state, new GameAction.SetPriceLevel(0.1)));
        Assert.NotNull(Game.ValidateAction(state, new GameAction.Donate()));
        Assert.Empty(state.Players[0].GoalOffers);
        Assert.Equal(0, state.HalfMonth);
    }

    [Fact]
    public void Ein_Jahr_hat_vierzehn_Zuege_mit_Winterzug_und_Lesehaelften()
    {
        var state = NewGame();
        var turns = new List<string>();
        while (state.Year == 1)
        {
            turns.Add(RulesV2.TurnName(state));
            state = EndTurn(state);
        }
        Assert.Equal(14, turns.Count);
        Assert.Equal("Winter (Januar/Februar)", turns[0]);
        Assert.Equal("März", turns[1]);
        Assert.Contains("September, 1. Hälfte", turns);
        Assert.Contains("September, 2. Hälfte", turns);
        Assert.Contains("November, 2. Hälfte", turns);
        Assert.Equal("Dezember", turns[^1]);
        Assert.Equal(1, state.Month);
    }

    [Fact]
    public void Erste_Monatshaelfte_laesst_nur_Wetter_und_Reife_laufen()
    {
        var state = NewGame();
        while (state.Month != 9) state = EndTurn(state);
        Assert.Equal(1, state.HalfMonth);
        var before = state.Players[0];
        var next = EndTurn(state);
        var after = next.Players[0];
        Assert.Equal(9, next.Month);
        Assert.Equal(2, next.HalfMonth);
        // Keine Monatskosten, kein Ab-Hof-Verkauf in der ersten Hälfte
        Assert.Equal(before.Money, after.Money);
        Assert.True(after.Plots[0].Oechsle > before.Plots[0].Oechsle);
        // Die zweite Hälfte schließt den Monat ab
        var october = EndTurn(next);
        Assert.Equal(10, october.Month);
        Assert.Equal(1, october.HalfMonth);
        Assert.True(october.Players[0].Money < after.Money);
    }

    [Fact]
    public void Zwei_halbe_Reifeschritte_liegen_nahe_am_ganzen_Monat()
    {
        // Gleicher Start, einmal Originalregeln, einmal Halbmonate: Das Mostgewicht Ende September darf sich nur wenig unterscheiden
        double OechsleEndOfSeptember(int rules)
        {
            var state = NewGame(seed: 5, rules: rules);
            state.RandomEvents = false;
            while (state.Month != 10) state = EndTurn(state);
            return state.Players[0].Plots[0].Oechsle;
        }
        var sum = 0.0;
        var whole = OechsleEndOfSeptember(1);
        var halves = OechsleEndOfSeptember(2);
        sum += Math.Abs(whole - halves);
        Assert.InRange(halves, whole - 12, whole + 12);
    }

    [Fact]
    public void Hoeherer_Preis_senkt_die_Nachfrage_und_hebt_den_Erloes_je_Flasche()
    {
        var state = NewGame();
        var player = state.Players[0];
        var normal = RulesV2.DirectDemand(state, player);
        player.PriceLevel = 0.3;
        Assert.True(RulesV2.DirectDemand(state, player) < normal);
        Assert.Equal(1.3, RulesV2.DirectPriceFactor(state, player), 10);
        player.PriceLevel = -0.2;
        Assert.True(RulesV2.DirectDemand(state, player) > normal);
        // Bekannte Güter verlieren bei gleichem Aufschlag weniger Kundschaft
        player.PriceLevel = 0.3;
        var unknown = RulesV2.PriceDemandFactor(player);
        player.Reputation = 90;
        Assert.True(RulesV2.PriceDemandFactor(player) > unknown);
    }

    [Fact]
    public void Preisaufschlag_ist_begrenzt()
    {
        var state = NewGame();
        Assert.Throws<RuleException>(() => Game.ApplyAction(state, new GameAction.SetPriceLevel(0.5)));
        Assert.Throws<RuleException>(() => Game.ApplyAction(state, new GameAction.SetPriceLevel(-0.3)));
        Assert.Equal(0.25, Game.ApplyAction(state, new GameAction.SetPriceLevel(0.25)).Players[0].PriceLevel);
    }

    [Fact]
    public void Jahresziel_wird_gewaehlt_und_im_Dezember_abgerechnet()
    {
        var state = NewGame();
        var player = state.Players[0];
        Assert.Equal(3, player.GoalOffers.Count);
        Assert.Null(player.Goal);
        Assert.Contains(Advisor.MonthHints(state, player), h => h.Text.Contains("Jahresziel"));
        // „Rebfläche vergrößern“ ist immer im Angebot oder wird hier erzwungen
        player.GoalOffers = ["flaeche", "vermoegen", "praedikat"];
        state = Game.ApplyAction(state, new GameAction.ChooseGoal("flaeche"));
        Assert.Throws<RuleException>(() => Game.ApplyAction(state, new GameAction.ChooseGoal("vermoegen")));
        while (state.Month != 3) state = EndTurn(state);
        var fallow = state.Players[0].Plots.First(p => p.Grape is null);
        state = Game.ApplyAction(state, new GameAction.Plant(fallow.Id, GrapeId.Riesling));
        var pointsBefore = state.Players[0].Points;
        while (state.Year == 1) state = EndTurn(state);
        var review = state.Players[0].Review;
        Assert.NotNull(review);
        Assert.Equal(1, review.Year);
        Assert.True(review.GoalReached);
        Assert.Equal(6, review.GoalPoints);
        Assert.True(state.Players[0].Points >= pointsBefore + 6);
        Assert.InRange(review.VintageStars, 1, 5);
        // Neues Jahr, neue Angebote
        Assert.Equal(3, state.Players[0].GoalOffers.Count);
        Assert.Null(state.Players[0].Goal);
    }

    [Fact]
    public void Ohne_Wahl_gilt_das_erste_Angebot()
    {
        var state = NewGame();
        var first = state.Players[0].GoalOffers[0];
        state = EndTurn(state);
        Assert.Equal(first, state.Players[0].Goal);
    }

    [Fact]
    public void Ambition_bringt_einmalig_Punkte_und_bleibt_geheim()
    {
        var state = NewGame();
        state = Game.ApplyAction(state, new GameAction.ChooseAmbition("exportmeister"));
        state = Game.ApplyAction(state, new GameAction.ChooseAmbition("sektbaron"));
        Assert.Throws<RuleException>(() => Game.ApplyAction(state, new GameAction.ChooseAmbition("grossgrund")));
        state.Players[0].Stats.ExportsCompleted = 2;
        var before = state.Players[0].Points;
        state = EndTurn(state);
        // Im selben Zug können Meilensteine hinzukommen, deshalb „mindestens“
        Assert.True(state.Players[0].Points >= before + RulesV2.Ambition("exportmeister")!.Points);
        Assert.Contains("exportmeister", state.Players[0].AmbitionsDone);
        var entry = Assert.Single(state.Log, e => e.Text.Contains("Ambition erfüllt"));
        Assert.Equal(state.Players[0].Id, entry.PlayerId);
        // Kein zweites Mal
        var again = EndTurn(state);
        Assert.Single(again.Log, e => e.Text.Contains("Ambition erfüllt"));
        Assert.Single(again.Players[0].AmbitionsDone);
    }

    [Fact]
    public void Saubermann_scheitert_nach_Panscherei()
    {
        var state = NewGame();
        var player = state.Players[0];
        player.Tainted = true;
        var (value, target) = RulesV2.Ambition("saubermann")!.Progress(state, player);
        Assert.Equal(0, value);
        Assert.True(target > 0);
    }

    [Fact]
    public void Ausbau_kostet_wirkt_und_laesst_sich_nicht_doppelt_bauen()
    {
        var state = NewGame();
        Assert.Throws<RuleException>(() => Game.ApplyAction(state, new GameAction.Construct("schaenke")));
        state.Players[0].Money = 500_000;
        var demand = RulesV2.DirectDemand(state, state.Players[0]);
        state = Game.ApplyAction(state, new GameAction.Construct("schaenke"));
        Assert.Equal(380_000, state.Players[0].Money);
        Assert.True(RulesV2.DirectDemand(state, state.Players[0]) > demand);
        Assert.Throws<RuleException>(() => Game.ApplyAction(state, new GameAction.Construct("schaenke")));
        Assert.Throws<RuleException>(() => Game.ApplyAction(state, new GameAction.Construct("gibtsnicht")));
    }

    [Fact]
    public void Gaestehaus_verdient_nur_in_der_Saison()
    {
        var state = NewGame();
        var player = state.Players[0];
        Assert.Equal(0, RulesV2.GuestHouseIncome(player, 3));
        Assert.True(RulesV2.GuestHouseIncome(player, 7) > 0);
    }

    [Fact]
    public void Spenden_werden_teurer()
    {
        var state = NewGame();
        state.Players[0].Money = 1_000_000;
        var first = RulesV2.DonationCost(state.Players[0]);
        state = Game.ApplyAction(state, new GameAction.Donate());
        Assert.Equal(RulesV2.DonationPoints, state.Players[0].Points);
        Assert.Equal(1_000_000 - first, state.Players[0].Money);
        Assert.True(RulesV2.DonationCost(state.Players[0]) > first);
    }

    [Fact]
    public void Praemierung_haelt_die_Noten_der_drei_Juroren_fest()
    {
        var state = NewGame();
        while (state.Month != 6) state = EndTurn(state);
        var player = state.Players[0];
        var lot = new BottleLot
        {
            Id = "test", Grape = GrapeId.Riesling, Vintage = 1, Oechsle = 95, Level = QualityLevel.Auslese, Quality = 90,
            Bottles = 1000, Registered = true,
        };
        player.Bottles.Add(lot);
        player.CompetitionEntries.Add(lot.Id);
        state = EndTurn(state);
        var show = state.LastShow;
        Assert.NotNull(show);
        Assert.Equal(CompetitionId.Regional, show.Competition);
        var entry = Assert.Single(show.Entries);
        Assert.Equal(3, entry.Jury.Count);
        Assert.All(entry.Jury, note => Assert.InRange(note, entry.BaseScore - 5.05, entry.BaseScore + 5.05));
        Assert.Equal(entry.Jury.Sum() / 3, entry.Score, 1);
        Assert.Equal(Medal.Gold, entry.Medal);
        Assert.Equal(1, state.Players[0].GoldByGrape["riesling"]);
    }

    public static TheoryData<AiProfileId> Profiles() => [AiProfileId.Tradition, AiProfileId.Genossenschaft, AiProfileId.Bio, AiProfileId.Schlitzohr];

    [Theory]
    [MemberData(nameof(Profiles))]
    public void Computergegner_spielen_fuenfzehn_Jahre_nach_den_neuen_Regeln(AiProfileId profile)
    {
        var result = Simulate.Run(new SimOptions { Seed = 3, Years = 15, RulesVersion = 2, Players = [new SimPlayer { RegionId = RegionId.Pfalz, Ai = profile }] });
        var player = result.State.Players[0];
        Assert.False(player.Bankrupt);
        Assert.Equal(15, result.Snapshots.Count);
        Assert.True(player.Points > 100, $"{profile}: {player.Points} Punkte");
        Assert.NotNull(player.Review);
        Assert.Equal(2, player.Ambitions.Count);
    }

    [Fact]
    public void Gleicher_Seed_gleiche_Partie()
    {
        string Run() => Json.Serialize(Simulate.Run(new SimOptions
        {
            Seed = 9, Years = 6, RulesVersion = 2,
            Players = [new SimPlayer { RegionId = RegionId.Mosel, Ai = AiProfileId.Tradition }, new SimPlayer { RegionId = RegionId.Mosel, Ai = AiProfileId.Schlitzohr }],
        }).State);
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void Spielstand_mit_neuen_Feldern_uebersteht_Speichern_und_Laden()
    {
        var state = Simulate.Run(new SimOptions { Seed = 4, Years = 3, RulesVersion = 2, Players = [new SimPlayer { RegionId = RegionId.Baden, Ai = AiProfileId.Bio }] }).State;
        var json = Json.Serialize(state);
        var loaded = Json.DeserializeState(json);
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.RulesVersion);
        Assert.Equal(json, Json.Serialize(loaded));
    }
}
