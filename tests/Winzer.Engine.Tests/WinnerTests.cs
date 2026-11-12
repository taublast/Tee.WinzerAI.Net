using Winzer.Engine.Data;

namespace Winzer.Engine.Tests;

/// <summary>Spielende: Ein Sieger kann auch mitten in einem Zug feststehen – die Partie darf dann nicht stehen bleiben.</summary>
public class WinnerTests
{
    static GameState TwoPlayers(int rules, double target = 100) => Game.NewGame(new NewGameOptions
    {
        Seed = 5,
        RulesVersion = rules,
        TargetPoints = target,
        Players =
        [
            new NewPlayer { Name = "Mensch", RegionId = RegionId.Pfalz },
            new NewPlayer { Name = "Rebenhain", RegionId = RegionId.Pfalz, Ai = AiProfileId.Tradition },
        ],
    });

    static BottleLot Lot(string id) => new()
    {
        Id = id, Grape = GrapeId.Riesling, Vintage = 1, Oechsle = 95, Level = QualityLevel.Auslese, Quality = 85, Bottles = 2000,
    };

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Computergegner_gewinnt_mitten_im_Zug_durch_die_Weinpruefung(int rules)
    {
        var state = TwoPlayers(rules);
        state = Game.ApplyAction(state, new GameAction.EndTurn());
        var ai = Rules.CurrentPlayer(state);
        Assert.NotNull(ai.Ai);
        // Kurz vor dem Ziel, mit einem prüfbaren Wein im Lager: Die Prüfung bringt die fehlenden Punkte
        ai.Points = 99;
        ai.Money = 200_000;
        ai.Bottles.Add(Lot("sieg"));

        var after = Ai.PlayAiTurn(state);

        Assert.Equal(ai.Id, after.WinnerId);
        Assert.True(after.Players[1].Points >= 100);
        // Weitere Züge der Computergegner ändern nichts mehr und scheitern nicht
        Assert.Same(after, Ai.RunAiTurns(after));
        Assert.Equal("Das Spiel ist beendet.", Game.ValidateAction(after, new GameAction.EndTurn()));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Sieger_wird_festgestellt_auch_wenn_ein_anderer_den_Wein_pruefen_laesst(int rules)
    {
        // Der Mensch liegt schon über dem Ziel, ohne dass es bemerkt wurde; der Computergegner lässt prüfen
        var state = TwoPlayers(rules);
        state.Players[0].Points = 120;
        state = Game.ApplyAction(state, new GameAction.EndTurn());
        var ai = Rules.CurrentPlayer(state);
        ai.Money = 200_000;
        ai.Bottles.Add(Lot("fremd"));

        var after = Ai.PlayAiTurn(state);

        Assert.Equal(state.Players[0].Id, after.WinnerId);
    }

    [Fact]
    public void Punkte_aus_dem_Jahresziel_koennen_die_Partie_entscheiden()
    {
        var state = TwoPlayers(2, target: 50);
        var human = state.Players[0];
        human.GoalOffers = ["flaeche", "vermoegen", "praedikat"];
        state = Game.ApplyAction(state, new GameAction.ChooseGoal("flaeche"));
        while (state.Month != 3) state = Ai.RunAiTurns(Game.ApplyAction(state, new GameAction.EndTurn()));
        var fallow = state.Players[0].Plots.First(p => p.Grape is null);
        state.Players[0].Money = 300_000;
        state = Game.ApplyAction(state, new GameAction.Plant(fallow.Id, GrapeId.Riesling));
        // Knapp unter dem Ziel: Die sechs Punkte des Jahresziels im Dezember reichen
        state.Players[0].Points = 46;
        state.Players[1].Points = 0;
        while (state.Year == 1 && string.IsNullOrEmpty(state.WinnerId))
        {
            state.Players[0].Points = Math.Min(state.Players[0].Points, 46);
            state.Players[1].Points = 0;
            state = Ai.RunAiTurns(Game.ApplyAction(state, new GameAction.EndTurn()));
        }
        Assert.Equal(state.Players[0].Id, state.WinnerId);
        Assert.True(state.Players[0].Points >= 50);
    }

    [Fact]
    public void Spende_kann_das_Punkteziel_erreichen()
    {
        var state = TwoPlayers(2, target: 50);
        state.Players[0].Points = 46;
        state.Players[0].Money = 500_000;
        state = Game.ApplyAction(state, new GameAction.Donate());
        Assert.Equal(state.Players[0].Id, state.WinnerId);
    }
}
