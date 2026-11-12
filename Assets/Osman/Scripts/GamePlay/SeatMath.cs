// Koltuk / sıra aritmetiği. Saf: UnityEngine ve Photon yok, EditMode testleri doğrudan çağırır.
public static class SeatMath
{
    // Hedef koltuğun, yerel oyuncuya göre masadaki yeri (0 = altta, yerel oyuncu).
    public static int RelativeIndex(int targetSeat, int localSeat) => (targetSeat - localSeat + 4) % 4;

    // RelativeIndex'in tersi: yerel koltuğa göre relativeIndex'teki koltuk (1..4).
    public static int SeatAtRelativeIndex(int relativeIndex, int localSeat) => (localSeat - 1 + relativeIndex) % 4 + 1;

    // seatOfQue[1..4] içinde koltuğun sırası; yoksa -1.
    public static int QueOfSeat(int[] seatOfQue, int seat)
    {
        if (seatOfQue == null || seat < 1 || seat > 4)
            return -1;
        for (int que = 1; que <= 4 && que < seatOfQue.Length; que++)
            if (seatOfQue[que] == seat)
                return que;
        return -1;
    }

    // Sıranın koltuğu; sıra ya da koltuk 1..4 dışındaysa (eşleme henüz gelmedi) -1.
    public static int MeldSeat(int[] seatOfQue, int que)
    {
        if (seatOfQue == null || que < 1 || que > 4 || que >= seatOfQue.Length)
            return -1;
        int seat = seatOfQue[que];
        return seat >= 1 && seat <= 4 ? seat : -1;
    }

    // Oyuncusuz koltuklar arasında koltuk sırasıyla 1'den başlayan bot numarası; koltukta oyuncu varsa 0.
    public static int BotNumber(bool[] seatHasPlayer, int seat)
    {
        if (seatHasPlayer[seat])
            return 0;
        int number = 0;
        for (int s = 1; s <= seat; s++)
            if (!seatHasPlayer[s])
                number++;
        return number;
    }
}
