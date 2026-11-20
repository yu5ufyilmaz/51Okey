using System.Collections.Generic;

// Koltuğu kimin oynadığı: oda özelliği "Ctl", koltuk başına bir bit (bit seat-1 açık = insan). Saf: Photon çağrısı yok.
public static class CtlBits
{
    public const string Key = "Ctl";

    public static bool Has(int ctl, int seat) => seat >= 1 && seat <= 4 && (ctl & (1 << (seat - 1))) != 0;

    public static int Set(int ctl, int seat, bool on)
    {
        if (seat < 1 || seat > 4) return ctl;
        return on ? ctl | (1 << (seat - 1)) : ctl & ~(1 << (seat - 1));
    }

    public static int Of(IEnumerable<int> seats)
    {
        int ctl = 0;
        foreach (int s in seats) ctl = Set(ctl, s, true);
        return ctl;
    }

    // Başlangıç değeri: yalnız etkin (kopmamış) oyuncuların koltukları.
    public static int OfActive(IEnumerable<(int seat, bool inactive)> players)
    {
        int ctl = 0;
        foreach (var p in players) if (!p.inactive) ctl = Set(ctl, p.seat, true);
        return ctl;
    }

    public static bool[] BotNow(int ctl) => new[] { !Has(ctl, 1), !Has(ctl, 2), !Has(ctl, 3), !Has(ctl, 4) };

    public static int Read(ExitGames.Client.Photon.Hashtable props) =>
        props != null && props.TryGetValue(Key, out object v) && v is int i ? i : 0;
}

// İnsanın hamle yapabilmesi: kayıt canlı + koltuk insan kontrolünde + sıra onda.
public static class TurnGate
{
    // Bot çekme RPC'sini gönderdikten sonra iptal edilmez: 15 taşlı el sahipsiz kalır.
    public static bool MayCancelBot(bool drew) => !drew;

    public static bool MayAct(bool live, bool humanControl, bool myQueIsCurrent) => live && humanControl && myQueIsCurrent;
}
