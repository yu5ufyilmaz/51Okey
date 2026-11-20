using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;

// Canlı sahneden TableSnapshot kurar. Yalnız sıra başında (NextTurn sonrası) tam anlamlıdır; başka anda çökmez, eksikse null döner.
public static class SnapshotBuilder
{
    // Taş nesneleri sonradan değişebilir (okey değeri); fotoğraf kendi kopyasını tutar.
    static Tiles Copy(Tiles t) => t == null ? null : new Tiles { id = t.id, type = t.type, color = t.color, number = t.number };
    static List<Tiles> Copy(IEnumerable<Tiles> l) => l.Select(Copy).ToList();

    public static TableSnapshot FromScene()
    {
        var gm = GameManager.Instance;
        var td = gm != null ? gm.tileDistrubite : null;
        var tm = gm != null ? gm.turnManager : null;
        var sm = SeatManager.Instance;
        if (td == null || tm == null || sm == null || gm.scoreManager == null)
        {
            Debug.LogWarning("[OZET] fotoğraf kurulamadı: masa bileşenleri eksik");
            return null;
        }
        if (td.seatOfQue == null || td.seatOfQue.Length < 5 || Enumerable.Range(1, 4).Any(q => td.seatOfQue[q] < 1 || td.seatOfQue[q] > 4))
        {
            Debug.LogWarning("[OZET] fotoğraf kurulamadı: sıra-koltuk eşlemesi eksik");
            return null;
        }

        var s = new TableSnapshot
        {
            round = PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("CurrentRound", out object cr) && cr is int c ? c : 0,
            turnSerial = tm.TurnSerial,
            currentTurn = tm.currentTurnPlayer,
            ready = gm.isGameReady,
            ended = gm.isGameEnded,
            turnStartTime = tm.TurnStartTime,
            devDeal = DesyncDetector.CurrentDeal,
            devMove = DesyncDetector.CurrentMove,
            indicator = Copy(td.GetIndicatorTile()),
            dropTile = Copy(td.dropTile),
            pile = Copy(td.allTiles),
        };
        for (int q = 1; q <= 4; q++)
        {
            s.seatOfQue[q] = td.seatOfQue[q];
            s.hands[q - 1] = Copy(td.GetPlayerTilesForBot(q));
            s.melds[q - 1] = td.MeldsOfQue(q).Select(Copy).ToList();
            s.meldPositions[q - 1] = td.PositionsOfQue(q).Select(p => new List<Vector2Int>(p)).ToList();
            // Tahta yalnız sahnede: yuva sırası, okeyin durduğu yuva, işlenmiş taşlar. Hiyerarşi sırası yerine (tür, yuva) sırası: istemciden bağımsız.
            Transform area = sm.MeldAreaOfSeat(td.seatOfQue[q]);
            if (area != null)
                s.boards[q - 1] = area.GetComponentsInChildren<TileUI>()
                    .Where(t => t.tileDataInfo != null && t.transform.parent != null && t.transform.parent.parent != null)
                    .Select(t => new SnapBoardTile { tile = Copy(t.tileDataInfo), meldType = t.transform.parent.parent.GetSiblingIndex(), slot = t.transform.parent.GetSiblingIndex() })
                    .OrderBy(b => b.meldType).ThenBy(b => b.slot).ToList();
        }
        s.discards = td.DiscardHistory().Select(d => new SnapDiscard { seat = d.seat, tile = Copy(d.tile) }).ToList();
        foreach (int q in gm.scoreManager.playersWhoOpened.OrderBy(x => x)) s.opened.Add(q);
        foreach (int q in gm.scoreManager.playersWhoOpenedPairs.OrderBy(x => x)) s.openedPairs.Add(q);
        foreach (var kv in gm.OpenValues.OrderBy(k => k.Key)) s.openValues.Add(new SnapOpenValue { que = kv.Key, value = kv.Value.value, pairs = kv.Value.pairs });
        if (MatchScores.Instance != null)
            foreach (var p in MatchScores.Instance.CurrentHandPenalties) s.penalties.Add(new SnapPenalty { seat = p.seat, amount = p.amount, reason = (int)p.reason });
        return s;
    }
}
