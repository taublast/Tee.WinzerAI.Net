using SkiaSharp;

namespace Winzer.Ui.Scenes;

/// <summary>
/// Bewegtes Wetter für die gemalten Landschaften. Alles hängt nur von der Zeit <c>t</c> (Sekunden) ab – es gibt keinen
/// gemerkten Zustand, jedes Bild wird aus der Zeit neu berechnet.
/// </summary>
internal static class WeatherFx
{
    /// <summary>Fester Pseudozufall 0…1 je Teilchen und Eigenschaft</summary>
    internal static float Hash(int i, int k)
    {
        var x = MathF.Sin(i * 12.9898f + k * 78.233f) * 43758.5453f;
        return x - MathF.Floor(x);
    }

    internal static float Wrap(float value, float span) => value - MathF.Floor(value / span) * span;

    /// <summary>Sonne mit atmendem Schein; bei klarem Wetter drehen sich dazu blasse Strahlen.</summary>
    public static void Sun(SKCanvas c, float x, float y, float glowRadius, float discRadius, float t, bool rays)
    {
        var radius = glowRadius * (1 + 0.05f * MathF.Sin(t * 0.8f));
        if (rays)
        {
            // Die Strahlen verlieren sich nach außen, statt hart zu enden
            using var ray = new SKPaint
            {
                IsAntialias = true,
                Shader = SKShader.CreateRadialGradient(new SKPoint(x, y), radius * 1.5f,
                    [SKColor.Parse("#fff3c4").WithAlpha(40), SKColor.Parse("#fff3c4").WithAlpha(0)], [0.2f, 1], SKShaderTileMode.Clamp),
            };
            c.Save();
            c.RotateDegrees(t * 4, x, y);
            for (var i = 0; i < 12; i++)
            {
                using var wedge = new SKPath();
                wedge.MoveTo(x, y);
                wedge.LineTo(x + radius * 1.5f, y - radius * 0.1f);
                wedge.LineTo(x + radius * 1.5f, y + radius * 0.1f);
                wedge.Close();
                c.DrawPath(wedge, ray);
                c.RotateDegrees(30, x, y);
            }
            c.Restore();
        }
        using (var glow = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(new SKPoint(x, y), radius,
                [SKColor.Parse("#fff3c4"), SKColor.Parse("#e6ffd27a"), SKColor.Parse("#00ffd27a")], [0, 0.34f, 1], SKShaderTileMode.Clamp),
        })
            c.DrawCircle(x, y, radius, glow);
        using var disc = Draw.Fill("#fff0bd");
        c.DrawCircle(x, y, discRadius, disc);
    }

    /// <summary>Wolken ziehen von links nach rechts und kommen links wieder herein.</summary>
    public static void Clouds(SKCanvas c, float w, float t, int count, float speed, SKColor color, float top, float bottom)
    {
        using var paint = Draw.Fill(color);
        var span = w + 700;
        for (var i = 0; i < count; i++)
        {
            var size = 130 + Hash(i, 1) * 150;
            var x = Wrap(Hash(i, 2) * span + t * speed * (0.6f + 0.8f * Hash(i, 3)), span) - 350;
            var y = top + Hash(i, 4) * (bottom - top) + 4 * MathF.Sin(t * 0.3f + i);
            c.DrawOval(x, y, size, size * 0.12f, paint);
            c.DrawOval(x + size * 0.45f, y + size * 0.09f, size * 0.62f, size * 0.08f, paint);
            c.DrawOval(x - size * 0.4f, y + size * 0.06f, size * 0.5f, size * 0.07f, paint);
        }
    }

    /// <summary>Schräg fallender Regen</summary>
    public static void Rain(SKCanvas c, float w, float h, float t, int drops)
    {
        using var near = Draw.Stroke(SKColor.Parse("#dfe8ef").WithAlpha(120), 2.2f);
        using var far = Draw.Stroke(SKColor.Parse("#dfe8ef").WithAlpha(70), 1.4f);
        var span = h + 80;
        for (var i = 0; i < drops; i++)
        {
            var close = i % 3 == 0;
            var speed = close ? 1250 + Hash(i, 1) * 350 : 820 + Hash(i, 1) * 260;
            var y = Wrap(Hash(i, 2) * span + t * speed, span) - 40;
            var x = Hash(i, 3) * (w + 320) - y * 0.3f;
            var length = close ? 38f : 24f;
            c.DrawLine(x, y, x - length * 0.3f, y + length, close ? near : far);
        }
    }

    /// <summary>Schnee sinkt langsam und pendelt dabei</summary>
    public static void Snow(SKCanvas c, float w, float h, float t, int flakes)
    {
        using var paint = Draw.Fill(SKColors.White);
        var span = h + 20;
        for (var i = 0; i < flakes; i++)
        {
            var size = 1.6f + Hash(i, 1) * 3;
            var y = Wrap(Hash(i, 2) * span + t * (28 + size * 14), span) - 10;
            var x = Wrap(Hash(i, 3) * w + MathF.Sin(t * (0.5f + Hash(i, 4)) + i) * 22 + t * 6, w);
            paint.Color = SKColors.White.WithAlpha((byte)(130 + Hash(i, 5) * 110));
            c.DrawCircle(x, y, size, paint);
        }
    }

    /// <summary>Herbstlaub treibt im Wind</summary>
    public static void Leaves(SKCanvas c, float w, float h, float t, int leaves)
    {
        string[] colors = ["#c9742a", "#b5562e", "#d9a23a", "#8a3b2c"];
        using var paint = Draw.Fill(SKColors.White);
        var span = w + 120;
        for (var i = 0; i < leaves; i++)
        {
            var speed = 60 + Hash(i, 1) * 70;
            var x = Wrap(Hash(i, 2) * span + t * speed, span) - 60;
            var fall = Wrap(Hash(i, 3) * h + t * (18 + Hash(i, 4) * 22), h);
            var y = fall + MathF.Sin(t * 1.3f + i * 1.7f) * 26;
            paint.Color = SKColor.Parse(colors[i % colors.Length]).WithAlpha(210);
            c.Save();
            c.RotateDegrees(t * (60 + Hash(i, 5) * 90) + i * 40, x, y);
            c.DrawOval(x, y, 9, 4.5f, paint);
            c.Restore();
        }
    }

    /// <summary>Funkelnde Sterne im oberen Himmel</summary>
    public static void Stars(SKCanvas c, float w, float height, float t, int count)
    {
        using var star = Draw.Fill(Theme.SkCream);
        for (var i = 0; i < count; i++)
        {
            var twinkle = 0.55f + 0.45f * MathF.Sin(t * (0.8f + Hash(i, 1) * 1.6f) + i);
            star.Color = Theme.SkCream.WithAlpha((byte)((40 + i * 37 % 110) * twinkle));
            c.DrawCircle((i * 337 + 71) % w, (i * 89 + 13) % height, 1.2f + i % 3 * 0.6f, star);
        }
    }

    /// <summary>Rauch steigt aus einem Schornstein, wird breiter und verliert sich</summary>
    public static void Smoke(SKCanvas c, float x, float y, float t)
    {
        using var paint = Draw.Fill(SKColors.White);
        const int puffs = 7;
        for (var i = 0; i < puffs; i++)
        {
            var age = Wrap(t * 0.16f + i / (float)puffs, 1);
            var px = x + age * 46 + MathF.Sin(age * 5 + i) * 6;
            var py = y - age * 120;
            paint.Color = SKColor.Parse("#e9dccb").WithAlpha((byte)(95 * (1 - age)));
            c.DrawCircle(px, py, 7 + age * 20, paint);
        }
    }
}
