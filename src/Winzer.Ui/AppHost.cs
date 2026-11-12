using DrawnUi.Draw;
using DrawnUi.Views;
using Winzer.Engine;
using Winzer.Presentation;
using Winzer.Ui.Scenes;

namespace Winzer.Ui;

/// <summary>
/// Die Anwendung oberhalb der Köpfe: Hauptmenü oder laufende Partie. Der Kopf (WPF, Blazor) fragt
/// <see cref="Build"/> nach dem Baum für die aktuelle Fenstergröße und baut bei <see cref="Changed"/> neu auf.
/// </summary>
public sealed class AppHost(ISaveStore store, IAudio audio)
{
    // Gezeichnete Pfade entstehen aus Zahlen im Text („M0 12.5 L…“). Unter deutscher Kultur würde daraus „12,5“ und der
    // Pfad wäre ungültig. Sichtbare Zahlen formatiert Util selbst, deshalb läuft die Oberfläche kulturneutral.
    readonly bool neutral = UseNeutralCulture();

    static bool UseNeutralCulture()
    {
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        return true;
    }

    public ISaveStore Store { get; } = store;
    public IAudio Audio { get; } = audio;
    public GameSession? Session { get; private set; }
    /// <summary>Zustand des Hauptmenüs (Schritt, Auswahl), der einen Neuaufbau überdauert</summary>
    public MenuState Menu { get; } = new();
    public event Action? Changed;
    /// <summary>Der Kopf soll die Anwendung beenden</summary>
    public event Action? QuitRequested;

    public void Notify() => Changed?.Invoke();

    public void Quit() => QuitRequested?.Invoke();

    public void ShowMenu()
    {
        Session = null;
        Menu.Reset();
        Changed?.Invoke();
    }

    public void Start(GameState state, string? saveId = null)
    {
        var id = saveId ?? NewSaveId(DateTime.Now);
        if (saveId is null) Store.Write(id, state);
        Session = new GameSession(state, id, Store, Audio);
        Session.Changed += () => Changed?.Invoke();
        Changed?.Invoke();
    }

    /// <summary>Neue Speicherplatz-ID, zugleich lesbarer Dateiname, z. B. 2026-10-16_14-03-22-512</summary>
    public static string NewSaveId(DateTime date) => date.ToString("yyyy-MM-dd_HH-mm-ss-fff");

    /// <summary>Baut die Oberfläche für ein Fenster dieser Größe (in Bildschirmpunkten).</summary>
    public SkiaControl Build(double width, double height)
    {
        Kit.S = Math.Max(0.2, Math.Min(width / Kit.DesignWidth, height / Kit.DesignHeight));
        Kit.ResetHover();
        var root = new SkiaLayout
        {
            Type = LayoutType.Absolute,
            BackgroundColor = Theme.C("#120c0e"),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
        // Entwurfsfläche mittig; bei abweichendem Seitenverhältnis bleibt ein dunkler Rand
        var offsetX = (width / Kit.S - Kit.DesignWidth) / 2;
        var offsetY = (height / Kit.S - Kit.DesignHeight) / 2;
        var frame = Kit.Layer(offsetX, offsetY, Kit.DesignWidth, Kit.DesignHeight);
        frame.IsClippedToBounds = true;
        frame.With(Session is { } session ? Shell.Build(session, this) : MainMenu.Build(this));
        return root.With(frame);
    }

    /// <summary>Tastenkürzel; liefert true, wenn die Taste verarbeitet wurde.</summary>
    public bool HandleKey(string key)
    {
        if (Session is not { } session) return false;
        var nav = session.Nav;
        if (key == "Escape")
        {
            if (nav.Overlay != Overlay.None) nav.Show(Overlay.None);
            else if (session.Error.Length > 0) session.ClearError();
            else nav.Show(Overlay.Menu);
            return true;
        }
        if (session.Summary is not null)
        {
            if (key is "Space" or "Enter") session.CloseSummary();
            return true;
        }
        if (session.LiveLot is not null || nav.Overlay != Overlay.None) return false;
        foreach (var info in Navigator.Scenes)
        {
            if (info.Key == key)
            {
                nav.Go(info.Scene);
                return true;
            }
        }
        switch (key)
        {
            case "Space" when !session.Over:
                session.Run(new GameAction.EndTurn());
                return true;
            case "F" when !session.Over:
                session.Run(new GameAction.AutoSkipTurn());
                return true;
            case "L":
                nav.Show(Overlay.Lexicon);
                return true;
            case "Tab":
                nav.SetTab(nav.Tab + 1);
                return true;
        }
        return false;
    }
}

/// <summary>Zustand des Hauptmenüs</summary>
public sealed class MenuState
{
    /// <summary>Frei belegbar durch das Hauptmenü: Schritt, gewählte Werte</summary>
    public Dictionary<string, object> Values { get; } = [];
    public int Step { get; set; }

    public T Get<T>(string key, T fallback) => Values.TryGetValue(key, out var value) && value is T typed ? typed : fallback;

    public void Set(string key, object value) => Values[key] = value;

    public void Reset()
    {
        Step = 0;
    }
}
