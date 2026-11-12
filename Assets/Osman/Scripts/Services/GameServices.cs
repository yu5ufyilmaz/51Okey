using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public static class GameServices
{
    // Nesneler beklemeden kurulur: arayüz InitAsync bitmeden Profile.Changed'a abone olabilir.
    static readonly LocalProfileStore store;
    static Task init;

    public static IAuthService Auth { get; }
    public static IProfileStore Profile => store;
    public static IWallet Wallet => store;
    public static IAdsService Ads { get; }
    public static string InstanceTag { get; }

    static GameServices()
    {
#if UNITY_EDITOR
        // Aynı bilgisayardaki editör kopyaları persistentDataPath'i paylaşır; klasör adı dosyaları ayırır.
        InstanceTag = Path.GetFileName(Path.GetDirectoryName(Application.dataPath));
#else
        InstanceTag = "device";
#endif
        Auth = new LocalAuthService(InstanceTag);
        store = new LocalProfileStore(LocalProfilePath(InstanceTag));
        Ads = new NullAdsService();
    }

    public static string LocalProfilePath(string instanceTag) =>
        Path.Combine(Application.persistentDataPath, "local-profile-" + instanceTag + ".json");

    // Aynı karede iki çağrı gelebilir; hep aynı Task döner (hata olsa da yeniden denenmez).
    public static Task InitAsync() => init ??= RunInitAsync();

    static async Task RunInitAsync()
    {
        await Auth.SignInAsync();
        await store.LoadOrCreateAsync(Auth.UserId, GameConfig.DefaultName(Auth.UserId));
        await Ads.InitializeAsync();
        var p = store.Current;
        if (p != null)
            Debug.Log("[SERVICES] " + p.UserId + " coins=" + p.Coins);
    }
}
