using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public static class GameServices
{
    public const int InitTimeoutMs = 10000;
    public const string UseFirebaseInEditorPref = "Okey.UseFirebaseInEditor";

    // Nesneler beklemeden ve süreç boyunca bir kez kurulur: arayüz InitAsync bitmeden Profile.Changed'a abone olabilir.
    static readonly InitGate init;
    static bool adsStarted;

    public static IAuthService Auth { get; }
    public static IProfileStore Profile { get; }
    public static IWallet Wallet { get; }
    public static IAdsService Ads { get; }
    public static string InstanceTag { get; }
    public static bool UsesFirebase { get; }

    // "Bağlanılamadı, tekrar dene" paneli için: InitFailed paneli açar, düğme PhotonManager.ConnectWhenReadyAsync'i çağırır.
    public static bool IsReady => init.IsReady;
    public static event Action<Exception> InitFailed
    {
        add => init.Failed += value;
        remove => init.Failed -= value;
    }

    static GameServices()
    {
        bool pref = false, autoTest = false;
#if UNITY_EDITOR
        // Aynı bilgisayardaki editör kopyaları persistentDataPath'i paylaşır; klasör adı dosyaları ayırır.
        InstanceTag = Path.GetFileName(Path.GetDirectoryName(Application.dataPath));
        pref = UnityEditor.EditorPrefs.GetBool(UseFirebaseInEditorPref, false);
        autoTest = SyncAutoTestFiles.TryLoadConfig(Directory.GetCurrentDirectory(), out _);
#else
        InstanceTag = "device";
#endif
        UsesFirebase = ChooseFirebase(Application.isEditor, pref, Application.isPlaying, autoTest);
        if (UsesFirebase)
        {
            var boot = new FirebaseBoot(InstanceTag, Application.isEditor);
            Auth = new FirebaseAuthService(boot);
            var store = new FirestoreProfileStore(boot);
            Profile = store;
            Wallet = store;
        }
        else
        {
            Auth = new LocalAuthService(InstanceTag);
            var store = new LocalProfileStore(LocalProfilePath(InstanceTag));
            Profile = store;
            Wallet = store;
        }
        Ads = new NullAdsService();
        Auth.UserChanged += OnUserChanged;
        init = new InitGate(RunInitAsync, InitTimeoutMs);
    }

    // Statik kurucu Application/EditorPrefs okur: ilk dokunuş ana iş parçacığında, sahne yüklenmeden olsun.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void TouchOnMainThread() { }

    // Cihaz: Firebase. Editör: yerel; tercih açıksa ve oynanıyorsa Firebase (EditMode testleri hep yerel). Senkron testi hep yerel.
    public static bool ChooseFirebase(bool isEditor, bool editorPref, bool isPlaying, bool autoTest) =>
        !autoTest && (!isEditor || (editorPref && isPlaying));

    public static string LocalProfilePath(string instanceTag) =>
        Path.Combine(Application.persistentDataPath, "local-profile-" + instanceTag + ".json");

    // Hata ya da zaman aşımı önbelleğe alınmaz: sonraki çağrı yeniden dener. Reklam başlatması bunu bekletmez.
    public static Task InitAsync()
    {
        StartAds();
        return init.RunAsync();
    }

    static async Task RunInitAsync()
    {
        await Auth.SignInAsync();
        await Profile.LoadOrCreateAsync(Auth.UserId, GameConfig.DefaultName(Auth.UserId));
        if (Profile.Current == null)
            throw new InvalidOperationException("profil yüklenemedi");
        LogProfile();
    }

    static async void StartAds()
    {
        if (adsStarted)
            return;
        adsStarted = true;
        try { await Ads.InitializeAsync(); }
        catch (Exception e)
        {
            adsStarted = false;
            Debug.LogWarning("[SERVICES] reklam başlatılamadı: " + e.GetType().Name);
        }
    }

    // Hesap değişince (Google hesabına geçiş, silme sonrası yeni misafir) profil yeniden yüklenir; ilk giriş init'in işi.
    static async void OnUserChanged()
    {
        if (!init.IsReady || Auth.UserId == null)
            return;
        try
        {
            await Profile.LoadOrCreateAsync(Auth.UserId, GameConfig.DefaultName(Auth.UserId));
            LogProfile();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SERVICES] yeni hesabın profili yüklenemedi: " + e.GetType().Name + " " + e.Message);
        }
    }

    // Yalnızca kullanıcı kimliği loglanır; token ya da e-posta asla.
    static void LogProfile()
    {
        var p = Profile.Current;
        if (p != null)
            Debug.Log("[SERVICES] " + p.UserId + " coins=" + p.Coins + " backend=" + (UsesFirebase ? "firebase" : "local"));
    }
}
