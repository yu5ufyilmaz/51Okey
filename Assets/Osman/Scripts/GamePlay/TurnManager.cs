using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class TurnManager : MonoBehaviourPunCallbacks
{
    // Singleton Instance

    private int currentTurnPlayer = 1; // İlk sıradaki oyuncu
    [SerializeField] private bool localPlayerTurn;
    private bool isGameRunning = false;


    public void StartGame()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            photonView.RPC("StartGameRPC", RpcTarget.AllBuffered);
        }
    }
    public bool IsPlayerTurn()
    {
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object queueValue))
        {
            localPlayerTurn = true;
            return (int)queueValue == currentTurnPlayer;
        }
        return false;

    }
    [PunRPC]
    public void StartGameRPC()
    {
        isGameRunning = true;
        currentTurnPlayer = 1;
        Debug.Log("Game started. Player 1's turn.");
    }

    public void EndTurn()
    {
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object queueValue) &&
            (int)queueValue == currentTurnPlayer)
        {

            photonView.RPC("NextTurn", RpcTarget.AllBuffered);
        }
    }

    [PunRPC]
    private void NextTurn()
    {
        localPlayerTurn = false;
        currentTurnPlayer++;
        if (currentTurnPlayer > PhotonNetwork.PlayerList.Length)
        {
            currentTurnPlayer = 1; // Döngü başa döner
        }
        Debug.Log($"Player {currentTurnPlayer}'s turn.");
    }

    private void Update()
    {
        if (!isGameRunning) return;

        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object queueValue))
        {
            if ((int)queueValue == currentTurnPlayer)
            {
                // Oyuncu kendi sırasındaysa oynamaya izin ver
                EnablePlayerActions(true);
            }
            else
            {
                // Sırası olmayan oyuncular bekler
                EnablePlayerActions(false);
            }
        }
    }

    private void EnablePlayerActions(bool enable)
    {
        // Oyun içi hareket ve diğer kontrolleri aktif/pasif yap
        Debug.Log($"Actions {(enable ? "enabled" : "disabled")} for {PhotonNetwork.LocalPlayer.NickName}");
        // Burada oyuncunun kontrolüne bağlı işlemleri ekleyin
    }
}
