using SkiaSharp;
using Winzer.Engine;
using Winzer.Engine.Data;

namespace Winzer.Ui.Scenes;

internal enum VineyardIcon { NoProtection, Conventional, Bio, Frost, Hail, Drought }

/// <summary>Malerei der Weinberg-Szene: Hang im Hintergrund, Parzellenbilder, Reifeskala, Sterne und Symbole.</summary>
internal static class VineyardPaint
{
    record Season(string SkyTop, string SkyMid, string SkyLow, string Far1, string Far2, string Mid1, string Mid2, string Near1, string Near2, bool Snow);

    static Season SeasonFor(int month) => month switch
    {
        12 or 1 or 2 => new("#33415c", "#7d8fa8", "#d9e2ec", "#9aa7b8", "#7c8a9d", "#cfd8e0", "#a9b6c2", "#b9c6cf", "#8e9ba6", true),
        3 or 4 or 5 => new("#3f6fa8", "#8fb8de", "#e9f1d8", "#7f95a8", "#657b8c", "#9fbf6a", "#6f9a4a", "#6f9a44", "#3f5f26", false),
        6 or 7 or 8 => new("#2f6fb5", "#7fbbe8", "#f7edc0", "#7c8f9c", "#5f7480", "#8fae4a", "#5f8236", "#5f8a30", "#34521e", false),
        _ => new("#27364f", "#b8656a", "#f3b871", "#7c6a78", "#5d5468", "#8c7a45", "#5f6a35", "#66702e", "#313f1c", false),
    };

    /// <summary>Hintergrund der Szene: Hang mit Rebzeilen in den Farben der Jahreszeit</summary>
    public static void Hill(SKCanvas c, float w, float h, GameState state, bool dim)
    {
        var season = SeasonFor(state.Month);
        var wet = state.LastWeather == WeatherKind.Nass;
        using (var sky = Draw.VerticalGradient(0, h * 0.6f, (0, season.SkyTop), (0.55f, season.SkyMid), (1, season.SkyLow)))
            c.DrawRect(0, 0, w, h, sky);

        if (!wet)
        {
            var sunY = state.Month is >= 5 and <= 8 ? 120f : 210f;
            using var glow = new SKPaint
            {
                IsAntialias = true,
                Shader = SKShader.CreateRadialGradient(new SKPoint(1010, sunY), 240,
                    [SKColor.Parse("#fff3c4"), SKColor.Parse("#e6ffd27a"), SKColor.Parse("#00ffd27a")], [0, 0.35f, 1], SKShaderTileMode.Clamp),
            };
            c.DrawCircle(1010, sunY, 240, glow);
            using var disc = Draw.Fill("#fff0bd");
            c.DrawCircle(1010, sunY, 46, disc);
        }
        using (var cloud = Draw.Fill(SKColors.White.WithAlpha((byte)(wet ? 150 : 70))))
        {
            c.DrawOval(760, 96, 190, 22, cloud);
            c.DrawOval(900, 124, 130, 16, cloud);
            if (wet) c.DrawOval(1120, 150, 260, 30, cloud);
        }

        using (var far = Draw.VerticalGradient(240, h, (0, season.Far1), (1, season.Far2)))
            Draw.Path(c, $"M0 330 C200 250 420 300 640 270 C900 235 1150 300 1400 262 C1560 240 1700 265 {w} 255 L{w} {h} L0 {h} Z", far);

        var winter = state.Month is 11 or 12 or 1 or 2 or 3;
        Slope(c, $"M0 470 C220 360 520 420 800 380 C1100 335 1400 420 {w} 360 L{w} {h} L0 {h} Z", 340, h, season.Mid1, season.Mid2,
            winter ? SKColor.Parse("#5a4a34").WithAlpha(60) : SKColors.White.WithAlpha(34), 24, 7, -7);
        Slope(c, $"M0 700 C260 640 660 720 1010 670 C1350 630 1590 680 {w} 660 L{w} {h} L0 {h} Z", 640, h, season.Near1, season.Near2,
            SKColors.Black.WithAlpha(34), 34, 10, 5);

        using (var veil = Draw.Fill(Theme.SkNight.WithAlpha((byte)(dim ? 120 : 50)))) c.DrawRect(0, 0, w, h, veil);
        if (wet)
        {
            using var rain = Draw.Stroke(SKColor.Parse("#dfe8ef").WithAlpha(70), 2);
            for (var i = 0; i < 90; i++)
            {
                var x = (i * 137 + 40) % w;
                var y = (i * 71 + 20) % (h - 60);
                c.DrawLine(x, y, x - 10, y + 34, rain);
            }
        }
        if (season.Snow && state.LastWeather != WeatherKind.Sonnig)
        {
            using var flake = Draw.Fill(SKColors.White.WithAlpha(170));
            for (var i = 0; i < 110; i++) c.DrawCircle((i * 173 + 30) % w, (i * 97 + 10) % h, 2 + i % 3, flake);
        }
    }

    static void Slope(SKCanvas c, string pathData, float top, float bottom, string color1, string color2, SKColor rowColor, float gap, float width, float degrees)
    {
        using var path = SKPath.ParseSvgPathData(pathData);
        using (var fill = Draw.VerticalGradient(top, bottom, (0, color1), (1, color2))) c.DrawPath(path, fill);
        c.Save();
        c.ClipPath(path, antialias: true);
        var bounds = path.Bounds;
        c.RotateDegrees(degrees, bounds.MidX, bounds.MidY);
        using var row = Draw.Stroke(rowColor, width);
        for (var y = bounds.Top - 160; y < bounds.Bottom + 160; y += gap) c.DrawLine(bounds.Left - 200, y, bounds.Right + 200, y, row);
        c.Restore();
    }

    /// <summary>
    /// Bild einer Parzelle (nach PlotScene des Originals): Rebzeilen am Hang, deren Aussehen Monat, Rebsorte und Zustand zeigt –
    /// kahl im Winter, Austrieb im Frühjahr, Trauben im Herbst, Schnee beim Eiswein-Poker. Bessere Lagen sind steiler.
    /// </summary>
    public static void Plot(SKCanvas c, float w, float h, GameState state, Plot plot, float radius = 14)
    {
        var month = state.Month;
        GrapeColor? color = plot.Grape is { } grapeId ? Grapes.All[grapeId].Color : null;
        var young = color is not null && Rules.PlotAgeFactor(plot, state.Year) == 0;
        var harvested = plot.HarvestedThisYear;
        var iceWine = plot.IceWineGamble;
        var winter = month == 12 || month <= 2;
        var snow = month == 1 || (iceWine && month >= 11);
        var sky = winter ? "#cfdce8" : month >= 9 ? "#cbd9e6" : "#a9d3f0";
        var ground = snow ? "#eef2f4" : color is null ? "#b48a5c" : winter ? "#a99a7c" : "#8fb36a";
        var groundLow = snow ? "#cfd8de" : color is null ? "#8f6a42" : winter ? "#84775c" : "#5f8a44";
        string? leaf = null;
        if (!winter && !young && color is not null)
        {
            if (month <= 4) leaf = "#b6dc84";
            else if (month >= 10 || harvested) leaf = plot.Health < 0.7 ? "#a8783a" : "#d39a34";
            else leaf = plot.Health < 0.7 ? "#a7a54a" : "#4f9a3c";
        }
        var leafSize = month == 3 ? 1.5f : month == 4 ? 2.6f : 3.6f;
        var grapeColor = plot.Botrytis ? "#8a6b45" : color == GrapeColor.Rot ? "#5a1d44" : month <= 8 ? "#9fc25a" : "#d4c24a";
        var showGrapes = color is not null && !young && !harvested && (month >= 7 || iceWine) && month > 2;

        c.Save();
        using (var clip = new SKRoundRect(new SKRect(0, 0, w, h), radius, radius)) c.ClipRoundRect(clip, antialias: true);
        var u = w / 200f;
        c.Scale(u);
        var height = h / u;
        using (var skyPaint = Draw.VerticalGradient(0, height * 0.5f, (0, sky), (1, "#eef5f8"))) c.DrawRect(0, 0, 200, height, skyPaint);
        if (!winter && month is >= 5 and <= 9)
        {
            using var sun = Draw.Fill("#f4c542");
            c.DrawCircle(24, height * 0.15f, 6.5f, sun);
        }
        var top = height * 0.36f;
        var rise = 4 + ((float)plot.Site + 1) * 4;
        var slope = -rise / 200f;
        using (var groundPaint = Draw.VerticalGradient(top - rise, height, (0, ground), (1, groundLow)))
            Draw.Path(c, FormattableString.Invariant($"M0 {top} L200 {top - rise} V{height} H0 Z"), groundPaint);

        var rowGap = (height - top - 3) / 4.2f;
        var k = rowGap / 10f;
        using var wire = Draw.Stroke(SKColor.Parse("#6b4a2f").WithAlpha(180), 0.8f);
        using var trunk = Draw.Stroke("#5b3d24", (young ? 0.9f : 1.4f) * k);
        using var twig = Draw.Stroke("#5b3d24", 0.8f * k);
        using var weed = Draw.Fill("#7aa04c");
        using var sprout = Draw.Fill("#9bcf6a");
        using var leafPaint = Draw.Fill(leaf ?? "#4f9a3c");
        using var berry = Draw.Fill(grapeColor);
        using var white = Draw.Fill(SKColors.White.WithAlpha(230));
        using var drip = Draw.Stroke(SKColor.Parse("#4a90c8").WithAlpha(210), 1.1f);
        for (var row = 0; row < 4; row++)
        {
            var y0 = top + rowGap * (0.95f + row);
            c.DrawLine(4, y0 + slope * 4, 196, y0 + slope * 196, wire);
            if (plot.Irrigation) c.DrawLine(4, y0 + 1.6f + slope * 4, 196, y0 + 1.6f + slope * 196, drip);
            for (var i = 0; i < 8; i++)
            {
                var x = 12 + i * 24 + row * 4;
                var y = y0 + slope * x;
                if (color is null)
                {
                    if ((i + row) % 3 == 0) c.DrawCircle(x, y - 1, 1.6f * k, weed);
                    continue;
                }
                c.DrawLine(x, y, x, y - (young ? 4 : 9) * k, trunk);
                if (winter && !young)
                {
                    c.DrawLine(x, y - 8 * k, x - 4 * k, y - 11 * k, twig);
                    c.DrawLine(x, y - 8 * k, x + 4 * k, y - 11 * k, twig);
                }
                if (young && !winter) c.DrawCircle(x, y - 5 * k, 1.6f * k, sprout);
                if (leaf is not null)
                {
                    c.DrawCircle(x - 2 * k, y - 10 * k, leafSize * k, leafPaint);
                    c.DrawCircle(x + 2.5f * k, y - 11 * k, leafSize * 0.85f * k, leafPaint);
                }
                if (showGrapes && (i + row) % 2 == 0)
                {
                    c.DrawCircle(x + 1 * k, y - 6 * k, 1.6f * k, berry);
                    c.DrawCircle(x - 0.8f * k, y - 5.4f * k, 1.4f * k, berry);
                    c.DrawCircle(x + 0.2f * k, y - 4 * k, 1.3f * k, berry);
                }
                if (snow) c.DrawCircle(x - 1 * k, y - 12 * k, 1.4f * k, white);
            }
        }
        if (plot.HailNet)
        {
            using var net = Draw.Stroke(SKColors.White.WithAlpha(110), 0.5f);
            for (var x = -height; x < 200; x += 7)
            {
                c.DrawLine(x, top - rise, x + height, height, net);
                c.DrawLine(x + height, top - rise, x, height, net);
            }
        }
        c.Restore();
    }

    /// <summary>Lage auf der Reifeskala 40–160 °Oe als Anteil 0–1</summary>
    public static float ScalePosition(double oechsle) => (float)Math.Clamp((oechsle - 40) / 120, 0, 1);

    /// <summary>Reifeskala: Füllung bis zum Mostgewicht, Striche an den Schwellen der Qualitätsstufen</summary>
    public static void Gauge(SKCanvas c, float w, float h, double oechsle, GrapeColor color, bool dark)
    {
        var rect = new SKRect(0, 0, w, h);
        using (var track = Draw.Fill(dark ? Theme.SkCream.WithAlpha(50) : SKColor.Parse("#d9c7a3"))) c.DrawRoundRect(rect, h / 2, h / 2, track);
        c.Save();
        using (var clip = new SKRoundRect(rect, h / 2, h / 2)) c.ClipRoundRect(clip, antialias: true);
        var fillWidth = w * ScalePosition(oechsle);
        if (oechsle > 0 && fillWidth > 0)
        {
            using var fill = color == GrapeColor.Rot
                ? Draw.HorizontalGradient(0, w, (0, "#b0607e"), (1, "#5a1d44"))
                : Draw.HorizontalGradient(0, w, (0, "#a8c25a"), (1, "#e0b83a"));
            c.DrawRect(0, 0, Math.Max(fillWidth, h), h, fill);
        }
        using var tick = Draw.Stroke(dark ? Theme.SkNight.WithAlpha(200) : SKColor.Parse("#fbf4e4"), 2);
        foreach (var level in Quality.QualityLevels)
        {
            if (level.Special || level.MinOechsle <= 0) continue;
            var x = w * ScalePosition(level.MinOechsle);
            c.DrawLine(x, 0, x, h, tick);
        }
        c.Restore();
    }

    /// <summary>Lagenqualität −1 bis +2 als vier Sterne</summary>
    public static void Stars(SKCanvas c, float w, float h, double site)
    {
        var filled = (int)Math.Clamp(site + 2, 0, 4);
        var size = h;
        using var on = Draw.Fill("#d9a62e");
        using var off = Draw.Stroke(SKColor.Parse("#d9a62e").WithAlpha(170), 30f / size * 1.6f);
        for (var i = 0; i < 4; i++)
        {
            c.Save();
            c.Translate(w - (4 - i) * size * 1.08f, 0);
            c.Scale(size / 30f);
            Draw.Path(c, "M15 2 l4.2 8.8 9.6 1.2 -7.1 6.6 1.9 9.5 -8.6 -4.7 -8.6 4.7 1.9 -9.5 -7.1 -6.6 9.6 -1.2 Z", i < filled ? on : off);
            c.Restore();
        }
    }

    /// <summary>Symbol in einem Feld von 64 × 64</summary>
    public static void Icon(SKCanvas c, VineyardIcon icon, SKColor color)
    {
        using var line = Draw.Stroke(color, 4.5f);
        using var fill = Draw.Fill(color);
        switch (icon)
        {
            case VineyardIcon.NoProtection:
                c.DrawCircle(32, 32, 22, line);
                c.DrawLine(17, 47, 47, 17, line);
                break;
            case VineyardIcon.Conventional:
                Draw.Path(c, "M25 8 H39 M28 8 V26 L14 52 a4 4 0 0 0 4 5 H46 a4 4 0 0 0 4 -5 L36 26 V8", line);
                Draw.Path(c, "M21 42 H43 L48 53 H16 Z", fill);
                break;
            case VineyardIcon.Bio:
                Draw.Path(c, "M12 52 C10 24 28 10 54 10 C54 38 40 54 12 52 Z", fill);
                using (var vein = Draw.Stroke(Theme.SkNight.WithAlpha(150), 3)) Draw.Path(c, "M14 50 C24 38 34 28 46 18", vein);
                break;
            case VineyardIcon.Frost:
                for (var i = 0; i < 3; i++)
                {
                    var a = i * Math.PI / 3;
                    var dx = (float)Math.Cos(a) * 24;
                    var dy = (float)Math.Sin(a) * 24;
                    c.DrawLine(32 - dx, 32 - dy, 32 + dx, 32 + dy, line);
                }
                c.DrawCircle(32, 32, 6, fill);
                break;
            case VineyardIcon.Hail:
                c.DrawOval(32, 24, 24, 13, fill);
                c.DrawCircle(22, 16, 10, fill);
                c.DrawCircle(18, 48, 4.5f, fill);
                c.DrawCircle(32, 54, 4.5f, fill);
                c.DrawCircle(46, 48, 4.5f, fill);
                break;
            default:
                c.DrawCircle(32, 24, 11, fill);
                for (var i = 0; i < 8; i++)
                {
                    var a = i * Math.PI / 4;
                    c.DrawLine(32 + (float)Math.Cos(a) * 16, 24 + (float)Math.Sin(a) * 16, 32 + (float)Math.Cos(a) * 21, 24 + (float)Math.Sin(a) * 21, line);
                }
                Draw.Path(c, "M6 56 H58 M20 56 l5 -7 M38 56 l-4 -8 M48 56 l4 -6", line);
                break;
        }
    }
}
