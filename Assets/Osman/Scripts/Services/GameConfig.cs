public static class GameConfig
{
    public const long StarterCoins = 1000;
    public const long RewardedAdCoins = 50;
    public const int RewardedAdsPerDay = 5;
    public const int InterstitialMinSeconds = 180;
    public const int XpPerGame = 10;
    public const int XpWinBonus = 25;
    public const int XpPerLevel = 100;
    public const int NameMin = 2, NameMax = 16;
    public const string GuestNamePrefix = "Misafir-";

    public static int LevelForXp(int xp) => 1 + xp / XpPerLevel;

    // Oyuncunun seçebileceği isim. Kırpılmış uzunluk sayılır: " ab " geçerli, " a " değil.
    // '/' Firestore belge yolunu böler; "Misafir-" öneki otomatik misafir isimlerine ayrılmış.
    public static bool IsValidName(string name) =>
        IsStorableName(name) && !name.Trim().StartsWith(GuestNamePrefix, System.StringComparison.OrdinalIgnoreCase);

    // Depoların yazdığı isim: oyuncu ismi ya da otomatik misafir ismi.
    public static bool IsStorableName(string name)
    {
        int length = name?.Trim().Length ?? 0;
        return length >= NameMin && length <= NameMax && name.IndexOf('/') < 0;
    }

    public static string GuestName(int number) => GuestNamePrefix + ((number % 100000 + 100000) % 100000).ToString("D5");

    public static bool IsGuestName(string name)
    {
        if (name == null || name.Length != GuestNamePrefix.Length + 5 || !name.StartsWith(GuestNamePrefix, System.StringComparison.Ordinal))
            return false;
        for (int i = GuestNamePrefix.Length; i < name.Length; i++)
            if (name[i] < '0' || name[i] > '9')
                return false;
        return true;
    }
}
