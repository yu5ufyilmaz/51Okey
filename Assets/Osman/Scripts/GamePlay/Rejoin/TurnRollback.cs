using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Sıra başında her istemcinin o koltuk için aldığı kayıt: tur ortasında kopan oyuncunun yarım turu buna döner.
public sealed class TurnStartRecord
{
    public int round, serial, que, discards, tableLimit; // discards: o koltuğun atık alanındaki TileUI sayısı
    public bool opened, openedPairs, doubleOpened;
    public List<Tiles> hand; // kopya, liste sırasıyla
    public List<List<Tiles>> melds;
    public List<List<Vector2Int>> positions;
    public bool hasOpenValue;
    public int openValue;
    public bool openValuePairs;
    public List<Tiles> drawn = new List<Tiles>(); // bu turda ortadan çekilenler, sırayla
    public Tiles sideTaken; // yandan alınıp iptal edilmemiş taş; yoksa null
    public List<Tiles> swapAdded = new List<Tiles>(); // bu turda joker takasıyla ele eklenen (elde başlamamış, çekilmemiş) taşlar
}

public enum RollbackKind { CloseTurn, Rollback }

public static class TurnRollback
{
    // Atış ulaşmış (atık sayısı arttı), NextTurn gelmemiş: geri alma yok, tur kapatılır.
    public static RollbackKind Decide(int discardsAtStart, int discardsNow) =>
        discardsNow > discardsAtStart ? RollbackKind.CloseTurn : RollbackKind.Rollback;

    // Elde başlamamış, ortadan/yandan alınmamış taş eline eklendiyse takasla gelmiştir (tahtadaki okey).
    public static bool IsSwapAdded(IReadOnlyList<Tiles> startHand, IReadOnlyList<Tiles> drawn, Tiles sideTaken, Tiles tile) =>
        startHand.All(t => t.id != tile.id) && drawn.All(t => t.id != tile.id) && (sideTaken == null || sideTaken.id != tile.id);

    // Oda özelliği (baraj / çift) güncellemesi tur başı kaydını yalnız sıra sahibi henüz hiçbir şey yapmadıysa tazeler:
    // sonraki değişiklik (açış) geri alınması gereken şeydir, kayda yazılmaz.
    public static bool MayRefreshLimits(int drawnCount, bool hasSideTaken, int handNow, int handAtStart) =>
        drawnCount == 0 && !hasSideTaken && handNow == handAtStart;

    // Tur başı eli sırasıyla + çekilenler; aynı id iki kez girmez. Yandan alınan taş kayıtta elde yoktur, dönmez.
    public static List<Tiles> HandAfterRollback(IReadOnlyList<Tiles> startHand, IReadOnlyList<Tiles> drawn)
    {
        var hand = startHand.ToList();
        foreach (var t in drawn)
            if (hand.All(h => h.id != t.id))
                hand.Add(t);
        return hand;
    }
}

public static class BotDrawRule
{
    // Geri alma sonrası el tur başı + çekilen olur (hâlâ 15'ten az olabilir): bu turda çekmişse bot bir daha çekmez.
    public static bool ShouldDraw(int handCount, bool alreadyDrewThisTurn) => !alreadyDrewThisTurn && handCount < 15;
}
