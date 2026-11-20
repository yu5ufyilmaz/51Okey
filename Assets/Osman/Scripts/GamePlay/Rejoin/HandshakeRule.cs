using System.Collections.Generic;

public enum HandshakeDecision { Grant, Wait, Again }

// El sıkışma kararı (saf): özet eşit ve istemci hazırsa, sıra o koltukta değilse ve o koltuk için bot oynamıyorsa geri verilir.
public static class HandshakeRule
{
    public static HandshakeDecision Decide(uint? masterDigest, uint clientDigest, bool clientReady, bool seatIsCurrentTurn, bool botRunningForSeat)
    {
        // Masa sahibinde o anahtar yok (yeni masa sahibi, eski anahtar): fark değil, sonraki raporu bekle.
        if (masterDigest == null)
            return HandshakeDecision.Wait;
        if (masterDigest != clientDigest || !clientReady)
            return HandshakeDecision.Again;
        return seatIsCurrentTurn || botRunningForSeat ? HandshakeDecision.Wait : HandshakeDecision.Grant;
    }

    public static string OverlayKey(int consecutiveAgains) => consecutiveAgains < 5 ? "rejoin.joining_game" : "rejoin.retrying";

    // Geri vermedeki ceza listesi: masa sahibi kendi cezasını sunucu yankısıyla yazar, yerel liste ondan uzun olabilir.
    // Masa sahibinin listesi yerelin başıysa yerel kalır; değilse masa sahibininki geçerli.
    public static bool KeepLocalList<T>(IReadOnlyList<T> master, IReadOnlyList<T> local)
    {
        if (master.Count > local.Count)
            return false;
        var eq = EqualityComparer<T>.Default;
        for (int i = 0; i < master.Count; i++)
            if (!eq.Equals(master[i], local[i]))
                return false;
        return true;
    }
}

// Masa sahibinin defteri: fotoğraf bekleyenler (pending) ve fotoğrafı yollanıp özeti beklenenler (restoring).
public sealed class HandshakeBook
{
    readonly List<int> pending = new List<int>();
    readonly HashSet<int> restoring = new HashSet<int>();

    public void Request(int actor)
    {
        restoring.Remove(actor);
        if (!pending.Contains(actor))
            pending.Add(actor);
    }

    public IReadOnlyList<int> TakePending()
    {
        var taken = new List<int>(pending);
        pending.Clear();
        foreach (int a in taken)
            restoring.Add(a);
        return taken;
    }

    public bool IsRestoring(int actor) => pending.Contains(actor) || restoring.Contains(actor);

    public bool OnDecision(int actor, HandshakeDecision d)
    {
        if (!restoring.Contains(actor) || d == HandshakeDecision.Wait)
            return false;
        restoring.Remove(actor);
        if (d == HandshakeDecision.Again)
            pending.Add(actor);
        return true;
    }

    // Yeni el: istemci RPC_PrepareNextRound'da kendiliğinden canlı olur, fotoğraf gerekmez.
    public void PendingToRestoring()
    {
        foreach (int a in pending)
            restoring.Add(a);
        pending.Clear();
    }

    public void Forget(int actor)
    {
        pending.Remove(actor);
        restoring.Remove(actor);
    }
}
