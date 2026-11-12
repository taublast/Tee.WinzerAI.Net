using System.Diagnostics;

namespace Winzer.Ui;

/// <summary>
/// Takt für bewegte Bilder. Ein Maler, der sich bewegen will, holt die Zeit mit <see cref="Use"/>; das Fenster fragt
/// regelmäßig mit <see cref="Take"/>, ob seit dem letzten Bild jemand danach gefragt hat, und zeichnet nur dann neu.
/// Steht nichts Bewegtes auf dem Schirm, ruht das Fenster.
/// </summary>
public static class Anim
{
    static readonly Stopwatch Clock = Stopwatch.StartNew();
    static bool wanted;

    /// <summary>Abgeschaltet steht die Zeit still und nichts fordert neue Bilder an.</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>Zeit in Sekunden; meldet zugleich, dass das Bild sich bewegt.</summary>
    public static float Use()
    {
        if (!Enabled) return 0;
        wanted = true;
        return (float)Clock.Elapsed.TotalSeconds;
    }

    /// <summary>Wahr, wenn seit dem letzten Aufruf ein bewegtes Bild gezeichnet wurde.</summary>
    public static bool Take()
    {
        var result = wanted;
        wanted = false;
        return result;
    }
}
