# Ayarlar ve profil — uygulama planı (paket 4/4)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ayarlar ve Profil pencereleri ortak UI kitine geçer; ayarlara gizlilik, şartlar, destek ve Play'in zorunlu tuttuğu uygulama içi hesap silme gelir.

**Architecture:** Hesap silme tek bir servis giriş noktasıdır (`GameServices.DeleteAccountAsync`) ve sonucu bir enum ile döner; arayüz yalnızca onu çağırır. İki pencere `LobbyMenu` sahnesinde bir Editor betiğiyle (`UiKitBuilder.BuildAccountWindows`) yeniden kurulur ve `LobbyAccountUI` alanları aynı betikte bağlanır; lobinin geri kalanı ve mağaza yeniden kurulmaz. `LobbyAccountUI`'nin isim, Google bağlama ve çıkış akışları davranış olarak aynı kalır; durum yazıları bildirime taşınır.

**Tech Stack:** Unity uGUI 2.0, TextMeshPro, Firebase Auth/Firestore (yalnızca cihazda), NUnit EditMode testleri, `unity` CLI.

**Spec:** `docs/superpowers/specs/2026-11-19-menu-ui-yenileme-design.md` — "Paket 4 — Ayarlar ve profil" ve devreden notların "Paket 4" başlıkları.

Önceki paketler: UI kiti, giriş ekranı, lobi (`2c396c5..` lobi commit'leri). Yöneten: ana oturum (Fable); görevleri alt ajanlar sırayla yapar.

## Global Constraints

- `EntryFlow`, `GoogleLinkFlow`, `NameFlow` mantığına dokunulmaz. `GameServices`'e yalnızca hesap silme giriş noktası eklenir; mevcut giriş, bağlama, çıkış ve profil yükleme yolları değişmez.
- `LobbyAccountUI`'de isim kaydetme, Google bağlama, hesap değiştirme onayı ve çıkışın servis çağrıları ve sırası değişmez; kimin hangi butonu gördüğü (`CanSignOut`, `CanChangeName`, misafir/Google) değişmez.
- Ses ve Müzik anahtarları kalır ve `GameSettings` üzerinden kaydedilir (Osman'ın kararı).
- Gerçek bir hesap üzerinde hesap silme ÇALIŞTIRILMAZ: ne Editördeki kayıtlı yerel profilde ne telefonda. Silme yolu yalnızca geçici dosyalarla çalışan EditMode testleriyle ve onay pencerelerinin açılıp "Vazgeç" ile kapatılmasıyla doğrulanır.
- Google girişi tetiklenmez, hesaptan çıkılmaz, kayıtlı isim değiştirilmez, satın alma butonuna basılmaz.
- `Debug.LogError` eklenmez; beklenen durumlar `LogWarning`. Yeni paket, görsel, font, `[PunRPC]` yok.
- Sahne YAML'ı elle yazılmaz. Kurucu yalnızca `ProfilePanel` ve `SettingsPanel` nesnelerini değiştirir; lobi Canvas'ının diğer çocuklarına, `ShopCanvas`'a ve kök nesnelere dokunmaz (`BuildLobbyScreen` yeniden çalıştırılmaz).
- Canvas ölçeği ve SafeArea lobi paketinde kuruldu; değişmez. Dokunma alanı en az 120 birim yükseklik. Yıkıcı eylemler kırmızı ve onaylıdır.
- Yazı tipi: başlık ve buton `Changa-ExtraBold SDF`, uzun metin `Changa-Bold SDF`; emoji ve simge glifi yok.
- Oyuncuya görünen metinler bu plandaki gibi, Türkçe. Yorumlar Türkçe ve seyrek; mevcut yorumlar, anlattıkları satır silinmedikçe kalır.
- `Assets/Fonts/Changa-*.asset` play modunda değişirse `git checkout` ile geri alınır.
- Commit: görev başına bir tane, Türkçe tek satır + `Co-Authored-By: Claude <noreply@anthropic.com>`. Push yok. Kullanıcının değişmiş dosyaları sahnelenmez.

## Review Focus

1. Hesap silinirken profil silindi ama kimlik silinemedi (Firebase "yakın zamanda giriş gerekli" ya da ağ hatası): oyuncu yarım silinmiş bir hesapta kilitlenmez; anlaşılır bir bildirim görür ve oyun kullanılabilir kalır. → Görev 1, `DeleteAccount_AuthFails_*` testleri.
2. Hesap silme başarılı: oturum ve profil temizlenir, isim kaydı serbest kalır, oyuncu giriş ekranına döner ve yeni hesapla girebilir. → Görev 1, `DeleteAccount_Ok_*`.
3. Oyuncu "Hesabımı Sil"e yanlışlıkla dokunur: iki ayrı onaydan geçmeden hiçbir şey silinmez; geri tuşu ve "Vazgeç" her adımda iptal eder. → Görev 2, Adım 6.
4. Adresler boş (bugünkü durum): Gizlilik, Şartlar ve Destek satırları görünmez; pencere boşluksuz dizilir. → Görev 2, Adım 6.
5. Ayar "kapalı" kayıtlıyken pencere açılır: anahtar kapalı görünür ve ilk dokunuş açar (kit anahtarı sessiz yüklemeyi gösterir). → Görev 2, Adım 6.
6. Misafir ve Google hesabında profil penceresi: misafirde isim alanı yerine düz isim ve ipucu, "Google'a Bağla" görünür, "Çıkış Yap" kurala göre; Google hesabında isim düzenlenir, hesap satırı Google der. → Görev 2, Adım 6.
7. Uzun bildirim metni ("Güvenlik için tekrar giriş yapıp yeniden dene") bildirim kutusuna sığar. → Görev 2, Adım 2.

---

### Task 1: Hesap silme servisi

**Files:**
- Modify: `Assets/Osman/Scripts/Services/GameServices.cs`
- Modify (gerekiyorsa): `Assets/Osman/Scripts/Services/IAuthService.cs`, `Assets/Osman/Scripts/Services/Firebase/FirebaseAuthService.cs`, `Assets/Osman/Scripts/Services/Local/LocalAuthService.cs`
- Modify: `Assets/Osman/Scripts/Services/PlayerProfile.cs` (enum)
- Test: `Assets/Osman/Editor/Tests/GameServicesTests.cs` ya da `LocalServicesTests.cs` (mevcut kalıba hangisi uyuyorsa)

**Interfaces:**
- Produces:
  - `public enum DeleteResult { Ok, NeedsRecentLogin, Failed }`
  - `public static Task<DeleteResult> GameServices.DeleteAccountAsync()` — mevcut `Exclusive` kalıbıyla (başka hesap işlemi sürerken ikinci işlem başlamaz).
  - Sıra: oturum yoksa `Failed`. Önce profil ve ona bağlı kayıtlar silinir (`Profile.DeleteAsync()`; isim kaydı dahil), sonra kimlik (`Auth.DeleteAccountAsync()`), sonra yerel profil durumu temizlenir (mevcut `DropProfile` yolu). Kimlik silme "yakın zamanda giriş gerekli" hatası verirse `NeedsRecentLogin`, başka hatada `Failed` döner; her iki durumda yöntem istisna fırlatmaz.
  - Kimlik silinemezse hesap profilsiz kalır: bu, mevcut "profil yoksa yeniden oluşturulur" yoluna düşer (oyuncu çıkıp girince başlangıç profiliyle devam eder). Yöntemin başına bunu söyleyen tek satır yorum.

- [ ] **Step 1: Mevcut silme kodunu oku ve sırayı doğrula.** `IAuthService.DeleteAccountAsync`, `IProfileStore.DeleteAsync`, `FirestoreProfileStore.DeleteAsync` (isim kaydını da siliyor mu?), `LocalAuthService`, `LocalProfileStore`, ve `FirebaseAuthService`'in geçişte misafiri silerken kullandığı yol. Firestore kuralları profil silmeyi kimlik hâlâ varken gerektirir: sıra profil → kimlik. İsim kaydı silinmiyorsa `DeleteAsync` içinde silinecek şekilde tamamlanır ve raporda yazılır.

- [ ] **Step 2: Testleri yaz** (yerel servislerle, geçici klasörde; mevcut `LocalServicesTests` kurulum kalıbı izlenir). Adlar ve iddialar:
  - `DeleteAccount_Ok_ClearsSessionAndProfile`: girişten sonra `DeleteAccountAsync()` → `DeleteResult.Ok`; `Auth.UserId == null`; `Profile.Current == null`; profil dosyası yok.
  - `DeleteAccount_Ok_NameCanBeTakenAgain`: isim seçmiş hesap silinir; aynı isim yeni hesapla alınabilir (`NameResult.Ok`).
  - `DeleteAccount_NoSession_IsFailed`: oturum yokken `Failed`, istisna yok.
  - `DeleteAccount_AuthFails_ReturnsFailed_DoesNotThrow`: kimlik silme istisna fırlatan sahte `IAuthService` ile `Failed`; istisna dışarı çıkmaz.
  - `DeleteAccount_AuthNeedsRecentLogin_ReturnsNeedsRecentLogin`: "yakın zamanda giriş gerekli"yi temsil eden hata ile `NeedsRecentLogin`.
  `GameServices` statik ve gerçek servislere bağlıysa, karar mantığı testlerin sahte servis verebileceği küçük bir statik yönteme ayrılır (`GoogleLinkFlow.RunAsync`'in yaptığı gibi): `AccountDeletion.RunAsync(Func<Task> deleteProfile, Func<Task> deleteIdentity, Func<Exception, bool> isRecentLoginError, Action dropLocal, bool hasSession)`; `GameServices.DeleteAccountAsync` onu gerçek servislerle çağırır.

- [ ] **Step 3: Kırmızıyı gör, sonra uygula.** "Yakın zamanda giriş gerekli" tanıma: `FirebaseAuthService` içinde, Firebase tiplerine başvuran tek bir yardımcı (`AuthError.RequiresRecentLogin`); Editörde Firebase derlenmiyorsa mevcut `#if` kalıbı izlenir. Yerel arka uç hiçbir zaman bu hatayı vermez.

- [ ] **Step 4: Yeşil.** Yeni 5 test geçer; tüm EditMode paketi geçer (231 + 5 = 236).

- [ ] **Step 5: Commit.** `Hesap silme: tek servis girişi, sonucu enum ile döner`

---

### Task 2: Ayarlar ve profil pencereleri

**Files:**
- Create: `Assets/Osman/Editor/UiKitAccount.cs` (`UiKitBuilder`'ın `partial` parçası)
- Modify: `Assets/Osman/Editor/UiKitPrefabs.cs` (yalnızca bildirim metni: otomatik boyut), `Assets/Resources/UIRoot.prefab` (betik üretir)
- Modify: `Assets/Osman/Scripts/UI/LobbyAccountUI.cs`
- Modify (betik üretir): `Assets/Osman/Scenes/LobbyMenu.unity` (yalnızca `ProfilePanel` ve `SettingsPanel`)

**Interfaces:**
- Consumes: Task 1 `GameServices.DeleteAccountAsync`, `DeleteResult`; `GameConfig.PrivacyUrl`, `TermsUrl`, `SupportEmail`; kit `Window`, `Row`, `Toggle`, `Btn_*`, `UIRoot`, `UiWindow`, `ChipInitial`.
- Produces: menü `Okey/UI Kit/Hesap Pencerelerini Kur` → `UiKitBuilder.BuildAccountWindows()`. `LobbyAccountUI` yeni genel metotları: `OpenPrivacy()`, `OpenTerms()`, `OpenSupport()`, `DeleteAccount()`.

**Ayarlar penceresi** (`SettingsPanel`, `Window` örneği, başlık "AYARLAR", X açık, `onBack` → `CloseSettings`, `closeOnBack = false`). İçerik dikey dizi, satır yüksekliği 120, aralık 14:

| Satır | İçerik | Davranış |
|---|---|---|
| Ses | etiket + `Toggle` | `OnSoundToggle` |
| Müzik | etiket + `Toggle` | `OnMusicToggle` |
| Gizlilik Politikası | etiket + ">" | `OpenPrivacy`; `GameConfig.PrivacyUrl` boşsa satır gizli |
| Kullanım Şartları | etiket + ">" | `OpenTerms`; `TermsUrl` boşsa gizli |
| Destek | etiket + ">" | `OpenSupport` (`mailto:`); `SupportEmail` boşsa gizli |
| Hesabımı Sil | kırmızı etiket (#FF8F85) + ">" | `DeleteAccount`; oturum varken her zaman görünür |

Altta `VersionText` ("Sürüm " + sürüm; Bold 30, #A9B3BD). Altı satır da görünürken içerik pencereye sığmıyorsa pencere gövdesi yükseltilir ya da içerik dikey kaydırılır; hangisi yapıldığı raporda yazılır.

**Profil penceresi** (`ProfilePanel`, `Window` örneği, başlık "PROFİL", X açık, `onBack` → `CloseProfile`, `closeOnBack = false`):

```
Content
  Avatar            sol; 220 daire + baş harf (ChipInitial, NameText'i okur)
  Info              sağ; dikey dizi, aralık 14
    NameRow         NameInput (giriş alanı) + NameSaveButton (Btn_Green 220x120 "KAYDET")   ← CanChangeName iken
                    NameText (ExtraBold 48) + NameHint (Bold 30, #A9B3BD)                    ← değilken
    NameError       Bold 30, #FF8F85 (yalnızca isim doğrulama hatası burada kalır)
    LevelRow        LevelText ("Seviye N", ExtraBold 40) + XpText ("340 / 500 XP", Bold 32) + XpBar (dolgu #F2C94C, zemin #3C4652)
    CoinRow         coin simgesi + CoinText (ExtraBold 40) + AccountText (Bold 32, #A9B3BD: "Misafir hesap" / "Google hesabı")
Buttons
  LinkGoogleButton  Btn_Green 620x130 "Google'a Bağla, ilerlemeni koru"   ← Firebase'de ve misafirken
  SignOutButton     Btn_Red 360x130 "ÇIKIŞ YAP"                           ← CanSignOut iken
```

- [ ] **Step 1: `LobbyAccountUI.cs` değişiklikleri.**
  - `linkStatus` ve `signOutStatus` alanları silinir; yazdıkları metinler aynı sözcüklerle `UIRoot.Toast(...)` olur ("Google girişi tamamlanmadı", "Google'a bağlanılamadı", "İsmini seçebilirsin", "Çıkış yapılamadı"). `nameError` alanı kalır.
  - Yeni alanlar: `public Image xpBar;`, `public GameObject privacyRow, termsRow, supportRow, deleteRow;`. `Refresh`: `xpBar.fillAmount = (profil.Xp % XpPerLevel) / (float)XpPerLevel`; `LevelText` "Seviye N", `XpText` "X / Y XP", `CoinText` yalnızca sayı, `AccountText` "Misafir hesap" / "Google hesabı". `OpenSettings`: üç bağlantı satırı ilgili `GameConfig` değeri boş değilken, `deleteRow` `GameServices.HasSession` iken görünür.
  - `OpenPrivacy` / `OpenTerms` → `Application.OpenURL(GameConfig.LegalUrl(...))`; `OpenSupport` → `Application.OpenURL("mailto:" + GameConfig.SupportEmail)`; değer boşsa hiçbir şey yapmaz.
  - `DeleteAccount()` (`async void`, mevcut `SignOut` kalıbıyla, `this == null` denetimleriyle):
    1. `UIRoot.Confirm("Hesabımı Sil", "Hesabın, ismin, seviyen ve coinlerin kalıcı olarak silinir. Satın aldıkların da gider. Geri alınamaz.", "Devam", true)` → hayırsa dön.
    2. `UIRoot.Confirm("Emin misin?", "Hesabın şimdi silinecek.", "Hesabı Sil", true)` → hayırsa dön.
    3. `UIRoot.ShowLoading("Hesap siliniyor...", false)`; `var r = await GameServices.DeleteAccountAsync()`; `UIRoot.HideLoading()`.
    4. `Ok` → `SceneChangeManager.Instance.ChangeScene("MainMenu")`. `NeedsRecentLogin` → `UIRoot.Toast("Güvenlik için tekrar giriş yapıp yeniden dene")`. `Failed` → `UIRoot.Toast("Hesap silinemedi")`.
  - Diğer metotların mantığı değişmez.

- [ ] **Step 2: Bildirim metni otomatik boyutlansın.** `UiKitPrefabs.cs`'te `UIRoot` içindeki bildirim metni: otomatik boyut açık (en çok 40, en az 26), tek satır, kutunun yatay dolgusu içinde. `UiKitBuilder.BuildPrefabs()` yeniden çalıştırılır; `Btn_Green.prefab` ve `Window.prefab` farksız kalmalı (`git diff --stat`), `UIRoot.prefab`'da yalnızca bildirim metni alanları değişmeli. PlayMode'da `UIRoot.Toast("Güvenlik için tekrar giriş yapıp yeniden dene")` çekilir ve bakılır.

- [ ] **Step 3: `UiKitBuilder.BuildAccountWindows()` (`UiKitAccount.cs`).** Lobi kurucusuyla aynı korumalar: play modunda çalışmaz; gereken varlıkları ve `Window` alt yollarını hiçbir şeyi silmeden önce denetler; hata olursa sahneyi kaydetmeden yeniden açar. Yalnızca `ProfilePanel` ve `SettingsPanel` silinip aynı adlarla, `Canvas`'ın son iki çocuğu olarak yeniden kurulur (kapalı kayıtlı). `LobbyAccountUI`'nin tüm alanları bağlanır; buton, anahtar ve satır olayları kalıcı dinleyiciyle bağlanır (`OnSoundToggle(bool)` ve `OnMusicToggle(bool)` dinamik parametreyle). Lobi üst barındaki `ProfileChip` ve `SettingsButton` bağlantıları `LobbyAccountUI` metotlarına gider ve değişmez. Kurucu sonunda sahnede başka hiçbir nesnenin değişmediğini kendi denetler (lobi Canvas'ının diğer çocuklarının ve `ShopCanvas`'ın nesne sayısı kurulumdan önceki ile aynı).

- [ ] **Step 4: Kur ve denetle.** `console status` temiz; `LobbyAccountUI`'nin tüm alanları dolu; sahnedeki kalıcı metot adlarının hepsi kodda var; `git diff --stat` yalnızca beklenen dosyaları gösterir; lobi (üst bar, kartlar, alt butonlar, masa kurma ve şifre pencereleri) ve mağaza kurulumdan önceki gibi çalışır.

- [ ] **Step 5: Ekran görüntüleri (PlayMode, ana Editör).** `settings.png` (bugünkü durum: adresler boş, dört satırdan yalnızca Ses, Müzik, Hesabımı Sil görünür), `settings-all-rows.png` (üç bağlantı satırı `script eval` ile görünür yapılarak; sığma kontrolü), `profile-guest.png`, `profile-google.png` (Google hesabı görünümü: `Refresh`'in baktığı değerler değiştirilemiyorsa ilgili nesneler elle açılıp kapatılarak kurgulanır ve raporda "kurgu" denir), `delete-confirm-1.png`, `delete-confirm-2.png`, `toast-long.png`; ayrıca `settings` ve `profile-guest` 20:9 ve 4:3'te. Her PNG okunur ve dürüstçe anlatılır.

- [ ] **Step 6: Davranış (PlayMode).**
  - Ayarlar: Ses anahtarı kapatılır, pencere kapatılıp açılır → kapalı görünür, `GameSettings.SoundOn == false`; tekrar açılır (ayar eski değerine döndürülür). Müzik için aynısı.
  - Adresler boşken üç bağlantı satırı kapalı, satırlar boşluksuz.
  - `DeleteAccount()`: ilk onay açılır; "Vazgeç" → hiçbir şey olmaz. Yeniden: ilk onay "Devam", ikinci onay açılır; geri tuşu (`UIRoot.HandleBack()`) → iptal. İKİNCİ ONAY ASLA ONAYLANMAZ. Sonrasında `GameServices.HasSession == true` ve profil yerinde.
  - Profil: misafirde isim alanı gizli, düz isim ve ipucu görünür; XP çubuğu profil değerine göre dolu; baş harf dairesi isimle uyumlu. "KAYDET"e geçerli isimle basılmaz; geçersiz isimle (`"a"`) basılırsa hata metni çıkar (isim alanı görünür durumdaysa).
  - `SignOut()` onayı açılır ve "Vazgeç" ile kapatılır. `LinkGoogle()` çağrılmaz.
  - Geri tuşu her iki pencereyi kapatır; X butonu da.

- [ ] **Step 7: Testler.** EditMode 236. `Run-SyncTest.ps1 -Solo -Mode smart -Seed 11` geçer.

- [ ] **Step 8: Commit.** `Ayarlar ve profil: ortak UI kiti, gizlilik/şartlar/destek satırları, hesap silme`

---

## Paket sonu doğrulama (ana oturum)

- İki görevin diff'i ve ekran görüntüleri incelenir.
- Klonlarda yenileme zorlanıp `Reload` pencereleri kapatıldıktan sonra `Run-SyncTest.ps1 -Deals 1`.
- Telefona APK kurulur. Telefonda, Osman'la: ayarlar ve profil pencereleri, anahtarlar, geri tuşu. Hesap silme telefonda yalnızca Osman'ın kendi kararıyla ve kaybetmek istemediği bir hesapta DEĞİL, deneme hesabında denenir.
- Kapsam dışı bırakılanlar raporlanır: ayarların masadan açılması (tasarım "ileride" diyor; masa işinde), anahtarın tam hap biçimi (görsel dosyası ister), şartlar satırının dokunma yüksekliği (adresler gelince).
