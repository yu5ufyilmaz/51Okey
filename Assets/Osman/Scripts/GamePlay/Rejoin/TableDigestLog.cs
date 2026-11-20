using System;
using System.IO;
using Photon.Pun;
using UnityEngine;

// Tur sınırında masa özeti: master kendi özetlerini Ring'de tutar (dönenin özeti bununla kıyaslanır);
// geliştirme yapısında her istemci "[OZET] el.seri özet" satırını konsola ve editör başına dosyaya (yalnız Editor) yazar (Run-SyncTest dört dosyayı kıyaslar).
public static class TableDigestLog
{
    public static readonly DigestRing Ring = new DigestRing(16);
    public static TableSnapshot Last { get; private set; }
    public static uint LastDigest { get; private set; }
    public static string LogPath { get; private set; }
    static StreamWriter file;

    public static void AtTurnBoundary()
    {
        bool master = PhotonNetwork.IsMasterClient;
        if (!master && !Debug.isDebugBuild) return;
        TableSnapshot s;
        try { s = SnapshotBuilder.FromScene(); }
        catch (Exception e) { Debug.LogException(e); return; }
        if (s == null) return;
        Last = s;
        LastDigest = TableDigest.Of(s);
        if (master) Ring.Put(s.round, s.turnSerial, LastDigest);
        if (!Debug.isDebugBuild) return;
        // Dönen istemci koltuğu geri alana kadar ayrı etiket: Run-SyncTest yalnız [OZET] satırlarını kıyaslar.
        string line = $"{(RejoinHandshake.Restoring ? "[OZET-KUR]" : "[OZET]")} {s.round}.{s.turnSerial} {LastDigest:x8}";
        Debug.Log(line);
        // Dosya yalnız Editor'de (senkron test donanımı): telefondaki geliştirme yapısında dönüşsüz büyür.
        if (!Application.isEditor) return;
        try
        {
            if (file == null)
            {
                string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
                Directory.CreateDirectory(dir);
                LogPath = Path.Combine(dir, $"ozet_{DateTime.Now:yyyyMMdd_HHmmss}_actor{PhotonNetwork.LocalPlayer?.ActorNumber ?? 0}.log");
                file = new StreamWriter(LogPath) { AutoFlush = true };
            }
            // Bölüm bölüm döküm: fark çıkarsa hangi bölümün ayrıştığı dosyadan okunur (tab ile başlayan satırlar).
            file.WriteLine(line);
            foreach (string l in s.Canonical(false).TrimEnd((char)10).Split((char)10)) file.WriteLine((char)9 + l);
        }
        catch (IOException e) { Debug.LogWarning($"[OZET] dosyaya yazılamadı: {e.Message}"); }
    }

    // Yeni oda: eski odanın özetleri ve dosyası karışmasın.
    public static void NewRoom()
    {
        file?.Dispose();
        file = null;
        LogPath = null;
        Last = null;
        LastDigest = 0;
        Ring.Clear();
    }
}
