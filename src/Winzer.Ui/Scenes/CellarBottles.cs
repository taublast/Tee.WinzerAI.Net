using DrawnUi.Draw;
using SkiaSharp;
using Winzer.Engine;
using Winzer.Engine.Data;
using Winzer.Presentation;

namespace Winzer.Ui.Scenes;

/// <summary>Reiter „Flaschenlager“: Regal mit zwölf Posten je Seite, rechts Werte und Prüfung des gewählten Postens.</summary>
internal static class CellarBottles
{
    const string Area = "keller.lot";
    const int Columns = 6;
    const int PerPage = 12;

    /// <summary>Oberkanten der beiden Regalfächer (auch für die Malerei des Regals)</summary>
    internal static readonly int[] RowTops = [92, 512];

    static readonly Dictionary<QualityLevel, string> ShortLevel = new()
    {
        [QualityLevel.DeutscherWein] = "DW",
        [QualityLevel.Qualitaetswein] = "QbA",
        [QualityLevel.Kabinett] = "KAB",
        [QualityLevel.Spaetlese] = "SPL",
        [QualityLevel.Auslese] = "ASL",
        [QualityLevel.Beerenauslese] = "BA",
        [QualityLevel.Trockenbeerenauslese] = "TBA",
        [QualityLevel.Eiswein] = "EIS",
    };

    public static void Build(SkiaLayout scene, GameSession session)
    {
        var state = session.State;
        var player = session.Player;
        var nav = session.Nav;
        var lots = player.Bottles;
        var competition = Market.UpcomingCompetition(state.Month);
        var total = lots.Sum(l => l.Bottles);

        scene.With(Kit.Text($"Flaschenlager: {Util.FormatNumber(total)} Fl. in {lots.Count} Posten", 830, 18, 400, 26, 18, Theme.Cream, bold: true, align: Align.End));
        scene.With(Kit.Text($"{competition.Name}: {Game.MonthNames[competition.Month - 1]} ({player.CompetitionEntries.Count}/{competition.MaxEntries} angemeldet)",
            830, 46, 400, 24, 16, Theme.CreamSoft, align: Align.End));

        var pages = Math.Max(1, (int)Math.Ceiling(lots.Count / (double)PerPage));
        var page = nav.Page("keller.lots", pages);
        var chosen = nav.Selected(Area);
        var lot = lots.Find(l => l.Id == chosen) ?? lots.Skip(page * PerPage).FirstOrDefault() ?? lots.FirstOrDefault();
        var shown = lots.Skip(page * PerPage).Take(PerPage).ToList();
        for (var i = 0; i < shown.Count; i++)
        {
            var item = shown[i];
            scene.With(Cell(session, item, 34 + i % Columns * 199, RowTops[i / Columns], item == lot));
        }

        if (lots.Count == 0)
        {
            var note = Kit.Glass(316, 400, 626, 150);
            note.With(Kit.Text("Noch nichts abgefüllt", 0, 22, 626, 40, 28, Theme.Gold, bold: true, serif: true, align: Align.Center));
            note.With(Kit.Text("Ausgebauten Wein aus dem Tank abfüllen.", 0, 66, 626, 28, 19, Theme.Cream, align: Align.Center));
            note.With(Kit.Button("Zu den Tanks ›", 213, 100, 200, 40, () => nav.SetTab(0), ButtonKind.Dark, fontSize: 17, align: Align.Center));
            scene.With(note);
        }
        else
        {
            var saturation = Market.WholesaleSaturation(player);
            scene.With(Kit.Text(saturation < 1
                ? $"Großhandel gesättigt: In diesem Jahr gibt es nur noch {Js.Round(saturation * 100)} % des üblichen Preises."
                : CellarParts.DemandText(player), 40, 930, 860, 26, 17, saturation < 1 ? Theme.Warn : Theme.CreamSoft));
            if (pages > 1) scene.With(Kit.Pager(930, 924, 300, page, pages, p => nav.SetPage("keller.lots", p)));
        }

        scene.With(lot is not null ? LotInspector(session, lot, competition) : EmptyInspector(session, competition));
    }

    static (string Text, Color Color) Status(BottleLot lot)
    {
        if (lot.MonthsUntilReady > 0) return ($"reift noch {CellarParts.Months(lot.MonthsUntilReady)} auf der Hefe", Theme.InkSoft);
        if (lot.Registered) return ("amtlich geprüft", Theme.Good);
        if (!lot.Sparkling && lot.Level == QualityLevel.DeutscherWein) return ("Deutscher Wein – ohne Prüfung", Theme.InkSoft);
        if (lot.Bottles >= Economy.RegisterMinBottles) return ("wartet auf die Prüfung", Theme.Alert);
        return ("Restposten – ohne Prüfung", Theme.InkSoft);
    }

    static (string Text, Color Color) ShortStatus(BottleLot lot)
    {
        if (lot.MonthsUntilReady > 0) return ($"reift {lot.MonthsUntilReady} Mon.", Theme.InkSoft);
        if (lot.Registered) return ("geprüft", Theme.Good);
        if (!lot.Sparkling && lot.Level == QualityLevel.DeutscherWein) return ("", Theme.InkSoft);
        if (lot.Bottles >= Economy.RegisterMinBottles) return ("ungeprüft", Theme.Alert);
        return ("Rest", Theme.InkSoft);
    }

    /// <summary>Ein Platz im Regal: Flasche mit Medaillen, darunter das Schild am Regalbrett</summary>
    static SkiaControl Cell(GameSession session, BottleLot lot, double x, double top, bool on)
    {
        var cell = Kit.Layer(x, top, 190, 410);
        cell.With(Kit.Paint(0, 0, 190, 290, (c, w, h) => PaintBottle(c, lot, on)));
        cell.With(Kit.Text(lot.Sparkling ? "SEKT" : ShortLevel[lot.Level], 62, 166, 66, 24, 16, Theme.Wine, bold: true, align: Align.Center));

        var tag = on
            ? Kit.Box(0, 312, 190, 96, radius: 10, gradient: Kit.Vertical(Theme.Paper, Theme.PaperDark), stroke: Theme.Gold, strokeWidth: 4, shadow: true)
            : Kit.Box(0, 312, 190, 96, radius: 10, gradient: Kit.Vertical(Theme.Paper, Theme.PaperDark), shadow: true);
        tag.With(Kit.Text(CellarParts.GrapeName(lot) + (lot.Adulterated is not null ? " 🧪" : ""), 10, 8, 170, 26, 18, Theme.Ink, bold: true, align: Align.Center));
        var kind = lot.Sparkling ? "Sekt" : Quality.QualityInfo(lot.Level).Name;
        tag.With(Kit.Text($"{(lot.Rose ? "Rosé " : "")}{kind} · Jg. {lot.Vintage}", 8, 36, 174, 24, 15, Theme.InkSoft, align: Align.Center));
        var status = ShortStatus(lot);
        tag.With(Kit.Text($"{Util.FormatNumber(lot.Bottles)} Fl.", 12, 64, 100, 24, 15, Theme.Ink, bold: true));
        tag.With(Kit.Text(status.Text, 90, 64, 90, 24, 15, status.Color, bold: true, align: Align.End));
        cell.With(tag);
        var id = lot.Id;
        cell.Tap(() => session.Nav.Select(Area, id));
        return cell;
    }

    static SkiaControl LotInspector(GameSession session, BottleLot lot, CompetitionInfo competition)
    {
        var state = session.State;
        var player = session.Player;
        var nav = session.Nav;
        var card = CellarParts.Inspector();
        var (name, vintage) = CellarParts.NameOf(lot);
        if (lot.Adulterated is { } method) CellarParts.Head(card, name, $"Jg. {lot.Vintage} · gepanscht: {Economy.Adulteration[method].Name}", Theme.Alert);
        else CellarParts.Head(card, name, $"{vintage} · {(lot.Sparkling ? "Flaschengärung" : "abgefüllt")}");

        card.With(CellarParts.QualityRing(CellarParts.Pad, 108, lot.Quality));
        var price = Market.BottlePrice(lot, player, state);
        var wholesale = Market.WholesalePrice(lot, player, state);
        var trend = CellarParts.TrendValue(CellarParts.TrendOf(state, lot));
        const double rx = 186, rw = 306;
        card.With(Kit.Row("Flaschen", $"{Util.FormatNumber(lot.Bottles)} Fl.", rx, 108, rw, size: 18));
        card.With(Kit.Row("Preis ab Hof", $"{CellarParts.Cents(price)}/Fl.", rx, 142, rw, size: 18));
        card.With(Kit.Row("Großhandel", $"{CellarParts.Cents(wholesale)}/Fl.", rx, 176, rw, size: 18));
        card.With(Kit.Row("Markttrend", trend.Text, rx, 210, rw, trend.Color, 18));
        card.With(Kit.Row("Mostgewicht", $"{Util.FormatNumber(lot.Oechsle)} °Oe", rx, 244, rw, size: 18));
        card.With(Kit.Row("Wert ab Hof", $"≈ {Util.FormatEuro(price * lot.Bottles)}", rx, 278, rw, size: 18));
        card.With(CellarParts.Divider(322));

        // Prüfung und Anmeldung
        var status = Status(lot);
        var entered = player.CompetitionEntries.Contains(lot.Id);
        card.With(Kit.Text("Amtliche Prüfung", CellarParts.Pad, 334, 200, 28, 19, Theme.InkSoft));
        card.With(Kit.Text(status.Text, 190, 334, 302, 28, 19, status.Color, bold: true, align: Align.End));
        card.With(Kit.Text("Prämierung", CellarParts.Pad, 368, 200, 28, 19, Theme.InkSoft));
        card.With(Kit.Text(entered ? $"angemeldet: {competition.Name}" : "nicht angemeldet", 150, 368, 342, 28, 19, entered ? Theme.Good : Theme.Ink, bold: true, align: Align.End));

        // Auszeichnungen
        card.With(Kit.Text("Auszeichnungen", CellarParts.Pad, 408, 300, 28, 19, Theme.Wine, bold: true));
        if (lot.Awards.Count == 0)
        {
            card.With(Kit.Text("Noch keine Medaille. Geprüfte Weine können zur Prämierung.", CellarParts.Pad, 438, CellarParts.Inner, 26, 17, Theme.InkSoft));
        }
        var ay = 438.0;
        var visible = lot.Awards.Count > 3 ? 2 : lot.Awards.Count;
        foreach (var award in lot.Awards.Take(visible))
        {
            var medal = award.Medal;
            card.With(Kit.Paint(CellarParts.Pad, ay, 30, 30, (c, w, h) => CellarParts.PaintMedal(c, 15, 11, 10, medal)));
            card.With(Kit.Text($"{Economy.MedalNames[award.Medal]} · {Economy.Competitions[award.Competition].Name} · Jahr {award.Year}", CellarParts.Pad + 38, ay + 1, CellarParts.Inner - 38, 26, 17, Theme.Ink));
            ay += 30;
        }
        if (lot.Awards.Count > visible)
        {
            var rest = lot.Awards.Skip(visible).ToList();
            card.With(Kit.Paint(CellarParts.Pad, ay, 30 * Math.Min(rest.Count, 6), 30, (c, w, h) =>
            {
                for (var i = 0; i < Math.Min(rest.Count, 6); i++) CellarParts.PaintMedal(c, 15 + i * 30, 11, 10, rest[i].Medal);
            }));
            card.With(Kit.Text($"und {rest.Count} weitere", CellarParts.Pad + 30 * Math.Min(rest.Count, 6) + 10, ay + 1, 200, 26, 17, Theme.InkSoft));
        }

        const double top = 544;
        var register = new GameAction.Register(lot.Id);
        card.With(CellarParts.Action("Prüfen lassen", CellarParts.Pad, top, CellarParts.Inner, 84, () => session.Run(register), ButtonKind.Primary,
            $"amtliche Prüfung · kostet {Util.FormatEuro(Economy.RegisterFee)} · +{Util.FormatNumber(Market.RegistrationPoints(lot))} Punkte", session.Problem(register)));
        card.With(Kit.Button("Großhandel ›", CellarParts.Pad, top + 96, CellarParts.Inner, 84, () => nav.Go(Scene.Markt, 1), ButtonKind.Secondary,
            lot.Registered
                ? $"Verkauf im Markt · alle Flaschen ≈ +{Util.FormatEuro(wholesale * lot.Bottles)}"
                : "Verkauf im Markt · nimmt nur geprüften Wein"));
        var entryProblem = Market.CompetitionEntryProblem(player, lot, competition);
        card.With(Kit.Button("Prämierung ›", CellarParts.Pad, top + 192, CellarParts.Inner, 84, () => nav.Go(Scene.Markt, 3), ButtonKind.Secondary,
            entered ? $"angemeldet · {competition.Name} im {Game.MonthNames[competition.Month - 1]}"
            : entryProblem is null ? $"{competition.Name} im {Game.MonthNames[competition.Month - 1]} · Gebühr {Util.FormatEuro(competition.Fee)}"
            : $"Anmeldung im Markt · {entryProblem}"));

        var saturation = Market.WholesaleSaturation(player);
        var note = saturation < 1
            ? $"Großhandel gesättigt: In diesem Jahr gibt es nur noch {Js.Round(saturation * 100)} % des üblichen Preises. {CellarParts.DemandText(player)}"
            : $"{CellarParts.DemandText(player)} Ungeprüfter Wein geht nur als „Deutscher Wein“ über den Tresen.";
        card.With(Kit.Text(note, CellarParts.Pad, 840, CellarParts.Inner, 84, 17, Theme.InkSoft, maxLines: 3));
        return card;
    }

    static SkiaControl EmptyInspector(GameSession session, CompetitionInfo competition)
    {
        var player = session.Player;
        var card = CellarParts.Inspector();
        CellarParts.Head(card, "Flaschenlager", "Noch nichts abgefüllt");
        card.With(Kit.Text("Abgefüllter Wein wartet hier auf Kundschaft. Geprüfte Weine bringen Punkte und den vollen Preis, die besten gewinnen Medaillen.",
            CellarParts.Pad, 112, CellarParts.Inner, 110, 19, Theme.Ink, maxLines: 4));
        card.With(CellarParts.Divider(236));
        card.With(Kit.Row("Prüfgebühr", Util.FormatEuro(Economy.RegisterFee), CellarParts.Pad, 252, CellarParts.Inner));
        card.With(Kit.Row("Mindestmenge", $"{Util.FormatNumber(Economy.RegisterMinBottles)} Fl. (5 hl)", CellarParts.Pad, 292, CellarParts.Inner));
        card.With(Kit.Row("Absatz ab Hof", $"≈ {Util.FormatNumber(Live.Demand(player))} Fl./Monat", CellarParts.Pad, 332, CellarParts.Inner));
        card.With(Kit.Row($"Prämierung im {Game.MonthNames[competition.Month - 1]}", competition.Name, CellarParts.Pad, 372, CellarParts.Inner, size: 18));
        card.With(Kit.Button("Zu den Tanks ›", CellarParts.Pad, 560, CellarParts.Inner, 84, () => session.Nav.SetTab(0), ButtonKind.Primary, "ausgebauten Wein abfüllen"));
        card.With(Kit.Button("Großhandel ›", CellarParts.Pad, 656, CellarParts.Inner, 84, () => session.Nav.Go(Scene.Markt, 1), ButtonKind.Secondary, "Verkauf im Markt"));
        card.With(Kit.Button("Prämierung ›", CellarParts.Pad, 752, CellarParts.Inner, 84, () => session.Nav.Go(Scene.Markt, 3), ButtonKind.Secondary,
            $"{competition.Name} im {Game.MonthNames[competition.Month - 1]}"));
        return card;
    }

    /// <summary>Flasche in einer Fläche von 190 × 290 – Sekt mit Folie und Perlen, Rotwein dunkel, Weißwein grünlich, Rosé hell.</summary>
    static void PaintBottle(SKCanvas c, BottleLot lot, bool selected)
    {
        if (selected)
        {
            using var glow = new SKPaint
            {
                IsAntialias = true,
                Shader = SKShader.CreateRadialGradient(new SKPoint(95, 150), 96,
                    [Theme.SkGold.WithAlpha(130), Theme.SkGold.WithAlpha(60), Theme.SkGold.WithAlpha(0)], [0, 0.6f, 1], SKShaderTileMode.Clamp),
            };
            c.DrawRect(0, 0, 190, 290, glow);
        }
        using (var shadow = new SKPaint { IsAntialias = true, Color = SKColors.Black.WithAlpha(140), MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6) })
            c.DrawOval(95, 278, 42, 7, shadow);

        var red = Grapes.All[lot.Grape].Color == GrapeColor.Rot;
        var glass = lot.Sparkling ? "#2f4a2a" : lot.Rose ? "#e9a9a2" : red ? "#3d1a2a" : "#6f8a3e";
        var wine = lot.Rose ? "#d0606e" : red ? "#6d1838" : "#e6d36a";
        c.Save();
        c.Translate(41, 10);
        c.Scale(2.7f);
        if (lot.Sparkling)
        {
            using var foil = Draw.Fill("#d6b24a");
            Draw.Path(c, "M15 2 h10 v20 h-10 Z", foil);
        }
        else
        {
            using var cap = Draw.Fill("#7b2346");
            c.DrawRoundRect(15.5f, 2, 9, 6, 1, 1, cap);
        }
        using (var body = Draw.Fill(glass)) Draw.Path(c, "M16 8 h8 v18 q0 8 8 14 v54 q0 4 -4 4 h-16 q-4 0 -4 -4 v-54 q8 -6 8 -14 Z", body);
        using (var fill = Draw.Fill(SKColor.Parse(wine).WithAlpha(90))) Draw.Path(c, "M10 56 h20 v36 q0 2 -2 2 h-16 q-2 0 -2 -2 Z", fill);
        using (var label = Draw.Fill("#f6efe2")) c.DrawRoundRect(10, 50, 20, 26, 1.5f, 1.5f, label);
        using (var line = Draw.Stroke(SKColor.Parse("#c8962e"), 0.6f)) c.DrawLine(12, 53, 28, 53, line);
        if (lot.Sparkling)
        {
            using var pearl = Draw.Fill(SKColors.White.WithAlpha(180));
            c.DrawCircle(14, 86, 1.2f, pearl);
            c.DrawCircle(22, 82, 1, pearl);
            c.DrawCircle(26, 88, 1.3f, pearl);
        }
        using (var shine = Draw.Fill(SKColors.White.WithAlpha(46))) Draw.Path(c, "M12 42 q2 -3 5 -4 v10 h-5 Z M10.8 78 h4 v15 h-3 q-1 0 -1 -1 Z", shine);
        c.Restore();

        // Medaillen als Aufkleber unter dem Etikett
        var count = Math.Min(3, lot.Awards.Count);
        var best = lot.Awards.OrderBy(a => (int)a.Medal).Take(count).ToList();
        for (var i = 0; i < count; i++) CellarParts.PaintMedal(c, 95 + (i - (count - 1) / 2f) * 25, 238, 11, best[i].Medal);
    }
}
