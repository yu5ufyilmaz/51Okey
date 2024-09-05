using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Realtime;
using Photon.Pun;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

public class PhotonManager : MonoBehaviourPunCallbacks
{
    // Start is called before the first frame update
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
