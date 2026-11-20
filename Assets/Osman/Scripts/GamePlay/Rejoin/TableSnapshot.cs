using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

public struct SnapDiscard { public int seat; public Tiles tile; }
public struct SnapBoardTile { public Tiles tile; public int meldType, slot; } // meldType = alanın child(0..2) sırası, slot = yuva sırası
public struct SnapOpenValue { public int que, value; public bool pairs; }
public struct SnapPenalty { public int seat, amount, reason; }

// Dönen oyuncunun masayı sıfırdan kurması için gereken her şey. Sıra başında (NextTurn sonrası) master'da alınır;
// o anda tur başı kaydı (TurnStartRecord) bundan kurulur: el = hands[sıra-1], meld/konum = melds/meldPositions[sıra-1],
// açık bayrakları/değer = opened/openedPairs/openValues, atık sayısı = discards'ta o koltuk, seri = turnSerial, çekilen/yandan/takas boş.
public sealed class TableSnapshot
{
    public const int Version = 1;
    public int round, turnSerial, currentTurn; public bool ready, ended;
    public double turnStartTime;               // meta: özete girmez
    public int devDeal, devMove;               // meta: DesyncDetector anahtarı, özete girmez
    public int[] seatOfQue = new int[5];       // [1..4]
    public Tiles indicator, dropTile;          // ikisi de null olabilir
    public List<Tiles> pile = new List<Tiles>();
    public List<Tiles>[] hands = New4<Tiles>();
    public List<List<Tiles>>[] melds = New4<List<Tiles>>();
    public List<List<Vector2Int>>[] meldPositions = New4<List<Vector2Int>>();
    public List<SnapDiscard> discards = new List<SnapDiscard>();
    public List<SnapBoardTile>[] boards = New4<SnapBoardTile>();
    public List<int> opened = new List<int>(), openedPairs = new List<int>();
    public List<SnapOpenValue> openValues = new List<SnapOpenValue>();
    public List<SnapPenalty> penalties = new List<SnapPenalty>();

    static List<T>[] New4<T>() => new[] { new List<T>(), new List<T>(), new List<T>(), new List<T>() };

    public static TableSnapshot Empty() => new TableSnapshot { ready = true };

    static string Key(Tiles t)
    {
        if (t == null) return "-";
        if (t.type == TileType.Number) return $"{"rbky"[(int)t.color]}{t.number}:{t.id}";
        return (t.type == TileType.Joker ? "J:" : "F:") + t.id;
    }

    static string Keys(IEnumerable<Tiles> l) => string.Join(",", l.Select(Key));

    public string Canonical(bool withMeta)
    {
        var sb = new StringBuilder();
        void L(string name, string v) => sb.Append(name).Append('=').Append(v).Append('\n');
        L("round", round.ToString()); L("serial", turnSerial.ToString()); L("turn", currentTurn.ToString());
        L("ready", ready ? "1" : "0"); L("ended", ended ? "1" : "0");
        if (withMeta)
        {
            L("startTime", turnStartTime.ToString("R", CultureInfo.InvariantCulture));
            L("devDeal", devDeal.ToString()); L("devMove", devMove.ToString());
        }
        L("seats", string.Join(",", seatOfQue.Skip(1)));
        L("ind", Key(indicator)); L("drop", Key(dropTile));
        L("pile", Keys(pile));
        for (int i = 0; i < 4; i++) L("hand" + (i + 1), Keys(hands[i]));
        for (int i = 0; i < 4; i++)
        {
            L("meld" + (i + 1), string.Join("|", melds[i].Select(Keys)));
            L("pos" + (i + 1), string.Join("|", meldPositions[i].Select(p => string.Join(",", p.Select(v => v.x + ":" + v.y)))));
        }
        L("discards", string.Join(",", discards.Select(d => d.seat + "/" + Key(d.tile))));
        for (int i = 0; i < 4; i++)
            L("board" + (i + 1), string.Join(",", boards[i].Select(b => $"{Key(b.tile)}@{b.meldType}.{b.slot}")));
        L("opened", string.Join(",", opened)); L("openedPairs", string.Join(",", openedPairs));
        L("openValues", string.Join(",", openValues.Select(o => $"{o.que}/{o.value}/{(o.pairs ? 1 : 0)}")));
        L("penalties", string.Join(",", penalties.Select(p => $"{p.seat}/{p.amount}/{p.reason}")));
        return sb.ToString();
    }
}
