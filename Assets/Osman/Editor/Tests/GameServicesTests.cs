using System.IO;
using NUnit.Framework;
using UnityEngine;

// Not: bu testler ana editörün gerçek yerel profilini kullanır (yoksa oluşturur); bakiyeyi değiştirmez.
// Çıkış testleri sonunda misafir olarak geri girer, isim testi eski ismi geri yazar.
public class GameServicesTests
{
    [Test]
    public void InstanceTag_IsProjectFolderName() =>
        Assert.AreEqual(new DirectoryInfo(Application.dataPath).Parent.Name, GameServices.InstanceTag);

    [Test]
    public void LocalProfilePath_DiffersPerTag()
    {
        string a = GameServices.LocalProfilePath("51Okey"), b = GameServices.LocalProfilePath("51Okey_clone_0");
        Assert.AreNotEqual(a, b);
        StringAssert.EndsWith("local-profile-51Okey_clone_0.json", b);
        StringAssert.StartsWith(Application.persistentDataPath, a);
    }

    [Test]
    public void Init_Twice_ReturnsSameTask() =>
        Assert.AreSame(GameServices.InitAsync(), GameServices.InitAsync());

    [Test]
    public void Init_SignsInAndLoadsProfile()
    {
        GameServices.InitAsync().GetAwaiter().GetResult();
        Assert.AreEqual("local-" + GameServices.InstanceTag, GameServices.Auth.UserId);
        Assert.IsNotNull(GameServices.Profile.Current);
        Assert.AreEqual(GameServices.Profile.Current.Coins, GameServices.Wallet.Coins);
        Assert.AreSame(GameServices.Profile, GameServices.Wallet);
    }

    // --- giriş/lobi İP1: misafir girişi, çıkış, isim ---

    [TestCase(null, false)]
    [TestCase("", false)]
    [TestCase("  ", false)]
    [TestCase("a", false)]
    [TestCase(" a ", false)]
    [TestCase("ab", true)]
    [TestCase(" ab ", true)]
    [TestCase("abcdefghijklmnop", true)]
    [TestCase("abcdefghijklmnopq", false)]
    public void IsValidName(string name, bool valid) =>
        Assert.AreEqual(valid, GameConfig.IsValidName(name));

    [Test]
    public void EnterAsGuest_GivesSession()
    {
        GameServices.InitAsync().GetAwaiter().GetResult();
        GameServices.SignOutAsync().GetAwaiter().GetResult();
        try
        {
            Assert.IsFalse(GameServices.HasSession);
            GameServices.EnterAsGuestAsync().GetAwaiter().GetResult();
            Assert.IsTrue(GameServices.HasSession);
            Assert.AreEqual("local-" + GameServices.InstanceTag, GameServices.Auth.UserId);
            Assert.AreEqual(GameServices.Auth.UserId, GameServices.Profile.Current.UserId);
        }
        finally { GameServices.EnterAsGuestAsync().GetAwaiter().GetResult(); }
    }

    // Gözden geçirme odağı 1: Google başarısızsa misafir hesap açılmaz (yerel arka uçta Google hep Failed).
    [Test]
    public void EnterWithGoogle_Failed_OpensNoAccount()
    {
        GameServices.InitAsync().GetAwaiter().GetResult();
        GameServices.SignOutAsync().GetAwaiter().GetResult();
        try
        {
            Assert.AreEqual(LinkResult.Failed, GameServices.EnterWithGoogleAsync().GetAwaiter().GetResult());
            Assert.IsFalse(GameServices.HasSession);
            Assert.IsNull(GameServices.Auth.UserId);
        }
        finally { GameServices.EnterAsGuestAsync().GetAwaiter().GetResult(); }
    }

    [Test]
    public void SignOut_DropsSessionAndProfile()
    {
        GameServices.InitAsync().GetAwaiter().GetResult();
        long coins = GameServices.Wallet.Coins;
        PlayerProfile seen = GameServices.Profile.Current;
        System.Action<PlayerProfile> onChanged = p => seen = p;
        GameServices.Profile.Changed += onChanged;
        try
        {
            Assert.IsTrue(GameServices.CanSignOut, "yerel arka uçta misafir de çıkabilir");
            GameServices.SignOutAsync().GetAwaiter().GetResult();
            Assert.IsFalse(GameServices.HasSession);
            Assert.IsFalse(GameServices.CanSignOut);
            Assert.IsNull(GameServices.Auth.UserId);
            Assert.IsNull(GameServices.Profile.Current);
            Assert.IsNull(seen, "arayüz eski hesabın profilini göstermesin");
            Assert.AreEqual(0, GameServices.Wallet.Coins);
        }
        finally
        {
            GameServices.Profile.Changed -= onChanged;
            GameServices.EnterAsGuestAsync().GetAwaiter().GetResult();
        }
        Assert.IsTrue(GameServices.HasSession);
        Assert.AreEqual(coins, GameServices.Wallet.Coins, "yerel profil dosyası çıkışta silinmez");
    }

    // Yerel arka uç aynı karede biter: aynı Task beklenemez, iki çağrının tek hesap açtığı sınanır.
    [Test]
    public void EnterAsGuest_Twice_OneAccount()
    {
        GameServices.InitAsync().GetAwaiter().GetResult();
        GameServices.SignOutAsync().GetAwaiter().GetResult();
        int fired = 0;
        System.Action onUser = () => fired++;
        GameServices.Auth.UserChanged += onUser;
        try
        {
            var first = GameServices.EnterAsGuestAsync();
            var uid = GameServices.Auth.UserId;
            var second = GameServices.EnterAsGuestAsync();
            first.GetAwaiter().GetResult();
            second.GetAwaiter().GetResult();
            Assert.AreEqual(1, fired);
            Assert.AreEqual(uid, GameServices.Auth.UserId);
            Assert.IsTrue(GameServices.HasSession);
        }
        finally
        {
            GameServices.Auth.UserChanged -= onUser;
            GameServices.EnterAsGuestAsync().GetAwaiter().GetResult();
        }
    }

    [Test]
    public void NeedsName_TrueForDefaultName_FalseAfterRename()
    {
        GameServices.InitAsync().GetAwaiter().GetResult();
        var original = GameServices.Profile.Current.DisplayName;
        try
        {
            GameServices.Profile.SetDisplayNameAsync(GameConfig.DefaultName(GameServices.Auth.UserId)).GetAwaiter().GetResult();
            Assert.IsTrue(GameServices.NeedsName);
            GameServices.Profile.SetDisplayNameAsync("Testİsim").GetAwaiter().GetResult();
            Assert.IsFalse(GameServices.NeedsName);
        }
        finally { GameServices.Profile.SetDisplayNameAsync(original).GetAwaiter().GetResult(); }
        Assert.AreEqual(original, GameServices.Profile.Current.DisplayName);
    }

    [Test]
    public void NullAds_RewardedSucceedsAndInterstitialDoesNothing()
    {
        IAdsService ads = new NullAdsService();
        ads.InitializeAsync().GetAwaiter().GetResult();
        Assert.IsTrue(ads.RewardedReady);
        Assert.IsTrue(ads.ShowRewardedAsync("shop").GetAwaiter().GetResult());
        Assert.DoesNotThrow(() => ads.TryShowInterstitial("game_end"));
    }
}
