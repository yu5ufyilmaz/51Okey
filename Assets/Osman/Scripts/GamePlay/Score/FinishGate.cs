// El sonucu kapısı (master): aynı el için kazanç beraberliği ezer, tersi olmaz; aynı sonuç ikinci kez geçmez.
public static class FinishGate
{
    // winner -1 = beraberlik. true: sonucu hesapla/dağıt.
    public static bool Accept(int round, int winner, ref int doneRound, ref int doneWinner)
    {
        if (round == doneRound && (doneWinner != -1 || winner == -1))
            return false;
        doneRound = round;
        doneWinner = winner;
        return true;
    }

    // Yeni masa sahibi: bu elin sonucu (Hand{round}) zaten yazıldıysa kapı ondan başlar (kazanç sonrası ikinci sonuç geçmez).
    public static void Seed(int round, int[] handArray, ref int doneRound, ref int doneWinner)
    {
        var d = HandBreakdown.Decode(handArray);
        if (d == null)
            return;
        doneRound = round;
        doneWinner = d.winnerSeat > 0 ? d.winnerSeat : -1;
    }
}
