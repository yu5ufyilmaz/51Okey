using System;
using System.Threading.Tasks;
using Firebase.Auth;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Firebase;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using Step = GoogleLinkFlow.Step;
#endif

// Açılış: kayıtlı Firebase kullanıcısı varsa o; yoksa oyuncu seçer (misafir ya da Google). Açılışta Play Games çağrısı yok (tasarım 5.3):
// PlayGamesPlatform yalnızca "Google ile gir" düğmesinde kullanılır, Activate/Authenticate çağrılmaz.
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
    public async Task SignOutAsync()
    {
        if (user != null && user.IsAnonymous)
            throw new InvalidOperationException("misafir hesaptan çıkılmaz");
        var auth = await boot.AuthAsync();
        auth.SignOut();
        SetUser(null);
    }

    // Play Games v2 ManuallyAuthenticate → RequestServerSideAccess → PlayGamesAuthProvider.GetCredential → anonim kullanıcıya
    // LinkWithCredentialAsync. Bağlıysa uid değişmez (UserChanged yok, IsGuest false olur); hesaba geçişte UserChanged tetiklenir.
    // Kullanıcı yoksa (giriş ekranından Google) misafir açılmaz: SignInWithCredentialAsync ile doğrudan Google kimliğiyle girilir.
    public Task<LinkResult> LinkGoogleAsync()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return LinkOnDeviceAsync();
#else
        // Editörde ve Android dışında Play Games yok: beklenen durum, uyarı.
        Debug.LogWarning("[AUTH] Google girişi yalnızca Android cihazda çalışır");
        return Task.FromResult(LinkResult.Failed);
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    async Task<LinkResult> LinkOnDeviceAsync()
    {
        if (user != null && !IsGuest)
            return LinkResult.Linked;
        string code = null;
        try
        {
            var result = await GoogleLinkFlow.RunAsync(
                async () =>
                {
                    var signedIn = await PlayGamesSignInAsync();
                    if (signedIn != Step.Ok)
                        return signedIn;
                    code = await ServerAuthCodeAsync();
                    return code == null ? Step.Failed : Step.Ok;
                },
                () => LinkAsync(code),
                confirmSwitch,
                SwitchToGoogleAccountAsync);
            Debug.Log("[AUTH] Google: " + result + " uid=" + UserId);
            return result;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[AUTH] Google'a bağlanılamadı: " + e.GetType().Name + " " + e.Message);
            return LinkResult.Failed;
        }
    }

    static Task<Step> PlayGamesSignInAsync()
    {
        if (PlayGamesPlatform.Instance.IsAuthenticated())
            return Task.FromResult(Step.Ok);
        var done = new TaskCompletionSource<Step>();
        PlayGamesPlatform.Instance.ManuallyAuthenticate(status => done.TrySetResult(
            status == SignInStatus.Success ? Step.Ok : status == SignInStatus.Canceled ? Step.Cancelled : Step.Failed));
        return done.Task;
    }

    // Kod tek kullanımlık: her Firebase denemesi için yenisi istenir. Kod loglanmaz.
    static Task<string> ServerAuthCodeAsync()
    {
        var done = new TaskCompletionSource<string>();
        PlayGamesPlatform.Instance.RequestServerSideAccess(false, c => done.TrySetResult(string.IsNullOrEmpty(c) ? null : c));
        return done.Task;
    }

    async Task<Step> LinkAsync(string code)
    {
        var auth = await boot.AuthAsync();
        if (auth.CurrentUser == null)
        {
            SetUser(await auth.SignInWithCredentialAsync(PlayGamesAuthProvider.GetCredential(code)));
            return Step.Ok;
        }
        try { await auth.CurrentUser.LinkWithCredentialAsync(PlayGamesAuthProvider.GetCredential(code)); }
        catch (Exception e) when (IsCredentialInUse(e)) { return Step.InUse; }
        SetUser(auth.CurrentUser);
        return Step.Ok;
    }

    // Sıra: misafir belge (kurallar yalnız kendi belgeni sildirir) → misafir Auth kullanıcısı → Google hesabıyla giriş.
    async Task<Step> SwitchToGoogleAccountAsync()
    {
        var code = await ServerAuthCodeAsync();
        if (code == null)
            return Step.Failed;
        var auth = await boot.AuthAsync();
        await deleteGuestProfile();
        try
        {
            await auth.CurrentUser.DeleteAsync();
            SetUser(await auth.SignInWithCredentialAsync(PlayGamesAuthProvider.GetCredential(code)));
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

    // shortcut: oturumun Firebase tarafında düşmesi (token iptali) izlenmiyor; gerekirse auth.StateChanged dinlenir.
    void SetUser(FirebaseUser next)
    {
        var before = UserId;
        user = next;
        if (before != UserId)
            UserChanged?.Invoke();
    }
}
