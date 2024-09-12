using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class TestPlayer_O : MonoBehaviourPunCallbacks
{
    public string playerName;
    public int playerQueue = 0;
    void Start()
    {
        if (photonView.IsMine)
        {
            playerName = PhotonNetwork.NickName;

        }
        Debug.Log("Taşlar dağıtılıyor...");
    }
}
