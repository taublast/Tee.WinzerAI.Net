// Balancing-Report (scripts/balance.sim.ts des Originals):
// Jedes KI-Profil spielt in jedem Anbaugebiet mehrere Partien allein, danach treten alle Profile gegeneinander an.
//
// Aufruf: dotnet run --project tools/Winzer.Sim -c Release [-- --seeds N --years N]
//   --seeds N  Anzahl Seeds je Kombination (Standard 3; die Schwierigkeitsgrade laufen mit doppelt so vielen)
//   --years N  Spieljahre je Partie (Standard 15)
//   --rules N  Regelversion: 1 = Original (Standard), 2 = erweiterte Regeln
using System.Text;
using Winzer.Engine;
using Winzer.Engine.Data;

Console.OutputEncoding = Encoding.UTF8;

var years = 15;
var seedCount = 3;
for (var i = 0; i < args.Length; i++)
{
    if (args[i] is "--seeds" or "--years" or "--rules")
    {
        if (i + 1 >= args.Length || !int.TryParse(args[i + 1], out var value) || value < 1)
        {
            Console.Error.WriteLine($"{args[i]} erwartet eine ganze Zahl ab 1.");
            return 1;
        }
        if (args[i] == "--rules") Simulate.DefaultRulesVersion = value;
        else if (args[i] == "--seeds") seedCount = value;
        else years = value;
        i++;
    }
    else
    {
        Console.Error.WriteLine($"Unbekanntes Argument: {args[i]}\nAufruf: Winzer.Sim [--seeds N] [--years N] [--rules 1|2]");
        return 1;
    }
}

var seeds = Enumerable.Range(1, seedCount).ToArray();
// Für die Schwierigkeitsgrade mehr Seeds: Dieselben Seeds wirken in allen Gebieten ähnlich (Wetter, Ereignisse, Trends),
// mit nur drei schwankten Pleiten zwischen 2 und 16 von 78 Partien.
var difficultySeeds = Enumerable.Range(1, seedCount * 2).ToArray();
var profiles = Ai.AiProfiles.Keys.ToArray();
var regions = Regions.All.Keys.ToArray();

static double Avg(IEnumerable<double> values)
{
    var list = values.ToList();
    return Js.Round(list.Sum() / Math.Max(1, list.Count));
}
static string Pad(object value, int width) => (value is double d ? Util.JsNumber(d) : value.ToString() ?? "").PadLeft(width);

// Solo-Partien je Profil und Gebiet
{
    Console.WriteLine("Solo-Partien je Profil und Gebiet");
    var lines = new List<string>
    {
        "Profil".PadRight(20) + "Gebiet".PadRight(22) + Pad("P. J5", 7) + Pad("P. J10", 8) + Pad($"P. J{years}", 8) + Pad("ha", 5) +
        Pad("Vermögen", 12) + Pad("pleite", 8),
    };
    foreach (var ai in profiles)
    {
        foreach (var regionId in regions)
        {
            var runs = seeds
                .Select(seed => Simulate.Run(new SimOptions { Seed = seed, Years = years, Players = [new SimPlayer { RegionId = regionId, Ai = ai }] }).Snapshots)
                .ToList();
            List<PlayerSnapshot> At(int year) => runs.Where(s => year - 1 < s.Count).Select(s => s[year - 1].Players[0]).ToList();
            var final = At(years);
            // Eine Insolvenz beendet die Partie vorzeitig – dann fehlen Jahresstände.
            var bankrupt = runs.Count(s => s.Count < years || s[^1].Players[0].Bankrupt);
            lines.Add(
                Ai.AiProfiles[ai].Name.PadRight(20) +
                Regions.All[regionId].Name.PadRight(22) +
                Pad(Avg(At(5).Select(p => p.Points)), 7) +
                Pad(Avg(At(10).Select(p => p.Points)), 8) +
                Pad(Avg(final.Select(p => p.Points)), 8) +
                Pad(Avg(final.Select(p => p.Hectares)), 5) +
                Pad(Util.FormatNumber(Avg(final.Select(p => p.NetWorth))), 12) +
                Pad($"{bankrupt}/{seeds.Length}", 8));
        }
    }
    Console.WriteLine(string.Join("\n", lines));
    Console.WriteLine();
}

// Alle Profile gegeneinander
{
    Console.WriteLine("Alle Profile gegeneinander");
    foreach (var regionId in regions)
    {
        var wins = new Dictionary<AiProfileId, int>();
        var points = new Dictionary<AiProfileId, List<double>>();
        foreach (var seed in seeds)
        {
            var snapshots = Simulate.Run(new SimOptions
            {
                Seed = seed, Years = years, Players = profiles.Select(ai => new SimPlayer { RegionId = regionId, Ai = ai }).ToList(),
            }).Snapshots;
            var final = snapshots[^1].Players;
            var best = final.OrderByDescending(p => p.Points).First(); // stabil wie Array.sort
            wins[best.Ai] = wins.GetValueOrDefault(best.Ai) + 1;
            foreach (var p in final)
            {
                if (!points.TryGetValue(p.Ai, out var list)) points[p.Ai] = list = [];
                list.Add(p.Points);
            }
        }
        Console.WriteLine(
            $"{Regions.All[regionId].Name}: " +
            string.Join(" · ", profiles.Select(ai =>
                $"{Ai.AiProfiles[ai].Name} {Util.JsNumber(Avg(points[ai]))} P. ({wins.GetValueOrDefault(ai)} Siege)")));
    }
    Console.WriteLine();
}

// Schwierigkeitsgrade (KI unter den Bedingungen der Menschen)
{
    Console.WriteLine("Schwierigkeitsgrade (KI unter den Bedingungen der Menschen)");
    var lines = new List<string> { "Grad".PadRight(12) + Pad("P. J10", 8) + Pad($"P. J{years}", 8) + Pad("Vermögen", 12) + Pad("pleite", 8) };
    var levels = new List<Difficulty?> { null };
    levels.AddRange(Economy.Difficulties.Keys.Select(d => (Difficulty?)d));
    foreach (var difficulty in levels)
    {
        var finals = new List<(double J10, double Final, double Worth, bool Bankrupt)>();
        foreach (var ai in new[] { AiProfileId.Tradition, AiProfileId.Bio })
        {
            foreach (var regionId in regions)
            {
                foreach (var seed in difficultySeeds)
                {
                    var snapshots = Simulate.Run(new SimOptions
                    {
                        Seed = seed, Years = years, Difficulty = difficulty, Players = [new SimPlayer { RegionId = regionId, Ai = ai }],
                    }).Snapshots;
                    PlayerSnapshot? At(int year) => year - 1 < snapshots.Count ? snapshots[year - 1].Players[0] : null;
                    finals.Add((
                        At(10)?.Points ?? 0,
                        At(years)?.Points ?? 0,
                        At(years)?.NetWorth ?? 0,
                        snapshots.Count < years || snapshots[^1].Players[0].Bankrupt));
                }
            }
        }
        lines.Add(
            (difficulty is { } level ? Economy.Difficulties[level].Name : "ohne (KI)").PadRight(12) +
            Pad(Avg(finals.Select(f => f.J10)), 8) +
            Pad(Avg(finals.Select(f => f.Final)), 8) +
            Pad(Util.FormatNumber(Avg(finals.Select(f => f.Worth))), 12) +
            Pad($"{finals.Count(f => f.Bankrupt)}/{finals.Count}", 8));
    }
    Console.WriteLine(string.Join("\n", lines));
}

return 0;
