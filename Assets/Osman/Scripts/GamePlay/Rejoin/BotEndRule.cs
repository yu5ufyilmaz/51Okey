// Botun atışından sonra el biter mi: masanın gördüğü yığın, bekleyen (henüz uygulanmamış) çekişi düşülerek.
public static class BotEndRule
{
    public static bool PileEmpty(int pileCount, bool drawPending) => pileCount - (drawPending ? 1 : 0) <= 0;
}
