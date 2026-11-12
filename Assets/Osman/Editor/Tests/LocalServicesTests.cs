using System.IO;
using NUnit.Framework;

public class LocalServicesTests
{
    string path;

    [SetUp]
    public void SetUp() =>
        path = Path.Combine(Path.GetTempPath(), "okey-test-" + System.Guid.NewGuid().ToString("N") + ".json");

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    LocalProfileStore Loaded()
    {
        var s = new LocalProfileStore(path);
        s.LoadOrCreateAsync("u1", "Oyuncu0001").GetAwaiter().GetResult();
        return s;
    }

    static bool Apply(IWallet w, string id, long amount) =>
        w.ApplyAsync(new CoinTx(id, amount, CoinReason.Purchase)).GetAwaiter().GetResult();

    [Test]
    public void Auth_UserIdIsNullBeforeSignIn_ThenLocalPlusTag()
    {
        var auth = new LocalAuthService("51Okey_clone_0");
        Assert.IsNull(auth.UserId);
        auth.SignInAsync().GetAwaiter().GetResult();
        Assert.AreEqual("local-51Okey_clone_0", auth.UserId);
        Assert.IsTrue(auth.IsGuest);
    }

    [Test]
    public void Profile_NewProfileGetsStarterCoinsNameAndLevelOne()
    {
        var s = Loaded();
        Assert.AreEqual("u1", s.Current.UserId);
        Assert.AreEqual("Oyuncu0001", s.Current.DisplayName);
        Assert.AreEqual(GameConfig.StarterCoins, s.Coins);
        Assert.AreEqual(1, s.Current.Level);
        Assert.IsTrue(File.Exists(path));
    }

    [Test]
    public void Profile_SecondStoreOnSameFileKeepsState()
    {
        Assert.IsTrue(Apply(Loaded(), "t1", 50));
        Assert.AreEqual(GameConfig.StarterCoins + 50, Loaded().Coins);
    }

    [Test]
    public void Profile_LoadTwiceGrantsStarterOnce()
    {
        var s = Loaded();
        s.LoadOrCreateAsync("u1", "Oyuncu0001").GetAwaiter().GetResult();
        Assert.AreEqual(GameConfig.StarterCoins, s.Coins);
    }

    [Test]
    public void Profile_CorruptFile_StartsFresh()
    {
        File.WriteAllText(path, "{ \"profile\": { \"Coins\": 99");
        Assert.AreEqual(GameConfig.StarterCoins, Loaded().Coins); // LogError basılırsa Unity testi kendiliğinden düşürür
    }

    [Test]
    public void Wallet_AddsCoinsAndFiresChanged()
    {
        var s = Loaded();
        long seen = -1;
        s.Changed += p => seen = p.Coins;
        Assert.IsTrue(Apply(s, "t1", 200));
        Assert.AreEqual(GameConfig.StarterCoins + 200, s.Coins);
        Assert.AreEqual(s.Coins, seen);
    }

    [Test]
    public void Wallet_RejectsOverspendAndLeavesBalance()
    {
        var s = Loaded();
        Assert.IsFalse(Apply(s, "t1", -(GameConfig.StarterCoins + 1)));
        Assert.AreEqual(GameConfig.StarterCoins, s.Coins);
        Assert.AreEqual(GameConfig.StarterCoins, Loaded().Coins);
        Assert.IsTrue(Apply(s, "t2", -GameConfig.StarterCoins), "tam bakiye harcanabilir");
        Assert.AreEqual(0, s.Coins);
    }

    [Test]
    public void Wallet_SameTxIdAppliesOnce()
    {
        var s = Loaded();
        Assert.IsTrue(Apply(s, "satin-alma-1", 100));
        Assert.IsTrue(Apply(s, "satin-alma-1", 100));
        Assert.AreEqual(GameConfig.StarterCoins + 100, s.Coins);
        Assert.IsTrue(Apply(Loaded(), "satin-alma-1", 100), "yeniden açılışta da hatırlanır");
        Assert.AreEqual(GameConfig.StarterCoins + 100, Loaded().Coins);
    }

    [Test]
    public void Wallet_BeforeLoad_ReturnsFalse() =>
        Assert.IsFalse(Apply(new LocalProfileStore(path), "t1", 10));

    [Test]
    public void Profile_AddXp_RaisesLevel()
    {
        var s = Loaded();
        s.AddXpAsync(0).GetAwaiter().GetResult();
        s.AddXpAsync(-5).GetAwaiter().GetResult();
        Assert.AreEqual(0, s.Current.Xp);
        s.AddXpAsync(GameConfig.XpPerLevel).GetAwaiter().GetResult();
        Assert.AreEqual(GameConfig.XpPerLevel, s.Current.Xp);
        Assert.AreEqual(2, s.Current.Level);
        Assert.AreEqual(2, Loaded().Current.Level);
    }

    [Test]
    public void Profile_SetDisplayName_TrimsAndIgnoresTooShort()
    {
        var s = Loaded();
        s.SetDisplayNameAsync("  Osman ").GetAwaiter().GetResult();
        Assert.AreEqual("Osman", s.Current.DisplayName);
        s.SetDisplayNameAsync(" a ").GetAwaiter().GetResult();
        Assert.AreEqual("Osman", s.Current.DisplayName);
        Assert.AreEqual("Osman", Loaded().Current.DisplayName);
    }

    [Test]
    public void Profile_Delete_RemovesFileAndNextLoadIsFresh()
    {
        var s = Loaded();
        Apply(s, "t1", 500);
        s.DeleteAsync().GetAwaiter().GetResult();
        Assert.IsNull(s.Current);
        Assert.IsFalse(File.Exists(path));
        Assert.AreEqual(GameConfig.StarterCoins, Loaded().Coins);
    }

    // --- brief'in açık bıraktığı kararlar ---

    [Test]
    public void Profile_LoadSameUserTwice_NoWriteNoChanged()
    {
        var s = Loaded();
        var stamp = File.GetLastWriteTimeUtc(path);
        int fired = 0;
        s.Changed += _ => fired++;
        s.LoadOrCreateAsync("u1", "Baska").GetAwaiter().GetResult();
        Assert.AreEqual(0, fired);
        Assert.AreEqual("Oyuncu0001", s.Current.DisplayName);
        Assert.AreEqual(stamp, File.GetLastWriteTimeUtc(path));
    }

    [Test]
    public void Profile_FileOfOtherUser_ReplacedByFreshProfile()
    {
        Apply(Loaded(), "t1", 500);
        UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex(".*"));
        var s = new LocalProfileStore(path);
        s.LoadOrCreateAsync("u2", "Oyuncu0002").GetAwaiter().GetResult();
        Assert.AreEqual("u2", s.Current.UserId);
        Assert.AreEqual(GameConfig.StarterCoins, s.Coins);
    }

    [Test]
    public void Wallet_ZeroAmountOrEmptyId_ReturnsFalseAndChangesNothing()
    {
        var s = Loaded();
        int fired = 0;
        s.Changed += _ => fired++;
        Assert.IsFalse(Apply(s, "t1", 0));
        Assert.IsFalse(Apply(s, "", 10));
        Assert.IsFalse(Apply(s, null, 10));
        Assert.AreEqual(GameConfig.StarterCoins, s.Coins);
        Assert.AreEqual(0, fired);
    }

    [Test]
    public void Wallet_LedgerKeepsOnlyLast200Ids()
    {
        var s = Loaded();
        for (int i = 0; i < 205; i++) Assert.IsTrue(Apply(s, "t" + i, 1));
        long afterAll = s.Coins;
        var again = Loaded();
        Assert.IsTrue(Apply(again, "t204", 1), "son işlem hatırlanır");
        Assert.AreEqual(afterAll, again.Coins);
        Assert.IsTrue(Apply(again, "t0", 1), "200'den eski unutulur, yeniden uygulanır");
        Assert.AreEqual(afterAll + 1, again.Coins);
    }
}
