using DrawnUi.Draw;
using SkiaSharp;
using Winzer.Engine;
using Winzer.Engine.Data;
using Winzer.Presentation;

namespace Winzer.Ui.Scenes;

/// <summary>
/// Weinberg: Parzellen am Hang mit Inspektor (lesen, pflanzen, roden, verkaufen), Pflanzenschutz und Bio-Stufenleiter,
/// Wetterschutz. Die Reiter 2 und 3 liegen in <see cref="VineyardCare"/>, die Malerei in <see cref="VineyardPaint"/>.
/// </summary>
public static class VineyardScene
{
    internal const string PlotKey = "weinberg.plot";
    /// <summary>Rückfrage im Inspektor: "clear:Parzelle" oder "sell:Parzelle"</summary>
    const string ModeKey = "weinberg.mode";
    const string GrapeKey = "weinberg.grape";
    const int PerPage = 8;

    public static SkiaControl Build(GameSession session)
    {
        var state = session.State;
        var player = session.Player;
        var tab = Math.Clamp(session.Nav.Tab, 0, 2);
        var scene = Kit.Layer(0, 0, Shell.StageWidth, Shell.StageHeight);
        scene.With(Kit.Paint(0, 0, Shell.StageWidth, Shell.StageHeight, (c, w, h) => VineyardPaint.Hill(c, w, h, state, tab > 0)));

        int[] badges =
        [
            player.Plots.Count(p => Harvestable(session, p)),
            session.Problem(new GameAction.ApplyBioLabel()) is null ? 1 : 0,
            session.Problem(new GameAction.FrostCandles()) is null ? 1 : 0,
        ];
        scene.With(Kit.Tabs(28, 20, ["Parzellen", "Pflanzenschutz & Bio", "Wetterschutz"], tab, session.Nav.SetTab, badges));

        var season = state.Month switch
        {
            3 or 4 => "Pflanzzeit",
            >= 8 => "Lesezeit",
            >= 5 => "Wachstum",
            _ => "Winterruhe",
        };
        var planted = Util.FormatNumber(Market.PlantedHectares(player), 1);
        var total = Util.FormatNumber(Market.TotalHectares(player), 1);
        var count = player.Plots.Count;
        scene.With(Pill($"{Game.MonthNames[state.Month - 1]} · {season} · {planted} von {total} ha bestockt · {count} {(count == 1 ? "Parzelle" : "Parzellen")}",
            696, 24, 534, 40, Theme.C("#c71c1418"), Theme.Cream, 18));

        switch (tab)
        {
            case 1: VineyardCare.Protection(session, scene); break;
            case 2: VineyardCare.Weather(session, scene); break;
            default: Plots(session, scene); break;
        }
        return scene;
    }

    // ---- Gemeinsame Helfer ----

    internal static bool Harvestable(GameSession session, Plot plot) =>
        plot.Oechsle > 0 && session.Problem(new GameAction.Harvest(plot.Id)) is null;

    internal static SkiaShape Pill(string text, double x, double y, double w, double h, Color fill, Color color, double size = 16, bool bold = false) =>
        Kit.Box(x, y, w, h, fill, h / 2).With(Kit.Text(text, 14, (h - size * 1.4) / 2, w - 28, size * 1.5, size, color, bold: bold, align: Align.Center));

    internal static string GrapeName(GrapeId id) => Grapes.All[id].Name.Replace(" (PIWI)", "");

    internal static string Hectares(double hectares) => $"{Util.FormatNumber(hectares, 1)} ha";

    internal static string PlotSub(Plot plot) =>
        plot.Grape is { } grape ? $"{GrapeName(grape)} · {Hectares(plot.Hectares)}" : $"Brache · {Hectares(plot.Hectares)}";

    static (string Text, Color Color) TrendText(GameState state, GrapeId id)
    {
        if (!state.Trends.TryGetValue((TrendKey)(int)id, out var trend)) return ("100 % →", Theme.Ink);
        var arrow = Trends.TrendDirection(trend) switch { TrendDirectionKind.Up => "↗", TrendDirectionKind.Down => "↘", _ => "→" };
        var color = trend.Value >= 1.05 ? Theme.Good : trend.Value <= 0.95 ? Theme.Alert : Theme.Ink;
        return ($"{Js.Round(trend.Value * 100)} % {arrow}", color);
    }

    static (string Word, Color Color) SuitabilityText(double value) => value switch
    {
        >= 2 => ("ideal", Theme.Good),
        >= 1 => ("gut geeignet", Theme.Good),
        >= 0 => ("geeignet", Theme.InkSoft),
        >= -1 => ("schwierig", Theme.Warn),
        _ => ("ungeeignet", Theme.Alert),
    };

    static string SiteWord(double site) => site switch
    {
        >= 2 => "Spitzenlage",
        >= 1 => "sehr gute Lage",
        >= 0 => "gute Lage",
        _ => "einfache Lage",
    };

    // ---- Reiter 1: Parzellen ----

    static void Plots(GameSession session, SkiaLayout scene)
    {
        var player = session.Player;
        var plots = player.Plots;
        if (plots.Count == 0) return;
        var selected = plots.Find(p => p.Id == session.Nav.Selected(PlotKey)) ?? plots[0];
        var pages = Math.Max(1, (int)Math.Ceiling(plots.Count / (double)PerPage));
        // Ohne eigenes Blättern liegt die Seite der gewählten Parzelle auf
        var page = Math.Clamp(plots.IndexOf(selected) / PerPage, 0, pages - 1);
        var shown = plots.Skip(page * PerPage).Take(PerPage).ToList();

        const double tileWidth = 287, gap = 18;
        var twoRows = shown.Count > 4;
        var pictureHeight = twoRows ? 140.0 : 250.0;
        var tileHeight = pictureHeight + 186;
        for (var i = 0; i < shown.Count; i++)
        {
            var inRow = twoRows ? 4 : shown.Count;
            var left = twoRows ? 28 : 28 + (1202 - (inRow * tileWidth + (inRow - 1) * gap)) / 2;
            var x = left + (i % 4) * (tileWidth + gap);
            var y = twoRows ? 92 + (i / 4) * (tileHeight + 16) : 196;
            scene.With(Tile(session, shown[i], x, y, tileWidth, pictureHeight, shown[i] == selected));
        }
        if (pages > 1)
        {
            scene.With(Kit.Box(940, 764, 290, 44, Theme.C("#c71c1418"), 22));
            scene.With(Kit.Pager(958, 768, 262, page, pages, p =>
            {
                var target = Math.Clamp(p, 0, pages - 1);
                session.Nav.Select(PlotKey, plots[target * PerPage].Id);
            }));
        }
        scene.With(RegionPanel(player));
        scene.With(Inspector(session, selected));
    }

    static SkiaControl Tile(GameSession session, Plot plot, double x, double y, double w, double pictureHeight, bool on)
    {
        var state = session.State;
        var h = pictureHeight + 186;
        var tile = Kit.Box(x, y, w, h, Theme.C("#eb1c1418"), 18, on ? Theme.Gold : Theme.GlassLine, on ? 4 : 1.5, shadow: true);
        tile.With(Kit.Paint(8, 8, w - 16, pictureHeight, (c, pw, ph) => VineyardPaint.Plot(c, pw, ph, state, plot)));
        if (Harvestable(session, plot))
            tile.With(Kit.Box(18, 18, 112, 32, radius: 16, gradient: Kit.Vertical(Theme.Gold, Theme.GoldDark)).With(Kit.Text("lesereif", 0, 4, 112, 24, 17, Theme.Ink, bold: true, align: Align.Center)));
        else if (plot.Grape is null && session.Problem(new GameAction.Plant(plot.Id, GrapeId.Riesling)) is null)
            tile.With(Kit.Box(18, 18, 132, 32, radius: 16, gradient: Kit.Vertical(Theme.Gold, Theme.GoldDark)).With(Kit.Text("pflanzbereit", 0, 4, 132, 24, 17, Theme.Ink, bold: true, align: Align.Center)));

        if (plot.ForAuction)
            tile.With(Kit.Box(w - 18 - 148, 18, 148, 32, radius: 16, gradient: Kit.Vertical(Theme.Wine, Theme.WineDark)).With(Kit.Text("Versteigerung", 0, 4, 148, 24, 17, Theme.Cream, bold: true, align: Align.Center)));

        var ty = pictureHeight + 16;
        tile.With(Kit.Text(plot.Name, 16, ty, w - 32 - 96, 30, 21, on ? Theme.Gold : Theme.Cream, bold: true));
        tile.With(Kit.Paint(w - 16 - 92, ty + 5, 92, 20, (c, pw, ph) => VineyardPaint.Stars(c, pw, ph, plot.Site)));
        tile.With(Kit.Text(PlotSub(plot), 16, ty + 31, w - 32, 26, 18, Theme.CreamSoft));

        var young = plot.Grape is not null && Rules.PlotAgeFactor(plot, state.Year) == 0;
        if (plot.Grape is { } grapeId && !young && !plot.HarvestedThisYear)
        {
            var color = Grapes.All[grapeId].Color;
            tile.With(Kit.Paint(16, ty + 66, w - 32, 14, (c, pw, ph) => VineyardPaint.Gauge(c, pw, ph, plot.Oechsle, color, true)));
            var ripeness = plot.Oechsle > 0 ? $"{Js.Round(plot.Oechsle)} °Oe · {Quality.LevelForOechsle(plot.Oechsle).Name}" : "Reife noch nicht begonnen";
            tile.With(Kit.Text(ripeness, 16, ty + 86, w - 32, 26, 18, Theme.Cream, bold: plot.Oechsle > 0));
            var weak = plot.Health < 0.7 || plot.CropFactor < 0.99;
            var health = $"Gesundheit {Js.Round(plot.Health * 100)} %" + (plot.CropFactor < 0.99 ? $" · Ertrag {Js.Round(plot.CropFactor * 100)} %" : "");
            tile.With(Kit.Text(health, 16, ty + 114, w - 32, 24, 16, weak ? Theme.Warn : Theme.CreamSoft));
        }
        else
        {
            var status = plot.Grape is null ? "Brache – gepflanzt wird im März und April."
                : young ? "Junganlage – trägt ab dem dritten Jahr."
                : "In diesem Jahr gelesen.";
            tile.With(Kit.Text(status, 16, ty + 64, w - 32, 50, 17, Theme.Cream, maxLines: 2));
        }
        var extras = new List<string>();
        if (plot.Botrytis) extras.Add("Edelfäule");
        if (plot.IceWineGamble) extras.Add($"Eiswein-Wette {Js.Round(Vineyard.IceWineChance(state, session.Player.RegionId) * 100)} %");
        if (plot.HailNet) extras.Add("Hagelnetz");
        if (plot.Irrigation) extras.Add("Bewässerung");
        if (extras.Count > 0) tile.With(Kit.Text(string.Join(" · ", extras), 16, ty + 140, w - 32, 24, 16, Theme.Gold));

        var id = plot.Id;
        tile.Tap(() => session.Nav.Select(PlotKey, id));
        return tile;
    }

    /// <summary>Charakter des Anbaugebiets in Kurzform (RegionProfile des Originals)</summary>
    static SkiaControl RegionPanel(Player player)
    {
        var region = Regions.All[player.RegionId];
        var character = region.Character;
        string[] prestige = ["Geheimtipp", "bekannt", "renommiert", "weltberühmt"];
        string[] tourism = ["kaum Ausflügler", "einige Ausflügler", "viele Ausflügler"];
        string Percent(double factor) => $"{Js.Round(factor * 100)} %";
        var panel = Kit.Glass(28, 818, 1202, 150);
        panel.With(Kit.Text(region.Name, 24, 12, 300, 36, 26, Theme.Gold, bold: true, serif: true));
        var nameWidth = 40 + region.Name.Length * 15.0;
        panel.With(Kit.Text(character.Profile, 24 + nameWidth, 19, 1154 - nameWidth, 28, 19, Theme.Cream));
        (string Title, string Note, int Tone)[] chips =
        [
            ($"Steillagen {Percent(character.Slope)}", "mehr Arbeit, mehr Güte", 0),
            (prestige[(int)Math.Clamp(character.Prestige + 1, 0, 3)], "Renommee hebt die Preise", Math.Sign(character.Prestige)),
            (tourism[(int)Math.Clamp(character.Tourism, 0, 2)], "Kundschaft ab Hof", character.Tourism > 0 ? 1 : 0),
            ($"Ertrag {Percent(character.YieldFactor)}", "Ertragsniveau im Gebiet", character.YieldFactor > 1 ? 1 : character.YieldFactor < 1 ? -1 : 0),
            ($"Boden {Percent(character.LandPrice)}", "Wert, Kredit, Auktionen", 0),
        ];
        for (var i = 0; i < chips.Length; i++)
        {
            var chip = chips[i];
            var color = chip.Tone > 0 ? Theme.GoodLight : chip.Tone < 0 ? Theme.Warn : Theme.Cream;
            panel.With(Kit.Box(24 + i * 232.5, 62, 224, 72, Theme.GlassLight, 14).With(
                Kit.Text(chip.Title, 14, 9, 196, 28, 19, color, bold: true),
                Kit.Text(chip.Note, 14, 39, 200, 24, 15, Theme.CreamSoft)));
        }
        return panel;
    }

    // ---- Inspektor ----

    static SkiaControl Inspector(GameSession session, Plot plot)
    {
        var state = session.State;
        var player = session.Player;
        var card = Kit.Card(1260, 20, 524, 948, 22);
        const double x = 32, w = 460;
        card.With(Kit.Text(plot.Name, x, 26, w - 124, 46, 32, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Paint(x + w - 116, 36, 116, 25, (c, pw, ph) => VineyardPaint.Stars(c, pw, ph, plot.Site)));

        var young = plot.Grape is not null && Rules.PlotAgeFactor(plot, state.Year) == 0;
        var subtitle = plot.Grape is null ? "Brache – bereit für eine neue Rebsorte"
            : young ? "Junganlage – trägt ab dem dritten Jahr"
            : plot.IceWineGamble ? "Die Trauben hängen für Eiswein"
            : plot.HarvestedThisYear ? "In diesem Jahr gelesen"
            : plot.Botrytis ? "Edelfäule – das Mostgewicht steigt"
            : plot.Oechsle > 0 ? "Die Trauben reifen" : "Die Reife hat noch nicht begonnen";
        card.With(Kit.Text(subtitle, x, 74, w, 28, 19, Theme.InkSoft));

        var equipment = new List<string>();
        if (plot.HailNet) equipment.Add("Hagelnetz");
        if (plot.Irrigation) equipment.Add("Bewässerung");
        var equipmentText = equipment.Count > 0 ? string.Join(", ", equipment) : "keine";

        var y = 112.0;
        void Row(string label, string value, Color? color = null)
        {
            card.With(Kit.Row(label, value, x, y, w, color, 18));
            y += 30;
        }

        if (plot.Grape is not { } grapeId)
        {
            Row("Fläche", Hectares(plot.Hectares));
            Row("Lage", SiteWord(plot.Site));
            Row("Ausstattung", equipmentText);
            card.With(Kit.Box(x, y + 6, w, 2, Theme.PaperLine));
            PlantSection(session, card, plot, y + 18);
            return card;
        }

        var grape = Grapes.All[grapeId];
        var age = state.Year - (plot.PlantedYear ?? state.Year);
        var ageFactor = Rules.PlotAgeFactor(plot, state.Year);
        var ageText = (age <= 0 ? "frisch gepflanzt" : age == 1 ? "1 Jahr" : $"{age} Jahre") +
            (ageFactor <= 0 ? " · Junganlage" : ageFactor < 1 ? " · halber Ertrag" : " · voll im Ertrag");
        var trend = TrendText(state, grapeId);
        Row("Sorte", $"{grape.Name} · {(grape.Color == GrapeColor.Rot ? "rot" : "weiß")}");
        Row("Alter", ageText);
        Row("Fläche", Hectares(plot.Hectares));
        Row("Lage", SiteWord(plot.Site));
        Row("Gesundheit", $"{Js.Round(plot.Health * 100)} %" + (plot.CropFactor < 0.99 ? $" · Ertrag {Js.Round(plot.CropFactor * 100)} %" : ""),
            plot.Health < 0.7 || plot.CropFactor < 0.99 ? Theme.Alert : null);
        Row("Markttrend", trend.Text, trend.Color);
        Row("Ausstattung", equipmentText);
        card.With(Kit.Box(x, y + 6, w, 2, Theme.PaperLine));
        y += 18;

        // Reife: Skala mit den Schwellen der Qualitätsstufen
        var ripening = !young && !plot.HarvestedThisYear;
        card.With(Kit.Text("Reife", x, y, 200, 30, 21, Theme.Ink, bold: true));
        if (ripening)
        {
            card.With(Kit.Text(plot.Oechsle > 0 ? $"{Js.Round(plot.Oechsle)} °Oe" : "0 °Oe", x + 200, y - 4, w - 200, 36, 26, Theme.Wine, bold: true, serif: true, align: Align.End));
            card.With(Kit.Paint(x, y + 40, w, 16, (c, pw, ph) => VineyardPaint.Gauge(c, pw, ph, plot.Oechsle, grape.Color, false)));
            (QualityLevel Level, string Short)[] ticks =
            [
                (QualityLevel.Qualitaetswein, "QbA"), (QualityLevel.Spaetlese, "Spl"), (QualityLevel.Auslese, "Asl"),
                (QualityLevel.Beerenauslese, "BA"), (QualityLevel.Trockenbeerenauslese, "TBA"),
            ];
            foreach (var tick in ticks)
            {
                var at = x + w * VineyardPaint.ScalePosition(Quality.QualityInfo(tick.Level).MinOechsle);
                card.With(Kit.Text(tick.Short, Math.Min(at - 30, x + w - 60), y + 59, 60, 22, 15, Theme.InkSoft, align: Align.Center));
            }
            string reach;
            if (plot.Oechsle <= 0) reach = "Das Mostgewicht steigt ab dem Sommer, Lese ab August.";
            else
            {
                var level = Quality.LevelForOechsle(plot.Oechsle);
                var next = Quality.QualityLevels.FirstOrDefault(q => !q.Special && q.MinOechsle > plot.Oechsle);
                reach = $"Reicht für {level.Name}" + (next is null ? "" : $" · noch {Math.Max(1, Math.Ceiling(next.MinOechsle - plot.Oechsle))} °Oe bis {next.Name}");
            }
            card.With(Kit.Text(reach, x, y + 84, w, 26, 17, Theme.InkSoft));
            y += 116;
        }
        else
        {
            card.With(Kit.Text(young ? "Junge Reben tragen noch nicht. Im dritten Jahr gibt es die halbe, danach die volle Ernte."
                : "Diese Parzelle ist für dieses Jahr gelesen. Im nächsten Sommer reifen neue Trauben.", x, y + 34, w, 52, 17, Theme.InkSoft, maxLines: 2));
            y += 92;
        }

        // Ertragsvorschau
        var harvest = new GameAction.Harvest(plot.Id);
        var harvestProblem = session.Problem(harvest);
        HarvestPreview? preview = ripening ? Game.HarvestPreview(state, player, plot) : null;
        var previewBox = Kit.Box(x, y, w, 62, Theme.C("#40d9c7a3"), 12);
        if (preview is not null && harvestProblem is null)
        {
            previewBox.With(Kit.Text($"Ertragsvorschau: ~{Util.FormatNumber(preview.Hectoliters)} hl {Quality.QualityInfo(preview.Level).Name}", 14, 6, w - 28, 26, 18, Theme.Ink, bold: true));
            var early = state.Month is 8 or 9 ? " · früh: weniger Menge" : "";
            previewBox.With(Kit.Text($"Güte {Util.FormatNumber(preview.Quality)} · Lesekosten {Util.FormatEuro(preview.Cost)}{early}", 14, 32, w - 28, 24, 16, Theme.InkSoft));
        }
        else if (preview is not null && plot.Oechsle > 0)
        {
            previewBox.With(Kit.Text($"Ertragsvorschau: Güte {Util.FormatNumber(preview.Quality)} nach heutigem Stand", 14, 6, w - 28, 26, 18, Theme.Ink, bold: true));
            previewBox.With(Kit.Text($"Lesekosten {Util.FormatEuro(preview.Cost)} · gelesen wird von August bis Dezember", 14, 32, w - 28, 24, 16, Theme.InkSoft));
        }
        else
        {
            previewBox.With(Kit.Text("Ertragsvorschau", 14, 6, w - 28, 26, 18, Theme.Ink, bold: true));
            previewBox.With(Kit.Text(ripening ? "erscheint, sobald die Trauben reifen" : young ? "erst wenn die Reben tragen" : "wieder im nächsten Jahr", 14, 32, w - 28, 24, 16, Theme.InkSoft));
        }
        card.With(previewBox);
        y += 72;

        // Rückfragen ersetzen die Aktionen
        var mode = session.Nav.Selected(ModeKey);
        if (mode == $"clear:{plot.Id}")
        {
            var cost = plot.Hectares * Economy.ClearingCostPerHa;
            Confirm(session, card, y,
                $"{grape.Name} in der Lage {plot.Name} roden? Das kostet {Util.FormatEuro(cost)}; eine neue Rebsorte pflanzt du im März oder April – sie trägt erst ab dem dritten Jahr.",
                "Ja, roden", new GameAction.ClearPlot(plot.Id));
            return card;
        }
        if (mode == $"sell:{plot.Id}")
        {
            SellConfirm(session, card, plot, y);
            return card;
        }

        var red = grape.Color == GrapeColor.Rot;
        var step = red ? 71.0 : 88.0;
        var height = red ? 63.0 : 78.0;
        var harvestSub = preview is not null && harvestProblem is null
            ? $"~{Util.FormatNumber(preview.Hectoliters)} hl {Quality.QualityInfo(preview.Level).Name}, Güte {Util.FormatNumber(preview.Quality)} · kostet {Util.FormatEuro(preview.Cost)}"
            : null;
        card.With(Kit.Button("Lesen", x, y, w, height, () => session.Run(harvest), ButtonKind.Primary, harvestSub, harvestProblem, 20));
        y += step;
        if (red)
        {
            var rose = new GameAction.Harvest(plot.Id, true);
            var roseTrend = state.Trends.TryGetValue(TrendKey.Rose, out var t) ? $" · Markt {Js.Round(t.Value * 100)} %" : "";
            card.With(Kit.Button("Als Rosé lesen", x, y, w, height, () => session.Run(rose), ButtonKind.Secondary,
                $"hell gekeltert · in 3 Monaten trinkfertig{roseTrend}", session.Problem(rose), 20));
            y += step;
        }
        var chance = Js.Round(Vineyard.IceWineChance(state, player.RegionId) * 100);
        var gamble = new GameAction.GambleIceWine(plot.Id, !plot.IceWineGamble);
        card.With(Kit.Button(plot.IceWineGamble ? "Eiswein-Wette abbrechen" : "Für Eiswein hängen lassen", x, y, w, height, () => session.Run(gamble), ButtonKind.Secondary,
            plot.IceWineGamble ? $"Frostchance {chance} % · die Trauben werden wieder normal gelesen" : $"Frost im Dezember: {chance} % · sonst ist die Ernte verloren",
            session.Problem(gamble), 20));
        y += step;
        var clear = new GameAction.ClearPlot(plot.Id);
        var id = plot.Id;
        card.With(Kit.Button("Roden", x, y, w, height, () => session.Nav.Select(ModeKey, $"clear:{id}"), ButtonKind.Secondary,
            $"kostet {Util.FormatEuro(plot.Hectares * Economy.ClearingCostPerHa)} · macht Platz für eine andere Sorte", session.Problem(clear), 20));
        y += step;
        SellButton(session, card, plot, y, height);
        return card;
    }

    /// <summary>
    /// Trennen von einer Parzelle: sofort zum festen Preis verkaufen oder – mit den erweiterten Regeln – zur nächsten
    /// Versteigerung geben, wo sie mehr bringen kann, aber auch liegen bleiben.
    /// </summary>
    static void SellButton(GameSession session, SkiaLayout card, Plot plot, double y, double height)
    {
        var id = plot.Id;
        var state = session.State;
        var player = session.Player;
        var price = Game.PlotSalePrice(player, plot, state.Year);
        if (!RulesV2.On(state))
        {
            card.With(Kit.Button("Verkaufen", 32, y, 460, height, () => session.Nav.Select(ModeKey, $"sell:{id}"), ButtonKind.Secondary,
                $"bringt {Util.FormatEuro(price)} · 90 % des Schätzwerts", session.Problem(new GameAction.SellPlot(plot.Id)), 20));
            return;
        }
        var start = Util.FormatEuro(Auction.OfferStartPrice(player, plot, state.Year));
        if (plot.ForAuction)
        {
            var withdraw = new GameAction.WithdrawPlot(id);
            card.With(Kit.Button("Versteigerung zurückziehen", 32, y, 460, height, () => session.Run(withdraw), ButtonKind.Secondary,
                $"angemeldet für die nächste Versteigerung · Ausruf {start}", session.Problem(withdraw), 20));
            return;
        }
        var sell = session.Problem(new GameAction.SellPlot(id));
        var offer = new GameAction.OfferPlot(id);
        var blocked = session.Problem(offer);
        card.With(Kit.Button("Verkaufen", 32, y, 224, height, () => session.Nav.Select(ModeKey, $"sell:{id}"), ButtonKind.Secondary,
            $"{Util.FormatEuro(price)} sofort", sell, 20, quiet: sell is not null));
        card.With(Kit.Button("Versteigern", 268, y, 224, height, () => session.Run(offer), ButtonKind.Secondary,
            $"Ausruf {start}", blocked, 20, quiet: blocked is not null));
    }

    static void SellConfirm(GameSession session, SkiaLayout card, Plot plot, double y)
    {
        var price = Game.PlotSalePrice(session.Player, plot, session.State.Year);
        Confirm(session, card, y,
            $"Die Lage {plot.Name} für {Util.FormatEuro(price)} verkaufen? Der Weinberg ist danach weg, Trauben am Stock inklusive.",
            "Ja, verkaufen", new GameAction.SellPlot(plot.Id));
    }

    static void Confirm(GameSession session, SkiaLayout card, double y, string question, string yes, GameAction action)
    {
        const double x = 32, w = 460;
        var box = Kit.Box(x, y, w, 250, Theme.C("#1f8e2440"), 16, Theme.Wine, 2.5);
        box.With(Kit.Text("Bist du sicher?", 20, 14, w - 40, 32, 23, Theme.Wine, bold: true, serif: true));
        box.With(Kit.Text(question, 20, 50, w - 40, 104, 18, Theme.Ink, maxLines: 4));
        box.With(Kit.Button(yes, 20, 170, 206, 62, () =>
        {
            session.Nav.Select(ModeKey, null);
            session.Run(action);
        }, ButtonKind.Primary, problem: session.Problem(action), fontSize: 20, align: Align.Center));
        box.With(Kit.Button("Abbrechen", 234, 170, 206, 62, () => session.Nav.Select(ModeKey, null), ButtonKind.Secondary, fontSize: 20, align: Align.Center));
        card.With(box);
    }

    /// <summary>Brache: Rebsorte aus einem blätterbaren Raster wählen und pflanzen</summary>
    static void PlantSection(GameSession session, SkiaLayout card, Plot plot, double y)
    {
        var state = session.State;
        var region = Regions.All[session.Player.RegionId];
        const double x = 32, w = 460;
        if (session.Nav.Selected(ModeKey) == $"sell:{plot.Id}")
        {
            SellConfirm(session, card, plot, y);
            return;
        }
        // Die im Gebiet am besten geeigneten Sorten stehen vorn
        var grapes = Grapes.All.Values.OrderByDescending(g => region.Suitability[g.Id]).ToList();
        var chosen = Enum.TryParse<GrapeId>(session.Nav.Selected(GrapeKey), out var stored) ? stored : grapes[0].Id;
        const int perPage = 6;
        var pages = (int)Math.Ceiling(grapes.Count / (double)perPage);
        var page = session.Nav.Page("weinberg.grapes", pages);
        card.With(Kit.Text("Rebsorte wählen", x, y + 4, 240, 30, 21, Theme.Ink, bold: true));
        card.With(Kit.Pager(x + 250, y, 210, page, pages, p => session.Nav.SetPage("weinberg.grapes", Math.Clamp(p, 0, pages - 1)), dark: false));
        y += 46;
        var shown = grapes.Skip(page * perPage).Take(perPage).ToList();
        for (var i = 0; i < shown.Count; i++)
        {
            var grape = shown[i];
            var on = grape.Id == chosen;
            var suit = SuitabilityText(region.Suitability[grape.Id]);
            var trend = TrendText(state, grape.Id);
            var cell = on
                ? Kit.Box(x + (i % 2) * 236, y + (i / 2) * 110, 224, 102, Theme.Paper, 14, Theme.Wine, 3.5)
                : Kit.Box(x + (i % 2) * 236, y + (i / 2) * 110, 224, 102, Theme.C("#4dd9c7a3"), 14);
            cell.With(Kit.Text(grape.Name, 14, 8, 200, 28, 19, on ? Theme.Wine : Theme.Ink, bold: true));
            cell.With(Kit.Box(14, 45, 12, 12, suit.Color, 6));
            cell.With(Kit.Text(suit.Word, 34, 37, 180, 26, 17, suit.Color, bold: true));
            cell.With(Kit.Text(Util.FormatEuro(grape.PlantCostPerHa * plot.Hectares), 14, 66, 110, 26, 17, Theme.Ink));
            cell.With(Kit.Text(trend.Text, 110, 66, 100, 26, 17, trend.Color, align: Align.End));
            var id = grape.Id;
            cell.Tap(() => session.Nav.Select(GrapeKey, id.ToString()));
            card.With(cell);
        }
        y += 3 * 110 + 4;

        var pick = Grapes.All[chosen];
        var pickSuit = SuitabilityText(region.Suitability[chosen]);
        var pickTrend = TrendText(state, chosen);
        card.With(Kit.Text($"{pick.Name}: {(pick.Color == GrapeColor.Rot ? "rot" : "weiß")} · {Util.FormatNumber(pick.YieldPerHa)} hl/ha · Güte-Potenzial {Util.FormatNumber(pick.QualityPotential)}/10",
            x, y, w, 26, 17, Theme.Ink, bold: true));
        card.With(Kit.Text($"In {region.Name} {pickSuit.Word} · Markt {pickTrend.Text} · neue Reben tragen erst ab dem dritten Jahr.", x, y + 26, w, 48, 16, Theme.InkSoft, maxLines: 2));
        y += 82;
        var plant = new GameAction.Plant(plot.Id, chosen);
        card.With(Kit.Button($"{GrapeName(chosen)} pflanzen", x, y, w, 78, () => session.Run(plant), ButtonKind.Primary,
            $"kostet {Util.FormatEuro(pick.PlantCostPerHa * plot.Hectares)} für {Hectares(plot.Hectares)}", session.Problem(plant)));
        SellButton(session, card, plot, y + 88, 66);
    }
}
