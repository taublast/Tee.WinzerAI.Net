using Winzer.Engine;
using Winzer.Engine.Data;

namespace Winzer.Presentation;

/// <summary>
/// Spielt für den Menschen am Zug – über dieselben Wege wie die Oberfläche (<see cref="GameSession.Run"/>).
/// Gedacht zum Prüfen: Eine Partie läuft von selbst bis zum Ende, und der Pilot probiert dabei jede Art von Zug
/// mindestens einmal aus, sobald die Lage es erlaubt. Er spielt solide, aber nicht auf Sieg optimiert.
/// </summary>
public sealed class AutoPilot
{
    /// <summary>Wie oft welche Art von Zug ausgeführt wurde</summary>
    public Dictionary<string, int> Used { get; } = [];
    /// <summary>Meldungen der Sitzung zu abgelehnten Zügen – sollte leer bleiben, weil der Pilot vorher prüft</summary>
    public List<string> Errors { get; } = [];
    public int Turns { get; private set; }
    /// <summary>
    /// Probiert auch riskante und teure Züge aus (Panschen, Sabotage, Eiswein-Wette, Roden, Verkaufen …), damit eine
    /// Partie jede Zugart berührt. Ohne das spielt der Pilot vorsichtig und auf Bestand.
    /// </summary>
    public bool Adventurous { get; init; } = true;

    readonly HashSet<string> once = [];
    int plantedYear;
    int sektYear;
    /// <summary>Parzelle, die im Oktober für die Eiswein-Wette hängen bleibt</summary>
    string? iceCandidate;

    /// <summary>Kreditrahmen zu mehr als der Hälfte genutzt: nur noch das Nötige ausgeben.</summary>
    static bool Tight(GameSession session)
    {
        var limit = Market.CreditLimit(session.Player);
        return limit <= 0 || session.Player.Loan > limit * 0.5;
    }

    bool Try(GameSession session, GameAction action)
    {
        if (session.Problem(action) is not null) return false;
        session.Run(action);
        if (session.Error.Length > 0)
        {
            Errors.Add($"{RulesV2.TurnName(session.State)} Jahr {session.State.Year}: {action.GetType().Name} – {session.Error}");
            session.ClearError();
            return false;
        }
        var name = action.GetType().Name;
        Used[name] = Used.GetValueOrDefault(name) + 1;
        return true;
    }

    /// <summary>Einmalige Erkundung: führt den Zug höchstens einmal je Partie aus.</summary>
    void Once(GameSession session, string key, GameAction action)
    {
        if (once.Contains(key)) return;
        if (Try(session, action)) once.Add(key);
    }

    /// <summary>Ein ganzer Zug des Menschen: alles Sinnvolle erledigen und den Zug beenden.</summary>
    public void PlayTurn(GameSession session)
    {
        if (session.Over) return;
        if (session.Summary is not null) session.CloseSummary();
        while (session.Nav.Overlay != Overlay.None) session.Nav.Show(Overlay.None);
        Auctions(session);
        if (session.Over) return;

        var state = session.State;
        var me = session.Player;
        var month = state.Month;

        // Erweiterte Regeln: Fernziele, Jahresziel, Preis
        if (RulesV2.On(state))
        {
            foreach (var ambition in RulesV2.Ambitions)
            {
                if (session.Player.Ambitions.Count >= RulesV2.AmbitionCount) break;
                Try(session, new GameAction.ChooseAmbition(ambition.Id));
            }
            if (session.Player.Goal is null && session.Player.GoalOffers.Count > 0) Try(session, new GameAction.ChooseGoal(session.Player.GoalOffers[0]));
            var level = session.Player.Reputation >= 40 ? 0.1 : 0;
            if (Math.Abs(session.Player.PriceLevel - level) > 0.001) Try(session, new GameAction.SetPriceLevel(level));
        }

        // Entscheidung: die erste Möglichkeit, die geht
        if (session.Player.Decision is { } decision)
        {
            for (var i = 0; i < decision.Options.Count; i++)
            {
                if (Try(session, new GameAction.Decide(i))) break;
            }
        }

        // Flüssig bleiben
        if (session.Player.Money < 25_000)
        {
            var amount = Math.Min(60_000, Math.Floor(Market.AvailableCredit(session.Player) / 1000) * 1000);
            if (amount >= 1000) Try(session, new GameAction.Borrow(amount));
            else Emergency(session);
        }
        else if (session.Player.Loan > 0 && session.Player.Money > 160_000)
        {
            Try(session, new GameAction.Repay(Math.Min(session.Player.Loan, Math.Floor((session.Player.Money - 100_000) / 1000) * 1000)));
        }

        // Schutz
        if (month <= 4) Try(session, new GameAction.InsureHarvest());
        if (Defense.FrostChance(session.State, session.Player) >= 0.1) Try(session, new GameAction.FrostCandles());

        // Kellermeister
        if (session.Player.CellarMaster is null && session.Player.Money > (Adventurous ? 80_000 : 150_000) && !Tight(session) && session.State.CellarMasterCandidates.Count > 0)
        {
            // Am liebsten jemand, der Barrique oder Sekt beherrscht
            var candidates = session.State.CellarMasterCandidates;
            var index = candidates.FindIndex(c => c.Skills.Contains(CellarSkill.Barrique));
            if (index < 0) index = candidates.FindIndex(c => c.Skills.Contains(CellarSkill.Sekt));
            Try(session, new GameAction.HireCellarMaster(Math.Max(0, index)));
        }

        // Pflanzen: höchstens eine Brache im Jahr, mit Polster
        if (Array.IndexOf(Rules.PlantMonths, month) >= 0 && plantedYear != state.Year)
        {
            var fallow = session.Player.Plots.FirstOrDefault(p => p.Grape is null);
            var region = Regions.All[session.Player.RegionId];
            // Die erste Brache mit der Gebietssorte, danach die beste rote Sorte – für Rosé und Barrique
            var hasRed = session.Player.Plots.Any(p => p.Grape is { } g && Grapes.All[g].Color == GrapeColor.Rot);
            var grape = hasRed || session.Player.Plots.Count(p => p.Grape is not null) < 2
                ? region.StartGrape
                : Grapes.All.Values.Where(g => g.Color == GrapeColor.Rot).OrderByDescending(g => region.Suitability[g.Id]).First().Id;
            if (fallow is not null)
            {
                var cost = Grapes.All[grape].PlantCostPerHa * fallow.Hectares;
                if (session.Player.Money < cost + 20_000 && state.Year <= 4 && !Tight(session) && Market.AvailableCredit(session.Player) >= cost + 60_000)
                {
                    Try(session, new GameAction.Borrow(Math.Ceiling(cost / 1000) * 1000));
                }
                if (session.Player.Money >= cost + 20_000 && Try(session, new GameAction.Plant(fallow.Id, grape))) plantedYear = state.Year;
            }
        }

        Harvest(session);
        Cellar(session);
        Bottles(session);
        Sales(session);
        if (!Tight(session)) Investments(session);
        if (Adventurous) Explore(session);

        if (session.Over) return;
        // Einmal vorspulen statt den Zug zu beenden, wenn gerade nichts Wichtiges ansteht
        var skipped = false;
        if (!once.Contains("skip") && session.State.Year >= 2 && month == 5 && Advisor.ImportantHints(session.State).Count == 0)
        {
            Once(session, "skip", new GameAction.AutoSkipTurn());
            skipped = once.Contains("skip");
        }
        if (!skipped && !Try(session, new GameAction.EndTurn())) return;
        Turns++;
    }

    void Auctions(GameSession session)
    {
        for (var guard = 0; guard < 200 && session.LiveLot is { } lot; guard++)
        {
            var me = session.Player;
            var minimum = Auction.NextMinimum(lot);
            // Bieten, solange es unter dem Schätzwert bleibt und ein Polster bleibt; sonst aussteigen
            // Nur aus eigenem Geld und mit Polster – oder einmal zum Ausprobieren
            var affordable = me.Money >= minimum + 60_000 || (Adventurous && !once.Contains("gebot") && minimum + 40_000 <= Auction.MaxAffordableBid(me));
            var worthIt = minimum <= lot.Estimate * 0.9 && affordable && lot.Leader != me.Id;
            if (worthIt && Try(session, new GameAction.AuctionBid(lot.Id, minimum)))
            {
                once.Add("gebot");
            }
            else
            {
                if (!Try(session, new GameAction.AuctionPass(lot.Id))) break;
            }
        }
    }

    void Harvest(GameSession session)
    {
        var state = session.State;
        bool Ripe(Plot p) => p.Grape is not null && !p.HarvestedThisYear && p.Oechsle > 0 && Rules.PlotAgeFactor(p, state.Year) > 0;
        // Einmal eine Nebenlage über den Oktober hinaus hängen lassen und im November auf Eiswein wetten
        if (Adventurous && iceCandidate is null && !once.Contains("eiswein") && state.Year >= 3 && state.Month == 10 && session.Player.Plots.Count(Ripe) >= 2)
        {
            iceCandidate = session.Player.Plots.Where(Ripe).OrderBy(p => p.Hectares).First().Id;
        }
        foreach (var plot in session.Player.Plots.ToList())
        {
            if (!Ripe(plot) || plot.IceWineGamble) continue;
            var grapeId = plot.Grape!.Value;
            if (plot.Id == iceCandidate)
            {
                if (state.Month == 10) continue;
                iceCandidate = null;
                Once(session, "eiswein", new GameAction.GambleIceWine(plot.Id, true));
                if (session.Player.Plots.First(p => p.Id == plot.Id).IceWineGamble) continue;
            }
            var late = state.Month >= 11 || (state.Month == 10 && state.HalfMonth != 1);
            if (!late && plot.Oechsle < 85) continue;
            // Reicht der Tank nicht, vorher erweitern – sonst geht der Most billig weg
            var preview = Game.HarvestPreview(state, session.Player, plot);
            var free = session.Player.TankCapacityHl - Rules.UsedTankHl(session.Player);
            if (preview.Hectoliters > free)
            {
                Try(session, new GameAction.ExpandTank(Math.Ceiling((preview.Hectoliters - free) / 50) * 50));
            }
            var rose = Adventurous && Grapes.All[grapeId].Color == GrapeColor.Rot && !once.Contains("rose");
            if (Try(session, new GameAction.Harvest(plot.Id, rose)) && rose) once.Add("rose");
        }
    }

    void Cellar(GameSession session)
    {
        var state = session.State;
        foreach (var batch in session.Player.Cellar.ToList())
        {
            if (Adventurous && Products.IsFreshMust(state, batch)) Once(session, "federweisser", new GameAction.SellFederweisser(batch.Id, Math.Min(2, batch.Hectoliters)));
            if (batch.MonthsUntilReady > 0 || batch.Barrique && batch.MonthsUntilReady > 0) continue;
            var current = session.Player.Cellar.Find(b => b.Id == batch.Id);
            if (current is null) continue;
            // Einmal im Jahr etwas Sekt, einmal Rotwein ins Fass, einmal panschen
            if (Adventurous)
            {
                if (sektYear != state.Year && current.Hectoliters >= 12 && Try(session, new GameAction.MakeSekt(current.Id, 10))) sektYear = state.Year;
                // Ins Barrique passt nur eine ganze Charge: einmal den größten Teil abfüllen und den Rest ins Fass legen
                var freeBarrique = session.Player.BarriqueCapacityHl - Rules.UsedBarriqueHl(session.Player);
                if (!once.Contains("barrique") && !current.Barrique && !current.Rose && freeBarrique >= 15 && CellarMasters.HasSkill(session.Player, CellarSkill.Barrique))
                {
                    var rest = Math.Floor(current.Hectoliters) - 15;
                    if (rest < 1 || Try(session, new GameAction.Bottle(current.Id, rest)))
                    {
                        if (Try(session, new GameAction.ToBarrique(current.Id))) once.Add("barrique");
                        continue;
                    }
                }
                if (state.Year >= 3) Once(session, "panschen", new GameAction.Adulterate(current.Id, AdulterationMethod.Wasser));
            }
            current = session.Player.Cellar.Find(b => b.Id == batch.Id);
            if (current is null || current.MonthsUntilReady > 0) continue;
            var whole = Math.Floor(current.Hectoliters);
            if (whole >= 1 && Try(session, new GameAction.Bottle(current.Id, whole))) continue;
            // Kein Geld für Flaschen: die Hälfte als Fasswein, das bringt Luft
            if (whole >= 2) Try(session, new GameAction.SellBulk(current.Id, Math.Floor(whole / 2)));
        }
    }

    void Bottles(GameSession session)
    {
        var state = session.State;
        foreach (var lot in session.Player.Bottles.ToList())
        {
            if (!lot.Registered) Try(session, new GameAction.Register(lot.Id));
        }
        var competition = Market.UpcomingCompetition(state.Month);
        if (competition.Month == state.Month)
        {
            foreach (var lot in session.Player.Bottles.Where(l => l.Registered).OrderByDescending(Market.CompetitionBaseScore).Take(competition.MaxEntries).ToList())
            {
                Try(session, new GameAction.EnterCompetition(lot.Id));
            }
        }
    }

    void Sales(GameSession session)
    {
        var state = session.State;
        foreach (var offer in session.Player.OrderOffers.ToList())
        {
            if (Orders.EligibleBottles(session.Player, offer) >= offer.Bottles) Try(session, new GameAction.AcceptOrder(offer.Id));
            else if (offer.OfferExpires <= Bio.GameMonth(state)) Try(session, new GameAction.DeclineOrder(offer.Id));
        }
        foreach (var order in session.Player.Orders.ToList()) Try(session, new GameAction.DeliverOrder(order.Id));
        if (session.Player.ExportOffer is { } export)
        {
            var enough = Exports.ExportEligible(session.Player, export).Sum(l => l.Bottles) >= export.BottlesPerMonth * 4;
            Try(session, new GameAction.AnswerExport(enough));
        }
        // Überfülltes Lager über den Großhandel räumen
        var demand = Math.Max(1, RulesV2.DirectDemand(state, session.Player));
        var stock = session.Player.Bottles.Where(l => l.Registered && l.MonthsUntilReady == 0).Sum(l => l.Bottles);
        var keep = Tight(session) ? 2 : 4;
        if (stock > demand * (keep + 3))
        {
            foreach (var lot in session.Player.Bottles.Where(l => l.Registered && l.MonthsUntilReady == 0 && !session.Player.CompetitionEntries.Contains(l.Id)).OrderBy(l => l.Quality).ToList())
            {
                if (stock <= demand * keep) break;
                var count = Math.Min(lot.Bottles, stock - demand * keep);
                if (count >= 1 && Try(session, new GameAction.SellWholesale(lot.Id, Math.Floor(count)))) stock -= Math.Floor(count);
            }
        }
        if (Tight(session)) return;
        if (session.Player.Money > 50_000) Try(session, new GameAction.Marketing(MarketingKind.Social));
        if (session.Player.Money > 120_000)
        {
            Try(session, new GameAction.Marketing(MarketingKind.Hoffest));
            Try(session, new GameAction.Marketing(MarketingKind.Messe));
        }
    }

    /// <summary>Kein Geld, kein Kredit: Flaschen an den Großhandel, Wein an die Kellerei.</summary>
    void Emergency(GameSession session)
    {
        foreach (var lot in session.Player.Bottles.Where(l => l.Registered && l.MonthsUntilReady == 0).ToList())
        {
            if (session.Player.Money >= 25_000) return;
            Try(session, new GameAction.SellWholesale(lot.Id, lot.Bottles));
        }
        foreach (var batch in session.Player.Cellar.ToList())
        {
            if (session.Player.Money >= 25_000) return;
            var whole = Math.Floor(batch.Hectoliters);
            if (whole >= 1) Try(session, new GameAction.SellBulk(batch.Id, whole));
        }
    }

    void Investments(GameSession session)
    {
        var me = session.Player;
        if (!me.HasOnlineShop && me.Money > 80_000) Try(session, new GameAction.BuyOnlineShop());
        if (!session.Player.HasVinothek && session.Player.Money > 300_000) Try(session, new GameAction.BuildVinothek());
        if (Adventurous && session.Player.BarriqueCapacityHl <= 0 && session.Player.Money > 60_000 && CellarMasters.HasSkill(session.Player, CellarSkill.Barrique))
        {
            Try(session, new GameAction.ExpandBarrique(20));
        }
        if (!RulesV2.On(session.State)) return;
        foreach (var building in RulesV2.Buildings.OrderBy(b => b.Cost))
        {
            if (!session.Player.Buildings.Contains(building.Id) && session.Player.Money >= building.Cost + 100_000)
            {
                Try(session, new GameAction.Construct(building.Id));
                break;
            }
        }
        if (session.Player.Money >= RulesV2.DonationCost(session.Player) + 400_000) Try(session, new GameAction.Donate());
    }

    /// <summary>Züge, die ein vorsichtiger Spieler selten macht – je einmal, damit die Partie alles berührt.</summary>
    void Explore(GameSession session)
    {
        var state = session.State;
        var me = session.Player;
        if (state.Year >= 3) Once(session, "strauss", new GameAction.OpenStrausswirtschaft());
        var bearing = me.Plots.FirstOrDefault(p => p.Grape is not null);
        if (bearing is not null && state.Year >= 4 && me.Money > 60_000)
        {
            Once(session, "hagelnetz", new GameAction.InstallHailNet(bearing.Id));
            Once(session, "bewaesserung", new GameAction.InstallIrrigation(bearing.Id));
        }
        var rival = state.Players.FirstOrDefault(p => p.Id != me.Id && !p.Bankrupt);
        if (rival is not null && state.Year >= 3 && me.Money > 80_000) Once(session, "geruecht", new GameAction.Sabotage(rival.Id, SabotageKind.Geruecht));
        if (state.Year >= 5)
        {
            if (!once.Contains("bio") && Try(session, new GameAction.SetProtection(ProtectionStrategy.Bio))) once.Add("bio");
            Try(session, new GameAction.ApplyBioLabel());
        }
        if (state.Year >= 6 && state.Month == 1 && me.CellarMaster is not null && state.CellarMasterCandidates.Count > 0)
        {
            Once(session, "entlassen", new GameAction.FireCellarMaster());
        }
        // Spät im Spiel: die kleinste Lage roden und eine verkaufen, wenn genug andere da sind
        if (state.Year >= 8 && me.Plots.Count(p => p.Grape is not null) >= 3)
        {
            var smallest = me.Plots.Where(p => p.Grape is not null).OrderBy(p => p.Hectares).First();
            Once(session, "roden", new GameAction.ClearPlot(smallest.Id));
        }
        if (state.Year >= 9 && session.Player.Plots.Count >= 4)
        {
            var fallow = session.Player.Plots.FirstOrDefault(p => p.Grape is null);
            if (fallow is not null) Once(session, "verkaufen", new GameAction.SellPlot(fallow.Id));
        }
    }

    /// <summary>Spielt, bis die Partie endet oder die Zahl der Züge erreicht ist.</summary>
    public void PlayToEnd(GameSession session, int maxTurns = 2000)
    {
        while (!session.Over && Turns < maxTurns)
        {
            var before = Turns;
            PlayTurn(session);
            if (Turns == before) break;
        }
    }
}
