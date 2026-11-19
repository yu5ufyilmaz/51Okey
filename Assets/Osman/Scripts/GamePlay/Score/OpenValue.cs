// Turun ilk gerçek açış puanı: sonradan eklenen yerleştirme değeri bozmaz.
public static class OpenValue
{
    public static int Pick(int current, int placedScore) => current == 0 ? placedScore : current;
}
