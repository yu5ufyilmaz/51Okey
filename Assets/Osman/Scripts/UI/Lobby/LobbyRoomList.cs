using System;
using System.Collections.Generic;

// Photon oda listesi farklarını biriktirir; görünür odaları verir.
public class LobbyRoomList
{
    readonly Dictionary<string, LobbyRoom> rooms = new Dictionary<string, LobbyRoom>();

    public void Apply(IEnumerable<LobbyRoom> delta)
    {
        foreach (LobbyRoom r in delta)
        {
            if (r.Removed)
                rooms.Remove(r.Name);
            else
                rooms[r.Name] = r;
        }
    }

    public void Clear() => rooms.Clear();

    public List<LobbyRoom> Visible()
    {
        var list = new List<LobbyRoom>();
        foreach (LobbyRoom r in rooms.Values)
        {
            if (!r.IsOpen || r.PlayerCount >= r.MaxPlayers)
                continue;
            for (int s = 1; s <= 4; s++)
                if (r.SeatFree(s)) { list.Add(r); break; }
        }
        list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return list;
    }
}
