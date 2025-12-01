using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;

public class MainMenuUIHandler : MonoBehaviourPunCallbacks
{
    public TMP_InputField playerNameInput;
    public TextMeshProUGUI buttonText;

    //Main menü kısmındaki kodların Manager Scripti.
    // Bu kısıma Google Hesaplarını, Facebook hesaplarını bağlama eklenecek.
    public void ConnectLobby()
    {
        if (PhotonNetwork.InLobby)
        {
            if (playerNameInput.text.Length > 1)
            {
                PhotonNetwork.NickName = playerNameInput.text;

                buttonText.text = "Connecting...";
                SceneChangeManager.Instance.ChangeScene("LobbyMenu");
            }
        }
    }
}
