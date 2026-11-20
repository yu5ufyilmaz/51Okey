using System;
using System.Collections;
using System.IO;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Giriş ekranı: kayıtlı oturum yoksa Misafir / Google seçimi, isim gerekiyorsa isim paneli, sonra lobi.
// Yükleme ve bildirimler kalıcı UIRoot katmanında; sahne yalnız seçim ekranını ve iki pencereyi tutar.
public class MainMenuUIHandler : MonoBehaviour
{
    public TextMeshProUGUI versionText;
    public GameObject title;

    [Header("Seçim")]
    public GameObject choicePanel;
    public Button guestButton;
    public Button googleButton;

    [Header("İsim")]
    public GameObject namePanel;
    public TMP_InputField nameInput;
    public TextMeshProUGUI nameError;
    public Button nameOkButton;

    [Header("Tekrar dene")]
    public GameObject retryPanel;
    public Button retryButton;

    bool initDone, initFailed, autoTest, enteringLobby, photonDropped, retrying;

    // Seçim paneli sahnede açık, pencereler kapalı kayıtlıdır; açılışta yalnız "Bağlanıyor..." katmanı görünür.
    void Awake()
    {
        Show(EntryFlow.Screen.Loading);
        // Editör'de Google girişi yok (yerel arka uç): düğme yalnız Firebase'de.
        googleButton.gameObject.SetActive(GameServices.UsesFirebase);
        nameInput.characterLimit = GameConfig.NameMax;
        nameError.text = "";
        versionText.text = "v" + Application.version;
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
        // Init başarılı olsa da Photon koptuysa Next "Lobby" der; panel RetryConnect'e kadar kalır.
        if (photonDropped)
            screen = EntryFlow.Screen.Retry;
        if (autoTest && (screen == EntryFlow.Screen.AskName || screen == EntryFlow.Screen.Lobby))
        {
            Show(EntryFlow.Screen.Loading);
            return;
        }
        Show(screen);
        if (screen == EntryFlow.Screen.Lobby && !enteringLobby)
            StartCoroutine(EnterLobby());
    }

    void Show(EntryFlow.Screen screen)
    {
        if (screen == EntryFlow.Screen.Loading || screen == EntryFlow.Screen.Lobby)
            UIRoot.ShowLoading(Loc.T("login.connecting"), false);
        else
            UIRoot.HideLoading();
        // Pencere ortadaki gövdesiyle büyük başlığı ikiye bölüyor: pencere açıkken başlık gizlenir
        title.SetActive(screen != EntryFlow.Screen.AskName && screen != EntryFlow.Screen.Retry);
        retryPanel.SetActive(screen == EntryFlow.Screen.Retry);
        choicePanel.SetActive(screen == EntryFlow.Screen.Choose);
        namePanel.SetActive(screen == EntryFlow.Screen.AskName);
    }

    // Lobiye geçiş tek yerde: Photon lobisi oturana kadar beklenir, NickName profil isminden gelir.
    bool identityFixed;
    IEnumerator EnterLobby()
    {
        enteringLobby = true;
        while (!PhotonNetwork.InLobby)
        {
            // Önce bir kare: aynı karede PhotonManager.Start bağlanmayı başlatmış olsun (ilk durum PeerCreated, Disconnected değil).
            yield return null;
            // Photon koptu ya da bağlanamadı: süresiz beklenmez, Tekrar dene paneli açılır ve basılana kadar kalır.
            if (!PhotonNetwork.IsConnected && PhotonNetwork.NetworkClientState == ClientState.Disconnected)
            {
                enteringLobby = false;
                photonDropped = true;
                // Tekrar dene'ye basıldıktan sonra yine kopma: bildirim burada
                if (retrying)
                {
                    retrying = false;
                    UIRoot.Toast(Loc.T("login.connection_failed"));
                }
                Show(EntryFlow.Screen.Retry);
                yield break;
            }
        }
        // İlk açılışta Photon girişten önce bağlanır: kimlik Firebase uid'i değilse bir kez kopar, uid ile yeniden bağlanır.
        if (!identityFixed && PhotonIdentity.NeedsReconnect(PhotonNetwork.AuthValues?.UserId, GameServices.Auth.UserId))
        {
            identityFixed = true;
            PhotonNetwork.Disconnect();
            while (PhotonNetwork.NetworkClientState != ClientState.Disconnected)
                yield return null;
            var connect = PhotonManager.ConnectWhenReadyAsync();
            while (!connect.IsCompleted)
                yield return null;
            enteringLobby = false;
            StartCoroutine(EnterLobby());
            yield break;
        }
        retrying = false;
        PhotonNetwork.NickName = GameServices.Profile.Current.DisplayName;
        // Uygulama oyun ortasında kapandıysa: kayıtlı odaya dönmek istiyor mu?
        string room = RejoinRecord.Load();
        if (room != null)
        {
            UIRoot.HideLoading(); // "Bağlanıyor" katmanı soruyu örtmesin
            var ask = UIRoot.Confirm(Loc.T("rejoin.ask_title"), Loc.T("rejoin.ask_body"), Loc.T("rejoin.ask_yes"));
            while (!ask.IsCompleted)
                yield return null;
            if (this == null)
                yield break;
            if (ask.Result)
            {
                PhotonNetwork.AutomaticallySyncScene = true;
                ReconnectFlow.RejoinSaved(room);
                yield break;
            }
            RejoinRecord.Clear();
        }
        SceneChangeManager.Instance.ChangeScene("LobbyMenu");
    }

    // Çift dokunuş ikinci hesap açmasın: iş sürerken iki giriş düğmesi de kapalı (GameServices başka işlemi fırlatır).
    void SetEntryBusy(bool busy)
    {
        guestButton.interactable = !busy;
        googleButton.interactable = !busy;
    }

    public async void PlayAsGuest()
    {
        SetEntryBusy(true);
        UIRoot.ShowLoading(Loc.T("login.signing_in"), false);
        try { await GameServices.EnterAsGuestAsync(); }
        catch (Exception e)
        {
            Debug.LogWarning("[SERVICES] misafir girişi olmadı: " + e.GetType().Name + " " + e.Message);
            if (this == null)
                return;
            SetEntryBusy(false);
            UIRoot.HideLoading();
            UIRoot.Toast(Loc.T("login.connection_failed"));
            return;
        }
        if (this == null)
            return;
        SetEntryBusy(false);
        UIRoot.HideLoading();
        Refresh();
    }

    // İptal ya da hata: seçim ekranında kalınır, misafir hesap açılmaz.
    public async void PlayWithGoogle()
    {
        SetEntryBusy(true);
        UIRoot.ShowLoading(Loc.T("login.signing_in"), false);
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
        UIRoot.HideLoading();
        if (result == LinkResult.Cancelled)
            UIRoot.Toast(Loc.T("toast.google_cancelled"));
        else if (result == LinkResult.Failed)
            UIRoot.Toast(Loc.T("toast.google_failed"));
        Refresh();
    }

    static string ErrorTextOf(NameResult result)
    {
        string key = NameFlow.ErrorText(result);
        return key.Length == 0 ? "" : Loc.T(key);
    }

    public async void ConfirmName()
    {
        nameError.text = "";
        nameOkButton.interactable = false;
        NameResult result;
        try { result = await GameServices.ChooseNameAsync(nameInput.text); }
        catch (Exception e)
        {
            Debug.LogWarning("[SERVICES] isim kaydedilemedi: " + e.GetType().Name + " " + e.Message);
            result = NameResult.Failed;
        }
        if (this == null)
            return;
        nameOkButton.interactable = true;
        nameError.text = ErrorTextOf(result);
        if (result == NameResult.Ok)
            Refresh();
    }

    public async void RetryConnect()
    {
        retryButton.interactable = false;
        UIRoot.ShowLoading(Loc.T("login.connecting"), false);
        retrying = true;
        await PhotonManager.ConnectWhenReadyAsync();
        if (this == null)
            return;
        retryButton.interactable = true;
        initFailed = !GameServices.IsReady;
        photonDropped = false;
        Refresh();
        if (EntryFlow.Next(initDone, initFailed, GameServices.HasSession, GameServices.NeedsName) == EntryFlow.Screen.Retry)
        {
            retrying = false;
            UIRoot.Toast(Loc.T("login.connection_failed"));
        }
    }
}
