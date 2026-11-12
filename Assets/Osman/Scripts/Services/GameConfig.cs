public static class GameConfig
{
    public const long StarterCoins = 1000;
    public const long RewardedAdCoins = 50;
    public const int RewardedAdsPerDay = 5;
    public const int InterstitialMinSeconds = 180;
    public const int XpPerGame = 10;
    public const int XpWinBonus = 25;
    public const int XpPerLevel = 100;

    public static int LevelForXp(int xp) => 1 + xp / XpPerLevel;

    // string.GetHashCode çalışma zamanları arasında sabit değil; FNV-1a sabit.
    public static string DefaultName(string userId)
    {
        uint h = 2166136261;
        foreach (char c in userId ?? "") h = (h ^ c) * 16777619;
        return "Oyuncu" + (h % 10000).ToString("D4");
    }
}
