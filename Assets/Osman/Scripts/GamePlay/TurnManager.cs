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
    public bool CanFinishTurn()
    {
        // Eğer yandan aldıysa...
        if (hasPickedFromSide)
        {
            // Eğer ne açtıysa NE DE işlediyse -> HATA (Ceza yer)
            // Yani: Açtıysa GEÇER, İşlediyse GEÇER.
            if (!hasOpenedThisTurn && !hasProcessedThisTurn)
            {
                return false;
            }
        }
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
