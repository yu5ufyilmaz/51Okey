using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class TurnManager : MonoBehaviourPunCallbacks
{
    // Singleton Instance

    public int currentTurnPlayer = 1; // İlk sıradaki oyuncu

    [SerializeField]
    private bool localPlayerTurn;
    public bool canDrop = false;
    public bool hasPickedFromSide = false; // Yandan mı çekti?
    public bool hasOpenedThisTurn = false; // Bu el per açtı mı?
    [Header("Bot System")]
    public bool[] isBotActive = new bool[5];

    public void StartGame()
    {
        Player player = PhotonNetwork.LocalPlayer;
        player.CustomProperties.TryGetValue("PlayerQue", out object queueValue);
        int queueValueInt = (int)queueValue;
        if (queueValueInt == 1)
        {
            canDrop = true;
        }
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateTurnIndicators(currentTurnPlayer);
        }
    }
    public bool IsPlayerTurn()
    {
        if (
            PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(
                "PlayerQue",
                out object queueValue
            )
        )
        {
            localPlayerTurn = true;
            return (int)queueValue == currentTurnPlayer;
        }
        return false;
    }

    // TurnManager.cs içine:

    public int GetPreviousPlayerQue(int currentPlayerQue)
    {
        // Eğer sıra 1 ise, önceki 4'tür. Değilse 1 eksiğidir.
        // (Toplam 4 oyuncu olduğunu varsayıyoruz)
        if (currentPlayerQue == 1)
            return 4;
        return currentPlayerQue - 1;
    }

    // TurnManager.cs

    public bool hasProcessedThisTurn = false; // Oyuncu bu tur yere taş işledi mi?

    public void ResetTurnFlags()
    {
        hasPickedFromSide = false;
        hasOpenedThisTurn = false;
        hasProcessedThisTurn = false; // Sıfırla
        canDrop = false;
    }

    public bool CanFinishTurn()
    {
        // Eğer yandan taş aldıysa kuralları kontrol et
        if (hasPickedFromSide)
        {
            // ScoreManager referansını al
            ScoreManager sm = GameManager.Instance.scoreManager;
            if (sm == null)
                sm = FindObjectOfType<ScoreManager>();

            // KURAL: Yandan taş alan oyuncu, O TUR İÇİNDE mutlaka:
            // 1. Ya Yeni Per Açmalı (hasOpenedThisTurn)
            // 2. Ya da Mevcut Perlere Taş işlemeli (hasProcessedThisTurn)
            // NOT: "Daha önce açmış olması" (alreadyOpened) bu kuralı bypass etmez!
            // O yüzden alreadyOpened değişkenini bu kontrole dahil etmiyoruz.

            if (!hasOpenedThisTurn && !hasProcessedThisTurn)
            {
                // Şartları sağlamadı, turu bitiremez (veya ceza yemeli)
                return false;
            }
        }

        // Yandan almadıysa veya şartları sağladıysa turu bitirebilir.
        return true;
    }

    // TurnManager.cs içine ekle
    [PunRPC]
    public void RPC_ResetTurnForNewRound()
    {
        currentTurnPlayer = 1; // Sırayı tekrar 1. oyuncuya çek
        ResetTurnFlags(); // Tüm PickedFromSide, hasOpened vb. bayrakları temizle

        // Yerel oyuncunun sırasını kontrol et
        if (
            PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(
                "PlayerQue",
                out object queueValue
            )
        )
        {
            int myQue = (int)queueValue;
            canDrop = (myQue == 1); // Eğer 1. oyuncuysam taş atabilirim
        }

        // UI Işığını güncelle
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateTurnIndicators(currentTurnPlayer);
        }

        Debug.Log("TurnManager: Yeni el için sıralar sıfırlandı. Sıra 1. oyuncuda.");
    }

    [PunRPC]
    private void NextTurn()
    {
        localPlayerTurn = false;
        currentTurnPlayer++;

        // Oyuncu sayısı sınırını aşarsa başa dön
       if (currentTurnPlayer > 4) 
        {
            currentTurnPlayer = 1;
        }

        // --- [DÜZELTME BAŞLANGICI] ---

        // KRİTİK DEĞİŞİKLİK:
        // Masadaki "İşlek Taşları" (Available Slots) hesaplama işlemini
        // "Sıra Bende mi?" kontrolünün DIŞINA çıkarıyoruz.
        // Böylece sıra kimde olursa olsun, senin ekranındaki eski yeşil ışıklar söner
        // ve liste (availableTiles) yeni tura göre güncellenir.

        TileDistrubite tileDistrubite = FindObjectOfType<TileDistrubite>();
        if (tileDistrubite != null)
        {
            tileDistrubite.RecalculateAllAvailableSlots();
        }

        // --- [DÜZELTME BİTİŞİ] ---


        // SIRA BANA GELDİYSE, KİŞİSEL BAYRAKLARI (Açtı mı? İşledi mi?) SIFIRLA!
        if (
            PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object q)
            && (int)q == currentTurnPlayer
        )
        {
            ResetTurnFlags();

            // NOT: RecalculateAllAvailableSlots() buradaydı,
            // yukarı (herkes için çalışacak yere) taşıdık.
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateTurnIndicators(currentTurnPlayer);
        }

        Debug.Log($"Player {currentTurnPlayer}'s turn.");
        if (PhotonNetwork.IsMasterClient && isBotActive[currentTurnPlayer])
    {
        StartCoroutine(PlayBotTurn(currentTurnPlayer));
    }
    }
    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    {
        Debug.LogWarning($"OYUNCU DÜŞTÜ: {otherPlayer.NickName}. Bot modu devreye giriyor.");

        // Düşen oyuncunun 'PlayerQue' (Sıra Numarası) değerini bulmamız lazım.
        if (otherPlayer.CustomProperties.TryGetValue("PlayerQue", out object queueVal))
        {
            int leftPlayerQue = (int)queueVal;
            
            // 1. O koltuğu BOT olarak işaretle
            if (leftPlayerQue >= 1 && leftPlayerQue <= 4)
            {
                isBotActive[leftPlayerQue] = true;
            }

            // 2. Eğer tam şu an sıra o düşen oyuncudaysa, oyun donmasın diye botu hemen oynat.
            // Sadece MasterClient çalıştırır (herkes çalıştırırsa kaos olur).
            if (PhotonNetwork.IsMasterClient && currentTurnPlayer == leftPlayerQue)
            {
                StartCoroutine(PlayBotTurn(leftPlayerQue));
            }
        }
    }
  private IEnumerator PlayBotTurn(int playerQue)
    {
        Debug.Log($"[BOT] Sıra {playerQue}. oyuncuda (Bot). Hamle yapılıyor...");
        yield return new WaitForSeconds(1.5f);

        TileDistrubite tileDistrubite = FindObjectOfType<TileDistrubite>();
        ScoreManager scoreManager = FindObjectOfType<ScoreManager>();

        // --- ADIM 1: TAŞ ÇEK ---
        tileDistrubite.photonView.RPC("AddTileFromMiddlePlayerList", RpcTarget.AllBuffered, playerQue);
        
        yield return new WaitForSeconds(1.5f);

        // --- ADIM 2: EN KÖTÜ TAŞI BUL ---
        // DİKKAT: Artık int değil string alıyoruz!
        string worstTileID = scoreManager.FindBestTileToDiscardForBot(playerQue);

        // --- ADIM 3: TAŞI AT ---
        // Kontrol: String boş değilse at
        if (!string.IsNullOrEmpty(worstTileID))
        {
            // RPC'ye string gönderiyoruz
            tileDistrubite.photonView.RPC("BotDiscardTileRPC", RpcTarget.AllBuffered, playerQue, worstTileID);
        }
        else
        {
            Debug.LogError("[BOT] Atacak taş bulamadı! Oyun kilitlenmesin diye rastgele atılıyor.");
            
            // Eğer sistem hata verirse, TurnManager kilitlenmesin diye turu zorla geçir (Fail-safe)
            if(PhotonNetwork.IsMasterClient)
            {
                 // Burada NextTurn çağırmak mantıklı olabilir veya rastgele bir taş ID'si bulup attırabilirsin.
                 // Şimdilik en azından log düşsün.
            }
        }
    }


   
}
