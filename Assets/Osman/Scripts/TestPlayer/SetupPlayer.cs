using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using TMPro;

public class PlayerSetup : MonoBehaviourPunCallbacks
{
    public string playerName;
    public int playerQueue;
    public TextMeshProUGUI nickNameText;

    public void IsLocal()
    {
        
    }

    void Start()
    {
        Debug.Log("Taşlar dağıtılıyor...");
    }
    [PunRPC]
    public void SetPlayerName(string _playerName)
    {
        playerName = _playerName;
        nickNameText.text = playerName;
    }
    [PunRPC]
    public void SetPlayerQueue(int _playerQueue)
    {
        playerQueue = _playerQueue;
    }
}
