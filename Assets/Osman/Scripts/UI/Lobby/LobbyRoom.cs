// Lobi listesindeki bir oda. Photon'dan bağımsız düz veri.
public class LobbyRoom
{
    public string Name;
    public int PlayerCount;
    public int MaxPlayers;
    public bool IsOpen;
    public bool Removed;
    public int Rounds;
    public string PasswordHash;
    public string[] Seats; // dört eleman, boş koltuk ""; null = dördü de boş

    public bool HasPassword => !string.IsNullOrEmpty(PasswordHash);

    public bool SeatFree(int seat)
    {
        if (seat < 1 || seat > 4)
            return false;
        return Seats == null || seat > Seats.Length || string.IsNullOrEmpty(Seats[seat - 1]);
    }
}
