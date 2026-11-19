// Profil yüklenince isim için ne yapılacağı. Karar saf kodda, tabloyla test edilir (EntryFlow ile aynı desen).
public static class NameFlow
{
    // None: dokunma. AssignGuest: misafire yeni Misafir-NNNNN. ClaimCurrent: Google hesabının bugünkü ismini kaydet.
    public enum Step { None, AssignGuest, ClaimCurrent }

    // Google hesabında Misafir- ismi ya da kaydedilemeyen isim kalırsa GameServices.NeedsName sorar.
    public static Step Next(bool isGuest, bool nameClaimed, string name)
    {
        bool guestName = GameConfig.IsGuestName(name);
        if (isGuest)
            return nameClaimed && guestName ? Step.None : Step.AssignGuest;
        return !nameClaimed && !guestName ? Step.ClaimCurrent : Step.None;
    }

    // Giriş ekranındaki isim paneli ve lobideki Profil aynı yazıyı gösterir. Dönen değer Loc.T ile çevrilecek anahtar; Ok boş.
    public static string ErrorText(NameResult result) =>
        result == NameResult.Taken ? "error.name_taken"
        : result == NameResult.Invalid ? "error.name_invalid"
        : result == NameResult.Failed ? "login.connection_failed"
        : "";
}
