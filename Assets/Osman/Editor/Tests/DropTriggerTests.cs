using NUnit.Framework;

public class DropTriggerTests
{
    static DropView V(bool myTurn = false, int hand = 14, bool opened = false, int seen = 2, bool master = false, bool ended = false) =>
        new DropView { myTurn = myTurn, handCount = hand, openedThisTurn = opened, ownTurnsSeen = seen, isMaster = master, handEnded = ended };

    [Test]
    public void Offturn()
    {
        Assert.IsTrue(DropTrigger.ShouldDrop("offturn", V()));
        Assert.IsFalse(DropTrigger.ShouldDrop("offturn", V(myTurn: true)));
        Assert.IsFalse(DropTrigger.ShouldDrop("offturn", V(seen: 1)));
    }

    [Test]
    public void Own_Turn_Points()
    {
        Assert.IsTrue(DropTrigger.ShouldDrop("before-draw", V(true, 14)));
        Assert.IsFalse(DropTrigger.ShouldDrop("before-draw", V(true, 15)));
        Assert.IsTrue(DropTrigger.ShouldDrop("after-draw", V(true, 15)));
        Assert.IsFalse(DropTrigger.ShouldDrop("after-draw", V(true, 15, opened: true)));
        Assert.IsTrue(DropTrigger.ShouldDrop("after-open", V(true, 12, opened: true)));
    }

    [Test]
    public void Master_HandEnd_Unknown()
    {
        Assert.IsTrue(DropTrigger.ShouldDrop("master", V(master: true)));
        Assert.IsFalse(DropTrigger.ShouldDrop("master", V()));
        Assert.IsTrue(DropTrigger.ShouldDrop("hand-end", V(ended: true)));
        Assert.IsFalse(DropTrigger.ShouldDrop("x", V()));
        Assert.IsTrue(DropTrigger.ShouldDrop("master-hand-end", V(master: true, ended: true)));
        Assert.IsFalse(DropTrigger.ShouldDrop("master-hand-end", V(ended: true)));
    }
}
