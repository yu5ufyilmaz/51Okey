# Benzersiz isim ve çıkışın Profil'e taşınması: tasarım ve plan

Tarih: 2026-11-19. Dal `MDC-001-firebase`. Önceki iş: `docs/superpowers/plans/2026-11-19-giris-lobi-akisi.md`.
Yöneten: ana oturum (Fable). İşi tek alt ajan yapar, iki commit halinde (önce A, sonra B); sonra bağımsız gözden geçirme.

## Kullanıcının istediği (2026-11-19)

- İki oyuncu aynı ismi alamasın; isim takibi yapılsın. "İsim seçme kısmı çok önemli."
- "Hesaptan çık" Profil panelinde olsun.

## Kararlar

Kullanıcının verdiği:
- İki isim harfi harfine aynıysa aynıdır ("Ahmet" alınmışken "ahmet" alınabilir).
- Misafir ismini seçemez ve değiştiremez: otomatik `Misafir-12345` biçiminde (5 rakam) atanır; bu isimler de benzersizdir.
- Yeni Firestore kurallarını ana oturum konsoldan yayınlar (değişen satırlar kullanıcıya gösterilmiş olarak).

Ana oturumun verdiği (itiraz edilebilir):
- İsim kaydı Firestore'da `names/{isim}` belgesi: `{ uid, createdAt }`. İlk alan tutar. İsim değişikliği tek transaction:
  yeni ismi al, eskisini bırak, `users/{uid}.displayName` ve `nameClaimed: true` yaz.
- Her yeni profil `Misafir-NNNNN` ismiyle başlar (çakışırsa yeniden denenir). Google hesabının ismi hâlâ `Misafir-` ile
  başlıyorsa ya da ismi kayıtlı değilse isim sorulur. Misafire isim sorulmaz.
- Misafir Google'a bağlanınca (aynı hesap, artık Google) ismini seçebilir: Profil'de isim kutusu açılır, sonraki açılışta da sorulur.
- `Misafir-` ile başlayan isim (büyük/küçük harf fark etmez) ve `/` içeren isim seçilemez.
- Eski profiller (isim kaydı yok): açılışta onarılır. Misafir → yeni `Misafir-NNNNN`. Google → bugünkü ismi boşsa kaydedilir,
  doluysa isim sorulur.
- Hesaptan çıkmak ismi bırakmaz. Misafirden mevcut Google hesabına geçişte silinen misafirin isim kaydı da silinir.
- İsim kaydı ağ ya da izin hatasıyla başarısız olursa misafir oyuna yine girer (bir sonraki açılışta yeniden denenir);
  isim seçen oyuncuya "Bağlanılamadı" yazılır.
- Yerel arka uç (Editör): benzersizlik kontrolü yok, tek profil. Editör oyuncusu misafirdir, ismi `Misafir-NNNNN` olur.
  `SyncAutoTest` kendi ismini kullanmaya devam eder.
- Kurallar yalnız isim kaydının tekliğini korur (belge varsa `create` reddedilir). `users.displayName` ile kaydın eşleşmesi
  kuralda denetlenmez: v1'de istemci yazar (coin ile aynı güven düzeyi).

Bilinen sınırlar:
- Oyunu silen misafirin `Misafir-NNNNN` kaydı kalır (100.000 isimlik havuz; temizlik ayrı iş).
- Editörde yerel arka uçta isim sorma ekranı artık görünmez (yalnız Google hesabında çıkar): cihazda denenir.

## Firestore kuralı (eklenecek blok; mevcut `users` bloğu değişmez)

```
    match /names/{name} {
      // İsim kaydı: ilk alan tutar. update yok: var olan kayda yazılamaz, sahibi dışında kimse silemez.
      allow get: if request.auth != null;
      allow create: if request.auth != null && request.resource.data.uid == request.auth.uid;
      allow delete: if request.auth != null && resource.data.uid == request.auth.uid;
    }
```

## Arayüz sözleşmesi

```csharp
public enum NameResult { Ok, Invalid, Taken, Failed }

public class PlayerProfile { /* mevcutlar */ public bool NameClaimed; }   // Firestore alanı "nameClaimed"

public interface IProfileStore
{
    /* mevcutlar */
    Task<NameResult> SetDisplayNameAsync(string name);   // önce Task idi. Aynı isim + NameClaimed ise Ok (yazmadan).
}

public static class GameConfig
{
    public const string GuestNamePrefix = "Misafir-";
    public static string GuestName(int number);          // "Misafir-" + number % 100000, 5 hane ("Misafir-00042")
    public static bool IsGuestName(string name);         // tam biçim: önek + 5 rakam
    public static bool IsValidName(string name);         // 2-16, '/' yok, "misafir-" ile başlamaz (harf duyarsız)
}

public static class GameServices
{
    public static bool NeedsName { get; }   // HasSession && !Auth.IsGuest && (!Current.NameClaimed || IsGuestName(Current.DisplayName))
    public static bool CanChangeName { get; }   // HasSession && !Auth.IsGuest
    public static Task<NameResult> ChooseNameAsync(string name);   // oyuncunun seçtiği isim: IsValidName + SetDisplayNameAsync
}
```

- `SetDisplayNameAsync` biçim denetimi yapmaz (2-16 ve `/` dışında): misafir isimlerini de o yazar. Oyuncu girdisi yalnız
  `ChooseNameAsync` üzerinden geçer; o `IsValidName` ile `Misafir-` önekini reddeder ve misafirken `Invalid` döner.
- Profil her yüklendiğinde (`LoadProfileAsync` içinde, yükleme `Task`'ının parçası) isim güvenceye alınır:
  misafir ve (`!NameClaimed` ya da `!IsGuestName`) → rastgele `GuestName` ile en çok 5 deneme; Google ve `!NameClaimed` ve
  `!IsGuestName` → bugünkü ismi kaydetmeyi dener. Sonuç `Taken`/`Failed` olursa istisna atılmaz.
- `FirestoreProfileStore.DeleteAsync`: kullanıcı belgesinden önce, sahibi oysa `names/{DisplayName}` silinir.
- `LocalProfileStore.SetDisplayNameAsync`: `Ok` ya da `Invalid`; `NameClaimed = true` yazar ve dosyada saklar.

## Paket A — "Hesaptan çık" Profil panelinde (ilk commit)

Dosyalar: `Assets/Osman/Scenes/LobbyMenu.unity`, gerekirse `Assets/Osman/Scripts/UI/LobbyAccountUI.cs`.
- `SignOutButton` ve `SignOutStatus`, `SettingsPanel/Box` altından `ProfilePanel/Box` altına taşınır (yeniden yaratılmaz;
  onClick ve alan başvuruları korunur). Profil kutusunda `LinkGoogleButton` ile `CloseButton` arasına yerleşir; kutu
  gerekiyorsa uzatılır, hiçbir şey üst üste binmez. Görünürlük kuralı aynı: yalnız `CanSignOut`.
- Ayarlar panelinde boşalan yer kapatılır (Kapat düğmesi yukarı alınır ya da kutu kısaltılır).
- Elle doğrula (Editör, yerel): Profil'de "Hesaptan çık" görünür ve çalışır (seçim ekranına döner); Ayarlar'da yoktur.
  Ekran görüntüsü: Profil ve Ayarlar panelleri.
- Commit: `Hesaptan çık Profil panelinde`.

## Paket B — Benzersiz isim (ikinci commit)

Dosyalar: `Services/PlayerProfile.cs`, `IProfileStore.cs`, `GameConfig.cs`, `GameServices.cs`, `Local/LocalProfileStore.cs`,
`Firebase/FirestoreProfileStore.cs`, `UI/MainMenuUIHandler.cs`, `UI/LobbyAccountUI.cs`, `Scenes/LobbyMenu.unity`,
`firebase/firestore.rules`, testler `Assets/Osman/Editor/Tests/`.

Testler (önce kırmızı):
- `GameConfig`: `GuestName(42) == "Misafir-00042"`, `GuestName(123456) == "Misafir-23456"`; `IsGuestName` doğru:
  `"Misafir-00042"`; yanlış: `"Misafir-42"`, `"Misafir-abcde"`, `"misafir-00042"`, `"Ahmet"`, `null`.
  `IsValidName` yanlış: `"Misafir-12345"`, `"misafir-x"`, `"MISAFIR-1"`, `"a/b"`, eski yanlışlar; doğru: `"Ahmet"`, `"Misafirim"`.
- `LocalProfileStore`: yeni profilin `NameClaimed` değeri false; `SetDisplayNameAsync("Ahmet")` → `Ok`, isim ve
  `NameClaimed` true ve dosyada kalıcı; 17 harf → `Invalid`, isim değişmez; aynı isim yeniden → `Ok`.
- `GameServices` (yerel): oturumdan sonra `Profile.Current.DisplayName` `IsGuestName` ve `NameClaimed` true; `NeedsName`
  false (yerel oyuncu misafir); `CanChangeName` false; `ChooseNameAsync("Ahmet")` → `Invalid`, isim değişmez.
  Mevcut `NeedsName_TrueForDefaultName_FalseAfterRename` testi yeni kurala göre yeniden yazılır ya da kaldırılır.
- `FirestoreProfileStore.FromFields`: `nameClaimed` alanı eşlenir; alan yoksa false.
- İsim güvence mantığı saf ve testli olsun: karar (`misafir mi`, `NameClaimed`, `IsGuestName` → `Yok / MisafirAta / BugünküyüKaydet`)
  küçük bir saf fonksiyonda dursun ve tablo testiyle kapansın (`EntryFlow` deseni).

Arayüz:
- `MainMenuUIHandler.ConfirmName`: `ChooseNameAsync` sonucu `Taken` → "Bu isim alınmış", `Invalid` → "İsim 2-16 karakter olmalı",
  `Failed` → "Bağlanılamadı"; `Ok` → lobi. Düğme her sonuçta yeniden açılır.
- `LobbyAccountUI`: isim kutusu ve "Kaydet" yalnız `CanChangeName` iken görünür; misafirde isim düz yazı olarak görünür ve
  altında "İsim seçmek için Google'a bağlan" yazar. `SaveName` aynı dört sonucu aynı yazılarla gösterir; `Ok` sonrası
  `PhotonNetwork.NickName` eşitlenir. Google'a bağlanınca (`Linked`) kutu açılır ve `LinkStatus` "İsmini seçebilirsin" yazar.
- Sahne: misafir için düz isim yazısı ve ipucu satırı eklenir; `NameInput`'a dokunulmadan gizlenir/gösterilir.

Kapı: `Run-EditModeTests.ps1 -Filter Tests` yeşil; `Check-Secrets.ps1` temiz; Editörde yerel arka uçta play: lobiye isim
sorulmadan girilir, kutucukta `Misafir-NNNNN` yazar, Profil'de isim kutusu yoktur, konsolda yeni hata yok; tek editörlü
senkron testi (`Run-SyncTest.ps1 -Solo -Mode smart -Seed 11`) geçer.
Commit: `Benzersiz isim: isim kaydı, otomatik misafir ismi`.

## Sonra (ana oturum)

- Kuralları konsoldan yayınla; `firebase/firestore.rules` ile aynı olduğunu doğrula.
- APK, telefon: misafir girişi → `Misafir-NNNNN`; Google ile gir → isim sorulur; alınmış isim reddedilir; Profil'den değişir.

## Sonuç (2026-11-19)

Commit'ler: `d851d00` (çıkış Profil'de), `5be090d` (benzersiz isim), `858f609` (gözden geçirme düzeltmeleri). Push yok.

- EditMode 195/195; tek editörlü senkron testi `5be090d` üzerinde geçti. Bağımsız gözden geçirme: kritik yok; iki önemli
  bulgu (eski isim bellekten okunuyordu → transaction artık önce `users/{uid}` okur; silmede isim + kullanıcı belgesi tek batch)
  ve iki küçük bulgu düzeltildi.
- Kurallar `okey51-dev` üzerinde 05:18'de yayınlandı; konsol metni `firebase/firestore.rules` ile birebir aynı.
- APK (`858f609`, 05:23) telefona kuruldu. Açılışta Google hesabı geri yüklendi; onarım `names/Duzz` kaydını oluşturdu ve
  `users/{uid}.nameClaimed = true` yazdı (Firestore konsolunda görüldü). Yani isim transaction'ı canlı kurallardan geçiyor.
  Logda izin ya da isim hatası yok.
- Kanıtlanmadı: misafire `Misafir-NNNNN` atanması (açılıştan sonra kullanıcı çıkış → Misafir yaptı, ama bakıldığında o
  misafirin belgesi ve isim kaydı yoktu; Google'a geri geçişte ikisinin birlikte silinmesiyle tutarlı, logu yok),
  "Bu isim alınmış" reddi, Profil'den isim değiştirme, yeni Google hesabında isim sorma.
- Bırakılan küçük bulgular: `Mısafir-` (noktasız ı) benzeri önek engellenmiyor; `Misafir-` ile başlayan isim için hata
  yazısı uzunluk yazısıyla aynı.
