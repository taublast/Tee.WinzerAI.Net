using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;
using Winzer.Engine.Data;

namespace Winzer.Engine;

// Zufallsereignisse: Jeden Monat kann jedem Weingut etwas passieren – meist Kleinigkeiten, selten Einschneidendes,
// ganz selten etwas, das das Spiel umkrempelt. Dazu befristete Folgen (Effekte) auf Absatz, Preise, Kosten und Güte.

public enum Rarity
{
    [JsonStringEnumMemberName("alltag")] Alltag,
    [JsonStringEnumMemberName("gelegentlich")] Gelegentlich,
    [JsonStringEnumMemberName("selten")] Selten,
    [JsonStringEnumMemberName("sehr-selten")] SehrSelten,
}

public sealed class EventContext
{
    public required GameState State { get; init; }
    public required Player Player { get; init; }

    public double Roll() => Rng.NextFloat(State.Rng);

    /// <summary>Zufallszahl im Bereich [min, max], auf <paramref name="step"/> gerundet</summary>
    public double Between(double min, double max, double step = 1) => min + Js.Round((Roll() * (max - min)) / step) * step;
}

public sealed class EstateEventDef
{
    public required string Id { get; init; }
    public required Rarity Rarity { get; init; }
    public required LogTone Tone { get; init; }
    /// <summary>Nur in diesen Monaten möglich</summary>
    public int[]? Months { get; init; }
    /// <summary>Nur möglich, wenn die Bedingung erfüllt ist</summary>
    public Func<EventContext, bool>? When { get; init; }
    /// <summary>Wendet das Ereignis an und liefert die Meldung</summary>
    public required Func<EventContext, string> Apply { get; init; }
}

/// <summary>Weitere Kandidaten für die Monatsziehung, etwa Entscheidungsereignisse</summary>
public sealed class ExtraCandidate
{
    public ExtraCandidate() { }

    [SetsRequiredMembers]
    public ExtraCandidate(Rarity rarity, Action trigger)
    {
        Rarity = rarity;
        Trigger = trigger;
    }

    public required Rarity Rarity { get; init; }
    public required Action Trigger { get; init; }
}

public static class Events
{
    const double CrisisChance = 0.015;
    const int CrisisMonths = 6;

    /// <summary>Chance je Weingut und Monat, dass überhaupt ein Ereignis eintritt</summary>
    public const double EventChance = 0.35;

    /// <summary>Relative Häufigkeit je Seltenheitsstufe</summary>
    public static readonly Dictionary<Rarity, double> RarityWeight = new()
    {
        [Rarity.Alltag] = 10,
        [Rarity.Gelegentlich] = 3,
        [Rarity.Selten] = 1,
        [Rarity.SehrSelten] = 0.15,
    };

    // Hilfen für den Katalog

    public static List<Plot> BearingPlots(EventContext ctx) =>
        ctx.Player.Plots.Where(p => p.Grape is not null && !p.HarvestedThisYear && Rules.PlotAgeFactor(p, ctx.State.Year) > 0).ToList();

    public static bool HasBearing(EventContext ctx) => BearingPlots(ctx).Count > 0;

    static List<Plot> PlantedPlots(EventContext ctx) => ctx.Player.Plots.Where(p => p.Grape is not null).ToList();

    static T RandomOf<T>(EventContext ctx, List<T> items) => items[(int)Math.Floor(ctx.Roll() * items.Count)];

    /// <summary>Verkaufsfertige Flaschen, die nicht für eine Prämierung angemeldet sind</summary>
    public static List<BottleLot> SellableLots(EventContext ctx, bool registeredOnly = false) =>
        ctx.Player.Bottles
            .Where(l => l.MonthsUntilReady == 0 && !ctx.Player.CompetitionEntries.Contains(l.Id) && (!registeredOnly || l.Registered))
            .ToList();

    public static double BottleCount(IEnumerable<BottleLot> lots)
    {
        var sum = 0.0;
        foreach (var l in lots) sum += l.Bottles;
        return sum;
    }

    public static string Money(EventContext ctx, double amount)
    {
        ctx.Player.Money += amount;
        return $"{(amount >= 0 ? "+" : "−")}{Util.FormatEuro(Math.Abs(amount))}";
    }

    public static string Reputation(EventContext ctx, double delta)
    {
        var change = Rules.ChangeReputation(ctx.Player, delta);
        return $"Bekanntheit {(change >= 0 ? "+" : "")}{change.ToString("F1", CultureInfo.InvariantCulture)}";
    }

    public static void AddEffect(Player player, EffectKind kind, double value, int months, string label)
    {
        player.Effects.Add(new Effect { Kind = kind, Value = value, MonthsLeft = months, Label = label });
    }

    /// <summary>Verkauft Flaschen, älteste zuerst; liefert verkaufte Menge und Erlös</summary>
    public static (double Sold, double Revenue) SellBottles(EventContext ctx, IEnumerable<BottleLot> lots, double count, double priceFactor)
    {
        var left = count;
        var revenue = 0.0;
        foreach (var lot in lots)
        {
            if (left <= 0) break;
            var n = Math.Min(left, lot.Bottles);
            revenue += n * Market.BottlePrice(lot, ctx.Player, ctx.State) * priceFactor;
            Rules.RemoveBottles(ctx.Player, lot, n);
            left -= n;
        }
        revenue = Js.Round(revenue);
        ctx.Player.Money += revenue;
        return (count - left, revenue);
    }

    /// <summary>Vernichtet einen Anteil aller Flaschen; liefert die Zahl</summary>
    static double DestroyBottles(EventContext ctx, double share)
    {
        var lost = 0.0;
        foreach (var lot in ctx.Player.Bottles.ToList())
        {
            var n = Js.Round(lot.Bottles * share);
            if (n > 0) Rules.RemoveBottles(ctx.Player, lot, n);
            lost += n;
        }
        return lost;
    }

    static void DamageCrop(IEnumerable<Plot> plots, double factor)
    {
        foreach (var plot in plots) plot.CropFactor *= factor;
    }

    /// <summary>ESTATE_EVENTS des Originals (der Name EstateEvents gehört der Funktion estateEvents).</summary>
    public static readonly List<EstateEventDef> EstateEventList =
    [
        // ── Alltag: häufig, kaum spürbar ─────────────────────────────────────────────
        new()
        {
            Id = "wandergruppe", Rarity = Rarity.Alltag, Tone = LogTone.Good, Months = [4, 5, 6, 7, 8, 9, 10],
            Apply = c => $"Eine Wandergruppe kehrt spontan zur Weinprobe ein: {Money(c, c.Between(200, 800, 50))}.",
        },
        new()
        {
            Id = "pumpe", Rarity = Rarity.Alltag, Tone = LogTone.Bad,
            Apply = c => $"Die Weinpumpe streikt – Reparatur {Money(c, -c.Between(200, 700, 50))}.",
        },
        new()
        {
            Id = "lokalblatt", Rarity = Rarity.Alltag, Tone = LogTone.Good,
            Apply = c => $"Das Lokalblatt bringt ein kleines Porträt über dein Weingut ({Reputation(c, 1)}).",
        },
        new()
        {
            Id = "bewertung", Rarity = Rarity.Alltag, Tone = LogTone.Bad,
            Apply = c => $"Eine verärgerte Online-Bewertung macht die Runde ({Reputation(c, -1)}).",
        },
        new()
        {
            Id = "stare", Rarity = Rarity.Alltag, Tone = LogTone.Bad, Months = [8, 9, 10], When = HasBearing,
            Apply = c =>
            {
                DamageCrop(BearingPlots(c), 0.96);
                return "Ein Starenschwarm fällt über die reifen Trauben her – ein paar Prozent der Ernte sind weg.";
            },
        },
        new()
        {
            Id = "wildschweine", Rarity = Rarity.Alltag, Tone = LogTone.Bad, Months = [8, 9, 10], When = HasBearing,
            Apply = c =>
            {
                var plot = RandomOf(c, BearingPlots(c));
                DamageCrop([plot], 0.94);
                return $"Wildschweine haben in der Lage {plot.Name} gewütet – rund 6 % weniger Trauben.";
            },
        },
        new()
        {
            Id = "hochzeit", Rarity = Rarity.Alltag, Tone = LogTone.Good, When = c => BottleCount(SellableLots(c)) >= 60,
            Apply = c =>
            {
                var (sold, revenue) = SellBottles(c, SellableLots(c), c.Between(60, 120, 10), 1);
                return $"Ein Stammkunde bestellt {Util.JsNumber(sold)} Flaschen für eine Hochzeit: +{Util.FormatEuro(revenue)}.";
            },
        },
        new()
        {
            Id = "steuer", Rarity = Rarity.Alltag, Tone = LogTone.Good,
            Apply = c => $"Das Finanzamt erstattet zu viel gezahlte Steuern: {Money(c, c.Between(500, 2000, 100))}.",
        },
        new()
        {
            Id = "traktor", Rarity = Rarity.Alltag, Tone = LogTone.Bad,
            Apply = c => $"Der Schmalspurtraktor braucht neue Reifen: {Money(c, -c.Between(300, 900, 100))}.",
        },
        new()
        {
            Id = "sommelier", Rarity = Rarity.Alltag, Tone = LogTone.Good,
            Apply = c => $"Ein Sommelier empfiehlt deinen Wein im Restaurant der Kreisstadt ({Reputation(c, 1.5)}).",
        },
        new()
        {
            Id = "weinfest", Rarity = Rarity.Alltag, Tone = LogTone.Good, Months = [5, 6, 7, 8, 9],
            Apply = c =>
            {
                AddEffect(c.Player, EffectKind.Demand, 1.2, 1, "Weinfest im Ort");
                return "Weinfest im Ort: Nächsten Monat kommen 20 % mehr Kunden in den Hofladen.";
            },
        },
        new()
        {
            Id = "rehe", Rarity = Rarity.Alltag, Tone = LogTone.Bad, Months = [4, 5, 6],
            When = c => c.Player.Plots.Any(p => p.Grape is not null && Rules.PlotAgeFactor(p, c.State.Year) == 0),
            Apply = c => $"Rehe knabbern an der Junganlage – ein Wildschutzzaun muss her: {Money(c, -c.Between(300, 800, 50))}.",
        },
        new()
        {
            Id = "starkregen", Rarity = Rarity.Alltag, Tone = LogTone.Bad, Months = [3, 4, 5, 6, 7, 8, 9, 10],
            Apply = c => $"Starkregen spült Erde aus den Rebzeilen – Aufräumarbeiten: {Money(c, -c.Between(200, 600, 50))}.",
        },
        new()
        {
            Id = "praktikum", Rarity = Rarity.Alltag, Tone = LogTone.Good,
            Apply = c => $"Eine Praktikantin packt kräftig mit an – gesparte Aushilfslöhne: {Money(c, c.Between(300, 900, 50))}.",
        },
        new()
        {
            Id = "korken", Rarity = Rarity.Alltag, Tone = LogTone.Bad, When = c => BottleCount(c.Player.Bottles) >= 50,
            Apply = c =>
            {
                var lot = RandomOf(c, c.Player.Bottles.Where(l => l.Bottles >= 10).ToList());
                var n = Math.Min(lot.Bottles, c.Between(10, 30));
                Rules.RemoveBottles(c.Player, lot, n);
                return $"Korkfehler: {Util.JsNumber(n)} Flaschen {Rules.WineLabel(lot)} müssen weggekippt werden.";
            },
        },

        // ── Gelegentlich: spürbar, aber verkraftbar ──────────────────────────────────
        new()
        {
            Id = "weinfuehrer", Rarity = Rarity.Gelegentlich, Tone = LogTone.Good,
            Apply = c =>
            {
                AddEffect(c.Player, EffectKind.Demand, 1.2, 3, "Empfehlung im Weinführer");
                return $"Ein Weinführer nennt dein Gut einen Geheimtipp ({Reputation(c, 4)}, drei Monate 20 % mehr Nachfrage).";
            },
        },
        new()
        {
            Id = "glas", Rarity = Rarity.Gelegentlich, Tone = LogTone.Bad,
            Apply = c =>
            {
                AddEffect(c.Player, EffectKind.Costs, 1.08, 3, "Teures Verpackungsmaterial");
                return "Glasflaschen und Kartons werden knapp: Drei Monate lang 8 % höhere Betriebskosten.";
            },
        },
        new()
        {
            Id = "erntehelfer", Rarity = Rarity.Gelegentlich, Tone = LogTone.Bad, Months = [8, 9, 10], When = HasBearing,
            Apply = c => $"Die Erntehelfer bleiben aus – teure Ersatzkräfte müssen her: {Money(c, -c.Between(1500, 3000, 100))}.",
        },
        new()
        {
            Id = "gastronomie", Rarity = Rarity.Gelegentlich, Tone = LogTone.Good, When = c => BottleCount(SellableLots(c, true)) >= 300,
            Apply = c =>
            {
                var (sold, revenue) = SellBottles(c, SellableLots(c, true), 300, 0.9);
                return $"Eine Restaurantkette nimmt {Util.JsNumber(sold)} Flaschen auf einen Schlag ab: +{Util.FormatEuro(revenue)}.";
            },
        },
        new()
        {
            Id = "wasserschaden", Rarity = Rarity.Gelegentlich, Tone = LogTone.Bad, When = c => c.Player.Cellar.Count > 0,
            Apply = c =>
            {
                var batch = RandomOf(c, c.Player.Cellar);
                var lost = Math.Max(1, Js.Round(batch.Hectoliters * (0.05 + c.Roll() * 0.05)));
                var label = Rules.WineLabel(batch);
                Rules.RemoveBatchVolume(c.Player, batch, Math.Min(lost, batch.Hectoliters));
                return $"Wasserrohrbruch im Keller: {Util.JsNumber(lost)} hl {label} sind verdorben.";
            },
        },
        new()
        {
            Id = "foerderung", Rarity = Rarity.Gelegentlich, Tone = LogTone.Good,
            Apply = c => $"Ein Förderprogramm für Steillagen und Kulturlandschaft zahlt aus: {Money(c, c.Between(4000, 10_000, 500))}.",
        },
        new()
        {
            Id = "firmenfeier", Rarity = Rarity.Gelegentlich, Tone = LogTone.Good,
            Apply = c => $"Ein Unternehmen bucht dein Weingut für seine Sommerfeier mit Weinprobe: {Money(c, c.Between(2000, 5000, 100))}.",
        },
        new()
        {
            Id = "fernsehen", Rarity = Rarity.Gelegentlich, Tone = LogTone.Good,
            Apply = c => $"Das Regionalfernsehen dreht einen Beitrag auf deinem Hof ({Reputation(c, 3)}).",
        },
        new()
        {
            Id = "einbruch", Rarity = Rarity.Gelegentlich, Tone = LogTone.Bad, When = c => BottleCount(c.Player.Bottles) >= 150,
            Apply = c =>
            {
                var target = c.Between(50, 150, 10);
                var stolen = 0.0;
                foreach (var lot in c.Player.Bottles.OrderByDescending(l => l.Quality).ToList())
                {
                    if (stolen >= target) break;
                    var n = Math.Min(lot.Bottles, target - stolen);
                    Rules.RemoveBottles(c.Player, lot, n);
                    stolen += n;
                }
                return $"Einbruch im Flaschenlager: {Util.JsNumber(stolen)} Flaschen der besten Weine sind gestohlen.";
            },
        },
        new()
        {
            Id = "strom", Rarity = Rarity.Gelegentlich, Tone = LogTone.Bad,
            Apply = c =>
            {
                AddEffect(c.Player, EffectKind.Costs, 1.05, 6, "Höhere Energiepreise");
                return "Der Stromversorger erhöht die Preise: Ein halbes Jahr lang 5 % höhere Betriebskosten.";
            },
        },
        new()
        {
            Id = "influencer", Rarity = Rarity.Gelegentlich, Tone = LogTone.Good,
            Apply = c =>
            {
                AddEffect(c.Player, EffectKind.Demand, 1.3, 2, "Video einer Influencerin");
                return "Eine Influencerin filmt beim Weinwandern auf deinem Hof – zwei Monate lang 30 % mehr Nachfrage.";
            },
        },
        new()
        {
            Id = "rebschnitt", Rarity = Rarity.Gelegentlich, Tone = LogTone.Good, Months = [1, 2, 3], When = c => PlantedPlots(c).Count > 0,
            Apply = c =>
            {
                AddEffect(c.Player, EffectKind.Quality, 3, 13 - c.State.Month, "Rebschnitt vom Profi");
                return "Ein erfahrener Rebschnitt-Profi hilft im Weinberg – der Jahrgang wird besser (+3 Güte).";
            },
        },
        new()
        {
            Id = "gewitter", Rarity = Rarity.Gelegentlich, Tone = LogTone.Bad, Months = [5, 6, 7, 8], When = HasBearing,
            Apply = c =>
            {
                var plot = RandomOf(c, BearingPlots(c));
                Defense.CropLoss(c.State, c.Player, [plot], 0.85, "Sturmböen"); // versicherbares Wetter
                return $"Ein Gewitter mit Sturmböen reißt in der Lage {plot.Name} Triebe ab – 15 % weniger Ertrag.";
            },
        },
        new()
        {
            Id = "zahlungsausfall", Rarity = Rarity.Gelegentlich, Tone = LogTone.Bad,
            Apply = c => $"Ein Großkunde ist pleite und zahlt seine Rechnung nicht: {Money(c, -c.Between(1500, 4000, 100))}.",
        },

        // ── Selten: deutliche Folgen ─────────────────────────────────────────────────
        new()
        {
            Id = "sturm", Rarity = Rarity.Selten, Tone = LogTone.Bad,
            Apply = c => $"Ein Orkan deckt das Dach der Kelterhalle ab: {Money(c, -c.Between(6000, 12_000, 1000))}.",
        },
        new()
        {
            Id = "erbschaft", Rarity = Rarity.Selten, Tone = LogTone.Good,
            Apply = c =>
            {
                var state = c.State;
                var player = c.Player;
                var hectares = 1 + Math.Floor(c.Roll() * 2);
                var suitable = Grapes.All.Keys.Where(g => Regions.All[player.RegionId].Suitability[g] >= 0).ToList();
                GrapeId? grape = c.Roll() < 0.5 ? suitable[(int)Math.Floor(c.Roll() * suitable.Count)] : null;
                var taken = new HashSet<string>(player.Plots.Select(p => p.Name));
                var name = "Erbstück";
                for (var n = 2; taken.Contains(name); n++) name = $"Erbstück {n}";
                player.Plots.Add(new Plot
                {
                    Id = Util.NewId(state, "plot"), Name = name, Hectares = hectares, Grape = grape,
                    PlantedYear = grape is not null ? state.Year - 20 : null, Site = 0,
                    Oechsle = 0, Health = 1, CropFactor = 1, HarvestedThisYear = false, Botrytis = false, IceWineGamble = false,
                });
                return $"Eine Großtante vererbt dir einen Weinberg: {Util.JsNumber(hectares)} ha {(grape is { } inherited ? Grapes.All[inherited].Name : "Brache")}.";
            },
        },
        new()
        {
            Id = "winzer-des-jahres", Rarity = Rarity.Selten, Tone = LogTone.Good,
            Apply = c =>
            {
                c.Player.Points += 15;
                return $"Der Weinbauverband kürt dich zum Winzer des Jahres im Gebiet: +15 Punkte, {Reputation(c, 8)}.";
            },
        },
        new()
        {
            Id = "reblaus", Rarity = Rarity.Selten, Tone = LogTone.Bad, Months = [5, 6, 7, 8, 9], When = c => PlantedPlots(c).Count > 0,
            Apply = c =>
            {
                var plot = RandomOf(c, PlantedPlots(c));
                plot.Health = Math.Max(0, plot.Health - 0.3);
                plot.CropFactor *= 0.7;
                return $"Reblaus-Befall in der Lage {plot.Name}! Bekämpfung {Money(c, -3000)}, 30 % weniger Ertrag in diesem Jahr.";
            },
        },
        new()
        {
            Id = "handelskette", Rarity = Rarity.Selten, Tone = LogTone.Good,
            Apply = c =>
            {
                AddEffect(c.Player, EffectKind.Demand, 1.5, 6, "Listung bei einer Handelskette");
                return "Eine Feinkostkette listet deine Weine: Ein halbes Jahr lang 50 % mehr Absatz ab Hof und online.";
            },
        },
        new()
        {
            Id = "lagerbrand", Rarity = Rarity.Selten, Tone = LogTone.Bad, When = c => BottleCount(c.Player.Bottles) >= 200,
            Apply = c => $"Ein Schwelbrand im Flaschenlager: {Util.JsNumber(DestroyBottles(c, 0.2))} Flaschen sind hinüber.",
        },
        new()
        {
            Id = "rechtsstreit", Rarity = Rarity.Selten, Tone = LogTone.Bad,
            Apply = c =>
                $"Ein Nachbar verklagt dich wegen eines Grenzsteins – Anwalt und Vergleich: {Money(c, -c.Between(5000, 10_000, 1000))}.",
        },
        new()
        {
            Id = "filmdreh", Rarity = Rarity.Selten, Tone = LogTone.Good,
            Apply = c => $"Ein Filmteam dreht eine Fernsehserie auf deinem Hof und zahlt Drehgebühr: {Money(c, c.Between(10_000, 20_000, 1000))}.",
        },
        new()
        {
            Id = "maezen", Rarity = Rarity.Selten, Tone = LogTone.Good,
            Apply = c =>
            {
                c.Player.BarriqueCapacityHl += 20;
                return "Ein Mäzen aus der Stadt schenkt dir neue Barriquefässer: +20 hl Barrique-Kapazität.";
            },
        },
        new()
        {
            Id = "preiskampf", Rarity = Rarity.Selten, Tone = LogTone.Bad,
            Apply = c =>
            {
                AddEffect(c.Player, EffectKind.Price, 0.92, 4, "Preiskampf der Discounter");
                return "Discounter verramschen Wein aus deinem Gebiet: Vier Monate lang 8 % niedrigere Preise.";
            },
        },
        new()
        {
            Id = "tankleck", Rarity = Rarity.Selten, Tone = LogTone.Bad, When = c => c.Player.Cellar.Any(b => !b.Barrique),
            Apply = c =>
            {
                var batch = c.Player.Cellar.Where(b => !b.Barrique).OrderByDescending(b => b.Hectoliters).First();
                var lost = Math.Max(1, Js.Round(batch.Hectoliters * 0.3));
                var label = Rules.WineLabel(batch);
                Rules.RemoveBatchVolume(c.Player, batch, Math.Min(lost, batch.Hectoliters));
                return $"Ein Tankventil bricht über Nacht: {Util.JsNumber(lost)} hl {label} laufen in den Gully.";
            },
        },

        // ── Sehr selten: drastisch ───────────────────────────────────────────────────
        new()
        {
            Id = "jahrhundertunwetter", Rarity = Rarity.SehrSelten, Tone = LogTone.Bad, Months = [5, 6, 7, 8, 9], When = HasBearing,
            Apply = c =>
            {
                Defense.CropLoss(c.State, c.Player, BearingPlots(c), 0.3, "das Jahrhundertunwetter");
                return $"Jahrhundertunwetter! Hagel und Sturzfluten vernichten 70 % der Trauben, Schäden an Mauern und Wegen: {Money(c, -20_000)}.";
            },
        },
        new()
        {
            Id = "kellerbrand", Rarity = Rarity.SehrSelten, Tone = LogTone.Bad,
            Apply = c =>
            {
                var hl = 0.0;
                foreach (var batch in c.Player.Cellar.ToList())
                {
                    var lost = Js.Round(batch.Hectoliters * 0.5);
                    hl += lost;
                    Rules.RemoveBatchVolume(c.Player, batch, lost);
                }
                var bottles = DestroyBottles(c, 0.3);
                return $"Großbrand im Weinkeller! {Util.JsNumber(hl)} hl Wein und {Util.JsNumber(bottles)} Flaschen verloren, Sanierung {Money(c, -25_000)}.";
            },
        },
        new()
        {
            Id = "weinpapst", Rarity = Rarity.SehrSelten, Tone = LogTone.Good,
            Apply = c =>
            {
                c.Player.Points += 40;
                AddEffect(c.Player, EffectKind.Demand, 2, 6, "100 Punkte vom Weinpapst");
                return $"Sensation: Ein weltberühmter Weinkritiker vergibt 100 Punkte! +40 Punkte, {Reputation(c, 20)}, ein halbes Jahr doppelte Nachfrage.";
            },
        },
        new()
        {
            Id = "vermoegen", Rarity = Rarity.SehrSelten, Tone = LogTone.Good,
            Apply = c => $"Ein ausgewanderter Onkel hinterlässt dir sein Vermögen: {Money(c, 150_000)}!",
        },
        new()
        {
            Id = "hangrutsch", Rarity = Rarity.SehrSelten, Tone = LogTone.Bad, When = c => c.Player.Plots.Count >= 2,
            Apply = c =>
            {
                var plot = RandomOf(c, c.Player.Plots);
                c.Player.Plots = c.Player.Plots.Where(p => !ReferenceEquals(p, plot)).ToList();
                return $"Hangrutsch! Die Lage {plot.Name} ({Util.JsNumber(plot.Hectares)} ha) ist abgerutscht und für immer verloren.";
            },
        },
        new()
        {
            Id = "gebietsskandal", Rarity = Rarity.SehrSelten, Tone = LogTone.Bad,
            Apply = c =>
            {
                var state = c.State;
                var player = c.Player;
                foreach (var p in state.Players.Where(q => q.RegionId == player.RegionId && !q.Bankrupt).ToList())
                {
                    Rules.ChangeReputation(p, -10);
                    AddEffect(p, EffectKind.Price, 0.8, 12, "Weinskandal im Anbaugebiet");
                }
                return $"Weinskandal im Anbaugebiet {Regions.All[player.RegionId].Name}: Eine Großkellerei hat gepanscht. Alle Winzer der Region verlieren Ansehen, ein Jahr lang 20 % niedrigere Preise.";
            },
        },
        new()
        {
            Id = "exportboom", Rarity = Rarity.SehrSelten, Tone = LogTone.Good,
            Apply = c =>
            {
                AddEffect(c.Player, EffectKind.Price, 1.2, 12, "Exportboom");
                return "Deutscher Wein ist in Asien plötzlich Kult: Ein Jahr lang 20 % höhere Preise.";
            },
        },
    ];

    /// <summary>Ereignisse für alle: Krise der Großkellereien halbiert den Fassweinpreis für ein halbes Jahr.</summary>
    public static void MarketEvents(GameState state)
    {
        if (state.BulkCrisisMonths > 0)
        {
            if (--state.BulkCrisisMonths == 0) Util.AddLog(state, null, "Die Großkellereien zahlen wieder normale Fassweinpreise.");
            return;
        }
        if (Rng.NextFloat(state.Rng) < CrisisChance)
        {
            state.BulkCrisisMonths = CrisisMonths;
            Util.AddLog(state, null, $"Eine Großkellerei ist pleite: {CrisisMonths} Monate lang gibt es nur den halben Fassweinpreis.", LogTone.Bad);
        }
    }

    public static EventContext Context(GameState state, Player player) => new() { State = state, Player = player };

    /// <summary>Schonfrist: Im ersten Jahr, wenn die Kasse am knappsten ist, bleiben schwere Rückschläge aus.</summary>
    static bool IsHeavyBlow(EstateEventDef e) => e.Tone == LogTone.Bad && (e.Rarity == Rarity.Selten || e.Rarity == Rarity.SehrSelten);

    /// <summary>Ereignisse, die gerade eintreten können – mit Gewicht nach Seltenheit</summary>
    public static List<EstateEventDef> PossibleEvents(GameState state, Player player)
    {
        var ctx = Context(state, player);
        var result = new List<EstateEventDef>();
        foreach (var e in EstateEventList)
        {
            if ((e.Months is null || e.Months.Contains(state.Month)) && (e.When is null || e.When(ctx)) && !(state.Year == 1 && IsHeavyBlow(e)))
                result.Add(e);
        }
        return result;
    }

    /// <summary>Würfelt höchstens ein Ereignis für ein Weingut aus – aus dem Katalog und den zusätzlichen Kandidaten.</summary>
    public static void EstateEvents(GameState state, Player player, IEnumerable<ExtraCandidate>? extra = null)
    {
        if (Rng.NextFloat(state.Rng) >= EventChance) return;
        var candidates = new List<ExtraCandidate>();
        foreach (var e in PossibleEvents(state, player))
            candidates.Add(new ExtraCandidate(e.Rarity, () => TriggerEvent(state, player, e)));
        if (extra is not null) candidates.AddRange(extra);
        var total = 0.0;
        foreach (var e in candidates) total += RarityWeight[e.Rarity];
        var pick = Rng.NextFloat(state.Rng) * total;
        ExtraCandidate? chosen = null;
        foreach (var e in candidates)
        {
            if ((pick -= RarityWeight[e.Rarity]) < 0)
            {
                chosen = e;
                break;
            }
        }
        chosen ??= candidates.Count > 0 ? candidates[^1] : null;
        chosen?.Trigger();
    }

    public static void TriggerEvent(GameState state, Player player, EstateEventDef @event)
    {
        var text = @event.Apply(Context(state, player));
        // Drastische Ereignisse erfahren alle am Tisch
        var isPublic = @event.Rarity == Rarity.SehrSelten || @event.Rarity == Rarity.Selten;
        Util.AddLog(state, isPublic ? null : player.Id, isPublic ? $"{player.Name}: {text}" : text, @event.Tone);
    }

    /// <summary>Effekte laufen monatlich ab.</summary>
    public static void TickEffects(GameState state, Player player)
    {
        foreach (var effect in player.Effects) effect.MonthsLeft--;
        foreach (var effect in player.Effects.Where(e => e.MonthsLeft <= 0).ToList())
        {
            Util.AddLog(state, player.Id, $"Vorbei: {effect.Label}.");
        }
        player.Effects = player.Effects.Where(e => e.MonthsLeft > 0).ToList();
    }
}
