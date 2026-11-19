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
        chipText.text = profile.DisplayName + " · Seviye " + profile.Level;
        nameText.text = profile.DisplayName;
        levelText.text = "Seviye " + profile.Level;
        xpText.text = profile.Xp % GameConfig.XpPerLevel + " / " + GameConfig.XpPerLevel + " XP";
        xpBar.fillAmount = profile.Xp % GameConfig.XpPerLevel / (float)GameConfig.XpPerLevel;
        coinText.text = profile.Coins.ToString();
        lobbyCoinText.text = profile.Coins.ToString();
        accountText.text = GameServices.Auth.IsGuest ? "Misafir hesap" : "Google hesabı";
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
            nameError.text = NameFlow.ErrorText(result);
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
                UIRoot.Toast("Google girişi tamamlanmadı");
            else if (result == LinkResult.Failed)
                UIRoot.Toast("Google'a bağlanılamadı");
            // Google hesabına geçildi (bağlandı ya da var olan hesaba geçti) ve isim gerekiyor: isim kutusu açılır
            // (RefreshCurrent), sonraki açılışta da sorulur.
            else if (GameServices.NeedsName)
                UIRoot.Toast("İsmini seçebilirsin");
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
            "Kayıtlı hesap bulundu",
            "Bu Google hesabı başka bir oyuncu profiline bağlı.\nBu cihazdaki misafir ilerleme silinecek, kayıtlı hesabına geçilecek. Devam edilsin mi?"
        );

    public void OpenSettings()
    {
        SyncToggles();
        RefreshCurrent();
        privacyRow.SetActive(GameConfig.PrivacyUrl.Length > 0);
        termsRow.SetActive(GameConfig.TermsUrl.Length > 0);
        supportRow.SetActive(GameConfig.SupportEmail.Length > 0);
        deleteRow.SetActive(GameServices.HasSession);
        settingsPanel.SetActive(true);
    }

    public void CloseSettings() => settingsPanel.SetActive(false);

    public void OnSoundToggle(bool on) => GameSettings.SoundOn = on;

    public void OnMusicToggle(bool on) => GameSettings.MusicOn = on;

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
            if (!await UIRoot.Confirm("Hesabımı Sil",
                    "Hesabın, ismin, seviyen ve coinlerin kalıcı olarak silinir. Satın aldıkların da gider. Geri alınamaz.",
                    "Devam", true) || this == null)
                return;
            if (!await UIRoot.Confirm("Emin misin?", "Hesabın şimdi silinecek.", "Hesabı Sil", true) || this == null)
                return;
            UIRoot.ShowLoading("Hesap siliniyor...", false);
            DeleteResult result;
            // Başka hesap işlemi sürerken çağrı Task dönmeden fırlatır
            try { result = await GameServices.DeleteAccountAsync(); }
            catch (Exception e)
            {
                Debug.LogWarning("[SERVICES] hesap silinemedi: " + e.GetType().Name);
                result = DeleteResult.Failed;
            }
            UIRoot.HideLoading();
            if (this == null)
                return;
            switch (result)
            {
                case DeleteResult.Ok:
                    SceneChangeManager.Instance.ChangeScene("MainMenu");
                    break;
                case DeleteResult.NeedsRecentLogin:
                    UIRoot.Toast("Güvenlik için çıkış yapıp tekrar giriş yap, sonra yeniden dene");
                    break;
                case DeleteResult.DataDeletedAccountRemains:
                    // Servis yeni başlangıç profilini yükledi, oturum sürüyor: lobi onu göstersin
                    UIRoot.Toast("Verilerin silindi ama hesap kapatılamadı. Tekrar giriş yapıp yeniden dene");
                    CloseSettings();
                    SyncNickName();
                    RefreshCurrent();
                    break;
                default:
                    UIRoot.Toast("Hesap silinemedi, tekrar dene");
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
                ? "Misafir hesabından çıkarsan ilerlemen kaybolur."
                : "Hesabından çıkılsın mı?";
            if (!await UIRoot.Confirm("Çıkış yap", body, "Çıkış yap", true) || this == null)
                return;
            signOutButton.interactable = false;
            try { await GameServices.SignOutAsync(); }
            catch (Exception e)
            {
                Debug.LogWarning("[SERVICES] çıkış yapılamadı: " + e.GetType().Name + " " + e.Message);
                if (this == null)
                    return;
                signOutButton.interactable = true;
                UIRoot.Toast("Çıkış yapılamadı");
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
