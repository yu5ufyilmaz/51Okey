using System;
using System.Threading.Tasks;

public class LocalAuthService : IAuthService
{
    readonly string instanceTag;
    string userId;

    public string UserId
    {
        get => userId;
        private set
        {
            if (userId == value)
                return;
            userId = value;
            UserChanged?.Invoke();
        }
    }
    public bool IsGuest => true;
    public event Action UserChanged;

    public LocalAuthService(string instanceTag) => this.instanceTag = instanceTag;

    // Yerel kimlik sabittir: kayıtlı oturum hep vardır, Editör doğrudan lobiye düşer.
    public Task<bool> RestoreAsync()
    {
        UserId = "local-" + instanceTag;
        return Task.FromResult(true);
    }

    public Task SignInGuestAsync()
    {
        UserId = "local-" + instanceTag;
        return Task.CompletedTask;
    }

    // Profil dosyası silinmez: yeniden girişte aynı kimlik aynı profili bulur.
    public Task SignOutAsync()
    {
        UserId = null;
        return Task.CompletedTask;
    }

    // Yerelde bağlanacak hesap yok.
    public Task<LinkResult> LinkGoogleAsync() => Task.FromResult(LinkResult.Failed);

    public Task DeleteAccountAsync()
    {
        UserId = null;
        return Task.CompletedTask;
    }
}
