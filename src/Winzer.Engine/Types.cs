using System.Text.Json.Serialization;

namespace Winzer.Engine;

// Portierung von src/engine/types.ts. Zahlen sind wie im Original double, sofern sie nicht
// eindeutig ganzzahlig sind (Jahr, Monat, Zähler). Namen im JSON entsprechen dem Original.

public enum GrapeId
{
    [JsonStringEnumMemberName("mueller-thurgau")] MuellerThurgau,
    [JsonStringEnumMemberName("silvaner")] Silvaner,
    [JsonStringEnumMemberName("riesling")] Riesling,
    [JsonStringEnumMemberName("weissburgunder")] Weissburgunder,
    [JsonStringEnumMemberName("grauburgunder")] Grauburgunder,
    [JsonStringEnumMemberName("spaetburgunder")] Spaetburgunder,
    [JsonStringEnumMemberName("dornfelder")] Dornfelder,
    [JsonStringEnumMemberName("trollinger")] Trollinger,
    [JsonStringEnumMemberName("lemberger")] Lemberger,
    [JsonStringEnumMemberName("regent")] Regent,
    [JsonStringEnumMemberName("solaris")] Solaris,
}

public enum GrapeColor
{
    [JsonStringEnumMemberName("weiss")] Weiss,
    [JsonStringEnumMemberName("rot")] Rot,
}

public enum QualityLevel
{
    [JsonStringEnumMemberName("deutscher-wein")] DeutscherWein,
    [JsonStringEnumMemberName("qualitaetswein")] Qualitaetswein,
    [JsonStringEnumMemberName("kabinett")] Kabinett,
    [JsonStringEnumMemberName("spaetlese")] Spaetlese,
    [JsonStringEnumMemberName("auslese")] Auslese,
    [JsonStringEnumMemberName("beerenauslese")] Beerenauslese,
    [JsonStringEnumMemberName("trockenbeerenauslese")] Trockenbeerenauslese,
    [JsonStringEnumMemberName("eiswein")] Eiswein,
}

public enum RegionId
{
    [JsonStringEnumMemberName("ahr")] Ahr,
    [JsonStringEnumMemberName("baden")] Baden,
    [JsonStringEnumMemberName("franken")] Franken,
    [JsonStringEnumMemberName("hessische-bergstrasse")] HessischeBergstrasse,
    [JsonStringEnumMemberName("mittelrhein")] Mittelrhein,
    [JsonStringEnumMemberName("mosel")] Mosel,
    [JsonStringEnumMemberName("nahe")] Nahe,
    [JsonStringEnumMemberName("pfalz")] Pfalz,
    [JsonStringEnumMemberName("rheingau")] Rheingau,
    [JsonStringEnumMemberName("rheinhessen")] Rheinhessen,
    [JsonStringEnumMemberName("saale-unstrut")] SaaleUnstrut,
    [JsonStringEnumMemberName("sachsen")] Sachsen,
    [JsonStringEnumMemberName("wuerttemberg")] Wuerttemberg,
}

public enum WeatherKind
{
    [JsonStringEnumMemberName("sonnig")] Sonnig,
    [JsonStringEnumMemberName("wechselhaft")] Wechselhaft,
    [JsonStringEnumMemberName("nass")] Nass,
}

public enum AdulterationMethod
{
    [JsonStringEnumMemberName("wasser")] Wasser,
    [JsonStringEnumMemberName("zucker")] Zucker,
    [JsonStringEnumMemberName("glykol")] Glykol,
}

public enum ProtectionStrategy
{
    [JsonStringEnumMemberName("none")] None,
    [JsonStringEnumMemberName("conventional")] Conventional,
    [JsonStringEnumMemberName("bio")] Bio,
}

/// <summary>Bio-Siegel in aufsteigender Strenge</summary>
public enum BioLabel
{
    [JsonStringEnumMemberName("basis")] Basis,
    [JsonStringEnumMemberName("verband")] Verband,
    [JsonStringEnumMemberName("biodynamisch")] Biodynamisch,
}

public enum SabotageKind
{
    [JsonStringEnumMemberName("geruecht")] Geruecht,
    [JsonStringEnumMemberName("weinberg")] Weinberg,
}

public enum CompetitionId
{
    [JsonStringEnumMemberName("regional")] Regional,
    [JsonStringEnumMemberName("bund")] Bund,
}

public enum Medal
{
    [JsonStringEnumMemberName("gold")] Gold,
    [JsonStringEnumMemberName("silber")] Silber,
    [JsonStringEnumMemberName("bronze")] Bronze,
}

public enum MarketingKind
{
    [JsonStringEnumMemberName("social")] Social,
    [JsonStringEnumMemberName("hoffest")] Hoffest,
    [JsonStringEnumMemberName("messe")] Messe,
}

/// <summary>Marktsegment mit eigener Beliebtheit: jede Rebsorte, Sekt und Rosé. Die Werte der Rebsorten entsprechen GrapeId.</summary>
public enum TrendKey
{
    [JsonStringEnumMemberName("mueller-thurgau")] MuellerThurgau,
    [JsonStringEnumMemberName("silvaner")] Silvaner,
    [JsonStringEnumMemberName("riesling")] Riesling,
    [JsonStringEnumMemberName("weissburgunder")] Weissburgunder,
    [JsonStringEnumMemberName("grauburgunder")] Grauburgunder,
    [JsonStringEnumMemberName("spaetburgunder")] Spaetburgunder,
    [JsonStringEnumMemberName("dornfelder")] Dornfelder,
    [JsonStringEnumMemberName("trollinger")] Trollinger,
    [JsonStringEnumMemberName("lemberger")] Lemberger,
    [JsonStringEnumMemberName("regent")] Regent,
    [JsonStringEnumMemberName("solaris")] Solaris,
    [JsonStringEnumMemberName("sekt")] Sekt = 100,
    [JsonStringEnumMemberName("rose")] Rose = 101,
}

public enum CellarSkill
{
    [JsonStringEnumMemberName("sekt")] Sekt,
    [JsonStringEnumMemberName("barrique")] Barrique,
    [JsonStringEnumMemberName("rotwein")] Rotwein,
    [JsonStringEnumMemberName("weisswein")] Weisswein,
    [JsonStringEnumMemberName("edelsuess")] Edelsuess,
    [JsonStringEnumMemberName("sparsam")] Sparsam,
}

public enum AiProfileId
{
    [JsonStringEnumMemberName("tradition")] Tradition,
    [JsonStringEnumMemberName("genossenschaft")] Genossenschaft,
    [JsonStringEnumMemberName("bio")] Bio,
    [JsonStringEnumMemberName("schlitzohr")] Schlitzohr,
}

public enum Difficulty
{
    [JsonStringEnumMemberName("leicht")] Leicht,
    [JsonStringEnumMemberName("normal")] Normal,
    [JsonStringEnumMemberName("schwierig")] Schwierig,
}

public enum LotStatus
{
    [JsonStringEnumMemberName("pending")] Pending,
    [JsonStringEnumMemberName("open")] Open,
    [JsonStringEnumMemberName("sold")] Sold,
    [JsonStringEnumMemberName("unsold")] Unsold,
}

public enum AuctionPhase
{
    [JsonStringEnumMemberName("announced")] Announced,
    [JsonStringEnumMemberName("live")] Live,
}

/// <summary>guard: Nachtwache gegen Sabotage (ohne Faktor)</summary>
public enum EffectKind
{
    [JsonStringEnumMemberName("demand")] Demand,
    [JsonStringEnumMemberName("price")] Price,
    [JsonStringEnumMemberName("costs")] Costs,
    [JsonStringEnumMemberName("quality")] Quality,
    [JsonStringEnumMemberName("guard")] Guard,
}

public enum LogTone
{
    [JsonStringEnumMemberName("good")] Good,
    [JsonStringEnumMemberName("bad")] Bad,
}

public enum StartPlots
{
    [JsonStringEnumMemberName("standard")] Standard,
    [JsonStringEnumMemberName("planted")] Planted,
    [JsonStringEnumMemberName("fallow")] Fallow,
}

public sealed class Plot
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public double Hectares { get; set; }
    public GrapeId? Grape { get; set; }
    public int? PlantedYear { get; set; }
    /// <summary>Lagenqualität −1 bis +2, wirkt auf die Güte</summary>
    public double Site { get; set; }
    /// <summary>Aktuelles Mostgewicht in °Oechsle (0 vor Reifebeginn)</summary>
    public double Oechsle { get; set; }
    /// <summary>0–1, sinkt durch Fäulnis</summary>
    public double Health { get; set; }
    /// <summary>Mengenfaktor des laufenden Jahrgangs</summary>
    public double CropFactor { get; set; }
    public bool HarvestedThisYear { get; set; }
    public bool Botrytis { get; set; }
    public bool IceWineGamble { get; set; }
    public bool HailNet { get; set; }
    public bool Irrigation { get; set; }

    public Plot Clone() => (Plot)MemberwiseClone();
}

/// <summary>Gemeinsame Eigenschaften von Wein im Tank und in der Flasche</summary>
public abstract class WineInfo
{
    public GrapeId Grape { get; set; }
    public int Vintage { get; set; }
    public double Oechsle { get; set; }
    public QualityLevel Level { get; set; }
    /// <summary>Güte 0–100</summary>
    public double Quality { get; set; }
    public AdulterationMethod? Adulterated { get; set; }
    /// <summary>Aus roten Trauben hell gekeltert</summary>
    public bool Rose { get; set; }

    /// <summary>Entspricht dem Spread <c>{ ...wine }</c> im Original für die gemeinsamen Felder.</summary>
    public void CopyInfoFrom(WineInfo other)
    {
        Grape = other.Grape;
        Vintage = other.Vintage;
        Oechsle = other.Oechsle;
        Level = other.Level;
        Quality = other.Quality;
        Adulterated = other.Adulterated;
        Rose = other.Rose;
    }
}

/// <summary>Wein im Tank oder im Barriquefass</summary>
public sealed class WineBatch : WineInfo
{
    public string Id { get; set; } = "";
    public double Hectoliters { get; set; }
    public int MonthsUntilReady { get; set; }
    public bool Barrique { get; set; }

    public WineBatch Clone() => (WineBatch)MemberwiseClone();
}

public sealed class Award
{
    public CompetitionId Competition { get; set; }
    public int Year { get; set; }
    public Medal Medal { get; set; }

    public Award Clone() => (Award)MemberwiseClone();
}

/// <summary>Abgefüllter Wein im Flaschenlager</summary>
public sealed class BottleLot : WineInfo
{
    public string Id { get; set; } = "";
    public double Bottles { get; set; }
    public bool Sparkling { get; set; }
    public int MonthsUntilReady { get; set; }
    public bool Registered { get; set; }
    public List<Award> Awards { get; set; } = [];

    public BottleLot Clone()
    {
        var copy = (BottleLot)MemberwiseClone();
        copy.Awards = Awards.Select(a => a.Clone()).ToList();
        return copy;
    }
}

public class ExportOffer
{
    public string Id { get; set; } = "";
    public string Country { get; set; } = "";
    public double BottlesPerMonth { get; set; }
    public double PricePerBottle { get; set; }
    public QualityLevel MinLevel { get; set; }
    public double MinQuality { get; set; }
    public int Months { get; set; }

    public ExportOffer Clone() => (ExportOffer)MemberwiseClone();
}

public sealed class ExportContract : ExportOffer
{
    public int MonthsLeft { get; set; }
    public int MissedMonths { get; set; }

    public new ExportContract Clone() => (ExportContract)MemberwiseClone();
}

/// <summary>Beliebtheit am Markt als Preisfaktor; sie wandert langsam auf ein Ziel zu</summary>
public sealed class Trend
{
    public double Value { get; set; }
    public double Target { get; set; }

    public Trend Clone() => (Trend)MemberwiseClone();
}

public sealed class CellarMaster
{
    public string Name { get; set; } = "";
    public double Talent { get; set; }
    public List<CellarSkill> Skills { get; set; } = [];
    public double Salary { get; set; }
    /// <summary>Jahr der Einstellung (null = Bewerber)</summary>
    public int? HiredYear { get; set; }

    public CellarMaster Clone()
    {
        var copy = (CellarMaster)MemberwiseClone();
        copy.Skills = [.. Skills];
        return copy;
    }
}

/// <summary>Lieferauftrag eines Kunden: einmalige Lieferung bis zu einem Termin</summary>
public sealed class CustomerOrder
{
    public string Id { get; set; } = "";
    public string Customer { get; set; } = "";
    public string Icon { get; set; } = "";
    public GrapeId? Grape { get; set; }
    public GrapeColor? Color { get; set; }
    public bool Sparkling { get; set; }
    public QualityLevel MinLevel { get; set; }
    public double MinQuality { get; set; }
    public BioLabel? BioLabel { get; set; }
    public double Bottles { get; set; }
    public double PricePerBottle { get; set; }
    public double Points { get; set; }
    public double Reputation { get; set; }
    /// <summary>Liefertermin: Ende dieses fortlaufenden Spielmonats</summary>
    public int Deadline { get; set; }
    public int OfferExpires { get; set; }

    public CustomerOrder Clone() => (CustomerOrder)MemberwiseClone();
}

/// <summary>Zähler für Meilensteine</summary>
public sealed class PlayerStats
{
    public int Harvests { get; set; }
    public int BestLevelRank { get; set; } = -1;
    public int Edelsuess { get; set; }
    public int Sekt { get; set; }
    public int Barrique { get; set; }
    public double BottlesSold { get; set; }
    public int Medals { get; set; }
    public int GoldMedals { get; set; }
    public int OrdersDelivered { get; set; }
    public int ExportsCompleted { get; set; }
    public int AuctionsWon { get; set; }

    public PlayerStats Clone() => (PlayerStats)MemberwiseClone();
}

public sealed class Bid
{
    public string Bidder { get; set; } = "";
    public double Amount { get; set; }

    public Bid Clone() => (Bid)MemberwiseClone();
}

/// <summary>Ein Weinberg, der bei der Live-Versteigerung unter den Hammer kommt</summary>
public sealed class AuctionLot
{
    public string Id { get; set; } = "";
    public RegionId RegionId { get; set; }
    public string Name { get; set; } = "";
    public double Hectares { get; set; }
    public GrapeId? Grape { get; set; }
    public int? PlantedYear { get; set; }
    public double Site { get; set; }
    public double Estimate { get; set; }
    public double StartPrice { get; set; }
    public double Increment { get; set; }
    public LotStatus Status { get; set; }
    /// <summary>Aktuelles Höchstgebot (0 = noch keins)</summary>
    public double Price { get; set; }
    /// <summary>Spieler-ID oder "investor"</summary>
    public string? Leader { get; set; }
    public List<string> HumansIn { get; set; } = [];
    public string? Awaiting { get; set; }
    /// <summary>Verdeckte Höchstgebote in Einfügereihenfolge</summary>
    public Dictionary<string, double> Limits { get; set; } = [];
    public List<Bid> Bids { get; set; } = [];

    public AuctionLot Clone()
    {
        var copy = (AuctionLot)MemberwiseClone();
        copy.HumansIn = [.. HumansIn];
        copy.Limits = new Dictionary<string, double>(Limits);
        copy.Bids = Bids.Select(b => b.Clone()).ToList();
        return copy;
    }
}

public sealed class AuctionState
{
    public AuctionPhase Phase { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public List<AuctionLot> Lots { get; set; } = [];

    public AuctionState Clone()
    {
        var copy = (AuctionState)MemberwiseClone();
        copy.Lots = Lots.Select(l => l.Clone()).ToList();
        return copy;
    }
}

/// <summary>Eine Möglichkeit bei einem Entscheidungsereignis</summary>
public sealed class DecisionOption
{
    public string Label { get; set; } = "";
    public string Hint { get; set; } = "";
    public double Cost { get; set; }

    public DecisionOption Clone() => (DecisionOption)MemberwiseClone();
}

/// <summary>Offene Entscheidung eines Weinguts – gilt bis Monatsende</summary>
public sealed class Decision
{
    public string EventId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public List<DecisionOption> Options { get; set; } = [];
    public int Fallback { get; set; }
    public Dictionary<string, double> Params { get; set; } = [];
    /// <summary>Letzter Spielmonat für die Antwort; ohne Angabe nur der laufende Monat</summary>
    public int? Deadline { get; set; }

    public Decision Clone()
    {
        var copy = (Decision)MemberwiseClone();
        copy.Options = Options.Select(o => o.Clone()).ToList();
        copy.Params = new Dictionary<string, double>(Params);
        return copy;
    }
}

/// <summary>Befristete Folge eines Ereignisses</summary>
public sealed class Effect
{
    public string Label { get; set; } = "";
    public EffectKind Kind { get; set; }
    public double Value { get; set; }
    public int MonthsLeft { get; set; }

    public Effect Clone() => (Effect)MemberwiseClone();
}

public sealed class AutoSkip
{
    public int Since { get; set; }
    public int Until { get; set; }

    public AutoSkip Clone() => (AutoSkip)MemberwiseClone();
}

public sealed class Player
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public RegionId RegionId { get; set; }
    /// <summary>null = Mensch</summary>
    public AiProfileId? Ai { get; set; }
    public Difficulty? Difficulty { get; set; }
    public double Money { get; set; }
    public double Points { get; set; }
    public double Reputation { get; set; }
    public double Loan { get; set; }
    public double TankCapacityHl { get; set; }
    public double BarriqueCapacityHl { get; set; }
    public bool HasOnlineShop { get; set; }
    public bool HasVinothek { get; set; }
    public double WholesaleThisYear { get; set; }
    public CellarMaster? CellarMaster { get; set; }
    public List<Plot> Plots { get; set; } = [];
    public List<WineBatch> Cellar { get; set; } = [];
    public List<BottleLot> Bottles { get; set; } = [];
    public List<MarketingKind> MarketingThisMonth { get; set; } = [];
    public List<string> CompetitionEntries { get; set; } = [];
    public ProtectionStrategy Protection { get; set; }
    public int? BioSince { get; set; }
    public BioLabel? BioLabel { get; set; }
    public int? BioLabelSince { get; set; }
    public List<CustomerOrder> OrderOffers { get; set; } = [];
    public List<CustomerOrder> Orders { get; set; } = [];
    public Dictionary<string, double> Relations { get; set; } = [];
    public PlayerStats Stats { get; set; } = new();
    public Dictionary<string, int> Milestones { get; set; } = [];
    public double FederweisserThisMonth { get; set; }
    public int? StraussYear { get; set; }
    public AutoSkip? AutoSkip { get; set; }
    public bool IntrigueThisMonth { get; set; }
    public ExportOffer? ExportOffer { get; set; }
    public ExportContract? ExportContract { get; set; }
    public int MonthsInsolvent { get; set; }
    public bool Bankrupt { get; set; }
    public List<Effect> Effects { get; set; } = [];
    public Decision? Decision { get; set; }
    public int? InsuredYear { get; set; }
    public bool FrostCandles { get; set; }

    // ---- Regelversion 2 (siehe RulesV2.cs); in Partien nach Originalregeln unbenutzt ----

    /// <summary>Auf- oder Abschlag auf den Ab-Hof-Preis, −0,2 bis +0,4</summary>
    public double PriceLevel { get; set; }
    /// <summary>Zur Wahl stehende Jahresziele (IDs)</summary>
    public List<string> GoalOffers { get; set; } = [];
    /// <summary>Gewähltes Jahresziel</summary>
    public string? Goal { get; set; }
    /// <summary>Kennzahlen zu Jahresbeginn – Grundlage für Jahresziel und Jahresrückblick</summary>
    public Dictionary<string, double> YearStart { get; set; } = [];
    /// <summary>Rückblick auf das abgeschlossene Jahr</summary>
    public YearReview? Review { get; set; }
    /// <summary>Gewählte Ambitionen (IDs); Gegner sehen sie nicht</summary>
    public List<string> Ambitions { get; set; } = [];
    public List<string> AmbitionsDone { get; set; } = [];
    /// <summary>Gebaute Ausbauten (IDs)</summary>
    public List<string> Buildings { get; set; } = [];
    /// <summary>Bisherige Spenden an die Stiftung</summary>
    public int Donations { get; set; }
    /// <summary>Hat je gepanscht oder sabotiert</summary>
    public bool Tainted { get; set; }
    /// <summary>Goldmedaillen je Rebsorte (Name der Sorte im Spielstand)</summary>
    public Dictionary<string, int> GoldByGrape { get; set; } = [];

    public Player Clone()
    {
        var copy = (Player)MemberwiseClone();
        copy.CellarMaster = CellarMaster?.Clone();
        copy.Plots = Plots.Select(p => p.Clone()).ToList();
        copy.Cellar = Cellar.Select(b => b.Clone()).ToList();
        copy.Bottles = Bottles.Select(l => l.Clone()).ToList();
        copy.MarketingThisMonth = [.. MarketingThisMonth];
        copy.CompetitionEntries = [.. CompetitionEntries];
        copy.OrderOffers = OrderOffers.Select(o => o.Clone()).ToList();
        copy.Orders = Orders.Select(o => o.Clone()).ToList();
        copy.Relations = new Dictionary<string, double>(Relations);
        copy.Stats = Stats.Clone();
        copy.Milestones = new Dictionary<string, int>(Milestones);
        copy.AutoSkip = AutoSkip?.Clone();
        copy.ExportOffer = ExportOffer?.Clone();
        copy.ExportContract = ExportContract?.Clone();
        copy.Effects = Effects.Select(e => e.Clone()).ToList();
        copy.Decision = Decision?.Clone();
        copy.GoalOffers = [.. GoalOffers];
        copy.YearStart = new Dictionary<string, double>(YearStart);
        copy.Ambitions = [.. Ambitions];
        copy.AmbitionsDone = [.. AmbitionsDone];
        copy.Buildings = [.. Buildings];
        copy.GoldByGrape = new Dictionary<string, int>(GoldByGrape);
        return copy;
    }
}

/// <summary>Rückblick auf ein abgeschlossenes Jahr (Regelversion 2) – wird nie verändert, nur ersetzt</summary>
public sealed class YearReview
{
    public int Year { get; set; }
    public string GoalText { get; set; } = "";
    public bool GoalReached { get; set; }
    public double GoalPoints { get; set; }
    public double PointsGained { get; set; }
    public double NetWorthChange { get; set; }
    public double BottlesSold { get; set; }
    public int Medals { get; set; }
    public int Rank { get; set; }
    /// <summary>Note des Jahrgangs 1 (schwach) bis 5 (herausragend) aus dem Wetter der Reifemonate</summary>
    public int VintageStars { get; set; }
}

/// <summary>Wertung eines Weins bei einer Prämierung, Juror für Juror</summary>
public sealed class ShowEntry
{
    public string PlayerId { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public string Wine { get; set; } = "";
    public double BaseScore { get; set; }
    public List<double> Jury { get; set; } = [];
    public double Score { get; set; }
    public Medal? Medal { get; set; }
    public double Points { get; set; }
}

/// <summary>Ergebnis der letzten Prämierung für die Live-Anzeige – wird nie verändert, nur ersetzt</summary>
public sealed class CompetitionShow
{
    public CompetitionId Competition { get; set; }
    public int Year { get; set; }
    public List<ShowEntry> Entries { get; set; } = [];
}

public sealed class LogEntry
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string? PlayerId { get; set; }
    public string Text { get; set; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LogTone? Tone { get; set; }
}

public sealed class WeatherOdds
{
    public double Sonnig { get; set; }
    public double Wechselhaft { get; set; }
    public double Nass { get; set; }

    public double this[WeatherKind kind] => kind switch
    {
        WeatherKind.Sonnig => Sonnig,
        WeatherKind.Wechselhaft => Wechselhaft,
        _ => Nass,
    };

    public WeatherOdds Clone() => (WeatherOdds)MemberwiseClone();
}

public sealed class GameState
{
    public int Version { get; set; } = 17;
    /// <summary>1 = Regeln des Originals, 2 = erweiterte Regeln der .NET-Fassung</summary>
    public int RulesVersion { get; set; } = 1;
    public Rng Rng { get; set; } = new();
    public int Year { get; set; }
    public int Month { get; set; }
    public List<Player> Players { get; set; } = [];
    /// <summary>Index des Spielers, der gerade am Zug ist</summary>
    public int CurrentPlayer { get; set; }
    public WeatherOdds Forecast { get; set; } = new();
    public WeatherKind? LastWeather { get; set; }
    public double TargetPoints { get; set; }
    public string? WinnerId { get; set; }
    public bool Finished { get; set; }
    public AuctionState? Auction { get; set; }
    public int MonthsToAuction { get; set; }
    public bool RandomEvents { get; set; }
    public List<CellarMaster> CellarMasterCandidates { get; set; } = [];
    public Dictionary<TrendKey, Trend> Trends { get; set; } = [];
    public int BulkCrisisMonths { get; set; }
    public bool ClimateChange { get; set; }
    public int? YearLimit { get; set; }
    public string? Challenge { get; set; }
    public Difficulty Difficulty { get; set; }
    public int NextId { get; set; }
    public List<LogEntry> Log { get; set; } = [];
    /// <summary>Regelversion 2: 0 = ganzer Monat, 1 oder 2 = Hälfte eines Lesemonats</summary>
    public int HalfMonth { get; set; }
    /// <summary>Regelversion 2: Ergebnis der letzten Prämierung</summary>
    public CompetitionShow? LastShow { get; set; }
    /// <summary>Regelversion 2: Wetter der Reifemonate des laufenden Jahres (für die Jahrgangsnote)</summary>
    public List<WeatherKind> RipeningWeather { get; set; } = [];

    /// <summary>Tiefe Kopie – entspricht structuredClone im Original.</summary>
    public GameState Clone()
    {
        var copy = (GameState)MemberwiseClone();
        copy.Rng = new Rng { State = Rng.State };
        copy.Players = Players.Select(p => p.Clone()).ToList();
        copy.Forecast = Forecast.Clone();
        copy.Auction = Auction?.Clone();
        copy.CellarMasterCandidates = CellarMasterCandidates.Select(c => c.Clone()).ToList();
        copy.Trends = Trends.ToDictionary(t => t.Key, t => t.Value.Clone());
        // Logeinträge werden nie verändert, nur angehängt und abgeschnitten
        copy.Log = [.. Log];
        copy.RipeningWeather = [.. RipeningWeather];
        return copy;
    }
}

public sealed class NewPlayer
{
    public string Name { get; set; } = "";
    public RegionId RegionId { get; set; }
    public AiProfileId? Ai { get; set; }
}

public sealed class StartSetup
{
    public double? Money { get; set; }
    public double? Loan { get; set; }
    public double? TankHl { get; set; }
    public double? Reputation { get; set; }
    public StartPlots? Plots { get; set; }
}

public sealed class NewGameOptions
{
    public int Seed { get; set; }
    public List<NewPlayer> Players { get; set; } = [];
    public double? TargetPoints { get; set; }
    public bool? ClimateChange { get; set; }
    public int? YearLimit { get; set; }
    public string? Challenge { get; set; }
    public Difficulty? Difficulty { get; set; }
    public bool? RandomEvents { get; set; }
    public StartSetup? Start { get; set; }
    /// <summary>1 = Regeln des Originals (Standard), 2 = erweiterte Regeln</summary>
    public int? RulesVersion { get; set; }
}

/// <summary>Regelverstoß bei einer Aktion – die Meldung ist für den Spieler bestimmt.</summary>
public sealed class RuleException(string message) : Exception(message);
