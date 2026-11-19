using System.Linq;
using NUnit.Framework;

public class ScoreTests
{
    static SeatInput S(int seat, bool opened, bool pairs, int tiles, int okeys, int pen) =>
        new SeatInput { seat = seat, opened = opened, openedPairs = pairs, normalTiles = tiles, okeys = okeys, inHandPenalty = pen };
    // Sahte ceza işlevi: bitiren -600; açmış tiles*40 + okeys*250; açmamış 1200
    static int Fake(SeatInput s, bool winner) => winner ? -600 : s.opened ? s.normalTiles * 40 + s.okeys * 250 : 1200;

    [Test] public void Build_Winner_Opened_NotOpened() {
        var d = HandBreakdown.Build(3, 1, 2, true, false, false,
            new[] { S(1, true, false, 0, 0, 0), S(2, true, false, 6, 0, 250), S(3, false, false, 14, 0, 0), S(4, true, false, 2, 1, 70) }, Fake);
        Assert.AreEqual(-600, d.rows[0].finishReward); Assert.AreEqual(0, d.rows[0].handPoints); Assert.AreEqual(-600, d.rows[0].handTotal);
        Assert.AreEqual(240, d.rows[1].handPoints); Assert.AreEqual(250, d.rows[1].inHandPenalty); Assert.AreEqual(490, d.rows[1].handTotal);
        Assert.AreEqual(1200, d.rows[2].handTotal); Assert.IsFalse(d.rows[2].opened); Assert.AreEqual(14, d.rows[2].tilesLeft);
        Assert.AreEqual(3, d.rows[3].tilesLeft); Assert.AreEqual(80 + 250 + 70, d.rows[3].handTotal);
    }
    [Test] public void Build_Draw_Has_No_Winner_Row() {
        var d = HandBreakdown.Build(1, 0, 1, false, false, false, new[] { S(1, true, false, 3, 0, 0), S(2, false, false, 14, 0, 0) }, Fake);
        Assert.IsTrue(d.rows.All(r => r.finishReward == 0)); Assert.AreEqual(120, d.rows[0].handTotal);
    }
    [Test] public void Encode_Decode_Round_Trip() {
        var d = HandBreakdown.Build(2, 4, 4, true, true, true, new[] { S(1, true, true, 5, 0, 30), S(3, false, false, 14, 0, 0), S(4, true, false, 0, 0, 0) }, Fake);
        var e = HandBreakdown.Decode(HandBreakdown.Encode(d));
        Assert.AreEqual(4 + 7 * 3, HandBreakdown.Encode(d).Length);
        Assert.AreEqual(2, e.handIndex); Assert.AreEqual(4, e.winnerSeat); Assert.AreEqual(4, e.finishMultiplier);
        Assert.IsTrue(e.okeyFinish && e.pairsFinish && e.kafa);
        CollectionAssert.AreEqual(new[] { 1, 3, 4 }, e.rows.Select(r => r.seat));
        Assert.AreEqual(d.rows[0].handTotal, e.rows[0].handTotal); Assert.IsFalse(e.rows[1].opened);
    }
    [Test] public void Decode_Bad_Input_Is_Null_Or_Drops_Partial_Row() {
        Assert.IsNull(HandBreakdown.Decode(null)); Assert.IsNull(HandBreakdown.Decode(new int[2]));
        var good = HandBreakdown.Encode(HandBreakdown.Build(1, 0, 1, false, false, false, new[] { S(1, true, false, 3, 0, 0), S(2, false, false, 14, 0, 0) }, Fake));
        var cut = good.Take(good.Length - 3).ToArray();
        Assert.AreEqual(1, HandBreakdown.Decode(cut).rows.Count);
    }
    [Test] public void Decode_Header_Only_And_Odd_Lengths_Never_Throw() {
        Assert.AreEqual(0, HandBreakdown.Decode(new[] { 1, 0, 1, 0 }).rows.Count);
        Assert.IsNull(HandBreakdown.Decode(new int[0]));
        foreach (var n in new[] { 4, 5, 10, 11, 12, 18 })
            Assert.DoesNotThrow(() => HandBreakdown.Decode(Enumerable.Repeat(-99999, n).ToArray()));
        Assert.AreEqual(1, HandBreakdown.Decode(Enumerable.Repeat(-5, 17).ToArray()).rows.Count);
    }
    [Test] public void History_Replaces_Same_Hand_And_Sums() {
        var h = new MatchHistory();
        var a = HandBreakdown.Build(1, 1, 1, false, false, false, new[] { S(1, true, false, 0, 0, 0), S(2, true, false, 5, 0, 0) }, Fake);
        h.Set(a); h.Set(a);
        Assert.AreEqual(1, h.Hands.Count); Assert.AreEqual(-600, h.Total(1)); Assert.AreEqual(200, h.Total(2));
        h.Set(HandBreakdown.Build(2, 2, 1, false, false, false, new[] { S(1, false, false, 14, 0, 0), S(2, true, false, 0, 0, 0) }, Fake));
        Assert.AreEqual(600, h.Total(1)); Assert.AreEqual(-400, h.Total(2)); Assert.AreEqual(2, h.LeaderSeat); Assert.AreEqual(2, h.Last.handIndex);
    }
    [Test] public void History_Out_Of_Order_Hands_Are_Sorted_And_Tie_Goes_To_Lower_Seat() {
        var h = new MatchHistory();
        h.Set(HandBreakdown.Build(2, 0, 1, false, false, false, new[] { S(1, true, false, 5, 0, 0), S(2, true, false, 5, 0, 0) }, Fake));
        h.Set(HandBreakdown.Build(1, 0, 1, false, false, false, new[] { S(1, true, false, 5, 0, 0), S(2, true, false, 5, 0, 0) }, Fake));
        CollectionAssert.AreEqual(new[] { 1, 2 }, h.Hands.Select(x => x.handIndex)); Assert.AreEqual(1, h.LeaderSeat);
        Assert.AreEqual(0, new MatchHistory().LeaderSeat); Assert.AreEqual(0, h.Total(9));
    }
    [Test] public void History_Seven_Hands() {
        var h = new MatchHistory();
        for (int i = 1; i <= 7; i++) h.Set(HandBreakdown.Build(i, 0, 1, false, false, false, new[] { S(1, true, false, 1, 0, 0) }, Fake));
        Assert.AreEqual(7, h.Hands.Count); Assert.AreEqual(280, h.Total(1));
    }
    [Test] public void History_Null_Clear_And_Leader_Ignores_Absent_Seats() {
        var h = new MatchHistory();
        Assert.DoesNotThrow(() => h.Set(null)); Assert.AreEqual(0, h.Hands.Count); Assert.IsNull(h.Last);
        h.Set(HandBreakdown.Build(1, 0, 1, false, false, false, new[] { S(3, true, false, 5, 0, 0), S(4, true, false, 9, 0, 0) }, Fake));
        Assert.AreEqual(3, h.LeaderSeat);
        h.Clear(); Assert.AreEqual(0, h.Hands.Count); Assert.AreEqual(0, h.LeaderSeat);
    }
    [Test] public void Names_Bots_Are_Numbered_Among_Bots_And_Leavers_Keep_Name() {
        CollectionAssert.AreEqual(new[] { "Bot 1", "Ayşe", "Bot 2", "Can (bot)" },
            SeatNaming.Names(new[] { null, "Ayşe", null, "Can" }, new[] { true, false, true, true }));
        CollectionAssert.AreEqual(new[] { "A", "B", "C", "D" }, SeatNaming.Names(new[] { "A", "B", "C", "D" }, new bool[4]));
    }
    [Test] public void History_Total_Clamps_Instead_Of_Overflow() {
        var h = new MatchHistory();
        for (int i = 1; i <= 2; i++) {
            var d = new HandResultData { handIndex = i };
            d.rows.Add(new HandRow { seat = 1, handTotal = int.MaxValue }); d.rows.Add(new HandRow { seat = 2, handTotal = int.MinValue });
            h.Set(d);
        }
        Assert.AreEqual(int.MaxValue, h.Total(1)); Assert.AreEqual(int.MinValue, h.Total(2));
        Assert.DoesNotThrow(() => { var _ = h.LeaderSeat; }); Assert.AreEqual(2, h.LeaderSeat);
    }
    [Test] public void Build_Sorts_Rows_By_Seat_And_Null_Seats_Is_Empty() {
        var d = HandBreakdown.Build(1, 0, 1, false, false, false, new[] { S(4, true, false, 1, 0, 0), S(2, true, false, 1, 0, 0), S(3, true, false, 1, 0, 0) }, Fake);
        CollectionAssert.AreEqual(new[] { 2, 3, 4 }, d.rows.Select(r => r.seat));
        Assert.AreEqual(0, HandBreakdown.Build(1, 0, 1, false, false, false, null, Fake).rows.Count);
    }
    [Test] public void Encode_Null_And_Null_Rows_Tolerated() {
        Assert.IsNull(HandBreakdown.Encode(null));
        var d = new HandResultData { handIndex = 3, rows = null };
        Assert.AreEqual(4, HandBreakdown.Encode(d).Length);
        Assert.DoesNotThrow(() => new MatchHistory().Set(d));
        var h = new MatchHistory(); h.Set(d);
        Assert.AreEqual(0, h.Total(1)); Assert.AreEqual(0, h.LeaderSeat);
    }
    // Gerçek PenaltySystem.EndPenalty; renk çarpanı 4. Beklenenler elle yazılmış sabitler.
    static int Real(SeatInput s, bool win, int fm) => PenaltySystem.EndPenalty(win, s.opened, s.openedPairs, s.normalTiles, s.okeys, 4, fm);
    [Test] public void HandTotal_Equals_Todays_Net_With_Real_Penalty_Rules() {
        var d = HandBreakdown.Build(1, 1, 2, false, true, false,
            new[] { S(1, true, false, 0, 0, 0), S(2, true, false, 6, 0, 250), S(3, false, false, 14, 0, 0), S(4, true, true, 4, 1, 70) },
            (s, w) => Real(s, w, 2));
        Assert.AreEqual(-400, d.rows[0].finishReward); Assert.AreEqual(-400, d.rows[0].handTotal);     // -100*4, çarpansız
        Assert.AreEqual(480, d.rows[1].handPoints); Assert.AreEqual(730, d.rows[1].handTotal);          // 6*10*4*2 + 250
        Assert.AreEqual(1200, d.rows[2].handTotal);                                                     // açmamış 600*2
        Assert.AreEqual(1140, d.rows[3].handPoints); Assert.AreEqual(1210, d.rows[3].handTotal);        // (4*10*4*2 + 250)*2 + 70
        Assert.AreEqual(5, d.rows[3].tilesLeft);
    }
    [Test] public void Draw_With_Real_Rules_Everyone_Pays_Loser_Formula() {
        var d = HandBreakdown.Build(1, 0, 1, false, false, false,
            new[] { S(1, true, false, 3, 0, 0), S(2, false, false, 14, 0, 0), S(3, true, true, 2, 1, 30) }, (s, w) => Real(s, w, 1));
        Assert.IsTrue(d.rows.All(r => r.finishReward == 0));
        Assert.AreEqual(120, d.rows[0].handTotal); Assert.AreEqual(600, d.rows[1].handTotal); Assert.AreEqual(440, d.rows[2].handTotal);
    }
    [Test] public void Names_Bad_Input_Never_Throws() {
        CollectionAssert.AreEqual(new[] { "Bot 1", "Bot 2", "Bot 3", "Bot 4" }, SeatNaming.Names(null, null));
        CollectionAssert.AreEqual(new[] { "Bot 1", "X", "Bot 2", "Bot 3" }, SeatNaming.Names(new[] { " ", "X" }, null));
        CollectionAssert.AreEqual(new[] { "A (bot)", "Bot 1", "Bot 2", "Bot 3" }, SeatNaming.Names(new[] { "A", "", null, null, "Z" }, new[] { true }));
    }
    [Test] public void OpenValue_First_Placement_Wins() {
        Assert.AreEqual(68, OpenValue.Pick(0, 68));
        Assert.AreEqual(68, OpenValue.Pick(68, 24));     // aynı turda sonradan eklenen değeri bozmaz
        Assert.AreEqual(0, OpenValue.Pick(0, 0));
    }
}
