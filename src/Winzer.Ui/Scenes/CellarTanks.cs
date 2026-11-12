using DrawnUi.Draw;
using SkiaSharp;
using Winzer.Engine;
using Winzer.Engine.Data;
using Winzer.Presentation;

namespace Winzer.Ui.Scenes;

/// <summary>Reiter „Tanks &amp; Fässer“: jede Charge ein Tank oder Fass, rechts Werte und Aktionen der gewählten Charge.</summary>
internal static class CellarTanks
{
    const string Area = "keller.batch";
    const string BuyTank = "+tank";
    const string BuyBarrique = "+barrique";
    const int TanksPerPage = 6;
    const int BarrelsPerPage = 5;
    static readonly double[] TankSteps = [50, 100, 200, 300, 500];
    static readonly double[] BarriqueSteps = [10, 25, 50, 100];

    public static void Build(SkiaLayout scene, GameSession session)
    {
        var state = session.State;
        var player = session.Player;
        var nav = session.Nav;
        var tanks = player.Cellar.Where(b => !b.Barrique).ToList();
        var barrels = player.Cellar.Where(b => b.Barrique).ToList();
        var chosen = nav.Selected(Area);
        var buying = chosen is BuyTank or BuyBarrique ? chosen : null;
        var batch = buying is null ? player.Cellar.Find(b => b.Id == chosen) ?? tanks.FirstOrDefault() ?? barrels.FirstOrDefault() : null;

        var usedTank = Rules.UsedTankHl(player);
        var usedBarrique = Rules.UsedBarriqueHl(player);
        var freeTank = Math.Max(0, player.TankCapacityHl - usedTank);
        var freeBarrique = Math.Max(0, player.BarriqueCapacityHl - usedBarrique);
        scene.With(CellarParts.Gauge(836, 22, 186, "Tank", $"{Util.FormatNumber(usedTank, 1)} / {Util.FormatNumber(player.TankCapacityHl)} hl",
            player.TankCapacityHl > 0 ? usedTank / player.TankCapacityHl : 0, Theme.Gold));
        scene.With(CellarParts.Gauge(1044, 22, 186, "Barrique", player.BarriqueCapacityHl > 0 ? $"{Util.FormatNumber(usedBarrique, 1)} / {Util.FormatNumber(player.BarriqueCapacityHl)} hl" : "keins",
            player.BarriqueCapacityHl > 0 ? usedBarrique / player.BarriqueCapacityHl : 0, Theme.C("#c08a52")));

        // ---- Tanks ----
        var tankPages = Math.Max(1, (int)Math.Ceiling(tanks.Count / (double)TanksPerPage));
        var tankPage = nav.Page("keller.tanks", tankPages);
        var reference = Math.Max(tanks.Count == 0 ? 0 : tanks.Max(b => b.Hectoliters), Math.Max(1, player.TankCapacityHl / 4));
        var shown = tanks.Skip(tankPage * TanksPerPage).Take(TanksPerPage).ToList();
        for (var i = 0; i < TanksPerPage; i++)
        {
            var b = i < shown.Count ? shown[i] : null;
            var on = b is not null && b == batch;
            var level = b is null ? 0 : Math.Clamp(b.Hectoliters / reference, 0.08, 1);
            var progress = b is null ? 0 : Progress(b);
            var slot = Kit.Layer(26 + i * 170, 118, 168, 490);
            slot.With(Kit.Paint(4, 0, 160, 400, (c, w, h) => PaintTank(c, b, level, progress, on)));
            if (b is not null)
            {
                var id = b.Id;
                slot.With(Kit.Text(CellarParts.GrapeName(b) + (b.Adulterated is not null ? " 🧪" : ""), 0, 410, 168, 26, 18, on ? Theme.Gold : Theme.Cream, bold: true, align: Align.Center));
                slot.With(Kit.Text((b.Rose ? "Rosé " : "") + Quality.QualityInfo(b.Level).Name, 0, 435, 168, 24, 16, Theme.CreamSoft, align: Align.Center));
                slot.With(Kit.Text($"{CellarParts.Hl(b.Hectoliters)} · {(b.MonthsUntilReady > 0 ? $"noch {b.MonthsUntilReady} Mon." : "reif")}", 0, 458, 168, 24, 16,
                    b.MonthsUntilReady > 0 ? Theme.CreamSoft : Theme.GoodLight, align: Align.Center));
                slot.Tap(() => nav.Select(Area, id));
            }
            else if (i == shown.Count)
            {
                slot.With(Kit.Text(freeTank > 0 ? "leer" : "Tanks voll", 0, 410, 168, 26, 18, freeTank > 0 ? Theme.CreamSoft : Theme.Warn, align: Align.Center));
                slot.With(Kit.Text($"{CellarParts.Hl(freeTank)} frei", 0, 435, 168, 24, 16, Theme.CreamSoft, align: Align.Center));
            }
            scene.With(slot);
        }
        scene.With(BuySlot(26 + TanksPerPage * 170, 118, 366, "Tank kaufen", $"{Util.FormatEuro(Economy.TankCostPerHl)} je hl", buying == BuyTank, () => nav.Select(Area, BuyTank)));
        if (tankPages > 1) scene.With(Kit.Pager(930, 80, 300, tankPage, tankPages, p => nav.SetPage("keller.tanks", p), noun: "Tanks · Seite"));

        // ---- Barrique ----
        scene.With(Kit.Text("Barrique", 40, 612, 150, 34, 24, Theme.Gold, bold: true, serif: true));
        scene.With(Kit.Text(player.BarriqueCapacityHl > 0
            ? $"{CellarParts.Hl(usedBarrique)} belegt · {CellarParts.Hl(freeBarrique)} frei – {Economy.BarriqueMonths} Monate im Eichenfass, vor allem Rotwein gewinnt an Güte"
            : "Noch keine Fässer – Eichenfässer heben die Güte, vor allem bei Rotwein", 180, 619, 700, 26, 17, Theme.CreamSoft));
        var barrelPages = Math.Max(1, (int)Math.Ceiling(barrels.Count / (double)BarrelsPerPage));
        var barrelPage = nav.Page("keller.barrels", barrelPages);
        var shownBarrels = barrels.Skip(barrelPage * BarrelsPerPage).Take(BarrelsPerPage).ToList();
        for (var i = 0; i < BarrelsPerPage; i++)
        {
            var b = i < shownBarrels.Count ? shownBarrels[i] : null;
            var on = b is not null && b == batch;
            var owned = player.BarriqueCapacityHl > 0;
            var slot = Kit.Layer(30 + i * 196, 654, 190, 200);
            slot.With(Kit.Paint(7, 0, 176, 148, (c, w, h) => PaintBarrel(c, b, on, owned)));
            if (b is not null)
            {
                var id = b.Id;
                slot.With(Kit.Text(CellarParts.GrapeName(b) + (b.Adulterated is not null ? " 🧪" : ""), 0, 146, 190, 26, 18, on ? Theme.Gold : Theme.Cream, bold: true, align: Align.Center));
                slot.With(Kit.Text($"{CellarParts.Hl(b.Hectoliters)} · {(b.MonthsUntilReady > 0 ? $"noch {b.MonthsUntilReady} Mon." : "reif")}", 0, 172, 190, 24, 16,
                    b.MonthsUntilReady > 0 ? Theme.CreamSoft : Theme.GoodLight, align: Align.Center));
                slot.Tap(() => nav.Select(Area, id));
            }
            else if (i == shownBarrels.Count && owned)
            {
                slot.With(Kit.Text($"{CellarParts.Hl(freeBarrique)} frei", 0, 146, 190, 26, 18, Theme.CreamSoft, align: Align.Center));
            }
            scene.With(slot);
        }
        scene.With(BuySlot(1046, 654, 124, "Fässer kaufen", $"{Util.FormatEuro(Economy.BarriqueCostPerHl)} je hl", buying == BuyBarrique, () => nav.Select(Area, BuyBarrique)));
        if (barrelPages > 1) scene.With(Kit.Pager(930, 610, 300, barrelPage, barrelPages, p => nav.SetPage("keller.barrels", p), noun: "Fässer · Seite"));

        scene.With(Notes(session));

        scene.With(buying == BuyTank ? BuyInspector(session, false)
            : buying == BuyBarrique ? BuyInspector(session, true)
            : batch is not null ? BatchInspector(session, batch)
            : EmptyInspector(session));
    }

    /// <summary>Ausbaufortschritt 0–1</summary>
    static double Progress(WineBatch batch)
    {
        var total = batch.Barrique ? Economy.BarriqueMonths : Math.Max(Quality.QualityInfo(batch.Level).MaturingMonths, 1);
        return 1 - batch.MonthsUntilReady / (double)Math.Max(total, batch.MonthsUntilReady);
    }

    static double AmountOf(double share, double total) =>
        share >= 0.999 ? total : Math.Min(total, Math.Max(1, Js.Round(share * total)));

    /// <summary>Gestrichelter Platz „+ … kaufen“</summary>
    static SkiaControl BuySlot(double x, double y, double height, string title, string price, bool on, Action tap)
    {
        var slot = Kit.Layer(x, y, 168, height + 110);
        slot.With(Kit.Paint(4, 0, 160, height + 20, (c, w, h) =>
        {
            using var back = Draw.Fill(SKColor.Parse("#f4ead7").WithAlpha((byte)(on ? 36 : 14)));
            c.DrawRoundRect(10, 10, 140, h - 20, 26, 26, back);
            using var line = Draw.Stroke(on ? Theme.SkGold : Theme.SkCreamSoft.WithAlpha(170), on ? 5 : 3);
            if (!on) line.PathEffect = SKPathEffect.CreateDash([12, 10], 0);
            c.DrawRoundRect(10, 10, 140, h - 20, 26, 26, line);
            using var plus = Draw.Stroke(on ? Theme.SkGold : Theme.SkCreamSoft, 6);
            var cy = h / 2 - 22;
            c.DrawLine(80 - 20, cy, 80 + 20, cy, plus);
            c.DrawLine(80, cy - 20, 80, cy + 20, plus);
        }));
        var mid = (height + 20) / 2;
        slot.With(Kit.Text(title, 4, mid + 12, 160, 26, 18, on ? Theme.Gold : Theme.Cream, bold: true, align: Align.Center));
        slot.With(Kit.Text(price, 4, mid + 38, 160, 24, 15, Theme.CreamSoft, align: Align.Center));
        slot.Tap(tap);
        return slot;
    }

    /// <summary>Hinweise unten: Absatz ab Hof, Kellermeister, Fassweinkrise</summary>
    static SkiaControl Notes(GameSession session)
    {
        var state = session.State;
        var player = session.Player;
        var lines = new List<(string Text, Color Dot, Action? Tap)> { (CellarParts.DemandText(player), Theme.Gold, null) };
        if (CellarParts.MasterHint(player) is { } hint)
            lines.Add(($"{hint} Bewerber findest du unter „Kellermeister“ ›", Theme.Warn, () => session.Nav.SetTab(3)));
        if (state.BulkCrisisMonths > 0)
            lines.Add(($"Krise der Großkellereien: Fasswein bringt noch {CellarParts.Months(state.BulkCrisisMonths)} nur den halben Preis.", Theme.C("#ff9b8a"), () => session.Nav.SetTab(2)));
        var panel = Kit.Glass(28, 866, 1202, 96, 16, shadow: false);
        var y = (96 - lines.Count * 28) / 2.0;
        foreach (var line in lines)
        {
            panel.With(Kit.Box(22, y + 9, 12, 12, line.Dot, 6));
            var text = Kit.Text(line.Text, 46, y, 1130, 28, 18, Theme.Cream);
            if (line.Tap is { } tap) text.Tap(tap);
            panel.With(text);
            y += 28;
        }
        return panel;
    }

    // ---- Inspektor ----

    static SkiaControl BatchInspector(GameSession session, WineBatch batch)
    {
        var state = session.State;
        var player = session.Player;
        var nav = session.Nav;
        var card = CellarParts.Inspector();
        var (name, vintage) = CellarParts.NameOf(batch);
        var vessel = batch.Barrique ? "Barriquefass" : "Edelstahltank";
        if (batch.Adulterated is { } method) CellarParts.Head(card, name, $"Jg. {batch.Vintage} · {vessel} · gepanscht: {Economy.Adulteration[method].Name}", Theme.Alert);
        else CellarParts.Head(card, name, $"{vintage} · {vessel}");

        card.With(CellarParts.QualityRing(CellarParts.Pad, 108, batch.Quality));
        var bottlesAll = Market.HlToBottles(batch.Hectoliters);
        // Wert, wenn der Wein geprüft ab Hof verkauft würde
        var probe = new BottleLot { Registered = true, Bottles = bottlesAll };
        probe.CopyInfoFrom(batch);
        var trend = CellarParts.TrendValue(CellarParts.TrendOf(state, batch));
        const double rx = 186, rw = 306;
        card.With(Kit.Row("Stufe", Quality.QualityInfo(batch.Level).Name, rx, 108, rw, size: 18));
        card.With(Kit.Row("Mostgewicht", $"{Util.FormatNumber(batch.Oechsle)} °Oe", rx, 142, rw, size: 18));
        card.With(Kit.Row("Menge", $"{CellarParts.Hl(batch.Hectoliters)} · {Util.FormatNumber(bottlesAll)} Fl.", rx, 176, rw, size: 18));
        card.With(Kit.Row("Reife", batch.MonthsUntilReady > 0 ? $"noch {CellarParts.Months(batch.MonthsUntilReady)}" : "ausgebaut", rx, 210, rw,
            batch.MonthsUntilReady > 0 ? Theme.Ink : Theme.Good, 18));
        card.With(Kit.Row("Markttrend", trend.Text, rx, 244, rw, trend.Color, 18));
        card.With(Kit.Row("Wert ab Hof", $"≈ {Util.FormatEuro(Market.BottlePrice(probe, player, state) * bottlesAll)}", rx, 278, rw, size: 18));
        card.With(CellarParts.Divider(322));

        var share = nav.Amount("keller.amount");
        var amount = AmountOf(share, batch.Hectoliters);
        var bottles = Market.HlToBottles(amount);
        card.With(Kit.Text("Menge für die Aktion", CellarParts.Pad, 334, CellarParts.Inner, 30, 20, Theme.Ink, bold: true));
        card.With(Kit.AmountSlider(CellarParts.Pad, 374, CellarParts.Inner, share,
            $"{CellarParts.Hl(amount)} von {CellarParts.Hl(batch.Hectoliters)}", s => nav.SetAmount("keller.amount", s)));

        var id = batch.Id;
        var fresh = Products.IsFreshMust(state, batch) && Economy.FederweisserMonths.Contains(state.Month);
        const double top = 466;
        if (nav.Selected("keller.more") != "1")
        {
            var bottle = new GameAction.Bottle(id, amount);
            card.With(CellarParts.Action("Abfüllen", CellarParts.Pad, top, CellarParts.Inner, 84, () => session.Run(bottle), ButtonKind.Primary,
                $"{Util.FormatNumber(bottles)} Flaschen · kostet {Util.FormatEuro(Js.Round(bottles * Cellar.BottleCost(player)))}", session.Problem(bottle)));

            var bulk = new GameAction.SellBulk(id, amount);
            card.With(CellarParts.Action("Als Fasswein verkaufen", CellarParts.Pad, top + 96, CellarParts.Inner, 84, () => session.Run(bulk), ButtonKind.Secondary,
                $"sofort +{Util.FormatEuro(Js.Round(Market.BulkPricePerHl(batch, state, player) * amount))} · keine Punkte{(state.BulkCrisisMonths > 0 ? " · Krise: halber Preis" : "")}",
                session.Problem(bulk)));

            var sekt = new GameAction.MakeSekt(id, amount);
            card.With(CellarParts.Action("Sekt ansetzen", CellarParts.Pad, top + 192, CellarParts.Inner, 84, () => session.Run(sekt), ButtonKind.Secondary,
                $"{Economy.SektMonths} Monate Reife · +{Util.FormatNumber(Economy.SektQualityBonus)} Güte · kostet {Util.FormatEuro(Js.Round(bottles * Cellar.SektCost(player)))}",
                session.Problem(sekt)));

            var barrique = new GameAction.ToBarrique(id);
            card.With(CellarParts.Action("Ins Barrique umlagern", CellarParts.Pad, top + 288, CellarParts.Inner, 84, () => session.Run(barrique), ButtonKind.Secondary,
                $"ganze Charge · {Economy.BarriqueMonths} Monate im Eichenfass · +{Util.FormatNumber(Cellar.BarriqueBonus(batch))} Güte", session.Problem(barrique)));

            card.With(Kit.Text(fresh ? $"Jetzt möglich: {Products.FederweisserName(batch)} – unter „…“" : "Mehr unter „…“: Federweißer, Panschen",
                CellarParts.Pad, 880, 380, 26, 17, fresh ? Theme.Wine : Theme.InkSoft, bold: fresh));
            card.With(Kit.Button("…", 436, 868, 56, 48, () => nav.Select("keller.more", "1"), ButtonKind.Ghost, fontSize: 22, align: Align.Center));
            if (fresh) card.With(Kit.Badge(1, 488, 870, 11));
        }
        else
        {
            var capacity = Products.FederweisserCapacity(player);
            var federHl = capacity > 0 ? Math.Min(amount, capacity) : amount;
            var feder = new GameAction.SellFederweisser(id, federHl);
            card.With(CellarParts.Action($"{Products.FederweisserName(batch)} verkaufen", CellarParts.Pad, top, CellarParts.Inner, 84, () => session.Run(feder), ButtonKind.Primary,
                $"{CellarParts.Hl(federHl)} · +{Util.FormatEuro(Js.Round(Products.FederweisserPricePerHl(player) * federHl))} · diesen Monat noch {CellarParts.Hl(capacity)}",
                session.Problem(feder)));

            card.With(Kit.Text("Kellertricks", CellarParts.Pad, top + 96, 200, 28, 20, Theme.Alert, bold: true));
            card.With(Kit.Text("Fliegt es auf, wird aller gepanschte Wein beschlagnahmt – dazu Strafe, Punktabzug und ein Skandal.",
                CellarParts.Pad, top + 124, CellarParts.Inner, 46, 16, Theme.InkSoft, maxLines: 2));
            var y = top + 178;
            foreach (var (kind, info) in Economy.Adulteration)
            {
                var action = new GameAction.Adulterate(id, kind);
                var effect = new List<string>();
                if (info.VolumeFactor != 1) effect.Add($"{(info.VolumeFactor > 1 ? "+" : "−")}{Math.Abs(Js.Round((info.VolumeFactor - 1) * 100))} % Menge");
                if (info.QualityDelta != 0) effect.Add($"{(info.QualityDelta > 0 ? "+" : "−")}{Util.FormatNumber(Math.Abs(info.QualityDelta))} Güte");
                if (info.LevelSteps > 0) effect.Add(info.LevelSteps == 1 ? "eine Stufe höher" : $"{info.LevelSteps} Stufen höher");
                card.With(CellarParts.Action($"Panschen: {info.Name}", CellarParts.Pad, y, CellarParts.Inner, 72, () => session.Run(action), ButtonKind.Secondary,
                    $"{string.Join(", ", effect)} · {Js.Round(info.Detection * 100)} % Risiko bei der Prüfung", session.Problem(action), 19));
                y += 80;
            }
            card.With(Kit.Button("‹ Zurück zu Abfüllen & Verkauf", CellarParts.Pad, 884, CellarParts.Inner, 44, () => nav.Select("keller.more", null), ButtonKind.Ghost, fontSize: 18, align: Align.Center));
        }
        return card;
    }

    /// <summary>Tanks oder Barriquefässer kaufen</summary>
    static SkiaControl BuyInspector(GameSession session, bool barrique)
    {
        var player = session.Player;
        var nav = session.Nav;
        var card = CellarParts.Inspector();
        var steps = barrique ? BarriqueSteps : TankSteps;
        var key = barrique ? "keller.buy.barrique" : "keller.buy.tank";
        var index = int.TryParse(nav.Selected(key), out var parsed) ? Math.Clamp(parsed, 0, steps.Length - 1) : 1;
        var hl = steps[index];
        var price = barrique ? Economy.BarriqueCostPerHl : Economy.TankCostPerHl;
        var capacity = barrique ? player.BarriqueCapacityHl : player.TankCapacityHl;
        var used = barrique ? Rules.UsedBarriqueHl(player) : Rules.UsedTankHl(player);

        CellarParts.Head(card, barrique ? "Barriquefässer" : "Edelstahltanks", barrique ? "Eichenfässer für den Ausbau" : "Mehr Platz für die Ernte");
        card.With(Kit.Paint(CellarParts.Pad - 6, 104, 140, 200, (c, w, h) =>
        {
            if (barrique)
            {
                c.Translate(-18, 26);
                PaintBarrel(c, null, false, true);
            }
            else
            {
                c.Translate(26, 0);
                c.Scale(0.5f);
                PaintTank(c, null, 0, 0, false, dim: false);
            }
        }));
        const double rx = 186, rw = 306;
        card.With(Kit.Row("Vorhanden", capacity > 0 ? CellarParts.Hl(capacity) : "keins", rx, 116, rw, size: 19));
        card.With(Kit.Row("Belegt", CellarParts.Hl(used), rx, 152, rw, size: 19));
        card.With(Kit.Row("Frei", CellarParts.Hl(Math.Max(0, capacity - used)), rx, 188, rw, size: 19));
        card.With(Kit.Row("Preis je hl", Util.FormatEuro(price), rx, 224, rw, size: 19));
        card.With(Kit.Row("Konto", Util.FormatEuro(player.Money), rx, 260, rw, player.Money < hl * price ? Theme.Alert : Theme.Ink, 19));
        card.With(CellarParts.Divider(322));

        card.With(Kit.Text("Wie viel?", CellarParts.Pad, 334, CellarParts.Inner, 30, 20, Theme.Ink, bold: true));
        card.With(CellarParts.Choice(CellarParts.Pad, 376, CellarParts.Inner, steps.Select(s => $"{Util.FormatNumber(s)} hl").ToArray(), index,
            i => nav.Select(key, i.ToString())));

        GameAction action = barrique ? new GameAction.ExpandBarrique(hl) : new GameAction.ExpandTank(hl);
        card.With(CellarParts.Action(barrique ? "Fässer kaufen" : "Tank kaufen", CellarParts.Pad, 466, CellarParts.Inner, 84, () => session.Run(action), ButtonKind.Primary,
            $"+{Util.FormatNumber(hl)} hl · kostet {Util.FormatEuro(hl * price)} · danach {CellarParts.Hl(capacity + hl)}", session.Problem(action)));

        card.With(Kit.Text(barrique
            ? $"Eichenfässer – vor allem Rotwein gewinnt an Güte (+{Util.FormatNumber(Economy.BarriqueQualityRed)}, Weißwein +{Util.FormatNumber(Economy.BarriqueQualityWhite)}). Der Wein reift {Economy.BarriqueMonths} Monate im Fass und macht so lange Platz im Tank. Nutzbar nur mit einem Kellermeister, der Barrique beherrscht."
            : "Was bei der Lese nicht mehr in die Tanks passt, geht sofort billig an die Kellerei. Wein im Barrique belegt keinen Tank.",
            CellarParts.Pad, 572, CellarParts.Inner, 130, 18, Theme.InkSoft, maxLines: 5));
        if (barrique && CellarParts.MasterHint(player) is { } hint && !CellarMasters.HasSkill(player, CellarSkill.Barrique))
        {
            card.With(Kit.Text(hint, CellarParts.Pad, 706, CellarParts.Inner, 50, 17, Theme.Alert, bold: true, maxLines: 2));
        }

        card.With(Kit.Button(barrique ? "Edelstahltanks ›" : "Barriquefässer ›", CellarParts.Pad, 784, CellarParts.Inner, 64,
            () => nav.Select(Area, barrique ? BuyTank : BuyBarrique), ButtonKind.Secondary,
            barrique ? $"{Util.FormatEuro(Economy.TankCostPerHl)} je hl" : $"{Util.FormatEuro(Economy.BarriqueCostPerHl)} je hl", fontSize: 19));
        if (player.Cellar.Count > 0)
        {
            card.With(Kit.Button("‹ Zurück zu den Chargen", CellarParts.Pad, 864, CellarParts.Inner, 52, () => nav.Select(Area, null), ButtonKind.Ghost, fontSize: 18, align: Align.Center));
        }
        return card;
    }

    /// <summary>Kein Wein im Keller: Überblick statt Charge</summary>
    static SkiaControl EmptyInspector(GameSession session)
    {
        var state = session.State;
        var player = session.Player;
        var nav = session.Nav;
        var card = CellarParts.Inspector();
        CellarParts.Head(card, "Dein Keller", "Kein Wein im Tank");
        card.With(Kit.Text("Gelesen wird ab August im Weinberg. Der Most kommt in die Tanks, reift dort einige Monate und wird dann abgefüllt, als Fasswein verkauft oder zu Sekt.",
            CellarParts.Pad, 112, CellarParts.Inner, 110, 19, Theme.Ink, maxLines: 4));
        card.With(CellarParts.Divider(236));
        var competition = Market.UpcomingCompetition(state.Month);
        var y = 252.0;
        void Row(string label, string value)
        {
            card.With(Kit.Row(label, value, CellarParts.Pad, y, CellarParts.Inner));
            y += 40;
        }
        Row("Edelstahltanks", CellarParts.Hl(player.TankCapacityHl));
        Row("Barriquefässer", player.BarriqueCapacityHl > 0 ? CellarParts.Hl(player.BarriqueCapacityHl) : "keine");
        Row("Flaschenlager", $"{Util.FormatNumber(player.Bottles.Sum(l => l.Bottles))} Fl.");
        Row("Absatz ab Hof", $"≈ {Util.FormatNumber(Live.Demand(player))} Fl./Monat");
        Row("Kellermeister", player.CellarMaster?.Name ?? "keiner");
        card.With(Kit.Row($"Prämierung im {Game.MonthNames[competition.Month - 1]}", competition.Name, CellarParts.Pad, y, CellarParts.Inner, size: 18));

        card.With(Kit.Button("Zum Weinberg ›", CellarParts.Pad, 560, CellarParts.Inner, 84, () => nav.Go(Scene.Weinberg, 0), ButtonKind.Primary,
            "Reben pflegen und im Herbst lesen"));
        card.With(Kit.Button("Tank kaufen ›", CellarParts.Pad, 656, CellarParts.Inner, 84, () => nav.Select(Area, BuyTank), ButtonKind.Secondary,
            $"mehr Platz für die Ernte · {Util.FormatEuro(Economy.TankCostPerHl)} je hl"));
        card.With(Kit.Button("Barriquefässer kaufen ›", CellarParts.Pad, 752, CellarParts.Inner, 84, () => nav.Select(Area, BuyBarrique), ButtonKind.Secondary,
            $"mehr Güte, vor allem für Rotwein · {Util.FormatEuro(Economy.BarriqueCostPerHl)} je hl"));
        if (player.Bottles.Count > 0)
        {
            card.With(Kit.Button("Zum Flaschenlager ›", CellarParts.Pad, 864, CellarParts.Inner, 52, () => nav.SetTab(1), ButtonKind.Ghost, fontSize: 18, align: Align.Center));
        }
        return card;
    }

    // ---- Malerei ----

    /// <summary>Edelstahltank in einer Fläche von 160 × 400: Füllstand im Schauglas, Reife an der Uhr.</summary>
    internal static void PaintTank(SKCanvas c, WineBatch? batch, double level, double progress, bool selected, bool dim = true)
    {
        var empty = batch is null;
        if (empty && dim)
        {
            using var veil = new SKPaint { Color = SKColors.White.WithAlpha(120) };
            c.SaveLayer(veil);
        }
        using (var shadow = new SKPaint { IsAntialias = true, Color = SKColors.Black.WithAlpha(120), MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 9) })
            c.DrawRoundRect(14, 20, 140, 366, 26, 26, shadow);
        using (var leg = Draw.HorizontalGradient(0, 160, (0, "#3d444a"), (0.5f, "#7b858e"), (1, "#3d444a")))
        {
            c.DrawRect(30, 366, 16, 26, leg);
            c.DrawRect(114, 366, 16, 26, leg);
        }
        using (var steel = Draw.HorizontalGradient(10, 150, (0, "#6d7780"), (0.35f, "#d7dee4"), (0.7f, "#8d98a2"), (1, "#555e66")))
            c.DrawRoundRect(10, 10, 140, 366, 26, 26, steel);
        using (var seam = Draw.Stroke(SKColor.Parse("#4a525a").WithAlpha(110), 2))
        {
            c.DrawLine(12, 302, 148, 302, seam);
            c.DrawLine(12, 22, 148, 22, seam);
        }

        // Schauglas
        var glass = new SKRoundRect(new SKRect(28, 32, 132, 292), 14);
        using (var dark = Draw.Fill(SKColor.Parse("#1c1418").WithAlpha(empty ? (byte)150 : (byte)190))) c.DrawRoundRect(glass, dark);
        if (batch is not null)
        {
            var (topColor, bottomColor) = CellarParts.WineColors(batch);
            var top = 292 - 260 * (float)level;
            c.Save();
            c.ClipRoundRect(glass, antialias: true);
            using (var wine = Draw.VerticalGradient(top, 292, (0, topColor), (1, bottomColor))) c.DrawRect(28, top, 104, 292 - top, wine);
            using (var surface = Draw.Fill(SKColors.White.WithAlpha(70))) c.DrawRect(28, top, 104, 5, surface);
            c.Restore();
        }
        using (var shine = Draw.Fill(SKColors.White.WithAlpha(34))) c.DrawRoundRect(36, 42, 9, 240, 4.5f, 4.5f, shine);
        using (var tick = Draw.Stroke(SKColor.Parse("#f4ead7").WithAlpha(110), 2))
        {
            for (var i = 1; i < 4; i++) c.DrawLine(116, 292 - i * 65, 130, 292 - i * 65, tick);
        }
        using (var frame = Draw.Stroke(SKColor.Parse("#4a525a").WithAlpha(180), 2.5f)) c.DrawRoundRect(glass, frame);

        // Reife-Uhr und Ablasshahn
        using (var face = Draw.Fill("#2a2420")) c.DrawCircle(58, 338, 24, face);
        using (var rim = Draw.Stroke("#4a525a", 3)) c.DrawCircle(58, 338, 24, rim);
        if (batch is not null)
        {
            var ready = batch.MonthsUntilReady <= 0;
            Draw.Ring(c, 58, 338, 15, 7, ready ? 1 : Math.Max(0.04, progress), SKColor.Parse("#f4ead7").WithAlpha(50), ready ? SKColor.Parse("#7dbb6a") : Theme.SkGold);
            if (ready)
            {
                using var check = Draw.Stroke("#7dbb6a", 3.5f);
                Draw.Path(c, "M51 338 l5 5 l9 -10", check);
            }
        }
        using (var valve = Draw.Fill("#3d444a"))
        {
            c.DrawRoundRect(96, 330, 30, 14, 4, 4, valve);
            c.DrawRect(107, 344, 8, 12, valve);
        }
        using (var handle = Draw.Stroke("#8e2440", 5)) c.DrawLine(100, 324, 122, 324, handle);

        if (empty && dim) c.Restore();
        if (selected)
        {
            using var gold = Draw.VerticalGradient(4, 382, (0, "#f6d477"), (1, "#c8962e"));
            gold.Style = SKPaintStyle.Stroke;
            gold.StrokeWidth = 5;
            c.DrawRoundRect(4, 4, 152, 378, 30, 30, gold);
        }
    }

    /// <summary>Barriquefass von vorn in einer Fläche von 176 × 148</summary>
    internal static void PaintBarrel(SKCanvas c, WineBatch? batch, bool selected, bool owned)
    {
        var empty = batch is null;
        if (empty)
        {
            using var veil = new SKPaint { Color = SKColors.White.WithAlpha((byte)(owned ? 130 : 60)) };
            c.SaveLayer(veil);
        }
        using (var shadow = new SKPaint { IsAntialias = true, Color = SKColors.Black.WithAlpha(130), MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 8) })
            c.DrawOval(88, 134, 76, 12, shadow);
        using (var chock = Draw.Fill("#4a3221"))
        {
            Draw.Path(c, "M22 142 L50 142 L44 118 Z", chock);
            Draw.Path(c, "M154 142 L126 142 L132 118 Z", chock);
        }
        using (var oak = Draw.VerticalGradient(8, 136, (0, "#c08a52"), (1, "#6e4524"))) c.DrawOval(88, 72, 78, 64, oak);
        c.Save();
        using (var clip = new SKPath())
        {
            clip.AddOval(new SKRect(10, 8, 166, 136));
            c.ClipPath(clip, antialias: true);
        }
        using (var hoop = Draw.Stroke("#2c2622", 7))
        {
            c.DrawLine(0, 42, 176, 42, hoop);
            c.DrawLine(0, 102, 176, 102, hoop);
        }
        c.Restore();
        using (var head = Draw.VerticalGradient(28, 116, (0, "#6a4020"), (1, "#4a2c14"))) c.DrawOval(88, 72, 54, 44, head);
        c.Save();
        using (var clip = new SKPath())
        {
            clip.AddOval(new SKRect(34, 28, 142, 116));
            c.ClipPath(clip, antialias: true);
        }
        using (var stave = Draw.Stroke(SKColor.Parse("#2c1a0c").WithAlpha(130), 2))
        {
            for (var x = 52; x <= 124; x += 18) c.DrawLine(x, 24, x, 120, stave);
        }
        c.Restore();
        using (var ring = Draw.Stroke(SKColor.Parse("#2c2622").WithAlpha(200), 3)) c.DrawOval(88, 72, 54, 44, ring);
        if (batch is not null)
        {
            // Spund in der Farbe des Weins
            var (topColor, bottomColor) = CellarParts.WineColors(batch);
            using (var wine = Draw.VerticalGradient(58, 86, (0, topColor), (1, bottomColor))) c.DrawCircle(88, 72, 15, wine);
            using (var edge = Draw.Stroke(SKColor.Parse("#2c1a0c"), 3)) c.DrawCircle(88, 72, 15, edge);
            if (batch.MonthsUntilReady <= 0)
            {
                using var check = Draw.Stroke(SKColors.White.WithAlpha(230), 3.5f);
                Draw.Path(c, "M81 72 l5 5 l9 -10", check);
            }
        }
        if (empty) c.Restore();
        if (selected)
        {
            using var gold = Draw.Stroke(Theme.SkGold, 5);
            c.DrawOval(88, 72, 84, 70, gold);
        }
    }
}
