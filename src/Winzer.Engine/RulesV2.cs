using Winzer.Engine.Data;

namespace Winzer.Engine;

/// <summary>Jahresziel: eines von drei Angeboten im Januar, Abrechnung im Dezember</summary>
public sealed record GoalDef(
    string Id, string Title, double Points,
    Func<GameState, Player, string> Text,
    Func<GameState, Player, bool> Available,
    Func<GameState, Player, (double Value, double Target)> Progress);

/// <summary>
/// Ambition: geheimes Fernziel, das beim Erreichen einmalig Punkte bringt. Die Punkte richten sich danach, wie schwer
/// das Ziel zu erreichen ist – wer viel will, muss viel wagen.
/// </summary>
public sealed record AmbitionDef(
    string Id, string Icon, string Name, string Text, double Points,
    Func<GameState, Player, (double Value, double Target)> Progress)
{
    /// <summary>Einstufung für die Anzeige, abgeleitet aus den Punkten</summary>
    public string Difficulty => Points >= 50 ? "schwer" : Points >= 25 ? "mittel" : "leicht";
}

/// <summary>Ausbau des Guts</summary>
public sealed record BuildingDef(string Id, string Icon, string Name, double Cost, double MonthlyCost, string Text);

/// <summary>
/// Regelversion 2: Erweiterungen gegenüber dem Original. Alles hier greift nur, wenn
/// <see cref="GameState.RulesVersion"/> mindestens 2 ist – Partien nach Originalregeln verlaufen unverändert.
/// <list type="bullet">
/// <item>Ab-Hof-Preise selbst setzen: Aufschlag gegen Nachfrage</item>
/// <item>Jahresziele und Jahresrückblick</item>
/// <item>Ambitionen als geheime Fernziele</item>
/// <item>Gutsausbau und Stiftung als Verwendung für Vermögen</item>
/// <item>Prämierung mit drei Juroren, deren Noten einzeln festgehalten werden</item>
/// <item>Lese in Halbmonaten (September bis November), Januar und Februar als ein Winterzug</item>
/// </list>
/// </summary>
public static class RulesV2
{
    public static bool On(GameState state) => state.RulesVersion >= 2;

    // ---- Zeit: Halbmonate und Winterzug ----

    public static bool HasHalves(int month) => month is 9 or 10 or 11;

    /// <summary>Bezeichnung des laufenden Zugs, z. B. „Oktober, 1. Hälfte“</summary>
    public static string TurnName(GameState state)
    {
        var month = Game.MonthNames[state.Month - 1];
        if (!On(state)) return month;
        if (state.Month == 1) return "Winter (Januar/Februar)";
        return state.HalfMonth > 0 ? $"{month}, {state.HalfMonth}. Hälfte" : month;
    }

    /// <summary>Bezeichnung des nächsten Zugs für die Schaltfläche „weiter“</summary>
    public static string NextTurnName(GameState state)
    {
        if (!On(state)) return Game.MonthNames[state.Month % 12];
        if (state.HalfMonth == 1) return $"die 2. {Game.MonthNames[state.Month - 1]}hälfte";
        var next = state.Month == 1 ? 3 : state.Month % 12 + 1;
        if (next == 1) return "Winter";
        return HasHalves(next) ? $"{Game.MonthNames[next - 1]} (1. Hälfte)" : Game.MonthNames[next - 1];
    }

    /// <summary>Erste Monatshälfte: die halbe Reife dieses Monats beim Wetter der ersten Hälfte, sonst nichts.</summary>
    public static void GrowFirstHalf(GameState state, Player player, Plot plot, WeatherKind weather)
    {
        plot.Oechsle += Vineyard.RipeningGain(state, player, plot, weather) / 2;
    }

    /// <summary>
    /// Zweite Monatshälfte: Krankheiten, Fäulnis und Edelfäule wie in einem ganzen Monat (einmal gewürfelt, mit dem
    /// Wetter der zweiten Hälfte), die Reife aber nur zur Hälfte – die andere Hälfte kam schon in der ersten.
    /// </summary>
    public static void GrowHalf(GameState state, Player player, Plot plot, WeatherKind weather)
    {
        var gain = Vineyard.RipeningGain(state, player, plot, weather);
        Vineyard.GrowPlot(state, player, plot, weather);
        plot.Oechsle -= gain / 2;
    }

    /// <summary>Ende der ersten Monatshälfte: Wetter und halber Reifeschritt, sonst nichts.</summary>
    public static void ResolveFirstHalf(GameState state)
    {
        var weather = Weather.DrawWeather(state.Rng, state.Forecast);
        state.LastWeather = weather;
        state.RipeningWeather.Add(weather);
        Util.AddLog(state, null, $"Wetter in der ersten {Game.MonthNames[state.Month - 1]}hälfte: {Weather.WeatherLabels[weather]}.");
        foreach (var player in state.Players)
        {
            if (player.Bankrupt) continue;
            foreach (var plot in player.Plots) GrowFirstHalf(state, player, plot, weather);
        }
        state.HalfMonth = 2;
        state.Forecast = Weather.MakeForecast(state.Rng, state.Month, 0.8);
    }

    // ---- Preise ----

    public const double MinPriceLevel = -0.2;
    public const double MaxPriceLevel = 0.4;

    /// <summary>Wie die Ab-Hof-Nachfrage auf den eigenen Preis reagiert: Bekannte Güter können mehr verlangen.</summary>
    public static double PriceDemandFactor(Player player)
    {
        var level = player.PriceLevel;
        if (level >= 0)
        {
            var elasticity = Math.Max(1.2, 2.2 - player.Reputation / 100);
            return Math.Max(0.15, 1 - elasticity * level);
        }
        return 1 + 1.5 * -level;
    }

    /// <summary>Ab-Hof-Nachfrage dieses Monats in Flaschen</summary>
    public static double DirectDemand(GameState state, Player player)
    {
        var demand = Market.MonthlyDemand(player);
        if (!On(state)) return demand;
        if (player.Buildings.Contains("schaenke")) demand *= 1.25;
        return Js.Round(demand * PriceDemandFactor(player));
    }

    /// <summary>Faktor auf den Ab-Hof-Preis je Flasche</summary>
    public static double DirectPriceFactor(GameState state, Player player)
    {
        if (!On(state)) return 1;
        return (1 + player.PriceLevel) * (player.Buildings.Contains("schatzkammer") ? 1.1 : 1);
    }

    // ---- Jahresziele ----

    static double Start(Player p, string key) => p.YearStart.GetValueOrDefault(key);

    static double SalesTarget(Player p) => Math.Max(2000, Js.Round((1500 + 500 * Start(p, "planted")) / 500) * 500);

    public static readonly GoalDef[] Goals =
    [
        new("medaillen", "Zwei Medaillen", 12,
            (s, p) => "Gewinne in diesem Jahr zwei Medaillen.",
            (s, p) => s.Year >= 2,
            (s, p) => (p.Stats.Medals - Start(p, "medals"), 2)),
        new("absatz", "Flaschen verkaufen", 8,
            (s, p) => $"Verkaufe in diesem Jahr {Util.FormatNumber(SalesTarget(p))} Flaschen.",
            (s, p) => s.Year >= 2,
            (s, p) => (p.Stats.BottlesSold - Start(p, "bottlesSold"), SalesTarget(p))),
        new("schulden", "Schulden halbieren", 8,
            (s, p) => $"Senke den Kredit bis Jahresende auf höchstens {Util.FormatEuro(Math.Floor(Start(p, "loan") / 2))}.",
            (s, p) => p.Loan >= 20_000,
            (s, p) => (Math.Max(0, Start(p, "loan") - p.Loan), Math.Ceiling(Start(p, "loan") / 2))),
        new("bekanntheit", "Bekannter werden", 8,
            (s, p) => $"Erreiche zum Jahresende eine Bekanntheit von {Util.JsNumber(Js.Round(Start(p, "reputation")) + 6)}.",
            (s, p) => p.Reputation < 80,
            (s, p) => (Js.Round(p.Reputation), Js.Round(Start(p, "reputation")) + 6)),
        new("flaeche", "Rebfläche vergrößern", 6,
            (s, p) => "Pflanze eine Brache oder ersteigere einen bestockten Weinberg.",
            (s, p) => true,
            (s, p) => (Market.PlantedHectares(p) > Start(p, "planted") ? 1 : 0, 1)),
        new("praedikat", "Eine Auslese lesen", 10,
            (s, p) => "Lies in diesem Jahr eine Auslese oder höher.",
            (s, p) => p.Plots.Any(plot => plot.Grape is not null && Rules.PlotAgeFactor(plot, s.Year) > 0),
            (s, p) => (Start(p, "bestThisYear") >= Rules.LevelRank(QualityLevel.Auslese) ? 1 : 0, 1)),
        new("vermoegen", "Vermögen aufbauen", 6,
            (s, p) => "Steigere das Reinvermögen in diesem Jahr um 40.000 €.",
            (s, p) => true,
            (s, p) => (Market.NetWorth(p) - Start(p, "netWorth"), 40_000)),
        new("auftraege", "Zwei Aufträge liefern", 8,
            (s, p) => "Liefere in diesem Jahr zwei Kundenaufträge.",
            (s, p) => s.Year >= 2,
            (s, p) => (p.Stats.OrdersDelivered - Start(p, "orders"), 2)),
    ];

    public static GoalDef? Goal(string? id) => id is null ? null : Array.Find(Goals, g => g.Id == id);

    public static bool GoalReached(GameState state, Player player, GoalDef goal)
    {
        var (value, target) = goal.Progress(state, player);
        return value >= target;
    }

    /// <summary>Jahresbeginn: Kennzahlen festhalten und drei Ziele anbieten.</summary>
    public static void StartYear(GameState state)
    {
        state.RipeningWeather = [];
        foreach (var player in state.Players)
        {
            if (player.Bankrupt) continue;
            player.YearStart = new Dictionary<string, double>
            {
                ["medals"] = player.Stats.Medals,
                ["bottlesSold"] = player.Stats.BottlesSold,
                ["netWorth"] = Market.NetWorth(player),
                ["loan"] = player.Loan,
                ["reputation"] = player.Reputation,
                ["planted"] = Market.PlantedHectares(player),
                ["orders"] = player.Stats.OrdersDelivered,
                ["points"] = player.Points,
                ["bestThisYear"] = -1,
            };
            var pool = Goals.Where(g => g.Available(state, player)).Select(g => g.Id).ToList();
            var offers = new List<string>();
            while (offers.Count < 3 && pool.Count > 0)
            {
                var index = (int)Math.Floor(Rng.NextFloat(state.Rng) * pool.Count);
                offers.Add(pool[index]);
                pool.RemoveAt(index);
            }
            player.GoalOffers = offers;
            // Computergegner nehmen das erste Angebot
            player.Goal = player.Ai is not null ? offers.FirstOrDefault() : null;
        }
    }

    /// <summary>Note des Jahrgangs aus dem Wetter der Reifemonate</summary>
    public static int VintageStars(GameState state)
    {
        var sunny = state.RipeningWeather.Count(w => w == WeatherKind.Sonnig);
        var wet = state.RipeningWeather.Count(w => w == WeatherKind.Nass);
        var total = Math.Max(1, state.RipeningWeather.Count);
        return Math.Clamp((int)Js.Round(3 + 3.0 * (sunny - wet) / total), 1, 5);
    }

    /// <summary>Jahresende: Ziel abrechnen und den Rückblick festhalten.</summary>
    public static void EndYear(GameState state)
    {
        var ranking = state.Players.OrderByDescending(p => p.Points).ThenByDescending(Market.NetWorth).ToList();
        var stars = VintageStars(state);
        foreach (var player in state.Players)
        {
            if (player.Bankrupt) continue;
            if (player.Buildings.Contains("schatzkammer"))
            {
                player.Points += 5;
                Util.AddLog(state, player.Id, "Die Schatzkammer mehrt den Ruf des Hauses: +5 Punkte.", LogTone.Good);
            }
            var goal = Goal(player.Goal);
            var reached = goal is not null && GoalReached(state, player, goal);
            if (goal is not null && reached)
            {
                player.Points += goal.Points;
                Rules.ChangeReputation(player, 2);
                Util.AddLog(state, player.Id, $"Jahresziel erreicht: {goal.Title}. +{Util.JsNumber(goal.Points)} Punkte.", LogTone.Good);
            }
            else if (goal is not null)
            {
                Util.AddLog(state, player.Id, $"Jahresziel verfehlt: {goal.Title}.", LogTone.Bad);
            }
            player.Review = new YearReview
            {
                Year = state.Year,
                GoalText = goal?.Title ?? "kein Ziel gewählt",
                GoalReached = reached,
                GoalPoints = reached ? goal!.Points : 0,
                PointsGained = player.Points - Start(player, "points"),
                NetWorthChange = Market.NetWorth(player) - Start(player, "netWorth"),
                BottlesSold = player.Stats.BottlesSold - Start(player, "bottlesSold"),
                Medals = (int)(player.Stats.Medals - Start(player, "medals")),
                Rank = ranking.IndexOf(player) + 1,
                VintageStars = stars,
            };
            player.Goal = null;
            player.GoalOffers = [];
        }
    }

    // ---- Ambitionen ----

    public const int AmbitionCount = 2;
    const int CleanYears = 7;
    /// <summary>Fläche für den Großgrundbesitzer</summary>
    public const double EstateHectares = 20;

    // Punkte nach Schwierigkeit, gemessen an Probepartien über zehn Jahre mit vier Gütern im selben Gebiet:
    // - Saubermann tritt von selbst ein, wenn man nichts Unrechtes tut (praktisch immer, in Jahr 8).
    // - Sekt ist eine Frage des Entschlusses, sobald ein Kellermeister da ist.
    // - Zwei Exportverträge brauchen Bekanntheit und zwei Jahre lückenlose Lieferung.
    // - Lunaris erreicht, wer konsequent umstellt, um Jahr 6 – bezahlt aber mit Ertrag und Kosten.
    // - Drei Goldmedaillen mit einer Sorte schafften gut zwei Drittel der soliden Güter, im Mittel in Jahr 6.
    // - Land ist knapp: Rund 45 ha kommen in zehn Jahren für alle zusammen unter den Hammer. 30 ha erreichte kein
    //   solide wirtschaftendes Gut; wer es erzwang, ging in zwei von drei Partien pleite. 20 ha schaffte etwa jedes
    //   neunte Gut nebenbei und etwa jedes vierte, das gezielt bis zum Schätzwert mitbot.
    public static readonly AmbitionDef[] Ambitions =
    [
        new("sortenkoenig", "👑", "Sortenkönig", "Gewinne drei Goldmedaillen mit derselben Rebsorte.", 30,
            (s, p) => (p.GoldByGrape.Count == 0 ? 0 : p.GoldByGrape.Values.Max(), 3)),
        new("grossgrund", "🗺️", "Großgrundbesitzer", $"Bewirtschafte {Util.JsNumber(EstateHectares)} Hektar.", 60,
            (s, p) => (Market.TotalHectares(p), EstateHectares)),
        new("biovorreiter", "🌙", "Bio-Vorreiter", "Erreiche das Lunaris-Siegel.", 30,
            (s, p) => (p.BioLabel is { } label ? Array.IndexOf(BioData.BioOrder, label) + 1 : 0, 3)),
        new("exportmeister", "🌍", "Exportmeister", "Erfülle zwei Exportverträge vollständig.", 25,
            (s, p) => (p.Stats.ExportsCompleted, 2)),
        new("sektbaron", "🍾", "Sektbaron", "Setze viermal Sekt an.", 20,
            (s, p) => (p.Stats.Sekt, 4)),
        new("saubermann", "🕊️", "Saubermann", $"Wirtschafte {CleanYears} Jahre, ohne je zu panschen oder zu sabotieren.", 15,
            (s, p) => (p.Tainted ? 0 : Math.Min(CleanYears, s.Year - 1), CleanYears)),
    ];

    public static AmbitionDef? Ambition(string id) => Array.Find(Ambitions, a => a.Id == id);

    static readonly Dictionary<AiProfileId, string[]> AiAmbitions = new()
    {
        [AiProfileId.Tradition] = ["sortenkoenig", "saubermann"],
        [AiProfileId.Genossenschaft] = ["grossgrund", "sektbaron"],
        [AiProfileId.Bio] = ["biovorreiter", "saubermann"],
        [AiProfileId.Schlitzohr] = ["grossgrund", "exportmeister"],
    };

    static void CheckAmbitions(GameState state, Player player)
    {
        foreach (var id in player.Ambitions)
        {
            if (player.AmbitionsDone.Contains(id) || Ambition(id) is not { } ambition) continue;
            var (value, target) = ambition.Progress(state, player);
            if (value < target) continue;
            player.AmbitionsDone.Add(id);
            player.Points += ambition.Points;
            // Nur der Spieler selbst erfährt davon – Ambitionen bleiben geheim
            Util.AddLog(state, player.Id, $"{ambition.Icon} Ambition erfüllt: {ambition.Name}! +{Util.JsNumber(ambition.Points)} Punkte.", LogTone.Good);
        }
    }

    // ---- Gutsausbau und Stiftung ----

    public static readonly BuildingDef[] Buildings =
    [
        new("schaenke", "🍽️", "Gutsschänke", 120_000, 800, "Ganzjähriger Ausschank: +25 % Ab-Hof-Nachfrage."),
        new("gaestehaus", "🛏️", "Gästehaus", 180_000, 0, "Zimmer für Weinreisende: Einnahmen von Mai bis Oktober, einmalig +3 Bekanntheit."),
        new("brennerei", "⚗️", "Brennerei", 100_000, 0, "Tresterbrand aus jeder Lese: +250 € je gelesenem Hektar."),
        new("schatzkammer", "🗝️", "Schatzkammer", 300_000, 0, "Raritätenkeller: +10 % auf Ab-Hof-Preise und jedes Jahr +5 Punkte."),
    ];

    public static BuildingDef? Building(string id) => Array.Find(Buildings, b => b.Id == id);

    public const double DonationPoints = 5;

    /// <summary>Preis der nächsten Spende – jede weitere kostet ein Viertel mehr.</summary>
    public static double DonationCost(Player player) => Js.Round(40_000 * Math.Pow(1.25, player.Donations) / 1000) * 1000;

    /// <summary>Monatliche Einnahmen des Gästehauses</summary>
    public static double GuestHouseIncome(Player player, int month) =>
        month is >= 5 and <= 10 ? Js.Round(1_500 * (1 + 0.5 * Regions.All[player.RegionId].Character.Tourism)) : 0;

    // ---- Aktionen ----

    public static string? Validate(GameState state, Player player, GameAction action)
    {
        if (!On(state)) return "Das gibt es nur mit den erweiterten Regeln.";
        switch (action)
        {
            case GameAction.SetPriceLevel a:
                if (!double.IsFinite(a.Level) || a.Level < MinPriceLevel - 1e-9 || a.Level > MaxPriceLevel + 1e-9) return "Der Preisaufschlag muss zwischen −20 % und +40 % liegen.";
                return null;
            case GameAction.ChooseGoal a:
                if (player.Goal is not null) return "Das Jahresziel steht schon fest.";
                if (!player.GoalOffers.Contains(a.GoalId)) return "Dieses Ziel steht nicht zur Wahl.";
                return null;
            case GameAction.ChooseAmbition a:
                if (Ambition(a.AmbitionId) is null) return "Diese Ambition gibt es nicht.";
                if (player.Ambitions.Contains(a.AmbitionId)) return "Diese Ambition ist schon gewählt.";
                if (player.Ambitions.Count >= AmbitionCount) return $"Mehr als {AmbitionCount} Ambitionen gibt es nicht.";
                return null;
            case GameAction.Construct a:
                if (Building(a.BuildingId) is not { } building) return "Diesen Ausbau gibt es nicht.";
                if (player.Buildings.Contains(a.BuildingId)) return $"{building.Name} steht bereits.";
                if (player.Money < building.Cost) return $"Nicht genug Geld (benötigt {Util.FormatEuro(building.Cost)}).";
                return null;
            case GameAction.Donate:
                if (player.Money < DonationCost(player)) return $"Nicht genug Geld (benötigt {Util.FormatEuro(DonationCost(player))}).";
                return null;
            default:
                return null;
        }
    }

    public static void Apply(GameState state, Player player, GameAction action)
    {
        switch (action)
        {
            case GameAction.SetPriceLevel a:
                player.PriceLevel = Math.Clamp(Js.Round(a.Level * 100) / 100, MinPriceLevel, MaxPriceLevel);
                break;
            case GameAction.ChooseGoal a:
                player.Goal = a.GoalId;
                Util.AddLog(state, player.Id, $"Jahresziel: {Goal(a.GoalId)!.Title}.");
                break;
            case GameAction.ChooseAmbition a:
                player.Ambitions.Add(a.AmbitionId);
                break;
            case GameAction.Construct a:
            {
                var building = Building(a.BuildingId)!;
                player.Money -= building.Cost;
                player.Buildings.Add(building.Id);
                if (building.Id == "gaestehaus") Rules.ChangeReputation(player, 3);
                Util.AddLog(state, null, $"{player.Name} baut: {building.Name}.");
                break;
            }
            case GameAction.Donate:
            {
                var cost = DonationCost(player);
                player.Money -= cost;
                player.Donations++;
                player.Points += DonationPoints;
                Util.AddLog(state, null, $"{player.Name} stiftet {Util.FormatEuro(cost)} für die Weinkultur: +{Util.JsNumber(DonationPoints)} Punkte.");
                break;
            }
        }
    }

    // ---- Einhängepunkte im Spielablauf ----

    /// <summary>Neue Partie: Ambitionen der Computergegner, erstes Jahr.</summary>
    public static void StartGame(GameState state)
    {
        foreach (var player in state.Players)
        {
            if (player.Ai is { } profile) player.Ambitions = [.. AiAmbitions[profile]];
        }
        StartYear(state);
    }

    public static void OnHarvest(GameState state, Player player, Plot plot, HarvestPreview preview)
    {
        player.YearStart["bestThisYear"] = Math.Max(Start(player, "bestThisYear"), Rules.LevelRank(preview.Level));
        if (player.Buildings.Contains("brennerei"))
        {
            var income = Js.Round(250 * plot.Hectares);
            player.Money += income;
            Util.AddLog(state, player.Id, $"Die Brennerei macht aus dem Trester Brand: +{Util.FormatEuro(income)}.");
        }
    }

    /// <summary>Monatsabschluss je Weingut: Ausbauten, Ambitionen, Standard-Jahresziel.</summary>
    public static void Monthly(GameState state, Player player)
    {
        var income = player.Buildings.Contains("gaestehaus") ? GuestHouseIncome(player, state.Month) : 0;
        var costs = player.Buildings.Sum(id => Building(id)?.MonthlyCost ?? 0);
        if (income > 0)
        {
            player.Money += income;
            Util.AddLog(state, player.Id, $"Gästehaus: +{Util.FormatEuro(income)}.");
        }
        player.Money -= costs;
        // Wer bis Ende Februar kein Ziel wählt, bekommt das erste Angebot
        if (state.Month == 2 && player.Goal is null && player.GoalOffers.Count > 0) player.Goal = player.GoalOffers[0];
        CheckAmbitions(state, player);
    }

    /// <summary>Prämierung mit drei Juroren; die Wertung ist ihr Mittel.</summary>
    public static (double Score, ShowEntry Entry) Judge(GameState state, Player player, BottleLot lot)
    {
        var baseScore = Market.CompetitionBaseScore(lot);
        var jury = new List<double>();
        for (var i = 0; i < 3; i++) jury.Add(Js.Round((baseScore + Rng.NextRange(state.Rng, -5, 5)) * 10) / 10);
        var score = Js.Round(jury.Sum() / 3 * 10) / 10;
        var entry = new ShowEntry { PlayerId = player.Id, PlayerName = player.Name, Wine = Rules.WineLabel(lot), BaseScore = baseScore, Jury = jury, Score = score };
        state.LastShow?.Entries.Add(entry);
        return (score, entry);
    }

    /// <summary>Zusätzliche Hinweise für die Aufgabenliste</summary>
    public static IEnumerable<Hint> Hints(GameState state, Player player)
    {
        if (!On(state)) yield break;
        if (player.Ambitions.Count < AmbitionCount)
        {
            yield return new Hint { Area = HintArea.Hof, Important = true, Text = "Wähle deine zwei Ambitionen – geheime Fernziele. Leichte bringen wenige Punkte, schwere viele." };
        }
        if (player.Goal is null && player.GoalOffers.Count > 0)
        {
            yield return new Hint { Area = HintArea.Hof, Important = true, Text = "Wähle dein Jahresziel aus drei Angeboten." };
        }
    }

    // ---- Computergegner ----

    /// <summary>Züge der Computergegner nach den erweiterten Regeln, vor ihrem gewohnten Ablauf.</summary>
    public static GameState AiTurn(GameState state)
    {
        var s = state;
        Player Me() => Rules.CurrentPlayer(s);
        void Act(GameAction action)
        {
            if (Game.ValidateAction(s, action) is null) s = Game.ApplyAction(s, action);
        }

        if (Me().Goal is null && Me().GoalOffers.Count > 0) Act(new GameAction.ChooseGoal(Me().GoalOffers[0]));

        // Preis nach Lagerstand: volles Lager billiger, knappes teurer; bekannte Güter und Bio verlangen mehr
        var me = Me();
        var stock = me.Bottles.Where(l => l.MonthsUntilReady == 0).Sum(l => l.Bottles);
        var demand = Math.Max(1, Market.MonthlyDemand(me));
        var months = stock / demand;
        var level = me.Ai switch
        {
            AiProfileId.Genossenschaft => 0,
            AiProfileId.Bio => me.BioLabel is not null ? 0.15 : 0.05,
            AiProfileId.Tradition => me.Reputation >= 40 ? 0.1 : 0,
            _ => 0.0,
        };
        if (months > 8) level -= 0.1;
        else if (months < 2) level += 0.1;
        level = Math.Clamp(Js.Round(level * 100) / 100, MinPriceLevel, MaxPriceLevel);
        if (Math.Abs(level - me.PriceLevel) > 0.001) Act(new GameAction.SetPriceLevel(level));

        // Ausbau nur mit dickem Polster, Spenden erst mit sehr dickem
        foreach (var building in Buildings.OrderBy(b => b.Cost))
        {
            if (!Me().Buildings.Contains(building.Id) && Me().Loan <= 0 && Me().Money >= building.Cost + 150_000)
            {
                Act(new GameAction.Construct(building.Id));
                break;
            }
        }
        if (Me().Loan <= 0 && Me().Money >= DonationCost(Me()) + 400_000) Act(new GameAction.Donate());
        return s;
    }
}
