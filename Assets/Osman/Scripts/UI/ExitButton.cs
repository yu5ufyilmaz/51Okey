using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class ExitButton : MonoBehaviourPunCallbacks
{
    //Oyuncunun bulunduğu odadan çıktıktan sonra tekrardan Lobbye bağlanmasını sağlayan Fonksiyonlar
    public void ExitGame()
    {
        PhotonNetwork.CurrentRoom.SetMasterClient(PhotonNetwork.LocalPlayer);
        PhotonNetwork.LeaveRoom();
        SceneChangeManager.Instance.ChangeScene("LobbyMenu");
    }
    public override void OnLeftRoom()
    { 
        Debug.Log("Left Room");
    }

    public override void OnConnectedToMaster()
    {
        PhotonNetwork.JoinLobby();
    }
}
