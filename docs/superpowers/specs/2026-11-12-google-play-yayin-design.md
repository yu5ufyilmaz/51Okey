# Google Play'e çıkış: hesap, coin, reklam, market — tasarım

Tarih: 2026-11-12. Kaynak: kullanıcıyla sohbet + aynı gün yapılan web araştırması (bölüm 3'teki kurallar o araştırmadan).
Durum: kullanıcı onayı bekliyor. Onaydan sonra her faz için ayrı uygulama planı yazılır.

## 1. Ne ve neden

51 Okey Google Play'de yayınlanacak. Bunun için oyuncunun kalıcı bir hesabı (ID, coin, level), reklam, gerçek parayla
coin satışı ve Play'in yayın şartları gerekiyor. Eski `origin/Osman` dalında (2025-02) bunların bir kısmı Firebase ile
denenmişti, ama o dal 89 commit geride ve Google girişi zorunlu olduğu için 4 editörlü klon testlerini engelliyordu.
O dal merge edilmez; bu iş güncel `OsmanUNmerge` dalı üzerinde sıfırdan yapılır.

İleride masaya oturma ücreti gelecek: kazanan parayı alır, ikinci amorti eder, üçüncü ve dördüncü kaybeder. O kısım
şimdi yapılmıyor ama yapı ona hazır olmalı; kullanıcı bu ekonomiyi epey genişletmeyi düşünüyor.

## 2. Hedefler

Kullanıcının istedikleri:
- Google Play'de yayınlanabilir bir sürüm; yapılması gereken her adım planda.
- Oyuncu için kalıcı kayıt: ID, coin, level (şimdilik yüzeysel).
- Misafir olarak oynanabilir; oyuncu isterse sonradan Google'a bağlar, coin'leri taşınır.
- Reklam: ödüllü reklam + geçiş reklamı. Banner yok.
- Market: coin paketleri (mevcut `coin100`, `coin200`, `coin500`, `coin1000`) ve `removeads`.
- Arka uç: Firebase. Unity Gaming Services'a bağımlı olunmayacak.
- **Klon testleri hiçbir aşamada bozulmayacak; klon başına Google hesabı gerekmeyecek.**
- İş, alt ajanlara (subagent) dağıtılabilecek parçalara bölünecek.

Hedef olmayanlar (bu tasarımda yok):
- Masa ücreti ve ödül dağıtımının kendisi (sadece yapısı hazır).
- Sıralama tablosu, arkadaş listesi, turnuva, bildirim.
- iOS.
- Banner reklam.
- Kopan oyuncunun aynı koltuğa geri dönmesi (rejoin).

Anti-hedefler (çalışsa bile başarısızlık sayılır):
- Editörde ya da test koşusunda Google hesabı, internet ya da Firebase'e bağımlılık.
- Coin'in gerçek paraya çevrilebilmesi ya da oyuncular arasında aktarılabilmesi (Play'in gerçek paralı kumar kuralına girer).
- Coin bakiyesini oyun kodunun dağınık yerlerden doğrudan değiştirmesi.
- Reklamın el oynanırken ya da sıra süresi işlerken çıkması.

## 3. Kısıtlar

### 3.1 Proje
- Unity 6000.3.10f1, Android, IL2CPP, ARMv7+ARM64, `minSdk 25`, paket adı `com.KhazadGame.Okey`.
- Photon PUN2; kimlik bugün sadece yazılan isim (`PhotonNetwork.NickName`), `UserId` ayarlanmıyor.
- Oyun kodu `Assembly-CSharp` içinde, asmdef yok. EditMode testleri `Assets/Osman/Editor/Tests/` altında. Bu düzen korunur.
- ParrelSync klonları (`51Okey_clone_0..2`): `Assets` ve `ProjectSettings` junction, **`Packages` düz kopya**. UPM ile
  eklenen her paket için `Packages/manifest.json` ve `packages-lock.json` klonlara elle kopyalanır.
- Test altyapısı: `Tools/SyncTest/Run-SyncTest.ps1`, `Tools/SyncTest/Run-EditModeTests.ps1`, `SyncAutoTest` (menü akışını
  kendisi sürer: lobiye bağlanınca `NickName` atar, `LobbyMenu`'ye geçer, oda kurar ya da şifreyle girer).
- Unity IAP `com.unity.purchasing 4.14.2` kurulu, Codeless IAP kullanılıyor (`IAPManager.OnPurchaseComplete(Product)`).
- `Assets/ExternalDependencyManager` 1.2.181 duruyor. `user.keystore` repoda takipte.

### 3.2 Güncel dış kurallar (2026-11-12 araştırması)
- **Hedef API 36 zorunlu** (2026-08-31'den beri, yeni uygulama ve güncelleme). Proje şu an 35: reddedilir.
- **Play Billing:** PBL 8, 2027-08-31'e kadar kabul. Unity IAP 4.14.2 PBL 8 kullanıyor, yani uygun. IAP 5.x kod değişikliği
  ister ve 5.4.4 Codeless IAP'yi yeni projelere kapattı.
- **AAB** ve **Play App Signing** zorunlu. **16 KB sayfa** desteği gerekli; Unity 6000.3 destekliyor, eklenti `.so` dosyaları uyarı verebilir.
- **Kapalı test:** 2023-11-13'ten sonra açılan kişisel hesaplar için 12 test kullanıcısı, 14 kesintisiz gün.
- **Hesap silme:** hesap oluşturan uygulama, uygulama içi silme yolu + web bağlantısı sunmalı. **Gizlilik politikası** ve
  **Veri güvenliği formu** (SDK'ların topladığı veriler dahil) kapalı testte bile zorunlu. **IARC** derecelendirmesi zorunlu.
- **Firebase Unity SDK 14.1.0:** en az Unity 2022.3, `minSdk 24`. Editör/masaüstü desteği beta: Auth, Firestore, Functions
  gerçek çalışır; Crashlytics ve Analytics Editörde boş kalır. Editörde Auth kaydı FirebaseApp adına göre tutulur; aynı
  makinedeki editörler varsayılan uygulamada **aynı kullanıcıyı paylaşır**.
- **Google girişi:** eski `google-signin-unity` eklentisi arşivlendi. Resmi yol Play Games Services v2 eklentisi (2.3.0) +
  Firebase `PlayGamesAuthProvider`. Sadece Android'de çalışır; Play Console ayarı ister.
- **Google Mobile Ads Unity 11.6.0:** en az API 24, hedef API 35+, Custom Main Gradle Template ister, UMP (rıza) içinde gelir.
  AEA / Birleşik Krallık / İsviçre'de kişiselleştirilmiş reklam için rıza mesajı şart.
- **Bağımlılık çözücü:** EDM4U arşivlendi, yerini Unity'nin kendi External Dependency Manager paketi alıyor. Projedeki
  1.2.181 eski.
- **Firebase fiyat:** Spark (ücretsiz) planda Auth, Firestore, Crashlytics var. Cloud Functions için Blaze (ödeme yöntemi) gerekir.
- **Kumar benzeri içerik:** Play'in gerçek paralı kumar kuralı, gerçek dünya değeri olan ödül için bahsi yasaklar. Nakde
  çevrilemeyen coin için açık bir madde yok. Simüle kumar sayılırsa: çocuk hedef kitlesi olamaz, kumar reklamı gösterilemez,
  yaş derecesi 18 olabilir.

## 4. Yaklaşımlar

### 4.1 Arka uç
- **Unity Gaming Services:** Klon başına ayrı anonim oyuncu hazır gelir, proje zaten bağlı. Kullanıcı UGS'ye bağımlı olmak istemedi: elendi.
- **PlayFab:** Sanal para ve satın alma doğrulama hazır, SDK saf C#. Topluluğu küçük, çökme raporu için yine Firebase gerekir: elendi.
- **Firebase (seçilen):** Google Play ile aynı aile, ekipte eski tecrübe var, Auth + Firestore ücretsiz planda. Bedeli: ağır
  SDK, Gradle bağımlılık yönetimi, Editör desteği beta.

### 4.2 Editörde ve testte kimlik
- **A. Editörde de Firebase, klon başına adlı FirebaseApp:** Testler gerçek arka ucu sınar. Ama Editör desteği beta, adlı
  uygulama davranışı belgelenmemiş, her test koşusu internete ve Firebase'e bağlanır.
- **B. Editörde yerel arka uç (seçilen), Firebase isteğe bağlı:** Editör varsayılan olarak Firebase'e hiç dokunmaz;
  kimlik ve profil yerelde üretilir. Senkron testleri bugünkü kadar hızlı ve kararlı kalır. Bir ayarla Editör gerçek
  Firebase'e geçirilebilir (o zaman A'daki adlı uygulama kullanılır). Bedeli: klon testleri arka ucu sınamaz; arka uç
  telefonda ve isteğe bağlı Editör modunda sınanır.

B seçildi çünkü kullanıcının birinci şartı testlerin bozulmaması; arka uç hatası oyun senkron testini düşürmemeli.

### 4.3 Coin yetkisi
- **Şimdi: istemci yazar.** Firestore kuralı sadece "kendi belgen" der. Basit, ücretsiz; oyuncu kendi coin'ini hileyle değiştirebilir.
- **Sonra: sunucu yazar** (Cloud Functions, Blaze). Masa ücreti ve gerçek para devreye girince şart.

İkisi aynı arayüzün (`IWallet`) arkasında durur; geçişte oyun kodu değişmez. Kapalı test istemci yazarıyla yapılabilir.
Herkese açık yayından önce sunucuya geçilip geçilmeyeceği ayrı bir karardır (bölüm 10).

## 5. Mimari

### 5.1 Servis katmanı
Yeni klasör `Assets/Osman/Scripts/Services/`. Oyun kodu yalnızca arayüzleri görür; hangi uygulamanın çalışacağını tek bir
başlangıç noktası seçer. Firebase / AdMob tiplerini kullanan kod yalnızca `Services/Firebase/` ve `Services/Ads/AdMob/`
altında durur; başka hiçbir dosya `using Firebase` ya da `using GoogleMobileAds` yazmaz.

```csharp
public interface IAuthService
{
    string UserId { get; }            // giriş bitmeden null
    bool IsGuest { get; }
    Task SignInAsync();               // ilk açılışta misafir hesap; tekrar çağrı zararsız
    Task<LinkResult> LinkGoogleAsync();
    Task DeleteAccountAsync();        // yalnızca kimlik; profil IProfileStore.DeleteAsync ile silinir
}

public interface IProfileStore
{
    PlayerProfile Current { get; }
    event Action<PlayerProfile> Changed;
    Task LoadOrCreateAsync(string userId, string defaultName);
    Task SetDisplayNameAsync(string name);
    Task AddXpAsync(int xp);
    Task DeleteAsync();
}

public interface IWallet
{
    long Coins { get; }
    Task<bool> ApplyAsync(CoinTx tx);  // false = yetersiz bakiye ya da reddedildi; aynı tx.Id ikinci kez etki etmez
}

public interface IAdsService
{
    Task InitializeAsync();                          // rıza akışı dahil
    bool RewardedReady { get; }
    Task<bool> ShowRewardedAsync(string placement);  // true = ödül hak edildi
    void TryShowInterstitial(string placement);      // sıklık sınırı ve reklamsız bayrağına uyar
}
```

```csharp
public class PlayerProfile { public string UserId, DisplayName; public long Coins; public int Level, Xp; public bool NoAds; public int SchemaVersion; }
public enum CoinReason { StarterGrant, RewardedAd, Purchase, TableFee, TablePayout }
public readonly struct CoinTx { public readonly string Id; public readonly long Amount; public readonly CoinReason Reason; public readonly string Reference; }
public enum LinkResult { Linked, SwitchedToExistingAccount, Cancelled, Failed }
```

Başlangıç noktası: `GameServices` statik sınıfı (`GameServices.Auth`, `.Profile`, `.Wallet`, `.Ads`, `Task GameServices.InitAsync()`).
Seçim kuralı:
- Editör: yerel uygulamalar. `EditorPrefs` anahtarı `Okey.UseFirebaseInEditor` açıksa Firebase.
- Cihaz: Firebase + AdMob.
- `SyncAutoTest` etkinken reklam her zaman boş uygulamadır (`NullAdsService`), ayar ne olursa olsun.

Uygulamalar:
| Arayüz | Yerel (Editör varsayılanı) | Gerçek |
|---|---|---|
| `IAuthService` | `LocalAuthService`: ID = `local-<proje klasör adı>` (`51Okey`, `51Okey_clone_0`…), anında döner | `FirebaseAuthService`: anonim giriş, Play Games ile bağlama |
| `IProfileStore` + `IWallet` | `LocalProfileStore`: `persistentDataPath/local-profile-<proje klasör adı>.json` | `FirestoreProfileStore` |
| `IAdsService` | `NullAdsService`: ödüllü reklam hemen `true` döner, geçiş reklamı hiçbir şey yapmaz | `AdMobAdsService` |

Proje klasör adı `Path.GetFileName(Path.GetDirectoryName(Application.dataPath))` ile alınır; ParrelSync'in editör
derlemesine çalışma zamanı bağımlılığı olmaz.

Editörde Firebase modu açıksa `FirebaseApp.Create(options, <proje klasör adı>)` ile klon başına ayrı uygulama ve ayrı
anonim kullanıcı kullanılır. Bu davranış kaynak koddan okundu, denenmedi; Faz 1 denemesi doğrular.

### 5.2 Veri modeli (Firestore)
- `users/{uid}`: `displayName`, `coins`, `level`, `xp`, `noAds`, `createdAt`, `lastSeenAt`, `schemaVersion`.
- `users/{uid}/ledger/{txId}`: `amount`, `reason`, `reference`, `createdAt`. Her coin değişikliği bir kayıt bırakır;
  belge kimliği `CoinTx.Id` olduğu için aynı işlem iki kez yazılamaz (satın alma tekrarı, bağlantı kopması).
- Coin değişikliği tek Firestore transaction'ı: bakiye oku, eksiye düşecekse reddet, `coins` güncelle, ledger kaydı yaz.
- Kural (v1): bir kullanıcı yalnızca kendi `users/{uid}` belgesini ve altını okur/yazar; ledger kaydı güncellenemez, silinemez
  (hesap silme hariç). Başka oyuncunun belgesi okunmaz: masada isim ve level Photon üzerinden taşınır.
- Sunucu fazında kural "coins ve ledger'ı yalnızca sunucu yazar" olur; şema değişmez.

### 5.3 Akışlar
**Açılış.** `PhotonManager.Start` bugün hemen `ConnectUsingSettings` çağırıyor. Yeni sıra: `GameServices.InitAsync()` →
`Auth.SignInAsync()` → `Profile.LoadOrCreateAsync` → `ConnectUsingSettings`. Yerel modda ilk üç adım aynı karede biter,
yani test akışının zamanlaması değişmez. Cihazda giriş 10 sn içinde bitmezse "bağlanılamadı, tekrar dene" paneli çıkar;
oyun zaten internetsiz oynanamıyor. Açılış yolunda Play Games çağrısı yoktur: kayıtlı Firebase kullanıcısı varsa o,
yoksa anonim giriş.

**Photon kimliği değişmez.** `PhotonNetwork.AuthValues` / `UserId` şimdi ayarlanmaz. Oda `PlayerTtl` 60 sn olduğu için
sabit `UserId`, kopan oyuncunun (ve 60 sn içinde yeniden başlatılan test klonunun) aynı odaya `JoinRoom` ile girmesini
engellerdi; bunu çözmek rejoin akışı ister ve o kapsam dışı. Hesap ID'si ileride gerekirse Photon oyuncu özelliği olarak taşınır (5.4).

**İsim.** Ana menüdeki isim kutusu profildeki `DisplayName` ile dolu gelir; değişirse profile yazılır. Yeni profilin
varsayılan adı tekildir: `Oyuncu` + hesap ID'sinden türetilen 4 hane. `SyncAutoTest` bugünkü gibi `NickName`'i kendi atar.

**Coin ve level gösterimi.** Menüde ve markette coin yazısı `IProfileStore.Changed` olayından beslenir.
`ShopController.AddCoins` (bugün ekrandaki yazıyı parse edip topluyor) kaldırılır; yerine `Wallet.ApplyAsync` gelir.

**Level.** Oyun bitince `Profile.AddXpAsync` çağrılır. Level, XP'den hesaplanır.

**Ödüllü reklam.** Markette "İzle, coin kazan" düğmesi → `Ads.ShowRewardedAsync("shop")` → `true` ise
`Wallet.ApplyAsync(RewardedAd)`.

**Geçiş reklamı.** Yalnızca oyun bitip sonuç ekranı kapanırken / lobiye dönerken `Ads.TryShowInterstitial("game_end")`.
El sırasında, sıra süresi işlerken, bekleme ekranında asla. `NoAds` açıksa hiç çıkmaz.

**Satın alma.** Codeless IAP ve Unity IAP 4.14.2 korunur. `IAPManager.OnPurchaseComplete` ürün kimliğini coin miktarına
çevirir ve `Wallet.ApplyAsync(Purchase, Id = satın alma işlem kimliği)` çağırır. `removeads` → `NoAds = true`. Makbuz
cihazda Unity IAP'nin yerel doğrulayıcısıyla (`GooglePlayTangle` projede var) doğrulanır; sunucu doğrulaması sunucu fazında gelir.
Misafir de satın alabilir; satın almadan önce "misafir hesap silinirse coin kaybolur" uyarısı ve Google'a bağlama önerisi gösterilir.

**Google'a bağlama.** Ayarlarda "Google ile bağla" → Play Games v2 `ManuallyAuthenticate` → `RequestServerSideAccess` →
`PlayGamesAuthProvider.GetCredential` → anonim kullanıcıya `LinkWithCredentialAsync`. O Google hesabı zaten başka bir
profile bağlıysa (yeniden kurulum) oyuncuya "bu cihazdaki misafir ilerleme silinecek, kayıtlı hesabına geçilecek" diye
sorulur; onaylarsa kayıtlı profile geçilir ve misafir profil silinir, vazgeçerse misafir kalır. **Profiller birleştirilmez**
(birleştirme, başlangıç coin'ini tekrar tekrar toplamaya izin verirdi). Yeniden kurulumda eski hesaba dönüş de bu düğmeyle
olur; misafirken menüde düğme görünür durur.

**Hesap silme.** Ayarlarda "Hesabımı sil" → onay → ledger kayıtları ve `users/{uid}` silinir, Auth kullanıcısı silinir,
uygulama yeni misafirle baştan açılır. Web tarafı: aynı talebin e-postayla yapılabildiği basit bir sayfa.

### 5.4 Sunucu fazına hazırlık (şimdi yapılmaz)
- `IWallet`'ın sunucu uygulaması Cloud Functions çağırır; `TableFee` ve `TablePayout` yalnızca orada var olur.
- Photon'da güvenilir sunucu yok (master bir oyuncunun telefonu). Maç sonucu için öngörülen yol: dört istemci sonucu
  ayrı ayrı bildirir, sunucu çoğunluk aynıysa öder. O fazda her oyuncunun hesap ID'si Photon oyuncu özelliği olarak
  odaya taşınır ve maç için tekil bir kimlik üretilir; şimdi eklenmez.

## 6. Test güvencesi

Her iş paketi bitmeden şu kapıdan geçer; geçmeyen iş bitmiş sayılmaz:
0. `Tools/Security/Check-Secrets.ps1` temiz (bölüm 13).
1. `Tools/SyncTest/Run-EditModeTests.ps1` yeşil.
2. Dört editörle `Run-SyncTest.ps1 -Mode smart -Seed 11`: PASS, desync 0.
3. `AndroidDevBuild.Build()` başarılı; APK telefonda açılıyor ve `-JoinRoom` ile test oyuncularının olduğu odaya girebiliyor.

Kurallar:
- Editörde varsayılan arka uç yereldir; test koşusu Firebase'e, AdMob'a, internete (Photon hariç) çıkmaz.
- Hiçbir servis çağrısı menü akışını süresiz bekletmez.
- Yeni servis kodu `Debug.LogError` basarsa test FAIL olur (koşucu böyle karar veriyor); beklenen durumlar uyarı olarak loglanır.
- SDK eklemek Editör konsoluna her açılışta hata basıyorsa o hata giderilmeden iş bitmez.
- `Packages/manifest.json` değişirse aynı işte klonlara kopyalanır ve klonların derlendiği doğrulanır.
- Sahne, prefab ve `ProjectSettings` dosyalarını aynı anda yalnızca bir ajan değiştirir.
- Saf mantık (profil, cüzdan, yerel uygulamalar, ürün tablosu, reklam sıklık sınırı) EditMode testiyle yazılır.

## 7. Fazlar

| Faz | İçerik | Bağlı olduğu | Play Console gerekir mi |
|---|---|---|---|
| 0 | Hesaplar ve kararlar (kullanıcı): Play Console, Firebase projesi, keystore şifresi, paket adı | — | — |
| 1 | SDK tabanı: bağımlılık çözücü güncellemesi, Firebase (Auth, Firestore, Crashlytics) + Google Mobile Ads içe aktarma, Gradle şablonları, hedef API 36, boş haliyle telefonda derleme. Özellik yok. | 0 (paket adı, Firebase projesi) | Hayır |
| 2 | Servis çekirdeği: arayüzler, modeller, yerel uygulamalar, `GameServices` başlangıcı, açılış sırası, tekil varsayılan isim, EditMode testleri | — (Faz 1 ile paralel) | Hayır |
| 3 | Firebase hesap ve profil: `FirebaseAuthService`, `FirestoreProfileStore`, güvenlik kuralları, menüde coin/level | 1, 2 | Hayır |
| 4 | Reklam: `AdMobAdsService`, rıza akışı, ödüllü ve geçiş yerleşimleri, sıklık sınırı (test reklam kimlikleriyle) | 1, 2 | Hayır |
| 5 | Market: satın almayı cüzdana bağlama, yerel makbuz doğrulama, `removeads` | 2 (gerçek satın alma testi için 3 ve Play Console) | Kod için hayır, gerçek test için evet |
| 6 | Google'a bağlama: Play Games v2 eklentisi, bağlama ve hesap çakışması | 3 | Evet |
| 7 | Uyum: hesap silme, gizlilik politikası sayfası, ayarlar ekranı, rıza ayarına dönüş | 3, 4 | Hayır |
| 8 | Yayın: imzalı AAB derleme betiği, sürüm kodu, 16 KB kontrolü, mağaza sayfası, Veri güvenliği formu, IARC, kapalı test | hepsi | Evet |
| 9 | (Sonra) Sunucu yetkisi: Cloud Functions cüzdan, makbuz doğrulama, masa ücreti ve ödül | 3, 5 | — |

Paralellik: Faz 1 ve 2 aynı anda yürür (dosyaları kesişmez). Faz 3, 4, 5'in kod kısmı paralel yazılabilir; menü/market
sahnesine bağlama adımları sırayla yapılır. Faz 1 tek ajan işidir ve en güçlü modelle yapılır (Gradle ve SDK çakışması
en riskli yer). Faz 2 saf C# olduğu için orta seviye modele uygundur.

Faz 1'i ilk sıraya koymanın nedeni: SDK ve Gradle sorunu varsa hiçbir özellik yazılmadan ortaya çıksın.

## 8. Kullanıcının yapacağı işler
- Play Console hesabını kurtarmak ya da yenisini açmak (hesap bilgileri kayıp; uygulama o hesapta test kanalında).
- Firebase projesi: eski projeye (146708517675) erişim varsa onu, yoksa yenisini seçmek; `google-services.json` indirmek.
- AdMob hesabı ve uygulama kaydı (Faz 4'ün gerçek reklam kimlikleri için; test kimlikleriyle geliştirme bunu beklemez).
- `user.keystore` şifresini bulmak (imzalı yayın derlemesi için).
- SDK paketlerinin indirilmesine onay vermek.
- Kapalı test için 12 test kullanıcısı bulmak (hesap kişisel ve yeniyse).
- Mağaza sayfası metni ve görselleri, gizlilik politikası metninin onayı.

## 9. Riskler ve doğrulanmamış noktalar
- **Paket adı eski Play hesabına kilitli.** Hesap kurtarılamazsa yeni paket adı gerekir. Firebase, AdMob ve Play Games
  kayıtları paket adına bağlı olduğu için bu Faz 1'den önce netleşmeli.
- **Okey'in Play'deki sınıfı belirsiz.** Nakde çevrilemeyen coin'le bahis için açık madde bulunamadı. En kötü durum: 18 yaş
  derecesi, çocuk hedef kitlesi yok, kumar reklamı yok. Tasarım buna göre: coin nakde çevrilmez, oyuncular arası aktarılmaz.
- **Firebase + Google Mobile Ads + Play Games aynı projede** güncel sürümlerle çakışıyor mu, doğrulanmadı. Faz 1 denemesi cevaplar.
- **Bağımlılık çözücü geçişi** (EDM4U → Unity paketi) Firebase 14.x ile uyumlu mu, doğrulanmadı.
- **Firebase Editörde** çöküş bildirimi var (2023, açık). Editör varsayılanı yerel olduğu için testleri etkilememeli; Faz 1 doğrular.
- **Play Games kimliğini anonim hesaba bağlama** belgede örnek olarak geçmiyor; Faz 6 başında denenir. Olmazsa: Play Games
  ile giriş yapılır ve misafir profil verisi yeni hesaba kopyalanır.
- **Unity IAP ve UGS:** `Assets/Resources/IAPProductCatalog.json` içinde `enableUnityGamingServicesAutoInitialization: true`
  var; Unity IAP 4.x açılışta Unity servis çekirdeğini başlatır. Bu arka uç bağımlılığı değildir (veri Firebase'de), ama
  Veri güvenliği formuna yazılır. Faz 5 kapatılıp kapatılamayacağını dener.
- **Aynı isimli iki oyuncu masayı bozar** (bugün de var): masa kodu per alanlarını `NickName + " meld"` adıyla buluyor
  (`ScoreManager`, `TileDistrubite`, `SeatManager`, `ProcessAssist`). Tekil varsayılan isim olasılığı düşürür, çözmez.
  Herkese açık yayından önce ayrı bir iş olarak ele alınmalı (senkron açısından hassas kod, kendi tasarımı gerekir).
- **Repo herkese açık** (`github.com/yu5ufyilmaz/51Okey`, 2026-11-12'de girişsiz erişilebiliyor) ve `user.keystore` içinde
  takipte. Anahtar şifreyle korunuyor ama açıkta; yayın için kullanılmaz (bölüm 13).
- Photon `AppIdRealtime` de açık repoda. APK'dan zaten çıkarılabilen bir kimliktir; kötüye kullanım (kotayı tüketme) ancak
  Photon özel kimlik doğrulamasıyla engellenir, o da sunucu fazının işi.
- İstemci yazarlı coin hileye açıktır; coin'in bir değeri oluşmadan önce Faz 9 gerekir.

## 10. Açık kararlar (kullanıcının)
1. Play Console: eski hesap kurtarılacak mı, yeni hesap + yeni paket adı mı? (Kullanıcı: şimdilik hesapsız işler yapılsın; sonra karar.)
2. Bu sürümde coin'i harcayacak bir yer yok (masa ücreti sonraya kaldı). Coin paketleri yine de satışa çıkacak mı,
   yoksa ilk sürümde yalnızca `removeads` mı satılacak, ya da basit bir masa ücreti bu sürüme mi alınacak?
3. Herkese açık yayın, sunucu yetkisi (Faz 9, Blaze planı) olmadan yapılacak mı?

Karara bağlananlar: Firebase projesi kullanıcının kendi Google hesabında yeni açılır; gerekirse sonra başka hesap Owner
olarak eklenir. Paket adı değişirse projeye ikinci Android uygulaması eklenir.

## 11. Benim verdiğim kararlar (kullanıcı değiştirebilir)
- Editör varsayılanı yerel arka uç; Firebase Editörde isteğe bağlı.
- Google girişi Play Games Services v2 üzerinden.
- Unity IAP 4.14.2 ve Codeless IAP korunur; 5.x'e geçiş 2027-08-31 son tarihinden önce ayrı iş.
- Tek Firebase projesi; geliştirme/yayın ayrımı herkese açık yayın öncesi yeniden değerlendirilir.
- Crashlytics var, Analytics ve Remote Config şimdilik yok (Veri güvenliği formu kısa kalır).
- Başlangıç değerleri: yeni hesaba 1000 coin; ödüllü reklam 50 coin, günde en fazla 5; geçiş reklamları arası en az 3 dakika;
  oyun bitince 10 XP, kazanana 25 XP; level = 1 + XP / 100. Hepsi tek bir ayar sınıfında durur.
- Coin paketleri mevcut ürün kimlikleriyle ve adlarındaki miktarla (100 / 200 / 500 / 1000).
- Photon `UserId` ayarlanmaz; rejoin kapsam dışı.
- Google hesabı çakışmasında profiller birleştirilmez; kayıtlı hesaba geçilir, misafir silinir (oyuncu onayıyla).
- Misafir satın alabilir (uyarıyla).
- Açılışta Play Games'e gidilmez; eski hesaba dönüş "Google ile bağla" düğmesiyle.
- Kullanılmayan `com.unity.services.cloudsave` paketi kaldırılır. Unity IAP kalır: mağaza sarmalayıcısıdır, arka uç değildir.

## 12. Yapıcıya bırakılanlar
- Sınıf içi tasarım, dosya adları (bölüm 5.1'deki arayüz imzaları ve klasör kuralı dışında).
- Ayarlar ekranının ve yeni düğmelerin görünümü (mevcut menü stiline uyar).
- Faz 1'de hangi bağımlılık çözücünün kalacağı (denemenin sonucuna göre; sonuç plana yazılır).
- Gizlilik politikası ve hesap silme sayfasının nerede barındırılacağı.

## 13. Gizli bilgi ve güvenlik kuralları

Kullanıcı şartı: hiçbir anahtar ya da şifre kodda, repoda, logda ya da sohbette açıkta kalmayacak. Repo herkese açık
olduğu için kurallar "repoya giren her şeyi herkes görür" varsayımıyla yazıldı.

**Asla repoya, APK'ya ya da sohbete girmeyenler (gerçek sırlar):**
- İmza anahtarı (`.keystore` / `.jks`) ve şifreleri.
- Firebase servis hesabı JSON'u, Google Play Developer API anahtarı, Cloud Functions sırları, Photon gizli anahtarı.
- Her türlü hesap şifresi ve kurtarma kodu.
Bunlar yalnızca kullanıcının bilgisayarında, repo dışında durur. Sunucu fazında sırlar Google Secret Manager'da tutulur,
koda yazılmaz. Ajanlar bu değerleri istemez, yazdırmaz, loglamaz; gerekiyorsa kullanıcı kendisi girer.

**APK'ya giren ama repoya girmeyenler (kimlikler):** `google-services.json` ve ondan üretilen dosyalar
(`Assets/StreamingAssets/google-services-desktop.json`, `Assets/Plugins/Android/FirebaseApp.androidlib/`), gerçek AdMob
uygulama ve reklam birimi kimlikleri. Bunlar gizli değildir (her APK'dan okunur) ama açık repoda durmaları başkalarının
kullanmasını kolaylaştırır. `.gitignore`'a alınır; kullanıcı yedeğini kendisi tutar. Repoda yalnızca Google'ın herkese
açık **test** reklam kimlikleri bulunur.

**Kimlikler tek başına koruma değildir; asıl koruma şunlardır:**
- Firestore güvenlik kuralları: varsayılan ret, yalnızca kendi belgen. Kurallar repoda `firebase/firestore.rules` olarak durur.
- Firebase API anahtarı Google Cloud'da Android uygulamasına (paket adı + SHA-1) kısıtlanır.
- Herkese açık yayından önce Firebase App Check (Play Integrity) açılır: Firestore'a yalnızca gerçek uygulama erişir.

**İmza anahtarı:** Repodaki `user.keystore` açığa çıkmış sayılır ve yayında kullanılmaz. Yayın için repo dışında yeni bir
yükleme anahtarı üretilir; şifresi Unity'ye her derlemede elle girilir, hiçbir dosyaya yazılmaz. `user.keystore` takipten
çıkarılır (geçmişte kalır; bu yüzden yeniden kullanılmaz). Test derlemesi (`AndroidDevBuild`) zaten Unity'nin debug
anahtarını kullanıyor, etkilenmez.

**Otomatik kontrol:** `Tools/Security/Check-Secrets.ps1` commit'e girecek dosyaları tarar: Google API anahtarı kalıbı,
özel anahtar bloğu, servis hesabı JSON'u, keystore dosyası, `google-services.json`, test olmayan `ca-app-pub-` kimliği,
şifre atanmış satır. Bulursa durur. Her iş paketinin kapısında ve her commit'ten önce çalışır.

**Ajan kuralları:** Her ajan görevinde şu satırlar yer alır: sır yazma, sır yazdırma, `git add -A` kullanma, dosyaları
adıyla ekle, commit'ten önce `Check-Secrets.ps1` çalıştır. Log satırlarına kullanıcı kimliği dışında kimlik bilgisi
(token, e-posta) yazılmaz.

**Kullanıcının kararına kalanlar:** repoyu gizliye almak (repo Yusuf'un hesabında; en etkili tek adım), eski Firebase
projesini kapatmak ya da anahtarlarını kısıtlamak (eski dallarda `google-services.json` duruyor).
