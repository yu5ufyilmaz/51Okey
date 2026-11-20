using System;
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
        s.ready = true; s.round = 3; s.turnSerial = 12; s.currentTurn = 2;
        s.seatOfQue = new[] { 0, 3, 4, 1, 2 };
        s.indicator = T(TileColor.red, 7); s.dropTile = T(TileColor.blue, 4);
        s.pile = Hand(5);
        s.hands[0] = Hand(14); s.hands[1] = Hand(14); s.hands[2] = Hand(15); s.hands[3] = Hand(14);
        s.hands[2][3] = T(TileColor.blue, 9, TileType.Joker);
        s.melds[1].Add(new List<Tiles> { T(TileColor.red, 2), T(TileColor.red, 2) });
        s.melds[1].Add(new List<Tiles> { T(TileColor.blue, 9), T(TileColor.blue, 9) });
        s.meldPositions[1].Add(new List<Vector2Int> { new Vector2Int(0, 1), new Vector2Int(0, 2) });
        s.meldPositions[1].Add(new List<Vector2Int>());
        for (int i = 0; i < 6; i++) s.discards.Add(new SnapDiscard { seat = i / 2 + 1, tile = T((TileColor)(i % 4), i + 1) });
        s.boards[2].Add(new SnapBoardTile { tile = T(TileColor.black, 5), meldType = 0, slot = 17 });
        s.boards[2].Add(new SnapBoardTile { tile = T(TileColor.black, 6), meldType = 0, slot = 18 });
        s.boards[2].Add(new SnapBoardTile { tile = T(TileColor.red, 1, TileType.Joker), meldType = 1, slot = 2 });
        s.boards[2].Add(new SnapBoardTile { tile = T(TileColor.black, 2, TileType.FakeJoker), meldType = 2, slot = 0 });
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
        // Canonical joker değerini yazmaz: renk/sayı ayrıca denetlenir.
        var j = d.hands[2][3]; Assert.AreEqual(TileType.Joker, j.type); Assert.AreEqual(TileColor.blue, j.color); Assert.AreEqual(9, j.number);
        var f = d.boards[2][3].tile; Assert.AreEqual(TileType.FakeJoker, f.type); Assert.AreEqual(TileColor.black, f.color); Assert.AreEqual(2, f.number);
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
    static TableSnapshot MinimalValid() { var e = TableSnapshot.Empty(); e.currentTurn = 1; e.seatOfQue = new[] { 0, 1, 2, 3, 4 }; return e; }
    [Test] public void Empty_Is_Not_Ready() => Assert.IsFalse(TableSnapshot.Empty().ready);
    [Test] public void Minimal_RoundTrips() {
        var e = MinimalValid(); var d = SnapshotCodec.Decode(SnapshotCodec.Encode(e));
        Assert.AreEqual(e.Canonical(true), d.Canonical(true));
    }
    static void Rejected(Action<TableSnapshot> bad, string why) {
        var s = Sample(); bad(s);
        Assert.IsNull(SnapshotCodec.Decode(SnapshotCodec.Encode(s)), why);
    }
    [Test] public void Decode_Rejects_Impossible_Content() {
        Assert.IsNotNull(SnapshotCodec.Decode(SnapshotCodec.Encode(Sample())));
        Rejected(s => s.currentTurn = 0, "turn 0"); Rejected(s => s.currentTurn = 5, "turn 5");
        Rejected(s => s.seatOfQue[2] = 3, "seatOfQue tekrar"); Rejected(s => s.seatOfQue[1] = 0, "seatOfQue 0"); Rejected(s => s.seatOfQue[4] = 5, "seatOfQue 5");
        Rejected(s => s.boards[2][0] = new SnapBoardTile { tile = s.boards[2][0].tile, meldType = 3, slot = 1 }, "meldType 3");
        Rejected(s => s.discards[0] = new SnapDiscard { seat = 5, tile = s.discards[0].tile }, "atık koltuğu 5");
        Rejected(s => s.discards[0] = new SnapDiscard { seat = 0, tile = s.discards[0].tile }, "atık koltuğu 0");
        Rejected(s => s.hands[1][0] = s.pile[0], "çift taş: havuz+el");
        Rejected(s => s.discards[1] = new SnapDiscard { seat = 1, tile = s.discards[0].tile }, "çift taş: atık+atık");
        Rejected(s => s.boards[2][1] = new SnapBoardTile { tile = s.hands[0][0], meldType = 0, slot = 3 }, "çift taş: tahta+el");
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

    // (alan, değişiklik): özeti DEĞİŞTİRMEK zorunda. Alan adı TableSnapshot'ın public alanıdır.
    static readonly (string field, string name, Action<TableSnapshot> change)[] Mutations = {
        ("currentTurn", "turn", s => s.currentTurn = 4), ("turnSerial", "serial", s => s.turnSerial++), ("round", "round", s => s.round++),
        ("ready", "ready", s => s.ready = !s.ready), ("ended", "ended", s => s.ended = !s.ended), ("seatOfQue", "seats", s => s.seatOfQue[1] = 2),
        ("indicator", "ind", s => s.indicator.number = 8), ("dropTile", "drop", s => s.dropTile = null), ("pile", "pileOrder", s => s.pile.Reverse()),
        ("pile", "pileCount", s => s.pile.RemoveAt(0)), ("hands", "handOrder", s => s.hands[0].Reverse()), ("hands", "handMove", s => { s.hands[1].Add(s.hands[0][0]); s.hands[0].RemoveAt(0); }),
        ("melds", "meldTile", s => s.melds[1][0].RemoveAt(0)),
        ("discards", "discardSeat", s => s.discards[0] = new SnapDiscard { seat = 4, tile = s.discards[0].tile }), ("discards", "discardOrder", s => s.discards.Reverse()),
        ("boards", "boardSlot", s => s.boards[2][0] = new SnapBoardTile { tile = s.boards[2][0].tile, meldType = s.boards[2][0].meldType, slot = 7 }),
        ("boards", "boardType", s => s.boards[2][0] = new SnapBoardTile { tile = s.boards[2][0].tile, meldType = 2, slot = s.boards[2][0].slot }),
        ("opened", "opened", s => s.opened.Add(3)), ("openedPairs", "pairs", s => s.openedPairs.Add(1)),
        ("openValues", "openValue", s => s.openValues[0] = new SnapOpenValue { que = 2, value = 69 }),
        ("openValues", "openPairs", s => s.openValues[0] = new SnapOpenValue { que = 2, value = 68, pairs = true }),
    };
    // Özette OLMAYAN alanlar ve nedeni. Yeni alan ya Mutations'a ya buraya girmeli (Every_Field_Has_A_Digest_Decision).
    static readonly (string field, Action<TableSnapshot> change)[] Ignored = {
        ("turnStartTime", s => s.turnStartTime += 3), ("devDeal", s => s.devDeal = 9), ("devMove", s => s.devMove = 1),
        // Ceza listesi gösterim verisi: ceza masa sahibinde duyurulur, eyleyen istemci kendi NextTurn'ünü ondan önce işler; aynı anahtarda istemciler ayrışır.
        ("penalties", s => s.penalties[0] = new SnapPenalty { seat = 1, amount = 251, reason = 1 }),
        ("penalties", s => s.penalties[0] = new SnapPenalty { seat = 1, amount = 250, reason = 2 }),
        // Per konumları yalnız per sahibi olmayan istemcilerde dolu (sahibinde RPC listesi gönderildikten sonra Clear edilir); ayrıntı raporda.
        ("meldPositions", s => s.meldPositions[1][0][0] = new Vector2Int(9, 9)),
    };
    [Test] public void Digest_Changes_For_Every_Field() {
        uint baseD = TableDigest.Of(Sample());
        foreach (var (field, name, change) in Mutations) { var s = Sample(); change(s); Assert.AreNotEqual(baseD, TableDigest.Of(s), name); }
    }
    [Test] public void Digest_Ignores_Documented_Exclusions() {
        uint baseD = TableDigest.Of(Sample());
        foreach (var (field, change) in Ignored) { var s = Sample(); change(s); Assert.AreEqual(baseD, TableDigest.Of(s), field); Assert.AreNotEqual(Sample().Canonical(true), s.Canonical(true), field + " tam metinde olmalı"); }
    }
    [Test] public void Every_Field_Has_A_Digest_Decision() {
        var fields = typeof(TableSnapshot).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Select(f => f.Name).OrderBy(x => x).ToArray();
        var decided = Mutations.Select(m => m.field).Concat(Ignored.Select(i => i.field)).Distinct().OrderBy(x => x).ToArray();
        CollectionAssert.AreEqual(fields, decided);
    }
    [Test] public void Digest_Ignores_Joker_Values() {
        var s = Sample(); uint d = TableDigest.Of(s);
        s.hands.SelectMany(h => h).First(t => t.type == TileType.Joker).number = 12;
        Assert.AreEqual(d, TableDigest.Of(s));
    }
    [Test] public void Ring_Keeps_Last_Sixteen() {
        var r = new DigestRing(16); for (int i = 1; i <= 20; i++) r.Put(1, i, (uint)i);
        Assert.IsFalse(r.TryGet(1, 4, out _)); Assert.IsTrue(r.TryGet(1, 5, out uint v)); Assert.AreEqual(5u, v);
        r.Put(1, 20, 99); Assert.IsTrue(r.TryGet(1, 20, out v)); Assert.AreEqual(99u, v); Assert.IsFalse(r.TryGet(2, 20, out _));
    }
}
