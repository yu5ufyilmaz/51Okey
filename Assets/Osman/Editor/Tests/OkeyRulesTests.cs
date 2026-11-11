using System.Collections.Generic;
using NUnit.Framework;

// Beklenen değerler kural kitabından (51 Okey.pdf) ve docs/superpowers/specs/2026-11-11-okey-kurallari.md'den.
public class OkeyRulesTests
{
    static Tiles T(TileColor c, int n) => new Tiles(c, n, TileType.Number);

    static Tiles Okey() => new Tiles(TileColor.black, 0, TileType.Joker);

    static List<Tiles> L(params Tiles[] t) => new List<Tiles>(t);

    // --- Seri (#9, #10) ---

    [Test]
    public void Series_RejectsRepeatedEnds()
    {
        Assert.IsFalse(OkeyRuleEngine.SingleColorCheck(L(T(TileColor.red, 12), T(TileColor.red, 13), T(TileColor.red, 13))));
        Assert.IsFalse(OkeyRuleEngine.SingleColorCheck(L(T(TileColor.red, 2), T(TileColor.red, 1), T(TileColor.red, 1))));
        Assert.IsFalse(OkeyRuleEngine.SingleColorCheck(L(T(TileColor.red, 1), T(TileColor.red, 1), T(TileColor.red, 1))));
    }

    [Test]
    public void Series_AcceptsRealRuns()
    {
        Assert.IsTrue(OkeyRuleEngine.SingleColorCheck(L(T(TileColor.red, 11), T(TileColor.red, 12), T(TileColor.red, 13))));
        Assert.IsTrue(OkeyRuleEngine.SingleColorCheck(L(T(TileColor.blue, 3), T(TileColor.blue, 2), T(TileColor.blue, 1))));
        Assert.IsTrue(OkeyRuleEngine.SingleColorCheck(L(T(TileColor.red, 6), Okey(), T(TileColor.red, 8))));
    }

    [Test]
    public void Series_NoWrapAround()
    {
        Assert.IsFalse(OkeyRuleEngine.SingleColorCheck(L(T(TileColor.red, 12), T(TileColor.red, 13), T(TileColor.red, 1))));
    }

    [Test]
    public void Series_IndicatorCopyIsNotAColorWildcard()
    {
        Assert.IsFalse(OkeyRuleEngine.SingleColorCheck(L(T(TileColor.red, 4), T(TileColor.blue, 5), T(TileColor.red, 6))));
    }

    // --- Çift ---

    [Test]
    public void Pair_IndicatorCopyStillPairsWithAnyTile()
    {
        var ind = T(TileColor.blue, 5);
        var pair = L(T(TileColor.red, 13), T(TileColor.blue, 5));
        Assert.IsTrue(OkeyRuleEngine.CheckForDoublePer(pair, ind));
        Assert.IsTrue(OkeyRuleEngine.IsSingleColor(pair, ind));
    }

    [Test]
    public void PairScore_FollowsRulebook()
    {
        var ind = T(TileColor.blue, 5);
        Assert.AreEqual(12, OkeyRuleEngine.CalculateDoublePerScore(L(T(TileColor.red, 6), T(TileColor.red, 6)), ind));
        Assert.AreEqual(12, OkeyRuleEngine.CalculateDoublePerScore(L(T(TileColor.red, 6), Okey()), ind));
        // "Gösterge 5 ise 13'ün yanında kullansanız bile 10 olarak hesaplanır"
        Assert.AreEqual(10, OkeyRuleEngine.CalculateDoublePerScore(L(T(TileColor.red, 13), T(TileColor.blue, 5)), ind));
    }

    // --- Açma limiti (katlamalı) ---

    [Test]
    public void OpenLimit_FirstOpenerNeeds51()
    {
        Assert.IsTrue(OkeyRuleEngine.PassesOpenLimit(51, 51, false, false));
        Assert.IsFalse(OkeyRuleEngine.PassesOpenLimit(50, 51, false, false));
    }

    [Test]
    public void OpenLimit_LaterOpenerMustBeatTable()
    {
        Assert.IsFalse(OkeyRuleEngine.PassesOpenLimit(51, 51, true, false));
        Assert.IsFalse(OkeyRuleEngine.PassesOpenLimit(61, 61, true, false));
        Assert.IsTrue(OkeyRuleEngine.PassesOpenLimit(62, 61, true, false));
    }

    [Test]
    public void OpenLimit_NoLimitOnceOpened()
    {
        Assert.IsTrue(OkeyRuleEngine.PassesOpenLimit(4, 61, true, true));
    }

    // --- İşleme: bir uçtan turda en fazla 2 taş ---

    [Test]
    public void SeriesProcess_TwoPerEndPerTurn()
    {
        var per = new List<int> { 6, 7, 8 };
        Assert.IsTrue(OkeyRuleEngine.CanProcessSeriesEnd(9, per, new List<int>()));
        Assert.IsTrue(OkeyRuleEngine.CanProcessSeriesEnd(10, per, new List<int> { 9 }));
        Assert.IsFalse(OkeyRuleEngine.CanProcessSeriesEnd(11, per, new List<int> { 9, 10 }));
        Assert.IsTrue(OkeyRuleEngine.CanProcessSeriesEnd(5, per, new List<int> { 9, 10 }));
        Assert.IsFalse(OkeyRuleEngine.CanProcessSeriesEnd(3, per, new List<int> { 9, 10, 5, 4 }));
    }

    // --- El sonu ---

    [Test]
    public void EndPenalty_FollowsRulebookExamples()
    {
        Assert.AreEqual(-600, PenaltySystem.EndPenalty(true, true, false, 0, 0, 6, 1));
        Assert.AreEqual(600, PenaltySystem.EndPenalty(false, false, false, 9, 1, 6, 1));
        // "Kırmızı açılmış, elinizde 3 taş ve 1 okey: 400"
        Assert.AreEqual(400, PenaltySystem.EndPenalty(false, true, false, 3, 1, 5, 1));
        // "Çift açmışsınız, 3 taş, yer mavi: 180"
        Assert.AreEqual(180, PenaltySystem.EndPenalty(false, true, true, 3, 0, 3, 1));
    }

    [Test]
    public void EndPenalty_FinishMultiplierDoublesLosersOnly()
    {
        Assert.AreEqual(1200, PenaltySystem.EndPenalty(false, false, false, 0, 0, 6, 2));
        Assert.AreEqual(-600, PenaltySystem.EndPenalty(true, true, false, 0, 0, 6, 4));
    }

    [Test]
    public void EndPenalty_WinnerRewardScalesWithColour()
    {
        // "Sarı da biter ise 600 düşer": düşer = 100 × renk çarpanı
        Assert.AreEqual(-600, PenaltySystem.EndPenalty(true, true, false, 0, 0, 6, 1));
        Assert.AreEqual(-500, PenaltySystem.EndPenalty(true, true, false, 0, 0, 5, 1));
        Assert.AreEqual(-800, PenaltySystem.EndPenalty(true, true, false, 0, 0, 8, 4));
    }

    // --- Atma cezası ---

    [Test]
    public void DiscardPenalty_IndicatorCopyOnlyAfterPairsOpened()
    {
        var ind = T(TileColor.blue, 5);
        var none = new List<Tiles>();
        Assert.AreEqual(0, GameManager.DiscardPenalty(T(TileColor.blue, 5), ind, false, none));
        Assert.AreEqual(250, GameManager.DiscardPenalty(T(TileColor.blue, 5), ind, true, none));
    }

    [Test]
    public void DiscardPenalty_OkeyAndIslek()
    {
        var ind = T(TileColor.blue, 5);
        var islek = new List<Tiles> { T(TileColor.red, 9) };
        Assert.AreEqual(250, GameManager.DiscardPenalty(Okey(), ind, false, islek));
        Assert.AreEqual(250, GameManager.DiscardPenalty(T(TileColor.red, 9), ind, false, islek));
        Assert.AreEqual(0, GameManager.DiscardPenalty(T(TileColor.red, 8), ind, false, islek));
        Assert.AreEqual(0, GameManager.DiscardPenalty(new Tiles(TileColor.red, 9, TileType.FakeJoker), ind, false, islek));
    }

    [Test]
    public void DiscardPenaltyReason_NamesWhatWasThrown()
    {
        var ind = T(TileColor.blue, 5);
        var islek = new List<Tiles> { T(TileColor.red, 9) };
        Assert.AreEqual("Okey attı", GameManager.DiscardPenaltyReason(Okey(), ind, false, islek));
        Assert.AreEqual("Okey attı", GameManager.DiscardPenaltyReason(T(TileColor.blue, 6), ind, false, islek));
        Assert.AreEqual("Gösterge attı", GameManager.DiscardPenaltyReason(T(TileColor.blue, 5), ind, true, islek));
        Assert.AreEqual("İşlek attı", GameManager.DiscardPenaltyReason(T(TileColor.red, 9), ind, false, islek));
        Assert.IsNull(GameManager.DiscardPenaltyReason(T(TileColor.blue, 5), ind, false, islek));
        Assert.IsNull(GameManager.DiscardPenaltyReason(T(TileColor.red, 8), ind, false, islek));
    }

    [Test]
    public void FinishMultiplier_Multiplies()
    {
        Assert.AreEqual(1, PenaltySystem.FinishMultiplier(false, false, false));
        Assert.AreEqual(2, PenaltySystem.FinishMultiplier(true, false, false));
        Assert.AreEqual(2, PenaltySystem.FinishMultiplier(false, true, false));
        // "Kimse açmadan okey atarsanız diğer oyuncular 4 katı ceza yer"
        Assert.AreEqual(4, PenaltySystem.FinishMultiplier(false, true, true));
    }
}
