using DrawnUi.Draw;
using SkiaSharp;
using Winzer.Engine;
using Winzer.Engine.Data;
using Winzer.Presentation;

namespace Winzer.Ui.Scenes;

/// <summary>
/// Gutsansicht: das Weingut als Bühne. Parzellen und Gebäude führen in die Szenen, unten liegen die
/// Aufgaben des Monats, rechts oben die Neuigkeiten.
/// </summary>
public static class EstateScene
{
    /// <summary>Plätze für bis zu acht Parzellen im Hang (Koordinaten der Bühne)</summary>
    static readonly (string Path, float Cx, float Cy)[] Slots =
    [
        ("M138 498 L448 456 L528 568 L188 608 Z", 326, 532),
        ("M588 456 L848 428 L928 538 L648 572 Z", 754, 500),
        ("M1048 468 L1308 478 L1348 576 L1068 568 Z", 1194, 522),
        ("M1428 508 L1648 492 L1688 584 L1448 598 Z", 1554, 546),
        ("M318 368 L498 352 L538 424 L348 440 Z", 426, 396),
        ("M738 340 L918 330 L948 398 L758 410 Z", 842, 370),
        ("M1118 364 L1278 372 L1298 432 L1128 426 Z", 1206, 398),
        ("M1438 388 L1578 380 L1608 448 L1458 456 Z", 1522, 418),
    ];

    record Season(string SkyTop, string SkyMid, string SkyLow, string Far1, string Far2, string Mid1, string Mid2, string Near1, string Near2, bool Snow);

    static Season SeasonFor(int month) => month switch
    {
        12 or 1 or 2 => new("#33415c", "#7d8fa8", "#d9e2ec", "#9aa7b8", "#7c8a9d", "#cfd8e0", "#a9b6c2", "#e8eef2", "#b9c6cf", true),
        3 or 4 or 5 => new("#3f6fa8", "#8fb8de", "#e9f1d8", "#7f95a8", "#657b8c", "#9fbf6a", "#6f9a4a", "#86b04a", "#4a6e2a", false),
        6 or 7 or 8 => new("#2f6fb5", "#7fbbe8", "#f7edc0", "#7c8f9c", "#5f7480", "#8fae4a", "#5f8236", "#6f9a36", "#3a5a20", false),
        _ => new("#27364f", "#b8656a", "#f3b871", "#7c6a78", "#5d5468", "#8c7a45", "#5f6a35", "#6f7f33", "#34451f", false),
    };

    public static SkiaControl Build(GameSession session)
    {
        var state = session.State;
        var player = session.Player;
        var scene = Kit.Layer(0, 0, Shell.StageWidth, Shell.StageHeight);
        var badges = Shell.Badges(session);
        var shown = player.Plots.Take(Slots.Length).ToList();

        scene.With(Kit.Paint(0, 0, Shell.StageWidth, Shell.StageHeight, (c, w, h) => PaintPanorama(c, w, h, state, player, shown)));

        // Parzellen: Schild und Tippfläche
        for (var i = 0; i < shown.Count; i++)
        {
            var plot = shown[i];
            var slot = Slots[i];
            var small = i >= 4;
            var width = small ? 220.0 : 290.0;
            var ripe = IsHarvestable(state, plot);
            var label = ripe
                ? Kit.Box(slot.Cx - width / 2, slot.Cy - 32, width, 64, radius: 12, gradient: Kit.Vertical(Theme.Paper, Theme.PaperDark), shadow: true)
                : Kit.Box(slot.Cx - width / 2, slot.Cy - 32, width, 64, Theme.C("#c71c1418"), 12);
            label.With(Kit.Text(plot.Name, 14, 6, width - 28, 28, small ? 17 : 19, ripe ? Theme.Ink : Theme.Cream, bold: true));
            label.With(Kit.Text(PlotLine(state, plot), 14, 33, width - 28, 26, small ? 15 : 17, ripe ? Theme.InkSoft : Theme.CreamSoft));
            var id = plot.Id;
            label.Tap(() =>
            {
                session.Nav.Select("weinberg.plot", id);
                session.Nav.Go(Scene.Weinberg, 0);
            });
            scene.With(label);
            if (ripe) scene.With(Kit.Badge(1, slot.Cx + width / 2 - 4, slot.Cy - 30, 13));
        }
        if (player.Plots.Count > shown.Count)
        {
            scene.With(Kit.Button($"+ {player.Plots.Count - shown.Count} weitere Lagen", 28, 280, 250, 48, () => session.Nav.Go(Scene.Weinberg, 0), ButtonKind.Dark, fontSize: 18, align: Align.Center));
        }

        // Gebäude
        scene.With(Building("Kontor", 508, 822, badges.GetValueOrDefault(Scene.Kontor), () => session.Nav.Go(Scene.Kontor)));
        scene.With(Building("Keller", 778, 822, badges.GetValueOrDefault(Scene.Keller), () => session.Nav.Go(Scene.Keller)));
        scene.With(Building(player.HasVinothek ? "Vinothek" : "Hofladen", 1048, 822, badges.GetValueOrDefault(Scene.Markt), () => session.Nav.Go(Scene.Markt)));
        scene.With(Kit.Box(448, 660, 250, 150, Theme.Clear).Tap(() => session.Nav.Go(Scene.Kontor)));
        scene.With(Kit.Box(738, 700, 210, 110, Theme.Clear).Tap(() => session.Nav.Go(Scene.Keller)));
        scene.With(Kit.Box(998, 704, 230, 106, Theme.Clear).Tap(() => session.Nav.Go(Scene.Markt)));
        scene.With(Building("Dorf", 1640, 372, 0, () => session.Nav.Go(Scene.Dorf), 96));

        // Wetterhinweise oben links
        var chipY = 24.0;
        void Chip(string text)
        {
            scene.With(Kit.Box(28, chipY, 420, 44, Theme.C("#c71c1418"), 22).With(Kit.Text(text, 20, 9, 384, 28, 18, Theme.Cream)));
            chipY += 52;
        }
        var frost = Defense.FrostChance(state, player);
        if (frost > 0) Chip($"Spätfrostgefahr diesen Monat: {Js.Round(frost * 100)} %{(player.FrostCandles ? " · Kerzen stehen" : "")}");
        if (state.Month is 11 or 12) Chip($"Eiswein-Frost im Dezember: {Js.Round(Vineyard.IceWineChance(state, player.RegionId) * 100)} %");
        if (state.ClimateChange && state.Year > 1) Chip($"Klima: +{Util.FormatNumber(Rules.ClimateWarming(state), 1)} Wärmestufen seit Beginn");

        scene.With(News(session));
        scene.With(Tasks(session));

        if (!session.Over)
        {
            scene.With(Kit.Box(1374, 876, 96, 96, Theme.C("#cc1c1418"), 18, Theme.GlassLine, 1.5, shadow: true)
                .With(Kit.Paint(24, 18, 48, 44, (c, w, h) =>
                {
                    using var p = Draw.Fill(Theme.SkCream);
                    Draw.Path(c, "M2 4 l22 18 -22 18 Z M24 4 l22 18 -22 18 Z", p);
                }))
                .With(Kit.Text("Vorspulen", 0, 68, 96, 20, 14, Theme.CreamSoft, align: Align.Center))
                .Tap(() => session.Run(new GameAction.AutoSkipTurn())));
            var next = RulesV2.NextTurnName(state);
            var end = Kit.Box(1486, 876, 298, 96, radius: 18, gradient: Kit.Vertical(Theme.Wine, Theme.WineDark), stroke: Theme.Gold, strokeWidth: 3, shadow: true);
            end.With(Kit.Text(session.HotSeat || state.HalfMonth > 0 ? "Zug beenden" : "Monat beenden", 0, 16, 298, 40, 27, Theme.C("#fff6e6"), bold: true, serif: true, align: Align.Center));
            end.With(Kit.Text($"weiter in {(next.StartsWith("die ") ? "" : "den ")}{next}{(next.Length > 12 ? "" : " · Leertaste")}", 0, 58, 298, 24, 15, Theme.C("#f0cfd6"), align: Align.Center));
            end.Tap(() => session.Run(new GameAction.EndTurn()));
            scene.With(end);
        }
        return scene;
    }

    static bool IsHarvestable(GameState state, Plot plot) =>
        plot.Grape is not null && !plot.HarvestedThisYear && plot.Oechsle > 0 &&
        Rules.PlotAgeFactor(plot, state.Year) > 0 && Array.IndexOf(Rules.HarvestMonths, state.Month) >= 0;

    static string PlotLine(GameState state, Plot plot)
    {
        var ha = $"{Util.FormatNumber(plot.Hectares, 1)} ha";
        if (plot.Grape is not { } grapeId) return $"Brache · {ha}";
        var grape = Grapes.All[grapeId].Name.Replace(" (PIWI)", "");
        var age = Rules.PlotAgeFactor(plot, state.Year);
        if (age <= 0) return $"{grape} · Junganlage";
        if (plot.HarvestedThisYear) return $"{grape} · gelesen";
        if (plot.IceWineGamble) return $"{grape} · hängt für Eiswein";
        if (plot.Oechsle > 0) return $"{grape} · {Js.Round(plot.Oechsle)} °Oe";
        return $"{grape} · {ha}";
    }

    static SkiaControl Building(string name, double cx, double y, int badge, Action tap, double width = 130)
    {
        var layer = Kit.Layer(cx - width / 2, y, width + 20, 44);
        layer.With(Kit.Box(0, 4, width, 34, Theme.C("#c71c1418"), 17).With(Kit.Text(name, 0, 4, width, 26, 19, Theme.Cream, align: Align.Center)));
        if (badge > 0) layer.With(Kit.Badge(badge, width - 6, 8, 13));
        layer.Tap(tap);
        return layer;
    }

    static SkiaControl News(GameSession session)
    {
        var state = session.State;
        var player = session.Player;
        var month = state.Month == 1 ? 12 : state.Month - 1;
        var year = state.Month == 1 ? state.Year - 1 : state.Year;
        var lines = new List<(string Text, Color Dot)>();
        foreach (var e in state.Log)
        {
            if (e.Tone is null || e.Month != month || e.Year != year || (e.PlayerId is not null && e.PlayerId != player.Id)) continue;
            lines.Add((e.Text, e.Tone == LogTone.Good ? Theme.GoodLight : Theme.Warn));
        }
        foreach (var effect in player.Effects)
        {
            lines.Add(($"{effect.Label}: {EffectText(effect)} · noch {effect.MonthsLeft} Mon.", Theme.Gold));
        }
        var panel = Kit.Glass(1388, 24, 396, 248);
        panel.With(Kit.Text("Neuigkeiten", 24, 14, 300, 32, 22, Theme.Gold, bold: true, serif: true));
        const int perPage = 3;
        var pages = Math.Max(1, (int)Math.Ceiling(lines.Count / (double)perPage));
        var page = session.Nav.Page("gut.news", pages);
        if (lines.Count == 0)
        {
            panel.With(Kit.Text("Keine besonderen Vorkommnisse im letzten Monat.", 24, 60, 348, 56, 18, Theme.CreamSoft, maxLines: 2));
        }
        var y = 54.0;
        foreach (var line in lines.Skip(page * perPage).Take(perPage))
        {
            panel.With(Kit.Box(24, y + 9, 12, 12, line.Dot, 6));
            panel.With(Kit.Text(line.Text, 46, y, 330, 48, 16, Theme.Cream, maxLines: 2));
            y += 50;
        }
        if (pages > 1) panel.With(Kit.Pager(24, 206, 348, page, pages, p => session.Nav.SetPage("gut.news", p)));
        return panel;
    }

    public static string EffectText(Effect effect)
    {
        if (effect.Kind == EffectKind.Guard) return "Schutz vor Sabotage";
        if (effect.Kind == EffectKind.Quality) return $"{(effect.Value > 0 ? "+" : "")}{Util.JsNumber(effect.Value)} Güte";
        var percent = Js.Round((effect.Value - 1) * 100);
        var what = effect.Kind switch { EffectKind.Demand => "Nachfrage", EffectKind.Price => "Preise", _ => "Kosten" };
        return $"{(percent > 0 ? "+" : "−")}{Math.Abs(percent)} % {what}";
    }

    static SkiaControl Tasks(GameSession session)
    {
        var layer = Kit.Layer(24, 836, 1340, 140);
        if (session.Over) return layer;
        var hints = Advisor.MonthHints(session.State, session.Player);
        const int perPage = 4;
        var pages = Math.Max(1, (int)Math.Ceiling(hints.Count / (double)perPage));
        var page = session.Nav.Page("gut.tasks", pages);
        layer.With(Kit.Box(0, 0, 250, 32, Theme.C("#c71c1418"), 16)
            .With(Kit.Text($"Diesen Monat · {hints.Count} {(hints.Count == 1 ? "Aufgabe" : "Aufgaben")}", 14, 5, 230, 24, 16, Theme.Cream)));
        if (pages > 1)
        {
            layer.With(Kit.Box(260, 0, 250, 32, Theme.C("#c71c1418"), 16));
            layer.With(Kit.Pager(274, -2, 228, page, pages, p => session.Nav.SetPage("gut.tasks", p)));
        }
        var x = 0.0;
        foreach (var hint in hints.Skip(page * perPage).Take(perPage))
        {
            var target = Navigator.SceneFor(hint.Area);
            var card = Kit.Card(x, 40, 322, 96, 16);
            card.With(Kit.Box(0, 0, 10, 96, hint.Important ? Theme.Alert : Theme.C("#7d9a3a"), 5));
            card.With(Kit.Text(hint.Text, 26, 8, 284, 58, 16, Theme.Ink, bold: hint.Important, maxLines: 3));
            if (hint.Area == HintArea.Hof)
            {
                if (hint.Text.StartsWith("Wähle"))
                {
                    card.With(Kit.Text("Zur Gutsplanung ›", 26, 68, 284, 22, 15, Theme.Wine, bold: true));
                    card.Tap(() => session.Nav.Show(Overlay.Plan));
                }
                else if (session.Player.Decision is not null)
                {
                    card.With(Kit.Text("Entscheiden ›", 26, 68, 284, 22, 15, Theme.Wine, bold: true));
                    card.Tap(() => session.Nav.Show(Overlay.Decision));
                }
            }
            else
            {
                card.With(Kit.Text($"Zum {Navigator.LabelFor(target)} ›", 26, 68, 284, 22, 15, Theme.Wine, bold: true));
                card.Tap(() => session.Nav.Go(target));
            }
            layer.With(card);
            x += 338;
        }
        return layer;
    }

    // ---- Panorama ----

    static void PaintPanorama(SKCanvas c, float w, float h, GameState state, Player player, List<Plot> plots)
    {
        var season = SeasonFor(state.Month);
        var wet = state.LastWeather == WeatherKind.Nass;
        using (var sky = Draw.VerticalGradient(0, h * 0.8f, (0, season.SkyTop), (0.5f, season.SkyMid), (1, season.SkyLow)))
            c.DrawRect(0, 0, w, h, sky);

        // Sonne: Stand nach Jahreszeit, bei Nässe verdeckt
        if (!wet)
        {
            var sunY = state.Month is >= 5 and <= 8 ? 190f : 330f;
            using var glow = new SKPaint
            {
                IsAntialias = true,
                Shader = SKShader.CreateRadialGradient(new SKPoint(1268, sunY), 260,
                    [SKColor.Parse("#fff3c4"), SKColor.Parse("#e6ffd27a"), SKColor.Parse("#00ffd27a")], [0, 0.35f, 1], SKShaderTileMode.Clamp),
            };
            c.DrawCircle(1268, sunY, 260, glow);
            using var disc = Draw.Fill("#fff0bd");
            c.DrawCircle(1268, sunY, 58, disc);
        }
        using (var cloud = Draw.Fill(SKColors.White.WithAlpha((byte)(wet ? 150 : 70))))
        {
            c.DrawOval(308, 118, 210, 26, cloud);
            c.DrawOval(448, 144, 150, 18, cloud);
            c.DrawOval(1538, 158, 170, 20, cloud);
            if (wet)
            {
                c.DrawOval(900, 90, 320, 36, cloud);
                c.DrawOval(1250, 130, 260, 30, cloud);
            }
        }

        using (var far = Draw.VerticalGradient(330, h, (0, season.Far1), (1, season.Far2)))
            Draw.Path(c, $"M0 428 C148 328 408 378 648 348 C928 308 1188 378 1448 338 C1608 318 1728 348 {w} 338 L{w} {h} L0 {h} Z", far);

        // Dorf am fernen Hang
        using (var house = Draw.Fill(season.Snow ? "#c9d2da" : "#d9c7a3"))
        using (var roof = Draw.Fill("#7a4a3a"))
        {
            c.DrawRect(1604, 330, 26, 30, house);
            Draw.Path(c, "M1600 332 L1617 314 L1634 332 Z", roof);
            c.DrawRect(1640, 316, 18, 44, house);
            Draw.Path(c, "M1637 318 L1649 286 L1661 318 Z", roof);
            c.DrawRect(1668, 336, 30, 24, house);
            Draw.Path(c, "M1664 338 L1683 322 L1702 338 Z", roof);
        }

        using (var mid = Draw.VerticalGradient(430, h, (0, season.Mid1), (1, season.Mid2)))
            Draw.Path(c, $"M0 548 C188 448 528 508 788 468 C1088 418 1388 508 {w} 448 L{w} {h} L0 {h} Z", mid);

        // Parzellen
        for (var i = 0; i < plots.Count; i++) PaintPlot(c, Slots[i].Path, state, plots[i]);

        // Fluss
        using (var river = Draw.Fill(SKColor.Parse(season.Snow ? "#dfe8ef" : "#c9d7e6").WithAlpha(150)))
            Draw.Path(c, $"M0 608 C188 568 408 628 708 598 C988 568 1288 628 {w} 578 L{w} 638 C1288 688 988 628 708 658 C408 688 188 628 0 668 Z", river);

        using (var near = Draw.VerticalGradient(640, h, (0, season.Near1), (1, season.Near2)))
            Draw.Path(c, $"M0 668 C248 608 648 688 1008 638 C1348 598 1588 648 {w} 628 L{w} {h} L0 {h} Z", near);

        PaintBuildings(c, player, season.Snow);

        if (wet)
        {
            using var veil = Draw.Fill(SKColor.Parse("#33415c").WithAlpha(70));
            c.DrawRect(0, 0, w, h, veil);
            using var rain = Draw.Stroke(SKColor.Parse("#dfe8ef").WithAlpha(90), 2);
            for (var i = 0; i < 90; i++)
            {
                var x = (i * 137 + 40) % w;
                var y = (i * 71 + 20) % (h - 160);
                c.DrawLine(x, y, x - 10, y + 34, rain);
            }
        }
        if (season.Snow && state.LastWeather != WeatherKind.Sonnig)
        {
            using var flake = Draw.Fill(SKColors.White.WithAlpha(200));
            for (var i = 0; i < 120; i++) c.DrawCircle((i * 173 + 30) % w, (i * 97 + 10) % (h - 120), 2 + i % 3, flake);
        }
    }

    static void PaintPlot(SKCanvas c, string pathData, GameState state, Plot plot)
    {
        using var path = SKPath.ParseSvgPathData(pathData);
        var winter = state.Month is 11 or 12 or 1 or 2 or 3;
        string dark, light;
        var rows = true;
        if (plot.Grape is not { } grapeId)
        {
            (dark, light, rows) = ("#7a6a4a", "#93825c", false);
        }
        else
        {
            var grape = Grapes.All[grapeId];
            var age = Rules.PlotAgeFactor(plot, state.Year);
            if (plot.HarvestedThisYear || (winter && !plot.IceWineGamble && plot.Oechsle <= 0)) (dark, light) = ("#5a4a34", "#7c6a4c");
            else if (age <= 0) (dark, light) = ("#6f6a45", "#9aa860");
            else if (plot.Oechsle >= 60) (dark, light) = grape.Color == GrapeColor.Rot ? ("#6a3320", "#b5562e") : ("#6b5a26", "#c9a23a");
            else (dark, light) = ("#3f5a22", "#7d9a3a");
        }
        using (var ground = Draw.Fill(dark)) c.DrawPath(path, ground);
        c.Save();
        c.ClipPath(path, antialias: true);
        var bounds = path.Bounds;
        if (rows)
        {
            using var row = Draw.Stroke(light, 9);
            c.RotateDegrees(-16, bounds.MidX, bounds.MidY);
            for (var y = bounds.Top - 120; y < bounds.Bottom + 120; y += 22) c.DrawLine(bounds.Left - 150, y, bounds.Right + 150, y, row);
        }
        else
        {
            using var dot = Draw.Fill(light);
            for (var y = bounds.Top; y < bounds.Bottom; y += 18)
            {
                for (var x = bounds.Left + ((int)(y / 18) % 2) * 9; x < bounds.Right; x += 18) c.DrawCircle(x, y, 1.8f, dot);
            }
        }
        c.Restore();
        using var edge = Draw.Stroke(Theme.SkNight.WithAlpha(110), 3);
        c.DrawPath(path, edge);
        if (IsHarvestable(state, plot))
        {
            using var mark = Draw.Stroke(Theme.SkGold, 6);
            mark.PathEffect = SKPathEffect.CreateDash([18, 10], 0);
            c.DrawPath(path, mark);
        }
        if (plot.HailNet)
        {
            using var net = Draw.Stroke(SKColors.White.WithAlpha(120), 1.5f);
            c.Save();
            c.ClipPath(path);
            for (var x = bounds.Left - 100; x < bounds.Right + 100; x += 16) c.DrawLine(x, bounds.Top, x + 60, bounds.Bottom, net);
            c.Restore();
        }
    }

    static void PaintBuildings(SKCanvas c, Player player, bool snow)
    {
        using var shadow = Draw.Fill(SKColors.Black.WithAlpha(50));
        c.DrawOval(700, 818, 420, 14, shadow);

        // Gutshaus (Kontor)
        using (var wall = Draw.Fill("#efe1c6")) c.DrawRect(448, 660, 250, 150, wall);
        using (var roof = Draw.Fill(snow ? "#e8eef2" : "#8a3b2c")) Draw.Path(c, "M428 664 L573 576 L718 664 Z", roof);
        using (var door = Draw.Fill("#5c1429")) c.DrawRoundRect(548, 734, 50, 76, 24, 24, door);
        using (var window = Draw.Fill("#3a4c66"))
        {
            c.DrawRect(478, 698, 40, 46, window);
            c.DrawRect(628, 698, 40, 46, window);
        }

        // Kelterhaus mit Kellertor
        using (var wall = Draw.Fill("#d9c7a3")) c.DrawRect(738, 700, 210, 110, wall);
        using (var roof = Draw.Fill(snow ? "#e8eef2" : "#6f4a36")) Draw.Path(c, "M724 704 L843 638 L962 704 Z", roof);
        using (var gate = Draw.Fill("#3a2a22")) Draw.Path(c, "M793 810 L793 754 A50 50 0 0 1 893 754 L893 810 Z", gate);
        if (player.BarriqueCapacityHl > 0)
        {
            using var oak = Draw.Fill("#b07a45");
            using var hoop = Draw.Stroke("#2c2622", 3);
            c.DrawOval(970, 796, 20, 16, oak);
            c.DrawOval(970, 796, 20, 16, hoop);
        }

        // Vinothek oder einfacher Hofladen
        if (player.HasVinothek)
        {
            using (var wall = Draw.Fill("#f4ead7")) c.DrawRect(998, 720, 230, 90, wall);
            using (var band = Draw.Fill("#5c1429")) c.DrawRect(986, 704, 254, 22, band);
            using var glass = Draw.Fill("#9cc0d8");
            c.DrawRect(1016, 742, 56, 50, glass);
            c.DrawRect(1086, 742, 56, 50, glass);
            c.DrawRect(1156, 742, 56, 50, glass);
        }
        else
        {
            using (var wall = Draw.Fill("#c9b58f")) c.DrawRect(1018, 744, 150, 66, wall);
            using (var roof = Draw.Fill(snow ? "#e8eef2" : "#7a4a3a")) Draw.Path(c, "M1006 748 L1093 706 L1180 748 Z", roof);
            using var counter = Draw.Fill("#5c1429");
            c.DrawRect(1040, 772, 106, 38, counter);
        }
    }
}
