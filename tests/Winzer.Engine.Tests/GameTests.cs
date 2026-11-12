using Winzer.Engine.Data;
using static Winzer.Engine.Tests.TestHelpersA;

namespace Winzer.Engine.Tests;

/// <summary>game.test.ts</summary>
public class GameTests
{
    static GameState Start(int seed = 42) =>
        // Ohne Zufallsereignisse, damit die Mechanik ungestört geprüft wird
        Game.NewGame(new NewGameOptions { Seed = seed, RandomEvents = false, Players = [Human("Anna", RegionId.Mosel)] });

    static GameState Act(GameState state, GameAction action) => Game.ApplyAction(state, action);

    /// <summary>April im Jahr 2 mit einer ausgebauten Charge Riesling im Tank</summary>
    static GameState WithReadyWine(int seed = 42)
    {
        var state = EndMonths(Start(seed), 9);
        state = Act(state, new GameAction.Harvest(CurrentPlayer(state).Plots[0].Id));
        state = EndMonths(state, 6);
        // Ein passives Mosel-Gut ist nach den teuren Steillagen-Monaten knapp bei Kasse – die Tests prüfen die Kellermechanik.
        CurrentPlayer(state).Money += 50_000;
        return state;
    }

    /// <summary>Wie WithReadyWine, aber 100 hl abgefüllt und amtlich geprüft</summary>
    static GameState WithRegisteredLot(int seed = 42)
    {
        var state = WithReadyWine(seed);
        state = Act(state, new GameAction.Bottle(CurrentPlayer(state).Cellar[0].Id, 100));
        return Act(state, new GameAction.Register(CurrentPlayer(state).Bottles[0].Id));
    }

    static GameState Duo() =>
        Game.NewGame(new NewGameOptions { Seed = 1, Players = [Human("Anna", RegionId.Mosel), Human("Ben", RegionId.Pfalz)] });

    // --- newGame ---

    [Fact]
    public void NewGame_stattet_den_Spieler_wie_im_Design_aus()
    {
        var player = CurrentPlayer(Start());
        Assert.Equal(Economy.StartMoney, player.Money);
        Assert.Equal(10, player.Plots.Aggregate(0.0, (s, p) => s + p.Hectares));
        Assert.Equal(GrapeId.Riesling, player.Plots[0].Grape);
        Assert.Null(player.Plots[1].Grape);
    }

    [Fact]
    public void NewGame_ist_mit_gleichem_Seed_reproduzierbar()
    {
        Assert.Equal(Json.Serialize(EndMonths(Start(7), 24)), Json.Serialize(EndMonths(Start(7), 24)));
    }

    // --- applyAction ---

    [Fact]
    public void ApplyAction_veraendert_den_alten_Spielstand_nicht()
    {
        var before = Start();
        var snapshot = Json.Serialize(before.Clone());
        EndMonths(before, 3);
        Assert.Equal(snapshot, Json.Serialize(before));
    }

    [Fact]
    public void ApplyAction_erlaubt_Pflanzen_nur_im_Fruehjahr()
    {
        var state = Start();
        var plotId = CurrentPlayer(state).Plots[1].Id;
        Assert.Throws<RuleException>(() => Act(state, new GameAction.Plant(plotId, GrapeId.Silvaner)));

        var march = EndMonths(state, 2);
        var planted = Act(march, new GameAction.Plant(plotId, GrapeId.Silvaner));
        Assert.Equal(GrapeId.Silvaner, CurrentPlayer(planted).Plots[1].Grape);
        Assert.Equal(CurrentPlayer(march).Money - 3 * 20_000, CurrentPlayer(planted).Money);
    }

    [Fact]
    public void Junganlagen_tragen_erst_im_dritten_Jahr()
    {
        var state = EndMonths(Start(), 2);
        var plotId = CurrentPlayer(state).Plots[1].Id;
        state = Act(state, new GameAction.Plant(plotId, GrapeId.Silvaner));
        state = EndMonths(state, 7); // Oktober Jahr 1
        Throws("zu jung", () => Act(state, new GameAction.Harvest(plotId)));
    }

    // --- Jahreslauf ---

    [Fact]
    public void Trauben_reifen_ab_August_und_koennen_im_Oktober_gelesen_werden()
    {
        var state = EndMonths(Start(1), 9); // Oktober; Seed 1 ohne Spätfrost und Hagel
        Assert.Equal(10, state.Month);
        var plot = CurrentPlayer(state).Plots[0];
        Assert.True(plot.Oechsle > 60);

        state = Act(state, new GameAction.Harvest(plot.Id));
        var player = CurrentPlayer(state);
        Assert.Single(player.Cellar);
        Assert.True(player.Cellar[0].Hectoliters > 200);
        Assert.True(player.Cellar[0].Hectoliters <= player.TankCapacityHl);
    }

    [Fact]
    public void Spaete_Lese_bringt_mehr_Mostgewicht_als_fruehe()
    {
        var @base = EndMonths(Start(3), 8); // September
        var plotId = CurrentPlayer(@base).Plots[0].Id;
        var early = CurrentPlayer(Act(@base, new GameAction.Harvest(plotId))).Cellar[0];
        var late = CurrentPlayer(Act(EndMonths(@base, 2), new GameAction.Harvest(plotId))).Cellar[0];
        Assert.True(late.Oechsle > early.Oechsle);
    }

    [Fact]
    public void Ungelesene_Trauben_gehen_zum_Jahreswechsel_verloren()
    {
        var state = EndMonths(Start(), 12);
        Assert.Equal(2, state.Year);
        Assert.Contains(state.Log, e => e.Text.Contains("Jahrgang ist verloren"));
    }

    [Fact]
    public void Fasswein_laesst_sich_jederzeit_verkaufen()
    {
        var state = EndMonths(Start(), 9);
        state = Act(state, new GameAction.Harvest(CurrentPlayer(state).Plots[0].Id));
        var batchId = CurrentPlayer(state).Cellar[0].Id;
        var sold = Act(state, new GameAction.SellBulk(batchId, 50));
        Assert.True(CurrentPlayer(sold).Money > CurrentPlayer(state).Money);
    }

    // --- Abfüllung & Prüfung ---

    [Fact]
    public void Abgefuellt_wird_erst_nach_dem_Ausbau()
    {
        var state = EndMonths(Start(), 9);
        state = Act(state, new GameAction.Harvest(CurrentPlayer(state).Plots[0].Id));
        var batchId = CurrentPlayer(state).Cellar[0].Id;
        Throws("nicht ausgebaut", () => Act(state, new GameAction.Bottle(batchId, 10)));
    }

    [Fact]
    public void Abfuellen_leert_den_Tank_und_kostet_Flaschen()
    {
        var state = WithReadyWine();
        var batch = CurrentPlayer(state).Cellar[0];
        var bottled = CurrentPlayer(Act(state, new GameAction.Bottle(batch.Id, 100)));
        Assert.Equal(batch.Hectoliters - 100, bottled.Cellar[0].Hectoliters);
        Assert.Equal(13_300, bottled.Bottles[0].Bottles);
        Assert.Equal(CurrentPlayer(state).Money - 13_300 * 0.6, bottled.Money);
    }

    [Fact]
    public void Kleine_Abfuellungen_desselben_Weins_landen_im_selben_Posten()
    {
        var state = WithReadyWine();
        var batchId = CurrentPlayer(state).Cellar[0].Id;
        state = Act(state, new GameAction.Bottle(batchId, 3));
        var lotId = CurrentPlayer(state).Bottles[0].Id;
        Throws(Util.JsNumber(Economy.RegisterMinBottles), () => Act(state, new GameAction.Register(lotId)));

        state = Act(state, new GameAction.Bottle(batchId, 3));
        Assert.Single(CurrentPlayer(state).Bottles);
        Assert.True(CurrentPlayer(Act(state, new GameAction.Register(lotId))).Points > 0);
    }

    [Fact]
    public void Grosshandel_nimmt_nur_gepruefte_Weine()
    {
        var state = WithReadyWine();
        state = Act(state, new GameAction.Bottle(CurrentPlayer(state).Cellar[0].Id, 10));
        var lotId = CurrentPlayer(state).Bottles[0].Id;
        Throws("amtlich geprüfte", () => Act(state, new GameAction.SellWholesale(lotId, 100)));

        state = Act(state, new GameAction.Register(lotId));
        var sold = Act(state, new GameAction.SellWholesale(lotId, 100));
        Assert.True(CurrentPlayer(sold).Money > CurrentPlayer(state).Money);
        Assert.Equal(1_330 - 100, CurrentPlayer(sold).Bottles[0].Bottles);
    }

    // --- Direktvermarktung ---

    [Fact]
    public void Der_Hofladen_verkauft_monatlich_so_viele_Flaschen_wie_die_Bekanntheit_hergibt()
    {
        var state = WithRegisteredLot();
        var demand = Market.MonthlyDemand(CurrentPlayer(state));
        var after = CurrentPlayer(EndMonths(state, 1));
        Assert.Equal(13_300 - demand, after.Bottles[0].Bottles);
    }

    [Fact]
    public void Der_Online_Shop_steigert_den_Absatz()
    {
        // Nach 15 Monaten ohne Verkäufe ist die Kasse knapp – der Shop wird per Kredit finanziert.
        var state = Act(WithRegisteredLot(), new GameAction.Borrow(20_000));
        var withShop = Act(state, new GameAction.BuyOnlineShop());
        Assert.True(Market.MonthlyDemand(CurrentPlayer(withShop)) > Market.MonthlyDemand(CurrentPlayer(state)));
        Throws("läuft bereits", () => Act(withShop, new GameAction.BuyOnlineShop()));
    }

    [Fact]
    public void Marketing_steigert_die_Bekanntheit_aber_nur_einmal_pro_Monat_und_Saison()
    {
        var state = Start();
        var after = Act(state, new GameAction.Marketing(MarketingKind.Social));
        Assert.True(CurrentPlayer(after).Reputation > CurrentPlayer(state).Reputation);
        Throws("schon gebucht", () => Act(after, new GameAction.Marketing(MarketingKind.Social)));
        Throws("nicht möglich", () => Act(state, new GameAction.Marketing(MarketingKind.Messe)));
        Act(EndMonths(state, 2), new GameAction.Marketing(MarketingKind.Messe)); // wirft nicht
    }

    [Fact]
    public void Je_bekannter_das_Weingut_desto_weniger_bringt_Marketing()
    {
        var unknown = Start();
        var famous = Start();
        CurrentPlayer(famous).Reputation = 80;
        static double Gain(GameState s) =>
            CurrentPlayer(Act(s, new GameAction.Marketing(MarketingKind.Social))).Reputation - CurrentPlayer(s).Reputation;
        Assert.True(Gain(famous) < Gain(unknown));
    }

    [Fact]
    public void Ohne_Pflege_sinkt_die_Bekanntheit()
    {
        var state = Start();
        CurrentPlayer(state).Reputation = 60;
        Assert.True(CurrentPlayer(EndMonths(state, 6)).Reputation < 60);
    }

    // --- Prämierungen ---

    [Fact]
    public void Nur_gepruefte_Weine_duerfen_an_Praemierungen_teilnehmen()
    {
        var state = WithReadyWine();
        state = Act(state, new GameAction.Bottle(CurrentPlayer(state).Cellar[0].Id, 10));
        var lotId = CurrentPlayer(state).Bottles[0].Id;
        Throws("amtlich geprüfte", () => Act(state, new GameAction.EnterCompetition(lotId)));
    }

    [Fact]
    public void Angemeldete_Weine_werden_im_Juni_bewertet_und_nicht_vorher_verkauft()
    {
        var state = WithRegisteredLot(); // April
        var lotId = CurrentPlayer(state).Bottles[0].Id;
        state = Act(state, new GameAction.EnterCompetition(lotId));
        Assert.True(CurrentPlayer(state).Money < CurrentPlayer(WithRegisteredLot()).Money);

        state = EndMonths(state, 3); // April, Mai, Juni
        var player = CurrentPlayer(state);
        Assert.Equal(7, state.Month);
        Assert.Empty(player.CompetitionEntries);
        Assert.Contains(state.Log, e => e.Text.StartsWith(Economy.Competitions[CompetitionId.Regional].Name, StringComparison.Ordinal));
        // bis zur Bewertung (April, Mai, Juni) wurde nichts aus dem Posten verkauft
        var sales = state.Log.Where(e => e.Text.StartsWith("Ab Hof", StringComparison.Ordinal)).ToList();
        Assert.Empty(sales);
    }

    // --- Kellermeister ---

    [Fact]
    public void Kellermeister_hebt_die_Guete_bei_der_Lese_um_sein_Talent_und_kostet_Gehalt()
    {
        var october = EndMonths(Start(), 9);
        var plotId = CurrentPlayer(october).Plots[0].Id;
        var without = CurrentPlayer(Act(october, new GameAction.Harvest(plotId))).Cellar[0];

        var candidate = october.CellarMasterCandidates[0];
        var hired = Act(october, new GameAction.HireCellarMaster(0));
        Assert.Equal(candidate.Name, CurrentPlayer(hired).CellarMaster?.Name);
        Assert.DoesNotContain(JsonOf(candidate), hired.CellarMasterCandidates.Select(JsonOf));
        var withMaster = CurrentPlayer(Act(hired, new GameAction.Harvest(plotId))).Cellar[0];
        Assert.True(withMaster.Quality >= without.Quality + candidate.Talent);

        var costWithout = CurrentPlayer(october).Money - CurrentPlayer(EndMonths(october, 1)).Money;
        var costWith = CurrentPlayer(hired).Money - CurrentPlayer(EndMonths(hired, 1)).Money;
        CloseTo(candidate.Salary, costWith - costWithout, -2);

        var fired = Act(hired, new GameAction.FireCellarMaster());
        Assert.Null(CurrentPlayer(fired).CellarMaster);
        Throws("kein Kellermeister", () => Act(fired, new GameAction.FireCellarMaster()));
    }

    // --- Kredit & Insolvenz ---

    [Fact]
    public void Kredite_sind_durch_den_Betriebswert_begrenzt_und_kosten_Zinsen()
    {
        var state = Start();
        var player = CurrentPlayer(state);
        Throws("Kreditrahmen", () => Act(state, new GameAction.Borrow(Market.CreditLimit(player) + 1)));

        var borrowed = Act(state, new GameAction.Borrow(100_000));
        Assert.Equal(Economy.StartMoney + 100_000, CurrentPlayer(borrowed).Money);
        Assert.Equal(Market.CreditLimit(player) - 100_000, Market.AvailableCredit(CurrentPlayer(borrowed)));

        var costsWithout = Economy.StartMoney - CurrentPlayer(EndMonths(state, 1)).Money;
        var costsWith = Economy.StartMoney + 100_000 - CurrentPlayer(EndMonths(borrowed, 1)).Money;
        Assert.Equal(500, costsWith - costsWithout); // 6 % p. a. auf 100.000 €

        var repaid = Act(borrowed, new GameAction.Repay(40_000));
        Assert.Equal(60_000, CurrentPlayer(repaid).Loan);
    }

    [Fact]
    public void Wer_drei_Monate_ohne_Geld_und_Kreditrahmen_ist_scheidet_aus()
    {
        var state = Start();
        var player = CurrentPlayer(state);
        player.Loan = Market.CreditLimit(player);
        player.Money = -50_000;
        var after = EndMonths(state, 2);
        Assert.False(CurrentPlayer(after).Bankrupt);
        var broke = EndMonths(after, 1);
        Assert.True(broke.Players[0].Bankrupt);
        Assert.True(broke.Finished);
        Throws("beendet", () => Act(broke, EndTurn));
    }

    [Fact]
    public void Insolvente_Spieler_werden_im_Mehrspieler_uebersprungen()
    {
        var state = Duo();
        state.Players[0].Bankrupt = true;
        state.CurrentPlayer = 1;
        var next = Act(state, EndTurn);
        Assert.Equal(2, next.Month);
        Assert.Equal(1, next.CurrentPlayer);
        Assert.False(next.Finished);
    }

    // --- Mehrspieler ---

    [Fact]
    public void Der_Monat_endet_erst_wenn_alle_Spieler_gezogen_haben()
    {
        var state = Duo();
        state = Act(state, EndTurn);
        Assert.Equal(1, state.Month);
        Assert.Equal(1, state.CurrentPlayer);
        state = Act(state, EndTurn);
        Assert.Equal(2, state.Month);
        Assert.Equal(0, state.CurrentPlayer);
    }
}
