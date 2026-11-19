# Dil sistemi (yerelleştirme) tasarımı

Tarih: 2026-11-19. Durum: onay bekliyor.

## Amaç ve neden

Oyun bugün yalnızca Türkçe. Bütün oyuncuya görünen metinler koda, ekran kuran Editor
builder'larına ve masa sahnesine gömülü. Osman İngilizce'yi, sonra da istediği dilleri
ekleyebilmek istiyor; bunun "Excel gibi bir tablodan" yönetilen sistemli bir yapı olmasını
istiyor. Yeni dil eklemek koda dokunmadan, tabloya sütun ekleyerek yapılabilmeli.

Ses sistemi ayrı bir iştir; bu belge yalnızca dili kapsar. Ses bu iş bitince kendi
tasarım belgesiyle gelir.

## Hedefler

- Oyuncuya görünen her metin tek tablodan gelir.
- Tablo projede duran bir CSV dosyasıdır; Excel veya Google Sheets ile açılıp düzenlenir.
- Türkçe ve İngilizce eksiksiz çıkar.
- Yeni dil = tabloya yeni sütun. Kod, sahne, prefab değişmez.
- Dil ilk açılışta telefonun dilinden seçilir; ayarlardan değiştirilir, seçim hatırlanır.
- Eksik, yanlış veya kullanılmayan satırları bir test ve bir Editor menüsü yakalar.

## Hedef dışı

- Mağaza ürün adları ve fiyatları ("Ürün 6", "19.99 TL"). Bunlar yer tutucu; gerçek içerik
  mağazadan gelecek. Mağazadaki sabit etiketler ("Satın Al", "Remove ADS") tabloya girer.
- Oyuncu adları, masa adları. Kullanıcının yazdığı veridir, çevrilmez.
- Misafir adı öneki `Misafir-`. Kayıtlı adlarda duruyor ve misafir adını tanımak için
  kullanılıyor (`GameConfig.IsGuestName`); sabit kalır. (Benim kararım.)
- Log, test ve hata ayıklama metinleri.
- Uygulama adı, simge, Play mağaza sayfası çevirisi.
- Sağdan sola diller (Arapça, İbranice) için ekran aynalama.
- Çoğul kuralları motoru. Sayıya göre değişen metin gerekirse ayrı satır açılır.
- Sayı ve tarih biçimlendirme (binlik ayırıcı vb.).

## Yapılmaması gerekenler

- Bir istemcinin dili başka istemcinin ekranına sızmamalı. Bugün ceza nedeni Türkçe cümle
  olarak ağdan gidiyor; bu kapanır.
- Çeviri eksik diye boş veya anahtar adı görünen bir düğme kalmamalı.
- Çok editörlü senkron testi bozulmamalı.
- Excel'de kaydedince Türkçe harflerin bozulması sessizce oyuna girmemeli.

## Kısıtlar

- Unity 6, uGUI + TextMeshPro, Photon PUN 2. Menü ekranları Editor builder'larıyla üretilir
  (`Assets/Osman/Editor/UiKit*.cs`); o sahne parçaları elle düzenlenmez.
- Masa sahnesi (`Table.unity`) elle yapılmıştır, builder yoktur.
- Changa fontu Latin ve Arapça harfleri kapsar, Kiril yok. Font atlası dinamiktir.
- Ayarlar `GameSettings` içinde PlayerPrefs ile tutulur (`Okey.Sound`, `Okey.Music`).

## Yaklaşımlar

1. **Projede CSV + küçük okuyucu (seçilen).** Tek dosya, satır = metin, sütun = dil.
   Excel ile açılır, git'te değişiklik satır satır görünür, ek paket ve internet gerekmez.
   Eksisi: çoğul kuralı ve tablo düzenleyicisi hazır gelmez; ikisine de bu oyunda gerek yok.
2. **Google Sheets + Editor'de indirme düğmesi.** Çevirmenle paylaşmak kolay; ek olarak
   bağlantı ayarı ve indirme adımı ister. Sonradan 1'in üstüne eklenebilir, çünkü çıktısı
   aynı CSV'dir.
3. **Unity Localization paketi.** Resmi çözüm; Addressables bağımlılığı getirir, derlemeyi
   ağırlaştırır. Ekranlar kodla üretildiği için kazancı az.

Osman 1'i seçti.

## Tasarım

### Tablo

Dosya: `Assets/Resources/Localization.csv` (çalışma zamanında `Resources.Load<TextAsset>`).

```
key,tr,en,note
lang.name,Türkçe,English,Ayarlardaki dil seçicide görünen ad
lobby.play_now,HEMEN OYNA,PLAY NOW,
room.rounds,{0} el,{0} rounds,{0} = el sayısı
toast.connection_timeout,Bağlantı zaman aşımına uğradı,Connection timed out,
```

- İlk satır başlıktır. `key` ve `note` dışındaki her sütun bir dildir; sütun adı dil
  kodudur (`tr`, `en`, `de`...).
- `note` çevirmene açıklamadır, oyuna girmez.
- `lang.name` satırı her dilin kendi adını taşır. Dil seçici listeyi buradan kurar; bu
  yüzden yeni sütun eklemek dili seçilebilir yapmaya yeter.
- Değerler `{0}`, `{1}` yer tutucusu ve TMP etiketi (`<color=...>`, `<link=...>`, `<size=...>`)
  içerebilir. Satır sonu hücre içinde gerçek satır sonudur (tırnaklı hücre).
- Anahtar biçimi: `bölüm.ad`, küçük harf, alt çizgi. Bölümler: `common`, `login`, `lobby`,
  `room`, `settings`, `profile`, `account`, `shop`, `table`, `penalty`, `toast`, `error`, `lang`.

Okuyucu şunları kaldırmalı, çünkü Excel bunları üretir:

- Ayırıcı virgül, noktalı virgül veya sekme olabilir (Türkçe Excel noktalı virgülle kaydeder).
  Ayırıcı başlık satırından anlaşılır.
- Tırnaklı hücre, hücre içinde ayırıcı, çift tırnak kaçışı, hücre içinde satır sonu.
- Baştaki UTF-8 BOM. Dosya BOM'lu UTF-8 olarak tutulur ki Excel Türkçe harfleri doğru açsın.
- Boş satırlar ve `#` ile başlayan satırlar atlanır (tabloda bölüm başlığı için).

### Çalışma zamanı

`Assets/Osman/Scripts/Localization/` altında:

- `LocTable` — saf C#, Unity'ye bağlı değil. CSV metnini ayrıştırır; dil listesi, anahtar
  listesi ve `Get(lang, key)` verir. EditMode testleri bunun üstünde koşar.
- `Loc` — statik giriş noktası.
  - `Loc.T(key)` ve `Loc.T(key, args)` (`string.Format`).
  - `Loc.Language` (oku/yaz), `Loc.Languages`, `Loc.Culture`, `event Action Changed`.
  - Tablo ilk kullanımda yüklenir.
  - Geri düşüş sırası: seçili dil → `tr` → anahtarın kendisi. Editor'de ve geliştirme
    derlemesinde eksik anahtar bir kez uyarı olarak loglanır.
  - Biçimlendirme hatası (yer tutucu sayısı tutmuyor) oyunu düşürmez; ham metin döner, uyarı loglanır.
- `LocText` — bir TMP metninin üstünde duran bileşen. `key` alanı vardır; açılınca ve
  `Loc.Changed` gelince metni yazar. Sabit etiketler için.
- `GameSettings.Language` — PlayerPrefs `Okey.Language`. Kayıt yoksa `Application.systemLanguage`
  Türkçe ise `tr`, tabloda karşılığı olan başka bir dilse o, değilse `en`. Kayıtlı dil tabloda
  artık yoksa aynı kurala dönülür.

### Metinlerin taşınması

- **Builder'lar.** `Text(...)` ve onu saran yardımcılar (`KitWindow`, `TextInput`, `ChoiceButton`,
  `KitRow`, `ToggleRow`, `LinkRow`, `RoundButton`, `FormLabel`, `LoginWindow`) düz metin yerine
  anahtar alır. Yardımcı `LocText` ekler ve Editor'de görünsün diye Türkçe karşılığını yazar.
  Doğrudan `.text =` ile yazılan sekiz yer de aynı yola girer. Sonra builder menüleri yeniden
  çalıştırılır; sahneler ve prefablar yeniden üretilir. Builder'lar kendi kurdukları parçayı
  her seferinde baştan kurar; o parçalarda elle yapılmış değişiklik yoktur (önceki işin kuralı).
  Menüler bütün olarak çalıştırılır (Prefabları Kur, Giriş Ekranını Kur, Lobiyi Kur, Mağazayı
  Düzelt, Hesap Pencerelerini Kur); sahne farkında beklenen tek değişiklik `LocText` bileşenleridir.
- **Çalışma zamanı sabit metinleri** (bildirimler, onay pencereleri, yükleniyor yazıları, hata
  metinleri): `Loc.T("...")`. `UIRoot.Confirm/Notice` varsayılan düğme adları tablodan gelir.
  `JoinErrorText` ve `NameFlow` metin yerine anahtar döndürür; testleri anahtarı sabitler.
- **Değerle kurulan metinler** (yaklaşık 16 yer): `{0}` yer tutuculu satır + `Loc.T(key, ...)`.
  Renk etiketleri satırın içinde kalır, renk değeri argüman olur.
- **Masa sahnesi.** Yeni menü `Okey/UI Kit/Masa Metinlerini Bağla`: sahnedeki sabit etiketli
  yaklaşık 11 metni nesne adından bulur, `LocText` ekler. Bu sırada eksik Türkçe harfler
  tabloda düzelir (TAŞ İŞLE, GERİ BIRAK, ÇİFT AÇ, SERİ AÇ, Çıkış). Çalışırken dolan
  "New Text" yer tutucularına dokunulmaz.
- **Mağaza.** Builder dışı sabit etiketler aynı menü mantığıyla `LocText` alır;
  `ShopController` içindeki "Active" tabloya girer.

### Ağdan giden ceza nedeni

`GameManager.AnnouncePenalty(playerQue, amount, string reason)` nedeni bugün Türkçe cümle
olarak `ShowPenaltyRPC` ile herkese yolluyor; herkes masa sahibinin dilini görür.

- Yeni `enum PenaltyReason : byte` (TookSideCouldNotOpen, SideTileTaken, DiscardedOkey,
  DiscardedIndicator, DiscardedIslek, ProcessedOnMeld).
- RPC `byte` taşır. Her istemci nedeni kendi dilinde `penalty.*` anahtarından yazar.
- `DiscardPenaltyReason(...)` `PenaltyReason?` döndürür; "ceza var mı" denetimi (`!= null`) aynı kalır.
- Türkçe metne bağlı testler güncellenir: `OkeyRulesTests.cs:174-179` (ceza nedenleri) enum
  değerini, `JoinErrorTextTests` anahtarı sabitler.
- RPC imzası değiştiği için eski ve yeni sürüm aynı masada oynayamaz. Oyun yayında değil, kabul edilir.

### Dil değiştirme

- Ayarlar penceresine "Dil" satırı: sol/sağ oklu seçici (el sayısı seçicisiyle aynı görünüm),
  ortada dilin kendi adı. Ayarlar yalnızca lobiden açıldığı için dil masada değişmez.
- Seçince `Loc.Language` yazılır, kaydedilir, `Loc.Changed` tetiklenir.
- `LocText` kendini günceller. Metni koddan yazan lobi parçaları (`LobbyAccountUI` profil
  çipi ve hesap satırları, `ButtonFuncs` masa kartları ve boş liste yazısı) `Loc.Changed`'e
  abone olup var olan yenileme yöntemlerini çağırır. Sahne yeniden yüklenmez.

### Büyük harf ve kültür

- `RoomCard.Initial` baş harfi `tr-TR` kültürüyle büyütüyor (`RoomCard.cs:10`; `ChipInitial`
  da onu çağırır). Bu `Loc.Culture`'a geçer (dil kodundan `CultureInfo`; kurulamazsa değişmez kültür).
- Tabloda büyük harfli metinler büyük harfle yazılır; kodda `ToUpper` ile üretilmez.
- `ChipInitial` baş harfi görünen çip metninden okuyor ("Ad · Seviye N"). Metin tablodan
  gelince de ad başta kalmalı; satırın notuna yazılır ve denetim bunu sınar.

### Denetim

EditMode testi (`LocalizationTests`) ve aynı denetimi çalıştıran menü `Okey/Dil/Tabloyu Denetle`:

| Denetim | Sonuç |
|---|---|
| Yinelenen anahtar | hata |
| `tr` veya `en` boş | hata |
| Diller arasında yer tutucu kümesi farklı (`{0}`, `{1}`) | hata |
| Bozuk karakter (U+FFFD) | hata |
| Kodda `Loc.T("...")` veya sahne/prefabda `LocText` anahtarı tabloda yok | hata |
| Tabloda olup hiçbir yerde kullanılmayan anahtar | uyarı |
| `tr`/`en` dışındaki dilde boş hücre | uyarı (Türkçe'ye düşer) |
| `Assets/Osman/Scripts` ve builder'larda oyuncuya giden Türkçe harfli düz metin kaldı mı | menüde liste (testte değil) |

Kodda anahtarlar her zaman sabit metin olarak yazılır (`Loc.T("lobby.play_now")`), değişkenden
kurulmaz; denetim kaynak dosyaları taradığı için bu gerekir. Tek istisna ceza nedenidir: enum'dan
anahtara çeviren tek bir `switch` her dalda sabit anahtar yazar.

`LocTable` birim testleri: üç ayırıcı, tırnak kaçışı, hücre içi satır sonu, BOM, boş ve `#` satırları,
geri düşüş sırası, biçimlendirme hatası.

### Yeni dil ekleme (sistem bittikten sonra)

1. `Localization.csv`'yi Excel'de aç, yeni sütun ekle, başlığa dil kodunu yaz (`de`).
2. Sütunu doldur; `lang.name` satırına dilin kendi adını yaz.
3. "CSV UTF-8" olarak kaydet.
4. `Okey/Dil/Tabloyu Denetle` çalıştır.

Dil ayarlarda kendiliğinden görünür. Changa'nın kapsamadığı bir alfabe (Kiril gibi) için yedek
font eklemek ayrı bir adımdır; denetim menüsü fontta bulunmayan harfleri listeler.

## Benim kararlarım (Osman değiştirebilir)

- İngilizce çevirileri ben yazarım; Osman tabloda gözden geçirir.
- Misafir adı öneki çevrilmez.
- Mağaza ürün adı ve fiyatları kapsam dışı.
- Dil seçici oklu seçici; açılır liste değil.
- Telefon dili Türkçe değilse ve tabloda yoksa İngilizce açılır.
- Anahtar adları İngilizce.

## Uygulayana bırakılanlar

- Anahtarların tam listesi ve adları (envanter: builder'larda ~35, çalışma zamanında ~70 sabit
  + ~16 değerli, masada ~11, mağazada birkaç etiket).
- Builder yardımcılarının yeni imzaları.
- Masa sahnesinde nesnelerin nasıl eşleneceği.

## Doğrulama

- EditMode testlerinin tamamı geçer.
- Editor'de iki dilde giriş, lobi, ayarlar, profil, masa ekranları gözle kontrol edilir;
  İngilizce metinlerde taşma aranır.
- Çok editörlü senkron testi (`Run-SyncTest.ps1 -Deals 1`) geçer; ceza RPC'si değişti.
- Biri Türkçe biri İngilizce iki editörde ceza yazısının her birinde kendi dilinde çıktığı görülür.
- Telefonda deneme Osman'dadır.
