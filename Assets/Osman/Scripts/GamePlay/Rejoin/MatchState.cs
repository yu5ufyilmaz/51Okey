// Maç bitti mi: biten el son eldi. GameManager'ın "sonraki ele geç" kararı ile aynı koşul.
public static class MatchState
{
    public static bool IsOver(int currentRound, int totalRounds, bool handEnded) => handEnded && currentRound >= totalRounds;
}
