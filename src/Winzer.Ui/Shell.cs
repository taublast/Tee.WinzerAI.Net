using DrawnUi.Draw;
using DrawnUi.Views;
using SkiaSharp;
using Winzer.Engine;
using Winzer.Engine.Data;
using Winzer.Presentation;
using Winzer.Ui.Scenes;

namespace Winzer.Ui;

/// <summary>
/// Rahmen einer laufenden Partie: Kopfzeile, Leiste links, Szene und alles, was darüber liegt
/// (Entscheidung, Versteigerung, Übersicht, Lexikon, Menü, Meldungen).
/// </summary>
public static class Shell
{
    public const double HudHeight = 92;
    public const double RailWidth = 112;
    public const double StageWidth = Kit.DesignWidth - RailWidth;
    public const double StageHeight = Kit.DesignHeight - HudHeight;

    public static SkiaControl Build(GameSession session, AppHost app)
    {
        Live.State = session.State;
        var frame = Kit.Layer(0, 0, Kit.DesignWidth, Kit.DesignHeight);
        var stage = Kit.Layer(RailWidth, HudHeight, StageWidth, StageHeight);
        stage.IsClippedToBounds = true;
        stage.With(session.Nav.Scene switch
        {
            Scene.Weinberg => VineyardScene.Build(session),
            Scene.Keller => CellarScene.Build(session),
            Scene.Markt => MarketScene.Build(session),
            Scene.Kontor => OfficeScene.Build(session),
            Scene.Dorf => VillageScene.Build(session),
            _ => EstateScene.Build(session),
        });
        frame.With(stage, Hud(session), Rail(session));

        if (session.Over) frame.With(GameOverBanner(session, app));
        if (session.Summary is { } summary) frame.With(Overlays.Summary(session, summary));
        else if (session.LiveLot is { } lot) frame.With(AuctionOverlay.Build(session, lot));
        else if (session.Nav.Overlay == Overlay.Decision && session.Player.Decision is not null && !session.Over) frame.With(Overlays.Decision(session));
        else if (session.Nav.Overlay == Overlay.Show && session.State.LastShow is not null) frame.With(OverlaysV2.Show(session));
        else if (session.Nav.Overlay == Overlay.Review && session.Player.Review is not null) frame.With(OverlaysV2.Review(session));
        else if (session.Nav.Overlay == Overlay.Plan && RulesV2.On(session.State)) frame.With(OverlaysV2.Plan(session));
        else if (session.Nav.Overlay == Overlay.Lexicon) frame.With(LexiconOverlay.Build(session));
        else if (session.Nav.Overlay == Overlay.Menu) frame.With(Overlays.Menu(session, app));

        if (session.Error.Length > 0) frame.With(Overlays.Toast(session.Error, session.ClearError));
        else if (session.SaveFailed) frame.With(Overlays.Toast("Der Spielstand konnte nicht gesichert werden.", () => { }));
        return frame;
    }

    /// <summary>Offene wichtige Aufgaben je Szene – als Zähler an Leiste und Gebäuden</summary>
    public static Dictionary<Scene, int> Badges(GameSession session)
    {
        var counts = new Dictionary<Scene, int>();
        if (session.Over) return counts;
        var player = session.Player;
        foreach (var hint in Advisor.MonthHints(session.State, player))
        {
            if (!hint.Important || hint.Area == HintArea.Auftraege) continue;
            var scene = Navigator.SceneFor(hint.Area);
            counts[scene] = counts.GetValueOrDefault(scene) + 1;
        }
        // Aufträge immer zählen – Anfragen, angenommene Aufträge und Exportanfragen –, damit nichts untergeht
        var orders = player.OrderOffers.Count + player.Orders.Count + (player.ExportOffer is null ? 0 : 1);
        if (orders > 0) counts[Scene.Markt] = orders;
        return counts;
    }

    public static string ReputationWord(double reputation) =>
        reputation >= 60 ? "gefragt" : reputation >= 30 ? "bekannt" : "Geheimtipp";

    static SkiaControl Hud(GameSession session)
    {
        var state = session.State;
        var player = session.Player;
        var region = Regions.All[player.RegionId];
        var bar = Kit.Box(0, 0, Kit.DesignWidth, HudHeight, Theme.Glass);

        // Jahresrad
        bar.With(Kit.Paint(22, 6, 80, 80, (c, w, h) =>
        {
            Draw.Ring(c, 40, 40, 32, 10, state.Month / 12.0, Theme.SkCream.WithAlpha(60), Theme.SkGold);
        }));
        bar.With(Kit.Text(state.Month.ToString(), 22, 31, 80, 30, 22, Theme.Cream, bold: true, serif: true, align: Align.Center));
        var date = $"{RulesV2.TurnName(state)} · Jahr {state.Year}{(state.YearLimit is > 0 ? $" von {state.YearLimit}" : "")}";
        bar.With(Kit.Text(date, 112, 10, 440, 40, date.Length > 24 ? 24 : 30, Theme.Cream, bold: true, serif: true));
        var sub = $"{player.Name} · {region.Name}";
        if (state.Challenge is not null) sub += $" · Tages-Challenge {state.Challenge}";
        if (Rules.IsEndless(state)) sub += " · Endlosspiel";
        foreach (var other in state.Players.Where(p => p.Ai is null && p.AutoSkip is not null && p.Id != player.Id)) sub += $" · {other.Name} spult vor";
        bar.With(Kit.Text(sub, 112, 52, 440, 28, 19, Theme.CreamSoft));

        // Konto, Punkte, Bekanntheit
        bar.With(Pill(560, 230, (c, w, h) =>
        {
            using var gold = Draw.VerticalGradient(0, h, (0, "#f6d477"), (1, "#c8962e"));
            c.DrawCircle(w / 2, h / 2, 15, gold);
            using var ink = Draw.Stroke(Theme.SkInk, 2.6f);
            Draw.Path(c, "M21 9.5 a7 7 0 1 0 0 11 M8 13 h9 M8 17 h9", ink);
        }, Util.FormatEuro(player.Money), null, player.Money < 0 ? Theme.C("#ff9b8a") : Theme.Cream)
            .Tap(() => session.Nav.Go(Scene.Kontor)));

        var hasTarget = Rules.HasPointTarget(state);
        var points = Util.FormatNumber(player.Points) + (hasTarget ? $" / {Util.FormatNumber(state.TargetPoints)}" : "");
        var pointsPill = Pill(806, 230, (c, w, h) =>
        {
            using var gold = Draw.VerticalGradient(0, h, (0, "#f6d477"), (1, "#c8962e"));
            Draw.Path(c, "M5 4 h20 v8 a10 10 0 0 1 -20 0 Z M12 22 h6 v5 h-6 Z M8 27 h14 v3 h-14 Z", gold);
        }, points, null, Theme.Cream).Tap(() => session.Nav.Go(Scene.Dorf));
        if (hasTarget)
        {
            pointsPill.With(Kit.Box(56, 42, 150, 4, Theme.GlassLine, 2));
            pointsPill.With(Kit.Box(56, 42, Math.Max(4, 150 * Math.Clamp(player.Points / state.TargetPoints, 0, 1)), 4, Theme.Gold, 2));
        }
        bar.With(pointsPill);

        bar.With(Pill(1052, 200, (c, w, h) =>
        {
            using var gold = Draw.VerticalGradient(0, h, (0, "#f6d477"), (1, "#c8962e"));
            Draw.Path(c, "M15 1 l4.5 9.5 10.5 1.3 -7.7 7.2 2 10.3 -9.3 -5.1 -9.3 5.1 2 -10.3 -7.7 -7.2 10.5 -1.3 Z", gold);
        }, Util.FormatNumber(Js.Round(player.Reputation)), ReputationWord(player.Reputation), Theme.Cream));

        // Wetteraussichten für den laufenden Monat
        var likeliest = Weather.WeatherKinds.OrderByDescending(k => state.Forecast[k]).First();
        var left = 1264.0;
        foreach (var kind in Weather.WeatherKinds)
        {
            var on = kind == likeliest;
            var chip = Kit.Box(left, 14, 128, 64, on ? Theme.C("#2ef4ead7") : Theme.C("#14f4ead7"), 12, on ? Theme.Gold : null, 2);
            var k = kind;
            chip.With(Kit.Paint(8, 14, 36, 36, (c, w, h) => WeatherIcon(c, k, w)));
            chip.With(Kit.Text($"{Js.Round(state.Forecast[kind] * 100)} %", 48, 8, 78, 26, 19, Theme.Cream, bold: true));
            chip.With(Kit.Text(Weather.WeatherLabels[kind], 46, 35, 82, 22, 13, Theme.CreamSoft));
            bar.With(chip);
            left += 134;
        }

        if (RulesV2.On(state))
        {
            // Gutsplanung: Ziele, Ausbau, Preise
            bar.With(Kit.Box(1730, 22, 48, 48, Theme.GlassLight, 12)
                .With(Kit.Paint(8, 8, 32, 32, (c, w, h) =>
                {
                    using var p = Draw.Stroke(Theme.SkGold, 3);
                    c.DrawCircle(16, 16, 12, p);
                    c.DrawCircle(16, 16, 5, p);
                    c.DrawLine(16, 0, 16, 6, p);
                }))
                .Tap(() => session.Nav.Show(Overlay.Plan)));
        }
        bar.With(Kit.IconButton("?", RulesV2.On(state) ? 1672 : 1730, 22, 48, () => session.Nav.Show(Overlay.Lexicon)));
        bar.With(Kit.IconButton("≡", 1788, 22, 48, () => session.Nav.Show(Overlay.Menu)));
        if (player.Decision is not null && !session.Over)
        {
            bar.With(Kit.Box(1846, 22, 56, 48, radius: 12, gradient: Kit.Vertical(Theme.Gold, Theme.GoldDark))
                .With(Kit.Text("!", 0, 6, 56, 36, 26, Theme.Ink, bold: true, align: Align.Center))
                .Tap(() => session.Nav.Show(Overlay.Decision)));
        }
        return bar;
    }

    static SkiaShape Pill(double x, double w, Action<SKCanvas, float, float> icon, string value, string? word, Color color)
    {
        var pill = Kit.Box(x, 20, w, 52, Theme.GlassLight, 26, Theme.GlassLine, 1.5);
        pill.With(Kit.Paint(13, 11, 30, 30, icon));
        var valueWidth = word is null ? w - 66 : 22 + value.Length * 14;
        pill.With(Kit.Text(value, 56, 9, valueWidth, 34, 24, color, bold: true));
        if (word is not null) pill.With(Kit.Text(word, 56 + valueWidth, 17, w - 62 - valueWidth, 24, 15, Theme.CreamSoft));
        return pill;
    }

    public static void WeatherIcon(SKCanvas c, WeatherKind kind, float size)
    {
        var u = size / 36f;
        c.Save();
        c.Scale(u);
        switch (kind)
        {
            case WeatherKind.Sonnig:
                using (var sun = Draw.Fill("#ffd27a")) c.DrawCircle(18, 18, 9, sun);
                using (var ray = Draw.Stroke("#ffd27a", 2.5f))
                {
                    for (var i = 0; i < 8; i++)
                    {
                        var a = i * Math.PI / 4;
                        c.DrawLine(18 + (float)Math.Cos(a) * 13, 18 + (float)Math.Sin(a) * 13, 18 + (float)Math.Cos(a) * 17, 18 + (float)Math.Sin(a) * 17, ray);
                    }
                }
                break;
            case WeatherKind.Wechselhaft:
                using (var sun = Draw.Fill("#ffd27a")) c.DrawCircle(13, 13, 8, sun);
                using (var cloud = Draw.Fill("#dfe6ee"))
                {
                    c.DrawOval(21, 23, 12, 7, cloud);
                    c.DrawCircle(17, 19, 6, cloud);
                }
                break;
            default:
                using (var cloud = Draw.Fill("#c9d7e6"))
                {
                    c.DrawOval(18, 14, 14, 8, cloud);
                    c.DrawCircle(13, 10, 6, cloud);
                }
                using (var rain = Draw.Stroke("#9cc0d8", 2.5f))
                {
                    c.DrawLine(11, 25, 9, 32, rain);
                    c.DrawLine(19, 25, 17, 32, rain);
                    c.DrawLine(27, 25, 25, 32, rain);
                }
                break;
        }
        c.Restore();
    }

    static SkiaControl Rail(GameSession session)
    {
        var rail = Kit.Box(0, HudHeight, RailWidth, StageHeight, Theme.Glass);
        var badges = Badges(session);
        var y = 34.0;
        foreach (var info in Navigator.Scenes)
        {
            var on = session.Nav.Scene == info.Scene;
            var scene = info.Scene;
            var item = Kit.Layer(0, y, RailWidth, 118);
            item.With(on
                ? Kit.Box(20, 0, 72, 72, radius: 36, gradient: Kit.Vertical(Theme.Gold, Theme.GoldDark))
                : Kit.Box(20, 0, 72, 72, Theme.GlassLight, 36));
            item.With(Kit.Paint(20, 0, 72, 72, (c, w, h) => SceneIcon(c, scene, on ? Theme.SkInk : Theme.SkCreamSoft)));
            item.With(Kit.Text(info.Label, 0, 78, RailWidth, 24, 17, on ? Theme.Gold : Theme.CreamSoft, bold: on, align: Align.Center));
            if (badges.GetValueOrDefault(scene) is > 0 and var count) item.With(Kit.Badge(count, 84, 8));
            item.Tap(() => session.Nav.Go(scene));
            rail.With(item);
            y += 128;
        }
        // Zug beenden von jeder Szene aus
        if (!session.Over)
        {
            rail.With(Kit.Box(16, 824, 80, 60, Theme.GlassLight, 16, Theme.GlassLine, 1.5)
                .With(Kit.Paint(22, 14, 36, 32, (c, w, h) =>
                {
                    using var p = Draw.Fill(Theme.SkCream);
                    Draw.Path(c, "M2 4 l14 12 -14 12 Z M18 4 l14 12 -14 12 Z", p);
                }))
                .Tap(() => session.Run(new GameAction.AutoSkipTurn())));
            rail.With(Kit.Box(16, 894, 80, 80, radius: 18, gradient: Kit.Vertical(Theme.Wine, Theme.WineDark), stroke: Theme.Gold, strokeWidth: 2.5)
                .With(Kit.Paint(24, 14, 32, 32, (c, w, h) =>
                {
                    using var p = Draw.Fill(SKColor.Parse("#fff6e6"));
                    Draw.Path(c, "M6 3 l20 13 -20 13 Z", p);
                }))
                .With(Kit.Text(session.HotSeat || RulesV2.On(session.State) ? "Zug" : "Monat", 0, 50, 80, 22, 15, Theme.C("#f0cfd6"), align: Align.Center))
                .Tap(() => session.Run(new GameAction.EndTurn())));
        }
        return rail;
    }

    static void SceneIcon(SKCanvas c, Scene scene, SKColor color)
    {
        using var fill = Draw.Fill(color);
        using var line = Draw.Stroke(color, 4);
        switch (scene)
        {
            case Scene.Gut:
                Draw.Path(c, "M18 40 L36 22 L54 40 V54 H18 Z", fill);
                break;
            case Scene.Weinberg:
                c.DrawCircle(28, 30, 7, fill);
                c.DrawCircle(44, 30, 7, fill);
                c.DrawCircle(36, 44, 7, fill);
                Draw.Path(c, "M36 22 V14 M36 16 q8 -6 12 0", line);
                break;
            case Scene.Keller:
                c.DrawOval(36, 36, 18, 14, line);
                c.DrawLine(18, 36, 54, 36, line);
                c.DrawLine(24, 26, 48, 26, line);
                c.DrawLine(24, 46, 48, 46, line);
                break;
            case Scene.Markt:
                Draw.Path(c, "M20 30 H52 L48 52 H24 Z M28 30 a8 8 0 0 1 16 0", line);
                break;
            case Scene.Kontor:
                Draw.Path(c, "M22 20 H50 V52 H22 Z M29 30 H43 M29 38 H43 M29 46 H38", line);
                break;
            default:
                c.DrawCircle(27, 29, 7, fill);
                c.DrawCircle(45, 29, 7, fill);
                Draw.Path(c, "M15 52 a12 12 0 0 1 24 0 Z M33 52 a12 12 0 0 1 24 0 Z", fill);
                break;
        }
    }

    static SkiaControl GameOverBanner(GameSession session, AppHost app)
    {
        var state = session.State;
        var text = state.Challenge is not null && state.Finished ? $"Tages-Challenge beendet. {session.ChallengeMessage}"
            : session.Winner is { } winner ? $"{winner.Name} hat gewonnen!"
            : state.Finished ? "Alle Weingüter sind insolvent – Spiel vorbei."
            : "Du bist insolvent und scheidest aus.";
        var banner = Kit.Card(460, 104, 1000, 84);
        banner.With(Kit.Text(text, 28, 24, 700, 36, 26, Theme.Ink, bold: true, serif: true));
        banner.With(Kit.Button("Neues Spiel", 760, 14, 216, 56, () => app.ShowMenu(), ButtonKind.Primary, align: Align.Center));
        return banner;
    }
}
