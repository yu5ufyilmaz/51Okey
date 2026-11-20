// NextTurn seri numarası: yalnız şimdikinden büyük olan kabul edilir (çift NextTurn bir turu atlatmasın).
public static class TurnSerialRule
{
    public static bool Accept(int current, int incoming) => incoming > current;
}
