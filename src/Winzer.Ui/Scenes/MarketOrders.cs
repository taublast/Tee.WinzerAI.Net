using DrawnUi.Draw;
using Winzer.Engine;
using Winzer.Engine.Data;
using Winzer.Presentation;

namespace Winzer.Ui.Scenes;

// Reiter „Aufträge & Export“: Anfragen, angenommene Aufträge, Export und der Absatz ab Hof
public static partial class MarketScene
{
    const double SlotWidth = 390, SlotHeight = 260;

    static void BuildOrders(GameSession session, SkiaLayout scene)
    {
        var state = session.State;
        var player = session.Player;
        var now = Bio.GameMonth(state);
        var all = player.Orders.Concat(player.OrderOffers).ToList();
        var wanted = session.Nav.Selected("markt.order");
        var selected = all.FirstOrDefault(o => o.Id == wanted) ?? all.FirstOrDefault();

        scene.With(Kit.Text("Anfragen", 28, 86, 400, 34, 24, Theme.Gold, bold: true, serif: true));
        scene.With(Kit.Text("gelten zwei Monate – annehmen oder ablehnen", 430, 93, 800, 26, 17, Theme.CreamSoft, align: Align.End));
        scene.With(Kit.Text($"Angenommene Aufträge · {player.Orders.Count} / {Orders.MaxOrders}", 28, 396, 600, 34, 24, Theme.Gold, bold: true, serif: true));
        scene.With(Kit.Text($"Verpasster Termin: {Js.Round(Orders.OrderPenaltyShare * 100)} % des Auftragswerts Vertragsstrafe", 530, 403, 700, 26, 17, Theme.CreamSoft, align: Align.End));

        for (var i = 0; i < 3; i++)
        {
            var x = 28 + i * (SlotWidth + 16);
            if (i < player.OrderOffers.Count) scene.With(OrderCard(session, player.OrderOffers[i], x, 124, false, now, selected));
            else
            {
                scene.With(EmptySlot(x, 124, SlotWidth, SlotHeight, i > 0 || player.OrderOffers.Count > 0 ? "frei"
                    : state.Year < 2 ? "Ab dem zweiten Jahr fragen Kunden nach deinen Weinen."
                    : "Gerade liegen keine Anfragen vor – neue kommen regelmäßig."));
            }
            if (i < player.Orders.Count) scene.With(OrderCard(session, player.Orders[i], x, 434, true, now, selected));
            else scene.With(EmptySlot(x, 434, SlotWidth, SlotHeight, i > 0 || player.Orders.Count > 0 ? "frei" : "Keine laufenden Aufträge."));
        }

        scene.With(ExportPanel(session));
        scene.With(FarmGatePanel(state, player));
        scene.With(selected is null ? OrdersIntro(player) : OrderInspector(session, selected, player.Orders.Contains(selected), now));
    }

    static string WishText(CustomerOrder order)
    {
        var text = $"{Util.FormatNumber(order.Bottles)} Flaschen {Orders.DescribeWine(order)}";
        if (order.BioLabel is { } label) text += $" · mit Bio-Siegel „{BioData.BioLabels[label].Name}“ oder höher";
        return text;
    }

    /// <summary>Ländername nach „aus“ und „mit“: „die USA“ → „den USA“, „die Niederlande“ → „den Niederlanden“</summary>
    static string Dative(string country)
    {
        if (!country.StartsWith("die ")) return country;
        var name = country[4..];
        return "den " + name + (name.EndsWith('e') ? "n" : "");
    }

    static (string Text, bool Urgent) TimeText(CustomerOrder order, int now)
    {
        var left = order.Deadline - now;
        return left <= 0 ? ("Letzter Monat zum Liefern!", true) : ($"Noch {left + 1} Monate Zeit.", left <= 1);
    }

    static SkiaControl OrderCard(GameSession session, CustomerOrder order, double x, double y, bool accepted, int now, CustomerOrder? selected)
    {
        var player = session.Player;
        var available = Orders.EligibleBottles(player, order);
        var full = available >= order.Bottles;
        var card = Kit.Card(x, y, SlotWidth, SlotHeight, 16);
        var (time, urgent) = TimeText(order, now);
        if (accepted && urgent) card.With(Kit.Box(0, 0, 10, SlotHeight, Theme.Alert, 5));
        card.With(Kit.Text($"{order.Icon} {order.Customer}", 22, 14, SlotWidth - 44, 30, 20, Theme.Ink, bold: true));
        card.With(Chip($"+{Util.FormatNumber(order.Points)} Punkte", SlotWidth - 126, 147, 104, Theme.PaperLine, Theme.Ink, 14));
        card.With(Kit.Text(WishText(order), 22, 50, SlotWidth - 44, 70, 17, Theme.Ink, maxLines: 3));
        card.With(Kit.Text($"{Price(order.PricePerBottle)}/Fl. · zusammen {Util.FormatEuro(Orders.OrderValue(order))}", 22, 124, SlotWidth - 44, 26, 17, Theme.Wine, bold: true));
        card.With(Kit.Text($"Lieferung bis {Orders.DeadlineText(order)}", 22, 150, SlotWidth - 156, 26, 16, Theme.InkSoft));
        card.With(Meter(22, 184, SlotWidth - 44, available / order.Bottles, full ? Theme.Good : Theme.Wine));
        card.With(Kit.Text($"Passend im Lager: {Util.FormatNumber(available)} / {Util.FormatNumber(order.Bottles)} Fl.", 22, 199, SlotWidth - 44, 24, 16, full ? Theme.Good : Theme.InkSoft, bold: full));
        card.With(accepted
            ? Kit.Text(time, 22, 226, SlotWidth - 44, 24, 16, urgent ? Theme.Alert : Theme.InkSoft, bold: urgent)
            : Kit.Text($"Die Anfrage gilt bis Ende {Orders.MonthText(order.OfferExpires)}.", 22, 226, SlotWidth - 44, 24, 16, Theme.InkSoft));
        card.With(SelectionFrame(order == selected, SlotWidth, SlotHeight));
        var id = order.Id;
        card.Tap(() => session.Nav.Select("markt.order", id));
        return card;
    }

    static SkiaControl OrderInspector(GameSession session, CustomerOrder order, bool accepted, int now)
    {
        var player = session.Player;
        var value = Orders.OrderValue(order);
        var penalty = Js.Round(value * Orders.OrderPenaltyShare);
        var eligible = Orders.OrderEligible(player, order);
        var available = Orders.EligibleBottles(player, order);
        var full = available >= order.Bottles;
        var (time, urgent) = TimeText(order, now);

        var card = Inspector();
        card.With(Kit.Text(accepted ? "ANGENOMMENER AUFTRAG" : "ANFRAGE", 32, 28, 460, 24, 16, Theme.Wine, bold: true));
        card.With(Kit.Text($"{order.Icon} {order.Customer}", 32, 52, 460, 44, 30, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Text(WishText(order), 32, 100, 460, 78, 19, Theme.Ink, maxLines: 3));

        var y = 190.0;
        void Row(string label, string text, Color? color = null)
        {
            card.With(Kit.Row(label, text, 32, y, 460, color, 19));
            y += 34;
        }
        Row("Menge", $"{Util.FormatNumber(order.Bottles)} Flaschen");
        Row("Preis je Flasche", Price(order.PricePerBottle));
        Row("Auftragswert", Util.FormatEuro(value), Theme.Wine);
        Row("Lohn", $"+{Util.FormatNumber(order.Points)} Punkte · +{Util.FormatNumber(order.Reputation)} Bekanntheit");
        Row("Liefertermin", Orders.DeadlineText(order));
        if (accepted) Row("Zeit", time, urgent ? Theme.Alert : null);
        else Row("Anfrage gilt bis", $"Ende {Orders.MonthText(order.OfferExpires)}");
        Row("Strafe bei Verzug", Util.FormatEuro(penalty), Theme.Alert);

        card.With(Rule(436));
        card.With(Kit.Text("Passender Vorrat", 32, 450, 300, 30, 20, Theme.Ink, bold: true));
        card.With(Kit.Text($"{Util.FormatNumber(available)} / {Util.FormatNumber(order.Bottles)} Fl.", 232, 452, 260, 28, 19, full ? Theme.Good : Theme.Wine, bold: true, align: Align.End));
        card.With(Meter(32, 490, 460, available / order.Bottles, full ? Theme.Good : Theme.Wine, h: 12));
        var ly = 514.0;
        if (eligible.Count == 0)
        {
            var why = order.BioLabel is not null && player.BioLabel is null
                ? "Der Kunde verlangt ein Bio-Siegel – das Weingut trägt keins."
                : "Kein geprüfter, fertiger Wein im Lager erfüllt den Wunsch. Rosé zählt nicht als Ersatz.";
            card.With(Kit.Text(why, 32, ly, 460, 50, 17, Theme.InkSoft, maxLines: 2));
        }
        foreach (var lot in eligible.Take(3))
        {
            card.With(Kit.Box(32, ly + 9, 10, 10, Theme.Wine, 5));
            card.With(Kit.Text($"{GrapeName(lot)} {KindName(lot)} (Jg. {lot.Vintage})", 52, ly, 280, 26, 17, Theme.Ink));
            card.With(Kit.Text($"{Util.FormatNumber(lot.Bottles)} Fl. · Güte {Util.FormatNumber(lot.Quality)}", 300, ly, 192, 26, 17, Theme.InkSoft, align: Align.End));
            ly += 30;
        }
        if (eligible.Count > 3) card.With(Kit.Text($"… und {eligible.Count - 3} weitere Posten", 52, ly, 440, 26, 16, Theme.InkSoft));
        card.With(Kit.Text("Geliefert werden die einfachsten passenden Weine zuerst – die besten bleiben für den Hofladen.",
            32, 640, 460, 50, 16, Theme.InkSoft, maxLines: 2));

        var id = order.Id;
        if (accepted)
        {
            var deliver = new GameAction.DeliverOrder(id);
            card.With(Kit.Button("Jetzt liefern", 32, 832, 460, 84, () => session.Run(deliver), ButtonKind.Primary,
                $"+{Util.FormatEuro(value)} · +{Util.FormatNumber(order.Points)} Punkte · +{Util.FormatNumber(order.Reputation)} Bekanntheit", session.Problem(deliver)));
            card.With(Kit.Text(full ? "Alles da – die Lieferung kann sofort hinaus." : $"Es fehlen noch {Util.FormatNumber(order.Bottles - available)} passende Flaschen: abfüllen und prüfen lassen.",
                32, 772, 460, 50, 17, full ? Theme.Good : Theme.InkSoft, bold: full, maxLines: 2));
        }
        else
        {
            var accept = new GameAction.AcceptOrder(id);
            var decline = new GameAction.DeclineOrder(id);
            card.With(Kit.Button("Annehmen", 32, 736, 460, 84, () => session.Run(accept), ButtonKind.Primary,
                $"Liefern bis {Orders.DeadlineText(order)} · sonst {Util.FormatEuro(penalty)} Strafe", session.Problem(accept)));
            card.With(Kit.Button("Ablehnen", 32, 832, 460, 84, () => session.Run(decline), ButtonKind.Secondary,
                "Die Anfrage verfällt ohne Folgen", session.Problem(decline)));
        }
        return card;
    }

    /// <summary>Inspektor ohne Auftrag: Worum es hier geht</summary>
    static SkiaControl OrdersIntro(Player player)
    {
        var card = Inspector();
        card.With(Kit.Text("Aufträge", 32, 32, 460, 46, 32, Theme.Ink, bold: true, serif: true));
        card.With(Kit.Text($"{player.Orders.Count} / {Orders.MaxOrders} Aufträge angenommen", 32, 78, 460, 28, 19, Theme.InkSoft));
        card.With(Kit.Text(
            $"Kunden bestellen größere Mengen eines bestimmten Weins bis zu einem Termin – besser bezahlt als im Hofladen, dazu Siegpunkte. Wer den Termin verpasst, zahlt {Js.Round(Orders.OrderPenaltyShare * 100)} % des Auftragswerts Vertragsstrafe.",
            32, 130, 460, 160, 19, Theme.Ink, maxLines: 6));
        card.With(Rule(306));
        card.With(Kit.Row("Bisher geliefert", $"{player.Stats.OrdersDelivered} {(player.Stats.OrdersDelivered == 1 ? "Auftrag" : "Aufträge")}", 32, 324, 460, size: 19));
        card.With(Kit.Row("Exportverträge erfüllt", player.Stats.ExportsCompleted.ToString(), 32, 358, 460, size: 19));
        card.With(Kit.Row("Flaschen verkauft", Util.FormatNumber(player.Stats.BottlesSold), 32, 392, 460, size: 19));
        card.With(Rule(440));
        card.With(Kit.Text("Sobald eine Anfrage vorliegt, steht sie links. Antippen zeigt hier die Einzelheiten – annehmen, ablehnen oder liefern.",
            32, 458, 460, 110, 18, Theme.InkSoft, maxLines: 4));
        return card;
    }

    static double ExportStock(Player player, ExportOffer offer)
    {
        var sum = 0.0;
        foreach (var lot in Exports.ExportEligible(player, offer)) sum += lot.Bottles;
        return sum;
    }

    static SkiaControl ExportPanel(GameSession session)
    {
        var player = session.Player;
        var panel = Kit.Glass(28, 716, 790, 244);
        panel.With(Kit.Text("Export", 24, 14, 300, 32, 22, Theme.Gold, bold: true, serif: true));
        var terms = player.ExportOffer ?? player.ExportContract;
        if (terms is null)
        {
            panel.With(Kit.Text("Keine Anfrage aus dem Ausland", 24, 52, 740, 32, 21, Theme.Cream, bold: true));
            var known = player.Reputation >= Economy.ExportMinReputation;
            panel.With(Kit.Text(
                $"Importeure melden sich gelegentlich bei Weingütern ab Bekanntheit {Util.FormatNumber(Economy.ExportMinReputation)} (aktuell {Util.FormatNumber(Js.Round(player.Reputation))}{(known ? "" : " – noch zu unbekannt")}). " +
                $"Ein Vertrag läuft zwölf Monate: Jeden Monat geht eine feste Menge automatisch hinaus. Fehlende Flaschen kosten {Util.FormatEuro(Economy.ExportPenaltyPerBottle)} Strafe je Flasche; wer vollständig liefert, bekommt +{Util.FormatNumber(Economy.ExportCompletionPoints)} Punkte.",
                24, 92, 742, 130, 18, Theme.CreamSoft, maxLines: 5));
            return panel;
        }

        var stock = ExportStock(player, terms);
        var enough = stock >= terms.BottlesPerMonth;
        var contract = player.ExportContract;
        panel.With(Kit.Text(contract is null ? $"Anfrage aus {Dative(terms.Country)} · {terms.Months} Monate" : $"Vertrag mit {Dative(terms.Country)}", 24, 48, 520, 32, 22, Theme.Cream, bold: true));
        panel.With(Kit.Text($"{Util.FormatNumber(terms.BottlesPerMonth)} Flaschen pro Monat zu {Price(terms.PricePerBottle)} · mindestens {Quality.QualityInfo(terms.MinLevel).Name}, Güte ab {Util.FormatNumber(terms.MinQuality)}",
            24, 84, 520, 52, 18, Theme.Cream, maxLines: 2));
        panel.With(Kit.Text($"Passender Vorrat: {Util.FormatNumber(stock)} Fl. – {(enough ? $"reicht für {Util.FormatNumber(Math.Floor(stock / terms.BottlesPerMonth))} {(Math.Floor(stock / terms.BottlesPerMonth) == 1 ? "Monat" : "Monate")}" : "zu wenig für eine Monatslieferung")}",
            24, 138, 520, 26, 18, enough ? Theme.GoodLight : Theme.Warn, bold: true));

        if (contract is null)
        {
            panel.With(Kit.Text(
                $"Geliefert wird automatisch am Monatsende. Fehlende Flaschen kosten {Util.FormatEuro(Economy.ExportPenaltyPerBottle)} Strafe je Flasche; wer ein Jahr lang vollständig liefert, bekommt +{Util.FormatNumber(Economy.ExportCompletionPoints)} Punkte. Die Anfrage gilt nur diesen Monat.",
                24, 170, 742, 66, 16, Theme.CreamSoft, maxLines: 3));
            var accept = new GameAction.AnswerExport(true);
            var decline = new GameAction.AnswerExport(false);
            panel.With(Kit.Button("Annehmen", 566, 16, 200, 68, () => session.Run(accept), ButtonKind.Gold,
                $"{Util.FormatEuro(Js.Round(terms.BottlesPerMonth * terms.PricePerBottle))} im Monat", session.Problem(accept), 20, Align.Center));
            panel.With(Kit.Button("Ablehnen", 566, 94, 200, 68, () => session.Run(decline), ButtonKind.Dark,
                "ohne Folgen", session.Problem(decline), 20, Align.Center));
        }
        else
        {
            var done = contract.Months - contract.MonthsLeft;
            panel.With(Kit.Paint(606, 20, 130, 130, (c, w, h) =>
                Draw.Ring(c, 65, 65, 52, 13, done / (double)Math.Max(1, contract.Months), Theme.SkCream.WithAlpha(50), Theme.SkGold)));
            panel.With(Kit.Text(contract.MonthsLeft.ToString(), 606, 52, 130, 44, 34, Theme.Cream, bold: true, serif: true, align: Align.Center));
            panel.With(Kit.Text(contract.MonthsLeft == 1 ? "Monat noch" : "Monate noch", 606, 92, 130, 22, 15, Theme.CreamSoft, align: Align.Center));
            panel.With(Kit.Text($"bringt {Util.FormatEuro(Js.Round(terms.BottlesPerMonth * terms.PricePerBottle))} im Monat", 566, 158, 210, 24, 16, Theme.CreamSoft, align: Align.Center));
            panel.With(contract.MissedMonths > 0
                ? Kit.Text($"{contract.MissedMonths} {(contract.MissedMonths == 1 ? "Monat" : "Monate")} mit Fehlmengen – der Punktebonus ist verloren. Fehlende Flaschen kosten {Util.FormatEuro(Economy.ExportPenaltyPerBottle)} Strafe je Flasche.",
                    24, 176, 742, 50, 17, Theme.Warn, bold: true, maxLines: 2)
                : Kit.Text($"Geliefert wird automatisch am Monatsende, vor dem Hofverkauf. Bisher vollständig – am Ende winken +{Util.FormatNumber(Economy.ExportCompletionPoints)} Punkte; fehlende Flaschen kosten {Util.FormatEuro(Economy.ExportPenaltyPerBottle)} je Flasche.",
                    24, 176, 742, 50, 17, Theme.CreamSoft, maxLines: 2));
        }
        return panel;
    }

    /// <summary>
    /// Vorschau auf den Hofverkauf am Monatsende – dieselbe Auswahl wie beim Monatsabschluss: fertige Weine,
    /// die nicht auf die Prüfung warten und nicht zur Prämierung angemeldet sind, älteste zuerst.
    /// </summary>
    static (double Ready, double Sold, double Net) FarmGate(GameState state, Player player)
    {
        var demand = Live.Demand(player);
        double ready = 0, sold = 0, revenue = 0;
        foreach (var lot in player.Bottles)
        {
            var waits = !lot.Registered && (lot.Sparkling || lot.Level != QualityLevel.DeutscherWein) && lot.Bottles >= Economy.RegisterMinBottles;
            if (player.CompetitionEntries.Contains(lot.Id) || lot.MonthsUntilReady > 0 || waits) continue;
            ready += lot.Bottles;
            var count = Math.Min(Math.Max(0, demand - sold), lot.Bottles);
            sold += count;
            revenue += count * Market.BottlePrice(lot, player, state);
        }
        return (ready, sold, Js.Round(revenue * (1 - Economy.DirectSalesCostShare)));
    }

    static SkiaControl FarmGatePanel(GameState state, Player player)
    {
        var panel = Kit.Glass(834, 716, 396, 244);
        var demand = Live.Demand(player);
        var (ready, sold, net) = FarmGate(state, player);
        var channels = "Hofladen" + (player.HasOnlineShop ? ", Online-Shop" : "") + (player.HasVinothek ? ", Vinothek" : "");
        panel.With(Kit.Text("Ab Hof diesen Monat", 24, 14, 348, 32, 22, Theme.Gold, bold: true, serif: true));
        panel.With(Kit.Text($"≈ {Util.FormatNumber(demand)} Flaschen", 24, 48, 348, 44, 32, Theme.Cream, bold: true, serif: true));
        panel.With(Kit.Text($"Nachfrage über {channels}", 24, 92, 348, 24, 16, Theme.CreamSoft));
        panel.With(Meter(24, 128, 348, demand > 0 ? ready / demand : 0, ready >= demand ? Theme.GoodLight : Theme.Warn, Theme.GlassLine));
        panel.With(DarkRow("Verkaufsbereit", $"{Util.FormatNumber(ready)} Fl.", 24, 146, 348, ready >= demand ? Theme.GoodLight : Theme.Warn, 17));
        panel.With(DarkRow("Voraussichtlich", sold > 0 ? $"{Util.FormatNumber(sold)} Fl. · +{Util.FormatEuro(net)}" : "kein Verkauf", 24, 174, 348, size: 17));
        panel.With(Kit.Text("Verkauf am Monatsende, nach Vertriebskosten.", 24, 206, 348, 24, 15, Theme.CreamSoft));
        return panel;
    }
}
