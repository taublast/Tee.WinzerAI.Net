using Winzer.Engine.Data;

namespace Winzer.Engine;

/// <summary>Weinberg: Reife, Krankheiten, Wetterereignisse, Eiswein und Pflanzenschutz (vineyard.ts).</summary>
public static class Vineyard
{
    /// <summary>Basis-Reifezuwachs in °Oe am Ende des jeweiligen Monats</summary>
    static readonly Dictionary<int, double> RipeningBase = new() { [8] = 20, [9] = 18, [10] = 12, [11] = 5 };
    public const double RipeningStartOechsle = 45;
    /// <summary>Mengenverlust durch Fäulnis am Ende des Monats</summary>
    static readonly Dictionary<int, double> RotLoss = new() { [10] = 0.05, [11] = 0.25 };
    /// <summary>Edelfäule: Chance bei wechselhaftem Herbstwetter (Morgennebel, Nachmittagssonne)</summary>
    const double BotrytisChance = 0.35;
    const double BotrytisOechsle = 30;

    static bool Bearing(GameState state, Plot plot) =>
        plot.Grape is not null && !plot.HarvestedThisYear && Rules.PlotAgeFactor(plot, state.Year) > 0;

    /// <summary>Reife, Fäulnis, Krankheiten und Edelfäule einer Parzelle am Monatsende</summary>
    /// <summary>
    /// Reifezuwachs dieses Monats in °Oechsle bei diesem Wetter – dieselbe Rechnung wie in <see cref="GrowPlot"/>,
    /// ohne die Parzelle zu verändern. Regelversion 2 teilt ihn auf zwei Monatshälften auf.
    /// </summary>
    public static double RipeningGain(GameState state, Player player, Plot plot, WeatherKind weather)
    {
        if (!Bearing(state, plot)) return 0;
        var grape = Grapes.All[plot.Grape!.Value];
        if (!RipeningBase.TryGetValue(state.Month, out var baseGain) || plot.Oechsle >= grape.MaxOechsle) return 0;
        var headroom = (grape.MaxOechsle - plot.Oechsle) / (grape.MaxOechsle - RipeningStartOechsle);
        var warmth = Regions.All[player.RegionId].Warmth + Rules.ClimateWarming(state);
        var gain = (baseGain * Weather.SunFactor[weather] * (0.8 + warmth * 0.04) * headroom) / grape.RipeningNeed;
        return Math.Min(grape.MaxOechsle, plot.Oechsle + gain) - plot.Oechsle;
    }

    public static void GrowPlot(GameState state, Player player, Plot plot, WeatherKind weather)
    {
        if (!Bearing(state, plot)) return;
        var grape = Grapes.All[plot.Grape!.Value];
        var region = Regions.All[player.RegionId];
        var shield = 1 - Economy.Protection[player.Protection].Protection;
        double Roll() => Rng.NextFloat(state.Rng);

        if (state.Month == 7) plot.Oechsle = RipeningStartOechsle;

        if (RipeningBase.TryGetValue(state.Month, out var baseGain) && plot.Oechsle < grape.MaxOechsle)
        {
            var headroom = (grape.MaxOechsle - plot.Oechsle) / (grape.MaxOechsle - RipeningStartOechsle);
            var warmth = region.Warmth + Rules.ClimateWarming(state);
            var gain = (baseGain * Weather.SunFactor[weather] * (0.8 + warmth * 0.04) * headroom) / grape.RipeningNeed;
            plot.Oechsle = Math.Min(grape.MaxOechsle, plot.Oechsle + gain);
        }

        // Pilzkrankheiten und Schädlinge – Pflanzenschutz senkt das Risiko
        if (state.Month >= 5 && state.Month <= 8 && weather == WeatherKind.Nass)
        {
            if (Roll() < 0.4 * grape.DiseaseSusceptibility * (0.5 + region.Wetness / 10) * shield)
            {
                plot.CropFactor *= 0.7;
                Util.AddLog(state, player.Id, $"{plot.Name}: Peronospora (Falscher Mehltau) – rund 30 % weniger Ertrag.");
            }
        }
        if (state.Month >= 6 && state.Month <= 8 && weather == WeatherKind.Sonnig)
        {
            if (Roll() < 0.25 * grape.DiseaseSusceptibility * shield)
            {
                plot.Health = Math.Max(0, plot.Health - 0.2);
                Util.AddLog(state, player.Id, $"{plot.Name}: Oidium (Echter Mehltau) – die Trauben leiden.");
            }
        }
        if (state.Month == 9 && grape.Color == GrapeColor.Rot && Roll() < 0.3 * shield)
        {
            plot.CropFactor *= 0.85;
            plot.Health = Math.Max(0, plot.Health - 0.1);
            Util.AddLog(state, player.Id, $"{plot.Name}: Die Kirschessigfliege ist da – Verluste bei den roten Trauben.");
        }

        if (state.Month >= 9 && weather == WeatherKind.Nass)
        {
            plot.Health = Math.Max(0, plot.Health - 0.15 * grape.RotSusceptibility);
        }
        if (RotLoss.TryGetValue(state.Month, out var rotLoss))
        {
            var wetBonus = weather == WeatherKind.Nass ? 0.1 * grape.RotSusceptibility : 0;
            plot.CropFactor *= 1 - rotLoss - wetBonus;
        }

        // Edelfäule: Fluch für die Menge, Segen fürs Mostgewicht
        if ((state.Month == 10 || state.Month == 11) && weather == WeatherKind.Wechselhaft && plot.Oechsle >= 80)
        {
            if (Roll() < BotrytisChance * (0.5 + grape.RotSusceptibility))
            {
                plot.Oechsle += BotrytisOechsle;
                plot.CropFactor *= plot.Botrytis ? 0.6 : 0.5;
                Util.AddLog(
                    state,
                    player.Id,
                    plot.Botrytis
                        ? $"{plot.Name}: Die Beeren schrumpfen zu Rosinen – {Util.JsNumber(Js.Round(plot.Oechsle))} °Oe!"
                        : $"{plot.Name}: Edelfäule (Botrytis)! Weniger Menge, aber {Util.JsNumber(Js.Round(plot.Oechsle))} °Oe.");
                plot.Botrytis = true;
            }
        }
    }

    /// <summary>Wetterereignisse, die den ganzen Betrieb treffen: Spätfrost, Hagel, Hitzewelle, Dürre – samt Wetterabwehr</summary>
    public static void VineyardEvents(GameState state, Player player, WeatherKind weather)
    {
        var plots = player.Plots.Where(p => Bearing(state, p)).ToList();
        if (plots.Count == 0) return;
        double Roll() => Rng.NextFloat(state.Rng);

        if (Roll() < Defense.FrostChance(state, player))
        {
            if (player.FrostCandles)
            {
                Util.AddLog(state, player.Id, "🕯️ Spätfrost in der Nacht – deine Frostkerzen haben die jungen Triebe gerettet.", LogTone.Good);
            }
            else
            {
                Util.AddLog(state, player.Id, "Spätfrost! Die jungen Triebe sind erfroren – rund 40 % weniger Ertrag in diesem Jahr.", LogTone.Bad);
                Defense.CropLoss(state, player, plots, 1 - Economy.FrostLoss, "Spätfrost");
            }
        }

        if (Economy.HailMonths.Contains(state.Month) && Roll() < Defense.HailChance(state))
        {
            // Eine Parzelle, manchmal zwei – ohne Zurücklegen gezogen
            var pool = new List<Plot>(plots);
            var hitCount = Math.Min(pool.Count, Roll() < 0.3 ? 2 : 1);
            var hit = new List<Plot>();
            for (var i = 0; i < hitCount; i++)
            {
                var index = (int)Math.Floor(Roll() * pool.Count);
                hit.Add(pool[index]);
                pool.RemoveAt(index);
            }
            foreach (var plot in hit)
            {
                if (plot.HailNet)
                {
                    Util.AddLog(state, player.Id, $"Hagelschlag über {plot.Name} – das Hagelnetz hält das Meiste ab.", LogTone.Good);
                    Defense.CropLoss(state, player, new List<Plot> { plot }, 1 - Economy.HailLoss * (1 - Economy.HailNetProtection), "Hagel");
                }
                else
                {
                    plot.Health = Math.Max(0, plot.Health - 0.1);
                    Util.AddLog(state, player.Id, $"Hagelschlag über {plot.Name} – die Hälfte der Trauben ist zerstört.", LogTone.Bad);
                    Defense.CropLoss(state, player, new List<Plot> { plot }, 1 - Economy.HailLoss, "Hagel");
                }
            }
        }

        var heatChance = 0.15 + (state.ClimateChange ? (state.Year - 1) * Economy.ClimateHeatPerYear : 0);
        if ((state.Month == 7 || state.Month == 8) && weather == WeatherKind.Sonnig && Roll() < heatChance)
        {
            foreach (var plot in plots)
            {
                plot.Oechsle += 4;
                if (plot.Irrigation) continue;
                plot.CropFactor *= 0.9;
                plot.Health = Math.Max(0, plot.Health - 0.05);
            }
            var watered = plots.Count(p => p.Irrigation);
            Util.AddLog(
                state,
                player.Id,
                watered == plots.Count
                    ? "Hitzewelle: Die Bewässerung hält die Reben frisch – nur die Süße legt zu."
                    : $"Hitzewelle: Sonnenbrand an den Trauben, etwas weniger Menge, dafür süßer{(watered != 0 ? " (bewässerte Lagen ohne Schaden)" : "")}.");
        }

        if (Economy.DroughtMonths.Contains(state.Month) && weather == WeatherKind.Sonnig && Roll() < Defense.DroughtChance(state, player))
        {
            var dry = plots.Where(p => !p.Irrigation).ToList();
            if (dry.Count == 0)
            {
                Util.AddLog(state, player.Id, "💧 Dürre im Hochsommer – deine Bewässerung bringt alle Lagen gut durch.", LogTone.Good);
            }
            else
            {
                foreach (var plot in dry) plot.Health = Math.Max(0, plot.Health - 0.05);
                Util.AddLog(
                    state,
                    player.Id,
                    $"Dürre! Unbewässerte Reben leiden unter Trockenstress – rund {Util.JsNumber(Js.Round(Economy.DroughtLoss * 100))} % weniger Ertrag dort.",
                    LogTone.Bad);
                Defense.CropLoss(state, player, dry, 1 - Economy.DroughtLoss, "Dürre");
            }
        }
    }

    /// <summary>Chance auf starken Dezemberfrost – der Klimawandel macht Eiswein Jahr für Jahr seltener.</summary>
    public static double IceWineChance(GameState state, RegionId regionId)
    {
        var baseChance = Economy.IceWineBaseChance + Regions.All[regionId].FrostRisk * Economy.IceWineChancePerFrostRisk;
        var loss = state.ClimateChange ? (state.Year - 1) * Economy.ClimateIceWineLossPerYear : 0;
        return Math.Max(0.05, baseChance - loss);
    }

    public static string? ValidateIceWineGamble(GameState state, Player player, string plotId, bool gamble)
    {
        var plot = player.Plots.Find(p => p.Id == plotId);
        if (plot is null) return "Unbekannte Parzelle.";
        if (!gamble) return plot.IceWineGamble ? null : "Die Parzelle ist nicht für Eiswein vorgesehen.";
        if (state.Month != 11 && state.Month != 12) return "Für Eiswein entscheidet man im November oder Dezember.";
        if (!Bearing(state, plot)) return "Hier hängen keine Trauben mehr.";
        if (plot.IceWineGamble) return "Die Trauben hängen bereits für Eiswein.";
        if (plot.Oechsle < Economy.IceWineMinOechsle) return $"Für Eiswein braucht es mindestens {Util.JsNumber(Economy.IceWineMinOechsle)} °Oe.";
        return null;
    }

    /// <summary>
    /// Ende Dezember: Bei starkem Frost werden die gefrorenen Trauben zu Eiswein gelesen,
    /// sonst sind sie verloren. <paramref name="frost"/> wird je Anbaugebiet einmal ausgewürfelt.
    /// </summary>
    public static void ResolveIceWine(GameState state, Player player, bool frost)
    {
        foreach (var plot in player.Plots.Where(p => p.IceWineGamble && Bearing(state, p)).ToList())
        {
            plot.HarvestedThisYear = true;
            if (!frost)
            {
                Util.AddLog(state, player.Id, $"{plot.Name}: Kein Frost – die Trauben für den Eiswein sind verloren.");
                continue;
            }
            var grape = Grapes.All[plot.Grape!.Value];
            var oechsle = Math.Max(125, plot.Oechsle + 40);
            var hectoliters = Js.Round(
                plot.Hectares * grape.YieldPerHa * Rules.PlotAgeFactor(plot, state.Year) * Economy.IceWineYieldShare * plot.CropFactor * (0.6 + 0.4 * plot.Health));
            var cost = Js.Round(plot.Hectares * Economy.HarvestCostPerHa * 1.5);
            player.Money -= cost;
            player.Stats.Harvests++;
            player.Stats.Edelsuess++;
            player.Stats.BestLevelRank = Math.Max(player.Stats.BestLevelRank, Rules.LevelRank(QualityLevel.Eiswein));
            Rules.StoreWine(state, player, new WineBatch
            {
                Grape = grape.Id,
                Vintage = state.Year,
                Hectoliters = hectoliters,
                Oechsle = Js.Round(oechsle),
                Level = QualityLevel.Eiswein,
                Quality = Rules.ComputeQuality(player, plot, state.Year, oechsle),
            });
            Util.AddLog(
                state,
                player.Id,
                $"Klirrender Frost! {plot.Name}: {Util.JsNumber(hectoliters)} hl Eiswein mit {Util.JsNumber(Js.Round(oechsle))} °Oe gelesen ({Util.FormatEuro(cost)}).");
        }
    }

    public static string? ValidateProtection(Player player, ProtectionStrategy strategy) =>
        player.Protection == strategy ? "Diese Strategie ist bereits gewählt." : null;

    public static void SetProtection(GameState state, Player player, ProtectionStrategy strategy)
    {
        if (player.Protection == ProtectionStrategy.Bio) Bio.LoseBio(state, player, "kein Bio-Pflanzenschutz mehr");
        player.Protection = strategy;
        player.BioSince = strategy == ProtectionStrategy.Bio ? Bio.GameMonth(state) : null;
        Util.AddLog(
            state,
            player.Id,
            strategy == ProtectionStrategy.Bio
                ? "Umstellung auf Bio-Weinbau begonnen – nach zwei Jahren kann das Grünblatt-Siegel beantragt werden."
                : $"Pflanzenschutz: {Economy.Protection[strategy].Name}.");
    }
}
