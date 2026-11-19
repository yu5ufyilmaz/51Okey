# Giriş ekranı — uygulama planı (paket 2/4)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `MainMenu` sahnesi ortak UI kitinden yeniden dizilir: oyun adı, Google ve Misafir butonları, şartlar satırı, sürüm; isim ve "tekrar dene" ortak pencere kalıbında; bekleme ve hatalar `UIRoot` üzerinden.

**Architecture:** Sahnenin Canvas içeriği tek bir Editor betiğiyle (`UiKitBuilder.BuildLoginScreen`) sıfırdan kurulur ve `MainMenuUIHandler` alanları aynı betikte bağlanır; sahne elle dizilmez, yeniden kurulabilir. `MainMenuUIHandler`'ın akışı (`EntryFlow`, `GameServices` çağrıları, sırası) aynı kalır; yalnızca ne gösterdiği değişir. `UIRoot` iki küçük ek alır: zaman aşımsız yükleme ve kapatılamayan pencerede geri tuşunun çıkış onayına düşmesi.

**Tech Stack:** Unity uGUI 2.0, TextMeshPro, Photon PUN 2, NUnit EditMode testleri (`Assets/Osman/Editor/Tests`), `unity` CLI + `com.unity.pipeline`.

**Spec:** `docs/superpowers/specs/2026-11-19-menu-ui-yenileme-design.md` — "Paket 2 — Giriş ekranı" ve "Paket 1'den devreden notlar → Paket 2 — giriş".

Önceki paket: `docs/superpowers/plans/2026-11-19-ui-kit-ve-kalici-katman.md` (bitti, `5efc977..5d31ab7`). Yöneten: ana oturum (Fable) planlar ve doğrular; görevleri alt ajanlar sırayla yapar.

## Global Constraints

- `EntryFlow`, `GameServices`, `GoogleLinkFlow`, `NameFlow` mantığına dokunulmaz. `MainMenuUIHandler`'da hangi durumda hangi ekranın açıldığı ve servis çağrılarının sırası değişmez.
- Senkron testi (`SyncAutoTest`, `autoTest` dalı) açılışta hiçbir pencereye ya da onaya takılmaz.
- `Debug.LogError` eklenmez (senkron test koşucusu hata logunu FAIL sayar); beklenen durumlar `LogWarning`.
- Yeni paket, tween kütüphanesi, `[PunRPC]` yok. Yeni görsel dosyası indirilmez ya da eklenmez.
- Sahne YAML'ı elle yazılmaz; sahne yalnızca Editor betiğiyle kurulur. CLI: `C:\Users\osman\AppData\Local\Unity\bin\unity.exe command ... --project-path F:\Documents\UnityProjects\51Okey --result-only --no-banner`.
- Prefab'lar `Assets/Osman/Prefabs/UI/` altından örneklenir (`PrefabUtility.InstantiatePrefab`), kopyalanmaz. Prefab'ların kendisi bu pakette değişmez.
- Yazı tipi: başlık ve buton `Assets/Fonts/Changa-ExtraBold SDF.asset`, uzun metin `Assets/Fonts/Changa-Bold SDF.asset`. `font` atandıktan sonra gerekiyorsa `fontSharedMaterial` yeniden atanır. Emoji ve simge glifi yok.
- Canvas: Scale With Screen Size, referans 1920x1080, Match 0.5. Arka plan dışındaki her şey `Crystal.SafeArea` taşıyan bir çocuğun altındadır.
- Dokunma alanı en az 120 birim yükseklik.
- Oyuncuya görünen metinler bu plandaki gibi, Türkçe. Yorumlar Türkçe ve seyrek; mevcut yorumlar, anlattıkları satır silinmedikçe kalır.
- `Assets/Fonts/Changa-*.asset` dosyalarını play modu değiştirirse `git checkout` ile geri alınır, commit edilmez.
- Commit: görev başına bir tane, Türkçe tek satır + `Co-Authored-By: Claude <noreply@anthropic.com>`. Push yok. Kullanıcının değişmiş dosyaları (`51Okey.slnx`, `GooglePlayGamesPlugin_v2.2.1.txt`, `ProjectSettings/*`, `plans/2026-11-11-sync-autotest.md`) sahnelenmez; `git add -A` kullanılmaz.

## Review Focus

1. Yavaş bağlantıda açılış 15 saniyeyi aşar: oyuncu hâlâ bağlanırken "Bağlantı zaman aşımına uğradı" bildirimi çıkmaz. → Görev 1, `UiLoadingTimerTests.NoTimeout_NeverFires`.
2. Google hesap seçici iptal edilir ya da hata verir: yükleme kapanır, iki buton yeniden basılabilir, bildirim çıkar; oyuncu seçim ekranında kalır. → Görev 2, Adım 6.
3. Lobiye girerken Photon kopar: yükleme kapanır, "Bağlanılamadı" penceresi açılır; "TEKRAR DENE" yeniden bağlanır. → Görev 2, Adım 6.
4. Gizlilik ya da şartlar adresi boş: şartlar satırı hiç görünmez, ölü bağlantı kalmaz. → Görev 1, `GameConfigTests.LegalUrl_*`; Görev 2, Adım 6.
5. Yerel arka uç (Editör): Google butonu gizli, Misafir butonu tek başına ortalı durur. → Görev 2, Adım 6.
6. İsim penceresinde geri tuşu: pencere kapanmaz, çıkış onayı açılır; "Vazgeç" isim penceresine döndürür. → Görev 1, Adım 5.
7. Sahne geçişi başlarken açık olan yüklemenin metni silinmez. → Görev 1, Adım 5.

---

### Task 1: `UIRoot` ekleri ve adres ayarları

**Files:**
- Modify: `Assets/Osman/Scripts/UI/Kit/UiLoadingTimer.cs`, `Assets/Osman/Scripts/UI/Kit/UIRoot.cs`
- Modify: `Assets/Osman/Scripts/Services/GameConfig.cs`
- Test: `Assets/Osman/Editor/Tests/UiLoadingTimerTests.cs`, `Assets/Osman/Editor/Tests/GameConfigTests.cs`

**Interfaces:**
- Produces:
  - `UiLoadingTimer.Show(float now, bool timeout = true)` — `timeout == false` iken `Tick` hiç `true` dönmez; `Active` yine `true`.
  - `UIRoot.ShowLoading(string text = null, bool timeout = true)` — yükleme zaten açıkken `text == null` gelirse görünen metin korunur; sayaç her çağrıda yeniden başlar ve o çağrının `timeout` değerini alır.
  - Geri tuşu, en üstteki pencere `closable == false` ise: pencere yığında kalır ve çıkış onayı açılır (Table sahnesinde masadan ayrılma onayı, diğerlerinde "Oyundan çıkılsın mı?"); onay zaten açıksa ikincisi açılmaz.
  - `GameConfig.PrivacyUrl`, `TermsUrl`, `AccountDeletionUrl`, `SupportEmail` — hepsi `public const string`, şimdilik `""`. `public static string GameConfig.LegalUrl(string linkId)` — `"privacy"` → `PrivacyUrl`, `"terms"` → `TermsUrl`, diğerleri `""`. `public static bool GameConfig.HasLegalLinks` — ikisi de boş değilse `true`.

- [ ] **Step 1: Testleri yaz.**

```csharp
// UiLoadingTimerTests.cs içine
[Test] public void NoTimeout_NeverFires()
{
    var t = new UiLoadingTimer(); t.Show(0f, false);
    Assert.IsTrue(t.Active);
    Assert.IsFalse(t.Tick(9999f)); Assert.IsTrue(t.Active);
    t.Hide(); Assert.IsFalse(t.Active);
}

[Test] public void ShowWithTimeoutAfterNoTimeout_Fires()
{
    var t = new UiLoadingTimer(); t.Show(0f, false); t.Show(100f);
    Assert.IsFalse(t.Tick(114f)); Assert.IsTrue(t.Tick(115f));
}

// GameConfigTests.cs içine
[TestCase("privacy")] [TestCase("terms")]
public void LegalUrl_KnownIdsReturnTheirConstant(string id) =>
    Assert.AreEqual(id == "privacy" ? GameConfig.PrivacyUrl : GameConfig.TermsUrl, GameConfig.LegalUrl(id));

[TestCase("")] [TestCase("baska")] [TestCase(null)]
public void LegalUrl_UnknownIdIsEmpty(string id) => Assert.AreEqual("", GameConfig.LegalUrl(id));

[Test] public void HasLegalLinks_NeedsBothUrls() =>
    Assert.AreEqual(GameConfig.PrivacyUrl != "" && GameConfig.TermsUrl != "", GameConfig.HasLegalLinks);
```

- [ ] **Step 2: Kırmızıyı gör.** `unity command test run --mode EditMode --filter UiLoadingTimerTests` ve `--filter GameConfigTests` (filtre tek sınıf adı alır). Expected: derleme hatası.

- [ ] **Step 3: `UiLoadingTimer` ve `GameConfig` eklerini yaz.** `GameConfig` adres sabitlerinin üstüne tek satır yorum: adresler Osman'dan gelecek, boşken ilgili satırlar gizlenir.

- [ ] **Step 4: `UIRoot` eklerini yaz.** `ShowLoading` imzası yukarıdaki gibi; mevcut çağıranlar (parametresiz ya da tek metinle) değişmeden derlenir. Kapatılamayan pencere dalı: bugünkü "yut ve yığına geri koy" davranışına çıkış onayı eklenir; onayın açılması `closable == false` pencereyi kapatmaz.

- [ ] **Step 5: Doğrula.** Yeni 8 test geçer; tüm EditMode paketi geçer (209 + 8 = 217). PlayMode (MainMenu'den başlat, `script eval` ile):
  - `UIRoot.ShowLoading("Deneme", false)`, 16 sn bekle: `UIRoot.IsLoading == true`, bildirim yok. Ardından `UIRoot.ShowLoading()`: metin hâlâ "Deneme" (ekran görüntüsüne bak).
  - `closable = false` bir `UiWindow` aç, `UIRoot.HandleBack()`: pencere açık, çıkış onayı görünür; bir kez daha `HandleBack()`: onay kapanır, pencere hâlâ açık; ikinci bir onay yığılmaz.

- [ ] **Step 6: Commit.** `UI kiti: zaman aşımsız yükleme, kapatılamayan pencerede çıkış onayı, adres ayarları`

---

### Task 2: Giriş ekranını kur ve bağla

**Files:**
- Create: `Assets/Osman/Editor/UiKitLogin.cs` (`UiKitBuilder`'ın `partial` parçası)
- Create: `Assets/Osman/Scripts/UI/LegalLinksText.cs`
- Modify: `Assets/Osman/Scripts/UI/MainMenuUIHandler.cs`
- Modify (betik üretir): `Assets/Osman/Scenes/MainMenu.unity`

**Interfaces:**
- Consumes: Task 1'in `UIRoot.ShowLoading(text, timeout)`, `GameConfig.LegalUrl`, `GameConfig.HasLegalLinks`; paket 1'in `UIRoot.Toast`, `UiWindow` (`closable`, `closeOnBack`), prefab'lar `Btn_Green`, `Window`.
- Produces: menü `Okey/UI Kit/Giriş Ekranını Kur` → `UiKitBuilder.BuildLoginScreen()`. `LegalLinksText : MonoBehaviour, IPointerClickHandler`.

**Bugünkü sahne (MainMenu.unity):** kökte `EventDispatcher`, `PhotonManager`, `Manager`, `EventSystem`, `Main Camera`, `Canvas`, `ADSManagers`, `SceneChangeManager`. `MainMenuUIHandler` `Canvas` nesnesinin üstünde. `Canvas` çocukları: `RetryPanel`, `MenuBackgorund` (altında `LoadingText`, `NamePanel`, `GameName`, `AdsButtons`, `ChoicePanel`). Kurucu yalnızca `Canvas`'ın çocuklarını siler ve yeniden kurar; kökteki diğer nesnelere, `ADSManagers`'a ve `Canvas` üstündeki bileşenlere (ölçek ayarı dışında) dokunmaz.

**Kurulacak hiyerarşi (1920x1080 birimi):**

```
Canvas                      CanvasScaler 1920x1080, Match 0.5
  Background                tam ekran Image, Assets/Sprites/okey_ui_demo/main_menu_background.png, raycast kapalı
  Safe                      tam ekran, Crystal.SafeArea
    Title                   TMP ExtraBold 170, dış çizgili materyal, üstten 150, ortalı; metin eski GameName nesnesinin metni
    ChoicePanel             ortalı, VerticalLayoutGroup, aralık 36, çocuk hizası orta
      GoogleButton          720x150, uGUI yerleşik "UISprite" Sliced, beyaz; etiket "Google ile Giriş", ExtraBold 52, renk #2C333C, dış çizgisiz
      GuestButton           Btn_Green örneği 720x150; etiket "Misafir Olarak Oyna", 52
    LegalText               alttan 70, ortalı, TMP Bold 30, renk #CFD6DE, LegalLinksText
    VersionText             sağ alt (40, 30 içeride), TMP Bold 28, renk #A9B3BD
  NamePanel                 Window örneği, kapalı; UiWindow closable=false; Close gizli
    Title                   "İsmini seç"
    Content                 NameInput (900x130, row_slate Sliced, TMP_InputField, yazı Bold 48, yer tutucu "İsim"), altında NameError (TMP Bold 34, #FF8F85, ortalı)
    Buttons                 NameOkButton: Btn_Green "TAMAM"
  RetryPanel                Window örneği, kapalı; UiWindow closable=false; Close gizli
    Title                   "Bağlanılamadı"
    Content                 Message: TMP Bold 40, #CFD6DE, "İnternet bağlantını kontrol edip tekrar dene."
    Buttons                 RetryButton: Btn_Green "TEKRAR DENE"
```

`LegalText` metni (tam olarak): `Devam ederek <link="terms"><u>Kullanım Şartları</u></link> ve <link="privacy"><u>Gizlilik Politikası</u></link>'nı kabul edersin`

- [ ] **Step 1: `LegalLinksText.cs` yaz.** `Awake`: `gameObject.SetActive(GameConfig.HasLegalLinks)`. `OnPointerClick`: `TMP_TextUtilities.FindIntersectingLink` ile dokunulan bağlantıyı bulur, `GameConfig.LegalUrl(linkId)` boş değilse `Application.OpenURL` çağırır. Canvas Screen Space Overlay olduğu için kamera `null` geçilir.

- [ ] **Step 2: `MainMenuUIHandler.cs` değişiklikleri.** Yalnızca gösterim değişir:
  - `loadingText` ve `statusText` alanları silinir; `public TextMeshProUGUI versionText` eklenir, `Awake`'te `"v" + Application.version` yazılır.
  - `Show(screen)`: `Loading` ve `Lobby` durumunda `UIRoot.ShowLoading("Bağlanıyor...", false)`, diğerlerinde `UIRoot.HideLoading()`; panel `SetActive` satırları aynı kalır. `Refresh` ve `EnterLobby` içindeki `loadingText.text = "Bağlanıyor..."` satırları silinir (metni `Show` veriyor).
  - `PlayAsGuest` ve `PlayWithGoogle`: iş başlarken `UIRoot.ShowLoading("Giriş yapılıyor...", false)`; her çıkış yolunda (`catch`, iptal, hata, başarı) `Refresh`'ten önce `UIRoot.HideLoading()`. `statusText.text = ...` satırları aynı metinle `UIRoot.Toast(...)` olur: "Bağlanılamadı", "Google girişi tamamlanmadı", "Google'a bağlanılamadı". `SetEntryBusy` kalır; içindeki `statusText` satırı silinir.
  - `ConfirmName`, `RetryConnect`, `Start`, `EnterLobby`'nin bekleme ve kopma mantığı değişmez. `this == null` denetimleri kalır.
  - Sınıf başındaki ve `Awake`'teki yorumlar yeni duruma göre düzeltilir ("Paneller sahnede açık durur" artık doğru değil: pencereler sahnede kapalı kayıtlıdır).

- [ ] **Step 3: `UiKitBuilder.BuildLoginScreen()` yaz (`UiKitLogin.cs`).** Play modunda çalışmaz (`LogWarning` + dönüş). Sırası: MainMenu sahnesini aç (kaydedilmemiş değişiklik varsa `SaveCurrentModifiedScenesIfUserWantsTo`); eski `GameName` metnini oku; `Canvas`'ın tüm çocuklarını sil; ölçeği ayarla; yukarıdaki hiyerarşiyi kur; `MainMenuUIHandler` alanlarını bağla (`choicePanel`, `guestButton`, `googleButton`, `namePanel`, `nameInput`, `nameError`, `nameOkButton`, `retryPanel`, `retryButton`, `versionText`); buton `onClick`'lerini kalıcı dinleyiciyle bağla (`GuestButton` → `PlayAsGuest`, `GoogleButton` → `PlayWithGoogle`, `NameOkButton` → `ConfirmName`, `RetryButton` → `RetryConnect`); sahneyi kaydet. Gerekli prefab, sprite ya da font bulunamazsa hiçbir şeyi silmeden `LogWarning` ile döner. İkinci çalıştırmada sahne aynı sonucu verir. `GameName` yoksa (ikinci çalıştırma) başlık metni mevcut `Title` nesnesinden okunur.

- [ ] **Step 4: Kur ve sahne farkına bak.**

Run: `unity command script eval "UiKitBuilder.BuildLoginScreen();" ...`
Expected: `console status` derleme hatası yok. `git diff --stat` MainMenu.unity'de büyük bir fark gösterir (beklenen); kök nesne adları aynı kalmalı: `EventDispatcher`, `PhotonManager`, `Manager`, `EventSystem`, `Main Camera`, `Canvas`, `ADSManagers` (dört çocuğuyla), `SceneChangeManager`. Sahnede `AdsButtons`, `LoadingText`, `StatusText`, `MenuBackgorund` adlı nesne kalmamalı; silinen nesnelere başvuran bileşen kalmamalı (sahneyi açınca "missing reference" uyarısı yok).

- [ ] **Step 5: Düzen ekran görüntüleri (düzenleme modunda değil, PlayMode'da).** MainMenu'den başlat. Kayıtlı oturum lobiye götürdüğü için giriş ekranını görmek üzere `script eval` ile handler'ın `choicePanel`, `namePanel`, `retryPanel` nesneleri sırayla açılıp `UIRoot.HideLoading()` çağrılır ve her biri `ScreenCapture.CaptureScreenshot` ile `.superpowers/sdd/<plan>/shots/` altına kaydedilir: `login-choice.png`, `login-name.png`, `login-retry.png`. Her PNG okunur ve şunlar yazılır: başlık okunaklı ve taşmıyor; butonlar esnememiş, ortalı; Türkçe harfler tek fonttan; isim ve tekrar-dene pencerelerinde X yok; sürüm sağ altta. Lobiye geçiş bu sırada başlarsa oyunu durdurup `SceneChangeManager` çağrısından önce yakalamak için `Time.timeScale` kullanılmaz; bunun yerine çekimler lobiye varıldıktan sonra MainMenu sahnesi `SceneManager.LoadScene("MainMenu")` ile yeniden açılarak alınır ve bu yöntem raporda yazılır.

- [ ] **Step 6: Davranış doğrulaması (PlayMode, `script eval`; Google girişi tetiklenmez, hesaptan çıkılmaz).**
  - Açılış: `UIRoot.IsLoading == true`, metin "Bağlanıyor...", lobiye varınca `false` ve etkin sahne `LobbyMenu`.
  - Yerel arka uçta (`GameServices.UsesFirebase == false` ise) `googleButton` kapalı ve `GuestButton` panelde ortalı; Firebase açıksa ikisi de görünür. Hangisinin denendiği raporda yazılır.
  - `GameConfig.HasLegalLinks == false` iken `LegalText` kapalı.
  - İsim penceresi açıkken `UIRoot.HandleBack()`: çıkış onayı açılır, isim penceresi açık kalır.
  - Tekrar dene penceresi: `retryPanel` açılıp `RetryConnect()` çağrılınca pencere kapanır ve akış sürer (bağlantı zaten varsa lobiye gider).
  - İsim doğrulaması: `nameInput.text = "a"` ile `ConfirmName()` → `nameError` dolu, pencere açık. (Geçerli bir isimle çağrılmaz: geliştiricinin kayıtlı ismini değiştirir.)
  - Review Focus 2 ve 3 canlı sahnelenemiyorsa (Google iptali, Photon kopması) kod okunarak doğrulanır ve raporda "canlı denenmedi" diye yazılır.

- [ ] **Step 7: Testler.** Tüm EditMode paketi geçer (217). `powershell -ExecutionPolicy Bypass -File Tools\SyncTest\Run-SyncTest.ps1 -Solo -Mode smart -Seed 11` arka planda: hata ve `[BUG]` yok.

- [ ] **Step 8: Commit.** `Giriş ekranı: ortak UI kitiyle yeniden dizildi`

---

## Paket sonu doğrulama (ana oturum)

- İki görevin diff'i okunur; üç ekran görüntüsüne bakılır.
- Klonlar açıksa `Run-SyncTest.ps1 -Deals 1`: açılış akışı dört istemcide de bu ekrandan geçer.
- Telefon (Redmi, Wi-Fi APK), Osman'la: gerçek geri tuşu (giriş ekranında çıkış onayı, isim penceresinde çıkış onayı), çentik altında kalan öğe var mı, Google butonu ve hesap seçici, dış çizginin görünümü, dokunma alanları.
- Beyaz Google butonu yerleşik sprite ile kuruluyor; görünümü zayıf kalırsa Osman'a sorulur (Google logosu için görsel dosyası gerekir).
