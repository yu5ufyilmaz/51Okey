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
        if (UIManager.Instance != null)
        {
            // Yeni sıra kimdeyse onun ışığını yak
            UIManager.Instance.UpdateTurnIndicators(currentTurnPlayer);
        }
        if (currentTurnPlayer > PhotonNetwork.PlayerList.Length)
        {
            currentTurnPlayer = 1; // Döngü başa döner
        }
        if (
            PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object q)
            && (int)q == currentTurnPlayer
        )
        {
            // Senin zaten var olan metodun:
            FindObjectOfType<TileDistrubite>()
                .RecalculateAllAvailableSlots();
        }
        Debug.Log($"Player {currentTurnPlayer}'s turn.");
    }
}
