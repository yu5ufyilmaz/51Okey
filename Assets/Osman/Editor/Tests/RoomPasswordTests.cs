using NUnit.Framework;

public class RoomPasswordTests
{
    [Test] public void EmptyPasswordHasNoHash()
    {
        Assert.AreEqual("", RoomPassword.Hash("masa", ""));
        Assert.AreEqual("", RoomPassword.Hash("masa", "   "));
        Assert.AreEqual("", RoomPassword.Hash("masa", null));
    }

    [Test] public void HashIsHexSha256_NotThePassword()
    {
        string h = RoomPassword.Hash("masa", "gizli");
        Assert.AreEqual(64, h.Length);
        StringAssert.IsMatch("^[0-9a-f]{64}$", h);
        StringAssert.DoesNotContain("gizli", h);
    }

    [Test] public void SamePasswordDifferentRoom_DifferentHash() =>
        Assert.AreNotEqual(RoomPassword.Hash("masa1", "gizli"), RoomPassword.Hash("masa2", "gizli"));

    [Test] public void Matches_TrimsAndCompares()
    {
        string h = RoomPassword.Hash("masa", "gizli");
        Assert.IsTrue(RoomPassword.Matches("masa", " gizli ", h));
        Assert.IsFalse(RoomPassword.Matches("masa", "Gizli", h));
        Assert.IsFalse(RoomPassword.Matches("masa", "", h));
        Assert.IsTrue(RoomPassword.Matches("masa", "", ""));
        Assert.IsFalse(RoomPassword.Matches("masa", "x", ""));
    }
}
