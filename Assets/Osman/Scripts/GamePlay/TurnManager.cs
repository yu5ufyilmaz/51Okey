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

    // Turu bitirme kontrolü (GÜNCELLENDİ)
    // TurnManager.cs -> CanFinishTurn Metodu

    public bool CanFinishTurn()
    {
        // Eğer yandan taş aldıysa kuralları kontrol et
        if (hasPickedFromSide)
        {
            // ScoreManager referansını güvenli şekilde al
            ScoreManager sm = GameManager.Instance.scoreManager;
            if (sm == null)
                sm = FindObjectOfType<ScoreManager>();

            // Kural 1: Bu tur elini açtıysa (hasOpenedThisTurn) -> OK
            // Kural 2: Bu tur yere taş işlediyse (hasProcessedThisTurn) -> OK
            // Kural 3 (YENİ): Zaten daha önceden açmışsa (hasOpenedSeries veya hasOpenedPairs) -> OK
            // Not: ScoreManager'daki hasOpenedSeries oyuncunun genel durumunu tutar.

            bool alreadyOpened = sm.hasOpenedSeries || sm.hasOpenedPairs;

            if (!hasOpenedThisTurn && !hasProcessedThisTurn && !alreadyOpened)
            {
                // Hiçbir şartı sağlamıyorsa -> Yandan taş aldı ama ne açtı ne işledi ne de zaten açıktı.
                // Bu durumda taşı geri iade edip ceza yemesi gerekir.
                return false;
            }
        }

        // Yandan almadıysa veya şartları sağladıysa turu bitirebilir.
        return true;
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

        Debug.Log($"Player {currentTurnPlayer}'s turn.");
    }
}
