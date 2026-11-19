using System;
using System.Collections.Generic;
using System.Linq;

public struct HandRow { public int seat, tilesLeft, handPoints, inHandPenalty, finishReward, handTotal; public bool opened; }

public sealed class HandResultData
{
    public int handIndex, winnerSeat, finishMultiplier;
    public bool okeyFinish, pairsFinish, kafa;
    public List<HandRow> rows = new List<HandRow>();   // koltuk sırasıyla
}

public struct SeatInput { public int seat; public bool opened, openedPairs; public int normalTiles, okeys, inHandPenalty; }

public static class HandBreakdown
{
    const int Head = 4, Per = 7;

    // endPenalty(satır, bitirenMi): PenaltySystem.EndPenalty değeri (çarpanlar dahil)
    public static HandResultData Build(int handIndex, int winnerSeat, int finishMultiplier, bool okeyFinish, bool pairsFinish, bool kafa,
                                       IReadOnlyList<SeatInput> seats, Func<SeatInput, bool, int> endPenalty)
    {
        var d = new HandResultData { handIndex = handIndex, winnerSeat = winnerSeat, finishMultiplier = finishMultiplier,
                                     okeyFinish = okeyFinish, pairsFinish = pairsFinish, kafa = kafa };
        // kablo biçimi koltuk sırası ister; null = boş
        foreach (var s in (seats ?? new SeatInput[0]).OrderBy(x => x.seat))
        {
            bool win = winnerSeat != 0 && s.seat == winnerSeat;
            var r = new HandRow { seat = s.seat, opened = s.opened, tilesLeft = s.normalTiles + s.okeys, inHandPenalty = s.inHandPenalty };
            if (win) r.finishReward = endPenalty(s, true); else r.handPoints = endPenalty(s, false);
            r.handTotal = r.handPoints + r.inHandPenalty + r.finishReward;
            d.rows.Add(r);
        }
        return d;
    }

    public static int[] Encode(HandResultData d)
    {
        if (d == null) return null;
        if (d.rows == null) d.rows = new List<HandRow>();
        var a = new int[Head + Per * d.rows.Count];
        a[0] = d.handIndex; a[1] = d.winnerSeat; a[2] = d.finishMultiplier;
        a[3] = (d.okeyFinish ? 1 : 0) | (d.pairsFinish ? 2 : 0) | (d.kafa ? 4 : 0);
        for (int i = 0; i < d.rows.Count; i++)
        {
            var r = d.rows[i]; int o = Head + Per * i;
            a[o] = r.seat; a[o + 1] = r.opened ? 1 : 0; a[o + 2] = r.tilesLeft; a[o + 3] = r.handPoints;
            a[o + 4] = r.inHandPenalty; a[o + 5] = r.finishReward; a[o + 6] = r.handTotal;
        }
        return a;
    }

    // Asla atmaz; kısa/null: null, eksik son satır atılır
    public static HandResultData Decode(int[] a)
    {
        if (a == null || a.Length < Head) return null;
        var d = new HandResultData { handIndex = a[0], winnerSeat = a[1], finishMultiplier = a[2],
                                     okeyFinish = (a[3] & 1) != 0, pairsFinish = (a[3] & 2) != 0, kafa = (a[3] & 4) != 0 };
        for (int o = Head; o + Per <= a.Length; o += Per)
            d.rows.Add(new HandRow { seat = a[o], opened = a[o + 1] != 0, tilesLeft = a[o + 2], handPoints = a[o + 3],
                                     inHandPenalty = a[o + 4], finishReward = a[o + 5], handTotal = a[o + 6] });
        return d;
    }
}
