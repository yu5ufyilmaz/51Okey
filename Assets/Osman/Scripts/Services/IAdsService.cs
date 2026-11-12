public interface IAdsService
{
    System.Threading.Tasks.Task InitializeAsync();
    bool RewardedReady { get; }
    System.Threading.Tasks.Task<bool> ShowRewardedAsync(string placement);
    void TryShowInterstitial(string placement);
}
