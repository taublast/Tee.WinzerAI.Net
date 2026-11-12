using System.Globalization;
using Winzer.Engine.Data;

namespace Winzer.Engine;

public sealed record ChallengeScenario(
    string Id, string Icon, string Name, string Description,
    int Years, int Opponents, bool ClimateChange, StartSetup? Start = null);

public sealed record DailyChallenge(string Date, ChallengeScenario Scenario, NewGameOptions Options);

/// <summary>Tages-Challenge (challenge.ts).</summary>
public static class Challenge
{
    /// <summary>Täglich wechselnde Ausgangslagen – sie gelten für alle Weingüter der Partie, auch für die Gegner.</summary>
    public static readonly ChallengeScenario[] ChallengeScenarios =
    [
        new("klassiker", "🍷", "Klassisches Duell",
            "Ganz normaler Start gegen zwei Computer-Winzer. Wer holt in zehn Jahren die meisten Punkte?",
            10, 2, true),
        new("schuldenberg", "💸", "Schuldenberg",
            "Der Hof ist mit 60.000 € Kredit belastet, auf dem Konto liegen nur 30.000 €. Erst konsolidieren, dann glänzen.",
            10, 2, true, new StartSetup { Money = 30_000, Loan = 60_000 }),
        new("erbe", "📜", "Das große Erbe",
            "Alle zehn Hektar tragen alte Reben – aber das Erbe hat die Kasse geleert: nur 40.000 € Startkapital.",
            8, 2, true, new StartSetup { Money = 40_000, Plots = StartPlots.Planted }),
        new("brache", "🌱", "Neuanfang auf der Brache",
            "Kein einziger Rebstock, dafür 160.000 € Kapital. Was pflanzt du – und wie überbrückst du die Jahre ohne Ernte?",
            12, 2, true, new StartSetup { Money = 160_000, Plots = StartPlots.Fallow }),
        new("sprint", "⚡", "Fünf-Jahres-Sprint",
            "Nur fünf Jahre Zeit gegen drei Gegner. Jede Lese zählt.",
            5, 3, true),
        new("name", "⭐", "Bekannter Name",
            "Dein Weingut ist schon bekannt (Bekanntheit 35), aber der Keller fasst nur 150 hl.",
            10, 2, true, new StartSetup { Reputation = 35, TankHl = 150 }),
        new("kaltzeit", "❄️", "Wie in alten Zeiten",
            "Ohne Klimawandel: kühlere Jahrgänge, später reif – aber gute Chancen auf Eiswein.",
            10, 2, false),
        new("marathon", "🏃", "Marathon",
            "Fünfzehn Jahre gegen drei Gegner. Hier gewinnt, wer langfristig investiert.",
            15, 3, true),
    ];

    /// <summary>Datum als JJJJ-MM-TT in Ortszeit. Ohne Angabe: heute (einziger Zugriff auf die Systemuhr).</summary>
    public static string TodayKey(DateTime? date = null)
    {
        var day = date ?? DateTime.Now;
        return $"{day.Year.ToString(CultureInfo.InvariantCulture)}-{day.Month:00}-{day.Day:00}";
    }

    /// <summary>Fortlaufende Tagesnummer – so wechselt das Szenario garantiert von Tag zu Tag.</summary>
    static int DayNumber(string date)
    {
        var parts = date.Split('-');
        var y = int.Parse(parts[0], CultureInfo.InvariantCulture);
        var m = int.Parse(parts[1], CultureInfo.InvariantCulture);
        var d = int.Parse(parts[2], CultureInfo.InvariantCulture);
        // Wie Date.UTC(y, m - 1, d): Überläufe bei Monat und Tag rollen weiter
        var utc = new DateTime(y, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(m - 1).AddDays(d - 1);
        return (int)Js.Round((utc - DateTime.UnixEpoch).TotalMilliseconds / 86_400_000);
    }

    /// <summary>
    /// Tages-Challenge: Alle spielen am selben Tag dieselbe Partie – gleiches Szenario, Gebiet, Wetter und dieselben
    /// Gegner. Gewertet werden die Punkte am Ende der vorgegebenen Jahre.
    /// </summary>
    public static DailyChallenge DailyChallenge(string date, string playerName = "Du")
    {
        var seed = Rng.SeedFromString($"winzer-{date}");
        // Eigener Zufall für die Aufstellung, damit Wetter und Ereignisse allein am Seed hängen
        var setup = new Rng { State = Rng.SeedFromString($"winzer-setup-{date}") };
        T Pick<T>(List<T> items)
        {
            var index = (int)Math.Floor(Rng.NextFloat(setup) * items.Count);
            var item = items[index];
            items.RemoveAt(index);
            return item;
        }

        var scenario = ChallengeScenarios[DayNumber(date) % ChallengeScenarios.Length];
        var regionId = Pick(Regions.All.Keys.ToList());
        var profiles = Ai.AiEstateNames.Keys.ToList();
        var opponents = new List<AiProfileId>();
        for (var i = 0; i < scenario.Opponents; i++) opponents.Add(Pick(profiles));

        var players = new List<NewPlayer> { new() { Name = playerName, RegionId = regionId } };
        foreach (var ai in opponents) players.Add(new NewPlayer { Name = Ai.AiEstateNames[ai], RegionId = regionId, Ai = ai });

        return new DailyChallenge(
            date,
            scenario,
            new NewGameOptions
            {
                Seed = seed,
                TargetPoints = Rules.NoPointTarget,
                YearLimit = scenario.Years,
                ClimateChange = scenario.ClimateChange,
                Challenge = date,
                Difficulty = Difficulty.Normal,
                Start = scenario.Start,
                Players = players,
            });
    }
}
