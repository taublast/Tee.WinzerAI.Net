using System.Text.Json.Serialization;

namespace Winzer.Engine;

/// <summary>Alle Spielzüge. Die Typnamen im JSON entsprechen dem Feld <c>type</c> des Originals.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Plant), "plant")]
[JsonDerivedType(typeof(Harvest), "harvest")]
[JsonDerivedType(typeof(SellBulk), "sellBulk")]
[JsonDerivedType(typeof(Bottle), "bottle")]
[JsonDerivedType(typeof(Register), "register")]
[JsonDerivedType(typeof(SellWholesale), "sellWholesale")]
[JsonDerivedType(typeof(EnterCompetition), "enterCompetition")]
[JsonDerivedType(typeof(Marketing), "marketing")]
[JsonDerivedType(typeof(ExpandTank), "expandTank")]
[JsonDerivedType(typeof(BuyOnlineShop), "buyOnlineShop")]
[JsonDerivedType(typeof(HireCellarMaster), "hireCellarMaster")]
[JsonDerivedType(typeof(FireCellarMaster), "fireCellarMaster")]
[JsonDerivedType(typeof(Borrow), "borrow")]
[JsonDerivedType(typeof(Repay), "repay")]
[JsonDerivedType(typeof(AuctionLimit), "auctionLimit")]
[JsonDerivedType(typeof(AuctionBid), "auctionBid")]
[JsonDerivedType(typeof(AuctionPass), "auctionPass")]
[JsonDerivedType(typeof(Decide), "decide")]
[JsonDerivedType(typeof(SellPlot), "sellPlot")]
[JsonDerivedType(typeof(OfferPlot), "offerPlot")]
[JsonDerivedType(typeof(WithdrawPlot), "withdrawPlot")]
[JsonDerivedType(typeof(ClearPlot), "clearPlot")]
[JsonDerivedType(typeof(InstallHailNet), "installHailNet")]
[JsonDerivedType(typeof(InstallIrrigation), "installIrrigation")]
[JsonDerivedType(typeof(InsureHarvest), "insureHarvest")]
[JsonDerivedType(typeof(FrostCandles), "frostCandles")]
[JsonDerivedType(typeof(ApplyBioLabel), "applyBioLabel")]
[JsonDerivedType(typeof(SellFederweisser), "sellFederweisser")]
[JsonDerivedType(typeof(OpenStrausswirtschaft), "openStrausswirtschaft")]
[JsonDerivedType(typeof(AcceptOrder), "acceptOrder")]
[JsonDerivedType(typeof(DeclineOrder), "declineOrder")]
[JsonDerivedType(typeof(DeliverOrder), "deliverOrder")]
[JsonDerivedType(typeof(SetProtection), "setProtection")]
[JsonDerivedType(typeof(GambleIceWine), "gambleIceWine")]
[JsonDerivedType(typeof(MakeSekt), "makeSekt")]
[JsonDerivedType(typeof(Adulterate), "adulterate")]
[JsonDerivedType(typeof(Sabotage), "sabotage")]
[JsonDerivedType(typeof(AnswerExport), "answerExport")]
[JsonDerivedType(typeof(ExpandBarrique), "expandBarrique")]
[JsonDerivedType(typeof(ToBarrique), "toBarrique")]
[JsonDerivedType(typeof(BuildVinothek), "buildVinothek")]
[JsonDerivedType(typeof(EndTurn), "endTurn")]
[JsonDerivedType(typeof(AutoSkipTurn), "autoSkip")]
[JsonDerivedType(typeof(SetPriceLevel), "setPriceLevel")]
[JsonDerivedType(typeof(ChooseGoal), "chooseGoal")]
[JsonDerivedType(typeof(ChooseAmbition), "chooseAmbition")]
[JsonDerivedType(typeof(Construct), "construct")]
[JsonDerivedType(typeof(Donate), "donate")]
public abstract record GameAction
{
    public sealed record Plant(string PlotId, GrapeId Grape) : GameAction;
    public sealed record Harvest(string PlotId, bool Rose = false) : GameAction;
    public sealed record SellBulk(string BatchId, double Hectoliters) : GameAction;
    public sealed record Bottle(string BatchId, double Hectoliters) : GameAction;
    public sealed record Register(string LotId) : GameAction;
    public sealed record SellWholesale(string LotId, double Bottles) : GameAction;
    public sealed record EnterCompetition(string LotId) : GameAction;
    public sealed record Marketing(MarketingKind Kind) : GameAction;
    public sealed record ExpandTank(double Hectoliters) : GameAction;
    public sealed record BuyOnlineShop : GameAction;
    public sealed record HireCellarMaster(int Candidate) : GameAction;
    public sealed record FireCellarMaster : GameAction;
    public sealed record Borrow(double Amount) : GameAction;
    public sealed record Repay(double Amount) : GameAction;
    public sealed record AuctionLimit(string LotId, double Amount) : GameAction;
    public sealed record AuctionBid(string LotId, double Amount) : GameAction;
    public sealed record AuctionPass(string LotId) : GameAction;
    public sealed record Decide(int Option) : GameAction;
    public sealed record SellPlot(string PlotId) : GameAction;
    /// <summary>Erweiterte Regeln: Parzelle für die nächste Versteigerung anmelden</summary>
    public sealed record OfferPlot(string PlotId) : GameAction;
    /// <summary>Erweiterte Regeln: Anmeldung zurückziehen, solange das Los noch nicht angekündigt ist</summary>
    public sealed record WithdrawPlot(string PlotId) : GameAction;
    public sealed record ClearPlot(string PlotId) : GameAction;
    public sealed record InstallHailNet(string PlotId) : GameAction;
    public sealed record InstallIrrigation(string PlotId) : GameAction;
    public sealed record InsureHarvest : GameAction;
    public sealed record FrostCandles : GameAction;
    public sealed record ApplyBioLabel : GameAction;
    public sealed record SellFederweisser(string BatchId, double Hectoliters) : GameAction;
    public sealed record OpenStrausswirtschaft : GameAction;
    public sealed record AcceptOrder(string OrderId) : GameAction;
    public sealed record DeclineOrder(string OrderId) : GameAction;
    public sealed record DeliverOrder(string OrderId) : GameAction;
    public sealed record SetProtection(ProtectionStrategy Strategy) : GameAction;
    public sealed record GambleIceWine(string PlotId, bool Gamble) : GameAction;
    public sealed record MakeSekt(string BatchId, double Hectoliters) : GameAction;
    public sealed record Adulterate(string BatchId, AdulterationMethod Method) : GameAction;
    public sealed record Sabotage(string TargetId, SabotageKind Kind) : GameAction;
    public sealed record AnswerExport(bool Accept) : GameAction;
    public sealed record ExpandBarrique(double Hectoliters) : GameAction;
    public sealed record ToBarrique(string BatchId) : GameAction;
    public sealed record BuildVinothek : GameAction;
    public sealed record EndTurn : GameAction;
    /// <summary>Vorspulen (<c>autoSkip</c> im Original)</summary>
    public sealed record AutoSkipTurn : GameAction;

    // ---- Regelversion 2 ----

    /// <summary>Auf- oder Abschlag auf den Ab-Hof-Preis</summary>
    public sealed record SetPriceLevel(double Level) : GameAction;
    /// <summary>Jahresziel aus den Angeboten wählen</summary>
    public sealed record ChooseGoal(string GoalId) : GameAction;
    public sealed record ChooseAmbition(string AmbitionId) : GameAction;
    /// <summary>Ausbau des Guts errichten</summary>
    public sealed record Construct(string BuildingId) : GameAction;
    /// <summary>Spende an die Stiftung: Geld gegen Punkte</summary>
    public sealed record Donate : GameAction;
}
