using Winzer.Engine.Data;
using static Winzer.Engine.Tests.TestHelpersA;

namespace Winzer.Engine.Tests;

/// <summary>sabotage.test.ts</summary>
public class SabotageTests
{
    static readonly GameAction Rumor = new GameAction.Sabotage("p2", SabotageKind.Geruecht);

    /// <summary>Zwei Menschen am selben Gerät, ohne Zufallsereignisse</summary>
    static GameState Duo(int seed = 1)
    {
        var s = Game.NewGame(new NewGameOptions
        {
            Seed = seed, RandomEvents = false, Players = [Human("Anna", RegionId.Mosel), Human("Ben", RegionId.Mosel)],
        });
        foreach (var p in s.Players) p.Money = 200_000;
        return s;
    }

    /// <summary>Sabotiert Ben durch Anna – wahlweise so lange Seeds probieren, bis sie unbemerkt bleibt bzw. auffliegt</summary>
    static GameState Sabotaged(bool detected)
    {
        for (var seed = 1; seed < 100; seed++)
        {
            var s = Duo(seed);
            var after = Game.ApplyAction(s, Rumor);
            if ((after.Players[0].Money < s.Players[0].Money - 10_000) == detected) return after;
        }
        throw new InvalidOperationException("kein passender Seed");
    }

    // --- Reaktion auf Sabotage ---

    [Fact]
    public void Das_Opfer_darf_bis_Ende_des_Folgemonats_reagieren()
    {
        var s = Sabotaged(false);
        var ben = s.Players[1];
        Assert.NotNull(ben.Decision);
        Assert.Contains("Sabotage", ben.Decision.Title);
        Assert.Equal(Bio.GameMonth(s) + 1, ben.Decision.Deadline);
        // Anna beendet den Zug, Ben ist dran – und die Entscheidung überlebt das Monatsende
        var next = Game.ApplyAction(s, EndTurn);
        Assert.Equal("p2", CurrentPlayer(next).Id);
        next = Game.ApplyAction(next, EndTurn);
        Assert.NotNull(next.Players[1].Decision);
        Assert.Contains("Sabotage", next.Players[1].Decision!.Title);
        // Am Ende des Folgemonats greift „Hinnehmen“
        next = Game.ApplyAction(Game.ApplyAction(next, EndTurn), EndTurn);
        Assert.Null(next.Players[1].Decision);
    }

    [Fact]
    public void Die_Detektei_ueberfuehrt_den_Taeter_manchmal_dann_gibt_es_Schadensersatz()
    {
        var caught = 0;
        for (var seed = 1; seed <= 40; seed++)
        {
            var s = Duo(seed);
            var after = Game.ApplyAction(s, Rumor);
            var decision = after.Players[1].Decision;
            if (decision is null || (decision.Params.TryGetValue("detected", out var detected) && detected == 1)) continue;
            after = Game.ApplyAction(after, EndTurn);
            var before = after.Players[1].Money;
            after = Game.ApplyAction(after, new GameAction.Decide(0));
            if (after.Players[1].Money > before)
            {
                caught++;
                Assert.Equal(before - 4_000 + Economy.SabotageDamages[SabotageKind.Geruecht], after.Players[1].Money);
                Assert.Contains(after.Log, e => e.Text.Contains("überführt Anna"));
            }
        }
        Assert.True(caught > 0);
    }

    [Fact]
    public void Auch_ein_erwischter_Taeter_laesst_sich_verklagen()
    {
        var s = Sabotaged(true);
        Assert.NotNull(s.Players[1].Decision);
        Assert.Contains("klagen", s.Players[1].Decision!.Options[0].Label);
    }

    [Fact]
    public void Die_Nachtwache_schuetzt_Saboteure_fliegen_fast_immer_auf()
    {
        var busted = 0;
        for (var seed = 1; seed <= 20; seed++)
        {
            var s = Duo(seed);
            s = Game.ApplyAction(s, EndTurn); // Ben ist dran …
            s.Players[1].Decision = null;
            var guarded = s.Clone();
            // … und heuert über die Reaktion die Nachtwache an
            guarded.Players[1].Effects.Add(new Effect { Kind = EffectKind.Guard, Value = 1, MonthsLeft = 12, Label = "Nachtwache im Weinberg" });
            var annasTurn = Game.ApplyAction(guarded, EndTurn);
            var after = Game.ApplyAction(annasTurn, Rumor);
            if (after.Players[1].Reputation >= annasTurn.Players[1].Reputation) busted++;
        }
        Assert.True(busted >= 15, $"aufgeflogen: {busted}");
    }

    // --- Vorspulen mit mehreren Menschen ---

    /// <summary>Anna spult vor, Ben spielt normal</summary>
    static GameState AnnaSkips() => Advisor.Advance(Game.ApplyAction(Duo(), new GameAction.AutoSkipTurn()));

    [Fact]
    public void Jeder_spult_fuer_sich_Wer_nicht_vorspult_kommt_jeden_Monat_dran()
    {
        var s = AnnaSkips();
        Assert.Equal("p2", CurrentPlayer(s).Id);
        Assert.NotNull(s.Players[0].AutoSkip);
        for (var i = 0; i < 3; i++)
        {
            var month = Bio.GameMonth(s);
            s = Advisor.Advance(Game.ApplyAction(s, EndTurn));
            if (CurrentPlayer(s).Id == "p1")
            {
                // Annas Vorspulen endet nur, wenn bei ihr etwas Wichtiges ansteht
                Assert.Null(s.Players[0].AutoSkip);
                Assert.True(Advisor.ImportantHints(s).Count > 0);
                return;
            }
            // Annas Zug lief von selbst durch – Ben ist im nächsten Monat wieder dran
            Assert.Equal(month + 1, Bio.GameMonth(s));
        }
    }

    [Fact]
    public void Eine_Sabotage_beendet_das_Vorspulen_des_Opfers()
    {
        var s = AnnaSkips();
        if (CurrentPlayer(s).Id != "p2") throw new InvalidOperationException("Ben sollte am Zug sein");
        s = Game.ApplyAction(s, new GameAction.Sabotage("p1", SabotageKind.Geruecht));
        s = Advisor.Advance(Game.ApplyAction(s, EndTurn));
        Assert.Equal("p1", CurrentPlayer(s).Id);
        Assert.Null(s.Players[0].AutoSkip);
        Assert.Contains(Advisor.ImportantHints(s), h => h.Text.Contains("Sabotage"));
    }

    [Fact]
    public void Computergegner_spulen_nicht_vor()
    {
        var s = Game.NewGame(new NewGameOptions
        {
            Seed = 1, Players = [Computer("KI", RegionId.Mosel, AiProfileId.Tradition), Human("Anna", RegionId.Mosel)],
        });
        Assert.Throws<RuleException>(() => Game.ApplyAction(s, new GameAction.AutoSkipTurn()));
    }
}
