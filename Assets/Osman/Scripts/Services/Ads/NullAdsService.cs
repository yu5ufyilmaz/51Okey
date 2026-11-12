using System.Threading.Tasks;

// Reklam SDK'sı gelene kadar: ödüllü reklam hep başarılı, geçiş reklamı hiçbir şey yapmaz.
public class NullAdsService : IAdsService
{
    public Task InitializeAsync() => Task.CompletedTask;
    public bool RewardedReady => true;
    public Task<bool> ShowRewardedAsync(string placement) => Task.FromResult(true);
    public void TryShowInterstitial(string placement) { }
}
