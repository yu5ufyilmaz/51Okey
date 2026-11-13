using System;
using System.Threading.Tasks;
using Firebase.Auth;

// Açılış: kayıtlı Firebase kullanıcısı varsa o, yoksa anonim giriş. Açılışta Play Games çağrısı yok (tasarım 5.3).
public class FirebaseAuthService : IAuthService
{
    readonly FirebaseBoot boot;
    FirebaseUser user;

    public string UserId => user?.UserId;
    public bool IsGuest => user == null || user.IsAnonymous;
    public event Action UserChanged;

    public FirebaseAuthService(FirebaseBoot boot) => this.boot = boot;

    public async Task SignInAsync()
    {
        var auth = await boot.AuthAsync();
        SetUser(auth.CurrentUser ?? (await auth.SignInAnonymouslyAsync()).User);
    }

    // İP3 doldurur: PlayGamesPlatform ManuallyAuthenticate → RequestServerSideAccess → PlayGamesAuthProvider.GetCredential
    // → anonim kullanıcıya LinkWithCredentialAsync; hesap başka profile bağlıysa onayla ona geçiş, SetUser ile UserChanged.
    public Task<LinkResult> LinkGoogleAsync() => Task.FromResult(LinkResult.Failed);

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
