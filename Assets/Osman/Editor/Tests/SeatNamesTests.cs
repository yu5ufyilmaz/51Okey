using System.Collections.Generic;
using NUnit.Framework;

public class SeatNamesTests
{
    static KeyValuePair<int, string> P(int s, string n) => new KeyValuePair<int, string>(s, n);

    [Test] public void BuildsFourSlotsBySeat()
    {
        var s = SeatNames.Build(new[] { P(3, "Zeynep"), P(1, "Ahmet"), P(9, "yok"), P(0, "yok"), P(2, null) });
        CollectionAssert.AreEqual(new[] { "Ahmet", "", "Zeynep", "" }, s);
        Assert.AreEqual(2, SeatNames.FreeCount(s));
    }

    [Test] public void FreeCount_NullIsFour() => Assert.AreEqual(4, SeatNames.FreeCount(null));
}
