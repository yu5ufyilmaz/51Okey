using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PenaltySystem : MonoBehaviourPunCallbacks
{
    // Singleton yapısı (Erişimi kolaylaştırmak için)
    public static PenaltySystem Instance;

    // --- VERİ YAPILARI ---
    [System.Serializable]
    public struct PendingPenaltyInfo // struct yerine class
    {
        public int victimQue;
        public int penaltyAmount;
        public string reason;
    }

    // Bu tur içinde birikmiş ama henüz kesinleşmemiş (Commit edilmemiş) cezalar
    // Geri Al (Undo) yapılabilmesi için burada tutuyoruz.
    public List<PendingPenaltyInfo> currentTurnPenalties = new List<PendingPenaltyInfo>();

    private void Awake()
    {
        // Singleton Pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // =================================================================================
    // BÖLÜM 1: CEZA HESAPLAMA MANTIĞI (Math & Rules)
    // =================================================================================

    // Kural kitabı: kazananın düşeri renge göre 100 × çarpan (sarı 600), bitiş türü değiştirmez. Açmayan sabit 600.
    // Açan: kalan taş başına 10 × renk çarpanı (çift açtıysa 2 kat), elde kalan her okey ayrıca 250.
    // Özel bitişte kaybedenlerin cezası finishMultiplier ile çarpılır.
    public static int EndPenalty(
        bool isWinner,
        bool hasOpened,
        bool openedPairs,
        int normalTiles,
        int okeys,
        int colorMultiplier,
        int finishMultiplier
    )
    {
        if (isWinner)
            return -100 * colorMultiplier;
        int penalty = hasOpened
            ? normalTiles * 10 * colorMultiplier * (openedPairs ? 2 : 1) + okeys * 250
            : 600;
        return penalty * finishMultiplier;
    }

    // Çiftten, okey atarak ya da kafa (kimse açmamışken aynı turda açıp bitmek) bitiş: her biri 2 kat, birbiriyle çarpılır.
    public static int FinishMultiplier(bool pairs, bool okeyShot, bool kafa) =>
        (pairs ? 2 : 1) * (okeyShot ? 2 : 1) * (kafa ? 2 : 1);

    /// <summary>
    /// Oyun sonunda bir oyuncunun yiyeceği cezayı hesaplar.
    /// </summary>
    public int CalculateEndGamePenalty(
        int playerQue,
        bool hasOpened,
        bool isWinner,
        bool openedPairs,
        int finishMultiplier
    )
    {
        int multiplier = GameManager.Instance.GetCurrentColorMultiplier();
        TileDistrubite td = FindObjectOfType<TileDistrubite>();
        List<Tiles> hand = td != null ? td.GetPlayerTilesForBot(playerQue) : new List<Tiles>();
        int okeys = hand.Count(t => t.type == TileType.Joker);

        int penalty = EndPenalty(
            isWinner,
            hasOpened,
            openedPairs,
            hand.Count - okeys,
            okeys,
            multiplier,
            finishMultiplier
        );
        Debug.Log(
            $"[PenaltySystem] Oyuncu {playerQue}: {hand.Count - okeys} taş + {okeys} okey, çift={openedPairs}, bitiş x{finishMultiplier} -> {penalty}"
        );
        return penalty;
    }

    /// <summary>
    /// İşleme sırasında (Active Pers) Gösterge taşı kullanıldıysa özel ceza hesaplar.
    /// </summary>
    public int CalculateProcessingPenalty(Tiles tile, bool isIndicatorCheckNeeded)
    {
        // Eğer gösterge kontrolü gerekmiyorsa veya gösterge değilse standart ceza
        // Senin kodunda: Standart = Taşın Değeri * 10
        int basePenalty = tile.number * 10;

        if (isIndicatorCheckNeeded)
        {
            // TileDistrubite'den göstergeyi sor
            TileDistrubite td = FindObjectOfType<TileDistrubite>();
            Tiles indicator = td != null ? td.GetIndicatorTile() : null;

            if (
                indicator != null
                && tile.color == indicator.color
                && tile.number == indicator.number
            )
            {
                // KURAL: Gösterge işlenirse ceza 20 katıdır.
                Debug.Log($"[PenaltySystem] Gösterge taşı işlendi! Özel Ceza.");
                return tile.number * 20;
            }
        }

        return basePenalty;
    }

    // =================================================================================
    // BÖLÜM 2: GEÇİCİ CEZA YÖNETİMİ (Add / Undo / Commit)
    // =================================================================================

    // 1. Cezayı havuza ekle (Henüz ScoreManager'a yansıtma)
    public void AddPendingPenalty(int victimQue, int amount, string reason = "")
    {
        PendingPenaltyInfo info = new PendingPenaltyInfo
        {
            victimQue = victimQue,
            penaltyAmount = amount,
            reason = reason,
        };
        currentTurnPenalties.Add(info);
        Debug.Log(
            $"[PenaltySystem] Bekleyen Ceza Eklendi -> Oyuncu: {victimQue}, Miktar: {amount}"
        );
    }

    // 2. Cezayı havuzdan sil (Undo işlemi için)
    public void RemovePendingPenalty(int victimQue, int amount)
    {
        // Listeyi sondan başa tara (En son ekleneni silmek mantıklıdır - Stack mantığı)
        for (int i = currentTurnPenalties.Count - 1; i >= 0; i--)
        {
            if (
                currentTurnPenalties[i].victimQue == victimQue
                && currentTurnPenalties[i].penaltyAmount == amount
            )
            {
                currentTurnPenalties.RemoveAt(i);
                Debug.Log(
                    $"[PenaltySystem] Bekleyen Ceza İPTAL EDİLDİ (Undo) -> Oyuncu: {victimQue}, Miktar: {amount}"
                );
                return; // Sadece bir tane sil ve çık
            }
        }
    }

    // 3. Tur sonunda cezaları kesinleştir ve herkese gönder
    public void CommitAllTurnPenalties()
    {
        if (currentTurnPenalties.Count == 0)
            return;

        Debug.Log(
            $"[PenaltySystem] Tur bitti. {currentTurnPenalties.Count} adet ceza işleniyor..."
        );

        foreach (var penalty in currentTurnPenalties)
        {
            // RPC ile cezayı herkese (özellikle Master Client'a) duyur
            // Not: Master Client skorları tuttuğu için hedef MasterClient olmalı veya AllBuffered.
            photonView.RPC(
                "ApplyPenaltyRPC",
                RpcTarget.All,
                penalty.victimQue,
                penalty.penaltyAmount
            );
        }

        // Listeyi temizle ki sonraki turda tekrar yazmasın
        currentTurnPenalties.Clear();
    }

    // =================================================================================
    // BÖLÜM 3: NETWORK VE UYGULAMA (RPC)
    // =================================================================================

    [PunRPC]
    public void ApplyPenaltyRPC(int playerQue, int penaltyAmount)
    {
        // Cezaları sadece master tutar (diğer cezalar da master'da); her client kendi eksik kopyasından
        // PlayerScore yazarsa son yazan kazanır ve ekrandaki puan yanlış olur.
        if (!PhotonNetwork.IsMasterClient)
            return;
        if (GameManager.Instance != null && GameManager.Instance.scoreManager != null)
        {
            // ScoreManager'daki UpdatePlayerScore metodunu çağır
            GameManager.Instance.scoreManager.UpdatePlayerScore(playerQue, penaltyAmount);
            GameManager.Instance.AnnouncePenalty(playerQue, penaltyAmount, "Perine işlendi");

            Debug.Log(
                $"<color=red>[PENALTY APPLIED]</color> Oyuncu {playerQue} ceza yedi: {penaltyAmount}"
            );
        }
    }
}
