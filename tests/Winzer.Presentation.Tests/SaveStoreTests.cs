using Winzer.Engine;

namespace Winzer.Presentation.Tests;

/// <summary>Spielstände auf der Platte: sichern, auflisten, laden, weiterspielen, löschen.</summary>
public class SaveStoreTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "winzer-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    static GameState NewGame(int seed = 6, int rules = 2) => Game.NewGame(new NewGameOptions
    {
        Seed = seed,
        RulesVersion = rules,
        TargetPoints = 600,
        Players =
        [
            new NewPlayer { Name = "Weingut Sonnenhang", RegionId = RegionId.Pfalz },
            new NewPlayer { Name = "Rebenhain", RegionId = RegionId.Pfalz, Ai = AiProfileId.Tradition },
        ],
    });

    [Fact]
    public void Jeder_Zug_wird_gesichert_und_laesst_sich_laden()
    {
        var store = new FileSaveStore(root);
        var session = new GameSession(NewGame(), "partie-1", store, new SilentAudio());
        var pilot = new AutoPilot();
        for (var i = 0; i < 30; i++) pilot.PlayTurn(session);
        Assert.False(session.SaveFailed);

        // Ein zweiter Zugriff (wie nach einem Neustart) findet den Stand
        var slot = Assert.Single(new FileSaveStore(root).List());
        Assert.Equal("partie-1", slot.Id);
        Assert.Equal(Json.Serialize(session.State), Json.Serialize(slot.State));
        Assert.True((DateTime.Now - slot.SavedAt).TotalMinutes < 5);
    }

    [Fact]
    public void Speichern_vor_dem_Beenden_sichert_den_Stand_sofort()
    {
        var store = new FileSaveStore(root);
        var session = new GameSession(NewGame(), "partie-1", store, new SilentAudio());
        new AutoPilot().PlayTurn(session);
        // Der Stand auf der Platte geht verloren – „Speichern und beenden“ schreibt ihn neu
        store.Delete("partie-1");
        Assert.Empty(new FileSaveStore(root).List());
        Assert.True(session.Save());
        Assert.False(session.SaveFailed);
        var slot = Assert.Single(new FileSaveStore(root).List());
        Assert.Equal(Json.Serialize(session.State), Json.Serialize(slot.State));
    }

    [Fact]
    public void Geladene_Partie_spielt_genauso_weiter_wie_die_laufende()
    {
        var store = new FileSaveStore(root);
        var running = new GameSession(NewGame(), "partie-1", store, new SilentAudio());
        var pilot = new AutoPilot();
        for (var i = 0; i < 40; i++) pilot.PlayTurn(running);

        var loaded = new GameSession(store.List()[0].State, "partie-1", new MemorySaveStore(), new SilentAudio());
        for (var i = 0; i < 25; i++)
        {
            running.Run(new GameAction.EndTurn());
            loaded.Run(new GameAction.EndTurn());
            if (running.Summary is not null) running.CloseSummary();
            if (loaded.Summary is not null) loaded.CloseSummary();
            while (running.LiveLot is { } a) running.Run(new GameAction.AuctionPass(a.Id));
            while (loaded.LiveLot is { } b) loaded.Run(new GameAction.AuctionPass(b.Id));
        }
        Assert.Equal(Json.Serialize(running.State), Json.Serialize(loaded.State));
    }

    [Fact]
    public void Mehrere_Spielstaende_neuester_zuerst_und_loeschen()
    {
        var store = new FileSaveStore(root);
        Assert.True(store.Write("alt", NewGame(1)));
        Thread.Sleep(30);
        Assert.True(store.Write("neu", NewGame(2)));
        Assert.Equal(["neu", "alt"], store.List().Select(s => s.Id));
        store.Delete("neu");
        Assert.Equal(["alt"], store.List().Select(s => s.Id));
        store.Delete("gibt-es-nicht");
    }

    [Fact]
    public void Kaputte_und_fremde_Dateien_werden_uebergangen()
    {
        var store = new FileSaveStore(root);
        store.Write("gut", NewGame());
        File.WriteAllText(Path.Combine(store.Location!, "kaputt.json"), "{ das ist kein JSON");
        File.WriteAllText(Path.Combine(store.Location!, "leer.json"), "{}");
        File.WriteAllText(Path.Combine(store.Location!, "alt.json"), "{\"format\":1,\"savedAt\":0,\"state\":{\"version\":9}}");
        Assert.Equal(["gut"], store.List().Select(s => s.Id));
    }

    [Fact]
    public void Spielstand_im_Format_des_alten_Spiels_wird_gelesen()
    {
        // Datei aus dem TypeScript-Spiel (npm run trace), wie sie unter %LOCALAPPDATA%\WinzerAI\Spielstände läge
        var source = Path.Combine(AppContext.BaseDirectory, "Saves", "alle-mosel.json");
        var store = new FileSaveStore(root);
        File.Copy(source, Path.Combine(store.Location!, "2026-10-16_14-03-22-512.json"));
        var slot = Assert.Single(store.List());
        Assert.Equal(4, slot.State.Players.Count);
        Assert.Equal(1, slot.State.RulesVersion);
        // Und es lässt sich darauf weiterspielen
        var next = Ai.PlayAiTurn(slot.State);
        Assert.NotEqual(Json.Serialize(slot.State), Json.Serialize(next));
    }

    [Fact]
    public void Einstellungen_bleiben_ueber_Neustarts_erhalten()
    {
        new FileSaveStore(root).SetSetting("muted", "1");
        var again = new FileSaveStore(root);
        Assert.Equal("1", again.GetSetting("muted"));
        Assert.Null(again.GetSetting("unbekannt"));
    }
}
