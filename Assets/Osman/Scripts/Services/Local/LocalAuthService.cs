using System.Threading.Tasks;

public class LocalAuthService : IAuthService
{
    readonly string instanceTag;

    public string UserId { get; private set; }
    public bool IsGuest => true;

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
