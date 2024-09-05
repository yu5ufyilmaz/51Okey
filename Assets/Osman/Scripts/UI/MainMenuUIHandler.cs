using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Photon.Realtime;
using UnityEngine.SceneManagement;

public class MainMenuUIHandler : MonoBehaviour
{
    public TMP_InputField playerNameInput;
    // Bu kısımıa Google Hesaplarını Facebookj hesaplarını bağlama eklenecek
    void Start()
    {
        if (PlayerPrefs.HasKey("PlayerNickname"))
            playerNameInput.text = PlayerPrefs.GetString("PlayerNickname");

    }

    public void OnJoinGameClicked()
    {

        PlayerPrefs.SetString("PlayerNickname", playerNameInput.text);
        PlayerPrefs.Save();
        if (playerNameInput.text.Length > 0)
            SceneManager.LoadScene("LobbyMenu");
    }

}
