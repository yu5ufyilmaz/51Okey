# Giriş ve lobi akışı: tasarım ve uygulama planı

Tarih: 2026-11-19. Dal `MDC-001-firebase`. Önceki iş: `docs/superpowers/plans/2026-11-13-google-giris.md`.
Yöneten: ana oturum (Fable): plan, ajan seçimi, doğrulama. İş paketlerini alt ajanlar yapar, sırayla: aynı anda tek
ajan Editörü ve sahneleri kullanır. Her paket için alt ajan `superpowers:subagent-driven-development` düzeniyle çalışır.

**Amaç:** Oyuncu açılışta "Misafir" ya da "Google" seçip doğrudan lobiye girer; isim bir kez sorulur, lobide Profil ve
Ayarlar panelleri vardır, Google hesabından çıkılabilir.

**Yaklaşım:** Servis çekirdeği "açılışta her zaman hesap aç" yerine "kayıtlı oturumu geri yükle, yoksa oyuncuya sor"
olur ve çıkışı öğrenir. `MainMenu` sahnesi giriş ekranına, `LobbyMenu` sahnesi profil ve ayar panellerine kavuşur.
Yeni sahne yok, yeni paket yok.

## Kullanıcının istediği (2026-11-19)

- Açılışta nasıl girileceği sorulur: Misafir / Google. Basınca doğrudan lobi.
- Ana menüdeki isim kutusu kalkar. İsim, hesaba ilk girişte sorulur; sonra Profil'den değişir.
- Lobide Profil ve Ayarlar. Ayarlar'da "Hesaptan çık": oyuncu ana menüye döner.
- Düzen, okey 101 türü oyunlardaki akış gibi (akış örnek alınır, görsel kopyalanmaz).

## Kararlar

Kullanıcının verdiği:
- Oturum varsa açılışta seçim ekranı atlanır, doğrudan lobi. Seçim ekranı yalnız ilk açılışta ve çıkıştan sonra.
- Misafirde "Hesaptan çık" yok (misafir hesaba geri girilemez); misafir yalnız "Google'a bağla" görür.
- Ayarlar'da Ses ve Müzik anahtarları şimdiden var ve kaydedilir (oyunda henüz ses yok).

Ana oturumun verdiği (itiraz edilebilir):
- "İsim seçilmedi" için yeni alan yok: `DisplayName == GameConfig.DefaultName(UserId)` ise isim sorulur.
- Açılışta Google seçilirse misafir hesap açılmaz; doğrudan Google kimliğiyle girilir.
- Lobi düzeni: üst çubukta solda profil kutusu (isim + seviye) Profil panelini, sağda dişli Ayarlar panelini açar.
- İsim 2-16 karakter (kırpılmış). İsim yalnız lobide değişir: masada `NickName + " meld"` aramaları bozulur.
- Çıkışta Photon bağlantısı kopmaz; yalnız sahne `MainMenu`'ye döner ve `NickName` sonraki girişte yeniden atanır.
- Yerel arka uçta (Editör) oturum her zaman vardır: Editör doğrudan lobiye düşer, test araçları değişmez. Giriş ekranını
  Editör'de görmek için yerel arka uçta "Hesaptan çık" misafire de görünür (yerel kimlik sabit, kayıp yok).
- Hesap değiştirme onayı (`ConfirmSwitchPanel`) `MainMenu`'den `LobbyMenu`'ye taşınır: bağlama artık Profil'de yapılır.

Bilinen sınırlar (bu planda çözülmez):
- Play Games v2'de çıkış yoktur. Çıkıştan sonra "Google" yine aynı Google hesabına girer; başka hesap için oyuncu
  Play Games uygulamasından hesap değiştirir.
- Aynı isimli iki oyuncu masada per aramasını bozar (eski hata).
- Hesap silme ekranı (Play politikası gereği yayından önce şart) Faz 7'de.
- Ses sistemi yok: Ses anahtarı `AudioListener.volume`'u ayarlar, Müzik anahtarı yalnız kaydedilir.

## Akış

```
Açılış ─ "Yükleniyor..." ─ GameServices.InitAsync()
   ├─ hata (çevrimdışı) ───────────────► Tekrar dene paneli
   ├─ oturum var ──────────────────────► [isim gerekli mi?] ─► Lobi
   └─ oturum yok ─► [ Misafir olarak oyna ] [ Google ile gir ]
                         │                      │ iptal/hata: ekranda kalır, yazı gösterir
                         ▼                      ▼
                    [isim gerekli mi?] ─ evet ─► İsim paneli (2-16) ─► Lobi
                                       └ hayır ───────────────────────► Lobi

Lobi üst çubuk:  [ Ahmet · Seviye 3 ]                      [ coin ] [ ⚙ ]
   Profil paneli: isim (değiştir), hesap türü, seviye, XP, coin, misafirse "Google'a bağla"
   Ayarlar paneli: Ses, Müzik, sürüm, Google hesabındaysa "Hesaptan çık" ─► MainMenu (seçim ekranı)
```

## Genel kurallar (her pakette geçerli)

- Sır yazma, sır yazdırma. Loga yalnız kullanıcı kimliği girer; token, e-posta, sunucu kodu girmez.
- `git add -A` / `git add .` yok; dosyalar adıyla eklenir. Commit'ten önce `Tools\Security\Check-Secrets.ps1`. Push yok.
- Çalışma ağacında bu işten önce değişmiş dört dosya var (`51Okey.slnx`, `ProjectSettings/PackageManagerSettings.asset`,
  `ProjectSettings/UnityConnectSettings.asset`, `docs/superpowers/plans/2026-11-11-sync-autotest.md`): dokunma.
  `Assets/Osman/Scenes/MainMenu.unity` içindeki bekleyen değişiklik (Google düğmesi açık) İP2'ye dahildir.
- `using Firebase` ve `GooglePlayGames` yalnız `Assets/Osman/Scripts/Services/Firebase/` altında.
- Oyun senkron koduna (`GamePlay/`, `TILES/`) dokunma; tek istisna İP2'de anılan `SyncAutoTest` uyumu.
- Sahne düzenlemesi çalışan Editör üzerinden (`unity command script eval`), tek seferde tek değişiklik, sonra doğrula.
  Arayüzde `unity:ui-ugui` skill'i kullanılır: TextMeshPro, tam ad alanlı tipler, mevcut `btn_teal` ve
  `ChangaOne-Regular SDF` stili. Kanvas referansı 2960x1440.
- Editörü sürme notları: `C:\Users\osman\.claude\projects\F--Documents-UnityProjects-51Okey\memory\` içinde
  `unity-cli-drive-editors.md`, `unity-on-f-drive.md`, `phone-wifi-build.md`.
- Bitince rapor: ne yapıldı, hangi commit, kapıdan ne geçti / geçmedi (çıktısıyla), açık kalanlar.

## Her paketin kapısı

0. `powershell -ExecutionPolicy Bypass -File Tools\Security\Check-Secrets.ps1` temiz.
1. `powershell -ExecutionPolicy Bypass -File Tools\SyncTest\Run-EditModeTests.ps1` yeşil (şu an 130 test).
2. Editör konsolunda bu işten gelen yeni hata yok (bilinen iki EDM hatası hariç).
3. Play modunda paketin "Elle doğrula" adımları, `script eval` ile durum okunarak.
Dört editörlü senkron testi, APK ve telefon denemesi İP4'te.

## Gözden geçirme odağı

Testlerin kendiliğinden yakalamadığı, oyuncuyu en çok vuracak durumlar. Her biri ilgili pakette test ya da elle adımdır.

1. Google girişi iptal edilir ya da başarısız olur: oyuncu seçim ekranında kalır, misafir hesap açılmaz. (İP1 test, İP2 elle)
2. Giriş düğmesine çift dokunuş ya da çevrimdışı açılış: ikinci anonim hesap açılmaz. (İP1 test, İP2 elle)
3. Çıkıştan sonra eski hesabın ismi ve coini hiçbir yerde görünmez; yeni girişte yenisi görünür. (İP1 test, İP3 elle)
4. İsim: boş, yalnız boşluk, 1 harf, 17 harf reddedilir; isim paneli açıkken uygulama kapanırsa sonraki açılışta
   yine sorulur. (İP1 test, İP2 elle)
5. Çıkıp yeniden girince Photon ikinci kez bağlanmaya çalışmaz, hata logu çıkmaz, `NickName` yeni hesabın adıdır. (İP2 elle)

---

## İP1 — Servis çekirdeği: oturum, giriş, çıkış (model: opus)

Dosyalar:
- Değişir: `Assets/Osman/Scripts/Services/IAuthService.cs`, `IProfileStore.cs`, `GameServices.cs`, `GameConfig.cs`,
  `Local/LocalAuthService.cs`, `Local/LocalProfileStore.cs`, `Firebase/FirebaseAuthService.cs`,
  `Firebase/FirestoreProfileStore.cs`
- Yeni: `Assets/Osman/Scripts/Services/GameSettings.cs`
- Değişir (yalnız derlensin ve eski ekran çalışsın diye): `Assets/Osman/Scripts/UI/MainMenuUIHandler.cs`
- Test: `Assets/Osman/Editor/Tests/LocalServicesTests.cs`, `GameServicesTests.cs`, yeni `GameSettingsTests.cs`

Ürettiği arayüz (İP2 ve İP3 bunlara dayanır):

```csharp
public interface IAuthService
{
    string UserId { get; }
    bool IsGuest { get; }
    event Action UserChanged;
    Task<bool> RestoreAsync();            // kayıtlı oturum varsa girer ve true döner; yoksa false, hesap AÇMAZ
    Task SignInGuestAsync();              // eski SignInAsync: anonim hesap açar
    Task<LinkResult> LinkGoogleAsync();   // kullanıcı yoksa doğrudan Google kimliğiyle girer, varsa bağlar
    Task SignOutAsync();
    Task DeleteAccountAsync();
}

public interface IProfileStore { /* mevcutlar */ void Clear(); }   // Current = null, Changed(null)

public static class GameServices
{
    public static bool HasSession { get; }   // Auth.UserId != null && Profile.Current?.UserId == Auth.UserId
    public static bool NeedsName { get; }    // HasSession && DisplayName == GameConfig.DefaultName(Auth.UserId)
    public static bool CanSignOut { get; }   // HasSession && (!Auth.IsGuest || !UsesFirebase)
    public static Task InitAsync();                        // artık: RestoreAsync + varsa profil yükle
    public static Task EnterAsGuestAsync();                // döndüğünde HasSession true; değilse fırlatır
    public static Task<LinkResult> EnterWithGoogleAsync(); // Linked / SwitchedToExistingAccount ise HasSession true
    public static Task SignOutAsync();                     // döndüğünde HasSession false, Profile.Current null
}

public static class GameConfig { public const int NameMin = 2, NameMax = 16; public static bool IsValidName(string name); }

public static class GameSettings   // PlayerPrefs "Okey.Sound", "Okey.Music"; varsayılan açık
{
    public static bool SoundOn { get; set; }   // set: kaydeder ve AudioListener.volume = on ? 1 : 0
    public static bool MusicOn { get; set; }   // set: yalnız kaydeder
    public static void Apply();                // açılışta SoundOn'u AudioListener'a uygular
}
```

Davranış kararları:
- `GameServices.InitAsync` oturum yoksa hata vermez: `IsReady` true olur, `HasSession` false kalır.
- Profil yükleme tek yoldan geçer: `OnUserChanged` yüklemeyi başlatır ve `Task`'ını saklar; `Enter*` metotları o
  `Task`'ı bekler. Aynı kullanıcı için `LoadOrCreateAsync` iki kez koşmaz (başlangıç coini iki kez yazılmasın).
- `OnUserChanged`, `UserId == null` olunca `Profile.Clear()` çağırır.
- `Enter*` ve `SignOutAsync` iç içe çağrıya kapalıdır: biri sürerken ikincisi aynı `Task`'ı alır.
- `FirebaseAuthService.SignOutAsync`: misafirse `InvalidOperationException` (anonim hesap kaybolmasın); değilse
  `auth.SignOut()` + `SetUser(null)`. `RestoreAsync`: `auth.CurrentUser` varsa onu alır, yoksa false.
  `LinkAsync`: `auth.CurrentUser == null` ise `SignInWithCredentialAsync` ile girer ve `Step.Ok` döner.
- `LocalAuthService`: `RestoreAsync` her zaman `"local-" + instanceTag` ile girer ve true döner; `SignInGuestAsync`
  aynı kimliği verir; `SignOutAsync` kimliği null yapar. Yerel profil dosyası çıkışta silinmez.
- `SetDisplayNameAsync` iki uygulamada da `GameConfig.IsValidName` kullanır (geçersizse eskisi gibi sessizce yazmaz).
- `MainMenuUIHandler` bu pakette yalnız köprülenir: `Start` içinde `InitAsync`'ten sonra `!HasSession` ise
  `EnterAsGuestAsync` çağrılır. Ekran İP2'de yeniden yazılır.

Adımlar:
- [ ] Testleri yaz (önce kırmızı):
  - `LocalServicesTests`: `Restore_SignsInAndReturnsTrue`; `SignOut_ClearsUserAndFiresUserChanged`;
    `SignOutThenGuest_GivesSameLocalId`; `Clear_NullsCurrentAndNotifiesNull`; `Clear_KeepsFileOnDisk`;
    `SetDisplayName_Rejects17Chars` (isim değişmez); mevcut `SignInAsync` çağrıları `SignInGuestAsync` olur.
  - `GameServicesTests`: `IsValidName` tablosu: `null`, `""`, `"  "`, `"a"`, `" a "` yanlış; `"ab"`, `" ab "`,
    16 harf doğru; 17 harf yanlış. `EnterAsGuest_GivesSession`; `SignOut_DropsSessionAndProfile` (ardından
    `EnterAsGuestAsync` ile geri gir: dosya gerçek yerel profil, test onu bozmadan bırakmalı);
    `EnterAsGuest_TwiceInOneFrame_ReturnsSameTask`; `NeedsName_TrueForDefaultName_FalseAfterRename` (ismi geri al).
  - `GameSettingsTests`: varsayılan açık; `SoundOn = false` kalıcı ve `AudioListener.volume == 0`; test sonunda
    iki anahtarı sil.
  - Google yolu için `GoogleLinkFlowTests`'e dokunma: `RunAsync` imzası değişmiyor.
- [ ] `Run-EditModeTests.ps1` çalıştır: yeni testler kırmızı (derleme hatası ya da FAIL).
- [ ] Arayüzleri ve uygulamaları yukarıdaki imzalarla yaz.
- [ ] `Run-EditModeTests.ps1` yeşil; `Check-Secrets.ps1` temiz.
- [ ] Elle doğrula (Editör, yerel arka uç, play modu): `GameServices.HasSession` true, eski ana menüden lobiye girilir.
- [ ] Commit: `İP1: oturum geri yükleme, misafir/Google girişi, çıkış`.

## İP2 — Giriş ekranı (model: opus)

Bağlı: İP1. Dosyalar:
- Yeniden yazılır: `Assets/Osman/Scripts/UI/MainMenuUIHandler.cs`
- Değişir: `Assets/Osman/Scripts/Photons/PhotonManager.cs`, `Assets/Osman/Scenes/MainMenu.unity`
- Gerekirse: `Assets/Osman/Scripts/GamePlay/SyncAutoTest.cs` (yalnız aşağıdaki uyum)
- Test: yeni `Assets/Osman/Editor/Tests/EntryFlowTests.cs`

Kullandığı: İP1'deki `GameServices.InitAsync / HasSession / NeedsName / EnterAsGuestAsync / EnterWithGoogleAsync`,
`GameConfig.IsValidName`, `GameSettings.Apply`.

Ürettiği:
```csharp
// Karar saf kodda, sahneden bağımsız test edilir (GoogleLinkFlow ile aynı desen).
public static class EntryFlow
{
    public enum Screen { Loading, Retry, Choose, AskName, Lobby }
    public static Screen Next(bool initDone, bool initFailed, bool hasSession, bool needsName);
}
// MainMenuUIHandler genel metotları (sahne onClick): PlayAsGuest(), PlayWithGoogle(), ConfirmName(), RetryConnect()
```

Sahne (`Canvas/MenuBackgorund` altında, mevcut `GameName` kalır):
- Silinir: `InputFieldBackground`, `ConnectButton`, `AccountStatus`, `ConfirmSwitchPanel` (İP3 lobide yeniden kurar).
  Silmeden önce `MainMenuUIHandler` alan başvuruları kaldırılır. `AdsButtons` ve `RetryPanel` kalır.
- `LoadingText` ("Yükleniyor..."), `ChoicePanel` (`GuestButton` "Misafir olarak oyna", `GoogleButton` "Google ile gir",
  `StatusText`), `NamePanel` (başlık "Oyuncu adın", `NameInput` karakter sınırı 16, `NameError`, `NameOkButton` "Tamam").
- `GoogleButton` yalnız `GameServices.UsesFirebase` iken görünür. Tüm paneller sahnede açık durur (Editör'de
  görünür); hangisinin görüneceğine `Awake` karar verir.

Davranış:
- `Start`: `GameSettings.Apply()`, sonra `InitAsync`. Ekran her adımda `EntryFlow.Next` ile seçilir.
- Giriş düğmeleri iş sürerken `interactable = false`. Google sonucu `Cancelled` ise "Google girişi tamamlanmadı",
  `Failed` ise "Google'a bağlanılamadı"; oyuncu seçim ekranında kalır. `EnterAsGuestAsync` fırlatırsa "Bağlanılamadı".
- İsim: `IsValidName` değilse `NameError` "İsim 2-16 karakter olmalı"; geçerliyse `SetDisplayNameAsync` beklenir.
- Lobiye geçiş tek yerde: `PhotonNetwork.InLobby` olana kadar "Bağlanıyor..." (bekleme, sessiz yutma yok), sonra
  `PhotonNetwork.NickName = GameServices.Profile.Current.DisplayName`, `SceneChangeManager.Instance.ChangeScene("LobbyMenu")`.
- `PhotonManager.ConnectWhenReadyAsync`: `PhotonNetwork.IsConnected` ise `ConnectUsingSettings` çağrılmaz (çıkıştan
  sonra `MainMenu` yeniden yüklenir). Bağlanmak artık oturum istemez: init başarılıysa bağlanır.
- `SyncAutoTest` etkinken `MainMenuUIHandler` kendiliğinden lobiye geçmez ve isim sormaz (isim ve sahne geçişi
  `SyncAutoTest`'in işidir). Koşul, `GameServices`'in kullandığı `SyncAutoTestFiles.TryLoadConfig` ile aynıdır.

Adımlar:
- [ ] `EntryFlowTests` yaz: `Next(false,false,*,*) == Loading`; `Next(true,true,*,*) == Retry`;
  `Next(true,false,false,*) == Choose`; `Next(true,false,true,true) == AskName`; `Next(true,false,true,false) == Lobby`.
- [ ] Testi çalıştır: kırmızı. `EntryFlow`'u yaz: yeşil.
- [ ] `MainMenuUIHandler`'ı yeniden yaz; `PhotonManager` korumasını ekle.
- [ ] Sahneyi düzenle (her değişiklikten sonra hiyerarşiyi oku), onClick bağlantılarını kur, sahneyi kaydet.
- [ ] Elle doğrula (Editör, yerel): play → isim paneli (yerel profil varsayılan isimdeyse) → "a" reddedilir → geçerli
  isim → lobi, `PhotonNetwork.NickName` profil ismiyle aynı. Yeniden play → doğrudan lobi. Konsolda yeni hata yok.
- [ ] Elle doğrula (Editör, `Okey.UseFirebaseInEditor` açık, yeni Firebase kullanıcısı): seçim ekranı görünür,
  "Misafir"e iki kez hızlı basılır → tek anonim hesap, lobi. (Editör'de Google düğmesi `Failed` verir: yazı görünür,
  ekranda kalınır.) Sonra tercihi kapat.
- [ ] `Run-EditModeTests.ps1` yeşil; `Check-Secrets.ps1` temiz.
- [ ] Commit: `İP2: giriş ekranı (misafir / Google), isim paneli`.

## İP3 — Lobi: profil ve ayarlar (model: sonnet)

Bağlı: İP1, İP2. Dosyalar:
- Yeni: `Assets/Osman/Scripts/UI/LobbyAccountUI.cs`
- Değişir: `Assets/Osman/Scenes/LobbyMenu.unity`
- Dokunma: `ButtonFuncs.cs`, `RoomManager.cs`, `ShopController.cs` (coin yazısı zaten profilden besleniyor).

Kullandığı: `GameServices.Profile` (`Current`, `Changed`, `SetDisplayNameAsync`), `Auth.IsGuest`, `Auth.LinkGoogleAsync`,
`ConfirmAccountSwitch`, `CanSignOut`, `SignOutAsync`, `UsesFirebase`, `GameConfig.IsValidName`, `GameSettings`.

`LobbyAccountUI` genel metotları (sahne onClick): `OpenProfile()`, `CloseProfile()`, `SaveName()`, `LinkGoogle()`,
`ConfirmSwitchYes()`, `ConfirmSwitchNo()`, `OpenSettings()`, `CloseSettings()`, `OnSoundToggle(bool)`,
`OnMusicToggle(bool)`, `SignOut()`.

Sahne (lobi kanvasının üstünde; mevcut oda listesi, market ve coin yerinde kalır):
- `ProfileChip` (sol üst, köşeye çapalı): isim + "Seviye N". `SettingsButton` (sağ üst, dişli; ikon yoksa "Ayarlar" yazılı düğme).
- `ProfilePanel`: `NameInput` (sınır 16) + "Kaydet" + `NameError`; "Hesap: Misafir" / "Hesap: Google"; Seviye; XP
  (`Xp % GameConfig.XpPerLevel` / `XpPerLevel`); Coin; `LinkGoogleButton` "Google'a bağla" (yalnız `UsesFirebase && IsGuest`);
  `LinkStatus`; "Kapat".
- `ConfirmSwitchPanel`: İP2'de `MainMenu`'den silinenin metni ve iki düğmesiyle aynı.
- `SettingsPanel`: `SoundToggle` "Ses", `MusicToggle` "Müzik", `VersionText` ("Sürüm " + `Application.version`),
  `SignOutButton` "Hesaptan çık" (yalnız `CanSignOut`), "Kapat".
- Paneller tam ekran yarı saydam arka planla açılır; arka plan tıklamaları alttaki lobiye geçmez.

Davranış:
- `Awake`: `Profile.Changed` ve `Auth.UserChanged`'a abone ol, `ConfirmAccountSwitch`'i ata; `OnDestroy`'da hepsini bırak
  (eski `MainMenuUIHandler` deseni). Kutucuk ve panel yazıları her `Changed`'da yenilenir; `profile == null` ise dokunma.
- `SaveName`: `IsValidName` değilse hata yazısı; geçerliyse `SetDisplayNameAsync` beklenir, sonra
  `PhotonNetwork.NickName = yeni isim`. Lobi sahnesi masada yüklü olmadığı için isim masada değişemez.
- `LinkGoogle`: düğme iş sürerken kapalı; sonuç yazıları İP2'deki gibi. `Linked` ya da `SwitchedToExistingAccount`
  sonrası panel yenilenir ve `PhotonNetwork.NickName` profil ismine eşitlenir.
- `SignOut`: düğme kapanır, `GameServices.SignOutAsync()` beklenir, `SceneChangeManager.Instance.ChangeScene("MainMenu")`.
  Hata olursa düğme açılır ve "Çıkış yapılamadı" yazılır.
- Anahtarlar açılışta `GameSettings` değerini gösterir; değişince yazar.

Adımlar:
- [ ] `LobbyAccountUI.cs` yaz (mantık İP1'de testli; bu sınıf yalnız bağlar).
- [ ] Sahneyi kur, başvuruları ve onClick'leri bağla, kaydet. EventSystem ve GraphicRaycaster'ın var olduğunu doğrula.
- [ ] Elle doğrula (Editör, yerel): lobide kutucuk ismi ve seviyeyi gösterir → Profil → isim "a" reddedilir → yeni isim
  kaydedilir, kutucuk ve `PhotonNetwork.NickName` güncellenir → Ayarlar → Ses kapat, play'i durdur/başlat, kapalı gelir
  → "Hesaptan çık" → `MainMenu` seçim ekranı, coin/isim görünmez → "Misafir" → lobi, aynı isim. Konsolda yeni hata yok.
- [ ] Oda kur ve masaya gir, masadan lobiye dön: kutucuk hâlâ doğru, paneller açılır (dinleyici birikmesi yok).
- [ ] `Run-EditModeTests.ps1` yeşil; `Check-Secrets.ps1` temiz.
- [ ] Commit: `İP3: lobi profil ve ayarlar panelleri, hesaptan çık`.

## İP4 — Doğrulama (ana oturum)

- [ ] Dört editörlü senkron testi: `Tools\SyncTest\Run-SyncTest.ps1 -Mode smart -Seed 11` (arka planda). Klonlar
  kapalıysa `-Solo`. Giriş ekranı test oyuncularını takmamalı.
- [ ] `AndroidDevBuild.Build()` ile APK; telefona kur (adb eşleştirmesi düşmüş: önce yeni eşleme kodu gerekir).
- [ ] Telefon, temiz kurulum (uygulama verisi silinmiş): seçim ekranı → "Google ile gir" → isim paneli (hesap zaten
  isimliyse atlanır) → lobi. Uygulamayı kapat/aç → doğrudan lobi. Ayarlar → "Hesaptan çık" → seçim ekranı →
  "Misafir olarak oyna" → isim → lobi; Ayarlar'da "Hesaptan çık" yok; Profil → "Google'a bağla" → hesap değiştirme
  onayı çıkar.
- [ ] Sonuç bu dosyanın sonuna "Sonuç" başlığıyla yazılır; hafıza notu güncellenir.

## Sonuç (2026-11-19)

Commit'ler (`MDC-001-firebase`, push yok): İP1 `5fa018a` + `9f076a9`, İP2 `13ecc67` + `a2a475c`, İP3 `f17577a` + `07df17f`,
son gözden geçirme düzeltmeleri `3733b34`.

Kapıdan geçen:
- EditMode testleri 159/159 (130'dan; 29 yeni). `Check-Secrets.ps1` her commit'te temiz.
- Tek editörlü senkron testi (`Run-SyncTest.ps1 -Solo -Mode smart -Seed 11`) `07df17f` üzerinde geçti: 1 el, 0 desync.
  Ondan önceki bir koşu `AT-host 60 sn'de masaya oturamadı: state=Joining` ile kaldı ve tekrarlanmadı; nedeni kanıtlanmadı.
- `AndroidDevBuild.Build()` `3733b34` üzerinde APK üretti (211 sn, 0 hata). İlk deneme Gradle'ın bayat ara dosyalarına
  takılmıştı (`:launcher:compressDebugAssets ... already contains entry`); `Library/Bee/.../compressed_assets` silinince geçti.
- Editörde elle (yerel arka uç): isim paneli ve kuralı, doğrudan lobi, çıkış → seçim ekranı → yeniden giriş, Photon
  koparsa Tekrar dene, profil ve ayarlar panelleri, ses ayarının kalıcılığı, oda → masa → lobi.
- Editörde Firebase ile: seçim ekranı, çift dokunuşta tek anonim hesap, Google hatasında ekranda kalma.

Yapılmayan / doğrulanmayan:
- Dört editörlü senkron testi (klonlar kapalıydı; yalnız tek editörlü koşuldu).
- Telefon: APK kurulmadı. adb eşleştirmesi düşmüş (port açık, `failed to connect`); yeni eşleme kodu gerekiyor.
  Cihazda Google ile giriş / bağlama / hesap geçişi, Firebase çıkışı ve çevrimdışı açılış hiç denenmedi.

Plandan sapmalar:
- Lobi kanvası 1920x1080 ve `SafeArea` altında (plandaki 2960x1440 yalnız `MainMenu` içindi). Profil kutusu ve Ayarlar
  düğmesi kendi `SafeArea`'lı `AccountBar` nesnesinde; ikisi de 50 px yüksek (mevcut düğmelerin üstünde 55 px yer vardı).
- "Google'a bağla" `Auth.LinkGoogleAsync` yerine `GameServices.EnterWithGoogleAsync` çağırır (tekil ve profili bekler).
- Photon bağlanamazsa süresiz bekleme yerine Tekrar dene paneli açılır (plan yalnız "bekle" diyordu).
- Lobi coin yazısı `LobbyAccountUI` tarafından da yazılır (market açılmadan "0" görünüyordu; `ShopController` değişmedi).

Açık kalanlar:
- MARKET düğmesi marketi açmıyor olabilir: `ShopController.OpenShop` yalnız `ShopBackground`'u açıyor, sahnede kapalı
  duran `ShopCanvas`'ı açan bir şey görünmüyor. Bu işten önce de böyleydi; doğrulanmadı.
- Firestore yazma hatası yutuluyor: isim kaydı başarısız olursa oyuncuya "Bağlanılamadı" gösterilmiyor.
- Play Games geri çağrısı hiç gelmezse giriş düğmeleri uygulama yeniden açılana kadar kapalı kalır (zaman aşımı yok).
- ChangaOne fontunda İ/ş/ğ yok, yedek fonta düşüyor (eski durum).
- Test verisi: yerel profil adı "Deneme Oyuncu"; geliştirme Firebase projesinde "Deneme FB" adlı anonim kullanıcı kaldı.

### Telefon (2026-11-19 04:40, `3733b34` APK'sı, eski verinin üstüne kuruldu)

- adb yeniden eşleştirildi, APK kuruldu. Açılış: oturum geri yüklendi, seçim ekranı atlandı, 10 saniyede lobi. Play Games hatası yok.
- Kullanıcı cihazda sırayla denedi ve logda doğrulandı: Hesaptan çık → Google ile gir (aynı hesap, profil yüklendi, lobi) →
  Hesaptan çık → Misafir olarak oyna (yeni anonim hesap, 1000 coin).
- Cihazda hâlâ denenmedi: misafirken Profil'den "Google'a bağla" ve hesap değiştirme onayı, çevrimdışı açılış.
