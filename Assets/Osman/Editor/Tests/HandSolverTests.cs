using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class HandSolverTests
{
    static Tiles T(TileColor c, int n) => new Tiles(c, n, TileType.Number);

    static Tiles Okey() => new Tiles(TileColor.black, 0, TileType.Joker);

    static List<Tiles> L(params Tiles[] t) => new List<Tiles>(t);

    static readonly Tiles Ind = T(TileColor.blue, 5);

    [Test]
    public void Series_PicksBestDisjointMelds()
    {
        var hand = L(
            T(TileColor.red, 1),
            T(TileColor.red, 2),
            T(TileColor.red, 3),
            T(TileColor.red, 4),
            T(TileColor.blue, 4),
            T(TileColor.black, 4),
            T(TileColor.yellow, 9)
        );
        var best = HandSolver.BestSeries(hand, Ind);
        Assert.AreEqual(18, best.Score);
        Assert.AreEqual(2, best.Melds.Count);
    }

    [Test]
    public void Series_OkeyFillsGap()
    {
        var best = HandSolver.BestSeries(L(T(TileColor.red, 6), Okey(), T(TileColor.red, 8)), Ind);
        Assert.AreEqual(1, best.Melds.Count);
        Assert.AreEqual(21, best.Score);
    }

    [Test]
    public void Series_NoWrap()
    {
        var best = HandSolver.BestSeries(L(T(TileColor.red, 12), T(TileColor.red, 13), T(TileColor.red, 1)), Ind);
        Assert.AreEqual(0, best.Melds.Count);
    }

    [Test]
    public void Series_IndicatorCopyIsNormal()
    {
        var best = HandSolver.BestSeries(L(T(TileColor.red, 4), T(TileColor.blue, 5), T(TileColor.red, 6)), Ind);
        Assert.AreEqual(0, best.Melds.Count);
    }

    [Test]
    public void Pairs_ScoreRules()
    {
        var hand = L(
            T(TileColor.red, 6),
            T(TileColor.red, 6),
            T(TileColor.yellow, 9),
            T(TileColor.yellow, 9),
            T(TileColor.black, 13),
            T(TileColor.blue, 5)
        );
        var best = HandSolver.BestPairs(hand, Ind);
        Assert.AreEqual(3, best.Melds.Count);
        Assert.AreEqual(40, best.Score);
    }

    [Test]
    public void Pairs_OkeyTakesPartner()
    {
        Assert.AreEqual(12, HandSolver.BestPairs(L(T(TileColor.red, 6), Okey()), Ind).Score);
    }

    [Test]
    public void Solver_DoesNotMutateInput()
    {
        var okey = Okey();
        var hand = L(T(TileColor.red, 6), okey, T(TileColor.red, 8));
        HandSolver.BestSeries(hand, Ind);
        HandSolver.BestPairs(hand, Ind);
        Assert.AreEqual(0, okey.number);
        Assert.AreEqual(TileColor.black, okey.color);
    }

    [Test]
    public void Melds_KeepInputIds()
    {
        var hand = L(T(TileColor.red, 6), Okey(), T(TileColor.red, 8));
        var ids = new HashSet<string>(hand.Select(t => t.id));
        var best = HandSolver.BestSeries(hand, Ind);
        Assert.IsTrue(best.Melds.SelectMany(m => m).All(t => ids.Contains(t.id)));
    }

    [Test]
    public void Series_RunOrderIsPlayable()
    {
        // Istakaya sırayla dizildiğinde oyunun kontrolü seri görmeli: okey boşluğun yerinde.
        var best = HandSolver.BestSeries(L(T(TileColor.red, 8), Okey(), T(TileColor.red, 6)), Ind);
        var run = best.Melds.Single();
        Assert.AreEqual(6, run[0].number);
        Assert.AreEqual(TileType.Joker, run[1].type);
        Assert.AreEqual(8, run[2].number);
    }
}
