using NUnit.Framework;

public class DropTriggerTests
{
    static DropView V(bool myTurn = false, int hand = 14, bool opened = false, int seen = 2, bool master = false, bool ended = false,
        bool live = true, bool other = true, float off = 0f, bool just = false, bool drew = false) =>
        new DropView { myTurn = myTurn, handCount = hand, openedThisTurn = opened, ownTurnsSeen = seen, isMaster = master, handEnded = ended,
            live = live, otherMoved = other, offSec = off, justDiscarded = just, drew = drew };

    [Test]
    public void Offturn_NeedsStableOffTurn()
    {
        Assert.IsTrue(DropTrigger.ShouldDrop("offturn", V()));
        Assert.IsTrue(DropTrigger.ShouldDrop("offturn", V(other: false, off: 1.6f)));
        Assert.IsFalse(DropTrigger.ShouldDrop("offturn", V(other: false, off: 0.2f)));
        Assert.IsFalse(DropTrigger.ShouldDrop("offturn", V(myTurn: true)));
        Assert.IsFalse(DropTrigger.ShouldDrop("offturn", V(seen: 1)));
        Assert.IsFalse(DropTrigger.ShouldDrop("offturn", V(live: false)), "el sonu ekranı sıra dışı sayılmaz");
        Assert.IsTrue(DropTrigger.ShouldDrop("two", V()));
    }

    [Test]
    public void AfterDiscard_OnlyInThatFrame()
    {
        Assert.IsTrue(DropTrigger.ShouldDrop("after-discard", V(other: false, just: true)));
        Assert.IsFalse(DropTrigger.ShouldDrop("after-discard", V()));
        Assert.IsFalse(DropTrigger.ShouldDrop("after-discard", V(seen: 1, just: true)));
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
    public void MasterTurn_NeedsMasterOnOwnTurnAfterDraw()
    {
        Assert.IsTrue(DropTrigger.ShouldDrop("master-turn", V(true, 15, master: true, drew: true)));
        Assert.IsTrue(DropTrigger.ShouldDrop("master-turn", V(true, 5, master: true, drew: true)), "açmış elde de çekmiş sayılır");
        Assert.IsFalse(DropTrigger.ShouldDrop("master-turn", V(true, 15, drew: true)));
        Assert.IsFalse(DropTrigger.ShouldDrop("master-turn", V(true, 14, master: true)));
        Assert.IsFalse(DropTrigger.ShouldDrop("master-turn", V(false, 15, master: true, drew: true)));
    }

    [Test]
    public void Master_HandEnd_Unknown()
    {
        Assert.IsTrue(DropTrigger.ShouldDrop("master", V(master: true)));
        Assert.IsFalse(DropTrigger.ShouldDrop("master", V()));
        Assert.IsFalse(DropTrigger.ShouldDrop("master", V(master: true, live: false)));
        Assert.IsTrue(DropTrigger.ShouldDrop("hand-end", V(ended: true)));
        Assert.IsFalse(DropTrigger.ShouldDrop("hand-end", V(ended: true, master: true)), "hand-end: yalnız master olmayan");
        Assert.IsFalse(DropTrigger.ShouldDrop("x", V()));
        Assert.IsTrue(DropTrigger.ShouldDrop("master-hand-end", V(master: true, ended: true)));
        Assert.IsFalse(DropTrigger.ShouldDrop("master-hand-end", V(ended: true)));
    }
}
