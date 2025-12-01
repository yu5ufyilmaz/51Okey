using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class PhotonManager : MonoBehaviourPunCallbacks
{
    static PhotonManager instance = null;

    void Start()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }
        // Bağlantı kopma süresini uzat (Milisaniye cinsinden)
        // Varsayılan genelde düşüktür, bunu artırarak kopmaları engellersin.
        PhotonNetwork.NetworkingClient.LoadBalancingPeer.DisconnectTimeout = 30000; // 30 Saniye (Normali 10000)
        PhotonNetwork.KeepAliveInBackground = 60000; // Arka planda 60 saniye tut
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Server");
        PhotonNetwork.JoinLobby();
    }

    public override void OnLeftLobby()
    {
        Debug.Log("Left Lobby");
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.Log("Failed to join room");
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log("Failed to join random room");
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.Log("Failed to create room");
    }
}
