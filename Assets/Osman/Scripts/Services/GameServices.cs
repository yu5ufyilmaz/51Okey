using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public static class GameServices
{
    // Editörde Firebase soğuk açılışı 9-12 sn ölçüldü (İP2, İP3): 10 sn sınırı ilk açılışta düşüyordu.
    public const int InitTimeoutMs = 20000;
    public const string UseFirebaseInEditorPref = "Okey.UseFirebaseInEditor";
    // Misafir ismi çakışırsa yeni rastgele numarayla en çok bu kadar denenir; olmazsa sonraki açılışta yeniden.
    public const int GuestNameAttempts = 5;

    // Nesneler beklemeden ve süreç boyunca bir kez kurulur: arayüz InitAsync bitmeden Profile.Changed'a abone olabilir.
    static readonly InitGate init;
    static bool adsStarted;
    // Profil yükleme tek yoldan: kullanıcı başına tek Task (başlangıç coini iki kez yazılmasın).
    static Task profileLoad = Task.CompletedTask;
    static string profileUser;
    // Süren giriş/çıkış: çift dokunuş yeni işlem başlatmaz, aynı Task'ı alır.
    static Task pending = Task.CompletedTask;
    static System.Reflection.MethodInfo pendingOp;
    // UnityEngine.Random yalnız ana iş parçacığında çalışır; EditMode testleri de buradan geçer.
    static readonly System.Random guestNumbers = new System.Random();

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
    // Misafire isim sorulmaz. Google hesabının ismi kayıtlı değilse ya da hâlâ Misafir- ise sorulur.
    public static bool NeedsName => CanChangeName
        && (!Profile.Current.NameClaimed || GameConfig.IsGuestName(Profile.Current.DisplayName));
    public static bool CanChangeName => HasSession && !Auth.IsGuest;
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

    // Oyuncunun seçtiği isim: misafir seçemez, Misafir- öneki ve '/' reddedilir.
    public static Task<NameResult> ChooseNameAsync(string name) =>
        CanChangeName && GameConfig.IsValidName(name) ? Profile.SetDisplayNameAsync(name) : Task.FromResult(NameResult.Invalid);

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
        // Geçiş yarıda kalınca misafir profili silinmiş olabilir ama yükleme kaydı "bitti" görünür: kayıt sıfırlanır, profil yeniden yüklenir.
        bool reload = Auth.UserId != null && !HasSession;
        if (reload && profileLoad.IsCompleted)
            ForgetProfileLoad();
        if (result == LinkResult.Linked || result == LinkResult.SwitchedToExistingAccount)
            await ProfileLoadAsync();
        else if (reload)
        {
            // Kurtarma yüklemesi düşerse asıl sonuç (iptal/hata) istisnaya dönüşmesin.
            try { await ProfileLoadAsync(); }
            catch (Exception)
            {
                Debug.LogWarning("[SERVICES] Google sonrası profil yeniden yüklenemedi: " + Auth.UserId);
            }
        }
        return result;
    }

    // Kimlik silinemezse hesap profilsiz kalır: profil hemen yeniden yüklenir (başlangıç profiliyle devam).
    public static Task<DeleteResult> DeleteAccountAsync() => Exclusive(DeleteAccountCoreAsync);

    static async Task<DeleteResult> DeleteAccountCoreAsync()
    {
        // Profil silindikten sonra sahipsiz kalan anonim kimlik veri taşımaz: misafirde bırakılması yeterli.
        var abandon = Auth.IsGuest ? (Func<Task>)Auth.AbandonGuestAsync : null;
        var result = await AccountDeletion.RunAsync(HasSession, Auth.NeedsRecentLoginToDelete,
            Limited(Profile.DeleteAsync), Limited(Auth.DeleteAccountAsync), abandon != null ? Limited(abandon) : null, DropProfile);
        if (result == DeleteResult.DataDeletedAccountRemains && Auth.UserId != null && !HasSession)
        {
            ForgetProfileLoad();
            try { await ProfileLoadAsync(); }
            catch (Exception)
            {
                Debug.LogWarning("[SERVICES] silme sonrası profil yeniden yüklenemedi: " + Auth.UserId);
            }
        }
        return result;
    }

    // Asılı kalan silme adımı (çevrimdışı Firestore yazımı) akışı kilitlemesin.
    static Func<Task> Limited(Func<Task> step) => () => AccountDeletion.WithTimeoutAsync(step(), AccountDeletion.StepTimeoutMs);

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
        ForgetProfileLoad();
        Profile.Clear();
    }

    static void ForgetProfileLoad()
    {
        profileUser = null;
        profileLoad = Task.CompletedTask;
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
        await Profile.LoadOrCreateAsync(uid, NewGuestName());
        if (Profile.Current?.UserId != uid)
            throw new InvalidOperationException("profil yüklenemedi");
        await EnsureNameAsync();
        LogProfile();
    }

    static string NewGuestName() => GameConfig.GuestName(guestNumbers.Next(100000));

    // İsim güvenceye alınır; Taken/Failed girişi engellemez (isim kayıtsız kalır, sonraki yüklemede yeniden denenir).
    static async Task EnsureNameAsync()
    {
        var profile = Profile.Current;
        var step = NameFlow.Next(Auth.IsGuest, profile.NameClaimed, profile.DisplayName);
        if (step == NameFlow.Step.ClaimCurrent)
        {
            await Profile.SetDisplayNameAsync(profile.DisplayName);
            return;
        }
        if (step != NameFlow.Step.AssignGuest)
            return;
        // Yeni profil zaten bir Misafir- adayıyla açıldı: önce o denenir.
        var name = GameConfig.IsGuestName(profile.DisplayName) ? profile.DisplayName : NewGuestName();
        for (int i = 0; i < GuestNameAttempts; i++, name = NewGuestName())
        {
            if (await Profile.SetDisplayNameAsync(name) != NameResult.Taken)
                return;
        }
        Debug.LogWarning("[SERVICES] misafir ismi alınamadı: " + Auth.UserId);
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
