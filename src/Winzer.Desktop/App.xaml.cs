using System.Windows;
using Winzer.Engine;
using Winzer.Presentation;
using Winzer.Ui;

namespace Winzer.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var shot = ShotOptions.Parse(e.Args);
        if (shot is not null)
        {
            RunShot(shot);
            return;
        }
        var store = new FileSaveStore();
        var audio = new SynthAudio { Muted = store.GetSetting("muted") == "1" };
        var window = new MainWindow(new AppHost(store, audio));
        MainWindow = window;
        window.Show();
    }

    /// <summary>
    /// Entwicklerhilfe: <c>--shot datei.png [--scene Keller] [--tab 1] [--months 9] [--seed 7] [--region mosel] [--menu]
    /// [--overlay Lexicon|Plan|Review|Show|Auto] [--otab 1] [--rules 1|2] [--click 1750,1015] [--size 1920x1080]</c> spielt eine Partie einige Monate weiter, zeigt die gewünschte Szene,
    /// speichert ein Bildschirmfoto und beendet sich. Es wird nichts auf die Platte gesichert. <c>--click</c> klickt vorher
    /// mit der echten Maus an Stellen der Entwurfsfläche (bewegt den Zeiger kurz); neben dem Bild entsteht eine Textdatei
    /// mit dem Stand danach.
    /// </summary>
    async void RunShot(ShotOptions shot)
    {
        var app = new AppHost(new MemorySaveStore(), new SilentAudio());
        if (!shot.Menu)
        {
            var region = Json.Enum<RegionId>(shot.Region);
            app.Start(Game.NewGame(new NewGameOptions
            {
                Seed = shot.Seed,
                TargetPoints = 600,
                RulesVersion = shot.Rules,
                Players =
                [
                    new NewPlayer { Name = "Weingut Sonnenhang", RegionId = region },
                    new NewPlayer { Name = Ai.AiEstateNames[AiProfileId.Tradition], RegionId = region, Ai = AiProfileId.Tradition },
                    new NewPlayer { Name = Ai.AiEstateNames[AiProfileId.Bio], RegionId = region, Ai = AiProfileId.Bio },
                    new NewPlayer { Name = Ai.AiEstateNames[AiProfileId.Schlitzohr], RegionId = region, Ai = AiProfileId.Schlitzohr },
                ],
            }));
            var session = app.Session!;
            // Ambitionen und Jahresziel wählen, damit die Gutsplanung nicht jedes Bild verdeckt
            void Choose()
            {
                foreach (var ambition in RulesV2.Ambitions.Take(RulesV2.AmbitionCount))
                {
                    var choose = new GameAction.ChooseAmbition(ambition.Id);
                    if (session.Problem(choose) is null) session.Run(choose);
                }
                if (session.Player.Goal is null && session.Player.GoalOffers.Count > 0) session.Run(new GameAction.ChooseGoal(session.Player.GoalOffers[0]));
                while (session.Nav.Overlay != Overlay.None) session.Nav.Show(Overlay.None);
            }
            if (shot.Rules >= 2 && shot.Months > 0) Choose();
            for (var i = 0; i < shot.Months && !session.Over; i++)
            {
                if (shot.Rules >= 2) Choose();
                // Offene Versteigerungen und Übersichten abräumen, damit die Partie weiterläuft
                while (session.LiveLot is { } lot) session.Run(new GameAction.AuctionPass(lot.Id));
                if (session.Summary is not null) session.CloseSummary();
                foreach (var script in shot.Script) script(session);
                session.Run(new GameAction.EndTurn());
            }
            if (!shot.KeepSummary && session.Summary is not null) session.CloseSummary();
            var waiting = session.Nav.Overlay;
            session.Nav.Go(shot.Scene, shot.Tab);
            // --overlay Auto zeigt, was von selbst erschienen wäre (Prämierung, Rückblick, Gutsplanung)
            if (shot.AutoOverlay) session.Nav.Show(waiting);
            else if (shot.Overlay != Overlay.None) session.Nav.Show(shot.Overlay);
            session.Nav.SetOverlayTab(shot.OverlayTab);
        }
        var window = new MainWindow(app, accelerated: false) { SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.Manual, Left = 0, Top = 0 };
        window.Root.Width = shot.Width;
        window.Root.Height = shot.Height;
        MainWindow = window;
        window.Show();
        await Task.Delay(2500);
        if (shot.Clicks.Count > 0)
        {
            window.Topmost = true;
            window.Activate();
            await Task.Delay(400);
            foreach (var (x, y) in shot.Clicks)
            {
                await window.RealClick(x, y);
                await Task.Delay(1200);
            }
        }
        window.SaveScreenshot(shot.File);
        if (app.Session is { } played)
        {
            // Kurzprotokoll neben dem Bild: Was nach den Tipps Sache ist
            var p = played.Player;
            System.IO.File.WriteAllText(shot.File + ".txt",
                $"szene={played.Nav.Scene} reiter={played.Nav.Tab} overlay={played.Nav.Overlay} zug={RulesV2.TurnName(played.State)} jahr={played.State.Year} " +
                $"konto={Util.JsNumber(Math.Round(p.Money))} kredit={Util.JsNumber(p.Loan)} chargen={p.Cellar.Count} posten={p.Bottles.Count} parzellen={p.Plots.Count(x => x.Grape is not null)} " +
                $"preis={Util.JsNumber(p.PriceLevel)} ziel={p.Goal} ambitionen={p.Ambitions.Count} fehler={played.Error}");
        }
        Shutdown();
    }
}

sealed class ShotOptions
{
    public string File = "";
    public Scene Scene = Scene.Gut;
    public int Tab;
    public int Months;
    public int Seed = 7;
    public string Region = "mosel";
    public bool Menu;
    public bool KeepSummary;
    public Overlay Overlay = Overlay.None;
    public bool AutoOverlay;
    /// <summary>Stellen der Entwurfsfläche (1920 × 1080), die vor dem Foto nacheinander mit der echten Maus angeklickt werden</summary>
    public List<(double X, double Y)> Clicks = [];
    public int OverlayTab;
    public int Rules = 2;
    public double Width = 1280;
    public double Height = 720;
    public List<Action<GameSession>> Script = [];

    public static ShotOptions? Parse(string[] args)
    {
        var index = Array.IndexOf(args, "--shot");
        if (index < 0 || index + 1 >= args.Length) return null;
        var options = new ShotOptions { File = args[index + 1] };
        for (var i = 0; i < args.Length; i++)
        {
            var value = i + 1 < args.Length ? args[i + 1] : "";
            switch (args[i])
            {
                case "--scene": options.Scene = Enum.Parse<Scene>(value, true); break;
                case "--tab": options.Tab = int.Parse(value); break;
                case "--months": options.Months = int.Parse(value); break;
                case "--seed": options.Seed = int.Parse(value); break;
                case "--region": options.Region = value; break;
                case "--menu": options.Menu = true; break;
                case "--summary": options.KeepSummary = true; break;
                case "--overlay" when value == "Auto": options.AutoOverlay = true; break;
                case "--overlay": options.Overlay = Enum.Parse<Overlay>(value, true); break;
                case "--otab": options.OverlayTab = int.Parse(value); break;
                case "--click":
                    var point = value.Split(',');
                    options.Clicks.Add((double.Parse(point[0], System.Globalization.CultureInfo.InvariantCulture), double.Parse(point[1], System.Globalization.CultureInfo.InvariantCulture)));
                    break;
                case "--rules": options.Rules = int.Parse(value); break;
                case "--size":
                    var parts = value.Split('x');
                    options.Width = double.Parse(parts[0]);
                    options.Height = double.Parse(parts[1]);
                    break;
                case "--play":
                    // Lässt den Menschen wie den Traditionswinzer spielen, damit Keller und Markt gefüllt sind
                    options.Script.Add(AutoPlay);
                    break;
            }
        }
        return options;
    }

    /// <summary>Einfache Züge für gefüllte Bildschirmfotos: lesen, was reif ist, und abfüllen, was fertig ist.</summary>
    static void AutoPlay(GameSession session)
    {
        var state = session.State;
        var player = session.Player;
        // Flüssig bleiben, damit abgefüllt werden kann
        if (player.Money < 30_000)
        {
            var borrow = new GameAction.Borrow(Math.Min(60_000, Market.AvailableCredit(player)));
            if (borrow.Amount >= 1_000 && session.Problem(borrow) is null) session.Run(borrow);
        }
        if (state.Month == 10 && state.HalfMonth != 1)
        {
            foreach (var plot in player.Plots.ToList())
            {
                var harvest = new GameAction.Harvest(plot.Id);
                if (session.Problem(harvest) is null) session.Run(harvest);
            }
        }
        foreach (var batch in session.Player.Cellar.ToList())
        {
            if (batch.MonthsUntilReady > 0) continue;
            var bottle = new GameAction.Bottle(batch.Id, Math.Floor(batch.Hectoliters / 2));
            if (bottle.Hectoliters >= 1 && session.Problem(bottle) is null) session.Run(bottle);
        }
        foreach (var lot in session.Player.Bottles.ToList())
        {
            var register = new GameAction.Register(lot.Id);
            if (!lot.Registered && session.Problem(register) is null) session.Run(register);
        }
    }
}
