using System;
using System.Collections.Generic;
using UnityEngine;

public static class RestoreQueue
{
    // nextTurnSerials[i] = i. kuyruk öğesi NextTurn ise serisi, değilse -1. Seri K'li NextTurn'den sonraki ilk indeks; K yoksa 0.
    public static int FirstToReplay(IReadOnlyList<int> nextTurnSerials, int snapshotSerial)
    {
        for (int i = 0; i < nextTurnSerials.Count; i++)
            if (nextTurnSerials[i] == snapshotSerial)
                return i + 1;
        return 0;
    }
}

// Dönen istemci fotoğrafı kurana kadar "canlı değil": oyun RPC işleyicileri atmaz, sıraya koyar.
// Canlı istemcide (her normal oyun) Defer her zaman false döner ve hiçbir şey yapmaz.
public static class RestoreGate
{
    static readonly List<(Action replay, int serial)> queue = new List<(Action, int)>();

    public static bool Live { get; private set; } = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset()
    {
        Live = true;
        queue.Clear();
    }

    public static void Enter()
    {
        Live = false;
        queue.Clear();
    }

    public static bool Defer(Action replay, int nextTurnSerial = -1)
    {
        if (Live)
            return false;
        queue.Add((replay, nextTurnSerial));
        return true;
    }

    // Fotoğraf seri K'da alındı: K'li NextTurn dahil öncesi fotoğrafta zaten var, atılır; sonrası sırayla oynatılır.
    public static int GoLive(int snapshotSerial)
    {
        var items = new List<(Action replay, int serial)>(queue);
        queue.Clear();
        Live = true; // oynatılan işleyiciler kapıdan geçmesin
        int from = RestoreQueue.FirstToReplay(items.ConvertAll(i => i.serial), snapshotSerial);
        for (int i = from; i < items.Count; i++)
            try { items[i].replay(); }
            catch (Exception e) { Debug.LogException(e); }
        return items.Count - from;
    }

    // Kurulum sırasında yeni el başladı: eski elin kuyruğu geçersiz, yeni hat tamamen işlenir.
    public static void GoLiveFromNewHand()
    {
        queue.Clear();
        Live = true;
    }
}
