using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PenaltySystem : MonoBehaviourPunCallbacks
{
    // Singleton yapısı (Erişimi kolaylaştırmak için)
    public static PenaltySystem Instance;

    // --- VERİ YAPILARI ---
    [System.Serializable]
    public class PendingPenaltyInfo // struct yerine class
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

    /// <summary>
    /// Oyun sonunda bir oyuncunun yiyeceği cezayı hesaplar.
    /// </summary>
    public int CalculateEndGamePenalty(int playerQue, bool hasOpened, bool isWinner)
    {
        // Renk çarpanını GameManager'dan al (Sarı x2, Kırmızı x3 vb.)
        int multiplier = GameManager.Instance.GetCurrentColorMultiplier();

        // 1. KAZANAN OYUNCU (Düşüm)
        if (isWinner)
        {
            return -600; // Standart bitiş düşümü
        }

        // 2. HİÇ AÇMAMIŞ OYUNCU
        if (!hasOpened)
        {
            // PDF Kuralı: Açmayana sabit ceza (Örn: 600)
            return 600;
        }

        // 3. AÇMIŞ AMA BİTMEMİŞ OYUNCU
        // Elindeki taşların toplam değeri veya adedi üzerinden hesaplanır.
        // Mevcut kodunda: Taş Adedi * 10 * Çarpan

        TileDistrubite td = FindObjectOfType<TileDistrubite>();
        if (td != null)
        {
            int remainingTileCount = td.GetPlayerHandCount(playerQue);

            // Örnek: Roket (x8) ve 5 taş kaldıysa -> 5 * 10 * 8 = 400 Ceza
            int calculatedPenalty = remainingTileCount * 10 * multiplier;

            Debug.Log(
                $"[PenaltySystem] Oyuncu {playerQue} elinde {remainingTileCount} taş kaldı. Ceza: {calculatedPenalty}"
            );
            return calculatedPenalty;
        }

        return 0; // Hata durumu
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
        // ScoreManager'ı bul ve puanı güncelle
        if (GameManager.Instance != null && GameManager.Instance.scoreManager != null)
        {
            // ScoreManager'daki UpdatePlayerScore metodunu çağır
            GameManager.Instance.scoreManager.UpdatePlayerScore(playerQue, penaltyAmount);

            Debug.Log(
                $"<color=red>[PENALTY APPLIED]</color> Oyuncu {playerQue} ceza yedi: {penaltyAmount}"
            );
        }
    }
}
