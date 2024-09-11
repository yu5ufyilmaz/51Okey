using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Photon.Pun;


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


        PhotonNetwork.ConnectUsingSettings();

    }
    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Server");
        PhotonNetwork.JoinLobby();

    }


    public override void OnJoinedRoom()
    {
        Debug.Log(PhotonNetwork.CurrentRoom.Name + " joined");
    }


    public override void OnLeftLobby()
    {
        Debug.Log("Left Lobby");
        PhotonNetwork.CurrentRoom.SetMasterClient(PhotonNetwork.LocalPlayer);
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
