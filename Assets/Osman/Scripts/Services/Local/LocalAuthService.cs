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

    public Task SignInAsync()
    {
        UserId = "local-" + instanceTag;
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
