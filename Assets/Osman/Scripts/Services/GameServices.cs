using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public static class GameServices
{
    // Editörde Firebase soğuk açılışı 9-12 sn ölçüldü (İP2, İP3): 10 sn sınırı ilk açılışta düşüyordu.
    public const int InitTimeoutMs = 20000;
    public const string UseFirebaseInEditorPref = "Okey.UseFirebaseInEditor";

    // Nesneler beklemeden ve süreç boyunca bir kez kurulur: arayüz InitAsync bitmeden Profile.Changed'a abone olabilir.
    static readonly InitGate init;
    static bool adsStarted;
    // Profil yükleme tek yoldan: kullanıcı başına tek Task (başlangıç coini iki kez yazılmasın).
    static Task profileLoad = Task.CompletedTask;
    static string profileUser;
    // Süren giriş/çıkış: çift dokunuş yeni işlem başlatmaz, aynı Task'ı alır.
    static Task pending = Task.CompletedTask;
    static System.Reflection.MethodInfo pendingOp;

    public static IAuthService Auth { get; }
    public static IProfileStore Profile { get; }
    public static IWallet Wallet { get; }
    public static IAdsService Ads { get; }
    public static string InstanceTag { get; }
    public static bool UsesFirebase { get; }

    // Google hesabı başka profile bağlıysa oyuncuya sorulur (MainMenu atar). Atanmamışsa misafir kalınır.
    public static Func<Task<bool>> ConfirmAccountSwitch { get; set; }

    // "Bağlanılamadı, tekrar dene" paneli için: InitFailed paneli açar, düğme PhotonManager.ConnectWhenReadyAsync'i çağırır.
    public static bool IsReady => init.IsReady;

    public static bool HasSession => Auth.UserId != null && Profile.Current?.UserId == Auth.UserId;
    // Ayrı "isim seçildi" alanı yok: varsayılan isim duruyorsa sorulur.
    public static bool NeedsName => HasSession && Profile.Current.DisplayName == GameConfig.DefaultName(Auth.UserId);
    // Firebase misafiri çıkarsa anonim hesaba geri dönemez; yerel kimlik sabit, kayıp yok.
    public static bool CanSignOut => HasSession && (!Auth.IsGuest || !UsesFirebase);
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
            Auth = new FirebaseAuthService(
                boot,
                () => Profile.DeleteAsync(),
                () => ConfirmAccountSwitch != null ? ConfirmAccountSwitch() : Task.FromResult(false));
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
    // Yalnız kayıtlı oturumu geri yükler; oturum yoksa hata değildir, hesap açılmaz (oyuncu giriş ekranında seçer).
    public static Task InitAsync()
    {
        StartAds();
        return init.RunAsync();
    }

    static async Task RunInitAsync()
    {
        if (await Auth.RestoreAsync())
            await ProfileLoadAsync();
    }

    // Oturum varsa yeniden giriş yapılmaz, yalnız profil beklenir.
    public static Task EnterAsGuestAsync() => Exclusive(EnterAsGuestCoreAsync);

    static async Task EnterAsGuestCoreAsync()
    {
        if (Auth.UserId == null)
            await Auth.SignInGuestAsync();
        await ProfileLoadAsync();
        if (!HasSession)
            throw new InvalidOperationException("misafir girişi tamamlanmadı");
    }

    // İptal ya da hata: oturum değişmez (giriş ekranında misafir açılmaz).
    public static Task<LinkResult> EnterWithGoogleAsync() => Exclusive(EnterWithGoogleCoreAsync);

    static async Task<LinkResult> EnterWithGoogleCoreAsync()
    {
        var result = await Auth.LinkGoogleAsync();
        if (result == LinkResult.Linked || result == LinkResult.SwitchedToExistingAccount)
            await ProfileLoadAsync();
        return result;
    }

    public static Task SignOutAsync() => Exclusive(SignOutCoreAsync);

    static async Task SignOutCoreAsync()
    {
        await Auth.SignOutAsync();
        // Kimlik zaten yoksa UserChanged gelmez: profil yine de düşer.
        if (Profile.Current != null)
            DropProfile();
    }

    // Aynı kimlikle yeniden girişte profil yeniden yüklensin: yükleme kaydı da sıfırlanır.
    static void DropProfile()
    {
        profileUser = null;
        profileLoad = Task.CompletedTask;
        Profile.Clear();
    }

    // Aynı işlem sürerken gelen çağrı onun Task'ını alır; başka bir işlem sürerken fırlatır (iki hesap işlemi iç içe koşmasın).
    static T Exclusive<T>(Func<T> start) where T : Task
    {
        if (!pending.IsCompleted)
        {
            if (pendingOp == start.Method)
                return (T)pending;
            throw new InvalidOperationException("başka bir hesap işlemi sürüyor");
        }
        var task = start();
        pending = task;
        pendingOp = start.Method;
        return task;
    }

    // Hata önbelleğe alınmaz: aynı kullanıcı için sonraki çağrı yeniden dener (yükleme kendi içinde tekrar güvenlidir).
    static Task ProfileLoadAsync()
    {
        var uid = Auth.UserId;
        if (uid == null)
            return Task.CompletedTask;
        if (profileUser != uid || profileLoad.IsFaulted || profileLoad.IsCanceled)
        {
            profileUser = uid;
            profileLoad = LoadProfileAsync(uid);
        }
        return profileLoad;
    }

    static async Task LoadProfileAsync(string uid)
    {
        await Profile.LoadOrCreateAsync(uid, GameConfig.DefaultName(uid));
        if (Profile.Current?.UserId != uid)
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

    // Hesap değişince (giriş, Google hesabına geçiş, çıkış) profil yüklenir ya da düşer; Init ve Enter* aynı Task'ı bekler.
    static async void OnUserChanged()
    {
        if (Auth.UserId == null)
        {
            DropProfile();
            return;
        }
        try { await ProfileLoadAsync(); }
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
