using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Photon.Pun;

public class MainMenuUIHandler : MonoBehaviourPunCallbacks
{

    public TMP_InputField playerNameInput;
    public TextMeshProUGUI buttonText;

    public FirebaseNetwork firebaseNetwork;

    //Main menü kısmındaki kodların Manager Scripti.
    // Bu kısıma Google Hesaplarını, Facebook hesaplarını bağlama eklenecek.
    public void ConnectLobby()
    {
        if (PhotonNetwork.InLobby)
        {


            PhotonNetwork.NickName = firebaseNetwork.username;
            buttonText.text = "Connecting...";
            SceneChangeManager.Instance.ChangeScene("LobbyMenu");

        }

    }


}
