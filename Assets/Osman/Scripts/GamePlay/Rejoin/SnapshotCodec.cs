using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// Photon yerel dizi türleriyle: { int[] ints, string[] strs }. strs[0] = tur başlangıç zamanı, strs[1..] = taş id'leri.
public static class SnapshotCodec
{
    sealed class Bad : Exception { }

    public static object[] Encode(TableSnapshot s)
    {
        var ints = new List<int>();
        var ids = new List<string> { s.turnStartTime.ToString("R", CultureInfo.InvariantCulture) };
        var index = new Dictionary<string, int>();
        void Tile(Tiles t)
        {
            if (t == null) { ints.Add(-1); ints.Add(0); ints.Add(0); ints.Add(0); return; }
            string id = t.id ?? "";
            if (!index.TryGetValue(id, out int ix)) { ix = ids.Count - 1; index[id] = ix; ids.Add(id); }
            ints.Add(ix); ints.Add((int)t.type); ints.Add((int)t.color); ints.Add(t.number);
        }
        void Tl(List<Tiles> l) { ints.Add(l.Count); foreach (var t in l) Tile(t); }

        ints.AddRange(new[] { TableSnapshot.Version, s.round, s.turnSerial, s.currentTurn, s.ready ? 1 : 0, s.ended ? 1 : 0, s.devDeal, s.devMove });
        for (int q = 1; q <= 4; q++) ints.Add(s.seatOfQue[q]);
        Tile(s.indicator); Tile(s.dropTile);
        Tl(s.pile);
        for (int i = 0; i < 4; i++) Tl(s.hands[i]);
        for (int i = 0; i < 4; i++)
        {
            ints.Add(s.melds[i].Count);
            for (int m = 0; m < s.melds[i].Count; m++)
            {
                Tl(s.melds[i][m]);
                var pos = m < s.meldPositions[i].Count ? s.meldPositions[i][m] : new List<Vector2Int>();
                ints.Add(pos.Count);
                foreach (var p in pos) { ints.Add(p.x); ints.Add(p.y); }
            }
        }
        ints.Add(s.discards.Count);
        foreach (var d in s.discards) { ints.Add(d.seat); Tile(d.tile); }
        for (int i = 0; i < 4; i++)
        {
            ints.Add(s.boards[i].Count);
            foreach (var b in s.boards[i]) { ints.Add(b.meldType); ints.Add(b.slot); Tile(b.tile); }
        }
        ints.Add(s.opened.Count); ints.AddRange(s.opened);
        ints.Add(s.openedPairs.Count); ints.AddRange(s.openedPairs);
        ints.Add(s.openValues.Count);
        foreach (var o in s.openValues) { ints.Add(o.que); ints.Add(o.value); ints.Add(o.pairs ? 1 : 0); }
        ints.Add(s.penalties.Count);
        foreach (var p in s.penalties) { ints.Add(p.seat); ints.Add(p.amount); ints.Add(p.reason); }
        return new object[] { ints.ToArray(), ids.ToArray() };
    }

    // Bozuk/eksik/fazla veri: null (asla atmaz)
    public static TableSnapshot Decode(object[] payload)
    {
        try
        {
            if (payload == null || payload.Length != 2) return null;
            var a = payload[0] as int[]; var ids = payload[1] as string[];
            if (a == null || ids == null || ids.Length < 1) return null;
            if (!double.TryParse(ids[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double time)) return null;
            int pos = 0;
            int Int() { if (pos >= a.Length || a[pos] < 0) throw new Bad(); return a[pos++]; }
            // sayaç: kalan int'ten fazla öğe olamaz (dev bellek ayırma yok)
            int Count() { int n = Int(); if (n > a.Length - pos) throw new Bad(); return n; }
            Tiles Tile()
            {
                if (pos + 4 > a.Length) throw new Bad();
                int ix = a[pos], type = a[pos + 1], color = a[pos + 2], num = a[pos + 3]; pos += 4;
                if (type < 0 || type > 2 || color < 0 || color > 3 || num < 0) throw new Bad();
                if (ix == -1) return null;
                if (ix < 0 || ix + 1 >= ids.Length) throw new Bad();
                return new Tiles { id = ids[ix + 1], type = (TileType)type, color = (TileColor)color, number = num };
            }
            List<Tiles> Tl() { int n = Count(); var l = new List<Tiles>(n); for (int i = 0; i < n; i++) l.Add(Tile()); return l; }

            if (Int() != TableSnapshot.Version) return null;
            var s = TableSnapshot.Empty();
            s.turnStartTime = time;
            s.round = Int(); s.turnSerial = Int(); s.currentTurn = Int(); s.ready = Int() != 0; s.ended = Int() != 0;
            s.devDeal = Int(); s.devMove = Int();
            for (int q = 1; q <= 4; q++) s.seatOfQue[q] = Int();
            s.indicator = Tile(); s.dropTile = Tile();
            s.pile = Tl();
            for (int i = 0; i < 4; i++) s.hands[i] = Tl();
            for (int i = 0; i < 4; i++)
            {
                int n = Count();
                for (int m = 0; m < n; m++)
                {
                    s.melds[i].Add(Tl());
                    int pc = Count(); var ps = new List<Vector2Int>(pc);
                    for (int k = 0; k < pc; k++) { int r = Int(); ps.Add(new Vector2Int(r, Int())); }
                    s.meldPositions[i].Add(ps);
                }
            }
            for (int n = Count(), i = 0; i < n; i++) { int seat = Int(); s.discards.Add(new SnapDiscard { seat = seat, tile = Tile() }); }
            for (int i = 0; i < 4; i++)
                for (int n = Count(), k = 0; k < n; k++) { int mt = Int(); int sl = Int(); s.boards[i].Add(new SnapBoardTile { meldType = mt, slot = sl, tile = Tile() }); }
            for (int n = Count(), i = 0; i < n; i++) s.opened.Add(Int());
            for (int n = Count(), i = 0; i < n; i++) s.openedPairs.Add(Int());
            for (int n = Count(), i = 0; i < n; i++) { int q = Int(); int v = Int(); s.openValues.Add(new SnapOpenValue { que = q, value = v, pairs = Int() != 0 }); }
            for (int n = Count(), i = 0; i < n; i++) { int se = Int(); int am = Int(); s.penalties.Add(new SnapPenalty { seat = se, amount = am, reason = Int() }); }
            return pos == a.Length ? s : null;
        }
        catch (Bad) { return null; }
    }
}
