using System.Collections.Generic;
using NUnit.Framework;

// Beklenen değerler kural kitabından (51 Okey.pdf) ve kullanıcı kararlarından; oyun kodundan değil.
public class RuleOracleTests
{
    static Tiles T(TileColor c, int n) => new Tiles(c, n, TileType.Number);

    static Tiles Okey() => new Tiles(TileColor.black, 0, TileType.Joker);

    static readonly Tiles Ind = T(TileColor.blue, 5);
    static readonly List<Tiles> NoIslek = new List<Tiles>();

    [Test]
    public void Discard_OkeyAndIslek()
    {
        var islek = new List<Tiles> { T(TileColor.red, 9) };
        Assert.AreEqual(250, RuleOracle.DiscardPenalty(Okey(), Ind, false, islek, false));
        Assert.AreEqual(250, RuleOracle.DiscardPenalty(T(TileColor.red, 9), Ind, false, islek, false));
        Assert.AreEqual(0, RuleOracle.DiscardPenalty(T(TileColor.red, 8), Ind, false, islek, false));
        Assert.AreEqual(0, RuleOracle.DiscardPenalty(new Tiles(TileColor.red, 9, TileType.FakeJoker), Ind, false, islek, false));
    }

    [Test]
    public void Discard_IndicatorCopyOnlyAfterPairsOpened()
    {
        Assert.AreEqual(0, RuleOracle.DiscardPenalty(T(TileColor.blue, 5), Ind, false, NoIslek, false));
        Assert.AreEqual(250, RuleOracle.DiscardPenalty(T(TileColor.blue, 5), Ind, true, NoIslek, false));
    }

    [Test]
    public void Discard_FinishingIsFree()
    {
        Assert.AreEqual(0, RuleOracle.DiscardPenalty(Okey(), Ind, true, NoIslek, true));
    }

    [Test]
    public void Process_PenaltyGoesToOwner()
    {
        Assert.AreEqual(70, RuleOracle.ProcessPenalty(T(TileColor.red, 7), 7, Ind, false));
        Assert.AreEqual(0, RuleOracle.ProcessPenalty(T(TileColor.red, 7), 7, Ind, true));
        // "Gösterge 6 ise işlediğiniz oyuncu 120 ceza yer"
        Assert.AreEqual(120, RuleOracle.ProcessPenalty(T(TileColor.red, 6), 6, T(TileColor.red, 6), false));
    }

    [Test]
    public void Process_OkeyUsesRepresentedNumber()
    {
        Assert.AreEqual(90, RuleOracle.ProcessPenalty(Okey(), 9, Ind, false));
    }

    [Test]
    public void SidePick_TenTimesNumber()
    {
        Assert.AreEqual(70, RuleOracle.SidePickPenalty(7));
    }

    [Test]
    public void Open_Limits()
    {
        Assert.IsTrue(RuleOracle.OpenAllowed(51, 51, false, false));
        Assert.IsFalse(RuleOracle.OpenAllowed(51, 51, true, false));
        Assert.IsTrue(RuleOracle.OpenAllowed(62, 61, true, false));
        Assert.IsTrue(RuleOracle.OpenAllowed(5, 61, true, true));
    }

    [Test]
    public void NewLimit_OnlyFirstOpen()
    {
        Assert.AreEqual(61, RuleOracle.NewTableLimit(51, 61, true));
        Assert.AreEqual(61, RuleOracle.NewTableLimit(61, 80, false));
    }

    [Test]
    public void ColorMultiplier_FromIndicator()
    {
        Assert.AreEqual(6, RuleOracle.ColorMultiplier(T(TileColor.yellow, 3)));
        Assert.AreEqual(5, RuleOracle.ColorMultiplier(T(TileColor.red, 3)));
        Assert.AreEqual(4, RuleOracle.ColorMultiplier(T(TileColor.black, 3)));
        Assert.AreEqual(3, RuleOracle.ColorMultiplier(T(TileColor.blue, 3)));
        Assert.AreEqual(8, RuleOracle.ColorMultiplier(new Tiles(TileColor.black, 1, TileType.FakeJoker)));
    }

    static EndState State(Tiles indicator, int winner)
    {
        return new EndState
        {
            WinnerQue = winner,
            Indicator = indicator,
            Hands = new Dictionary<int, List<Tiles>>
            {
                { 1, new List<Tiles>() },
                { 2, new List<Tiles> { T(TileColor.red, 1), T(TileColor.red, 2), T(TileColor.red, 3), Okey() } },
                { 3, new List<Tiles> { T(TileColor.blue, 1), T(TileColor.blue, 2), T(TileColor.blue, 3) } },
                { 4, new List<Tiles> { T(TileColor.blue, 9) } },
            },
            Opened = new HashSet<int> { 1, 2, 3 },
            OpenedPairs = new HashSet<int> { 3 },
            InHandPenalties = new Dictionary<int, int> { { 1, 0 }, { 2, 0 }, { 3, 0 }, { 4, 250 } },
        };
    }

    [Test]
    public void EndResults_RulebookExamples()
    {
        var r = RuleOracle.EndResults(State(T(TileColor.red, 4), 1));
        Assert.AreEqual(-500, r[1]); // kazanan: 100 × kırmızı 5
        Assert.AreEqual(400, r[2]); // "kırmızı, 3 taş + 1 okey = 400"
        Assert.AreEqual(300, r[3]); // çift açmış: 3 taş × 10 × 5 × 2
        Assert.AreEqual(850, r[4]); // açmamış 600 + el içi 250

        var blue = RuleOracle.EndResults(State(T(TileColor.blue, 4), 1));
        Assert.AreEqual(180, blue[3]); // "çift açmış, 3 taş, yer mavi: 180"
    }

    [Test]
    public void EndResults_FinishMultipliers()
    {
        var s = State(T(TileColor.red, 4), 1);
        s.OkeyFinish = true;
        s.Kafa = true;
        var r = RuleOracle.EndResults(s);
        Assert.AreEqual(-500, r[1]); // kazananın düşeri değişmez
        Assert.AreEqual(1600, r[2]); // 400 × 4
        Assert.AreEqual(2650, r[4]); // 600 × 4 + 250 (el içi ceza çarpılmaz)
    }

    [Test]
    public void EndResults_DrawHasNoWinner()
    {
        var s = State(T(TileColor.red, 4), -1);
        s.OkeyFinish = true;
        var r = RuleOracle.EndResults(s);
        Assert.AreEqual(0, r[1]); // eli boş, açmış
        Assert.AreEqual(400, r[2]); // çarpan yok
    }
}
