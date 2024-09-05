using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Photon.Realtime;
using UnityEngine.SceneManagement;
using Photon.Pun;

public class MainMenuUIHandler : MonoBehaviourPunCallbacks
{

    public TMP_InputField playerNameInput;
    public TextMeshProUGUI buttonText;

    // Bu kısımıa Google Hesaplarını Facebookj hesaplarını bağlama eklenecek
    public void ConnectLobby()
    {
        if (PhotonNetwork.InLobby)
        {
            if (playerNameInput.text.Length > 1)
            {
                PhotonNetwork.NickName = playerNameInput.text;
                buttonText.text = "Connecting...";
                SceneManager.LoadScene("LobbyMenu");
            }
        }

    }


}
