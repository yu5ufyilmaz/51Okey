using NUnit.Framework;

public class LobbyRoomListTests
{
    static LobbyRoom R(string name, int players = 1, bool open = true, bool removed = false, params string[] seats) =>
        new LobbyRoom { Name = name, PlayerCount = players, MaxPlayers = 4, IsOpen = open, Removed = removed,
            Seats = seats.Length == 0 ? new[] { "A", "", "", "" } : seats };

    static string[] Names(LobbyRoomList l) => l.Visible().ConvertAll(r => r.Name).ToArray();

    [Test] public void DeltaKeepsUntouchedRooms()
    {
        var l = new LobbyRoomList();
        l.Apply(new[] { R("a"), R("b") });
        l.Apply(new[] { R("c") });
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, Names(l));
    }

    [Test] public void RemovedRoomDisappears_UnknownRemovalIsIgnored()
    {
        var l = new LobbyRoomList();
        l.Apply(new[] { R("a"), R("b") });
        l.Apply(new[] { R("a", removed: true), R("yok", removed: true) });
        CollectionAssert.AreEqual(new[] { "b" }, Names(l));
    }

    [Test] public void UpdateReplacesRoom()
    {
        var l = new LobbyRoomList();
        l.Apply(new[] { R("a", 1) });
        l.Apply(new[] { R("a", 3, true, false, "A", "B", "C", "") });
        Assert.AreEqual(3, l.Visible()[0].PlayerCount);
        Assert.AreEqual("B", l.Visible()[0].Seats[1]);
    }

    [Test] public void FullRoomIsHidden()
    {
        var l = new LobbyRoomList();
        l.Apply(new[] { R("dolu", 4, true, false, "A", "B", "C", ""), R("kapali", 2, false), R("oturulmus", 3, true, false, "A", "B", "C", "D") });
        Assert.IsEmpty(Names(l));
    }

    [Test] public void NullSeatsCountAsFourFree()
    {
        var l = new LobbyRoomList();
        l.Apply(new[] { new LobbyRoom { Name = "eski", PlayerCount = 1, MaxPlayers = 4, IsOpen = true, Seats = null } });
        Assert.AreEqual(1, l.Visible().Count);
        Assert.IsTrue(l.Visible()[0].SeatFree(1));
        Assert.IsFalse(l.Visible()[0].SeatFree(0));
        Assert.IsFalse(l.Visible()[0].SeatFree(5));
    }

    [Test] public void ShortSeatsArrayDoesNotThrow()
    {
        var l = new LobbyRoomList();
        l.Apply(new[] { R("kisa", 1, true, false, "A", "") });
        Assert.AreEqual(1, l.Visible().Count);
        Assert.IsTrue(l.Visible()[0].SeatFree(3));
    }

    [Test] public void ClearEmpties()
    {
        var l = new LobbyRoomList(); l.Apply(new[] { R("a") }); l.Clear();
        Assert.IsEmpty(Names(l));
    }
}
