using Winzer.Engine;

namespace Winzer.Ui;

/// <summary>
/// Der Spielstand, der gerade gezeichnet wird. <see cref="Shell.Build"/> setzt ihn vor jedem Aufbau, damit Szenen
/// Werte abfragen können, die von der Regelversion abhängen, ohne den Stand überall durchzureichen.
/// </summary>
public static class Live
{
    public static GameState? State { get; set; }

    /// <summary>Ab-Hof-Nachfrage dieses Monats – mit den erweiterten Regeln samt eigenem Preis und Gutsschänke.</summary>
    public static double Demand(Player player) =>
        State is null ? Market.MonthlyDemand(player) : RulesV2.DirectDemand(State, player);
}
