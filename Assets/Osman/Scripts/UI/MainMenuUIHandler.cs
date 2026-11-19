using System;
using System.Collections;
using System.IO;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Giriş ekranı: kayıtlı oturum yoksa Misafir / Google seçimi, isim gerekiyorsa isim paneli, sonra lobi.
public class MainMenuUIHandler : MonoBehaviour
{
    public TextMeshProUGUI loadingText;

    [Header("Seçim")]
    public GameObject choicePanel;
    public Button guestButton;
    public Button googleButton;
    public TextMeshProUGUI statusText;

    [Header("İsim")]
    public GameObject namePanel;
    public TMP_InputField nameInput;
    public TextMeshProUGUI nameError;
    public Button nameOkButton;

    [Header("Tekrar dene")]
    public GameObject retryPanel;
    public Button retryButton;

    bool initDone, initFailed, autoTest, enteringLobby;

    // Paneller sahnede açık durur (Editör'de görünsün); açılışta yalnız "Yükleniyor..." görünür.
    void Awake()
    {
        Show(EntryFlow.Screen.Loading);
        // Editör'de Google girişi yok (yerel arka uç): düğme yalnız Firebase'de.
        googleButton.gameObject.SetActive(GameServices.UsesFirebase);
        nameInput.characterLimit = GameConfig.NameMax;
        statusText.text = "";
        nameError.text = "";
    }

    async void Start()
    {
        GameSettings.Apply();
        // Senkron testinde isim ve lobiye geçiş SyncAutoTest'in işi; koşul GameServices'tekiyle aynı.
        autoTest = Application.isEditor && SyncAutoTestFiles.TryLoadConfig(Directory.GetCurrentDirectory(), out _);
        // Init hatasını PhotonManager zaten loglar; burada yalnız Tekrar dene paneli açılır.
        try { await GameServices.InitAsync(); }
        catch (Exception) { }
        if (this == null)
            return;
        initDone = true;
        initFailed = !GameServices.IsReady;
        Refresh();
    }

    void Refresh()
    {
        var screen = EntryFlow.Next(initDone, initFailed, GameServices.HasSession, GameServices.NeedsName);
        if (autoTest && (screen == EntryFlow.Screen.AskName || screen == EntryFlow.Screen.Lobby))
        {
            Show(EntryFlow.Screen.Loading);
            loadingText.text = "Bağlanıyor...";
            return;
        }
        Show(screen);
        if (screen == EntryFlow.Screen.Lobby && !enteringLobby)
            StartCoroutine(EnterLobby());
    }

    void Show(EntryFlow.Screen screen)
    {
        loadingText.gameObject.SetActive(screen == EntryFlow.Screen.Loading || screen == EntryFlow.Screen.Lobby);
        retryPanel.SetActive(screen == EntryFlow.Screen.Retry);
        choicePanel.SetActive(screen == EntryFlow.Screen.Choose);
        namePanel.SetActive(screen == EntryFlow.Screen.AskName);
    }

    // Lobiye geçiş tek yerde: Photon lobisi oturana kadar beklenir, NickName profil isminden gelir.
    IEnumerator EnterLobby()
    {
        enteringLobby = true;
        loadingText.text = "Bağlanıyor...";
        while (!PhotonNetwork.InLobby)
            yield return null;
        PhotonNetwork.NickName = GameServices.Profile.Current.DisplayName;
        SceneChangeManager.Instance.ChangeScene("LobbyMenu");
    }

    // Çift dokunuş ikinci hesap açmasın: iş sürerken iki giriş düğmesi de kapalı (GameServices başka işlemi fırlatır).
    void SetEntryBusy(bool busy)
    {
        guestButton.interactable = !busy;
        googleButton.interactable = !busy;
        if (busy)
            statusText.text = "";
    }

    public async void PlayAsGuest()
    {
        SetEntryBusy(true);
        try { await GameServices.EnterAsGuestAsync(); }
        catch (Exception e)
        {
            Debug.LogWarning("[SERVICES] misafir girişi olmadı: " + e.GetType().Name + " " + e.Message);
            if (this == null)
                return;
            SetEntryBusy(false);
            statusText.text = "Bağlanılamadı";
            return;
        }
        if (this == null)
            return;
        SetEntryBusy(false);
        Refresh();
    }

    // İptal ya da hata: seçim ekranında kalınır, misafir hesap açılmaz.
    public async void PlayWithGoogle()
    {
        SetEntryBusy(true);
        LinkResult result;
        try { result = await GameServices.EnterWithGoogleAsync(); }
        catch (Exception e)
        {
            // Google girişi olup profil yüklenemediyse de buraya düşer.
            Debug.LogWarning("[SERVICES] Google girişi olmadı: " + e.GetType().Name + " " + e.Message);
            result = LinkResult.Failed;
        }
        if (this == null)
            return;
        SetEntryBusy(false);
        if (result == LinkResult.Cancelled)
            statusText.text = "Google girişi tamamlanmadı";
        else if (result == LinkResult.Failed)
            statusText.text = "Google'a bağlanılamadı";
        Refresh();
    }

    public async void ConfirmName()
    {
        if (!GameConfig.IsValidName(nameInput.text))
        {
            nameError.text = "İsim 2-16 karakter olmalı";
            return;
        }
        nameError.text = "";
        nameOkButton.interactable = false;
        try { await GameServices.Profile.SetDisplayNameAsync(nameInput.text); }
        catch (Exception e)
        {
            Debug.LogWarning("[SERVICES] isim kaydedilemedi: " + e.GetType().Name + " " + e.Message);
            if (this == null)
                return;
            nameOkButton.interactable = true;
            nameError.text = "Bağlanılamadı";
            return;
        }
        if (this == null)
            return;
        nameOkButton.interactable = true;
        Refresh();
    }

    public async void RetryConnect()
    {
        retryButton.interactable = false;
        await PhotonManager.ConnectWhenReadyAsync();
        if (this == null)
            return;
        retryButton.interactable = true;
        initFailed = !GameServices.IsReady;
        Refresh();
    }
}
