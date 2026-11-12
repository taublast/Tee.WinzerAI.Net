using System.IO;
using System.Media;
using Winzer.Presentation;

namespace Winzer.Desktop;

/// <summary>
/// Kurze Klänge ohne Audiodateien: Jeder Klang ist eine Folge von Tönen, die einmal als WAV im Speicher
/// erzeugt und dann abgespielt wird – wie die Web-Audio-Klänge des Originals.
/// </summary>
public sealed class SynthAudio : IAudio
{
    const int SampleRate = 22_050;

    /// <summary>Ton: Frequenz in Hz, Dauer in Sekunden, Lautstärke 0–1</summary>
    readonly record struct Note(double Frequency, double Seconds, double Volume = 0.25);

    static readonly Dictionary<Sound, Note[]> Scores = new()
    {
        [Sound.Click] = [new(660, 0.04, 0.12)],
        [Sound.Harvest] = [new(392, 0.08), new(494, 0.08), new(587, 0.14)],
        [Sound.Sale] = [new(880, 0.06), new(1175, 0.12)],
        [Sound.Month] = [new(330, 0.10, 0.18), new(262, 0.16, 0.18)],
        [Sound.Medal] = [new(523, 0.10), new(659, 0.10), new(784, 0.10), new(1047, 0.28)],
        [Sound.Frost] = [new(1568, 0.07, 0.15), new(1319, 0.07, 0.15), new(1047, 0.2, 0.15)],
        [Sound.Scandal] = [new(196, 0.2), new(185, 0.2), new(147, 0.4)],
    };

    readonly Dictionary<Sound, SoundPlayer> players = [];

    public bool Muted { get; set; }

    public void Play(Sound sound)
    {
        if (Muted) return;
        try
        {
            if (!players.TryGetValue(sound, out var player))
            {
                player = new SoundPlayer(new MemoryStream(Render(Scores[sound])));
                player.Load();
                players[sound] = player;
            }
            player.Play();
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or TimeoutException)
        {
            // Kein Audiogerät: Das Spiel bleibt stumm
        }
    }

    static byte[] Render(Note[] notes)
    {
        var samples = new List<short>();
        foreach (var note in notes)
        {
            var count = (int)(note.Seconds * SampleRate);
            for (var i = 0; i < count; i++)
            {
                // Weicher Ein- und Ausklang gegen Knacken
                var envelope = Math.Min(1, Math.Min(i / 200.0, (count - i) / (count * 0.6)));
                samples.Add((short)(Math.Sin(2 * Math.PI * note.Frequency * i / SampleRate) * note.Volume * envelope * short.MaxValue));
            }
        }
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        var dataSize = samples.Count * 2;
        writer.Write("RIFF"u8);
        writer.Write(36 + dataSize);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1); // PCM
        writer.Write((short)1); // Mono
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(dataSize);
        foreach (var sample in samples) writer.Write(sample);
        writer.Flush();
        return stream.ToArray();
    }
}
