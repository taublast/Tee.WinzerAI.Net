using System.Text.Json.Serialization;

namespace Winzer.Engine;

// Meilensteine: Zwischenziele quer durchs Spiel, vor allem fürs Endlosspiel. Jeder Meilenstein bringt ein paar Punkte
// und etwas Bekanntheit und wird mit Datum in der Chronik des Weinguts festgehalten – unabhängig vom Logbuch.

public enum Tier
{
    [JsonStringEnumMemberName("bronze")] Bronze,
    [JsonStringEnumMemberName("silber")] Silber,
    [JsonStringEnumMemberName("gold")] Gold,
}

public sealed class MilestoneDef
{
    public required string Id { get; init; }
    public required string Icon { get; init; }
    public required string Name { get; init; }
    public required string Text { get; init; }
    public required Tier Tier { get; init; }
    /// <summary>Fortschritt als [erreicht, Ziel]</summary>
    public required Func<GameState, Player, (double Value, double Target)> Progress { get; init; }
}

public sealed record TierReward(double Points, double Reputation, string Icon, string Name);

public static class Milestones
{
    public static readonly Dictionary<Tier, TierReward> TierRewards = new()
    {
        [Tier.Bronze] = new(1, 1, "🥉", "Bronze"),
        [Tier.Silber] = new(3, 2, "🥈", "Silber"),
        [Tier.Gold] = new(5, 4, "🥇", "Gold"),
    };

    public static PlayerStats EmptyStats() => new()
    {
        Harvests = 0, BestLevelRank = -1, Edelsuess = 0, Sekt = 0, Barrique = 0, BottlesSold = 0,
        Medals = 0, GoldMedals = 0, OrdersDelivered = 0, ExportsCompleted = 0, AuctionsWon = 0,
    };

    static Func<GameState, Player, (double Value, double Target)> Stat(Func<PlayerStats, double> key, double target) =>
        (_, p) => (key(p.Stats), target);

    static Func<GameState, Player, (double Value, double Target)> Flag(Func<GameState, Player, bool> test) =>
        (s, p) => (test(s, p) ? 1 : 0, 1);

    /// <summary>MILESTONES des Originals (der Name Milestones gehört der Klasse).</summary>
    public static readonly List<MilestoneDef> All =
    [
        // Weinberg und Keller
        new() { Id = "erste-lese", Icon = "✂️", Name = "Erste Lese", Text = "Den ersten eigenen Jahrgang gelesen.", Tier = Tier.Bronze, Progress = Stat(s => s.Harvests, 1) },
        new()
        {
            Id = "auslese", Icon = "🍇", Name = "Auslese", Text = "Eine Lese mit mindestens Auslese-Mostgewicht eingebracht.", Tier = Tier.Silber,
            Progress = (_, p) => (Math.Min(p.Stats.BestLevelRank, Rules.LevelRank(QualityLevel.Auslese)), Rules.LevelRank(QualityLevel.Auslese)),
        },
        new() { Id = "edelsuess", Icon = "🍯", Name = "Edelsüß", Text = "Beerenauslese, Trockenbeerenauslese oder Eiswein gelesen.", Tier = Tier.Gold, Progress = Stat(s => s.Edelsuess, 1) },
        new() { Id = "sekt", Icon = "🍾", Name = "Prickelnd", Text = "Den ersten Sekt angesetzt.", Tier = Tier.Bronze, Progress = Stat(s => s.Sekt, 1) },
        new() { Id = "barrique", Icon = "🛢️", Name = "Im Eichenfass", Text = "Den ersten Wein ins Barrique gelegt.", Tier = Tier.Bronze, Progress = Stat(s => s.Barrique, 1) },
        new() { Id = "lesen-25", Icon = "🧺", Name = "Erfahrene Hand", Text = "25 Lesen eingebracht.", Tier = Tier.Silber, Progress = Stat(s => s.Harvests, 25) },

        // Wachstum
        new() { Id = "flaeche-20", Icon = "🗺️", Name = "Stattliches Gut", Text = "20 Hektar Rebfläche.", Tier = Tier.Silber, Progress = (_, p) => (Market.TotalHectares(p), 20) },
        new() { Id = "flaeche-50", Icon = "🏞️", Name = "Großweingut", Text = "50 Hektar Rebfläche.", Tier = Tier.Gold, Progress = (_, p) => (Market.TotalHectares(p), 50) },
        new() { Id = "versteigerung", Icon = "🔨", Name = "Zuschlag!", Text = "Einen Weinberg ersteigert.", Tier = Tier.Bronze, Progress = Stat(s => s.AuctionsWon, 1) },
        new()
        {
            Id = "vermoegen-1m", Icon = "💶", Name = "Millionär", Text = "Reinvermögen von 1 Mio. €.", Tier = Tier.Silber,
            Progress = (_, p) => (Js.Round(Market.NetWorth(p) / 1000), 1000),
        },
        new()
        {
            Id = "vermoegen-5m", Icon = "💰", Name = "Weinbaron", Text = "Reinvermögen von 5 Mio. €.", Tier = Tier.Gold,
            Progress = (_, p) => (Js.Round(Market.NetWorth(p) / 1000), 5000),
        },
        new()
        {
            Id = "schuldenfrei", Icon = "🧾", Name = "Schuldenfrei", Text = "Ab Jahr 3 ohne Kredit und mit mindestens 100.000 € auf dem Konto.", Tier = Tier.Silber,
            Progress = Flag((s, p) => s.Year >= 3 && p.Loan == 0 && p.Money >= 100_000),
        },

        // Markt und Ansehen
        new() { Id = "flaschen-10k", Icon = "🍷", Name = "Gut im Geschäft", Text = "10.000 Flaschen verkauft.", Tier = Tier.Bronze, Progress = Stat(s => s.BottlesSold, 10_000) },
        new() { Id = "flaschen-200k", Icon = "🚚", Name = "Marktgröße", Text = "200.000 Flaschen verkauft.", Tier = Tier.Gold, Progress = Stat(s => s.BottlesSold, 200_000) },
        new() { Id = "medaille", Icon = "🏅", Name = "Prämiert", Text = "Die erste Medaille gewonnen.", Tier = Tier.Bronze, Progress = Stat(s => s.Medals, 1) },
        new() { Id = "gold", Icon = "🥇", Name = "Goldrichtig", Text = "Die erste Goldmedaille gewonnen.", Tier = Tier.Silber, Progress = Stat(s => s.GoldMedals, 1) },
        new() { Id = "medaillen-10", Icon = "🎖️", Name = "Medaillensammler", Text = "10 Medaillen gewonnen.", Tier = Tier.Gold, Progress = Stat(s => s.Medals, 10) },
        new() { Id = "auftraege-5", Icon = "📦", Name = "Verlässlicher Lieferant", Text = "5 Kundenaufträge geliefert.", Tier = Tier.Silber, Progress = Stat(s => s.OrdersDelivered, 5) },
        new() { Id = "export", Icon = "🌍", Name = "Weltweit", Text = "Einen Exportvertrag vollständig erfüllt.", Tier = Tier.Silber, Progress = Stat(s => s.ExportsCompleted, 1) },
        new() { Id = "bekanntheit-50", Icon = "⭐", Name = "Gefragt", Text = "Bekanntheit 50.", Tier = Tier.Silber, Progress = (_, p) => (Math.Floor(p.Reputation), 50) },
        new() { Id = "bekanntheit-80", Icon = "🌟", Name = "Kultweingut", Text = "Bekanntheit 80.", Tier = Tier.Gold, Progress = (_, p) => (Math.Floor(p.Reputation), 80) },

        // Betrieb
        new() { Id = "vinothek", Icon = "🏠", Name = "Probierstube", Text = "Eine Vinothek eröffnet.", Tier = Tier.Bronze, Progress = Flag((_, p) => p.HasVinothek) },
        new() { Id = "onlineshop", Icon = "🛒", Name = "Online", Text = "Einen Online-Shop gestartet.", Tier = Tier.Bronze, Progress = Flag((_, p) => p.HasOnlineShop) },
        new() { Id = "bio", Icon = "🌿", Name = "Bio-zertifiziert", Text = "Das erste Bio-Siegel erhalten.", Tier = Tier.Silber, Progress = Flag((_, p) => p.BioLabel is not null) },
        new() { Id = "lunaris", Icon = "🌙", Name = "Biodynamisch", Text = "Das Lunaris-Siegel erhalten.", Tier = Tier.Gold, Progress = Flag((_, p) => p.BioLabel == BioLabel.Biodynamisch) },
        new()
        {
            Id = "zehn-jahre", Icon = "🎂", Name = "Zehn Jahre", Text = "Zehn Jahre lang ein Weingut geführt.", Tier = Tier.Silber,
            Progress = (s, _) => (Math.Min(10, s.Year - 1), 10),
        },
    ];

    public static bool IsReached(GameState state, Player player, MilestoneDef def)
    {
        var (value, target) = def.Progress(state, player);
        return value >= target;
    }

    /// <summary>
    /// Monatsende: neu erreichte Meilensteine eintragen und belohnen. <paramref name="silent"/> trägt nur ein – für alte
    /// Spielstände, damit dort nichts rückwirkend Punkte bringt.
    /// </summary>
    public static void CheckMilestones(GameState state, bool silent = false)
    {
        var now = Bio.GameMonth(state);
        foreach (var player in state.Players.Where(p => !p.Bankrupt).ToList())
        {
            foreach (var def in All)
            {
                if (player.Milestones.ContainsKey(def.Id) || !IsReached(state, player, def)) continue;
                player.Milestones[def.Id] = now;
                if (silent) continue;
                var reward = TierRewards[def.Tier];
                player.Points += reward.Points;
                Rules.ChangeReputation(player, reward.Reputation);
                Util.AddLog(
                    state,
                    def.Tier == Tier.Gold ? null : player.Id,
                    $"🏆 Meilenstein {reward.Icon} „{def.Name}“{(def.Tier == Tier.Gold ? $" für {player.Name}" : "")}: {def.Text} +{Util.JsNumber(reward.Points)} Punkte.",
                    LogTone.Good);
            }
        }
    }
}
