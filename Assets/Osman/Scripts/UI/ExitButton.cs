using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;

public class ExitButton : MonoBehaviourPunCallbacks
{
    //Oyuncunun bulunduğu odadan çıktıktan sonra tekrardan Lobbye bağlanmasını sağlayan Fonksiyonlar
    ScoreManager scoreManager;

    public void ExitGame()
    {
        Debug.Log("Çıkış butonuna basıldı, odadan ayrılınıyor...");
        // Sadece odadan çık emri veriyoruz. SAHNE DEĞİŞTİRMİYORUZ.
        PhotonNetwork.LeaveRoom();
    }

    // Photon "Tamam, odadan çıktın" dediği an burası çalışır.
    public override void OnLeftRoom()
    {
        Debug.Log("Odadan çıkış onaylandı. Lobiye dönülüyor...");
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

    public void TakeBackTileButton()
    {
        if (scoreManager == null)
        {
            if (GameObject.Find("ScoreManager(Clone)") != null)
            {
                scoreManager = GameObject.Find("ScoreManager(Clone)").GetComponent<ScoreManager>();
                scoreManager.OnTakeBackButtonClick();
            }
        }
        else
            scoreManager.OnTakeBackButtonClick();
    }

    public void ActiveTilesButton()
    {
        if (scoreManager == null)
        {
            if (GameObject.Find("ScoreManager(Clone)") != null)
            {
                scoreManager = GameObject.Find("ScoreManager(Clone)").GetComponent<ScoreManager>();
                scoreManager.OnActiveButtonClick();
            }
        }
        else
            scoreManager.OnActiveButtonClick();
    }
}
