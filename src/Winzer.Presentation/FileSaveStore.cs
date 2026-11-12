using System.Text.Json;
using Winzer.Engine;

namespace Winzer.Presentation;

/// <summary>
/// Spielstände als JSON-Dateien unter %LOCALAPPDATA%\WinzerAI\Spielstände – derselbe Ort und dasselbe
/// Format ({ format, savedAt, state }) wie in der bisherigen Desktop-App.
/// </summary>
public sealed class FileSaveStore : ISaveStore
{
    readonly string folder;
    readonly string settingsFile;
    readonly Dictionary<string, string> settings;

    public FileSaveStore(string? root = null)
    {
        var home = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinzerAI");
        folder = Path.Combine(home, "Spielstände");
        settingsFile = Path.Combine(home, "einstellungen.json");
        Directory.CreateDirectory(folder);
        settings = ReadSettings();
    }

    public string? Location => folder;

    string FileFor(string id) => Path.Combine(folder, id + ".json");

    public bool Write(string id, GameState state)
    {
        try
        {
            var savedAt = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            var content = $"{{\"format\":1,\"savedAt\":{savedAt},\"state\":{Json.Serialize(state)}}}";
            // Erst daneben schreiben, dann ersetzen – ein Absturz hinterlässt nie eine halbe Datei
            var temp = FileFor(id) + ".tmp";
            File.WriteAllText(temp, content);
            File.Move(temp, FileFor(id), overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void Delete(string id)
    {
        try
        {
            File.Delete(FileFor(id));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Die Datei bleibt liegen und erscheint weiter in der Liste
        }
    }

    public IReadOnlyList<SaveSlot> List()
    {
        var slots = new List<SaveSlot>();
        foreach (var file in Directory.EnumerateFiles(folder, "*.json"))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                var savedAt = DateTimeOffset.FromUnixTimeMilliseconds(doc.RootElement.GetProperty("savedAt").GetInt64()).LocalDateTime;
                var state = Json.DeserializeState(doc.RootElement.GetProperty("state").GetRawText());
                // Ältere Fassungen des Spielstands werden nicht angehoben
                if (state is { Version: 17 }) slots.Add(new SaveSlot(Path.GetFileNameWithoutExtension(file), savedAt, state));
            }
            catch (Exception e) when (e is IOException or JsonException or KeyNotFoundException or InvalidOperationException or UnauthorizedAccessException)
            {
                // Unlesbare Dateien werden übergangen
            }
        }
        return slots.OrderByDescending(s => s.SavedAt).ToList();
    }

    public string? GetSetting(string key) => settings.GetValueOrDefault(key);

    public void SetSetting(string key, string value)
    {
        settings[key] = value;
        try
        {
            File.WriteAllText(settingsFile, JsonSerializer.Serialize(settings));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Einstellungen gelten dann nur für diese Sitzung
        }
    }

    Dictionary<string, string> ReadSettings()
    {
        try
        {
            return File.Exists(settingsFile)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(settingsFile)) ?? []
                : [];
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
