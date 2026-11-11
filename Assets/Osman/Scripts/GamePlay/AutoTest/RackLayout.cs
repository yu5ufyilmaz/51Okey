using System.Collections.Generic;
using System.Linq;

// Istaka: 2 satır × 15 slot (0–14, 15–29). Oyun (ScoreManager.GetSplittedGroups) satırda bitişik taşları
// bir grup sayar, boş slot grubu böler. Perler bitişik, aralarında boşluk; tek taşlar da yer varsa aralıklı
// (yan yana gelip kazara per oluşturmasınlar).
public static class RackLayout
{
    public const int RowSize = 15;
    const int Rows = 2;

    // Taş id → slot. Perler sığmazsa null.
    public static Dictionary<string, int> Place(List<List<Tiles>> groups, List<Tiles> rest)
    {
        var slots = new Dictionary<string, int>();
        int[] cursor = new int[Rows];

        foreach (var group in groups.OrderByDescending(g => g.Count))
        {
            int row = -1;
            for (int r = 0; r < Rows && row < 0; r++)
                if (RowSize - cursor[r] >= group.Count)
                    row = r;
            if (row < 0)
                return null;
            for (int i = 0; i < group.Count; i++)
                slots[group[i].id] = row * RowSize + cursor[row] + i;
            cursor[row] += group.Count + 1;
        }

        var free = Enumerable
            .Range(0, Rows)
            .SelectMany(r => Enumerable.Range(cursor[r], System.Math.Max(0, RowSize - cursor[r])).Select(c => r * RowSize + c))
            .ToList();
        // Önce aralıklı (her ikinci boş slot), yetmezse bitişik.
        var spaced = free.Where(s => (s % RowSize - cursor[s / RowSize]) % 2 == 0).ToList();
        var target = spaced.Count >= rest.Count ? spaced : free;
        if (target.Count < rest.Count)
            return null;
        for (int i = 0; i < rest.Count; i++)
            slots[rest[i].id] = target[i];
        return slots;
    }
}
