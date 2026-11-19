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

    // Adresler Osman'dan gelecek; boşken ilgili satırlar gizlenir.
    public const string PrivacyUrl = "";
    public const string TermsUrl = "";
    public const string AccountDeletionUrl = "";
    public const string SupportEmail = "";

    // Length ile bakılır: sabit "" ile == karşılaştırması erişilemeyen kod uyarısı verir
    public static bool HasLegalLinks => PrivacyUrl.Length > 0 && TermsUrl.Length > 0;

    public static string LegalUrl(string linkId) => linkId == "privacy" ? PrivacyUrl : linkId == "terms" ? TermsUrl : "";

    public static int LevelForXp(int xp) => 1 + xp / XpPerLevel;

    // Oyuncunun seçebileceği isim. Kırpılmış uzunluk sayılır: " ab " geçerli, " a " değil.
    // '/' Firestore belge yolunu böler; "Misafir-" öneki otomatik misafir isimlerine ayrılmış.
    public static bool IsValidName(string name) =>
        IsStorableName(name) && !name.Trim().StartsWith(GuestNamePrefix, System.StringComparison.OrdinalIgnoreCase);

    // Depoların yazdığı isim: oyuncu ismi ya da otomatik misafir ismi. Firestore belge kimliği olamayanlar (".", "..", "__x__")
    // ve görünmez karakterler reddedilir (kontrol: NUL yerel SDK'da kesebilir; biçim: sıfır genişlikli boşlukla benzer isim).
    public static bool IsStorableName(string name)
    {
        var trimmed = name?.Trim();
        int length = trimmed?.Length ?? 0;
        if (length < NameMin || length > NameMax || trimmed == ".." || name.IndexOf('/') >= 0)
            return false;
        if (length >= 4 && trimmed.StartsWith("__", System.StringComparison.Ordinal) && trimmed.EndsWith("__", System.StringComparison.Ordinal))
            return false;
        foreach (char c in trimmed)
            if (char.IsControl(c) || System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format)
                return false;
        return true;
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
