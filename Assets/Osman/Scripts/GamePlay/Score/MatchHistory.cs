using System.Collections.Generic;
using System.Linq;

public sealed class MatchHistory
{
    readonly SortedDictionary<int, HandResultData> hands = new SortedDictionary<int, HandResultData>();

    public void Set(HandResultData d) { if (d != null) hands[d.handIndex] = d; }   // aynı el yerine yazılır
    public IReadOnlyList<HandResultData> Hands => hands.Values.ToList();
    public HandResultData Last => hands.Count == 0 ? null : hands.Values.Last();
    public void Clear() => hands.Clear();

    public int Total(int seat) => hands.Values.Sum(h => h.rows.Where(r => r.seat == seat).Sum(r => r.handTotal));

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
