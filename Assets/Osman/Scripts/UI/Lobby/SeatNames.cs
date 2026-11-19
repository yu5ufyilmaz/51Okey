using System.Collections.Generic;

// Koltuk numarası (1..4) -> ad dizisi; oda özelliğinde taşınır.
public static class SeatNames
{
    public static string[] Build(IEnumerable<KeyValuePair<int, string>> seatToName)
    {
        var seats = new[] { "", "", "", "" };
        foreach (var kv in seatToName)
            if (kv.Key >= 1 && kv.Key <= 4)
                seats[kv.Key - 1] = kv.Value ?? "";
        return seats;
    }

    public static int FreeCount(string[] seats)
    {
        if (seats == null)
            return 4;
        int n = 0;
        foreach (string s in seats)
            if (string.IsNullOrEmpty(s)) n++;
        return n;
    }
}
