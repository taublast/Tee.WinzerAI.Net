using DrawnUi.Draw;
using Winzer.Engine;
using Winzer.Presentation;

namespace Winzer.Ui;

/// <summary>Was über der Szene liegt: Entscheidung, Übersicht, Menü, Meldungen.</summary>
public static class Overlays
{
    /// <summary>Abgedunkelter Hintergrund, der Eingaben abfängt. <paramref name="opaque"/> verdeckt beim Spielerwechsel die Werte des Vorgängers.</summary>
    public static SkiaLayout Backdrop(bool opaque = false, Action? onTap = null)
    {
        var back = Kit.Box(0, 0, Kit.DesignWidth, Kit.DesignHeight, Theme.C(opaque ? "#f5140a0e" : "#b3140a0e"));
        back.Tap(onTap ?? (() => { }));
        return back;
    }

    public static SkiaControl Toast(string text, Action dismiss)
    {
        var toast = Kit.Box(460, 980, 1000, 72, Theme.C("#f2fbf4e4"), 16, Theme.Alert, 3, shadow: true);
        toast.With(Kit.Text(text, 28, 12, 880, 52, 20, Theme.Alert, bold: true, maxLines: 2));
        toast.With(Kit.Text("✕", 930, 20, 50, 32, 22, Theme.Alert, bold: true, align: Align.Center));
        toast.Tap(dismiss);
        return toast;
    }

    /// <summary>Entscheidungsereignis: Titel, Beschreibung, Frist und je Möglichkeit eine Schaltfläche mit Folgen.</summary>
    public static SkiaControl Decision(GameSession session)
    {
        var decision = session.Player.Decision!;
        var back = Backdrop(onTap: () => session.Nav.Show(Overlay.None));
        var count = decision.Options.Count;
        var height = 330 + count * 104;
        var top = (Kit.DesignHeight - height) / 2;
        var card = Kit.Card(460, top, 1000, height, 24);
        card.Tap(() => { });
        card.With(Kit.Box(0, 0, 1000, 12, Theme.Wine, 6));
        card.With(Kit.Text(decision.Title, 48, 44, 904, 52, 38, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Text(decision.Text, 48, 108, 904, 130, 22, Theme.Ink, maxLines: 4));
        var nextMonth = (decision.Deadline ?? 0) > Bio.GameMonth(session.State);
        card.With(Kit.Text($"Gilt bis {(nextMonth ? "Ende nächsten Monats" : "Monatsende")}. Ohne Antwort: „{decision.Options[decision.Fallback].Label}“.",
            48, 244, 904, 30, 18, Theme.InkSoft));
        for (var i = 0; i < count; i++)
        {
            var index = i;
            var option = decision.Options[i];
            var title = option.Label + (option.Cost > 0 ? $" ({Util.FormatEuro(option.Cost)})" : "");
            card.With(Kit.Button(title, 48, 292 + i * 104, 904, 92,
                () => { session.Nav.Overlay = Overlay.None; session.Run(new GameAction.Decide(index)); },
                i == 0 ? ButtonKind.Primary : ButtonKind.Secondary, option.Hint, session.Problem(new GameAction.Decide(index)), 23));
        }
        return back.With(card);
    }

    /// <summary>Übersicht nach dem Vorspulen und beim Spielerwechsel: Wer ist dran, was steht an, was ist passiert?</summary>
    public static SkiaControl Summary(GameSession session, Summary summary)
    {
        var state = session.State;
        var back = Backdrop(summary.Changed);
        var card = Kit.Card(360, 90, 1200, 900, 24);
        card.Tap(() => { });
        var y = 40.0;
        if (summary.Changed)
        {
            card.With(Kit.Box(48, y, 1104, 96, radius: 16, gradient: Kit.Vertical(Theme.Wine, Theme.WineDark)));
            card.With(Kit.Text("Spielerwechsel", 76, y + 12, 600, 26, 18, Theme.C("#f0cfd6")));
            card.With(Kit.Text($"{summary.PlayerName} ist am Zug", 76, y + 38, 1040, 46, 34, Theme.C("#fff6e6"), bold: true, serif: true));
            y += 120;
        }
        card.With(Kit.Text($"Jetzt: {Game.MonthNames[state.Month - 1]}, Jahr {state.Year}", 48, y, 1104, 50, 36, Theme.Ink, bold: true, serif: true));
        var good = summary.Entries.Count(e => e.Tone == LogTone.Good);
        var bad = summary.Entries.Count(e => e.Tone == LogTone.Bad);
        var lead = summary.Changed ? "Seit deinem letzten Zug" : "Unterwegs";
        card.With(Kit.Text(summary.Entries.Count == 0 ? $"{lead} keine besonderen Vorkommnisse." : $"{lead}: {good} gute und {bad} schlechte Nachrichten",
            48, y + 52, 1104, 30, 20, Theme.InkSoft));
        y += 100;

        // Links: Die Arbeit ruft
        card.With(Kit.Text("Die Arbeit ruft", 48, y, 520, 34, 24, Theme.Wine, bold: true, serif: true));
        var ry = y + 46;
        if (summary.Reasons.Count > 0)
        {
            foreach (var reason in summary.Reasons.Take(6))
            {
                var scene = Navigator.SceneFor(reason.Area);
                var row = Kit.Box(48, ry, 520, 84, Theme.C("#33d9c7a3"), 12);
                row.With(Kit.Text(reason.Text, 16, 8, 340, 70, 17, Theme.Ink, maxLines: 3));
                if (reason.Area != HintArea.Hof)
                {
                    row.With(Kit.Button($"{Navigator.LabelFor(scene)} ›", 368, 18, 140, 48, () => session.CloseSummary(scene), ButtonKind.Ghost, fontSize: 17, align: Align.Center));
                }
                card.With(row);
                ry += 94;
            }
        }
        else
        {
            var text = summary.Over ? "Das Spiel ist zu Ende."
                : summary.Skipped ? $"Höchstens {summary.Months} Monate am Stück – es stand keine wichtige Entscheidung an."
                : "Nichts Dringendes – Zeit für Marketing oder Investitionen.";
            card.With(Kit.Text(text, 48, ry, 520, 80, 19, Theme.InkSoft, maxLines: 3));
        }

        // Rechts: Was passiert ist, geblättert
        card.With(Kit.Text("Was geschehen ist", 620, y, 520, 34, 24, Theme.Wine, bold: true, serif: true));
        const int perPage = 7;
        var pages = Math.Max(1, (int)Math.Ceiling(summary.Entries.Count / (double)perPage));
        var page = session.Nav.Page("summary", pages);
        var ey = y + 46;
        foreach (var entry in summary.Entries.Skip(page * perPage).Take(perPage))
        {
            var color = entry.Tone == LogTone.Good ? Theme.GoodLight : entry.Tone == LogTone.Bad ? Theme.Warn : Theme.GoldDark;
            card.With(Kit.Box(620, ey + 8, 12, 12, color, 6));
            card.With(Kit.Text($"{Game.MonthNames[entry.Month - 1]} J. {entry.Year}: {entry.Text}", 644, ey, 508, 72, 17, Theme.Ink, maxLines: 3));
            ey += 78;
        }
        if (pages > 1) card.With(Kit.Pager(620, 810, 300, page, pages, p => session.Nav.SetPage("summary", p), dark: false));

        card.With(Kit.Button(summary.Changed ? "Los geht's" : "Weiter", 932, 800, 220, 64,
            () => { session.Nav.SetPage("summary", 0); session.CloseSummary(); }, ButtonKind.Primary, fontSize: 24, align: Align.Center));
        return back.With(card);
    }

    public static SkiaControl Menu(GameSession session, AppHost app)
    {
        var back = Backdrop(onTap: () => session.Nav.Show(Overlay.None));
        var card = Kit.Card(660, 300, 600, 480, 24);
        card.Tap(() => { });
        card.With(Kit.Text("Menü", 48, 40, 504, 50, 36, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Text("Der Spielstand wird nach jedem Zug gesichert.", 48, 96, 504, 30, 18, Theme.InkSoft));
        card.With(Kit.Button("Weiterspielen", 48, 150, 504, 72, () => session.Nav.Show(Overlay.None), ButtonKind.Primary, fontSize: 24));
        card.With(Kit.Button(app.Audio.Muted ? "Ton einschalten" : "Ton ausschalten", 48, 236, 504, 72, () =>
        {
            app.Audio.Muted = !app.Audio.Muted;
            app.Store.SetSetting("muted", app.Audio.Muted ? "1" : "0");
            session.Nav.Notify();
        }, fontSize: 24));
        card.With(Kit.Button("Zum Hauptmenü", 48, 322, 504, 72, app.ShowMenu, fontSize: 24));
        return back.With(card);
    }
}
