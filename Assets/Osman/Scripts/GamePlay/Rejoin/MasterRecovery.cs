using System.Collections.Generic;

// Yeni masa sahibi el içi ceza tablosunu her istemcide tutulan ceza kaydından (MatchScores.current, koltuk bazlı) kurar.
public static class PenaltyRebuild
{
    // Sonuç sıra (1..4) bazlı; her sıra var (cezası yoksa 0).
    public static Dictionary<int, int> FromLog(IEnumerable<(int seat, int amount)> log, int[] seatOfQue)
    {
        var d = new Dictionary<int, int> { { 1, 0 }, { 2, 0 }, { 3, 0 }, { 4, 0 } };
        foreach (var (seat, amount) in log)
            for (int q = 1; q <= 4; q++)
                if (seatOfQue != null && q < seatOfQue.Length && seatOfQue[q] == seat)
                    d[q] += amount;
        return d;
    }
}
