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
    // Lobideki coin yazısı ShopController'a da ait (sahne açılınca abone olur); profil değişince burada da yazılır.
    public TextMeshProUGUI lobbyCoinText;

    [Header("Profil")]
    public GameObject profilePanel;
    public TMP_InputField nameInput;
    public Button nameSaveButton;
    public TextMeshProUGUI nameError;
    // Misafir ismini seçemez: kutu yerine düz isim ve "Google'a bağlan" ipucu görünür.
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI nameHint;
    public TextMeshProUGUI accountText;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI xpText;
    public TextMeshProUGUI coinText;
    public Image xpBar;
    public Button linkGoogleButton;
    public Button signOutButton;

    [Header("Ayarlar")]
    public GameObject settingsPanel;
    public Toggle soundToggle;
    public Toggle musicToggle;
    public TMP_Text languageValue;
    public TextMeshProUGUI versionText;
    // Adres boşsa bağlantı satırı gizli; silme satırı oturum varken görünür.
    public GameObject privacyRow, termsRow, supportRow, deleteRow;

    void Awake()
    {
        GameServices.Profile.Changed += Refresh;
        GameServices.Auth.UserChanged += RefreshCurrent;
        GameServices.ConfirmAccountSwitch = AskSwitchAsync;

        nameInput.characterLimit = GameConfig.NameMax;
        nameError.text = "";
        SyncToggles();
        RefreshLanguage();
    }

    void OnEnable() => Loc.Changed += RefreshLanguage;

    void OnDisable() => Loc.Changed -= RefreshLanguage;

    // Dil değişince dile bağlı her yazı yeniden yazılır
    void RefreshLanguage()
    {
        languageValue.text = Loc.NameOf(Loc.Language);
        versionText.text = Loc.T("settings.version", Application.version);
        RefreshCurrent();
    }

    void OnDestroy()
    {
        GameServices.Profile.Changed -= Refresh;
        GameServices.Auth.UserChanged -= RefreshCurrent;
        if (GameServices.ConfirmAccountSwitch == AskSwitchAsync)
            GameServices.ConfirmAccountSwitch = null;
    }

    void RefreshCurrent() => Refresh(GameServices.Profile.Current);

    // Profil yüklenmediyse (null) yazılara dokunulmaz; düğme görünürlüğü profile bağlı değildir ve her zaman kurulur
    // (çıkış yalnız CanSignOut iken görünür: Firebase misafiri çıkarsa hesabı kalıcı kaybeder).
    void Refresh(PlayerProfile profile)
    {
        linkGoogleButton.gameObject.SetActive(GameServices.UsesFirebase && GameServices.Auth.IsGuest);
        signOutButton.gameObject.SetActive(GameServices.CanSignOut);
        bool canChangeName = GameServices.CanChangeName;
        nameInput.gameObject.SetActive(canChangeName);
        nameSaveButton.gameObject.SetActive(canChangeName);
        nameError.gameObject.SetActive(canChangeName);
        nameText.gameObject.SetActive(!canChangeName);
        nameHint.gameObject.SetActive(!canChangeName);
        if (profile == null)
            return;
        chipText.text = Loc.T("profile.chip", profile.DisplayName, profile.Level);
        nameText.text = profile.DisplayName;
        levelText.text = Loc.T("profile.level", profile.Level);
        xpText.text = Loc.T("profile.xp", profile.Xp % GameConfig.XpPerLevel, GameConfig.XpPerLevel);
        xpBar.fillAmount = profile.Xp % GameConfig.XpPerLevel / (float)GameConfig.XpPerLevel;
        coinText.text = profile.Coins.ToString();
        lobbyCoinText.text = profile.Coins.ToString();
        accountText.text = GameServices.Auth.IsGuest ? Loc.T("account.type_guest") : Loc.T("account.type_google");
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
        nameInput.text = GameServices.Profile.Current?.DisplayName ?? "";
        RefreshCurrent();
        profilePanel.SetActive(true);
    }

    public void CloseProfile() => profilePanel.SetActive(false);

    public async void SaveName()
    {
        try
        {
            nameError.text = "";
            nameSaveButton.interactable = false;
            NameResult result;
            try { result = await GameServices.ChooseNameAsync(nameInput.text); }
            catch (Exception e)
            {
                Debug.LogWarning("[SERVICES] isim kaydedilemedi: " + e.GetType().Name + " " + e.Message);
                result = NameResult.Failed;
            }
            if (this == null)
                return;
            nameSaveButton.interactable = true;
            nameError.text = NameFlow.ErrorText(result) is { Length: > 0 } key ? Loc.T(key) : "";
            if (result != NameResult.Ok)
                return;
            SyncNickName();
            RefreshCurrent();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SERVICES] isim kaydı hata: " + e.GetType().Name + " " + e.Message);
        }
    }

    // Düğme iş sürerken kapalı; iptal ya da hata bildirimle gösterilir, oturum değişmez.
    public async void LinkGoogle()
    {
        try
        {
            linkGoogleButton.interactable = false;
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
                UIRoot.Toast(Loc.T("toast.google_cancelled"));
            else if (result == LinkResult.Failed)
                UIRoot.Toast(Loc.T("toast.google_failed"));
            // Google hesabına geçildi (bağlandı ya da var olan hesaba geçti) ve isim gerekiyor: isim kutusu açılır
            // (RefreshCurrent), sonraki açılışta da sorulur.
            else if (GameServices.NeedsName)
                UIRoot.Toast(Loc.T("toast.choose_name"));
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
    Task<bool> AskSwitchAsync() =>
        UIRoot.Confirm(
            Loc.T("account.switch_title"),
            Loc.T("account.switch_body")
        );

    public void OpenSettings()
    {
        SyncToggles();
        RefreshCurrent();
        privacyRow.SetActive(GameConfig.PrivacyUrl.Length > 0);
        termsRow.SetActive(GameConfig.TermsUrl.Length > 0);
        // Gizlilik ve Şartlar ortak satırda; ikisi de gizliyse satır boşluk bırakmasın
        privacyRow.transform.parent.gameObject.SetActive(privacyRow.activeSelf || termsRow.activeSelf);
        supportRow.SetActive(GameConfig.SupportEmail.Length > 0);
        deleteRow.SetActive(GameServices.HasSession);
        settingsPanel.SetActive(true);
    }

    public void CloseSettings() => settingsPanel.SetActive(false);

    public void OnSoundToggle(bool on) => GameSettings.SoundOn = on;

    public void OnMusicToggle(bool on) => GameSettings.MusicOn = on;

    public void LanguagePrev() => ChangeLanguage(-1);

    public void LanguageNext() => ChangeLanguage(1);

    void ChangeLanguage(int step)
    {
        Loc.Language = Loc.Next(step);
        GameSettings.Language = Loc.Language;
    }

    public void OpenPrivacy() => OpenUrl(GameConfig.LegalUrl("privacy"));

    public void OpenTerms() => OpenUrl(GameConfig.LegalUrl("terms"));

    public void OpenSupport()
    {
        if (GameConfig.SupportEmail.Length > 0)
            Application.OpenURL("mailto:" + GameConfig.SupportEmail);
    }

    static void OpenUrl(string url)
    {
        if (!string.IsNullOrEmpty(url))
            Application.OpenURL(url);
    }

    // İki onay; yükleme katmanı (tam ekran, zaman aşımsız) iş bitene dek lobiye dokunulmasını engeller ve her yolda kapanır.
    public async void DeleteAccount()
    {
        try
        {
            if (!await UIRoot.Confirm(Loc.T("settings.delete_account"),
                    Loc.T("account.delete_body"),
                    Loc.T("account.delete_continue"), true) || this == null)
                return;
            if (!await UIRoot.Confirm(Loc.T("account.delete_sure_title"), Loc.T("account.delete_sure_body"), Loc.T("account.delete_sure_yes"), true) || this == null)
                return;
            // Silme sürerken uygulamadan çıkılırsa sonraki açılış sessizce başlangıç profili kurar: geri tuşu kapalı.
            UIRoot.BackBlocked = true;
            UIRoot.ShowLoading(Loc.T("account.deleting"), false);
            DeleteResult result;
            try
            {
                // Başka hesap işlemi sürerken çağrı Task dönmeden fırlatır
                try { result = await GameServices.DeleteAccountAsync(); }
                catch (Exception e)
                {
                    Debug.LogWarning("[SERVICES] hesap silinemedi: " + e.GetType().Name);
                    result = DeleteResult.Failed;
                }
            }
            finally { UIRoot.BackBlocked = false; }
            UIRoot.HideLoading();
            if (this == null)
                return;
            switch (result)
            {
                case DeleteResult.Ok:
                    SceneChangeManager.Instance.ChangeScene("MainMenu");
                    break;
                // Oyuncunun yapacağı iş anlatılıyor: kısa bildirim yerine okunup kapatılan pencere
                case DeleteResult.NeedsRecentLogin:
                    await UIRoot.Notice(Loc.T("account.relogin_title"),
                        Loc.T("account.relogin_body"));
                    break;
                case DeleteResult.DataDeletedAccountRemains:
                    // Servis yeni başlangıç profilini yükledi, oturum sürüyor: lobi onu göstersin
                    CloseSettings();
                    SyncNickName();
                    RefreshCurrent();
                    await UIRoot.Notice(Loc.T("account.close_failed_title"),
                        Loc.T("account.close_failed_body"));
                    break;
                default:
                    UIRoot.Toast(Loc.T("account.delete_failed"));
                    break;
            }
        }
        catch (Exception e)
        {
            UIRoot.HideLoading();
            Debug.LogWarning("[SERVICES] hesap silme hata: " + e.GetType().Name);
        }
    }

    public async void SignOut()
    {
        try
        {
            string body = GameServices.Auth.IsGuest
                ? Loc.T("account.sign_out_guest")
                : Loc.T("account.sign_out_google");
            if (!await UIRoot.Confirm(Loc.T("account.sign_out"), body, Loc.T("account.sign_out"), true) || this == null)
                return;
            signOutButton.interactable = false;
            try { await GameServices.SignOutAsync(); }
            catch (Exception e)
            {
                Debug.LogWarning("[SERVICES] çıkış yapılamadı: " + e.GetType().Name + " " + e.Message);
                if (this == null)
                    return;
                signOutButton.interactable = true;
                UIRoot.Toast(Loc.T("toast.sign_out_failed"));
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
