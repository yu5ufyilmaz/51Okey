// -DropRejoin senaryolarının "şimdi kop" kararı; saf mantık, EditMode'da sınanır.
public struct DropView
{
    public bool myTurn, isMaster, handEnded, openedThisTurn;
    public int handCount, ownTurnsSeen;
}

public static class DropTrigger
{
    // Tek atışı çağıran tutar (ilk true'da bir kez).
    public static bool ShouldDrop(string scenario, DropView v)
    {
        switch (scenario)
        {
            case "offturn":
            case "two":
                return !v.myTurn && v.ownTurnsSeen >= 2;
            case "master":
                return v.isMaster && !v.myTurn && v.ownTurnsSeen >= 2;
            case "before-draw":
                return v.myTurn && v.handCount == 14 && v.ownTurnsSeen >= 2;
            case "after-draw":
                return v.myTurn && v.handCount == 15 && !v.openedThisTurn && v.ownTurnsSeen >= 2;
            case "after-open":
                return v.myTurn && v.openedThisTurn;
            case "hand-end":
                return v.handEnded;
            case "master-hand-end":
                return v.isMaster && v.handEnded;
            default:
                return false;
        }
    }
}
