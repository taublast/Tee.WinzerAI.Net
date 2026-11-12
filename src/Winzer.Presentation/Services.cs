using Winzer.Engine;

namespace Winzer.Presentation;

/// <summary>Ein gespeicherter Spielstand</summary>
public sealed record SaveSlot(string Id, DateTime SavedAt, GameState State);

/// <summary>Ablage für Spielstände und kleine Einstellungen – je Kopf (Desktop, Browser) eigens umgesetzt.</summary>
public interface ISaveStore
{
    /// <summary>Sichert den Stand; false, wenn das nicht gelang.</summary>
    bool Write(string id, GameState state);
    void Delete(string id);
    /// <summary>Alle lesbaren Spielstände, der zuletzt gespielte zuerst</summary>
    IReadOnlyList<SaveSlot> List();
    /// <summary>Ablageort für die Anzeige; null, wenn es keinen Ordner gibt</summary>
    string? Location { get; }
    string? GetSetting(string key);
    void SetSetting(string key, string value);
}

public enum Sound { Click, Harvest, Sale, Month, Medal, Frost, Scandal }

public interface IAudio
{
    bool Muted { get; set; }
    void Play(Sound sound);
}

/// <summary>Ablage im Arbeitsspeicher – für Tests und als Rückfall.</summary>
public sealed class MemorySaveStore : ISaveStore
{
    readonly Dictionary<string, SaveSlot> slots = [];
    readonly Dictionary<string, string> settings = [];

    public bool Write(string id, GameState state)
    {
        slots[id] = new SaveSlot(id, DateTime.Now, state.Clone());
        return true;
    }

    public void Delete(string id) => slots.Remove(id);
    public IReadOnlyList<SaveSlot> List() => slots.Values.OrderByDescending(s => s.SavedAt).ToList();
    public string? Location => null;
    public string? GetSetting(string key) => settings.GetValueOrDefault(key);
    public void SetSetting(string key, string value) => settings[key] = value;
}

public sealed class SilentAudio : IAudio
{
    public bool Muted { get; set; } = true;
    public void Play(Sound sound) { }
}
