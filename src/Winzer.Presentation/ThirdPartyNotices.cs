namespace Winzer.Presentation;

/// <summary>
/// Die Lizenzhinweise der mitgelieferten Fremdsoftware (<c>THIRD-PARTY-NOTICES.txt</c> neben der App), die
/// <c>tools/third-party-notices.ps1</c> beim Veröffentlichen erzeugt. Je Kopf eigens bereitgestellt.
/// </summary>
public interface INotices
{
    /// <summary>Inhalt der Datei; null, wenn diese Fassung keine mitbringt (Entwicklerbau, Browser)</summary>
    string? Read();
    /// <summary>Öffnet die Datei im Textbetrachter des Systems; false, wenn das nicht ging</summary>
    bool Open();
}

/// <summary>Keine Lizenzdatei vorhanden – für Tests und Köpfe ohne Dateisystem</summary>
public sealed class NoNotices : INotices
{
    public string? Read() => null;
    public bool Open() => false;
}

/// <summary>Die Lizenzdatei neben der App (Desktop)</summary>
public sealed class FileNotices(string path) : INotices
{
    public FileNotices() : this(Path.Combine(AppContext.BaseDirectory, ThirdPartyNotices.FileName)) { }

    public string? Read()
    {
        try { return File.Exists(path) ? File.ReadAllText(path) : null; }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    public bool Open()
    {
        if (!File.Exists(path)) return false;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            return false;
        }
    }
}

/// <summary>Ein Fremdpaket aus der Übersicht der Lizenzdatei</summary>
public sealed record NoticeEntry(string Id, string Version, string License);

/// <summary>Liest die Übersicht der Lizenzdatei und fasst sie für die Lizenzseite zusammen.</summary>
public static class ThirdPartyNotices
{
    public const string FileName = "THIRD-PARTY-NOTICES.txt";

    /// <summary>
    /// Die Zeilen „  Paket&lt;TAB&gt;Version&lt;TAB&gt;Lizenz“ zwischen „Übersicht“ und der nächsten Leerzeile,
    /// wie sie <c>tools/third-party-notices.ps1</c> schreibt.
    /// </summary>
    public static IReadOnlyList<NoticeEntry> Parse(string text)
    {
        var entries = new List<NoticeEntry>();
        var inOverview = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (!inOverview)
            {
                inOverview = line == "Übersicht";
                continue;
            }
            if (line.Trim().Length == 0) break;
            var parts = line.Trim().Split('\t');
            if (parts.Length == 3) entries.Add(new NoticeEntry(parts[0], parts[1], parts[2]));
        }
        return entries;
    }

    /// <summary>
    /// Kurzname des Projekts hinter einem Paket, damit die Seite „SkiaSharp“ statt sechs SkiaSharp-Pakete nennt.
    /// </summary>
    public static string Project(string id) => id switch
    {
        _ when id.StartsWith("Microsoft.NETCore.App.", StringComparison.Ordinal) => ".NET",
        _ when id.StartsWith("Microsoft.WindowsDesktop.App.", StringComparison.Ordinal) => ".NET (WPF)",
        _ when id.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal) => "Microsoft.Extensions",
        _ when id.StartsWith("Avalonia.Angle.", StringComparison.Ordinal) => "ANGLE",
        "ShimSkiaSharp" => "Svg.Skia",
        "Svg.Custom" => "SVG.NET",
        _ when id.StartsWith("Svg.", StringComparison.Ordinal) => "Svg.Skia",
        _ when id.StartsWith("Newtonsoft.", StringComparison.Ordinal) => "Json.NET",
        _ => id.Split('.')[0],
    };

    /// <summary>Projekte je Lizenz, die häufigste Lizenz zuerst; Projekte alphabetisch, „.NET“ vorn</summary>
    public static IReadOnlyList<(string License, IReadOnlyList<string> Projects)> ByLicense(IEnumerable<NoticeEntry> entries) =>
        entries.GroupBy(e => e.License)
            .Select(g => (License: g.Key, Projects: (IReadOnlyList<string>)g.Select(e => Project(e.Id)).Distinct()
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList()))
            .OrderByDescending(g => g.Projects.Count).ThenBy(g => g.License, StringComparer.Ordinal)
            .ToList();
}
