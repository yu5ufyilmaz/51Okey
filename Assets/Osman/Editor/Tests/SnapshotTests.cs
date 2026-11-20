using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class SnapshotTests
{
    static int n;
    static Tiles T(TileColor c, int num, TileType t = TileType.Number) => new Tiles { id = "id" + (n++), color = c, number = num, type = t };
    static List<Tiles> Hand(int count) => Enumerable.Range(0, count).Select(i => T((TileColor)(i % 4), i % 13 + 1)).ToList();
    static int[] With(int[] a, int i, int v) { var c = (int[])a.Clone(); c[i] = v; return c; }

    static TableSnapshot Sample()
    {
        n = 0;
        var s = TableSnapshot.Empty();
        s.round = 3; s.turnSerial = 12; s.currentTurn = 2;
        s.seatOfQue = new[] { 0, 3, 4, 1, 2 };
        s.indicator = T(TileColor.red, 7); s.dropTile = T(TileColor.blue, 4);
        s.pile = Hand(5);
        s.hands[0] = Hand(14); s.hands[1] = Hand(14); s.hands[2] = Hand(15); s.hands[3] = Hand(14);
        s.hands[2][3] = T(TileColor.red, 1, TileType.Joker);
        s.melds[1].Add(new List<Tiles> { T(TileColor.red, 2), T(TileColor.red, 2) });
        s.melds[1].Add(new List<Tiles> { T(TileColor.blue, 9), T(TileColor.blue, 9) });
        s.meldPositions[1].Add(new List<Vector2Int> { new Vector2Int(0, 1), new Vector2Int(0, 2) });
        s.meldPositions[1].Add(new List<Vector2Int>());
        for (int i = 0; i < 6; i++) s.discards.Add(new SnapDiscard { seat = i / 2 + 1, tile = T((TileColor)(i % 4), i + 1) });
        s.boards[2].Add(new SnapBoardTile { tile = T(TileColor.black, 5), meldType = 0, slot = 17 });
        s.boards[2].Add(new SnapBoardTile { tile = T(TileColor.black, 6), meldType = 0, slot = 18 });
        s.boards[2].Add(new SnapBoardTile { tile = T(TileColor.red, 1, TileType.Joker), meldType = 1, slot = 2 });
        s.boards[2].Add(new SnapBoardTile { tile = T(TileColor.red, 1, TileType.FakeJoker), meldType = 2, slot = 0 });
        s.opened.Add(2);
        s.openValues.Add(new SnapOpenValue { que = 2, value = 68, pairs = false });
        s.penalties.Add(new SnapPenalty { seat = 1, amount = 250, reason = 1 });
        s.penalties.Add(new SnapPenalty { seat = 3, amount = 40, reason = 4 });
        s.turnStartTime = 1234.5678; s.devDeal = 2; s.devMove = 31;
        return s;
    }

    [Test] public void RoundTrip_Keeps_Everything() {
        var s = Sample(); var d = SnapshotCodec.Decode(SnapshotCodec.Encode(s));
        Assert.AreEqual(s.Canonical(true), d.Canonical(true));
        Assert.AreEqual(1234.5678, d.turnStartTime); Assert.AreEqual(31, d.devMove);
    }
    [Test] public void Encode_Is_Deterministic() {
        var a = SnapshotCodec.Encode(Sample()); var b = SnapshotCodec.Encode(Sample());
        CollectionAssert.AreEqual((int[])a[0], (int[])b[0]); CollectionAssert.AreEqual((string[])a[1], (string[])b[1]);
    }
    [Test] public void Null_Tiles_RoundTrip() {
        var s = Sample(); s.dropTile = null; s.indicator = null;
        var d = SnapshotCodec.Decode(SnapshotCodec.Encode(s)); Assert.IsNull(d.dropTile); Assert.IsNull(d.indicator);
    }
    [Test] public void Canonical_Meta_Is_Excluded_Without_Flag() {
        var s = Sample(); var c = s.Canonical(false); s.devMove = 99; s.turnStartTime = 1;
        Assert.AreEqual(c, s.Canonical(false)); Assert.AreNotEqual(c, s.Canonical(true));
    }
    [Test] public void Empty_RoundTrips() {
        var d = SnapshotCodec.Decode(SnapshotCodec.Encode(TableSnapshot.Empty()));
        Assert.AreEqual(TableSnapshot.Empty().Canonical(true), d.Canonical(true));
    }
    [Test] public void Decode_Rejects_Bad_Data() {
        var p = SnapshotCodec.Encode(Sample()); var ints = (int[])p[0]; var strs = (string[])p[1];
        Assert.IsNull(SnapshotCodec.Decode(null)); Assert.IsNull(SnapshotCodec.Decode(new object[] { ints }));
        Assert.IsNull(SnapshotCodec.Decode(new object[] { With(ints, 0, 2), strs }));                       // sürüm
        Assert.IsNull(SnapshotCodec.Decode(new object[] { ints.Take(ints.Length - 1).ToArray(), strs }));   // kısa
        Assert.IsNull(SnapshotCodec.Decode(new object[] { ints.Concat(new[] { 0 }).ToArray(), strs }));     // fazla
        Assert.IsNull(SnapshotCodec.Decode(new object[] { ints, strs.Take(2).ToArray() }));                 // id yok
        Assert.IsNull(SnapshotCodec.Decode(new object[] { ints, new[] { "x" }.Concat(strs.Skip(1)).ToArray() })); // zaman
        Assert.IsNull(SnapshotCodec.Decode(new object[] { With(ints, 12, 9999), strs }));                   // gösterge idIndex aralık dışı
        Assert.IsNull(SnapshotCodec.Decode(new object[] { With(ints, 13, 7), strs }));                      // gösterge type > 2
        Assert.IsNull(SnapshotCodec.Decode(new object[] { With(ints, 14, 9), strs }));                      // color > 3
        Assert.IsNull(SnapshotCodec.Decode(new object[] { With(ints, 1, -1), strs }));                      // eksi sayı
        Assert.IsNull(SnapshotCodec.Decode(new object[] { With(ints, 20, int.MaxValue), strs }));           // dev sayaç (havuz)
    }
}
