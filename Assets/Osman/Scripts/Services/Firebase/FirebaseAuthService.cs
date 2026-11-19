using System;
using System.Threading.Tasks;
using Firebase.Auth;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Firebase;
using Step = GoogleLinkFlow.Step;
#endif

// Açılış: kayıtlı Firebase kullanıcısı varsa o; yoksa oyuncu seçer (misafir ya da Google). Açılışta Google çağrısı yok:
// "Google ile oturum aç" (Credential Manager, GoogleSignIn.cs) yalnızca "Google ile gir" / "Google'a bağla" düğmesinde açılır.
public class FirebaseAuthService : IAuthService
{
    readonly FirebaseBoot boot;
    readonly Func<Task> deleteGuestProfile;
    readonly Func<Task<bool>> confirmSwitch;
    FirebaseUser user;

    public string UserId => user?.UserId;
    public bool IsGuest => user == null || user.IsAnonymous;
    public event Action UserChanged;

    // deleteGuestProfile: misafir profil belgesini siler (misafir kimliğiyle çağrılır). confirmSwitch: oyuncuya hesap geçişini sorar.
    public FirebaseAuthService(FirebaseBoot boot, Func<Task> deleteGuestProfile, Func<Task<bool>> confirmSwitch)
    {
        this.boot = boot;
        this.deleteGuestProfile = deleteGuestProfile;
        this.confirmSwitch = confirmSwitch;
    }

    public async Task<bool> RestoreAsync()
    {
        var auth = await boot.AuthAsync();
        if (auth.CurrentUser == null)
            return false;
        SetUser(auth.CurrentUser);
        return true;
    }

    public async Task SignInGuestAsync()
    {
        var auth = await boot.AuthAsync();
        SetUser(auth.CurrentUser ?? (await auth.SignInAnonymouslyAsync()).User);
    }

    // Anonim hesaba geri girilemez: misafirin çıkışı hesabı ve coinini kaybettirir.
    // auth.CurrentUser'a bakılır: RestoreAsync'ten önce de kayıtlı misafir korunur.
    public async Task SignOutAsync()
    {
        var auth = await boot.AuthAsync();
        if (auth.CurrentUser?.IsAnonymous == true)
            throw new InvalidOperationException("misafir hesaptan çıkılmaz");
        auth.SignOut();
        GoogleSignIn.SignOut(); // sonraki "Google ile gir" hesabı yeniden sorsun
        SetUser(null);
    }

    // Credential Manager hesap seçme penceresi → Google ID token → GoogleAuthProvider.GetCredential → anonim kullanıcıya
    // LinkWithCredentialAsync. Bağlıysa uid değişmez (UserChanged yok, IsGuest false olur); hesaba geçişte UserChanged tetiklenir.
    // Kullanıcı yoksa (giriş ekranından Google) misafir açılmaz: SignInWithCredentialAsync ile doğrudan Google kimliğiyle girilir.
    public Task<LinkResult> LinkGoogleAsync()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return LinkOnDeviceAsync();
#else
        // Editörde ve Android dışında Google girişi yok: beklenen durum, uyarı.
        Debug.LogWarning("[AUTH] Google girişi yalnızca Android cihazda çalışır");
        return Task.FromResult(LinkResult.Failed);
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    async Task<LinkResult> LinkOnDeviceAsync()
    {
        if (user != null && !IsGuest)
            return LinkResult.Linked;
        // Pencere akış başına bir kez açılır: token akış boyunca saklanır, geçişte yeniden sorulmaz.
        string idToken = null;
        Credential updated = null;
        try
        {
            var result = await GoogleLinkFlow.RunAsync(
                async () =>
                {
                    var (step, token) = await GoogleSignIn.SignInAsync();
                    idToken = token;
                    return step;
                },
                async () =>
                {
                    var (step, credential) = await LinkAsync(idToken);
                    updated = credential;
                    return step;
                },
                confirmSwitch,
                () => SwitchToGoogleAccountAsync(updated != null && updated.IsValid() ? updated : GoogleAuthProvider.GetCredential(idToken, null)));
            Debug.Log("[AUTH] Google: " + result + " uid=" + UserId);
            return result;
        }
        catch (Exception e)
        {
            // İleti loglanmaz: e-posta içerebilir.
            Debug.LogWarning("[AUTH] Google'a bağlanılamadı: " + e.GetType().Name);
            return LinkResult.Failed;
        }
    }

    // InUse'da Firebase'in verdiği güncel kimlik bilgisi (varsa) geçiş için döner.
    async Task<(Step, Credential)> LinkAsync(string idToken)
    {
        var auth = await boot.AuthAsync();
        if (auth.CurrentUser == null)
        {
            SetUser(await auth.SignInWithCredentialAsync(GoogleAuthProvider.GetCredential(idToken, null)));
            return (Step.Ok, null);
        }
        try { await auth.CurrentUser.LinkWithCredentialAsync(GoogleAuthProvider.GetCredential(idToken, null)); }
        catch (Exception e) when (IsCredentialInUse(e)) { return (Step.InUse, UpdatedCredential(e)); }
        SetUser(auth.CurrentUser);
        return (Step.Ok, null);
    }

    // Sıra: misafir belge (kurallar yalnız kendi belgeni sildirir) → misafir Auth kullanıcısı → Google hesabıyla giriş.
    async Task<Step> SwitchToGoogleAccountAsync(Credential credential)
    {
        var auth = await boot.AuthAsync();
        await deleteGuestProfile();
        try
        {
            await auth.CurrentUser.DeleteAsync();
            SetUser(await auth.SignInWithCredentialAsync(credential));
            return Step.Ok;
        }
        catch
        {
            // shortcut: misafir Auth silinemezse misafir profilsiz kalır (uygulama yeniden açılınca profil yeniden oluşur).
            // Misafir silindi ama giriş olmadıysa oyun hesapsız kalmasın: yeni misafir.
            if (auth.CurrentUser == null)
                SetUser((await auth.SignInAnonymouslyAsync()).User);
            throw;
        }
    }

    static Credential UpdatedCredential(Exception e)
    {
        for (var x = e; x != null; x = x.InnerException)
            if (x is FirebaseAccountLinkException link)
                return link.UserInfo?.UpdatedCredential;
        return null;
    }

    // Firebase görevleri istisnayı AggregateException içinde verebilir.
    static bool IsCredentialInUse(Exception e)
    {
        for (var x = e; x != null; x = x.InnerException)
        {
            if (x is FirebaseAccountLinkException)
                return true;
            if (x is FirebaseException f && (AuthError)f.ErrorCode == AuthError.CredentialAlreadyInUse)
                return true;
        }
        return false;
    }
#endif

    public async Task DeleteAccountAsync()
    {
        var auth = await boot.AuthAsync();
        if (auth.CurrentUser != null)
            await auth.CurrentUser.DeleteAsync();
        SetUser(null);
    }

    // Firebase silmeyi son girişten 5 dk içinde kabul eder; 4 dk ile pay bırakılır. LastSignInTimestamp UTC.
    public bool NeedsRecentLoginToDelete =>
        user != null && !user.IsAnonymous && DateTime.UtcNow - (user.Metadata?.LastSignInTimestamp ?? DateTime.MinValue) > TimeSpan.FromMinutes(4);

    // SignOutAsync misafiri reddeder; bu yalnız hesap silme içindir (profil zaten silinmiştir).
    public async Task AbandonGuestAsync()
    {
        var auth = await boot.AuthAsync();
        if (auth.CurrentUser != null && !auth.CurrentUser.IsAnonymous)
            throw new InvalidOperationException("yalnız misafir kimliği bırakılır");
        auth.SignOut();
        SetUser(null);
    }

    // shortcut: oturumun Firebase tarafında düşmesi (token iptali) izlenmiyor; gerekirse auth.StateChanged dinlenir.
    void SetUser(FirebaseUser next)
    {
        var before = UserId;
        user = next;
        if (before != UserId)
            UserChanged?.Invoke();
    }
}
