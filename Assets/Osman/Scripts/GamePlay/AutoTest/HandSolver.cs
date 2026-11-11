using System;
using System.Collections.Generic;
using System.Linq;

// Test oyuncusu için elin en iyi seri/grup ve çift bölünmesi. Oyun kurallarını çağırmaz, girdiyi değiştirmez
// (oyunun kural kontrolü okey taşlarının sayı/rengini yerinde değiştiriyor). Dönen taşlar kopyadır, id'ler korunur.
public class MeldSet
{
    public List<List<Tiles>> Melds = new List<List<Tiles>>();
    public int Score;
}

public static class HandSolver
{
    public static List<Tiles> Copy(IEnumerable<Tiles> tiles) =>
        tiles
            .Select(t => new Tiles
            {
                id = t.id,
                color = t.color,
                number = t.number,
                type = t.type,
            })
            .ToList();

    static bool IsOkey(Tiles t) => t.type == TileType.Joker;

    static bool IsIndicatorCopy(Tiles t, Tiles ind) =>
        ind != null && t.type == TileType.Number && t.color == ind.color && t.number == ind.number;

    static int Key(Tiles t) => (int)t.color * 100 + t.number;

    static int OkeyNumber(Tiles ind) => ind != null ? ind.number % 13 + 1 : 0;

    // Aday per: sıralı taş anahtarları (-1 = okey), kullandığı okey sayısı, puanı.
    class Candidate
    {
        public List<int> Ordered;
        public int Okeys;
        public int Score;
    }

    public static MeldSet BestSeries(IList<Tiles> hand, Tiles indicator)
    {
        List<Tiles> tiles = Copy(hand);
        int okeys = tiles.Count(IsOkey);
        var counts = new Dictionary<int, int>();
        foreach (var t in tiles.Where(t => !IsOkey(t)))
            counts[Key(t)] = counts.TryGetValue(Key(t), out int c) ? c + 1 : 1;

        var candidates = new List<Candidate>();
        foreach (TileColor color in Enum.GetValues(typeof(TileColor)))
        {
            for (int start = 1; start <= 11; start++)
            for (int len = 3; start + len - 1 <= 13; len++)
            {
                var ordered = new List<int>();
                int missing = 0;
                for (int n = start; n < start + len; n++)
                {
                    int k = (int)color * 100 + n;
                    if (counts.ContainsKey(k))
                        ordered.Add(k);
                    else
                    {
                        ordered.Add(-1);
                        missing++;
                    }
                }
                // Oyunun kontrolü başta iki okeyi kabul etmez (ilk okey sağ komşusuna bakar).
                bool twoOkeysFirst = ordered[0] == -1 && ordered[1] == -1;
                if (missing <= 2 && missing < len && !twoOkeysFirst)
                    candidates.Add(
                        new Candidate
                        {
                            Ordered = ordered,
                            Okeys = missing,
                            Score = Enumerable.Range(start, len).Sum(),
                        }
                    );
            }
        }
        for (int n = 1; n <= 13; n++)
        {
            var present = Enum.GetValues(typeof(TileColor))
                .Cast<TileColor>()
                .Select(c => (int)c * 100 + n)
                .Where(counts.ContainsKey)
                .ToList();
            for (int mask = 1; mask < (1 << present.Count); mask++)
            {
                var used = present.Where((k, i) => (mask & (1 << i)) != 0).ToList();
                for (int size = 3; size <= 4; size++)
                {
                    int need = size - used.Count;
                    if (need < 0 || need > 2)
                        continue;
                    candidates.Add(
                        new Candidate
                        {
                            Ordered = used.Concat(Enumerable.Repeat(-1, need)).ToList(),
                            Okeys = need,
                            Score = n * size,
                        }
                    );
                }
            }
        }

        var memo = new Dictionary<string, (int score, List<Candidate> picks)>();
        var (score, picks) = Solve(counts, okeys, candidates, memo);
        return Build(tiles, picks, score);
    }

    // En küçük taş ya perde kullanılır ya da elde bırakılır; aynı durumlar ezberlenir.
    static (int, List<Candidate>) Solve(
        Dictionary<int, int> counts,
        int okeys,
        List<Candidate> candidates,
        Dictionary<string, (int, List<Candidate>)> memo
    )
    {
        var keys = counts.Where(kv => kv.Value > 0).Select(kv => kv.Key).OrderBy(k => k).ToList();
        if (keys.Count == 0)
            return (0, new List<Candidate>());
        string state = string.Join(",", keys.Select(k => k + "x" + counts[k])) + "|" + okeys;
        if (memo.TryGetValue(state, out var cached))
            return cached;

        int smallest = keys[0];
        counts[smallest]--;
        var best = Solve(counts, okeys, candidates, memo);
        counts[smallest]++;

        foreach (var cand in candidates.Where(c => c.Ordered.Contains(smallest)))
        {
            if (cand.Okeys > okeys)
                continue;
            var needed = cand.Ordered.Where(k => k >= 0).GroupBy(k => k).ToList();
            if (needed.Any(g => !counts.TryGetValue(g.Key, out int have) || have < g.Count()))
                continue;
            foreach (var g in needed)
                counts[g.Key] -= g.Count();
            var (subScore, subPicks) = Solve(counts, okeys - cand.Okeys, candidates, memo);
            foreach (var g in needed)
                counts[g.Key] += g.Count();
            if (subScore + cand.Score > best.Item1)
                best = (subScore + cand.Score, new List<Candidate>(subPicks) { cand });
        }
        memo[state] = best;
        return best;
    }

    static MeldSet Build(List<Tiles> tiles, List<Candidate> picks, int score)
    {
        var pool = tiles.Where(t => !IsOkey(t)).GroupBy(Key).ToDictionary(g => g.Key, g => new Queue<Tiles>(g));
        var okeyPool = new Queue<Tiles>(tiles.Where(IsOkey));
        var set = new MeldSet { Score = score };
        foreach (var cand in picks)
            set.Melds.Add(cand.Ordered.Select(k => k >= 0 ? pool[k].Dequeue() : okeyPool.Dequeue()).ToList());
        return set;
    }

    public static MeldSet BestPairs(IList<Tiles> hand, Tiles indicator)
    {
        List<Tiles> tiles = Copy(hand);
        var set = new MeldSet();
        var okeys = new Queue<Tiles>(tiles.Where(IsOkey));
        var indicatorCopies = new Queue<Tiles>(tiles.Where(t => IsIndicatorCopy(t, indicator)));
        var leftovers = new List<Tiles>();

        foreach (var g in tiles.Where(t => !IsOkey(t) && !IsIndicatorCopy(t, indicator)).GroupBy(Key))
        {
            var group = g.ToList();
            for (int i = 0; i + 1 < group.Count; i += 2)
            {
                set.Melds.Add(new List<Tiles> { group[i], group[i + 1] });
                set.Score += group[i].number * 2;
            }
            if (group.Count % 2 == 1)
                leftovers.Add(group[group.Count - 1]);
        }

        // Okey eşinin sayısını alır: en büyük tekleri okeyle eşle.
        leftovers = leftovers.OrderByDescending(t => t.number).ToList();
        while (okeys.Count > 0 && leftovers.Count > 0)
        {
            var partner = leftovers[0];
            leftovers.RemoveAt(0);
            set.Melds.Add(new List<Tiles> { partner, okeys.Dequeue() });
            set.Score += partner.number * 2;
        }
        // Gösterge kopyası hangi taşla eşleşirse eşleşsin kendi sayısı × 2: en küçük tekle eşle.
        while (indicatorCopies.Count > 0 && (leftovers.Count > 0 || okeys.Count > 0))
        {
            Tiles partner;
            if (leftovers.Count > 0)
            {
                partner = leftovers[leftovers.Count - 1];
                leftovers.RemoveAt(leftovers.Count - 1);
            }
            else
                partner = okeys.Dequeue();
            set.Melds.Add(new List<Tiles> { partner, indicatorCopies.Dequeue() });
            set.Score += indicator.number * 2;
        }
        if (okeys.Count >= 2)
        {
            set.Melds.Add(new List<Tiles> { okeys.Dequeue(), okeys.Dequeue() });
            set.Score += OkeyNumber(indicator) * 2;
        }
        return set;
    }
}
