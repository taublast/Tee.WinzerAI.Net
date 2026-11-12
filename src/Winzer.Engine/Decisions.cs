using System.Text.RegularExpressions;
using Winzer.Engine.Data;

namespace Winzer.Engine;

// Entscheidungsereignisse: Statt einfach einzutreten, stellen sie das Weingut vor eine Wahl – meist mit Risiko
// oder Preis. Die Entscheidung gilt bis Monatsende, danach greift die vorsichtige Wahl. Computergegner entscheiden sofort.

/// <summary>Ergebnis einer Entscheidung: Meldung und Tonfall</summary>
public sealed record Outcome(string Text, LogTone? Tone = null);

public sealed class DecisionDef
{
    public required string Id { get; init; }
    public required Rarity Rarity { get; init; }
    public int[]? Months { get; init; }
    public Func<EventContext, bool>? When { get; init; }
    /// <summary>Baut die Entscheidung mit ausgewürfelten Werten (ohne <c>EventId</c>)</summary>
    public required Func<EventContext, Decision> Offer { get; init; }
    /// <summary>Wahl eines Computergegners</summary>
    public required Func<EventContext, Decision, int> Ai { get; init; }
    public required Func<EventContext, int, Decision, Outcome> Resolve { get; init; }
}

public static class Decisions
{
    static DecisionOption Option(string label, string hint, double cost = 0) => new() { Label = label, Hint = hint, Cost = cost };
    static DecisionOption Decline(string hint = "Es passiert nichts.") => Option("Ablehnen", hint);

    /// <summary>Kann der Betrag über Konto und freien Kreditrahmen bezahlt werden?</summary>
    public static bool Affordable(Player player, double amount) => Math.Max(0, player.Money) + Market.AvailableCredit(player) >= amount;

    /// <summary>Nur ausgeben, was die Rücklage nicht angreift – Maßstab der Computergegner</summary>
    static bool Comfortable(Player player, double amount) => player.Money - amount > 20_000;

    /// <summary>Bezahlt; ein Fehlbetrag wird automatisch über den Kredit gedeckt.</summary>
    static string Pay(EventContext ctx, double amount)
    {
        var player = ctx.Player;
        player.Money -= amount;
        if (player.Money < 0)
        {
            var loan = Math.Min(-player.Money, Market.AvailableCredit(player));
            player.Loan += loan;
            player.Money += loan;
            if (loan > 0) return $"{Util.FormatEuro(amount)}, davon {Util.FormatEuro(loan)} per Kredit";
        }
        return Util.FormatEuro(amount);
    }

    /// <summary>Erwarteter Erlös für Flaschen zum angegebenen Anteil des Ab-Hof-Preises, älteste zuerst</summary>
    static double ExpectedRevenue(EventContext ctx, double bottles, double share)
    {
        var left = bottles;
        var revenue = 0.0;
        foreach (var lot in Events.SellableLots(ctx))
        {
            var n = Math.Min(left, lot.Bottles);
            revenue += n * Market.BottlePrice(lot, ctx.Player, ctx.State) * share;
            left -= n;
            if (left <= 0) break;
        }
        return Js.Round(revenue);
    }

    /// <summary>Ein Computer-Nachbar im selben Gebiet, der nicht verfeindet ist – der freundlichste zuerst</summary>
    static Player? Neighbour(EventContext ctx, double minRelation = -10)
    {
        if (ctx.Player.Ai is not null) return null;
        return ctx.State.Players
            .Where(p => p.Ai is not null && !p.Bankrupt && p.RegionId == ctx.Player.RegionId && Rivals.Relation(p, ctx.Player.Id) >= minRelation)
            .OrderByDescending(p => Rivals.Relation(p, ctx.Player.Id))
            .FirstOrDefault();
    }

    static Player PartnerOf(EventContext ctx, Decision d) => ctx.State.Players[(int)d.Params["partner"]];

    /// <summary>Fertiger Wein im Tank, den ein Nachbar kaufen könnte</summary>
    static WineBatch? SellableBatch(EventContext ctx) =>
        ctx.Player.Cellar
            .Where(b => !b.Barrique && b.MonthsUntilReady == 0 && b.Hectoliters >= 20)
            .OrderByDescending(b => b.Hectoliters)
            .FirstOrDefault();

    static double BearingHa(EventContext ctx)
    {
        var sum = 0.0;
        foreach (var p in Events.BearingPlots(ctx)) sum += p.Hectares;
        return sum;
    }

    static readonly GrapeId[] GrapeIds = [.. Grapes.All.Keys];

    /// <summary>Titel ohne das führende Symbol (<c>title.replace(/^\S+\s/, '')</c>)</summary>
    static string PlainTitle(string title) => Regex.Replace(title, @"^\S+\s", "");

    public static readonly List<DecisionDef> DecisionEvents =
    [
        // ── Alltag ───────────────────────────────────────────────────────────────────
        new()
        {
            Id = "stadtweinfest", Rarity = Rarity.Alltag, Months = [5, 6, 7, 8],
            Offer = c =>
            {
                var rent = c.Between(1500, 2500, 100);
                return new Decision
                {
                    Title = "🎪 Stand auf dem Stadtweinfest",
                    Text = $"Das große Weinfest in der Kreisstadt sucht noch Winzer. Die Standmiete beträgt {Util.FormatEuro(rent)}.",
                    Options = [Option("Stand mieten", "+3 Bekanntheit, nächsten Monat 40 % mehr Nachfrage", rent), Decline()],
                    Fallback = 1,
                    Params = new() { ["rent"] = rent },
                };
            },
            Ai = (c, d) => Comfortable(c.Player, d.Params["rent"]) ? 0 : 1,
            Resolve = (c, choice, d) =>
            {
                if (choice == 1) return new Outcome("Du verzichtest auf einen Stand beim Stadtweinfest.");
                var paid = Pay(c, d.Params["rent"]);
                Events.AddEffect(c.Player, EffectKind.Demand, 1.4, 1, "Nach dem Stadtweinfest");
                return new Outcome($"Dein Stand auf dem Stadtweinfest ist gut besucht ({paid}, {Events.Reputation(c, 3)}).", LogTone.Good);
            },
        },
        new()
        {
            Id = "vereinsfest", Rarity = Rarity.Alltag, When = c => Events.BottleCount(Events.SellableLots(c)) >= 60,
            Offer = _ => new Decision
            {
                Title = "⚽ Spende fürs Vereinsfest",
                Text = "Der Sportverein im Ort bittet um 60 Flaschen Wein für sein Sommerfest – als Spende.",
                Options = [Option("Spenden", "60 Flaschen weniger, +1,5 Bekanntheit"), Option("Ablehnen", "Im Ort wird getuschelt: −0,5 Bekanntheit")],
                Fallback = 1,
                Params = [],
            },
            Ai = (_, _) => 0,
            Resolve = (c, choice, _) =>
            {
                if (choice == 1) return new Outcome($"Keine Spende für den Sportverein ({Events.Reputation(c, -0.5)}).", LogTone.Bad);
                Events.SellBottles(c, Events.SellableLots(c), 60, 0);
                return new Outcome($"Der Sportverein bedankt sich für 60 Flaschen ({Events.Reputation(c, 1.5)}).", LogTone.Good);
            },
        },
        new()
        {
            Id = "influencer-werbung", Rarity = Rarity.Alltag,
            Offer = c =>
            {
                var fee = c.Between(800, 1500, 100);
                return new Decision
                {
                    Title = "📱 Bezahlte Werbung",
                    Text = $"Eine Wein-Influencerin bietet dir einen gesponserten Beitrag für {Util.FormatEuro(fee)} an.",
                    Options = [Option("Buchen", "Meist: +3 Bekanntheit und 2 Monate 25 % mehr Nachfrage – manchmal verpufft es", fee), Decline()],
                    Fallback = 1,
                    Params = new() { ["fee"] = fee },
                };
            },
            Ai = (c, d) => Comfortable(c.Player, d.Params["fee"]) && c.Player.Reputation < 40 ? 0 : 1,
            Resolve = (c, choice, d) =>
            {
                if (choice == 1) return new Outcome("Du lehnst die bezahlte Werbung ab.");
                var paid = Pay(c, d.Params["fee"]);
                if (c.Roll() < 0.7)
                {
                    Events.AddEffect(c.Player, EffectKind.Demand, 1.25, 2, "Gesponserter Beitrag");
                    return new Outcome($"Der gesponserte Beitrag kommt gut an ({paid}, {Events.Reputation(c, 3)}).", LogTone.Good);
                }
                return new Outcome($"Der gesponserte Beitrag verpufft – kaum jemand reagiert ({paid}).", LogTone.Bad);
            },
        },

        // ── Gelegentlich ─────────────────────────────────────────────────────────────
        new()
        {
            Id = "discounter", Rarity = Rarity.Gelegentlich, When = c => Events.BottleCount(Events.SellableLots(c)) >= 1000,
            Offer = c =>
            {
                var bottles = Math.Min(Events.BottleCount(Events.SellableLots(c)), c.Between(800, 2000, 100));
                var revenue = ExpectedRevenue(c, bottles, 0.5);
                return new Decision
                {
                    Title = "🛒 Großauftrag vom Discounter",
                    Text = $"Eine Discounter-Kette will {Util.FormatNumber(bottles)} Flaschen auf einen Schlag – zum halben Ab-Hof-Preis.",
                    Options = [Option("Verkaufen", $"rund +{Util.FormatEuro(revenue)} sofort, aber −3 Bekanntheit (Ramsch-Image)"), Decline()],
                    Fallback = 1,
                    Params = new() { ["bottles"] = bottles },
                };
            },
            Ai = (c, _) => c.Player.Money < 20_000 ? 0 : 1,
            Resolve = (c, choice, d) =>
            {
                if (choice == 1) return new Outcome("Du lehnst den Großauftrag des Discounters ab.");
                var (sold, revenue) = Events.SellBottles(c, Events.SellableLots(c), d.Params["bottles"], 0.5);
                return new Outcome($"{Util.JsNumber(sold)} Flaschen gehen an den Discounter: +{Util.FormatEuro(revenue)} ({Events.Reputation(c, -3)}).", LogTone.Good);
            },
        },
        new()
        {
            Id = "journalist", Rarity = Rarity.Gelegentlich,
            Offer = c =>
            {
                var cost = c.Between(1500, 3000, 100);
                return new Decision
                {
                    Title = "📰 Reportage im Weinmagazin",
                    Text = $"Ein Journalist eines großen Weinmagazins möchte über dein Gut schreiben. Bewirtung und Zeitaufwand kosten rund {Util.FormatEuro(cost)}.",
                    Options =
                    [
                        Option("Einladen", "Meist ein begeisterter Artikel (+6 Bekanntheit, mehr Nachfrage) – manchmal ein Verriss (−3)", cost),
                        Option("Absagen", "Es passiert nichts."),
                    ],
                    Fallback = 1,
                    Params = new() { ["cost"] = cost },
                };
            },
            Ai = (c, d) => Comfortable(c.Player, d.Params["cost"] * 3) ? 0 : 1,
            Resolve = (c, choice, d) =>
            {
                if (choice == 1) return new Outcome("Du sagst dem Journalisten ab.");
                var paid = Pay(c, d.Params["cost"]);
                if (c.Roll() < 0.7)
                {
                    Events.AddEffect(c.Player, EffectKind.Demand, 1.2, 2, "Reportage im Weinmagazin");
                    return new Outcome($"Die Reportage ist eine Hymne auf dein Gut ({paid}, {Events.Reputation(c, 6)}).", LogTone.Good);
                }
                return new Outcome($"Der Journalist verreißt deinen Wein ({paid}, {Events.Reputation(c, -3)}).", LogTone.Bad);
            },
        },
        new()
        {
            Id = "erntehelfer-lohn", Rarity = Rarity.Gelegentlich, Months = [8, 9], When = c => Events.HasBearing(c),
            Offer = c =>
            {
                var extra = Js.Round(BearingHa(c) * c.Between(200, 400, 10) / 100) * 100;
                return new Decision
                {
                    Title = "✊ Erntehelfer wollen mehr Lohn",
                    Text = $"Kurz vor der Lese fordern die Erntehelfer einen Zuschlag von {Util.FormatEuro(extra)}.",
                    Options =
                    [
                        Option("Zahlen", "Die Lese läuft wie geplant.", extra),
                        Option("Ablehnen", "Einige reisen ab – rund 15 % weniger Ertrag auf allen tragenden Lagen"),
                    ],
                    Fallback = 1,
                    Params = new() { ["extra"] = extra },
                };
            },
            Ai = (c, d) => Affordable(c.Player, d.Params["extra"]) ? 0 : 1,
            Resolve = (c, choice, d) =>
            {
                if (choice == 0) return new Outcome($"Du zahlst den Erntehelfern einen Zuschlag ({Pay(c, d.Params["extra"])}).");
                foreach (var plot in Events.BearingPlots(c)) plot.CropFactor *= 0.85;
                return new Outcome("Ein Teil der Erntehelfer reist ab – rund 15 % der Trauben bleiben hängen.", LogTone.Bad);
            },
        },
        new()
        {
            Id = "gebrauchte-tanks", Rarity = Rarity.Gelegentlich,
            Offer = c =>
            {
                var hl = c.Between(100, 200, 50);
                var price = Js.Round(hl * Economy.TankCostPerHl * 0.5 / 100) * 100;
                return new Decision
                {
                    Title = "🛢️ Gebrauchte Tanks günstig",
                    Text = $"Ein Weingut gibt auf und verkauft {Util.JsNumber(hl)} hl Edelstahltanks für {Util.FormatEuro(price)} – halb so viel wie neu.",
                    Options = [Option("Kaufen", $"+{Util.JsNumber(hl)} hl Tankkapazität", price), Decline()],
                    Fallback = 1,
                    Params = new() { ["hl"] = hl, ["price"] = price },
                };
            },
            Ai = (c, d) => Rules.UsedTankHl(c.Player) > c.Player.TankCapacityHl * 0.7 && Comfortable(c.Player, d.Params["price"]) ? 0 : 1,
            Resolve = (c, choice, d) =>
            {
                if (choice == 1) return new Outcome("Du lässt die gebrauchten Tanks stehen.");
                var paid = Pay(c, d.Params["price"]);
                c.Player.TankCapacityHl += d.Params["hl"];
                return new Outcome($"Schnäppchen: {Util.JsNumber(d.Params["hl"])} hl gebrauchte Tanks gekauft ({paid}).", LogTone.Good);
            },
        },

        // ── Nachbarschaft: Angebote freundlicher Computergegner ─────────────────────
        new()
        {
            Id = "kooperation-messestand", Rarity = Rarity.Gelegentlich, When = c => Neighbour(c) is not null,
            Offer = c =>
            {
                var partner = Neighbour(c)!;
                var share = c.Between(3000, 5000, 500);
                return new Decision
                {
                    Title = $"🤝 Gemeinsamer Messestand mit {partner.Name}",
                    Text = $"{partner.Name} schlägt vor, auf der Regionalmesse gemeinsam auszustellen. Dein Anteil: {Util.FormatEuro(share)}.",
                    Options =
                    [
                        Option("Mitmachen", "Beide +5 Bekanntheit, das Verhältnis zum Nachbarn wird besser", share),
                        Option("Ablehnen", "Der Nachbar ist ein wenig verstimmt"),
                    ],
                    Fallback = 1,
                    Params = new() { ["share"] = share, ["partner"] = c.State.Players.IndexOf(partner) },
                };
            },
            Ai = (_, _) => 1,
            Resolve = (c, choice, d) =>
            {
                var partner = PartnerOf(c, d);
                if (choice == 1)
                {
                    Rivals.AdjustRelation(partner, c.Player.Id, -5);
                    return new Outcome($"Du lehnst den gemeinsamen Messestand mit {partner.Name} ab.");
                }
                var paid = Pay(c, d.Params["share"]);
                Rules.ChangeReputation(partner, 5);
                Rivals.AdjustRelation(partner, c.Player.Id, 15);
                return new Outcome($"Der gemeinsame Messestand mit {partner.Name} ist ein Erfolg ({paid}, {Events.Reputation(c, 5)}).", LogTone.Good);
            },
        },
        new()
        {
            Id = "kooperation-weinkauf", Rarity = Rarity.Gelegentlich, Months = [3, 4, 5, 6, 7, 8],
            When = c => Neighbour(c) is not null && SellableBatch(c) is not null,
            Offer = c =>
            {
                var partner = Neighbour(c)!;
                var batch = SellableBatch(c)!;
                var hectoliters = Math.Min(batch.Hectoliters, c.Between(20, 40, 5));
                var price = Js.Round(Market.BulkPricePerHl(batch, c.State, c.Player) * hectoliters * 1.3);
                return new Decision
                {
                    Title = $"🍷 {partner.Name} braucht Wein",
                    Text = $"{partner.Name} hat zu wenig Wein für seine Kunden und möchte dir {Util.JsNumber(hectoliters)} hl {Rules.WineLabel(batch)} abkaufen – für {Util.FormatEuro(price)}, 30 % über dem Fassweinpreis.",
                    Options =
                    [
                        Option("Verkaufen", $"+{Util.FormatEuro(price)}, das Verhältnis zum Nachbarn wird besser"),
                        Option("Ablehnen", "Der Wein bleibt in deinem Tank"),
                    ],
                    Fallback = 1,
                    Params = new() { ["partner"] = c.State.Players.IndexOf(partner), ["hectoliters"] = hectoliters, ["price"] = price },
                };
            },
            Ai = (_, _) => 1,
            Resolve = (c, choice, d) =>
            {
                var partner = PartnerOf(c, d);
                var batch = SellableBatch(c);
                if (choice == 1 || batch is null) return new Outcome($"Du behältst deinen Wein – {partner.Name} sucht anderswo.");
                var hectoliters = Math.Min(batch.Hectoliters, d.Params["hectoliters"]);
                var label = Rules.WineLabel(batch);
                var bought = batch.Clone();
                bought.Id = Util.NewId(c.State, "wine");
                bought.Hectoliters = hectoliters;
                partner.Cellar.Add(bought);
                Rules.RemoveBatchVolume(c.Player, batch, hectoliters);
                c.Player.Money += d.Params["price"];
                Rivals.AdjustRelation(partner, c.Player.Id, 10);
                return new Outcome($"{Util.JsNumber(hectoliters)} hl {label} an {partner.Name} verkauft: +{Util.FormatEuro(d.Params["price"])}.", LogTone.Good);
            },
        },
        new()
        {
            Id = "erntehilfe", Rarity = Rarity.Selten, Months = [9, 10], When = c => Events.HasBearing(c) && Neighbour(c, 20) is not null,
            Offer = c =>
            {
                var partner = Neighbour(c, 20)!;
                return new Decision
                {
                    Title = $"🧺 Erntehilfe von {partner.Name}",
                    Text = $"{partner.Name} ist mit der Lese fertig und bietet dir seine erfahrenen Erntehelfer an – unentgeltlich, unter Nachbarn.",
                    Options = [Option("Gern annehmen", "Sorgfältige Handlese: +3 Güte für diesen Jahrgang"), Option("Danke, nein", "Es passiert nichts")],
                    Fallback = 1,
                    Params = new() { ["partner"] = c.State.Players.IndexOf(partner) },
                };
            },
            Ai = (_, _) => 1,
            Resolve = (c, choice, d) =>
            {
                var partner = PartnerOf(c, d);
                if (choice == 1) return new Outcome($"Du dankst {partner.Name} für das Angebot.");
                Events.AddEffect(c.Player, EffectKind.Quality, 3, 13 - c.State.Month, $"Erntehilfe von {partner.Name}");
                Rivals.AdjustRelation(partner, c.Player.Id, 5);
                return new Outcome($"Die Erntehelfer von {partner.Name} lesen sorgfältig mit (+3 Güte für den Jahrgang).", LogTone.Good);
            },
        },

        // ── Selten ───────────────────────────────────────────────────────────────────
        new()
        {
            Id = "nachbarlage", Rarity = Rarity.Selten,
            Offer = c =>
            {
                var state = c.State;
                var player = c.Player;
                var hectares = c.Between(1, 3, 1);
                var site = c.Between(-1, 1, 1);
                var suitable = GrapeIds.Where(g => Regions.All[player.RegionId].Suitability[g] >= 0).ToArray();
                GrapeId? grape = c.Roll() < 0.7 ? suitable[(int)Math.Floor(c.Roll() * suitable.Length)] : null;
                int? plantedYear = grape is not null ? state.Year - (int)c.Between(5, 30, 1) : null;
                var estimate = Auction.ParcelValue(hectares, grape, plantedYear, site, player.RegionId, state.Year);
                var price = Js.Round(estimate * (0.85 + c.Roll() * 0.3) / 1000) * 1000;
                return new Decision
                {
                    Title = "🤝 Der Nachbar will verkaufen",
                    Text =
                        $"Dein Nachbar setzt sich zur Ruhe und bietet dir privat seine Lage an: {Util.JsNumber(hectares)} ha, {Auction.SiteLabels[site]}, " +
                        $"{(grape is { } g ? $"{Grapes.All[g].Name}, {state.Year - plantedYear!.Value} Jahre alt" : "Brache")}. Schätzwert {Util.FormatEuro(Js.Round(estimate / 1000) * 1000)}.",
                    Options = [Option("Kaufen", $"Für {Util.FormatEuro(price)} – ohne Versteigerung, Fehlbetrag per Kredit", price), Decline("Er verkauft an jemand anderen.")],
                    Fallback = 1,
                    Params = new()
                    {
                        ["hectares"] = hectares,
                        ["site"] = site,
                        ["grape"] = grape is { } id ? Array.IndexOf(GrapeIds, id) : -1,
                        ["plantedYear"] = plantedYear ?? -1,
                        ["price"] = price,
                    },
                };
            },
            Ai = (c, d) => Comfortable(c.Player, d.Params["price"]) ? 0 : 1,
            Resolve = (c, choice, d) =>
            {
                if (choice == 1) return new Outcome("Du lehnst das Angebot deines Nachbarn ab.");
                var player = c.Player;
                var state = c.State;
                var paid = Pay(c, d.Params["price"]);
                var taken = new HashSet<string>(player.Plots.Select(p => p.Name));
                var name = "Nachbarlage";
                for (var n = 2; taken.Contains(name); n++) name = $"Nachbarlage {n}";
                GrapeId? grape = d.Params["grape"] >= 0 ? GrapeIds[(int)d.Params["grape"]] : null;
                player.Plots.Add(new Plot
                {
                    Id = Util.NewId(state, "plot"), Name = name, Hectares = d.Params["hectares"], Grape = grape,
                    PlantedYear = grape is not null ? (int)d.Params["plantedYear"] : null,
                    Site = d.Params["site"], Oechsle = 0, Health = 1, CropFactor = 1, HarvestedThisYear = false, Botrytis = false, IceWineGamble = false,
                });
                return new Outcome($"Handschlag: Die Lage des Nachbarn ({Util.JsNumber(d.Params["hectares"])} ha) gehört jetzt dir ({paid}).", LogTone.Good);
            },
        },
        new()
        {
            Id = "kellermeister-abwerbung", Rarity = Rarity.Selten, When = c => c.Player.CellarMaster is not null,
            Offer = c =>
            {
                var master = c.Player.CellarMaster!;
                var bonus = Js.Round(master.Salary * c.Between(12, 20, 1) / 10 / 500) * 500;
                return new Decision
                {
                    Title = "🧑‍🔬 Dein Kellermeister wird abgeworben",
                    Text = $"Ein Großweingut will {master.Name} abwerben. Mit einer Prämie von {Util.FormatEuro(bonus)} würde {master.Name.Split(' ')[0]} bleiben.",
                    Options =
                    [
                        Option("Prämie zahlen", "Bleibt – mit allen Fähigkeiten und der Erfahrung.", bonus),
                        Option("Ziehen lassen", "Geht: kein Gehalt mehr, aber auch kein Güte-Zuschlag und keine Spezialweine mehr"),
                    ],
                    Fallback = 1,
                    Params = new() { ["bonus"] = bonus },
                };
            },
            Ai = (c, d) => Affordable(c.Player, d.Params["bonus"]) ? 0 : 1,
            Resolve = (c, choice, d) =>
            {
                var name = c.Player.CellarMaster?.Name ?? "Dein Kellermeister";
                if (choice == 0) return new Outcome($"{name} bleibt (Prämie {Pay(c, d.Params["bonus"])}).");
                c.Player.CellarMaster = null;
                return new Outcome($"{name} wechselt zur Konkurrenz.", LogTone.Bad);
            },
        },
        new()
        {
            Id = "fernsehkoch", Rarity = Rarity.Selten, When = c => Events.BottleCount(Events.SellableLots(c)) >= 600,
            Offer = c => new Decision
            {
                Title = "👨‍🍳 Auftritt in einer Kochsendung",
                Text = "Ein bekannter Fernsehkoch will deinen Wein in seiner Sendung vorstellen – wenn du 600 Flaschen zu 40 % des Preises lieferst.",
                Options =
                [
                    Option("Liefern", $"600 Fl. für rund {Util.FormatEuro(ExpectedRevenue(c, 600, 0.4))}, +8 Bekanntheit, drei Monate 40 % mehr Nachfrage"),
                    Decline(),
                ],
                Fallback = 1,
                Params = [],
            },
            Ai = (_, _) => 0,
            Resolve = (c, choice, _) =>
            {
                if (choice == 1) return new Outcome("Du lehnst den Auftritt in der Kochsendung ab.");
                var (_, revenue) = Events.SellBottles(c, Events.SellableLots(c), 600, 0.4);
                Events.AddEffect(c.Player, EffectKind.Demand, 1.4, 3, "Auftritt in der Kochsendung");
                return new Outcome($"Dein Wein glänzt in der Kochsendung (+{Util.FormatEuro(revenue)}, {Events.Reputation(c, 8)}).", LogTone.Good);
            },
        },
        new()
        {
            Id = "weinmesse-einladung", Rarity = Rarity.Selten,
            Offer = c =>
            {
                var fee = c.Between(6000, 10_000, 500);
                return new Decision
                {
                    Title = "🌐 Einladung zur internationalen Weinmesse",
                    Text = $"Du kannst auf dem Gemeinschaftsstand deines Gebiets ausstellen. Standanteil und Reise kosten {Util.FormatEuro(fee)}.",
                    Options = [Option("Teilnehmen", "+6 Bekanntheit, ein Jahr lang 5 % höhere Preise", fee), Decline()],
                    Fallback = 1,
                    Params = new() { ["fee"] = fee },
                };
            },
            Ai = (c, d) => Comfortable(c.Player, d.Params["fee"] * 2) ? 0 : 1,
            Resolve = (c, choice, d) =>
            {
                if (choice == 1) return new Outcome("Du verzichtest auf die internationale Weinmesse.");
                var paid = Pay(c, d.Params["fee"]);
                Events.AddEffect(c.Player, EffectKind.Price, 1.05, 12, "Kontakte von der Weinmesse");
                return new Outcome($"Auf der Weinmesse knüpfst du wertvolle Kontakte ({paid}, {Events.Reputation(c, 6)}).", LogTone.Good);
            },
        },
    ];

    // ── Reaktion auf Sabotage ──────────────────────────────────────────────────────
    // Kein Zufallsereignis: Sie wird angeboten, wenn ein Mensch sabotiert wurde, und gilt bis Ende des Folgemonats –
    // so kann das Opfer reagieren, auch wenn der Täter erst nach ihm am Zug war.

    static readonly SabotageKind[] SabotageKinds = [SabotageKind.Geruecht, SabotageKind.Weinberg];

    static Player CulpritOf(EventContext ctx, Decision d) => ctx.State.Players[(int)d.Params["culprit"]];

    /// <summary>Täter zahlt dem Opfer Schadensersatz (notfalls ins Minus)</summary>
    static void Compensate(EventContext ctx, Player culprit, double amount)
    {
        culprit.Money -= amount;
        ctx.Player.Money += amount;
    }

    static string Guard(EventContext ctx)
    {
        Events.AddEffect(ctx.Player, EffectKind.Guard, 1, Economy.GuardMonths, "Nachtwache im Weinberg");
        return $"Eine Nachtwache bewacht ab sofort {Economy.GuardMonths} Monate lang dein Gut ({Pay(ctx, Economy.GuardCost)}).";
    }

    static readonly DecisionDef SabotageReaction = new()
    {
        Id = "sabotage-reaktion",
        Rarity = Rarity.Selten,
        When = _ => false,
        Offer = _ => throw new InvalidOperationException("wird über offerSabotageReaction angeboten"),
        Ai = (_, d) => d.Fallback,
        Resolve = (c, choice, d) =>
        {
            var culprit = CulpritOf(c, d);
            var damage = d.Params["damage"];
            var known = d.Params["detected"] == 1;
            if (choice == 1) return new Outcome(Guard(c), LogTone.Good);
            if (choice == 2)
            {
                if (!known) return new Outcome("Du nimmst die Sabotage hin und blickst nach vorn.");
                return new Outcome($"Du verzichtest großmütig auf eine Klage gegen {culprit.Name} ({Events.Reputation(c, 2)}).", LogTone.Good);
            }
            if (known)
            {
                var lawyer = Pay(c, Economy.LawsuitCost);
                if (c.Roll() >= Economy.LawsuitSuccess) return new Outcome($"Die Klage gegen {culprit.Name} wird abgewiesen ({lawyer} Anwaltskosten).", LogTone.Bad);
                Compensate(c, culprit, damage);
                Util.AddLog(c.State, culprit.Id, $"{c.Player.Name} hat dich wegen der Sabotage verklagt: {Util.FormatEuro(damage)} Entschädigung.", LogTone.Bad);
                return new Outcome($"Das Gericht gibt dir recht: {culprit.Name} zahlt {Util.FormatEuro(damage)} Entschädigung ({lawyer} Anwaltskosten).", LogTone.Good);
            }
            var paid = Pay(c, Economy.DetectiveCost);
            if (c.Roll() >= Economy.DetectiveSuccess) return new Outcome($"Die Detektei findet keine verwertbare Spur ({paid}).", LogTone.Bad);
            culprit.Money -= Economy.SabotageFine;
            Rules.ChangeReputation(culprit, -Economy.SabotageReputation);
            Compensate(c, culprit, damage);
            Rivals.AdjustRelation(culprit, c.Player.Id, -20);
            Util.AddLog(
                c.State,
                null,
                $"Eine Detektei überführt {culprit.Name} der Sabotage gegen {c.Player.Name}: {Util.FormatEuro(Economy.SabotageFine)} Strafe und {Util.FormatEuro(damage)} Schadensersatz.");
            return new Outcome($"Die Detektei hat {culprit.Name} überführt – du erhältst {Util.FormatEuro(damage)} Schadensersatz ({paid}).", LogTone.Good);
        },
    };

    /// <summary>
    /// Ein Mensch wurde sabotiert: Er darf reagieren – Detektei oder Klage, Nachtwache oder hinnehmen.
    /// Eine noch offene Entscheidung wird dafür mit ihrer vorsichtigen Wahl abgeschlossen.
    /// </summary>
    public static void OfferSabotageReaction(GameState state, Player victim, Player culprit, SabotageKind kind, bool detected)
    {
        if (victim.Ai is not null) return; // Computergegner merken sich so etwas über ihr Verhältnis
        if (victim.Decision is not null) ExpireDecision(state, victim, true);
        // Erwischte Täter haben nichts angerichtet – eine Klage bringt dann nur eine Entschädigung für den Ärger
        var damage = detected ? Economy.SabotageDamages[kind] / 2 : Economy.SabotageDamages[kind];
        var what = kind == SabotageKind.Geruecht ? "Üble Gerüchte über dein Weingut" : "Beschädigte Rebstöcke";
        List<DecisionOption> options = detected
            ?
            [
                Option("Auf Entschädigung klagen", $"{Util.JsNumber(Js.Round(Economy.LawsuitSuccess * 100))} % Aussicht auf {Util.FormatEuro(damage)} von {culprit.Name}", Economy.LawsuitCost),
                Option("Nachtwache anheuern", $"{Economy.GuardMonths} Monate Schutz: Saboteure werden fast sicher erwischt", Economy.GuardCost),
                Option("Großmütig verzeihen", "+2 Bekanntheit"),
            ]
            :
            [
                Option(
                    "Detektei beauftragen",
                    $"{Util.JsNumber(Js.Round(Economy.DetectiveSuccess * 100))} % Aussicht, den Täter zu überführen: Strafe für ihn, {Util.FormatEuro(damage)} Schadensersatz für dich",
                    Economy.DetectiveCost),
                Option("Nachtwache anheuern", $"{Economy.GuardMonths} Monate Schutz: Saboteure werden fast sicher erwischt", Economy.GuardCost),
                Option("Hinnehmen", "Es passiert nichts."),
            ];
        victim.Decision = new Decision
        {
            EventId = SabotageReaction.Id,
            Title = "🕵️ Sabotage!",
            Text = detected
                ? $"{culprit.Name} wollte dir schaden ({(kind == SabotageKind.Geruecht ? "Gerüchte" : "Rebstöcke beschädigen")}), wurde aber erwischt und muss Strafe zahlen. Willst du nachlegen?"
                : $"{what} – jemand will dir schaden. Wer, ist unklar. Wie reagierst du?",
            Options = options,
            Fallback = 2,
            Params = new()
            {
                ["culprit"] = state.Players.IndexOf(culprit),
                ["kind"] = Array.IndexOf(SabotageKinds, kind),
                ["damage"] = damage,
                ["detected"] = detected ? 1 : 0,
            },
            Deadline = (state.Year - 1) * 12 + state.Month + 1,
        };
        Util.AddLog(state, victim.Id, "Entscheidung gefragt: Reaktion auf die Sabotage.", LogTone.Bad);
    }

    /// <summary>Entscheidungen, die gerade angeboten werden können</summary>
    public static List<DecisionDef> PossibleDecisions(GameState state, Player player)
    {
        if (player.Decision is not null) return [];
        var ctx = Events.Context(state, player);
        var result = new List<DecisionDef>();
        foreach (var d in DecisionEvents)
        {
            if ((d.Months is null || d.Months.Contains(state.Month)) && (d.When is null || d.When(ctx))) result.Add(d);
        }
        return result;
    }

    /// <summary>Bietet eine Entscheidung an; Computergegner entscheiden sofort.</summary>
    public static void OfferDecision(GameState state, Player player, DecisionDef def)
    {
        var ctx = Events.Context(state, player);
        var decision = def.Offer(ctx);
        decision.EventId = def.Id;
        if (player.Ai is not null)
        {
            var choice = def.Ai(ctx, decision);
            if (!Affordable(player, decision.Options[choice].Cost)) choice = decision.Fallback;
            ApplyDecision(state, player, decision, choice);
            return;
        }
        player.Decision = decision;
        Util.AddLog(state, player.Id, $"Entscheidung gefragt: {PlainTitle(decision.Title)}.");
    }

    static void ApplyDecision(GameState state, Player player, Decision decision, int choice)
    {
        var def = DecisionEvents.Append(SabotageReaction).First(d => d.Id == decision.EventId);
        var outcome = def.Resolve(Events.Context(state, player), choice, decision);
        Util.AddLog(state, player.Id, outcome.Text, outcome.Tone);
    }

    public static string? ValidateDecide(Player player, int choice)
    {
        var decision = player.Decision;
        if (decision is null) return "Es steht keine Entscheidung an.";
        if (choice < 0 || choice >= decision.Options.Count) return "Ungültige Wahl.";
        var option = decision.Options[choice];
        if (!Affordable(player, option.Cost)) return $"Nicht finanzierbar (benötigt {Util.FormatEuro(option.Cost)}).";
        return null;
    }

    public static void Decide(GameState state, Player player, int choice)
    {
        var decision = player.Decision!;
        player.Decision = null;
        ApplyDecision(state, player, decision, choice);
    }

    /// <summary>Monatsende: Ohne Antwort gilt die vorsichtige Wahl. <paramref name="force"/> schließt auch eine Entscheidung mit späterer Frist ab.</summary>
    public static void ExpireDecision(GameState state, Player player, bool force = false)
    {
        var decision = player.Decision;
        if (decision is null) return;
        if (!force && decision.Deadline is { } deadline && deadline > (state.Year - 1) * 12 + state.Month) return;
        player.Decision = null;
        Util.AddLog(state, player.Id, $"Keine Antwort auf „{PlainTitle(decision.Title)}“ – es gilt: {decision.Options[decision.Fallback].Label}.");
        ApplyDecision(state, player, decision, decision.Fallback);
    }

    /// <summary>Monatliches Zufallsereignis: Glücksfall, Rückschlag oder Entscheidung</summary>
    public static void MonthlyEvent(GameState state, Player player)
    {
        var decisions = PossibleDecisions(state, player)
            .Select(def => new ExtraCandidate(def.Rarity, () => OfferDecision(state, player, def)))
            .ToList();
        Events.EstateEvents(state, player, decisions);
    }
}
