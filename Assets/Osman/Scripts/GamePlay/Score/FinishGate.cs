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
}
