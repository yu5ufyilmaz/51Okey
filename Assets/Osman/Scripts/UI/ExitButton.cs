using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;

public class ExitButton : MonoBehaviourPunCallbacks
{
    //Oyuncunun bulunduğu odadan çıktıktan sonra tekrardan Lobbye bağlanmasını sağlayan Fonksiyonlar
    ScoreManager scoreManager;
    private void Start()
    {

    }
    public void ExitGame()
    {
        PhotonNetwork.CurrentRoom.SetMasterClient(PhotonNetwork.LocalPlayer);
        Debug.Log(PhotonNetwork.MasterClient.NickName);
        PhotonNetwork.LeaveRoom();
        SceneChangeManager.Instance.ChangeScene("LobbyMenu");
    }

    public override void OnConnectedToMaster()
    {
        PhotonNetwork.JoinLobby();
    }

    public void MeldTileButton()
    {
        if (scoreManager == null)
        {
            if (GameObject.Find("ScoreManager(Clone)") != null)
            {
                scoreManager = GameObject.Find("ScoreManager(Clone)").GetComponent<ScoreManager>();
                scoreManager.OnButtonClick();
            }
        }
        else
            scoreManager.OnButtonClick();
    }
    public void MeldPairTileButton()
    {
        if (scoreManager == null)
        {
            if (GameObject.Find("ScoreManager(Clone)") != null)
            {
                scoreManager = GameObject.Find("ScoreManager(Clone)").GetComponent<ScoreManager>();
                scoreManager.OnPairButtonClick();
            }
        }
        else
            scoreManager.OnPairButtonClick();
    }
}
