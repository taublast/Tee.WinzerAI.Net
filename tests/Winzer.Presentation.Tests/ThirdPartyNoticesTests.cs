namespace Winzer.Presentation.Tests;

/// <summary>Die Lizenzseite liest die Übersicht, die tools/third-party-notices.ps1 schreibt.</summary>
public class ThirdPartyNoticesTests
{
    const string Sample = "WinzerAI – Lizenzhinweise für Software von Dritten\r\n\r\nEinleitung\r\n\r\nÜbersicht\r\n" +
        "  Avalonia.Angle.Windows.Natives\t2.2.0.20230930\tBSD-3-Clause\r\n" +
        "  Microsoft.Extensions.Http\t9.0.6\tMIT\r\n" +
        "  Microsoft.Extensions.Logging\t9.0.6\tMIT\r\n" +
        "  Microsoft.NETCore.App.Runtime.win-x64\t11.0.0\tMIT\r\n" +
        "  Polly\t7.2.4\tBSD-3-Clause\r\n" +
        "  SkiaSharp\t4.148.0\tMIT\r\n" +
        "  SkiaSharp.NativeAssets.Win32\t4.148.0\tMIT\r\n" +
        "  Svg.Custom\t4.9.0\tMS-PL\r\n" +
        "  Svg.Skia\t4.9.0\tMIT\r\n" +
        "\r\n=====\r\nPolly 7.2.4\r\n  Eingerückt\tmit\tTabs\r\n";

    [Fact]
    public void Liest_nur_die_Uebersicht()
    {
        var entries = ThirdPartyNotices.Parse(Sample);
        Assert.Equal(9, entries.Count);
        Assert.Equal(new NoticeEntry("Avalonia.Angle.Windows.Natives", "2.2.0.20230930", "BSD-3-Clause"), entries[0]);
        Assert.Equal("Svg.Skia", entries[^1].Id);
    }

    [Fact]
    public void Fasst_Pakete_zu_Projekten_je_Lizenz_zusammen()
    {
        var groups = ThirdPartyNotices.ByLicense(ThirdPartyNotices.Parse(Sample));
        Assert.Equal(["MIT", "BSD-3-Clause", "MS-PL"], groups.Select(g => g.License));
        Assert.Equal([".NET", "Microsoft.Extensions", "SkiaSharp", "Svg.Skia"], groups[0].Projects);
        Assert.Equal(["ANGLE", "Polly"], groups[1].Projects);
        Assert.Equal(["SVG.NET"], groups[2].Projects);
    }

    [Fact]
    public void Ohne_Datei_bleibt_die_Liste_leer()
    {
        Assert.Empty(ThirdPartyNotices.Parse("irgendein Text"));
        Assert.Null(new NoNotices().Read());
        Assert.Null(new FileNotices(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt")).Read());
    }
}
