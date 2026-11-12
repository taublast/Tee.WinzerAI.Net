namespace Winzer.Engine.Data;

public sealed record Grape(
    GrapeId Id, string Name, GrapeColor Color,
    /* Ertrag in hl/ha bei voll ertragsfähigen Reben */ double YieldPerHa,
    /* Teiler für den Reifezuwachs: > 1 reift langsamer */ double RipeningNeed,
    /* Mostgewicht, dem sich die Traube ohne Botrytis annähert */ double MaxOechsle,
    /* 1–10 */ double QualityPotential,
    /* 0–1: Anfälligkeit für Fäulnis bei nassem Herbst */ double RotSusceptibility,
    /* 0–1: Anfälligkeit für Pilzkrankheiten */ double DiseaseSusceptibility,
    double PlantCostPerHa,
    /* Pilzwiderstandsfähige Neuzüchtung */ bool Piwi = false);

public static class Grapes
{
    /// <summary>GRAPES des Originals, in derselben Reihenfolge.</summary>
    public static readonly Dictionary<GrapeId, Grape> All = new()
    {
        [GrapeId.MuellerThurgau] = new(GrapeId.MuellerThurgau, "Müller-Thurgau", GrapeColor.Weiss, 110, 0.85, 90, 4, 0.8, 0.5, 18_000),
        [GrapeId.Silvaner] = new(GrapeId.Silvaner, "Silvaner", GrapeColor.Weiss, 95, 1.0, 105, 6, 0.5, 0.5, 20_000),
        [GrapeId.Riesling] = new(GrapeId.Riesling, "Riesling", GrapeColor.Weiss, 80, 1.15, 125, 9, 0.3, 0.5, 22_000),
        [GrapeId.Weissburgunder] = new(GrapeId.Weissburgunder, "Weißburgunder", GrapeColor.Weiss, 85, 1.0, 110, 7, 0.5, 0.5, 22_000),
        [GrapeId.Grauburgunder] = new(GrapeId.Grauburgunder, "Grauburgunder", GrapeColor.Weiss, 85, 1.0, 115, 7, 0.8, 0.5, 22_000),
        [GrapeId.Spaetburgunder] = new(GrapeId.Spaetburgunder, "Spätburgunder", GrapeColor.Rot, 70, 1.15, 115, 9, 0.8, 0.8, 25_000),
        [GrapeId.Dornfelder] = new(GrapeId.Dornfelder, "Dornfelder", GrapeColor.Rot, 120, 0.85, 92, 4, 0.5, 0.5, 18_000),
        [GrapeId.Trollinger] = new(GrapeId.Trollinger, "Trollinger", GrapeColor.Rot, 120, 1.15, 90, 3, 0.5, 0.5, 18_000),
        [GrapeId.Lemberger] = new(GrapeId.Lemberger, "Lemberger", GrapeColor.Rot, 90, 1.15, 105, 7, 0.3, 0.5, 22_000),
        [GrapeId.Regent] = new(GrapeId.Regent, "Regent (PIWI)", GrapeColor.Rot, 85, 1.0, 100, 5, 0.3, 0.1, 20_000, Piwi: true),
        [GrapeId.Solaris] = new(GrapeId.Solaris, "Solaris (PIWI)", GrapeColor.Weiss, 75, 0.75, 110, 5, 0.3, 0.1, 20_000, Piwi: true),
    };
}
