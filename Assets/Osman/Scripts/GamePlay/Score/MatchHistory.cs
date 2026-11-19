using System.Collections.Generic;
using System.Linq;

public sealed class MatchHistory
{
    readonly SortedDictionary<int, HandResultData> hands = new SortedDictionary<int, HandResultData>();

    public void Set(HandResultData d) { if (d == null) return; if (d.rows == null) d.rows = new List<HandRow>(); hands[d.handIndex] = d; }   // aynı el yerine yazılır
    public IReadOnlyList<HandResultData> Hands => hands.Values.ToList();
    public HandResultData Last => hands.Count == 0 ? null : hands.Values.Last();
    public void Clear() => hands.Clear();

    // long'da topla, int aralığına kırp: bozuk veri OverflowException atmasın
    public int Total(int seat)
    {
        long t = 0;
        foreach (var h in hands.Values) foreach (var r in h.rows) if (r.seat == seat) t += r.handTotal;
        return (int)System.Math.Max(int.MinValue, System.Math.Min(int.MaxValue, t));
    }

    // en düşük toplam; eşitlikte küçük koltuk; el yoksa 0
    public int LeaderSeat
    {
        get
        {
            var seats = hands.Values.SelectMany(h => h.rows).Select(r => r.seat).Distinct().OrderBy(s => s).ToList();
            if (seats.Count == 0) return 0;
            int best = seats[0], bestT = Total(best);
            foreach (var s in seats) { int t = Total(s); if (t < bestT) { best = s; bestT = t; } }
            return best;
        }
    }
}
