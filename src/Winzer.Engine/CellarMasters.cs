using Winzer.Engine.Data;

namespace Winzer.Engine;

public sealed record SkillInfo(string Icon, string Name, string Text);

/// <summary>
/// Kellermeister mit Talent und Fähigkeiten: Er hebt die Güte jeder Lese, manche Weinarten (Sekt, Barrique)
/// gibt es nur mit dem passenden Können. Jeden Januar bewerben sich neue Kandidaten. (cellarmaster.ts)
/// </summary>
public static class CellarMasters
{
    public static readonly Dictionary<CellarSkill, SkillInfo> Skills = new()
    {
        [CellarSkill.Sekt] = new("🍾", "Sekt", "beherrscht die Flaschengärung – Voraussetzung für Sekt"),
        [CellarSkill.Barrique] = new("🛢️", "Barrique", "kennt den Ausbau im Eichenfass – Voraussetzung für Barriqueweine"),
        [CellarSkill.Rotwein] = new("🍷", "Rotwein", "+3 Güte bei roten Sorten"),
        [CellarSkill.Weisswein] = new("🥂", "Weißwein", "+3 Güte bei weißen Sorten"),
        [CellarSkill.Edelsuess] = new("🍯", "Edelsüß", "+4 Güte ab Auslese-Mostgewicht (95 °Oe)"),
        [CellarSkill.Sparsam] = new("💡", "Sparsam", "25 % weniger Kosten fürs Abfüllen und die Sektherstellung"),
    };

    const double ColorBonus = 3;
    const double SweetBonus = 4;
    const double SweetOechsle = 95;
    const double ThriftFactor = 0.75;

    static readonly string[] Names =
    [
        "Karl Brenner", "Marie Lehmann", "Jonas Weidinger", "Lena Hofmann", "Paul Kessler", "Sophie Rieger", "Anton Bauer",
        "Clara Winkler", "Felix Gruber", "Hannah Scholz", "Matthias Engel", "Julia Kranz", "Tobias Hahn", "Eva Lindner",
        "Ralf Schreiber", "Nora Feldmann", "Georg Maurer", "Ines Kaufmann",
    ];

    public static bool HasSkill(Player player, CellarSkill skill) => player.CellarMaster?.Skills.Contains(skill) ?? false;

    /// <summary>Dienstjahre bringen Erfahrung: +1 Güte alle vier Jahre, höchstens +2</summary>
    public static double Experience(CellarMaster master, int year)
    {
        if (master.HiredYear is null) return 0;
        return Math.Min(Economy.CellarMasterMaxExperience, Math.Floor((double)(year - master.HiredYear.Value) / Economy.CellarMasterYearsPerExperience));
    }

    /// <summary>Güte-Zuschlag des Kellermeisters für eine Lese dieser Sorte mit diesem Mostgewicht</summary>
    public static double CellarMasterBonus(Player player, GrapeId grape, double oechsle, int year)
    {
        var master = player.CellarMaster;
        if (master is null) return 0;
        var color = Grapes.All[grape].Color;
        return
            master.Talent +
            Experience(master, year) +
            (color == GrapeColor.Rot && master.Skills.Contains(CellarSkill.Rotwein) ? ColorBonus : 0) +
            (color != GrapeColor.Rot && master.Skills.Contains(CellarSkill.Weisswein) ? ColorBonus : 0) +
            (oechsle >= SweetOechsle && master.Skills.Contains(CellarSkill.Edelsuess) ? SweetBonus : 0);
    }

    /// <summary>Faktor auf Flaschen- und Sektkosten</summary>
    public static double BottlingCostFactor(Player player) => HasSkill(player, CellarSkill.Sparsam) ? ThriftFactor : 1;

    public static double SalaryFor(double talent, List<CellarSkill> skills)
    {
        var raw = Economy.CellarMasterBaseSalary + talent * Economy.CellarMasterSalaryPerTalent + skills.Count * Economy.CellarMasterSalaryPerSkill;
        return Js.Round(raw / 100) * 100;
    }

    /// <summary>Neue Bewerber zum Jahresbeginn: Talent 1–5, ein bis drei Fähigkeiten</summary>
    public static void RefreshCandidates(GameState state)
    {
        double Roll() => Rng.NextFloat(state.Rng);
        var taken = new HashSet<string?>(state.Players.Select(p => p.CellarMaster?.Name));
        var names = Names.Where(n => !taken.Contains(n)).ToList();
        var all = Skills.Keys.ToList();
        var candidates = new List<CellarMaster>();
        for (var i = 0; i < Economy.CellarMasterCandidates; i++)
        {
            var nameIndex = (int)Math.Floor(Roll() * names.Count);
            var name = names[nameIndex];
            names.RemoveAt(nameIndex);
            var talent = 1 + Math.Floor(Roll() * 5);
            var pool = new List<CellarSkill>(all);
            var count = 1 + (int)Math.Floor(Roll() * 3);
            var skills = new List<CellarSkill>();
            for (var k = 0; k < count; k++)
            {
                var skillIndex = (int)Math.Floor(Roll() * pool.Count);
                skills.Add(pool[skillIndex]);
                pool.RemoveAt(skillIndex);
            }
            candidates.Add(new CellarMaster { Name = name, Talent = talent, Skills = skills, Salary = SalaryFor(talent, skills), HiredYear = null });
        }
        state.CellarMasterCandidates = candidates;
    }

    public static string? ValidateHire(GameState state, Player player, int candidate)
    {
        var master = candidate >= 0 && candidate < state.CellarMasterCandidates.Count ? state.CellarMasterCandidates[candidate] : null;
        if (master is null) return "Diese Bewerbung liegt nicht vor.";
        if (player.Money < master.Salary) return $"Nicht genug Geld für das erste Gehalt ({Util.FormatEuro(master.Salary)}).";
        return null;
    }

    /// <summary>Einstellen; ein bisheriger Kellermeister geht.</summary>
    public static void Hire(GameState state, Player player, int candidate)
    {
        var master = state.CellarMasterCandidates[candidate];
        state.CellarMasterCandidates.RemoveAt(candidate);
        var previous = player.CellarMaster;
        var hired = master.Clone();
        hired.HiredYear = state.Year;
        player.CellarMaster = hired;
        Util.AddLog(
            state,
            player.Id,
            $"{master.Name} ist dein neuer Kellermeister ({Util.FormatEuro(master.Salary)}/Monat){(previous is not null ? $" und löst {previous.Name} ab" : "")}.");
    }

    public static void Fire(GameState state, Player player)
    {
        var master = player.CellarMaster!;
        player.CellarMaster = null;
        Util.AddLog(state, player.Id, $"Kellermeister {master.Name} ist entlassen.");
    }

    /// <summary>Kurzbeschreibung für Logbuch und Oberfläche</summary>
    public static string DescribeMaster(CellarMaster master, int year)
    {
        var skills = string.Join(", ", master.Skills.Select(s => Skills[s].Name));
        var bonus = master.Talent + Experience(master, year);
        return $"+{Util.JsNumber(bonus)} Güte{(skills != "" ? $" · {skills}" : "")}";
    }
}
