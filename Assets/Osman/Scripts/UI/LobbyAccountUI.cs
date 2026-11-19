using System;
using System.Threading.Tasks;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Lobi üst çubuğu: profil kutucuğu, Profil ve Ayarlar panelleri, hesap değiştirme onayı, hesaptan çıkış.
// Mantık GameServices'te; bu sınıf yalnız arayüze bağlar. Sahne düğmeleri genel metotları persistent onClick ile çağırır.
public class LobbyAccountUI : MonoBehaviour
{
    [Header("Üst çubuk")]
    public TextMeshProUGUI chipText;
    // Lobideki coin yazısı ShopController'a ait ama market ilk açılana dek abone olmaz: burada da yazılır.
    public TextMeshProUGUI lobbyCoinText;

    [Header("Profil")]
    public GameObject profilePanel;
    public TMP_InputField nameInput;
    public Button nameSaveButton;
    public TextMeshProUGUI nameError;
    public TextMeshProUGUI accountText;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI xpText;
    public TextMeshProUGUI coinText;
    public Button linkGoogleButton;
    public TextMeshProUGUI linkStatus;

    [Header("Hesap değiştirme onayı")]
    public GameObject confirmSwitchPanel;

    [Header("Ayarlar")]
    public GameObject settingsPanel;
    public Toggle soundToggle;
    public Toggle musicToggle;
    public TextMeshProUGUI versionText;
    public Button signOutButton;
    public TextMeshProUGUI signOutStatus;

    TaskCompletionSource<bool> switchAnswer;

    void Awake()
    {
        GameServices.Profile.Changed += Refresh;
        GameServices.Auth.UserChanged += RefreshCurrent;
        GameServices.ConfirmAccountSwitch = AskSwitchAsync;

        nameInput.characterLimit = GameConfig.NameMax;
        nameError.text = "";
        linkStatus.text = "";
        signOutStatus.text = "";
        versionText.text = "Sürüm " + Application.version;
        SyncToggles();
        RefreshCurrent();
    }

    void OnDestroy()
    {
        GameServices.Profile.Changed -= Refresh;
        GameServices.Auth.UserChanged -= RefreshCurrent;
        if (GameServices.ConfirmAccountSwitch == AskSwitchAsync)
            GameServices.ConfirmAccountSwitch = null;
        // Onay beklerken sahne kapanırsa GameServices bekleyip kalmasın: hayır sayılır.
        switchAnswer?.TrySetResult(false);
    }

    void RefreshCurrent() => Refresh(GameServices.Profile.Current);

    // Profil yüklenmediyse (null) yazılara dokunulmaz; düğme görünürlüğü profile bağlı değildir ve her zaman kurulur
    // (çıkış yalnız CanSignOut iken görünür: Firebase misafiri çıkarsa hesabı kalıcı kaybeder).
    void Refresh(PlayerProfile profile)
    {
        linkGoogleButton.gameObject.SetActive(GameServices.UsesFirebase && GameServices.Auth.IsGuest);
        signOutButton.gameObject.SetActive(GameServices.CanSignOut);
        if (profile == null)
            return;
        chipText.text = profile.DisplayName + " · Seviye " + profile.Level;
        levelText.text = "Seviye: " + profile.Level;
        xpText.text = "XP: " + profile.Xp % GameConfig.XpPerLevel + " / " + GameConfig.XpPerLevel;
        coinText.text = "Coin: " + profile.Coins;
        lobbyCoinText.text = profile.Coins.ToString();
        accountText.text = GameServices.Auth.IsGuest ? "Hesap: Misafir" : "Hesap: Google";
        // Yazılırken (ör. Changed başka sebeple gelince) kutudaki metin ezilmesin.
        if (!nameInput.isFocused)
            nameInput.text = profile.DisplayName;
    }

    void SyncToggles()
    {
        soundToggle.SetIsOnWithoutNotify(GameSettings.SoundOn);
        musicToggle.SetIsOnWithoutNotify(GameSettings.MusicOn);
    }

    // Lobi sahnesi masada yüklü olmadığı için isim masada değişemez; Photon adı burada eşitlenir.
    static void SyncNickName()
    {
        var profile = GameServices.Profile.Current;
        if (profile != null)
            PhotonNetwork.NickName = profile.DisplayName;
    }

    public void OpenProfile()
    {
        nameError.text = "";
        linkStatus.text = "";
        nameInput.text = GameServices.Profile.Current?.DisplayName ?? "";
        RefreshCurrent();
        profilePanel.SetActive(true);
    }

    public void CloseProfile() => profilePanel.SetActive(false);

    public async void SaveName()
    {
        try
        {
            if (!GameConfig.IsValidName(nameInput.text))
            {
                nameError.text = "İsim 2-16 karakter olmalı";
                return;
            }
            nameError.text = "";
            nameSaveButton.interactable = false;
            try { await GameServices.Profile.SetDisplayNameAsync(nameInput.text); }
            catch (Exception e)
            {
                Debug.LogWarning("[SERVICES] isim kaydedilemedi: " + e.GetType().Name + " " + e.Message);
                if (this == null)
                    return;
                nameSaveButton.interactable = true;
                nameError.text = "Bağlanılamadı";
                return;
            }
            if (this == null)
                return;
            nameSaveButton.interactable = true;
            SyncNickName();
            RefreshCurrent();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SERVICES] isim kaydı hata: " + e.GetType().Name + " " + e.Message);
        }
    }

    // Düğme iş sürerken kapalı; iptal ya da hata profilde yazıyla gösterilir, oturum değişmez.
    public async void LinkGoogle()
    {
        try
        {
            linkGoogleButton.interactable = false;
            linkStatus.text = "";
            LinkResult result;
            try { result = await GameServices.EnterWithGoogleAsync(); }
            catch (Exception e)
            {
                // Başka hesap işlemi sürüyor ya da Google girişi olup profil yüklenemedi: ikisi de buraya düşer.
                Debug.LogWarning("[SERVICES] Google'a bağlanılamadı: " + e.GetType().Name + " " + e.Message);
                result = LinkResult.Failed;
            }
            if (this == null)
                return;
            linkGoogleButton.interactable = true;
            if (result == LinkResult.Cancelled)
                linkStatus.text = "Google girişi tamamlanmadı";
            else if (result == LinkResult.Failed)
                linkStatus.text = "Google'a bağlanılamadı";
            // Geçiş yarıda kalınca yeni misafire düşülebilir: Photon adı her sonuçta profille eşitlenir.
            SyncNickName();
            RefreshCurrent();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SERVICES] Google bağlama hata: " + e.GetType().Name + " " + e.Message);
        }
    }

    // Google hesabı başka profile bağlıysa GameServices oyuncuya sorar (eski MainMenu deseni).
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

    public void OpenSettings()
    {
        signOutStatus.text = "";
        SyncToggles();
        RefreshCurrent();
        settingsPanel.SetActive(true);
    }

    public void CloseSettings() => settingsPanel.SetActive(false);

    public void OnSoundToggle(bool on) => GameSettings.SoundOn = on;

    public void OnMusicToggle(bool on) => GameSettings.MusicOn = on;

    public async void SignOut()
    {
        try
        {
            signOutButton.interactable = false;
            signOutStatus.text = "";
            try { await GameServices.SignOutAsync(); }
            catch (Exception e)
            {
                Debug.LogWarning("[SERVICES] çıkış yapılamadı: " + e.GetType().Name + " " + e.Message);
                if (this == null)
                    return;
                signOutButton.interactable = true;
                signOutStatus.text = "Çıkış yapılamadı";
                return;
            }
            if (this == null)
                return;
            // Çıkışta Photon bağlantısı kopmaz; yalnız giriş ekranına dönülür, NickName sonraki girişte atanır.
            SceneChangeManager.Instance.ChangeScene("MainMenu");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SERVICES] çıkış hata: " + e.GetType().Name + " " + e.Message);
        }
    }
}
