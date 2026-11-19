# Ses sistemi tasarımı

Tarih: 2026-11-19. Durum: onay bekliyor.

## Amaç ve neden

Oyunda hiç ses yok: ses kodu, ses dosyası, `AudioSource` yok. Osman her olayın kendi sesiyle
duyulmasını istiyor: taş çekme, taş atma, taş sürükleme, taşların dizilmesi, ceza yeme, ceza
yedirme ve türüne göre ayrı düğme sesleri ("odaya girme farklı, seri-çift açma farklı").

## Hedefler

- Her olayın bir adı ve o ada bağlı bir sesi olur; aynı ses her yerde çalmaz.
- Sesler tek bir yerden eşlenir: Unity'de bir "Ses Listesi" dosyası. Ses değiştirmek koda dokunmaz.
- "Kim yaptı" ayrımı duyulur: kendi hamlem tam ses, rakibin ve botun hamlesi kısık; bana gelen
  ceza ile başkasına gelen ceza ayrı sesler.
- Ayarlardaki Ses düğmesi bütün sesleri açıp kapatmaya devam eder.
- Oyun mantığına, ağ mesajlarına ve senkrona dokunulmaz.

## Hedef dışı

- Müzik. Ayarlardaki Müzik düğmesi bağlanmadan kalır.
- Titreşim.
- Ses yüksekliği kaydırıcısı.
- Taşların tek tek gelerek dizilme animasyonu. Dağıtımda 14 taş tek karede beliriyor; o ana tek
  bir "dizilme" sesi konur.
- Bildirim (toast), yükleniyor ekranı ve mağaza satın alma sesleri.
- Sesin 3 boyutlu konumlanması (rakibin sesi kendi yönünden gelmez).

## Yapılmaması gerekenler

- Aynı ses bir karede üst üste çalıp patlamamalı (bir el açılınca rakipler taşları tek karede
  görür; cezalar atışta art arda gelir).
- Otomatik senkron testi sırasında dört editörden ses gelmemeli.
- Ses dosyası eksik diye oyun hata vermemeli; sessiz geçmeli.
- Çok editörlü senkron testi bozulmamalı.
- Kullanım hakkı belirsiz ses dosyası projeye girmemeli.

## Kısıtlar

- Menü ekranları Editor builder'larıyla üretilir (`Assets/Osman/Editor/UiKit*.cs`); o parçalar elle
  düzenlenmez. Masa sahnesi (`Table.unity`) elle yapılmıştır; `Okey/UI Kit/Masa Metinlerini Bağla`
  menüsü ona dokunan tek builder'dır.
- `GameSettings.SoundOn` bugün `AudioListener.volume`'u 0/1 yapar; bu davranış kalır.
- Otomatik test bayrağı: `SyncAutoTest.Instance != null`.
- Ses dosyaları: Kenney'nin CC0 lisanslı paketleri (kenney.nl). Destek sayfası: ticari projede
  serbest, atıf zorunlu değil. Paket sayfaları içeriği yazmıyor; uygun ses ancak dosyalar indirilip
  adlarına, sürelerine ve türlerine bakılarak seçilir. Claude sesi duyamaz; son karar Osman'ın kulağıdır.
- İndirme için Osman'dan ayrıca onay alınır (dosya adı, kaynak, boyut). Bu belgenin onayı indirme onayı değildir.

## Yaklaşımlar

1. **Unity'de ses listesi (seçilen).** Tek bir `ScriptableObject`: her satır bir olay, ses
   dosyası (veya rastgele seçilen çeşitler), yükseklik, tekrar süresi. Inspector'da dinlenir,
   sürükle-bırak ile değişir. Yanlış dosya adı diye bir hata olamaz.
2. **CSV tablosu (dil sistemi gibi).** Excel'de düzenlenir; ama ses kulakla seçildiği için yine
   Unity'ye dönülür, dosya adı elle yazılır.
3. **Sesleri nesnelerin üstüne dağıtmak** (her düğmede, her betikte ayrı `AudioSource`). Tek
   yerden yönetilemez; elendi.

Osman kararı Claude'a bıraktı ("hangisi doğru yaklaşımsa"); 1 seçildi.

## Tasarım

### Parçalar

`Assets/Osman/Scripts/Audio/` altında:

- `SfxId` — olay adları. Açık numaralı `enum`; numaralar liste dosyasında saklandığı için yalnızca
  sona eklenir, sıra değişmez, numara yeniden kullanılmaz.
- `SfxLibrary : ScriptableObject` — `Assets/Resources/SfxLibrary.asset`. Satır:
  `id`, `clips[]` (birden çoksa rastgele biri), `volume` (0-1), `pitchJitter` (0-0.2; her çalışta
  perde ±bu kadar oynar, tekrar eden tık sesleri mekanik durmasın diye), `minInterval` (saniye;
  bu süre dolmadan aynı ses yeniden çalmaz).
- `Sfx` — statik giriş: `Sfx.Play(SfxId id, float volumeScale = 1f)`.
  - İlk çağrıda kalıcı (`DontDestroyOnLoad`) bir nesne ve 2B `AudioSource` kurar; `PlayOneShot` ile çalar.
  - `SyncAutoTest.Instance != null` ise çalmaz.
  - Listede satır ya da dosya yoksa çalmaz; Editor'de olay başına bir kez uyarı loglar.
  - `Sfx.OtherVolume = 0.5f`: rakip/bot hamlelerinde kullanılan çarpan.
  - Doğrulama için: son çalınan olayların küçük bir listesi (`Sfx.Recent`, en çok 32) tutulur.
- `SfxGate` — saf C#: "bu ses şimdi çalabilir mi" kararı (`minInterval`). Testi Unity'siz koşar.
- `UiSfx` — düğme bileşeni. `id` alanı (varsayılan `UiClick`). `IPointerClickHandler`; yanındaki
  `Button`/`Toggle` etkin ve tıklanabilirse çalar. Basıp dışarı sürüklenen dokunuşta çalmaz.

### Olaylar

Kendi hamlem tam ses; "rakip" sütununda işaretli olanlar başkasının hamlesinde aynı sesi
`Sfx.OtherVolume` ile çalar.

| SfxId | Ne zaman | Rakipte de | Bağlanma yeri |
|---|---|---|---|
| `TilePickUp` | Taşı elime aldım (sürükleme başladı) | — | `TileUI.OnBeginDrag`, gerçek başlangıç (`held = this`) |
| `TileDrop` | Taşı ıstakada bir yere bıraktım (çekme değilse) | — | `TileUI.OnEndDrag` ıstaka dalları (yer değiştirme ve boş yuva) |
| `TileSnapBack` | Bırakma geçersiz, taş geri döndü | — | `TileUI.OnEndDrag` geri dönüş dalları |
| `TileDraw` | Ortadan taş çekildi | ✓ | `TileDistrubite.AddTileFromMiddlePlayerList(playerNumber)` |
| `TileTakeSide` | Yandan (atılan) taş alındı | ✓ | `TileDistrubite.AddTileFromDropPlayerList(playerNumber)` |
| `TileDiscard` | Taş atıldı | ✓ | Ben: `TileUI.OnEndDrag` kabul edilen atış dalı, bırakma anında (eli bitiren atış dahil). Rakip: `TileDistrubite.RemoveTileFromPlayerList`, `playerNumber != myQue`. Bot: `BotDiscardTileRPC` |
| `TilesDeal` | Taşlar dağıtıldı, ıstakaya dizildi | herkes aynı | `TileDistrubite.DistributeTilesToAllPlayers` |
| `OpenRuns` | Seri açtım (masaya gerçekten per yerleşti) | — | `ScoreManager.PlaceValidPers` içinde `hasOpenedAnyMeld`'in işaretlendiği yer |
| `OpenPairs` | Çift açtım (masaya gerçekten çift yerleşti) | — | `ScoreManager.PlacePairPers` içinde `hasOpenedAnyPair`'in işaretlendiği yer |
| `MeldShown` | Bir rakibin açtığı taşlar masaya geldi (rakip taşını atınca gelir, açtığı anda değil) | yalnız rakip | `TileDistrubite.MeldTiles` RPC'si, `playerNumber != myQue` (taş başına gelir; `minInterval` ile tek ses) |
| `TileProcess` | Taş işlendi | ✓ | Ben: `ScoreManager.PerformTileProcessing`; rakip: `TileDistrubite.SyncProcessedTileRPC` |
| `TilesCollect` | TAŞLARI TOPLA ile geri alındı | — | `ScoreManager.OnTakeBackButtonClick`'in üç başarı dalı (işlemeyi geri alma dahil) |
| `PutBack` | Yandan alınan taş geri bırakıldı | ✓ | `TileDistrubite.ReturnTileToSide(returningPlayerQue, …)` |
| `Error` | Yapılamadı | — | Masadaki eylem düğmelerinin her reddi (aşağıdaki not) |
| `PenaltyTaken` | Ceza bana geldi | — | `UIManager.ShowPenaltyFloat`, `playerQue == myQue` |
| `PenaltyGiven` | Ceza başka bir oyuncuya geldi | — | aynı yer, `playerQue != myQue` |
| `MyTurn` | Sıra bana geçti | — | `TurnManager.NextTurn`, `currentTurnPlayer == myQue`; elin ilk turu için `TurnManager.StartRoundVisuals` |
| `TimeLow` | Sıram ve süre 5 saniyenin altına indi (tur başına bir kez) | — | `UIManager.Update` içindeki süre çubuğu, kenar algılama |
| `Win` | Eli ben bitirdim | — | `GameManager.FinishGameRPC`, kazanan benim |
| `Lose` | Eli başkası bitirdi ya da taş bitti | — | aynı yer, kazanan ben değilim |
| `SeatTaken` | Bir oyuncu ya da bot koltuğa oturdu, koltuk değiştirdi | herkes aynı | `SeatManager.AssignSeatToPlayer` |
| `UiClick` | Varsayılan düğme tıkı | — | `UiSfx` (kit düğme prefablarının hepsinde) |
| `UiClose` | Pencere kapatma (X), "Vazgeç" | — | `UiSfx` (Btn_Close; onay penceresinin Vazgeç düğmesi) |
| `UiToggle` | Anahtar ve ok düğmeleri (Ses, Müzik, şifreli masa, dil, el sayısı) | — | `UiSfx` (Toggle; ok düğmeleri `Btn_Close` prefabından kurulduğu için builder'daki `Stepper` yardımcısında `UiToggle` olarak ezilir) |
| `UiPlayNow` | HEMEN OYNA | — | `UiSfx`, builder'da etiketlenir |
| `UiCreateTable` | MASA AÇ | — | `UiSfx` |
| `UiPlayBots` | BOTLARLA OYNA | — | `UiSfx` |
| `UiEnterRoom` | Odaya girme: karttaki OTUR, MASAYI KUR, şifre penceresindeki GİR | — | `UiSfx` |

Notlar:

- Masadaki SERİ AÇ, ÇİFT AÇ, TAŞ İŞLE, TAŞLARI TOPLA, GERİ BIRAK düğmeleri basınca tık çalmaz;
  ses sonuçtan gelir (başarı sesi ya da `Error`). Böylece "seri açma" ile "çift açma" gerçekten
  farklı duyulur ve başarısız denemede yanlış ses çıkmaz. Masadaki Çıkış ve TABLO düğmeleri `UiClick` alır.
- `Error` şu durumların hepsinde çalar: SERİ AÇ ve ÇİFT AÇ'ın her reddi (limit yetmiyor, sıra bende
  değil, atacak taş kalmıyor, önce diğer türü açmış olma, yerleşecek per yok), TAŞ İŞLE'de hiçbir taş
  işlenemediğinde, TAŞLARI TOPLA'da geri alınacak şey yokken, sürükleyerek işleme reddedildiğinde,
  atış reddedildiğinde (sıra bende değil, henüz taş çekmedim, yandan aldığım taşı kullanmadım).
  Bu dönüşlerin çoğu bugün sessiz `return`; her birine tek satır eklenir, akış değişmez.
- Tek harekette tek ses: ortadan ya da yandan taşı sürükleyip ıstakaya bırakınca `TilePickUp`, sonra
  yalnızca `TileDraw`/`TileTakeSide` çalar; `TileDrop` çalmaz. Reddedilen işleme ve atışta yalnızca
  `Error` çalar; `TileSnapBack` yalnızca ıstaka içindeki geçersiz bırakmada çalar.
- Süre dolunca oyunun benim yerime yaptığı hamle (`TileUI.SimulateDrag`) `TilePickUp`, `TileDrop` ve
  `TileSnapBack` çalmaz; `TileDraw` ve `TileDiscard` çalar.
- Rakibin tur sonu: açtığı perler, işlediği taşlar, attığı taş ve varsa cezası bana aynı anda gelir
  (oyun bunları atışla birlikte yolluyor). Sesler üst üste biner; rakip sesleri yarı yükseklikte
  olduğu için kabul edilir. Öncelik ya da kısma mekanizması kurulmaz.
- Oyuna el ortasında geri dönme bugün yok (oda oyun başlayınca kapanıyor, yeniden katılma kodu yok);
  bu yüzden birikmiş mesajların toplu çalmasına karşı ayrı bir kapı kurulmaz. Yeniden katılma
  eklenirse bu belgeye dönülür.
- `PenaltyGiven`: "ceza yedirme" sesi, cezaya ben sebep olmuş olsam da olmasam da başka bir oyuncu
  ceza yediğinde çalar. Kod "kimin yüzünden" bilgisini her ceza türünde taşımıyor. (Claude'un kararı.)
- İlk tur: el başında `SetTurnDirectly` ve `StartRoundVisuals` ikisi de çalışır; `SetTurnDirectly`
  sırasında yerel sıra numarası henüz kesin değildir, bu yüzden ilk tur için yalnızca `StartRoundVisuals` kullanılır.
- `TileDraw` zaman aşımı ve otomatik oynama (`SimulateDrag`) yollarından da geçer; bu istenen davranıştır.
- `TilesDeal` her yeni elde yeniden çalar.
- `SeatTaken`: masaya sonradan giren oyuncu önceki oturmaları toplu alır; `minInterval` (0.3 sn) tek sese indirir.
- Varsayılan `minInterval`: taş ve düğme sesleri 0.05 sn; `MeldShown`, `TileProcess`, `PenaltyTaken`,
  `PenaltyGiven`, `SeatTaken` 0.3 sn.

### Düğme sesleri nasıl bağlanır

- Kit düğme prefabları (`Btn_Green`, `Btn_Orange`, `Btn_Teal`, `Btn_Red`, `Btn_Close`) ve `Toggle`
  builder'da `UiSfx` alır; varsayılan `UiClick` (`Btn_Close` için `UiClose`, `Toggle` için `UiToggle`).
- Özel sesli düğmeler builder kodunda, düğme kurulduktan sonra etiketlenir (küçük bir yardımcı:
  `Sound(GameObject button, SfxId id)`). Elle prefab düzenlenmez.
- `Row` üstüne kurulan bağlantı satırları (Gizlilik, Şartlar, Destek, Hesabımı Sil) ve profil çipi `UiClick` alır.
- Oda kartındaki OTUR düğmesi `UiEnterRoom`.
- Masadaki Çıkış ve TABLO düğmeleri `Okey/UI Kit/Masa Metinlerini Bağla` menüsünde `UiSfx` alır
  (menü yeniden çalıştırılınca ikinci bileşen eklemez).

### Ses dosyaları ve liste

- Dosyalar `Assets/Osman/Audio/Sfx/` altında, olay adına göre adlandırılır (`tile_draw_1.ogg`).
- `Assets/Osman/Audio/KAYNAK.md`: her dosyanın geldiği paket, özgün dosya adı ve lisans (CC0).
- Menü `Okey/Ses/Ses Listesini Kur`: `SfxLibrary.asset`'i oluşturur ve **eksik satırları** varsayılan
  dosya, yükseklik ve tekrar süresiyle ekler. Var olan satırlara dokunmaz; Osman'ın Inspector'da
  yaptığı değişiklik menü yeniden çalışınca kaybolmaz.
- Aynı menü ses dosyalarının içe aktarma ayarlarını düzeltir (kısa sesler: mono, belleğe açılmış).
- Seçim yöntemi: paket dosyalarının adları, süreleri ve dalga biçimi. Taş sesleri için kısa, sert
  "tık/klak" (Casino Audio fiş/zar, Impact Sounds); düğmeler için Interface Sounds / UI Audio;
  kazanma/kaybetme/ceza için Music Jingles veya kısa arayüz sesleri. Uygun ses bulunamayan olay
  listede boş bırakılır ve belgeye yazılır; boş satır sessizdir, hata değildir.

### Denetim ve testler

- `SfxGate` birim testleri: süre dolmadan ikinci çalma reddedilir, dolunca kabul edilir, farklı
  olaylar birbirini engellemez.
- Liste testi: `SfxId`'nin her değeri için `SfxLibrary`'de tam bir satır vardır; numaralar benzersizdir;
  `clips` içindeki hiçbir öğe boş (kayıp dosya) değildir. Sesi bilerek boş bırakılan olaylar testte
  adıyla sayılan bir istisna listesindedir.
- Builder testi: kit düğme prefablarının hepsinde `UiSfx` vardır.
- `Okey/Ses/Ses Listesini Denetle` menüsü: sesi olmayan olayları, hiçbir yerde `Sfx.Play` ile ya da
  `UiSfx` üzerinden kullanılmayan olayları ve hiçbir satıra bağlı olmayan ses dosyalarını listeler.

## Claude'un kararları (Osman değiştirebilir)

- Yaklaşım 1 (Unity'de ses listesi).
- Rakip ve bot hamleleri yarı ses yüksekliğinde duyulur; sürükleme sesleri yalnızca kendi taşımda.
- "Dizilme" sesi dağıtım anında tek ses.
- Masadaki açma/işleme düğmelerinde tık yok, sonuç sesi var.
- `PenaltyGiven` her "başkası ceza yedi" durumunda çalar.
- Süre azalıyor uyarısı 5 saniyede, yalnızca kendi sıramda.
- Kazanma/kaybetme sesleri el bitince; oyun sonu için ayrı ses yok.
- Hangi dosyanın hangi olaya gittiği (Osman dinleyip değiştirir).

## Uygulayana bırakılanlar

- `SfxId` numaraları ve dosya adları.
- Her bağlanma yerinde tam satır (yukarıdaki yöntem adları kılavuzdur; satır numaraları kayabilir).
- İçe aktarma ayarlarının ayrıntısı.
- `Sfx.Recent`'in biçimi.

## Doğrulama

- EditMode testlerinin tamamı geçer.
- Editor'de botlarla bir el: `Sfx.Recent` üzerinden `TilesDeal`, `MyTurn`, `TileDraw`, `TilePickUp`,
  `TileDrop`, `TileDiscard`, rakip atışı ve bir ceza olayının tetiklendiği okunur. Menülerde
  HEMEN OYNA, MASA AÇ, X, anahtar ve dil oklarına basılıp doğru olayın çaldığı okunur.
- Çok editörlü senkron testi (`Run-SyncTest.ps1 -Deals 1`) geçer ve test sırasında `Sfx.Recent` boş kalır.
- Kulakla dinleme Osman'dadır: Editor'de ve telefonda. Claude seslerin nasıl duyulduğunu doğrulayamaz.

## Uygulamadan devreden notlar

Son durum: commit `3c707e8`, dal `MDC-001-firebase`, push yok.

Ne kuruldu:

- 28 olay, 35 ses dosyası (`Assets/Osman/Audio/Sfx/`, toplam 364 KB). Hangi olaya hangi dosyanın
  gittiği `Assets/Resources/SfxLibrary.asset` içinde; dosyaların geldiği paket ve özgün adları
  `Assets/Osman/Audio/KAYNAK.md` içinde. Hepsi Kenney, CC0; her dosya paketteki özgün dosyayla
  bayt bayt aynı (sha1 ile karşılaştırıldı).
- Menüler: `Okey/Ses/Ses Listesini Kur` (yalnızca eksik satırı ekler, var olan satıra dokunmaz),
  `Okey/Ses/Ses Listesini Denetle`, `Okey/Ses/Ses Düzeylerini Ölç`.
- Ses değiştirmek: `SfxLibrary` dosyasını Inspector'da aç, satırın `clips` alanına başka dosya
  sürükle. Bir olayı susturmak: `clips` alanını boşalt. Yüksekliği `volume` ile ayarla.

Ne, nasıl doğrulandı:

- EditMode: 337 test geçti, 0 başarısız.
- Ses ve dil denetimleri: hata yok (sesi olmayan olay 0, kullanılmayan olay 0, sahipsiz dosya 0).
- Tek başına masa testi (`Run-SyncTest.ps1 -Solo -Mode smart -Seed 11`) son commit üzerinde: PASS.
- Play modunda otomatik test olmadan botlarla bir el: koltuk, sıra, dağıtım, 24. saniyede süre uyarısı,
  otomatik atış, bot çekme ve atışları, ceza yeme, ceza yedirme, "yapılamadı" olay kaydından okundu;
  ses kaynağının çaldığı (`isPlaying`) okundu. Menü düğmelerinin her birinin doğru olayı çaldığı okundu.
- Oyun dosyalarındaki değişiklik iki ayrı incelemede satır satır sayıldı: ses çağrısı dışındaki her
  satır salt okumadır; koşul, dönüş, ağ mesajı ve sıra değişmedi.

Yapılmayanlar:

- **Hiçbir ses kulakla dinlenmedi.** Dosyalar ada ve süreye göre seçildi; yükseklikler Unity'de
  ölçülen ses düzeyinden hesaplandı.
- **Çok editörlü senkron testi koşmadı** (klon editörler Osman'ın isteğiyle kapalı). Otomatik testte
  her ses çağrısı ilk satırda döndüğü için risk düşük; klonlar açılınca bir kez koşulmalı.
- Telefonda hiçbir şey denenmedi.

Tasarıma göre değişenler:

- Düğme, basıldığı anda etkin olup olmadığını hatırlar; kendini pasifleyen giriş ve isim düğmeleri
  de tık çalar.
- İki ses kaynağı var: perdesi oynayan taş sesleri ile jingle'lar ayrı çalar.
- Ses kodu kendi rastgele sayı üretecini kullanır; taş dağıtımının üretecine dokunmaz.
- "Yapılamadı" sesi tasarımdakinden fazla yerde: açma düğmelerinde yerleşecek per çıkmadığında ve
  sıra bende değilken TAŞLARI TOPLA'da da çalar. Toplam 20 ret noktası.
- Kazanma/kaybetme sesi el başına yalnızca ilk bitiş mesajında çalar.
- `tile_drop` için iki çeşit kaldı (üçüncüsü diğerlerinden 5 dB kısıktı).
- Hata sesi dosyası `error_1.ogg` adını taşır.

Kulağa bırakılanlar (bilerek karar verilmedi):

- Jingle seçimleri: seri açma, çift açma, kazanma, kaybetme, ceza yeme. Yalnızca çalgı türünden seçildi.
- "Sıra bende" çanı fazla büyük olabilir.
- Dağıtım ve taşları toplama sesleri en yüksek ayarda bile taş tıklarından 3-8 dB kısık.
- Geri dönen taşın sesi en düşük ayarda bile hedeften biraz yüksek.
- El başında birinci oyuncu önce "sıra bende" çanını, yarım saniye sonra dağıtım sesini duyar.
- Eli bitiren atışta atış sesi kazanma jingle'ının başını örter.
- Rakip açıp attığında beş ses 0,2 saniyede üst üste gelir (tasarımda kabul edildi).
- Yandan alıp açamayınca art arda dört ses: yapılamadı, geri bırakma, çekme, ceza.

Bilinen açıklar:

- Berabere biten elde (taş bitince) herkes kaybetme sesini duyar.
- Masadaki OTUR düğmesi basınca tık çalmaz; oturunca "koltuk" sesi gelir.
- Masadaki "tekrar oyna" düğmesinin işlevi yok (ses işinden önce de yoktu).
- Oyuna yeniden katılma eklenirse birikmiş mesajlar toplu ses çalar; bugün yeniden katılma yok.
- Mağaza, bildirim, yükleniyor ekranı sessiz; müzik ve titreşim yok (kapsam dışı).
- Bir doğrulama turunda, yerel oyuncunun sırasında 12 saniye boyunca "ele alma / bırakma" olayları
  kimse dokunmadan kaydedildi. Kodda bu sesleri gerçek fare ya da dokunma girdisi olmadan çalan bir
  yol yok; o sırada Game penceresinde biri taş oynatmış olmalı. Doğrulanmadı.

Osman'ın kulakla dinleyeceği liste:

- Editor'de `SfxLibrary` Inspector'ında 28 satırı tek tek dinle; beğenmediğini değiştir.
- Telefonda bir el: çekme, yandan alma, atma, sürükleme, dağıtım, seri açma, çift açma, işleme,
  taşları toplama, geri bırakma, yapılamadı, ceza yeme, ceza yedirme, sıra, süre, kazanma, kaybetme.
- Bütün menü düğmeleri: HEMEN OYNA, MASA AÇ, BOTLARLA OYNA, MASAYI KUR, GİR, karttaki OTUR, X, anahtarlar, oklar.
- Ayarlardan Ses kapalıyken tam sessizlik.
- Dokunmayla ses arasında gecikme hissedilirse söyle (ses arabelleği ayarı küçültülebilir).

