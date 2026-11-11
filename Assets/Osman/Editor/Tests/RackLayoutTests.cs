using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class RackLayoutTests
{
    static Tiles T(TileColor c, int n) => new Tiles(c, n, TileType.Number);

    static List<Tiles> Run(TileColor c, int from, int len) =>
        Enumerable.Range(from, len).Select(n => T(c, n)).ToList();

    static int Row(int slot) => slot / RackLayout.RowSize;

    [Test]
    public void Layout_GroupsAreContiguousAndSeparated()
    {
        var g1 = Run(TileColor.red, 1, 3);
        var g2 = new List<Tiles> { T(TileColor.black, 4), T(TileColor.blue, 4), T(TileColor.yellow, 4) };
        var rest = new List<Tiles> { T(TileColor.yellow, 9), T(TileColor.blue, 11), T(TileColor.red, 13) };
        var slots = RackLayout.Place(new List<List<Tiles>> { g1, g2 }, rest);

        Assert.IsNotNull(slots);
        foreach (var g in new[] { g1, g2 })
        {
            var s = g.Select(t => slots[t.id]).ToList();
            for (int i = 1; i < s.Count; i++)
                Assert.AreEqual(s[i - 1] + 1, s[i], "grup bitişik ve sıralı");
            Assert.AreEqual(Row(s[0]), Row(s.Last()), "grup tek satırda");
        }
        var occupied = new HashSet<int>(slots.Values);
        int g1End = slots[g1.Last().id];
        Assert.IsFalse(occupied.Contains(g1End + 1) && Row(g1End + 1) == Row(g1End), "gruptan sonra boşluk");
        Assert.AreEqual(9, slots.Count);
        Assert.IsTrue(slots.Values.All(s => s >= 0 && s < 2 * RackLayout.RowSize));
    }

    [Test]
    public void Layout_RestDoesNotFormAccidentalGroup()
    {
        var rest = Run(TileColor.red, 5, 5); // r5..r9 yan yana gelirse oyun seri sayar
        var slots = RackLayout.Place(new List<List<Tiles>>(), rest);
        var s = rest.Select(t => slots[t.id]).OrderBy(x => x).ToList();
        for (int i = 1; i < s.Count; i++)
            Assert.IsTrue(s[i] - s[i - 1] > 1 || Row(s[i]) != Row(s[i - 1]), "tek taşlar arasında boşluk");
    }

    [Test]
    public void Layout_ReturnsNullWhenItCannotFit()
    {
        var groups = new List<List<Tiles>>
        {
            Run(TileColor.red, 1, 10),
            Run(TileColor.blue, 1, 10),
            Run(TileColor.black, 1, 10),
        };
        Assert.IsNull(RackLayout.Place(groups, new List<Tiles>()));
    }
}
