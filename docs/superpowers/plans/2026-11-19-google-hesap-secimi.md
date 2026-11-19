# Hesap seçtiren Google girişi: tasarım ve plan

Tarih: 2026-11-19. Dal `MDC-001-firebase`. Önceki işler: `2026-11-13-google-giris.md`, `2026-11-19-giris-lobi-akisi.md`,
`2026-11-19-benzersiz-isim.md`. Yöneten: ana oturum (Fable); işi alt ajan yapar, sonra bağımsız gözden geçirme.

## Kullanıcının istediği (2026-11-19)

"Google ile gir" hangi hesapla girileceğini sormuyor, telefonda kayıtlı olanla doğrudan giriyor. Hesap seçtirsin.

## Neden böyle

Play Games Services v2 oyun içinde hesap seçtirmez ve çıkış yaptırmaz; hesap telefon ayarlarından yönetilir. Çözüm giriş
yöntemini değiştirmek: Play Games kimliği yerine normal "Google ile oturum aç" (Android Credential Manager) ve Firebase'de
`GoogleAuthProvider`.

## Kararlar

Kullanıcının verdiği: hesap seçtiren girişe geçilir.

Ana oturumun verdiği (itiraz edilebilir):
- Google'ın hazır Unity eklentisi (`googlesamples/siwg-mobile-unity-plugin`) kullanılmaz: Kotlin ile gelir ("Unity 6.5+ için
  denendi", bizde 6000.3.10 ve Gradle 8.x), gradle şablonlarını elle birleştirmek gerekir, çıkış (`clearCredentialState`) ve
  iptal ayrımı yoktur, "etkin geliştirme için değil" notu vardır. Yerine aynı işi yapan küçük bir **Java** köprüsü yazılır
  (Kotlin derlemesi yok, şablon birleştirme yok).
- Akış: `GetSignInWithGoogleOption` (düğmeye basınca açılan tam hesap seçme penceresi). Alttan çıkan sessiz sayfa
  (`GetGoogleIdOption`) kullanılmaz: hesapta "oturum açma istemleri" kapalıysa hiç görünmez.
- Web istemci kimliği koda ve depoya yazılmaz: `google-services.json`'dan üretilen `default_web_client_id` Android
  kaynağından okunur (dosya gitignore'da). Kaynak yoksa giriş `Failed` döner ve tek satır uyarı loglanır.
- Çıkışta `clearCredentialState` çağrılır.
- Play Games eklentisi projede kalır (ileride skor tablosu vb. için), ama giriş için artık kullanılmaz.
- Bugünkü Play Games kimlikli hesaplar (ör. `Duzz`) yeni yöntemle **ayrı** Firebase hesabıdır; taşınmaz. Geliştirme verisi.

Bilinen sınırlar:
- OAuth izin ekranı "Testing" durumunda: yalnız test kullanıcısı olarak eklenmiş Google hesapları girebilir. Listede olmayan
  hesap seçilirse Google reddeder. Herkese açmak için izin ekranı yayına alınmalı (yayından önce ayrı iş).
- Play Games SDK'sı projede kaldığı için açılıştaki otomatik Play Games girişi (karşılama balonu) sürer.
- Editörde Google girişi yine yok (yalnız Android cihazda).

## Konsol işleri (ana oturum, kullanıcı onayıyla)

1. Firebase Authentication → Sign-in method → Google sağlayıcısını aç (destek e-postası: proje sahibinin hesabı).
2. Firebase proje ayarları → Android uygulaması `com.modiacgames.okey51` → debug SHA-1 parmak izi ekli değilse ekle.
3. `google-services.json`'ı yeniden indir, `Assets/google-services.json` üzerine yaz (gitignore'da; commit edilmez).
   İçinde bu paket için `client_type: 3` (web) istemcisi olmalı.

## Sözleşme

```java
// Assets/Plugins/Android/GoogleSignInBridge.java  (paket: com.modiacgames.okey51.auth)
public final class GoogleSignInBridge {
    public interface Callback { void onToken(String idToken); void onCancelled(); void onError(String type); }
    public static void signIn(android.app.Activity activity, Callback cb);   // GetSignInWithGoogleOption + default_web_client_id
    public static void signOut(android.app.Activity activity);                // clearCredentialStateAsync; hata yutulur, loglanır
}
```
- `onError(type)`: yalnız istisna türü / kısa kod (ör. `NoCredential`, `NoWebClientId`, `GetCredentialUnknown`); ileti metni,
  e-posta ya da token loglanmaz ve C#'a taşınmaz.
- İptal: `GetCredentialCancellationException` → `onCancelled`.
- Geri çağrılar hangi iş parçacığından gelirse gelsin C# tarafı sonucu ana iş parçacığında tamamlar (mevcut
  `TaskCompletionSource` deseni; Play Games geri çağrılarında olduğu gibi).

```csharp
// Assets/Osman/Scripts/Services/Firebase/GoogleSignIn.cs  (yalnız UNITY_ANDROID && !UNITY_EDITOR içinde gerçek)
static class GoogleSignIn
{
    public static Task<(GoogleLinkFlow.Step step, string idToken)> SignInAsync();   // Ok+token / Cancelled / Failed
    public static void SignOut();
}
```

`FirebaseAuthService` değişiklikleri (arayüz imzaları aynı kalır):
- `LinkOnDeviceAsync`: Play Games adımları (`PlayGamesSignInAsync`, `ServerAuthCodeAsync`) kalkar; `GoogleSignIn.SignInAsync`
  ile ID token alınır, `GoogleAuthProvider.GetCredential(idToken, null)` kullanılır.
- Hesap seçme penceresi bir akışta **bir kez** açılır: token akış boyunca saklanır; "hesap başkasına bağlı → geçiş" yolunda
  yeniden sorulmaz. Geçişte önce `FirebaseAccountLinkException.UserInfo.UpdatedCredential` (varsa ve geçerliyse), yoksa aynı
  token'dan üretilen kimlik bilgisi kullanılır.
- `SignOutAsync`: `auth.SignOut()`'tan sonra `GoogleSignIn.SignOut()`.
- `using GooglePlayGames` bu dosyadan kalkar. `IsCredentialInUse` ve geri dönüş (yeni misafir) davranışı aynen kalır.
- Yorumlar güncellenir (Play Games anlatan satırlar).

Bağımlılıklar: `Assets/Osman/Editor/GoogleSignInDependencies.xml` (EDM4U):
`androidx.credentials:credentials:1.3.0`, `androidx.credentials:credentials-play-services-auth:1.3.0`,
`com.google.android.libraries.identity.googleid:googleid:1.1.1`. Sonra EDM "Force Resolve" (`mainTemplate.gradle` güncellenir
ve commit edilir). Sürüm çakışması çıkarsa çözen en düşük sürüme çıkılır ve rapora yazılır.

R8/IL2CPP: köprü sınıfı ve `Callback` arayüzü için koruma kuralı gerekiyorsa (`AndroidJavaProxy` yansıma kullanır) eklenir.

## Kapı

- `Run-EditModeTests.ps1 -Filter Tests` yeşil (195); `Check-Secrets.ps1` temiz.
- `AndroidDevBuild.Build()` APK üretir (Java derlenir, bağımlılıklar çözülür, çift sınıf hatası yok). Gradle
  `compressDebugAssets ... already contains entry` hatası çıkarsa
  `Library/Bee/Android/Prj/IL2CPP/Gradle/launcher/build/intermediates/compressed_assets` silinip yeniden derlenir.
- Editörde play: giriş ekranı ve lobi eskisi gibi (yerel arka uç), konsolda yeni hata yok.
- Commit: `Google girişi: hesap seçtiren oturum açma (Credential Manager)`.

## Sonra (ana oturum)

Konsol işleri, yeni `google-services.json` ile APK, telefonda: "Google ile gir" hesap listesini açar; seçilen hesapla girilir;
çıkıştan sonra yine sorar; iptal → "Google girişi tamamlanmadı"; misafir → "Google'a bağla" → aynı pencere.

## Sonuç (2026-11-19)

Commit: `ea39df5` (Java köprüsü `GoogleSignInBridge`, `GoogleSignIn.cs`, `FirebaseAuthService`, EDM bağımlılıkları). Push yok.

- EditMode 195/195. Bağımsız gözden geçirme: kritik/önemli kod bulgusu yok.
- Konsol: Firebase Auth'ta Google sağlayıcısı açıldı; SHA-1 zaten ekliydi. `google-services.json` otomatik sekmeden inmedi;
  web istemcisi (`client_type: 3`) gitignore'daki dosyaya elle eklendi, `default_web_client_id` kaynağı üretildi.
  OAuth test kullanıcıları: `osmansisman433`, `osmansisman1453`.
- İlk kurulumda açılışta çöktü: `FirebaseInitProvider ... Resources$NotFoundException: Unable to find resource ID #0x7f0a001b`.
  Neden: artımlı Gradle derlemesi kaynak tablosunu yeniledi (aynı kaynak `0x7f0c001b` oldu) ama eski numaralarla derlenmiş
  kodu yeniden kullandı. Çözüm: `Library/Bee/Android/Prj/IL2CPP/Gradle/{launcher,unityLibrary}/build` silinip temiz derleme.
  Android bağımlılığı ya da kaynağı değişince artımlı derlemeye güvenme.
- Telefonda doğrulandı (log): çıkış → "Google ile gir" → hesap seçme → giriş, iki farklı Google hesabıyla, üç kez; her çıkıştan
  sonra yeniden soruldu; yeni hesaplara isim soruldu ve isimler kaydedildi (`Duzz`, `Duzz48`).
- Eski Play Games kimlikli hesap (`Duzz41`) yeni yöntemle erişilemez kaldığı için ismi dolu kalıyordu; kullanıcının onayıyla
  `users/i1DW…` (defteriyle) ve `names/Duzz41` Firestore'dan silindi. Firebase Auth'taki eski kullanıcı kaydı duruyor.
- Denenmedi: pencerede iptal, misafir → "Google'a bağla" (yeni yöntemle), test kullanıcısı olmayan hesap, hesapsız cihaz.
- Bırakılan küçük bulgular: iptal loglanmıyor; Credential Manager hiç geri dönmezse zaman aşımı yok; sürüm derlemesinde
  (R8) `@Keep` yeterliliği denenmedi; Play uygulama imzası SHA-1'i için ayrı Android istemcisi gerekecek.
