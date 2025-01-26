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


    public void StartGame()
    {

        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object queueValue))
        {
            localPlayerTurn = true;
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

}
