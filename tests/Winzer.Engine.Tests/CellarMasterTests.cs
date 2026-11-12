using static Winzer.Engine.Tests.TestHelpersB;

namespace Winzer.Engine.Tests;

/// <summary>cellarmaster.test.ts – Kellermeister mit Fähigkeiten</summary>
public class CellarMasterTests
{
    static GameState Start() => Game.NewGame(Solo(3, RegionId.Pfalz));

    static CellarMaster Master(List<CellarSkill> skills, double talent = 4, int hiredYear = 1) => new()
    {
        Name = "Test", Talent = talent, Skills = skills, Salary = CellarMasters.SalaryFor(talent, skills), HiredYear = hiredYear,
    };

    [Fact]
    public void Spezialisten_heben_die_Guete_ihrer_Weine()
    {
        var player = Current(Start());
        player.CellarMaster = Master([CellarSkill.Rotwein]);
        Assert.Equal(4.0 + 3, CellarMasters.CellarMasterBonus(player, GrapeId.Spaetburgunder, 80, 1));
        Assert.Equal(4.0, CellarMasters.CellarMasterBonus(player, GrapeId.Riesling, 80, 1));

        player.CellarMaster = Master([CellarSkill.Weisswein, CellarSkill.Edelsuess]);
        Assert.Equal(4.0 + 3, CellarMasters.CellarMasterBonus(player, GrapeId.Riesling, 80, 1));
        Assert.Equal(4.0 + 3 + 4, CellarMasters.CellarMasterBonus(player, GrapeId.Riesling, 110, 1));
        Assert.Equal(4.0 + 4, CellarMasters.CellarMasterBonus(player, GrapeId.Dornfelder, 110, 1));
    }

    [Fact]
    public void Wird_mit_den_Dienstjahren_besser_bis_hoechstens_plus_2()
    {
        var m = Master([], 4, 1);
        Assert.Equal(0.0, CellarMasters.Experience(m, 1));
        Assert.Equal(1.0, CellarMasters.Experience(m, 5));
        Assert.Equal(2.0, CellarMasters.Experience(m, 9));
        Assert.Equal(2.0, CellarMasters.Experience(m, 30));
    }

    [Fact]
    public void Ein_sparsamer_Kellermeister_senkt_die_Flaschen_und_Sektkosten()
    {
        var player = Current(Start());
        player.CellarMaster = Master([CellarSkill.Sekt]);
        var (bottle, sekt) = (Cellar.BottleCost(player), Cellar.SektCost(player));
        player.CellarMaster = Master([CellarSkill.Sekt, CellarSkill.Sparsam]);
        Assert.Equal(bottle * 0.75, Cellar.BottleCost(player), 2);
        Assert.Equal(sekt * 0.75, Cellar.SektCost(player), 2);
    }

    [Fact]
    public void Gehalt_steigt_mit_Talent_und_Faehigkeiten()
    {
        Assert.True(CellarMasters.SalaryFor(6, [CellarSkill.Sekt]) > CellarMasters.SalaryFor(3, [CellarSkill.Sekt]));
        Assert.True(CellarMasters.SalaryFor(4, [CellarSkill.Sekt, CellarSkill.Barrique]) > CellarMasters.SalaryFor(4, [CellarSkill.Sekt]));
    }

    [Fact]
    public void Jeden_Januar_bewerben_sich_drei_neue_Kandidaten()
    {
        var s = Start();
        Assert.Equal(3, s.CellarMasterCandidates.Count);
        foreach (var c in s.CellarMasterCandidates)
        {
            Assert.True(c.Talent >= 1);
            Assert.True(c.Talent <= 5);
            Assert.True(c.Skills.Count >= 1);
            Assert.Equal(c.Skills.Count, c.Skills.Distinct().Count());
        }
        s = Game.ApplyAction(s, new GameAction.HireCellarMaster(1));
        Assert.Equal(2, s.CellarMasterCandidates.Count);
        var before = s.CellarMasterCandidates.Select(c => c.Name).ToList();
        for (var i = 0; i < 12; i++) s = Game.ApplyAction(s, EndTurn);
        Assert.Equal(1, s.Month);
        Assert.Equal(3, s.CellarMasterCandidates.Count);
        Assert.NotEqual(before, s.CellarMasterCandidates.Select(c => c.Name).ToList());
        Assert.DoesNotContain(Current(s).CellarMaster!.Name, s.CellarMasterCandidates.Select(c => c.Name));
    }

    [Fact]
    public void Ein_neuer_Kellermeister_loest_den_bisherigen_ab()
    {
        var s = Game.ApplyAction(Start(), new GameAction.HireCellarMaster(0));
        var first = Current(s).CellarMaster!.Name;
        s = Game.ApplyAction(s, new GameAction.HireCellarMaster(0));
        Assert.NotEqual(first, Current(s).CellarMaster!.Name);
        Assert.Contains($"löst {first} ab", s.Log[^1].Text);
    }

    [Fact]
    public void Computergegner_suchen_sich_einen_Kellermeister_mit_den_Faehigkeiten_die_ihr_Profil_braucht()
    {
        foreach (var seed in new[] { 1, 2, 3 })
        {
            // Der Traditionswinzer setzt auf Sekt und Barrique
            var state = SimulateSolo(seed, 8, RegionId.Rheingau, AiProfileId.Tradition).State;
            var skills = state.Players[0].CellarMaster?.Skills ?? [];
            Assert.True(skills.Any(s => s == CellarSkill.Sekt || s == CellarSkill.Barrique), $"Seed {seed}");
        }
    }
}
