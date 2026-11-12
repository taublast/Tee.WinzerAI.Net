using Winzer.Engine.Data;
using static Winzer.Engine.Tests.TestHelpersA;

namespace Winzer.Engine.Tests;

/// <summary>bio.test.ts</summary>
public class BioTests
{
    static readonly GameAction ApplyLabel = new GameAction.ApplyBioLabel();

    /// <summary>Weingut mit viel Geld, das im Januar auf Bio umstellt</summary>
    static GameState Converting()
    {
        var s = Game.NewGame(new NewGameOptions
        {
            Seed = 5, RandomEvents = false, Start = new StartSetup { Money = 2_000_000 }, Players = [Human("Anna", RegionId.Baden)],
        });
        return Game.ApplyAction(s, new GameAction.SetProtection(ProtectionStrategy.Bio));
    }

    static readonly BottleLot TestLot = Lot("x", GrapeId.Riesling, 1, 85, QualityLevel.Kabinett, 75, 100);

    [Fact]
    public void Das_Gruenblatt_Siegel_gibt_es_erst_nach_zwei_Jahren_Umstellung_und_nur_auf_Antrag()
    {
        var s = Converting();
        var start = s;
        Throws($"Noch {Economy.BioConversionMonths} Monate Umstellung", () => Game.ApplyAction(start, ApplyLabel));
        s = EndMonths(s, Economy.BioConversionMonths - 1);
        Assert.Equal(1, Bio.MonthsUntilEligible(s, CurrentPlayer(s)));
        s = EndMonths(s, 1);
        Assert.Equal(0, Bio.MonthsUntilEligible(s, CurrentPlayer(s)));
        Assert.Null(CurrentPlayer(s).BioLabel); // nicht automatisch
        var money = CurrentPlayer(s).Money;
        s = Game.ApplyAction(s, ApplyLabel);
        Assert.Equal(BioLabel.Basis, CurrentPlayer(s).BioLabel);
        Assert.Equal(money - BioData.BioLabels[BioLabel.Basis].Fee, CurrentPlayer(s).Money);
    }

    [Fact]
    public void Terravita_und_Lunaris_folgen_jeweils_nach_einem_Jahr_auf_der_Vorstufe()
    {
        var s = EndMonths(Converting(), Economy.BioConversionMonths);
        s = Game.ApplyAction(s, ApplyLabel);
        var basis = s;
        Throws("Grünblatt", () => Game.ApplyAction(basis, ApplyLabel));
        s = EndMonths(s, 12);
        s = Game.ApplyAction(s, ApplyLabel);
        Assert.Equal(BioLabel.Verband, CurrentPlayer(s).BioLabel);
        s = EndMonths(s, 12);
        s = Game.ApplyAction(s, ApplyLabel);
        Assert.Equal(BioLabel.Biodynamisch, CurrentPlayer(s).BioLabel);
        Throws("strengste", () => Game.ApplyAction(s, ApplyLabel));
    }

    [Fact]
    public void Hoehere_Siegel_bringen_bessere_Preise_und_mehr_Nachfrage()
    {
        var s = Converting();
        var player = CurrentPlayer(s);
        var (basePrice, baseDemand) = (Market.BottlePrice(TestLot, player, s), Market.MonthlyDemand(player));
        player.BioLabel = BioLabel.Basis;
        var basis = Market.BottlePrice(TestLot, player, s);
        player.BioLabel = BioLabel.Verband;
        var verband = Market.BottlePrice(TestLot, player, s);
        player.BioLabel = BioLabel.Biodynamisch;
        var biodynamisch = Market.BottlePrice(TestLot, player, s);
        CloseTo(basePrice * 1.1, basis, 1);
        Assert.True(verband > basis);
        Assert.True(biodynamisch > verband);
        Assert.True(Market.MonthlyDemand(player) > baseDemand);
    }

    [Fact]
    public void Lunaris_kostet_Ertrag_und_mehr_Pflanzenschutz_bringt_aber_Guete()
    {
        var s = EndMonths(Converting(), 9); // Oktober Jahr 1
        var player = CurrentPlayer(s);
        var plot = player.Plots[0];
        var (beforePreview, beforeProtection) = (Game.HarvestPreview(s, player, plot), Market.MonthlyCosts(player, 6).Protection);
        player.BioLabel = BioLabel.Biodynamisch;
        var (afterPreview, afterProtection) = (Game.HarvestPreview(s, player, plot), Market.MonthlyCosts(player, 6).Protection);
        Assert.True(afterPreview.Hectoliters < beforePreview.Hectoliters);
        Assert.Equal(beforePreview.Quality + BioData.BioLabels[BioLabel.Biodynamisch].Quality, afterPreview.Quality);
        CloseTo(beforeProtection * 1.25, afterProtection);
    }

    [Fact]
    public void Wer_vom_Bio_Pflanzenschutz_abweicht_verliert_Siegel_und_Umstellungszeit()
    {
        var s = EndMonths(Converting(), Economy.BioConversionMonths);
        s = Game.ApplyAction(s, ApplyLabel);
        s = Game.ApplyAction(s, new GameAction.SetProtection(ProtectionStrategy.Conventional));
        Assert.Null(CurrentPlayer(s).BioLabel);
        Assert.Contains(s.Log, e => e.Text.Contains("verliert das Grünblatt-Siegel"));
        s = Game.ApplyAction(s, new GameAction.SetProtection(ProtectionStrategy.Bio));
        Assert.Equal(Economy.BioConversionMonths, Bio.MonthsUntilEligible(s, CurrentPlayer(s)));
    }

    [Fact]
    public void Siegel_kosten_jedes_Jahr_eine_Gebuehr()
    {
        var s = EndMonths(Converting(), Economy.BioConversionMonths); // Januar Jahr 3
        s = Game.ApplyAction(s, ApplyLabel);
        s = EndMonths(s, 12);
        Assert.Contains(s.Log, e => e.Text.StartsWith("Jahresgebühr für Kontrolle und Verband (Grünblatt)", StringComparison.Ordinal));
    }

    [Fact]
    public void Der_Biopionier_arbeitet_sich_die_Stufen_hinauf()
    {
        var state = Simulate.Run(new SimOptions
        {
            Seed = 1, Years = 8, Players = [new SimPlayer { RegionId = RegionId.Baden, Ai = AiProfileId.Bio }],
        }).State;
        Assert.NotNull(state.Players[0].BioLabel);
    }
}
