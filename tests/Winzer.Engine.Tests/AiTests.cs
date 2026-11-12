using Winzer.Engine.Data;
using static Winzer.Engine.Tests.TestHelpersA;

namespace Winzer.Engine.Tests;

/// <summary>ai.test.ts</summary>
public class AiTests
{
    static readonly List<AiProfileId> Profiles = Ai.AiProfiles.Keys.ToList();
    static readonly List<RegionId> AllRegions = Regions.All.Keys.ToList();

    /// <summary>it.each(profiles) des Originals: Object.keys(AI_PROFILES)</summary>
    public static TheoryData<AiProfileId> ProfileData() => new(Ai.AiProfiles.Keys);

    [Theory]
    [MemberData(nameof(ProfileData))]
    public void Profil_spielt_8_Jahre_in_jedem_Gebiet_fehlerfrei_und_sammelt_Punkte(AiProfileId ai)
    {
        var survived = 0;
        foreach (var regionId in AllRegions)
        {
            var result = Simulate.Run(new SimOptions { Seed = 9, Years = 8, Players = [new SimPlayer { RegionId = regionId, Ai = ai }] });
            var player = result.State.Players[0];
            Assert.True(double.IsFinite(player.Money));
            Assert.True(player.Points > 0);
            if (result.Snapshots.Count == 8 && !player.Bankrupt) survived++;
        }
        // Die soliden Profile gehen nirgends pleite; das Schlitzohr darf sich gelegentlich verzocken.
        var required = Ai.AiProfiles[ai].Intrigue ? (int)Math.Ceiling(AllRegions.Count * 0.75) : AllRegions.Count;
        Assert.True(survived >= required, $"überlebt: {survived}, verlangt: {required}");
    }

    [Fact]
    public void Kauft_im_Lauf_der_Jahre_Weinberge_dazu()
    {
        var result = Simulate.Run(new SimOptions
        {
            Seed = 2, Years = 10, Players = [new SimPlayer { RegionId = RegionId.Pfalz, Ai = AiProfileId.Schlitzohr }],
        });
        Assert.True(result.Snapshots[^1].Players[0].Hectares > 10);
    }

    [Fact]
    public void Ist_mit_gleichem_Seed_reproduzierbar()
    {
        SimOptions Options() => new()
        {
            Seed = 4, Years = 4, Players = Profiles.Select(ai => new SimPlayer { RegionId = RegionId.Mosel, Ai = ai }).ToList(),
        };
        Assert.Equal(JsonOf(Simulate.Run(Options()).Snapshots), JsonOf(Simulate.Run(Options()).Snapshots));
    }

    [Fact]
    public void Zieht_nach_dem_Menschen_und_gibt_den_Zug_zurueck()
    {
        var state = Game.NewGame(new NewGameOptions
        {
            Seed = 1,
            Players = [Human("Anna", RegionId.Mosel), Computer("KI", RegionId.Pfalz, AiProfileId.Tradition)],
        });
        state = Ai.RunAiTurns(Game.ApplyAction(state, EndTurn));
        Assert.Equal(2, state.Month);
        Assert.Equal("Anna", CurrentPlayer(state).Name);
    }

    [Fact]
    public void Beginnt_die_Partie_selbst_wenn_ein_Computergegner_zuerst_dran_ist()
    {
        var state = Ai.RunAiTurns(Game.NewGame(new NewGameOptions
        {
            Seed = 1,
            Players = [Computer("KI", RegionId.Pfalz, AiProfileId.Bio), Human("Anna", RegionId.Mosel)],
        }));
        Assert.Equal("Anna", CurrentPlayer(state).Name);
        Assert.Equal(1, state.Month);
    }

    [Fact]
    public void Spielt_ohne_Menschen_nicht_von_allein_weiter()
    {
        var state = Game.NewGame(new NewGameOptions { Seed = 1, Players = [Computer("KI", RegionId.Pfalz, AiProfileId.Bio)] });
        Assert.Same(state, Ai.RunAiTurns(state));
    }
}
