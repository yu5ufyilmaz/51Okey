using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuUIHandler : MonoBehaviourPunCallbacks
{
    public TMP_InputField playerNameInput;
    public TextMeshProUGUI buttonText;

    [Header("Hesap (yalnızca Firebase arka ucunda görünür)")]
    public Button googleButton;
    public TextMeshProUGUI accountStatusText;
    public GameObject retryPanel;
    public Button retryButton;
    public GameObject confirmSwitchPanel;

    TaskCompletionSource<bool> switchAnswer;
    string filledName;

    void Awake()
    {
        GameServices.InitFailed += OnInitFailed;
        GameServices.Auth.UserChanged += RefreshAccount;
        GameServices.Profile.Changed += OnProfileChanged;
        GameServices.ConfirmAccountSwitch = AskSwitchAsync;
        RefreshAccount();
    }

    void OnDestroy()
    {
        GameServices.InitFailed -= OnInitFailed;
        GameServices.Auth.UserChanged -= RefreshAccount;
        GameServices.Profile.Changed -= OnProfileChanged;
        if (GameServices.ConfirmAccountSwitch == AskSwitchAsync)
            GameServices.ConfirmAccountSwitch = null;
        switchAnswer?.TrySetResult(false);
    }

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
            filledName = profile.DisplayName;
        }
        RefreshAccount();
    }

    // Tekrar dene ya da hesap geçişi sonrası profil yüklenince isim kutusu dolar; oyuncunun yazdığı isim ezilmez.
    void OnProfileChanged(PlayerProfile profile)
    {
        if (profile == null || playerNameInput == null)
            return;
        if (string.IsNullOrEmpty(playerNameInput.text) || playerNameInput.text == filledName)
        {
            playerNameInput.text = profile.DisplayName;
            filledName = profile.DisplayName;
        }
        RefreshAccount();
    }

    void RefreshAccount()
    {
        if (this == null)
            return;
        bool firebase = GameServices.UsesFirebase;
        bool guest = GameServices.Auth.IsGuest;
        if (googleButton != null)
            googleButton.gameObject.SetActive(firebase && guest && GameServices.IsReady);
        if (accountStatusText != null)
        {
            accountStatusText.gameObject.SetActive(firebase && GameServices.IsReady);
            accountStatusText.text = guest ? "Misafir hesap" : "Google hesabı bağlı";
        }
    }

    // Yerel arka uçta init hatası olsa da bağlanılır (PhotonManager); panel yalnızca Firebase'de.
    void OnInitFailed(System.Exception e)
    {
        if (this != null && GameServices.UsesFirebase && retryPanel != null)
            retryPanel.SetActive(true);
    }

    public async void RetryConnect()
    {
        retryButton.interactable = false;
        bool ok = await PhotonManager.ConnectWhenReadyAsync();
        if (this == null)
            return;
        retryButton.interactable = true;
        retryPanel.SetActive(!ok);
        RefreshAccount();
    }

    public async void LinkGoogle()
    {
        googleButton.interactable = false;
        var result = await GameServices.Auth.LinkGoogleAsync();
        if (this == null)
            return;
        googleButton.interactable = true;
        RefreshAccount();
        if (result == LinkResult.Failed)
            accountStatusText.text = "Google'a bağlanılamadı";
    }

    Task<bool> AskSwitchAsync()
    {
        switchAnswer?.TrySetResult(false);
        switchAnswer = new TaskCompletionSource<bool>();
        confirmSwitchPanel.SetActive(true);
        return switchAnswer.Task;
    }

    public void ConfirmSwitchYes() => AnswerSwitch(true);

    public void ConfirmSwitchNo() => AnswerSwitch(false);

    void AnswerSwitch(bool yes)
    {
        confirmSwitchPanel.SetActive(false);
        switchAnswer?.TrySetResult(yes);
        switchAnswer = null;
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
                // Her lobi girişinde değil, yalnızca isim değiştiyse yaz (Firestore'da her yazım ağ isteği).
                if (GameServices.Profile.Current?.DisplayName != playerNameInput.text.Trim())
                    _ = GameServices.Profile.SetDisplayNameAsync(playerNameInput.text);

                buttonText.text = "Connecting...";
                SceneChangeManager.Instance.ChangeScene("LobbyMenu");
            }
        }
    }
}
