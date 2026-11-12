using System.Linq;
using NUnit.Framework;

public class GameConfigTests
{
    [Test]
    public void DefaultName_IsOyuncuPlusFourDigits() =>
        StringAssert.IsMatch(@"^Oyuncu\d{4}$", GameConfig.DefaultName("local-51Okey"));

    [Test]
    public void DefaultName_IsStableForSameUser() =>
        Assert.AreEqual(GameConfig.DefaultName("abc"), GameConfig.DefaultName("abc"));

    [Test]
    public void DefaultName_DiffersForTheFourEditors()
    {
        var ids = new[] { "local-51Okey", "local-51Okey_clone_0", "local-51Okey_clone_1", "local-51Okey_clone_2" };
        Assert.AreEqual(4, ids.Select(GameConfig.DefaultName).Distinct().Count());
    }

    [TestCase(0, 1)]
    [TestCase(99, 1)]
    [TestCase(100, 2)]
    [TestCase(250, 3)]
    public void LevelForXp_OneLevelPerHundredXp(int xp, int level) =>
        Assert.AreEqual(level, GameConfig.LevelForXp(xp));

    [Test]
    public void CoinTx_KeepsItsFields()
    {
        var tx = new CoinTx("t1", -40, CoinReason.TableFee, "oda-7");
        Assert.AreEqual("t1", tx.Id);
        Assert.AreEqual(-40, tx.Amount);
        Assert.AreEqual(CoinReason.TableFee, tx.Reason);
        Assert.AreEqual("oda-7", tx.Reference);
    }
}
