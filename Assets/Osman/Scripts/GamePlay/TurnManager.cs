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
    public bool canDrop = false;


    public void StartGame()
    {
        Player player = PhotonNetwork.LocalPlayer;
        player.CustomProperties.TryGetValue("PlayerQue", out object queueValue);
        int queueValueInt = (int)queueValue;
        if (queueValueInt == 1)
        {
            canDrop = true;
        }
        else
            Debug.Log(queueValueInt);

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
