using Winzer.Engine.Data;
using static Winzer.Engine.Tests.TestHelpersA;

namespace Winzer.Engine.Tests;

/// <summary>m4.test.ts</summary>
public class M4Tests
{
    static GameState Start(int seed = 42, RegionId regionId = RegionId.Mosel) =>
        Game.NewGame(new NewGameOptions { Seed = seed, RandomEvents = false, Players = [Human("Anna", regionId)] });

    /// <summary>April Jahr 2 mit ausgebautem Wein und Polster auf dem Konto</summary>
    static GameState WithReadyWine(int seed = 42)
    {
        var state = EndMonths(Start(seed), 9);
        state = Game.ApplyAction(state, new GameAction.Harvest(CurrentPlayer(state).Plots[0].Id));
        state = EndMonths(state, 6);
        return Game.ApplyAction(state, new GameAction.Borrow(50_000));
    }

    /// <summary>Summe der Ernteverluste (cropFactor) über viele Partien bis Ende August</summary>
    static double AverageCrop(ProtectionStrategy protection, int seeds)
    {
        double total = 0;
        for (var seed = 1; seed <= seeds; seed++)
        {
            var state = Start(seed);
            if (protection == ProtectionStrategy.None) state = Game.ApplyAction(state, new GameAction.SetProtection(ProtectionStrategy.None));
            state = EndMonths(state, 8);
            total += CurrentPlayer(state).Plots[0].CropFactor;
        }
        return total / seeds;
    }

    // --- Pflanzenschutz & Krankheiten ---

    [Fact]
    public void Ohne_Pflanzenschutz_faellt_im_Schnitt_mehr_Ernte_aus()
    {
        Assert.True(AverageCrop(ProtectionStrategy.None, 60) < AverageCrop(ProtectionStrategy.Conventional, 60));
    }

    [Fact]
    public void Pflanzenschutz_kostet_von_Mai_bis_August()
    {
        var player = CurrentPlayer(Start());
        Assert.Equal(0, Market.MonthlyCosts(player, 3).Protection);
        Assert.Equal((4 * 600) / 4.0, Market.MonthlyCosts(player, 6).Protection);
    }

    [Fact]
    public void Die_Bio_Umstellung_beginnt_mit_dem_Wechsel_und_endet_ohne_Bio_Pflanzenschutz()
    {
        var state = Game.ApplyAction(Start(), new GameAction.SetProtection(ProtectionStrategy.Bio));
        Assert.Equal(1, CurrentPlayer(state).BioSince);
        var back = Game.ApplyAction(state, new GameAction.SetProtection(ProtectionStrategy.Conventional));
        Assert.Null(CurrentPlayer(back).BioSince);
    }

    // --- Eiswein ---

    static GameState Gambling()
    {
        var state = EndMonths(Start(), 10); // November
        var plot = CurrentPlayer(state).Plots[0];
        plot.Oechsle = 85; // getestet wird die Eiswein-Mechanik, nicht das Wetter
        return Game.ApplyAction(state, new GameAction.GambleIceWine(plot.Id, true));
    }

    [Fact]
    public void Eiswein_laesst_sich_erst_ab_November_und_mit_genug_Mostgewicht_wetten()
    {
        var october = EndMonths(Start(), 9);
        var plotId = CurrentPlayer(october).Plots[0].Id;
        Throws("November", () => Game.ApplyAction(october, new GameAction.GambleIceWine(plotId, true)));
    }

    [Fact]
    public void Bei_Frost_entsteht_Eiswein_sonst_ist_die_Ernte_verloren()
    {
        var withFrost = Gambling().Clone();
        Vineyard.ResolveIceWine(withFrost, CurrentPlayer(withFrost), true);
        var wine = CurrentPlayer(withFrost).Cellar[0];
        Assert.Equal(QualityLevel.Eiswein, wine.Level);
        Assert.True(wine.Oechsle >= 125);
        Assert.True(wine.Hectoliters < 80); // nur ein Bruchteil der normalen Menge

        var withoutFrost = Gambling().Clone();
        Vineyard.ResolveIceWine(withoutFrost, CurrentPlayer(withoutFrost), false);
        Assert.Empty(CurrentPlayer(withoutFrost).Cellar);
        Assert.True(CurrentPlayer(withoutFrost).Plots[0].HarvestedThisYear);
    }

    [Fact]
    public void Normale_Lese_hebt_die_Eiswein_Wette_auf()
    {
        var state = Gambling();
        state = Game.ApplyAction(state, new GameAction.Harvest(CurrentPlayer(state).Plots[0].Id));
        Assert.False(CurrentPlayer(state).Plots[0].IceWineGamble);
    }

    // --- Sekt ---

    [Fact]
    public void Sekt_reift_neun_Monate_und_bringt_bei_hoher_Guete_10_Punkte()
    {
        var state = WithReadyWine();
        var sekt = new GameAction.MakeSekt(CurrentPlayer(state).Cellar[0].Id, 10);
        Throws("Flaschengärung", () => Game.ApplyAction(state, sekt));
        CurrentPlayer(state).CellarMaster = new CellarMaster
        {
            Name = "Test", Talent = 5, Skills = [CellarSkill.Sekt, CellarSkill.Barrique], Salary = 4000, HiredYear = 1,
        };
        CurrentPlayer(state).Money += 100_000; // Gehalt für die neun Monate Reife
        state = Game.ApplyAction(state, sekt);
        var lot = CurrentPlayer(state).Bottles[0];
        Assert.True(lot.Sparkling);
        Assert.Equal(Economy.SektMonths, lot.MonthsUntilReady);
        var reifend = state;
        Throws("reift", () => Game.ApplyAction(reifend, new GameAction.Register(lot.Id)));

        state = EndMonths(state, Economy.SektMonths);
        var ready = CurrentPlayer(state).Bottles.Find(l => l.Id == lot.Id)!;
        Assert.Equal(1330, ready.Bottles); // weder reifend noch ungeprüft wird Sekt ab Hof verkauft
        var registered = Game.ApplyAction(state, new GameAction.Register(lot.Id));
        Assert.Equal(
            ready.Quality >= 80 ? Economy.SektPointsHigh : 3,
            CurrentPlayer(registered).Points - CurrentPlayer(state).Points);
    }

    // --- Panschen & Sabotage ---

    [Fact]
    public void Wasser_bringt_Menge_Zucker_eine_hoehere_Stufe()
    {
        var state = WithReadyWine();
        var batch = CurrentPlayer(state).Cellar[0];
        var watered = CurrentPlayer(Game.ApplyAction(state, new GameAction.Adulterate(batch.Id, AdulterationMethod.Wasser))).Cellar[0];
        Assert.Equal(Js.Round(batch.Hectoliters * 1.2), watered.Hectoliters);
        Assert.Equal(batch.Quality - 15, watered.Quality);

        var sugared = CurrentPlayer(Game.ApplyAction(state, new GameAction.Adulterate(batch.Id, AdulterationMethod.Zucker))).Cellar[0];
        Assert.NotEqual(batch.Level, sugared.Level);
        Assert.Equal(AdulterationMethod.Zucker, sugared.Adulterated);
    }

    [Fact]
    public void Panschen_nur_einmal_pro_Monat_und_nicht_zweimal_am_selben_Wein()
    {
        var state = WithReadyWine();
        var batchId = CurrentPlayer(state).Cellar[0].Id;
        state = Game.ApplyAction(state, new GameAction.Adulterate(batchId, AdulterationMethod.Wasser));
        Assert.Throws<RuleException>(() => Game.ApplyAction(state, new GameAction.Adulterate(batchId, AdulterationMethod.Zucker)));
    }

    [Fact]
    public void Bei_der_Pruefung_fliegt_Glykol_Wein_manchmal_auf_mit_Skandal()
    {
        var caught = 0;
        var passed = 0;
        for (var seed = 1; seed <= 80; seed++)
        {
            var state = WithReadyWine(seed);
            var batchId = CurrentPlayer(state).Cellar[0].Id;
            state = Game.ApplyAction(state, new GameAction.Adulterate(batchId, AdulterationMethod.Glykol));
            state = Game.ApplyAction(state, new GameAction.Bottle(batchId, 10));
            var before = CurrentPlayer(state);
            state = Game.ApplyAction(state, new GameAction.Register(before.Bottles[0].Id));
            var after = CurrentPlayer(state);
            if (after.Bottles.Count == 0)
            {
                caught++;
                Assert.Empty(after.Cellar); // auch der Rest im Tank wird beschlagnahmt
                Assert.True(after.Reputation < before.Reputation);
                Assert.Contains(state.Log, e => e.PlayerId is null && e.Text.StartsWith("Weinskandal", StringComparison.Ordinal));
            }
            else
            {
                passed++;
                Assert.True(after.Points > before.Points);
            }
        }
        // Entdeckungsrisiko 40 % – mit Spielraum für den Zufall
        Assert.True((double)caught / (caught + passed) > 0.25);
        Assert.True((double)caught / (caught + passed) < 0.55);
    }

    [Fact]
    public void Sabotage_trifft_den_Gegner_oder_kostet_bei_Entdeckung_Strafe()
    {
        var hits = 0;
        var busted = 0;
        for (var seed = 1; seed <= 30; seed++)
        {
            var state = Game.NewGame(new NewGameOptions { Seed = seed, Players = [Human("Anna", RegionId.Mosel), Human("Ben", RegionId.Mosel)] });
            var sabotage = new GameAction.Sabotage("p2", SabotageKind.Geruecht);
            var after = Game.ApplyAction(state, sabotage);
            if (after.Players[1].Reputation < state.Players[1].Reputation) hits++;
            else busted++;
            Assert.Throws<RuleException>(() => Game.ApplyAction(after, sabotage));
        }
        Assert.True(hits > 0);
        Assert.True(busted > 0);
    }

    // --- Export ---

    static (GameState State, Player Player) WithContract()
    {
        var state = WithReadyWine();
        state = Game.ApplyAction(state, new GameAction.Bottle(CurrentPlayer(state).Cellar[0].Id, 100));
        state = Game.ApplyAction(state, new GameAction.Register(CurrentPlayer(state).Bottles[0].Id));
        var next = state.Clone();
        var player = CurrentPlayer(next);
        player.ExportOffer = new ExportOffer
        {
            Id = "x", Country = "Japan", BottlesPerMonth = 1000, PricePerBottle = 6,
            MinLevel = QualityLevel.Qualitaetswein, MinQuality = 0, Months = 2,
        };
        Exports.AnswerExport(next, player, true);
        return (next, player);
    }

    [Fact]
    public void Export_liefert_monatlich_und_belohnt_vollstaendige_Erfuellung_mit_Punkten()
    {
        var (state, player) = WithContract();
        var points = player.Points;
        var money = player.Money;
        Exports.DeliverExport(state, player);
        Exports.DeliverExport(state, player);
        Assert.Null(player.ExportContract);
        Assert.Equal(points + 15, player.Points);
        Assert.Equal(money + 2 * 1000 * 6, player.Money);
    }

    [Fact]
    public void Fehlmengen_kosten_Vertragsstrafe_und_den_Bonus()
    {
        var (state, player) = WithContract();
        player.Bottles = [];
        var points = player.Points;
        var money = player.Money;
        Exports.DeliverExport(state, player);
        Exports.DeliverExport(state, player);
        Assert.Equal(points, player.Points);
        Assert.Equal(money - 2 * 1000 * 2, player.Money);
    }

    // --- Betrieb ---

    [Fact]
    public void Personal_waechst_anteilig_mit_der_Flaeche()
    {
        var player = CurrentPlayer(Start());
        Assert.Equal(1, Market.StaffFte(player));
        var extra = player.Plots[1].Clone();
        extra.Id = "extra";
        extra.Hectares = 5;
        player.Plots.Add(extra);
        Assert.Equal(1.5, Market.StaffFte(player));
    }

    [Fact]
    public void Eine_Kellerei_Krise_halbiert_den_Fassweinpreis()
    {
        var state = WithReadyWine();
        var batch = CurrentPlayer(state).Cellar[0];
        var normal = Market.BulkPricePerHl(batch, state, CurrentPlayer(state));
        var crisis = state.Clone();
        crisis.BulkCrisisMonths = 3;
        CloseTo(normal / 2, Market.BulkPricePerHl(batch, crisis, CurrentPlayer(state)));
    }
}
