# Google girişi ve Firebase hesabı: uygulama planı

Tarih: 2026-11-13. Tasarım: `docs/superpowers/specs/2026-11-12-google-play-yayin-design.md` (Faz 1, 3, 6'nın giriş için gereken kısmı).
Yöneten: ana oturum (plan, ajan seçimi, konsol işleri). İş paketlerini alt ajanlar yapar, sırayla: aynı anda tek ajan
Editörü, sahneleri ve `ProjectSettings`'i kullanır.

## Kullanıcının istediği

Oyunun girişinde oyuncu "Google ile gir" ya da "isimle gir" seçebilsin. Hesap Firebase'de dursun.

## Kararlar (tasarımdan sapma yok, bir ekleme var)

- Giriş ekranı ayrı sahne değil, mevcut `MainMenu`: isim kutusu + bağlan düğmesi "isimle gir" yoludur (misafir),
  yanına "Google ile gir" düğmesi eklenir. Tasarımda bu düğme ayarlardaydı; kullanıcı girişte istedi.
- Açılış sırası tasarımdaki gibi: kayıtlı Firebase kullanıcısı varsa o, yoksa anonim giriş. Açılışta Play Games çağrısı yok.
- "Google ile gir" = `IAuthService.LinkGoogleAsync()`. Arayüz imzaları değişmez.
- Editörde varsayılan arka uç yerel kalır; yerel modda Google düğmesi gizlidir. Testler Firebase'e dokunmaz.
- Bu planda yok: reklam (Faz 4), Crashlytics, market (Faz 5), hesap silme ekranı (Faz 7).

## Sabit bilgiler

- Paket adı `com.modiacgames.okey51`. Firebase projesi `okey51-dev` (numara 650666962378).
- `Assets/google-services.json` yerinde ve gitignore'da; iki paketi de içerir. Asla commit edilmez.
- Unity 6000.3.10f1, proje `F:\Documents\UnityProjects\51Okey`, klonlar `51Okey_clone_0..2` (Assets ve ProjectSettings
  junction, `Packages` düz kopya).
- Editörü sürme ve test betikleri için oku: `C:\Users\osman\.claude\projects\F--Documents-UnityProjects-51Okey\memory\`
  içinde `unity-cli-drive-editors.md`, `unity-on-f-drive.md`, `phone-wifi-build.md`, `ads-plugins-return.md`.

## Her paketin kapısı

0. `powershell -ExecutionPolicy Bypass -File Tools\Security\Check-Secrets.ps1` temiz (commit'e girecek dosyalar stage'liyken).
1. `Tools\SyncTest\Run-EditModeTests.ps1` yeşil.
2. Editör konsolunda bu işten gelen yeni hata yok (bilinen iki EDM hatası hariç, bkz. `unity-cli-drive-editors.md`).
3. `AndroidDevBuild.Build()` APK üretir (İP1 ve İP3 için zorunlu).
Dört editörlü senkron testi ve telefon denemesini ana oturum en sonda yapar.

## Ajan kuralları (her görevde geçerli)

- Sır yazma, sır yazdırma. `google-services.json`, keystore, OAuth istemci sırrı commit'e ve loga girmez.
- `git add -A` / `git add .` yok; dosyaları adıyla ekle. Commit'ten önce `Check-Secrets.ps1`.
- Çalışma ağacında bu işten önce değişmiş dört dosya var (`51Okey.slnx`, `ProjectSettings/PackageManagerSettings.asset`,
  `ProjectSettings/UnityConnectSettings.asset`, `docs/superpowers/plans/2026-11-11-sync-autotest.md`): dokunma, commit etme.
- Dal `MDC-001-firebase`. Push yok.
- `Packages/manifest.json` değişirse aynı işte `manifest.json` + `packages-lock.json` üç klona kopyalanır.
- Oyun senkron koduna (`GamePlay/`, `TILES/`) dokunma.
- Bitince rapor: ne yapıldı, hangi commit, kapıdan ne geçti / ne geçmedi (çıktısıyla), açık kalanlar.

## İş paketleri

### İP1 — SDK tabanı (model: opus; en riskli iş)
Amaç: Firebase ve Play Games eklentileri projede, proje derleniyor, boş haliyle APK çıkıyor. Özellik kodu yok.
- Firebase Unity SDK güncel kararlı sürüm, yalnızca `FirebaseAuth` ve `FirebaseFirestore` (`.unitypackage`, `Assets`
  altına: klonlar junction ile görür, manifest kayması olmaz). Kaynak yalnızca Google'ın resmi indirme sayfası.
- Play Games Plugin for Unity v2 güncel sürüm (resmi GitHub `playgameservices/play-games-plugin-for-unity` sürümleri).
  Kurulum sihirbazı (uygulama kimliği, web istemci kimliği) İP3'te doldurulur; burada eklenti yalnızca derlenir.
- Bağımlılık çözücü: Firebase'in getirdiği EDM4U kalır, eski `1.2.181` kopyası çakışıyorsa kaldırılır.
- Gradle şablonları gerektiği kadar; `AndroidTargetSdkVersion` 36; `minSdk` 25 kalır.
- Editör varsayılanı Firebase'i başlatmaz: bu pakette hiçbir kod `FirebaseApp`'e dokunmaz.
- Üretilen `google-services-desktop.json` ve `FirebaseApp.androidlib` gitignore'da; doğrula.
- 50 MB'tan büyük dosya commit etme; varsa raporla.
- Sonucu (sürümler, kalan çözücü, bilinen uyarılar) bu dosyanın sonuna "İP1 sonucu" başlığıyla yaz.

### İP2 — Firebase hesap ve profil servisleri (model: opus)
Bağlı: İP1. Dosyalar: `Assets/Osman/Scripts/Services/**`, `Photons/PhotonManager.cs`, `Assets/Osman/Editor/Tests/**`.
- `Services/Firebase/FirebaseAuthService.cs`: kayıtlı kullanıcı varsa o, yoksa anonim giriş. `LinkGoogleAsync` İP3'te dolar
  (burada `Failed` döner). `using Firebase` yalnızca `Services/Firebase/` altında.
- `Services/Firebase/FirestoreProfileStore.cs` (`IProfileStore` + `IWallet`): şema ve transaction kuralı tasarım 5.2;
  kurallar `firebase/firestore.rules` (yayında). Cüzdan: istisna = sonuç bilinmiyor, aynı `Id` ile tekrar dene; `false` = uygulanmadı.
- `GameServices`: arka uç seçimi (cihaz → Firebase; Editör → yerel, `EditorPrefs` `Okey.UseFirebaseInEditor` açıksa Firebase,
  o zaman `FirebaseApp.Create(options, <proje klasör adı>)`). `SyncAutoTest` etkinken her zaman yerel.
- `InitAsync` sıfırlanabilir: hata olursa sonraki çağrı yeniden dener; 10 sn zaman aşımı. `PhotonManager` hata/zaman
  aşımında "bağlanılamadı, tekrar dene" durumuna düşer (panel İP3'te; burada olay/durum yeter). `Ads.InitializeAsync`
  Photon bağlantısını bekletmez.
- "Kullanıcı değişti" olayı (hesap değişince profil yeniden yüklenir). `DeleteAsync` `Changed` tetikler.
- Saf mantık EditMode testiyle (init sıfırlama, zaman aşımı, arka uç seçimi). Firestore'un kendisi telefonda sınanır.

### İP3 — Google girişi ve giriş ekranı (model: opus kod + sahne)
Bağlı: İP2 ve konsol işleri (aşağıda). Sahne `Assets/Osman/Scenes/MainMenu.unity`'yi yalnızca bu paket değiştirir.
- Play Games kurulum sihirbazı: konsoldan gelen kaynak XML + web istemci kimliği (ana oturum verir).
- `FirebaseAuthService.LinkGoogleAsync`: `PlayGamesPlatform.Instance.ManuallyAuthenticate` → `RequestServerSideAccess`
  → `PlayGamesAuthProvider.GetCredential` → anonim kullanıcıya `LinkWithCredentialAsync`. Hesap zaten başka profile
  bağlıysa oyuncuya sorulur; onaylarsa o hesaba geçilir, misafir profil silinir (tasarım 5.3). Profiller birleştirilmez.
- `MainMenu`: "Google ile gir" düğmesi (mevcut menü stiline uyar), durum yazısı (misafir / Google bağlı), bağlanamadı
  + tekrar dene paneli, hesap çakışması onay paneli. Yerel arka uçta Google düğmesi gizli.
- `SyncAutoTest` menü akışı bozulmaz (isim kutusu ve bağlan düğmesi aynı nesne adlarıyla kalır).
- `MainMenuUIHandler` profili her lobi girişinde yeniden yazmasın: yalnızca isim değiştiyse yaz.

### Konsol işleri (ana oturum, tarayıcı)
- Play Console → Play Games Hizmetleri kurulumu, Cloud projesi `okey51-dev` ile; arzebagames hesabına projede yetki.
- OAuth izin ekranı; Android istemcisi (paket + debug SHA-1); web istemcisi.
- Firebase Auth → Play Games sağlayıcısı. İstemci sırrını kullanıcı kendisi yapıştırır.
- Firebase Android uygulamasına debug SHA-1. Test kullanıcıları.

### Son kapı (ana oturum)
EditMode testleri, senkron testi (`Run-SyncTest.ps1 -Mode smart -Seed 11`; klonlar kapalıysa `-Solo`), dev APK telefonda:
isimle giriş → Firestore'da `users/{uid}` oluşur; Google ile giriş → hesap bağlanır.

## İP1 sonucu (2026-11-13)

- **Sürümler:** Firebase Unity SDK 14.1.0 (`dl.google.com/firebase/sdk/unity/firebase_unity_sdk_14.1.0.zip`), yalnız
  `FirebaseAuth` + `FirebaseFirestore`. Play Games Plugin for Unity 2.3.0 (GitHub sürümü; `package.json` 2.3.0, ama
  `PluginVersion.cs` ve `ProjectSettings/GooglePlayGameSettings.txt` "2.1.0" yazar: eklentinin kendi tutarsızlığı).
- **Çözücü:** eski EDM4U 1.2.181 silindi; Firebase'in getirdiği **EDM4U 1.2.190** tek çözücü. Play Games paketi kendi
  EDM 1.2.182 kopyasıyla gelir; o kısım çıkarılarak içe aktarıldı (paket yeniden kurulursa EDM'i hariç tut).
- **Android çözümleme:** Force Resolve `True`. EDM `mainTemplate.gradle`, `gradleTemplate.properties`,
  `settingsTemplate.gradle` dosyalarını `Assets/Plugins/Android`'e kopyaladı (AndroidX + Jetifier açık). Bağımlılıklar:
  firebase-auth 25.0.0, firebase-firestore 26.6.0, play-services-games-v2 22.1.0, play-services-nearby 18.5.0.
  Firestore paketi **firebase-analytics 24.0.0**'ı da çekiyor (Analytics kodu yok, ama Veri güvenliği formu için not).
- **Hedef API 36**, `minSdk` 25. APK doğrulandı: `aapt` → `targetSdkVersion:'36'`, `sdkVersion:'25'`.
- **Bilinen hatalar değişti:** `Google.IOSResolver.dll` ve `Unable to copy 'mainTemplate.gradle'` hataları artık yok
  (Plugins/Android var, yeni EDM iOS modülü yokken hata basmıyor). Konsolda hata yok; uyarılar: Play Games
  `CS0618` (Social API eskidi), MainMenu'de eksik reklam betikleri (eski, Faz 4).
- **GI Cache:** F:'ye taşımadan sonra varsayılan `LocalLow\Unity\Caches\GiCache` junction'ı Unity'de
  "Failed to create GICache directory" döngüsüne giriyordu ve build bitmiyordu. Editör tercihi (GI Cache özel yol)
  `F:/Unity/Cache/LocalLow-Unity-Caches/GiCache` yapıldı. Klonlar aynı kullanıcı tercihini paylaşır.
- **Commit'lenmeyen (50 MB üstü, yalnız masaüstü editörü):** `Assets/Firebase/Plugins/x86_64/FirebaseCppApp-14_1_0.bundle`
  (macOS, 100 316 448 bayt) ve `FirebaseCppApp-14_1_0.so` (Linux, 78 851 080 bayt) + `.meta`'ları. Windows ve Android
  için gerekmez; diskte izlenmeyen dosya olarak duruyorlar. Karar ana oturumun: gitignore, LFS ya da silme.
- **İP3 için:** Play Games kurulum sihirbazı yapılmadı. `GooglePlayGamesManifest.androidlib/AndroidManifest.xml` içinde
  `APP_ID` boş, `Assets/GooglePlayGames/Resources/PlayGamesSettings.asset` boş; build bunlarla geçiyor.
- `Assets/StreamingAssets/google-services-desktop.json` ve `Assets/Plugins/Android/FirebaseApp.androidlib/` üretildi,
  gitignore'da (doğrulandı). Klasör `.meta`'sı `Assets/StreamingAssets.meta` commit edilmedi.
- Manifest değişmedi; klonlara kopya gerekmedi (SDK'lar `Assets` altında, junction ile görünür).
