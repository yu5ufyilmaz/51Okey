using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class TestPlayerBrainTests
{
    static Tiles T(TileColor c, int n) => new Tiles(c, n, TileType.Number);

    static Tiles Okey() => new Tiles(TileColor.black, 0, TileType.Joker);

    static TableView View(List<Tiles> hand, bool canDrop, Tiles side = null) =>
        new TableView
        {
            MyQue = 2,
            Hand = hand,
            Indicator = T(TileColor.blue, 5),
            SideTile = side,
            Slots = new List<SlotView>(),
            TableLimit = 51,
            CanDrop = canDrop,
            PileCount = 40,
        };

    static List<Tiles> SeriesHand() =>
        new List<Tiles>
        {
            T(TileColor.red, 11),
            T(TileColor.red, 12),
            T(TileColor.red, 13),
            T(TileColor.yellow, 5),
            T(TileColor.yellow, 6),
            T(TileColor.black, 2),
        };

    [Test]
    public void Brain_TakesSideTileWhenItEnablesOpening()
    {
        var brain = new TestPlayerBrain(1, 0);
        var step = brain.PlanDraw(View(SeriesHand(), false, T(TileColor.yellow, 7)));
        Assert.AreEqual(StepKind.DrawSide, step.Kind);
    }

    [Test]
    public void Brain_DrawsMiddleOtherwise()
    {
        var brain = new TestPlayerBrain(1, 0);
        var step = brain.PlanDraw(View(SeriesHand(), false, T(TileColor.black, 9)));
        Assert.AreEqual(StepKind.DrawMiddle, step.Kind);
    }

    [Test]
    public void Brain_TwinOnSideIsTakenAndReturnedOncePerDeal()
    {
        var brain = new TestPlayerBrain(1, 0);
        brain.NewDeal();
        var twin = T(TileColor.black, 2); // elde de b2 var
        var view = View(SeriesHand(), false, twin);
        Assert.AreEqual(StepKind.DrawSide, brain.PlanDraw(view).Kind);

        var hand = SeriesHand();
        hand.Add(twin);
        var picked = View(hand, true, null);
        picked.PickedFromSide = true;
        Assert.AreEqual(StepKind.CancelSide, brain.NextAction(picked).Kind);

        brain.NewTurn();
        Assert.AreEqual(StepKind.DrawMiddle, brain.PlanDraw(view).Kind);
    }

    [Test]
    public void Brain_FirstPlayerDoesNotDraw()
    {
        var brain = new TestPlayerBrain(1, 0);
        Assert.IsNull(brain.PlanDraw(View(SeriesHand(), true)));
    }

    [Test]
    public void Brain_OpensWhenLimitPassesAndKeepsOneTile()
    {
        var hand = SeriesHand();
        hand.Add(T(TileColor.yellow, 7)); // r11-13 (36) + y5-7 (18) = 54
        var brain = new TestPlayerBrain(1, 0);
        Assert.AreEqual(StepKind.Arrange, brain.NextAction(View(hand, true)).Kind);
        Assert.AreEqual(StepKind.OpenSeries, brain.NextAction(View(hand, true)).Kind);

        // Elde sadece per taşları varsa açınca atacak taş kalmaz: açmaz.
        var tight = hand.Where(t => !(t.color == TileColor.black && t.number == 2)).ToList();
        var brain2 = new TestPlayerBrain(1, 0);
        var steps = new[] { brain2.NextAction(View(tight, true)), brain2.NextAction(View(tight, true)) };
        Assert.IsFalse(steps.Any(s => s.Kind == StepKind.OpenSeries));
    }

    [Test]
    public void Brain_KeepsChosenPath()
    {
        var pairsHand = new List<Tiles>
        {
            T(TileColor.red, 13),
            T(TileColor.red, 13),
            T(TileColor.black, 12),
            T(TileColor.black, 12),
            T(TileColor.yellow, 11),
            T(TileColor.yellow, 11),
            T(TileColor.blue, 2),
        };
        var brain = new TestPlayerBrain(1, 0);
        brain.NewDeal();
        brain.NextAction(View(pairsHand, true));
        Assert.IsTrue(brain.PathIsPairs);

        brain.NewTurn();
        var seriesHand = SeriesHand();
        seriesHand.Add(T(TileColor.yellow, 7));
        brain.NextAction(View(seriesHand, true));
        Assert.IsTrue(brain.PathIsPairs);
    }

    static SlotView SeriesSlot(Tiles needed, string meld, bool high) =>
        new SlotView
        {
            Key = meld + (high ? ":h" : ":l") + needed.number,
            OwnerQue = 1,
            MeldType = 0,
            MeldKey = meld,
            HighEnd = high,
            Needed = needed,
        };

    [Test]
    public void Brain_ProcessesIslekAfterOpening()
    {
        var hand = new List<Tiles> { T(TileColor.red, 9), Okey(), T(TileColor.black, 3) };
        var v = View(hand, true);
        v.IOpenedSeries = true;
        v.Slots.Add(SeriesSlot(T(TileColor.red, 9), "1:0:0", true));
        var brain = new TestPlayerBrain(1, 0);
        var step = brain.NextAction(v);
        Assert.AreEqual(StepKind.Process, step.Kind);
        Assert.AreEqual(hand[0].id, step.TileId);
    }

    [Test]
    public void Brain_AtMostTwoPerSeriesEndPerTurn()
    {
        var brain = new TestPlayerBrain(1, 0);
        for (int n = 9; n <= 11; n++)
        {
            var hand = new List<Tiles> { T(TileColor.red, n), T(TileColor.black, 3), T(TileColor.black, 4) };
            var v = View(hand, true);
            v.IOpenedSeries = true;
            v.Slots.Add(SeriesSlot(T(TileColor.red, n), "1:0:0", true));
            var step = brain.NextAction(v);
            if (n < 11)
                Assert.AreEqual(StepKind.Process, step.Kind, $"r{n}");
            else
                Assert.AreNotEqual(StepKind.Process, step.Kind, "aynı uca 3. taş");
        }
    }

    [Test]
    public void Brain_NeverDiscardsOkeyOrIslekWhenAlternative()
    {
        var hand = new List<Tiles> { Okey(), T(TileColor.red, 9), T(TileColor.black, 3) };
        var v = View(hand, true);
        v.Slots.Add(SeriesSlot(T(TileColor.red, 9), "1:0:0", true));
        var brain = new TestPlayerBrain(1, 0);
        var step = brain.NextAction(v);
        Assert.AreEqual(StepKind.Discard, step.Kind);
        Assert.AreEqual(hand[2].id, step.TileId);
    }

    static List<Mistake> MistakeRun(int seed, double rate)
    {
        var brain = new TestPlayerBrain(seed, rate);
        var seen = new List<Mistake>();
        for (int turn = 0; turn < 20; turn++)
        {
            brain.NewTurn();
            seen.Add(brain.TurnMistake);
        }
        return seen;
    }

    [Test]
    public void Brain_MistakesAreSeeded()
    {
        CollectionAssert.AreEqual(MistakeRun(7, 0.5), MistakeRun(7, 0.5));
        Assert.IsTrue(MistakeRun(7, 0.5).Any(m => m != Mistake.None));
        Assert.IsTrue(MistakeRun(7, 0).All(m => m == Mistake.None));
    }
}
