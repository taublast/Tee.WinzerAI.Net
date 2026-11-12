using System.Text.Json;
using System.Text.Json.Serialization;

namespace Winzer.Engine;

/// <summary>Serialisierung von Spielständen und Aktionen im Format des Originals (camelCase, Enums als Text).</summary>
public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() },
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public static string Serialize(GameState state) => JsonSerializer.Serialize(state, Options);

    public static GameState? DeserializeState(string json) => JsonSerializer.Deserialize<GameState>(json, Options);

    /// <summary>Liest einen Enum-Wert aus seinem Namen im Spielstand, z. B. "mueller-thurgau".</summary>
    public static T Enum<T>(string name) where T : struct, System.Enum =>
        JsonSerializer.Deserialize<T>($"\"{name}\"", Options);

    /// <summary>Name eines Enum-Werts im Spielstand.</summary>
    public static string Name<T>(T value) where T : struct, System.Enum =>
        JsonSerializer.Serialize(value, Options).Trim('"');
}
