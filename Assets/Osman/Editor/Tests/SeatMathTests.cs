using NUnit.Framework;

public class SeatMathTests
{
    [Test]
    public void RelativeIndex_LocalSeatIsAlwaysZero()
    {
        for (int seat = 1; seat <= 4; seat++)
            Assert.AreEqual(0, SeatMath.RelativeIndex(seat, seat));
    }

    [TestCase(2, 1, 1)]
    [TestCase(1, 2, 3)]
    [TestCase(4, 1, 3)]
    [TestCase(3, 1, 2)]
    public void RelativeIndex_MatchesTheDropAreaFormula(int targetSeat, int localSeat, int expected) =>
        Assert.AreEqual(expected, SeatMath.RelativeIndex(targetSeat, localSeat));

    [Test]
    public void SeatAtRelativeIndex_IsTheInverseForAllSixteenPairs()
    {
        for (int local = 1; local <= 4; local++)
        for (int target = 1; target <= 4; target++)
            Assert.AreEqual(target, SeatMath.SeatAtRelativeIndex(SeatMath.RelativeIndex(target, local), local));
    }

    [Test]
    public void QueOfSeat_FindsTheQueOrMinusOne()
    {
        int[] seatOfQue = { 0, 3, 4, 1, 2 }; // sıra 1 -> koltuk 3 ...
        Assert.AreEqual(3, SeatMath.QueOfSeat(seatOfQue, 1));
        Assert.AreEqual(1, SeatMath.QueOfSeat(seatOfQue, 3));
        Assert.AreEqual(-1, SeatMath.QueOfSeat(new int[5], 2));
    }

    [Test]
    public void MeldSeat_UnmappedQueGivesNoSeat()
    {
        Assert.AreEqual(-1, SeatMath.MeldSeat(new int[5], 1)); // eşleme henüz gelmedi
        Assert.AreEqual(-1, SeatMath.MeldSeat(new[] { 0, 3, 4, 1, 2 }, 0));
        Assert.AreEqual(-1, SeatMath.MeldSeat(new[] { 0, 3, 4, 1, 2 }, 5));
        Assert.AreEqual(4, SeatMath.MeldSeat(new[] { 0, 3, 4, 1, 2 }, 2));
    }

    [Test]
    public void BotNumber_CountsEmptySeatsInSeatOrder()
    {
        bool[] has = { false, true, false, true, false }; // koltuk 1 ve 3 dolu
        Assert.AreEqual(0, SeatMath.BotNumber(has, 1));
        Assert.AreEqual(1, SeatMath.BotNumber(has, 2));
        Assert.AreEqual(0, SeatMath.BotNumber(has, 3));
        Assert.AreEqual(2, SeatMath.BotNumber(has, 4));
    }
}
