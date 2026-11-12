using SkiaSharp;
using Winzer.Engine;
using Winzer.Engine.Data;

namespace Winzer.Ui.Scenes;

/// <summary>Malerei für Dorf und Versteigerung: Dorfplatz, Gesichter der Nachbarn, Parzellen, Hammer.</summary>
internal static class VillagePaint
{
    /// <summary>Grundlinie der Häuserzeile auf der Bühne</summary>
    public const float Baseline = 372;
    public const float ChurchX = 640;

    /// <summary>Erkennungsfarben der Weingüter in Reihenfolge der Spieler</summary>
    public static readonly string[] EstateColors = ["#8e2440", "#2f6f8f", "#4d7a2a", "#c8962e", "#6a4a8a", "#b5562e", "#3a7a7a", "#7a6a5a"];

    public sealed record House(float Cx, float Width, string Color, bool Own, bool Bankrupt);

    sealed record Palette(string SkyTop, string SkyLow, string Hill1, string Hill2, string Vine, string Ground1, string Ground2, string Tree, bool Snow, bool Lit);

    static Palette For(int month) => month switch
    {
        12 or 1 or 2 => new("#42547a", "#dfe6ee", "#c3ced8", "#98a5b3", "#8793a0", "#d3d6d8", "#9a9690", "#8a7a6a", true, true),
        3 or 4 or 5 => new("#4f86c2", "#e6f0d8", "#a4c672", "#6f9a4a", "#4f7a34", "#d2bd97", "#9a8464", "#86b955", false, false),
        6 or 7 or 8 => new("#3a7cc4", "#f6ecc4", "#93b24c", "#5f8236", "#3f6424", "#d6bf95", "#9c8562", "#4f8a2e", false, false),
        _ => new("#34446a", "#f3c483", "#b8924a", "#8a6a35", "#7a4a26", "#cdb088", "#8f7758", "#c8742e", false, true),
    };

    /// <summary>Plätze der Häuser: gleichmäßig über die Zeile verteilt, die Kirche steht in der Mitte.</summary>
    public static List<(float Cx, float Width)> HouseSlots(int count)
    {
        var slots = new List<(float, float)>();
        var left = count / 2;
        var right = count - left;
        var spacing = Math.Min(240f, 540f / Math.Max(1, right));
        var width = Math.Min(160, spacing - 26);
        for (var i = 0; i < left; i++) slots.Add((ChurchX - (left - i) * spacing, width));
        for (var i = 0; i < right; i++) slots.Add((ChurchX + (i + 1) * spacing, width));
        return slots;
    }

    public static void Square(SKCanvas c, float w, float h, GameState state, List<House> houses)
    {
        var p = For(state.Month);
        var wet = state.LastWeather == WeatherKind.Nass;
        using (var sky = Draw.VerticalGradient(0, Baseline, (0, p.SkyTop), (1, p.SkyLow))) c.DrawRect(0, 0, w, Baseline + 10, sky);
        if (!wet)
        {
            using var glow = new SKPaint
            {
                IsAntialias = true,
                Shader = SKShader.CreateRadialGradient(new SKPoint(1060, 130), 220,
                    [SKColor.Parse("#fff3c4"), SKColor.Parse("#b0ffd27a"), SKColor.Parse("#00ffd27a")], [0, 0.3f, 1], SKShaderTileMode.Clamp),
            };
            c.DrawCircle(1060, 130, 220, glow);
            using var disc = Draw.Fill("#fff0bd");
            c.DrawCircle(1060, 130, 40, disc);
        }
        using (var cloud = Draw.Fill(SKColors.White.WithAlpha((byte)(wet ? 170 : 90))))
        {
            c.DrawOval(250, 120, 170, 22, cloud);
            c.DrawOval(360, 142, 120, 15, cloud);
            c.DrawOval(900, 96, 150, 18, cloud);
            if (wet) c.DrawOval(1100, 130, 240, 30, cloud);
        }

        // Weinberge hinter dem Dorf
        using (var hillPath = SKPath.ParseSvgPathData(FormattableString.Invariant($"M0 292 C180 196 430 250 660 214 C900 176 1180 262 1420 204 C1580 170 1720 214 {w} 196 L{w} 420 L0 420 Z")))
        {
            using (var hill = Draw.VerticalGradient(190, 400, (0, p.Hill1), (1, p.Hill2))) c.DrawPath(hillPath, hill);
            c.Save();
            c.ClipPath(hillPath, antialias: true);
            using var row = Draw.Stroke(SKColor.Parse(p.Vine).WithAlpha(120), 5);
            for (var x = -200f; x < w + 200; x += 26) c.DrawLine(x, 400, x + 120, 170, row);
            c.Restore();
        }

        // Bäume zwischen den Häusern
        using (var trunk = Draw.Fill("#5a4030"))
        using (var crown = Draw.Fill(p.Tree))
        using (var crownLight = Draw.Fill(SKColors.White.WithAlpha(40)))
        {
            foreach (var x in new[] { 30f, 1236f, 1420f, 1600f, 1770f })
            {
                c.DrawRect(x - 5, Baseline - 60, 10, 62, trunk);
                c.DrawCircle(x, Baseline - 86, 40, crown);
                c.DrawCircle(x - 12, Baseline - 98, 20, crownLight);
            }
        }

        Church(c, p);
        foreach (var house in houses) PaintHouse(c, house, p);

        // Gepflasterter Platz
        using (var ground = Draw.VerticalGradient(Baseline, h, (0, p.Ground1), (1, p.Ground2))) c.DrawRect(0, Baseline, w, h - Baseline, ground);
        using (var cobble = Draw.Stroke(SKColors.Black.WithAlpha(26), 2))
        {
            var line = 0;
            for (var y = Baseline + 14; y < h + 20; y += 24, line++)
            {
                for (var x = (line % 2) * 22f - 22; x < w + 44; x += 44)
                {
                    c.DrawArc(new SKRect(x, y - 12, x + 44, y + 12), 200, 140, false, cobble);
                }
            }
        }
        using (var edge = Draw.Fill(SKColors.Black.WithAlpha(40))) c.DrawRect(0, Baseline, w, 5, edge);

        Fountain(c, p);

        if (wet)
        {
            using var veil = Draw.Fill(SKColor.Parse("#33415c").WithAlpha(60));
            c.DrawRect(0, 0, w, h, veil);
            using var rain = Draw.Stroke(SKColor.Parse("#dfe8ef").WithAlpha(90), 2);
            for (var i = 0; i < 70; i++)
            {
                var x = (i * 137 + 40) % w;
                var y = (i * 71 + 20) % 400;
                c.DrawLine(x, y, x - 8, y + 28, rain);
            }
        }
        if (p.Snow && state.LastWeather != WeatherKind.Sonnig)
        {
            using var flake = Draw.Fill(SKColors.White.WithAlpha(210));
            for (var i = 0; i < 90; i++) c.DrawCircle((i * 173 + 30) % w, (i * 97 + 10) % 420, 2 + i % 3, flake);
        }
    }

    static void Church(SKCanvas c, Palette p)
    {
        var x = ChurchX;
        var roofHex = p.Snow ? "#e8eef2" : "#5d4a52";
        using var wall = Draw.VerticalGradient(120, Baseline, (0, "#f6eedc"), (1, "#dccdb0"));
        using var roof = Draw.Fill(roofHex);
        using var dark = Draw.Fill("#4a3528");
        using var glass = Draw.Fill(p.Lit ? "#ffd98a" : "#49607c");
        using var gold = Draw.Fill(Theme.SkGold);
        using var line = Draw.Stroke(SKColors.Black.WithAlpha(60), 2);

        // Schiff
        c.DrawRect(x - 30, Baseline - 112, 150, 112, wall);
        Path(c, $"M{x - 30} {Baseline - 112} L{x - 10} {Baseline - 166} L{x + 108} {Baseline - 166} L{x + 128} {Baseline - 112} Z", roof);
        for (var i = 0; i < 3; i++)
        {
            var wx = x + 42 + i * 28;
            Path(c, $"M{wx - 8} {Baseline - 40} V{Baseline - 78} a8 8 0 0 1 16 0 V{Baseline - 40} Z", glass);
        }
        // Turm
        c.DrawRect(x - 36, 150, 72, Baseline - 150, wall);
        c.DrawRect(x - 36, 150, 72, Baseline - 150, line);
        Path(c, $"M{x - 44} 152 L{x} 72 L{x + 44} 152 Z", roof);
        using (var cross = Draw.Stroke(Theme.SkGold, 4))
        {
            c.DrawLine(x, 72, x, 46, cross);
            c.DrawLine(x - 9, 56, x + 9, 56, cross);
        }
        Path(c, $"M{x - 12} 222 V190 a12 12 0 0 1 24 0 V222 Z", dark);
        c.DrawCircle(x, 258, 17, gold);
        using (var face = Draw.Fill("#fbf4e4")) c.DrawCircle(x, 258, 13, face);
        using (var hand = Draw.Stroke(Theme.SkInk, 2.5f))
        {
            c.DrawLine(x, 258, x, 249, hand);
            c.DrawLine(x, 258, x + 7, 261, hand);
        }
        Path(c, $"M{x - 17} {Baseline} V{Baseline - 44} a17 17 0 0 1 34 0 V{Baseline} Z", dark);
    }

    static void PaintHouse(SKCanvas c, House house, Palette p)
    {
        var w = house.Width;
        var left = house.Cx - w / 2;
        var bodyH = 98f;
        var top = Baseline - bodyH;
        using var wall = Draw.VerticalGradient(top, Baseline, (0, house.Bankrupt ? "#cfc8ba" : "#f6ecd6"), (1, house.Bankrupt ? "#aaa294" : "#e2d2b2"));
        using var beam = Draw.Stroke(house.Bankrupt ? "#6a625a" : "#6b4a32", 5);
        using var roof = Draw.VerticalGradient(top - 70, top, (0, p.Snow ? "#f4f7f9" : "#a04a34"), (1, p.Snow ? "#cfd8df" : "#7a3626"));
        using var accent = Draw.Fill(house.Bankrupt ? "#6a625a" : house.Color);
        using var shadow = Draw.Fill(SKColors.Black.WithAlpha(45));

        c.DrawOval(house.Cx, Baseline + 2, w * 0.56f, 7, shadow);
        // Schornstein
        using (var brick = Draw.Fill("#8a5a48")) c.DrawRect(house.Cx + w * 0.2f, top - 62, 14, 40, brick);
        c.DrawRect(left, top, w, bodyH, wall);
        // Fachwerk
        c.DrawRect(left, top, w, bodyH, beam);
        c.DrawLine(left, top + 44, left + w, top + 44, beam);
        c.DrawLine(left + w * 0.33f, top, left + w * 0.33f, top + 44, beam);
        c.DrawLine(left + w * 0.67f, top, left + w * 0.67f, top + 44, beam);
        c.DrawLine(left, top + 44, left + w * 0.18f, Baseline, beam);
        c.DrawLine(left + w, top + 44, left + w * 0.82f, Baseline, beam);
        Path(c, $"M{left - 14} {top + 2} L{house.Cx} {top - 72} L{left + w + 14} {top + 2} Z", roof);

        // Fenster mit Läden in der Farbe des Weinguts
        using var glass = Draw.Fill(house.Bankrupt ? "#3a3632" : p.Lit ? "#ffd98a" : "#49607c");
        var ww = w * 0.18f;
        foreach (var fx in new[] { left + w * 0.165f, left + w * 0.835f })
        {
            c.DrawRect(fx - ww / 2 - 6, top + 10, 6, 26, accent);
            c.DrawRect(fx + ww / 2, top + 10, 6, 26, accent);
            c.DrawRect(fx - ww / 2, top + 10, ww, 26, glass);
            if (house.Bankrupt)
            {
                using var plank = Draw.Stroke("#8a7a66", 4);
                c.DrawLine(fx - ww / 2 - 4, top + 12, fx + ww / 2 + 4, top + 34, plank);
                c.DrawLine(fx - ww / 2 - 4, top + 34, fx + ww / 2 + 4, top + 12, plank);
            }
        }
        // Tor
        Path(c, $"M{house.Cx - 17} {Baseline} V{Baseline - 34} a17 17 0 0 1 34 0 V{Baseline} Z", accent);
        // Giebelfenster
        c.DrawCircle(house.Cx, top - 22, 9, glass);

        if (house.Own)
        {
            using var pole = Draw.Stroke("#4a3528", 3);
            c.DrawLine(house.Cx, top - 72, house.Cx, top - 122, pole);
            using var flag = Draw.Fill(Theme.SkGold);
            Path(c, $"M{house.Cx} {top - 122} l34 9 -34 11 Z", flag);
        }
    }

    static void Fountain(SKCanvas c, Palette p)
    {
        var x = ChurchX;
        var y = Baseline + 38;
        using var shadow = Draw.Fill(SKColors.Black.WithAlpha(50));
        c.DrawOval(x, y + 14, 96, 14, shadow);
        using var stone = Draw.VerticalGradient(y - 20, y + 20, (0, "#e3dccb"), (1, "#a59b86"));
        using var stoneDark = Draw.Fill("#8f866f");
        using var water = Draw.Fill(p.Snow ? "#e6eef4" : "#8fc1dc");
        using var jet = Draw.Stroke(SKColor.Parse(p.Snow ? "#f4f8fb" : "#d5ecf8"), 3);
        c.DrawRoundRect(x - 84, y - 14, 168, 30, 14, 14, stone);
        c.DrawOval(x, y - 12, 84, 13, stoneDark);
        c.DrawOval(x, y - 12, 76, 10, water);
        c.DrawRect(x - 8, y - 62, 16, 52, stone);
        c.DrawOval(x, y - 62, 34, 8, stoneDark);
        c.DrawOval(x, y - 64, 28, 5, water);
        c.DrawCircle(x, y - 74, 7, stone);
        if (!p.Snow)
        {
            Path(c, $"M{x - 26} {y - 62} q-12 26 -22 46", jet);
            Path(c, $"M{x + 26} {y - 62} q12 26 22 46", jet);
        }
    }

    /// <summary>Stufe −2 (verfeindet) … +2 (befreundet), passend zu <see cref="Rivals.RelationLabel"/></summary>
    public static int RelationLevel(double value) => value >= 40 ? 2 : value >= 10 ? 1 : value > -10 ? 0 : value > -40 ? -1 : -2;

    public static string RelationHex(int level) => level switch
    {
        2 => "#6fae5a",
        1 => "#a9c860",
        0 => "#e3c65a",
        -1 => "#e0994c",
        _ => "#d2573f",
    };

    /// <summary>Gezeichnetes Gesicht; <paramref name="level"/> null steht für einen menschlichen Mitspieler.</summary>
    public static void Face(SKCanvas c, float s, int? level)
    {
        var hex = level is { } l ? RelationHex(l) : "#9cb8d0";
        using var skin = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(new SKPoint(s * 0.4f, s * 0.32f), s * 0.75f,
                [SKColor.Parse(hex).WithAlpha(255), Darken(SKColor.Parse(hex), 0.78f)], [0, 1], SKShaderTileMode.Clamp),
        };
        using var rim = Draw.Stroke(Theme.SkInk, s * 0.045f);
        using var ink = Draw.Fill(Theme.SkInk);
        using var mouth = Draw.Stroke(Theme.SkInk, s * 0.055f);
        c.DrawCircle(s / 2, s / 2, s * 0.46f, skin);
        c.DrawCircle(s / 2, s / 2, s * 0.46f, rim);
        var lv = level ?? 0;
        c.DrawOval(s * 0.35f, s * 0.42f, s * 0.045f, s * (lv == 2 ? 0.03f : 0.06f), ink);
        c.DrawOval(s * 0.65f, s * 0.42f, s * 0.045f, s * (lv == 2 ? 0.03f : 0.06f), ink);
        var bend = lv * s * 0.11f;
        var my = s * 0.66f - bend * 0.35f;
        Path(c, $"M{F(s * 0.31f)} {F(my)} Q{F(s * 0.5f)} {F(my + bend * 1.6f)} {F(s * 0.69f)} {F(lv == -1 ? my - s * 0.04f : my)}", mouth);
        if (lv <= -2)
        {
            c.DrawLine(s * 0.25f, s * 0.27f, s * 0.42f, s * 0.34f, mouth);
            c.DrawLine(s * 0.75f, s * 0.27f, s * 0.58f, s * 0.34f, mouth);
        }
        else if (lv == -1)
        {
            c.DrawLine(s * 0.27f, s * 0.31f, s * 0.42f, s * 0.31f, mouth);
            c.DrawLine(s * 0.58f, s * 0.27f, s * 0.73f, s * 0.31f, mouth);
        }
        else if (lv == 2)
        {
            using var cheek = Draw.Fill(SKColor.Parse("#e0704c").WithAlpha(110));
            c.DrawCircle(s * 0.26f, s * 0.58f, s * 0.06f, cheek);
            c.DrawCircle(s * 0.74f, s * 0.58f, s * 0.06f, cheek);
        }
    }

    public static void Path(SKCanvas c, FormattableString data, SKPaint paint) => Draw.Path(c, FormattableString.Invariant(data), paint);

    static string F(float value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    static SKColor Darken(SKColor color, float factor) =>
        new((byte)(color.Red * factor), (byte)(color.Green * factor), (byte)(color.Blue * factor));

    /// <summary>Kleines Landschaftsbild eines Loses: Hang, Rebzeilen oder Brache, Sterne für die Lage.</summary>
    public static void Parcel(SKCanvas c, float w, float h, AuctionLot lot, int month, float radius = 14)
    {
        var p = For(month);
        c.Save();
        using (var clip = new SKPath())
        {
            clip.AddRoundRect(new SKRect(0, 0, w, h), radius, radius);
            c.ClipPath(clip, antialias: true);
        }
        using (var sky = Draw.VerticalGradient(0, h * 0.7f, (0, p.SkyTop), (1, p.SkyLow))) c.DrawRect(0, 0, w, h, sky);
        using (var sun = Draw.Fill("#fff0bd")) c.DrawCircle(w * 0.82f, h * 0.24f, h * 0.1f, sun);
        using (var sunGlow = Draw.Fill(SKColor.Parse("#fff0bd").WithAlpha(60))) c.DrawCircle(w * 0.82f, h * 0.24f, h * 0.19f, sunGlow);
        using (var far = Draw.Fill(p.Hill2))
            Path(c, $"M0 {F(h * 0.56f)} C{F(w * 0.25f)} {F(h * 0.36f)} {F(w * 0.6f)} {F(h * 0.52f)} {F(w)} {F(h * 0.4f)} L{F(w)} {F(h)} L0 {F(h)} Z", far);
        using (var near = Draw.VerticalGradient(h * 0.5f, h, (0, p.Hill1), (1, p.Hill2)))
            Path(c, $"M0 {F(h * 0.74f)} C{F(w * 0.3f)} {F(h * 0.56f)} {F(w * 0.7f)} {F(h * 0.7f)} {F(w)} {F(h * 0.6f)} L{F(w)} {F(h)} L0 {F(h)} Z", near);

        // Die Parzelle: je größer, desto breiter
        var share = (float)Math.Clamp(0.5 + lot.Hectares * 0.07, 0.5, 0.88);
        var pl = w * (0.5f - share / 2);
        var pr = w * (0.5f + share / 2);
        using var field = SKPath.ParseSvgPathData(
            $"M{F(pl + w * 0.06f)} {F(h * 0.6f)} L{F(pr - w * 0.03f)} {F(h * 0.55f)} L{F(pr + w * 0.02f)} {F(h * 0.93f)} L{F(pl - w * 0.02f)} {F(h * 0.96f)} Z");
        var planted = lot.Grape is not null && lot.PlantedYear is not null;
        var red = planted && Grapes.All[lot.Grape!.Value].Color == GrapeColor.Rot;
        var winter = month is 11 or 12 or 1 or 2 or 3;
        using (var soil = Draw.Fill(planted ? (winter ? "#5a4a34" : "#3f5a22") : "#7a6a4a")) c.DrawPath(field, soil);
        c.Save();
        c.ClipPath(field, antialias: true);
        if (planted)
        {
            using var row = Draw.Stroke(winter ? "#7c6a4c" : month is 9 or 10 ? (red ? "#b5562e" : "#c9a23a") : "#7d9a3a", h * 0.035f);
            for (var x = pl - w * 0.3f; x < pr + w * 0.3f; x += w * 0.045f) c.DrawLine(x, h, x + w * 0.1f, h * 0.5f, row);
        }
        else
        {
            using var dot = Draw.Fill("#93825c");
            var line = 0;
            for (var y = h * 0.56f; y < h; y += h * 0.07f, line++)
            {
                for (var x = pl - w * 0.05f + (line % 2) * w * 0.02f; x < pr + w * 0.05f; x += w * 0.04f) c.DrawCircle(x, y, h * 0.012f, dot);
            }
        }
        c.Restore();
        using (var edge = Draw.Stroke(Theme.SkGold.WithAlpha(220), h * 0.02f)) c.DrawPath(field, edge);

        // Sterne für die Lage (−1 … +2 → 1 … 4)
        var stars = (int)lot.Site + 2;
        using var starOn = Draw.Fill(Theme.SkGold);
        using var starOff = Draw.Fill(SKColors.White.WithAlpha(70));
        using var plate = Draw.Fill(SKColor.Parse("#1c1418").WithAlpha(150));
        var size = h * 0.11f;
        c.DrawRoundRect(h * 0.06f, h * 0.06f, size * 4.9f, size * 1.5f, size * 0.75f, size * 0.75f, plate);
        for (var i = 0; i < 4; i++)
        {
            c.Save();
            c.Translate(h * 0.06f + size * 0.35f + i * size * 1.1f, h * 0.06f + size * 0.22f);
            c.Scale(size / 30f);
            Draw.Path(c, "M15 1 l4.5 9.5 10.5 1.3 -7.7 7.2 2 10.3 -9.3 -5.1 -9.3 5.1 2 -10.3 -7.7 -7.2 10.5 -1.3 Z", i < stars ? starOn : starOff);
            c.Restore();
        }
        c.Restore();
    }

    /// <summary>Auktionshammer mit Klotz in einem Quadrat der Kantenlänge <paramref name="s"/></summary>
    public static void Gavel(SKCanvas c, float s, SKColor wood, SKColor band)
    {
        using var fill = Draw.Fill(wood);
        using var metal = Draw.Fill(band);
        c.Save();
        c.RotateDegrees(-38, s * 0.45f, s * 0.42f);
        c.DrawRoundRect(s * 0.4f, s * 0.36f, s * 0.56f, s * 0.1f, s * 0.05f, s * 0.05f, fill);
        c.DrawRoundRect(s * 0.2f, s * 0.14f, s * 0.26f, s * 0.54f, s * 0.05f, s * 0.05f, fill);
        c.DrawRect(s * 0.2f, s * 0.22f, s * 0.26f, s * 0.05f, metal);
        c.DrawRect(s * 0.2f, s * 0.55f, s * 0.26f, s * 0.05f, metal);
        c.Restore();
        c.DrawRoundRect(s * 0.08f, s * 0.82f, s * 0.6f, s * 0.12f, s * 0.04f, s * 0.04f, fill);
        c.DrawRect(s * 0.14f, s * 0.78f, s * 0.48f, s * 0.05f, metal);
    }
}
