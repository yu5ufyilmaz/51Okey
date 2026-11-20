using System.Linq;
using Photon.Pun;
using UnityEngine;

// Fotoğrafı sahneye kurar: taze dağıtım yolunun animasyonsuz, sessiz, coroutine'siz karşılığı. Canlı bayrağına dokunmaz
// (RestoreGate.GoLive'ı çağıran el sıkışma karar verir). Sıra, envanter §2'nin sıralama kısıtlarıdır.
public static class SnapshotApplier
{
    public static void Apply(TableSnapshot s)
    {
        var gm = GameManager.Instance;
        var td = gm.tileDistrubite;
        var tm = gm.turnManager;
        var sm = gm.scoreManager;
        int myQue = td.GetQueueNumberOfPlayer(PhotonNetwork.LocalPlayer);

        td.ClearTableVisuals(true);
        td.LoadData(s);
        SeatManager.Instance.UpdateSeatDisplay(); // "<ad> meld" adları: tahta taşları bunlar olmadan yerleşmez
        if (s.indicator != null)
            td.PlaceIndicator(s.indicator);
        td.PlaceRemainingTilesInMiddleContainer();
        // Atıklar zaman sırasıyla: sonraki yandan alma "son görseli" siler; dropTile ayrıca aynen (alınınca da kalır).
        foreach (var d in s.discards)
            td.InstatiateSideTiles(SeatMath.QueOfSeat(s.seatOfQue, d.seat), d.tile);
        td.dropTile = s.dropTile;
        for (int q = 1; q <= 4; q++)
            foreach (var b in s.boards[q - 1])
                td.PlaceBoardTile(s.seatOfQue[q], b.meldType, b.slot, b.tile);
        td.RebuildLocalRack();

        sm.RestoreOpenState(s, myQue);
        td.RecalculateAllAvailableSlots(); // açış bayrakları kurulduktan sonra: işlek yuva ışıkları yalnız açanlar için yanar
        gm.RestoreState(s.ready, s.ended, s.openValues);
        MatchScores.Instance?.RestoreCurrent(s.penalties.Select(p => new MatchScores.PenaltyEntry { seat = p.seat, amount = p.amount, reason = (PenaltyReason)p.reason }));
        tm.RestoreTurn(s.currentTurn, s.turnSerial, s.turnStartTime);
        DesyncDetector.AdoptKey(s.devDeal, s.devMove);
        td.CaptureTurnStart(s.currentTurn);

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateTileCount(td.allTiles.Count);
            UIManager.Instance.UpdatePlayerStats(sm.totalScore, sm.pairTotalScore, gm.CurrentTableLimit);
        }
    }
}
