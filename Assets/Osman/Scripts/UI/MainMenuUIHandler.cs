using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;

public class MainMenuUIHandler : MonoBehaviourPunCallbacks
{
    public TMP_InputField playerNameInput;
    public TextMeshProUGUI buttonText;

    async void Start()
    {
        // Init hatasını PhotonManager zaten loglar; burada yutulur.
        try { await GameServices.InitAsync(); }
        catch (System.Exception) { }
        // Profil yüklenemediyse (Current null) isim kutusuna dokunma.
        var profile = GameServices.Profile.Current;
        if (profile != null && playerNameInput != null && string.IsNullOrEmpty(playerNameInput.text))
        {
            playerNameInput.text = profile.DisplayName;
        }
    }

    //Main menü kısmındaki kodların Manager Scripti.
    // Bu kısıma Google Hesaplarını, Facebook hesaplarını bağlama eklenecek.
    public void ConnectLobby()
    {
        if (PhotonNetwork.InLobby)
        {
            if (playerNameInput.text.Length > 1)
            {
                PhotonNetwork.NickName = playerNameInput.text;
                _ = GameServices.Profile.SetDisplayNameAsync(playerNameInput.text);

                buttonText.text = "Connecting...";
                SceneChangeManager.Instance.ChangeScene("LobbyMenu");
            }
        }
    }
}
