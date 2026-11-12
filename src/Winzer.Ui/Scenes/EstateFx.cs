using SkiaSharp;

namespace Winzer.Ui.Scenes;

/// <summary>Kleines Leben in der Gutsansicht – wie <see cref="WeatherFx"/> allein aus der Zeit berechnet.</summary>
internal static class EstateFx
{
    /// <summary>Ein Vogelschwarm zieht alle halbe Minute in Keilform über den Himmel; die Flügel schlagen.</summary>
    public static void Birds(SKCanvas c, float w, float t)
    {
        const float period = 34;
        var lap = (int)MathF.Floor(t / period);
        var p = t / period - lap;
        // Ein gutes Drittel der Zeit ist der Himmel leer
        if (p > 0.62f) return;
        var leftward = lap % 2 == 1;
        var travel = p / 0.62f * (w + 300) - 150;
        var headX = leftward ? w - travel : travel;
        var headY = 150 + WeatherFx.Hash(lap, 7) * 110 + MathF.Sin(t * 0.5f) * 10;
        using var wing = Draw.Stroke(SKColor.Parse("#2c2622").WithAlpha(170), 2.2f);
        wing.StrokeCap = SKStrokeCap.Round;
        for (var i = 0; i < 7; i++)
        {
            var rank = (i + 1) / 2;
            var side = i % 2 == 0 ? 1 : -1;
            var x = headX + (leftward ? 1 : -1) * rank * 26;
            var y = headY + side * rank * 13 + MathF.Sin(t * 1.4f + i) * 3;
            var flap = MathF.Sin(t * 9 + i * 1.3f) * 5;
            c.DrawLine(x - 9, y - 3 - flap, x, y, wing);
            c.DrawLine(x + 9, y - 3 - flap, x, y, wing);
        }
    }

    /// <summary>Ein Traktor mit Traubenanhänger pendelt zwischen zwei Stellen; <paramref name="ground"/> ist die Höhe der Räder.</summary>
    public static void Tractor(SKCanvas c, float from, float to, float ground, float t)
    {
        const float oneWay = 15;
        var lap = (int)MathF.Floor(t / oneWay);
        var p = t / oneWay - lap;
        // Sanft anfahren und abbremsen
        var eased = p * p * (3 - 2 * p);
        var forward = lap % 2 == 0;
        var x = forward ? from + (to - from) * eased : to - (to - from) * eased;
        var rolling = eased * (to - from) / 11f;
        var bump = MathF.Sin(t * 13) * 0.8f;

        c.Save();
        c.Translate(x, ground + bump);
        if (!forward) c.Scale(-1, 1);
        using (var shadow = Draw.Fill(SKColors.Black.WithAlpha(45))) c.DrawOval(-12, 1, 58, 5, shadow);

        // Anhänger mit Trauben
        using (var box = Draw.Fill("#7a5a3a")) c.DrawRect(-62, -24, 38, 14, box);
        using (var grapes = Draw.Fill("#6b3fa0"))
        {
            for (var i = 0; i < 6; i++) c.DrawCircle(-57 + i * 6, -26 - i % 2 * 2, 4, grapes);
        }
        using (var bar = Draw.Stroke("#2c2622", 2.5f)) c.DrawLine(-24, -13, -12, -13, bar);
        Wheel(c, -43, -7, 7, rolling);

        // Zugmaschine
        using (var body = Draw.Fill("#8e2440"))
        {
            c.DrawRoundRect(-12, -26, 44, 16, 4, 4, body);
            c.DrawRect(-10, -42, 20, 18, body);
        }
        using (var glass = Draw.Fill(SKColor.Parse("#cfe3f0").WithAlpha(210))) c.DrawRect(-6, -38, 12, 11, glass);
        using (var pipe = Draw.Stroke("#2c2622", 3)) c.DrawLine(22, -26, 22, -38, pipe);
        Wheel(c, -1, -11, 11, rolling);
        Wheel(c, 26, -7, 7, rolling);
        c.Restore();

        // Auspuffwölkchen bleiben hinter dem Traktor zurück
        using var puff = Draw.Fill(SKColors.White);
        for (var i = 0; i < 3; i++)
        {
            var age = WeatherFx.Wrap(t * 0.9f + i / 3f, 1);
            puff.Color = SKColor.Parse("#d8d2c8").WithAlpha((byte)(90 * (1 - age)));
            c.DrawCircle(x + (forward ? 22 : -22) - (forward ? 1 : -1) * age * 16, ground - 40 - age * 22, 3 + age * 6, puff);
        }
    }

    static void Wheel(SKCanvas c, float x, float y, float radius, float turn)
    {
        using (var tyre = Draw.Fill("#2c2622")) c.DrawCircle(x, y, radius, tyre);
        using (var hub = Draw.Fill("#d9c7a3")) c.DrawCircle(x, y, radius * 0.45f, hub);
        using var spoke = Draw.Stroke("#2c2622", 1.4f);
        for (var i = 0; i < 3; i++)
        {
            var a = turn + i * MathF.PI / 3;
            c.DrawLine(x - MathF.Cos(a) * radius * 0.45f, y - MathF.Sin(a) * radius * 0.45f, x + MathF.Cos(a) * radius * 0.45f, y + MathF.Sin(a) * radius * 0.45f, spoke);
        }
    }
}
