using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class ExitButton : MonoBehaviourPunCallbacks
{
    public void ExitGame()
    {
        PhotonNetwork.LeaveRoom();
        SceneChangeManager.Instance.ChangeScene("LobbyMenu");
    }


}
