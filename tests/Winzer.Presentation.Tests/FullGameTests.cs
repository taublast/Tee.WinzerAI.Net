using Winzer.Engine;

namespace Winzer.Presentation.Tests;

/// <summary>
/// Ganze Partien über die Spielsitzung – denselben Weg, den die Oberfläche nimmt: Der Autopilot spielt den Menschen,
/// die Computergegner ziehen mit, bis jemand gewinnt.
/// </summary>
public class FullGameTests
{
    static GameSession NewSession(int rules, int seed, int humans = 1, double target = 600, int? yearLimit = null, ISaveStore? store = null, RegionId region = RegionId.Mosel)
    {
        var options = new NewGameOptions { Seed = seed, RulesVersion = rules, TargetPoints = target, YearLimit = yearLimit };
        for (var i = 0; i < humans; i++) options.Players.Add(new NewPlayer { Name = $"Mensch {i + 1}", RegionId = region });
        options.Players.Add(new NewPlayer { Name = "Rebenhain", RegionId = region, Ai = AiProfileId.Tradition });
        options.Players.Add(new NewPlayer { Name = "Morgentau", RegionId = region, Ai = AiProfileId.Bio });
        if (humans == 1) options.Players.Add(new NewPlayer { Name = "Gaukler", RegionId = region, Ai = AiProfileId.Schlitzohr });
        return new GameSession(Game.NewGame(options), "test", store ?? new MemorySaveStore(), new SilentAudio());
    }

    public static TheoryData<int, int> Games() => new() { { 1, 3 }, { 1, 8 }, { 2, 3 }, { 2, 8 }, { 2, 21 } };

    [Theory]
    [MemberData(nameof(Games))]
    public void Eine_Partie_laeuft_bis_zum_Sieg(int rules, int seed)
    {
        var session = NewSession(rules, seed);
        // Vorsichtige Spielweise: Die Partie soll mit einem Sieger enden, nicht mit der Pleite des Menschen
        var pilot = new AutoPilot { Adventurous = false };
        pilot.PlayToEnd(session);

        Assert.True(session.Over, $"nach {pilot.Turns} Zügen in Jahr {session.State.Year} nicht zu Ende");
        var me = session.State.Players[0];
        Assert.True(session.Winner is not null,
            $"kein Sieger: Jahr {session.State.Year}, Konto {me.Money:0}, Kredit {me.Loan:0}, pleite {me.Bankrupt} | " +
            string.Join(" | ", session.State.Log.Where(e => e.PlayerId is null || e.PlayerId == me.Id).TakeLast(14).Select(e => $"{e.Month}/{e.Year} {e.Text}")));
        Assert.True(session.Winner.Points >= 600);
        Assert.Empty(pilot.Errors);
        Assert.False(session.SaveFailed);
        // Der Mensch hat wirklich gespielt, nicht nur Züge beendet
        var human = session.State.Players[0];
        Assert.True(human.Points > 50, $"nur {human.Points} Punkte");
        Assert.True(human.Stats.Harvests >= 5);
        Assert.True(human.Stats.BottlesSold > 1000);
        Assert.False(human.Bankrupt);
        foreach (var core in new[] { "Harvest", "Bottle", "Register", "EnterCompetition", "Borrow", "Plant", "InsureHarvest", "EndTurn" })
        {
            Assert.True(pilot.Used.ContainsKey(core), $"Zugart {core} kam nie vor");
        }
        if (rules >= 2)
        {
            Assert.True(pilot.Used.ContainsKey("ChooseGoal"));
            Assert.Equal(RulesV2.AmbitionCount, human.Ambitions.Count);
            Assert.NotNull(human.Review);
        }
    }

    [Fact]
    public void Ueber_mehrere_Partien_kommt_jede_Zugart_vor()
    {
        var used = new HashSet<string>();
        var errors = new List<string>();
        // Auch Rotweingebiete, sonst kommen Rosé und Barrique nie vor
        RegionId[] regions = [RegionId.Mosel, RegionId.Ahr, RegionId.Wuerttemberg, RegionId.Pfalz, RegionId.Baden, RegionId.Ahr];
        var index = 0;
        foreach (var seed in new[] { 3, 8, 21, 34, 55, 89 })
        {
            // Hohes Punkteziel, damit die Partie lang genug für späte Züge läuft
            var session = NewSession(2, seed, target: 2500, yearLimit: 14, region: regions[index++]);
            var pilot = new AutoPilot();
            pilot.PlayToEnd(session);
            Assert.True(session.Over);
            used.UnionWith(pilot.Used.Keys);
            errors.AddRange(pilot.Errors);
        }
        Assert.Empty(errors);
        var all = typeof(GameAction).GetNestedTypes().Select(t => t.Name).ToList();
        // AuctionLimit ist Computergegnern vorbehalten
        var missing = all.Where(name => name != "AuctionLimit" && !used.Contains(name)).ToList();
        Assert.True(missing.Count == 0, "nie ausgeführt: " + string.Join(", ", missing));
    }

    [Theory]
    [InlineData(7, 150)]
    [InlineData(7, 300)]
    [InlineData(3, 150)]
    public async Task Kurze_Partie_endet_sobald_jemand_das_Ziel_erreicht(int seed, double target)
    {
        var session = NewSession(2, seed, target: target);
        var pilot = new AutoPilot { Adventurous = false };
        var play = Task.Run(() => pilot.PlayToEnd(session));
        var finished = await Task.WhenAny(play, Task.Delay(TimeSpan.FromSeconds(20))) == play;
        Assert.True(finished, $"hängt nach {pilot.Turns} Zügen: {RulesV2.TurnName(session.State)} Jahr {session.State.Year}, Sieger {session.State.WinnerId}, beendet {session.State.Finished}, vorbei {session.Over}");
        await play;
        Assert.True(session.Over,
            $"bleibt stehen nach {pilot.Turns} Zügen: {RulesV2.TurnName(session.State)} Jahr {session.State.Year}, am Zug {session.Player.Name}, " +
            $"Zug beenden: {session.Problem(new GameAction.EndTurn()) ?? "möglich"}, Versteigerung {session.LiveLot?.Name}, Übersicht {session.Summary is not null}, " +
            $"Fehler '{session.Error}', Pilot: {string.Join("; ", pilot.Errors.TakeLast(3))}");
        Assert.NotNull(session.Winner);
        Assert.True(session.Winner.Points >= target);
    }

    [Fact]
    public void Partie_mit_Jahreslimit_endet_nach_der_Zeit()
    {
        var session = NewSession(2, 5, target: Rules.NoPointTarget, yearLimit: 3);
        new AutoPilot().PlayToEnd(session);
        Assert.True(session.State.Finished);
        Assert.Equal(3, session.State.Year);
        Assert.NotNull(session.Winner);
    }

    [Fact]
    public void Zwei_Menschen_am_selben_Geraet_wechseln_sich_ab()
    {
        var session = NewSession(2, 13, humans: 2, target: 300);
        Assert.True(session.HotSeat);
        var pilot = new AutoPilot();
        var handovers = 0;
        var seenBoth = new HashSet<string>();
        while (!session.Over && pilot.Turns < 1500)
        {
            seenBoth.Add(session.Player.Id);
            var before = session.Player.Id;
            pilot.PlayTurn(session);
            if (session.Summary is { Changed: true } summary)
            {
                handovers++;
                Assert.NotEqual(before, session.Player.Id);
                Assert.Equal(session.Player.Name, summary.PlayerName);
                // Beim Wechsel landet der Nächste auf der Gutsansicht, nicht in der Szene des Vorgängers
                Assert.Equal(Scene.Gut, session.Nav.Scene);
            }
        }
        Assert.True(session.Over);
        Assert.Equal(2, seenBoth.Count);
        Assert.True(handovers > 20, $"nur {handovers} Spielerwechsel");
        Assert.Empty(pilot.Errors);
    }

    [Fact]
    public void Vorspulen_zeigt_danach_die_Uebersicht()
    {
        var session = NewSession(1, 4);
        session.Run(new GameAction.AutoSkipTurn());
        Assert.NotNull(session.Summary);
        Assert.True(session.Summary.Skipped);
        Assert.True(session.Summary.Months >= 1);
        Assert.Null(session.Player.AutoSkip);
        session.CloseSummary(Scene.Keller);
        Assert.Null(session.Summary);
        Assert.Equal(Scene.Keller, session.Nav.Scene);
    }

    [Fact]
    public void Abgelehnter_Zug_meldet_den_Grund_und_aendert_nichts()
    {
        var session = NewSession(1, 4);
        var before = Json.Serialize(session.State);
        session.Run(new GameAction.Repay(5000));
        Assert.NotEqual("", session.Error);
        Assert.Equal(before, Json.Serialize(session.State));
        session.ClearError();
        Assert.Equal("", session.Error);
    }

    [Fact]
    public void Neue_Entscheidung_oeffnet_sich_von_selbst()
    {
        var session = NewSession(1, 3);
        var pilot = new AutoPilot();
        var opened = false;
        for (var i = 0; i < 400 && !session.Over && !opened; i++)
        {
            // Wie der Pilot, aber ohne die Entscheidung gleich zu beantworten
            session.Run(new GameAction.EndTurn());
            if (session.Summary is not null) session.CloseSummary();
            while (session.LiveLot is { } lot) session.Run(new GameAction.AuctionPass(lot.Id));
            if (session.Player.Decision is not null)
            {
                opened = session.Nav.Overlay == Overlay.Decision;
                break;
            }
        }
        Assert.True(opened, "keine Entscheidung erschienen oder nicht geöffnet");
        session.Run(new GameAction.Decide(session.Player.Decision!.Fallback));
        Assert.Null(session.Player.Decision);
        Assert.NotEqual(Overlay.Decision, session.Nav.Overlay);
        _ = pilot;
    }
}
