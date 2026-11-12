namespace Winzer.Engine.Data;

public sealed record QualityInfo(
    QualityLevel Level, string Name, double MinOechsle,
    /* Siegpunkte bei der amtlichen Prüfung */ double ApPoints,
    /* Fassweinpreis in €/l bei Güte 60 */ double BulkPricePerLiter,
    /* Ab-Hof-Grundpreis je Flasche */ double BottlePrice,
    /* Bonus auf die Wertung bei Prämierungen */ double CompetitionBonus,
    /* Monate im Tank bis zur Abfüllreife */ int MaturingMonths,
    /* Nur über eine besondere Lese erreichbar (Eiswein) */ bool Special = false);

public static class Quality
{
    /// <summary>Aufsteigend sortiert – die höchste erreichte Stufe gewinnt.</summary>
    public static readonly QualityInfo[] QualityLevels =
    [
        new(QualityLevel.DeutscherWein, "Deutscher Wein", 0, 0, 1.0, 3, 0, 3),
        new(QualityLevel.Qualitaetswein, "Qualitätswein", 60, 1, 1.3, 5, 0, 3),
        new(QualityLevel.Kabinett, "Kabinett", 73, 2, 1.5, 7, 2, 6),
        new(QualityLevel.Spaetlese, "Spätlese", 80, 4, 1.8, 9, 4, 6),
        new(QualityLevel.Auslese, "Auslese", 90, 8, 2.4, 14, 7, 6),
        new(QualityLevel.Beerenauslese, "Beerenauslese", 110, 15, 3.0, 25, 10, 6),
        new(QualityLevel.Trockenbeerenauslese, "Trockenbeerenauslese", 150, 30, 4.0, 45, 14, 9),
        new(QualityLevel.Eiswein, "Eiswein", 110, 25, 3.5, 40, 12, 9, Special: true),
    ];

    public static QualityInfo LevelForOechsle(double oechsle)
    {
        var result = QualityLevels[0];
        foreach (var info in QualityLevels)
        {
            if (!info.Special && oechsle >= info.MinOechsle) result = info;
        }
        return result;
    }

    public static QualityInfo QualityInfo(QualityLevel level) => QualityLevels.First(q => q.Level == level);
}
