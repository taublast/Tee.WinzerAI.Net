using Winzer.Engine.Data;
using static Winzer.Engine.Tests.TestHelpersB;

namespace Winzer.Engine.Tests;

/// <summary>trends.test.ts – Markttrends</summary>
public class TrendsTests
{
    static GameState Start(int seed = 4) => Game.NewGame(Solo(seed, RegionId.Pfalz));

    static BottleLot NewLot(bool sparkling = false) =>
        Lot("x", GrapeId.Riesling, 1, 85, QualityLevel.Kabinett, 75, 100, sparkling: sparkling);

    [Fact]
    public void Jede_Rebsorte_und_Sekt_starten_nahe_100_Prozent()
    {
        var s = Start();
        Assert.Equal(Trends.TrendKeys.Order(), s.Trends.Keys.Order());
        foreach (var trend in s.Trends.Values)
        {
            Assert.True(trend.Value >= 0.85);
            Assert.True(trend.Value <= 1.15);
        }
    }

    [Fact]
    public void Wandern_ueber_die_Jahre_bleiben_aber_in_der_Spanne()
    {
        var s = Start();
        var initial = s.Trends.ToDictionary(t => t.Key, t => t.Value.Clone());
        for (var month = 0; month < 12 * 20; month++)
        {
            Trends.UpdateTrends(s);
            foreach (var trend in s.Trends.Values)
            {
                Assert.True(trend.Value >= Economy.TrendRange.Min);
                Assert.True(trend.Value <= Economy.TrendRange.Max);
            }
        }
        var moved = Trends.TrendKeys.Where(k => Math.Abs(s.Trends[k].Value - initial[k].Value) > 0.1).ToList();
        Assert.True(moved.Count > 3, $"bewegt: {moved.Count}");
        Assert.Contains(s.Log, e => e.Text.StartsWith("📈 Markttrend", StringComparison.Ordinal));
        Assert.Contains(s.Log, e => e.Text.StartsWith("📉 Markttrend", StringComparison.Ordinal));
    }

    [Fact]
    public void Naehern_sich_dem_Ziel_schrittweise()
    {
        var s = Start();
        s.Trends[TrendKey.Riesling] = new Trend { Value = 0.8, Target = 1.3 };
        var values = new List<double> { s.Trends[TrendKey.Riesling].Value };
        for (var i = 0; i < 6; i++)
        {
            s.Trends[TrendKey.Riesling].Target = 1.3; // ein zufälliger Zielwechsel soll den Test nicht stören
            Trends.UpdateTrends(s);
            values.Add(s.Trends[TrendKey.Riesling].Value);
        }
        for (var i = 1; i < values.Count; i++) Assert.True(values[i] > values[i - 1]);
        Assert.True(values[^1] < 1.3);
        Assert.Equal(TrendDirectionKind.Up, Trends.TrendDirection(s.Trends[TrendKey.Riesling]));
    }

    [Fact]
    public void Wirken_auf_Flaschen_und_Fassweinpreise()
    {
        var s = Start();
        var player = Current(s);
        var lot = NewLot();
        s.Trends[TrendKey.Riesling].Value = 1;
        var normal = Market.BottlePrice(lot, player, s);
        var batch = new WineBatch { Id = lot.Id, Hectoliters = 10, MonthsUntilReady = 0, Barrique = false };
        batch.CopyInfoFrom(lot);
        var bulk = Market.BulkPricePerHl(batch, s, player);
        s.Trends[TrendKey.Riesling].Value = 1.3;
        Assert.Equal(normal * 1.3, Market.BottlePrice(lot, player, s), 1);
        Assert.Equal(bulk * 1.3, Market.BulkPricePerHl(batch, s, player), 1);
    }

    [Fact]
    public void Sekt_folgt_dem_Sekt_Trend_nicht_der_Rebsorte()
    {
        var s = Start();
        var player = Current(s);
        var sekt = NewLot(sparkling: true);
        s.Trends[TrendKey.Sekt].Value = 1;
        var normal = Market.BottlePrice(sekt, player, s);
        s.Trends[TrendKey.Riesling].Value = 0.7;
        Assert.Equal(normal, Market.BottlePrice(sekt, player, s));
        s.Trends[TrendKey.Sekt].Value = 1.2;
        Assert.Equal(normal * 1.2, Market.BottlePrice(sekt, player, s), 1);
    }

    [Fact]
    public void Sind_fuer_denselben_Seed_gleich()
    {
        var a = Start(9);
        var b = Start(9);
        for (var i = 0; i < 24; i++)
        {
            a = Game.ApplyAction(a, EndTurn);
            b = Game.ApplyAction(b, EndTurn);
        }
        Assert.Equal(
            a.Trends.Select(t => (t.Key, t.Value.Value, t.Value.Target)),
            b.Trends.Select(t => (t.Key, t.Value.Value, t.Value.Target)));
    }
}
