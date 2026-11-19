public static class SeatNaming
{
    // Oyuncu ayrılınca adı yalnız oyun başladıysa kalır; oyun öncesi ayrılan iz bırakmaz
    public static void RememberLeaver(string[] remembered, int seat, string name, bool seatsLocked)
    {
        if (remembered == null || seat < 1 || seat > remembered.Length) return;
        remembered[seat - 1] = seatsLocked ? name : null;
    }

    // humanNames[i]: koltuk i+1'deki insan (hiç oturmadıysa null); botNow[i]: şu an bot mu oynuyor
    public static string[] Names(string[] humanNames, bool[] botNow)
    {
        var res = new string[4]; int bot = 0;
        for (int i = 0; i < 4; i++)
        {
            string h = humanNames != null && i < humanNames.Length ? humanNames[i] : null;
            if (string.IsNullOrWhiteSpace(h)) res[i] = "Bot " + (++bot);
            else res[i] = botNow != null && i < botNow.Length && botNow[i] ? h + " (bot)" : h;
        }
        return res;
    }
}
