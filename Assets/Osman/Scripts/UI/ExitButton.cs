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
        ReconnectFlow.DeliberateLeave = true; // OnLeftRoom kopma sanmasın; koltuk maç sonuna dek bot
        RejoinRecord.Clear();
        Debug.Log("Çıkış butonuna basıldı, odadan ayrılınıyor...");
        // Sadece odadan çık emri veriyoruz. SAHNE DEĞİŞTİRMİYORUZ.
        // Oyun başlamadan (oda açık) kalıcı çık: inactive kalan 60 sn koltuğu tutar ve aynı masaya giremez.
        // Oyun sırasında eskisi gibi inactive kalır, yerine bot oynar.
        PhotonNetwork.LeaveRoom(PhotonNetwork.CurrentRoom != null && !PhotonNetwork.CurrentRoom.IsOpen);
    }

    // Masadaki "Çıkış Butonu" ve geri tuşu buraya gelir ve önce onay sorar; oyun sonu odadan buraya uğramadan doğrudan çıkar.
    public async void AskExit()
    {
        if (await UIRoot.Confirm(Loc.T("table.leave_title"), Loc.T("table.leave_body"), Loc.T("table.leave_yes"), true) && this != null)
            ExitGame();
    }

    // Photon "Tamam, odadan çıktın" dediği an burası çalışır.
    // Beklenmeyen düşmede (kayıt varken) ReconnectFlow nötr sahneden geri döner; bilerek çıkış ve oyun öncesi düşme lobiye.
    public override void OnLeftRoom()
    {
        if (!ReconnectFlow.DeliberateLeave && RejoinRecord.Load() != null)
            return;
        ReconnectFlow.DeliberateLeave = false;
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
