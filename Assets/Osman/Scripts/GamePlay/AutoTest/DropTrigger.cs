// -DropRejoin senaryolarının "şimdi kop" kararı; saf mantık, EditMode'da sınanır.
public struct DropView
{
    public bool myTurn, isMaster, handEnded, openedThisTurn;
    public int handCount, ownTurnsSeen;
    public bool live; // el sürüyor: oyun hazır ve bitmemiş (el sonu ekranında false)
    public bool otherMoved; // son atışımdan beri başka koltuğun hamlesi görüldü
    public float offSec; // son atışımdan beri geçen süre
    public bool justDiscarded; // atış bu karede tamamlandı
    public bool drew; // bu turda çektim (canDrop): açınca el küçüldüğü için el sayısı bunu söylemez
}

public static class DropTrigger
{
    public const float StableSec = 1.5f;

    // Tek atışı çağıran tutar (ilk true'da bir kez).
    public static bool ShouldDrop(string scenario, DropView v)
    {
        bool offTurnStable = v.live && !v.myTurn && v.ownTurnsSeen >= 2 && (v.otherMoved || v.offSec >= StableSec);
        switch (scenario)
        {
            case "offturn":
            case "two":
                return offTurnStable;
            case "master":
                return v.isMaster && offTurnStable;
            case "after-discard": // atıştan hemen sonraki kare: atış/NextTurn sunucuya ulaşmamış olabilir
                return v.justDiscarded && !v.myTurn && v.ownTurnsSeen >= 2;
            case "before-draw":
                return v.myTurn && v.handCount == 14 && v.ownTurnsSeen >= 2;
            case "after-draw":
                return v.myTurn && v.handCount == 15 && !v.openedThisTurn && v.ownTurnsSeen >= 2;
            case "master-turn":
                return v.isMaster && v.myTurn && v.drew && v.ownTurnsSeen >= 2;
            case "after-open":
                return v.myTurn && v.openedThisTurn;
            case "hand-end":
                return !v.isMaster && v.handEnded;
            case "master-hand-end":
                return v.isMaster && v.handEnded;
            default:
                return false;
        }
    }
}
