using System.IO;
using NUnit.Framework;
using UnityEngine;

// Not: bu testler ana editörün gerçek yerel profilini kullanır (yoksa oluşturur); bakiyeyi değiştirmez.
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
