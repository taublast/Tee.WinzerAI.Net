using System.Diagnostics;

namespace Winzer.Ui;

/// <summary>
/// Takt für bewegte Bilder. Die Oberfläche wird bei jeder Änderung neu aufgebaut und merkt sich nichts – was sich
/// bewegt, wird aus der Zeit berechnet:
/// <list type="bullet">
/// <item><see cref="Use"/>: Zeit für Maler, die sich dauernd bewegen (Wetter).</item>
/// <item><see cref="Tween"/>: ein Wert, der weich auf sein Ziel zuläuft (Konto, Füllstand).</item>
/// <item><see cref="Since"/>: Zeit seit dem ersten Auftreten von etwas (Aufdecken der Wertungen).</item>
/// <item><see cref="Drive"/>: Schritt je Bild für Steuerelemente, die keine Maler sind (Texte, Deckkraft).</item>
/// </list>
/// Das Fenster fragt regelmäßig mit <see cref="Step"/>, ob neu gezeichnet werden muss; steht nichts Bewegtes auf dem
/// Schirm, ruht es.
/// </summary>
public static class Anim
{
    static readonly Stopwatch Clock = Stopwatch.StartNew();
    static readonly List<Func<bool>> Drivers = [];
    static readonly Dictionary<string, (double From, double To, double Start, double Seconds)> Tweens = [];
    static readonly Dictionary<string, double> Marks = [];
    static bool wanted;
    // Ein Übergang läuft (Zählen, Aufdecken, Überblenden): Er muss zu Ende laufen, auch wenn das Fenster nicht vorn liegt
    static bool busy;

    /// <summary>Abgeschaltet steht die Zeit still, Werte springen sofort ans Ziel und nichts fordert neue Bilder an.</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>Zählt bei jedem <see cref="Reset"/> hoch – wer eigene Übergänge merkt, erkennt daran eine neue Partie.</summary>
    public static int Epoch { get; private set; }

    public static double Now => Enabled ? Clock.Elapsed.TotalSeconds : 0;

    /// <summary>Zeit in Sekunden; meldet zugleich, dass das Bild sich bewegt.</summary>
    public static float Use()
    {
        if (!Enabled) return 0;
        wanted = true;
        return (float)Clock.Elapsed.TotalSeconds;
    }

    /// <summary>
    /// Wert, der in <paramref name="seconds"/> weich auf <paramref name="target"/> zuläuft. Beim ersten Auftreten des
    /// Schlüssels steht er sofort am Ziel – außer <paramref name="start"/> nennt einen Anfangswert.
    /// </summary>
    public static double Tween(string key, double target, double seconds = 0.7, double? start = null)
    {
        if (!Enabled) return target;
        var now = Now;
        if (!Tweens.TryGetValue(key, out var tween))
        {
            tween = (start ?? target, target, now, seconds);
            Tweens[key] = tween;
        }
        else if (tween.To != target)
        {
            tween = (Value(tween, now), target, now, seconds);
            Tweens[key] = tween;
        }
        if (now - tween.Start < tween.Seconds && tween.From != tween.To) busy = true;
        return Value(tween, now);
    }

    static double Value((double From, double To, double Start, double Seconds) tween, double now)
    {
        var p = tween.Seconds <= 0 ? 1 : Math.Clamp((now - tween.Start) / tween.Seconds, 0, 1);
        return tween.From + (tween.To - tween.From) * EaseOut(p);
    }

    /// <summary>Schnell los, sanft ankommen</summary>
    public static double EaseOut(double p) => 1 - Math.Pow(1 - Math.Clamp(p, 0, 1), 3);

    /// <summary>
    /// Sekunden seit dem ersten Aufruf mit diesem Schlüssel. Solange weniger als <paramref name="lasting"/> vergangen
    /// sind, wird weiter gezeichnet.
    /// </summary>
    public static double Since(string key, double lasting = 0)
    {
        if (!Enabled) return 1e6;
        var now = Now;
        if (!Marks.TryGetValue(key, out var mark))
        {
            mark = now;
            Marks[key] = now;
        }
        if (now - mark < lasting) busy = true;
        return now - mark;
    }

    /// <summary>
    /// Schritt, der bei jedem Bild läuft, bis er <c>false</c> liefert oder die Oberfläche neu aufgebaut wird. Er läuft
    /// einmal sofort, damit das Element schon im ersten Bild richtig steht.
    /// </summary>
    public static void Drive(Func<bool> step)
    {
        if (!step() || !Enabled) return;
        Drivers.Add(step);
        busy = true;
    }

    /// <summary>Vor jedem Neuaufbau: Die alten Elemente gibt es nicht mehr.</summary>
    public static void BeginBuild() => Drivers.Clear();

    /// <summary>Neue Partie oder Hauptmenü: Übergänge beginnen von vorn, nichts läuft von alten Werten los.</summary>
    public static void Reset()
    {
        Drivers.Clear();
        Tweens.Clear();
        Marks.Clear();
        Epoch++;
    }

    /// <summary>Beim letzten <see cref="Step"/> lief ein Übergang – das Fenster zeichnet dann in kürzerem Takt.</summary>
    public static bool InTransition { get; private set; }

    /// <summary>Meldet einen laufenden Übergang, der ein weiteres Bild braucht.</summary>
    public static void Busy()
    {
        if (Enabled) busy = true;
    }

    /// <summary>
    /// Führt die laufenden Schritte aus; wahr, wenn das Fenster neu zeichnen soll. Dauerbewegung (Wetter) zählt nur,
    /// wenn <paramref name="ambient"/> gesetzt ist – ein Fenster im Hintergrund lässt sie ruhen, Übergänge aber laufen aus.
    /// </summary>
    public static bool Step(bool ambient = true)
    {
        var running = false;
        for (var i = Drivers.Count - 1; i >= 0; i--)
        {
            if (Drivers[i]()) running = true;
            else Drivers.RemoveAt(i);
        }
        InTransition = busy || running;
        var result = InTransition || (ambient && wanted);
        wanted = false;
        busy = false;
        return result;
    }
}
