# Menü arayüzü yenileme — tasarım

Tarih: 2026-11-19

## Amaç ve neden

Oyun Google Play'e çıkacak. Giriş, lobi, ayarlar ve profil ekranları bugün çalışıyor ama her sahne ayrı elle dizilmiş, ortak bir stil yok ve oyuncuya hata, yükleme ya da onay gösteren hiçbir şey yok. Hedef: bu ekranların tür rakipleriyle (101 Okey Plus ve benzerleri) aynı seviyede görünmesi ve davranması, Play'in zorunlu tuttuğu hesap silme ve gizlilik erişiminin yerinde olması.

Karşılaştırma kaynağı: 101 Okey Plus'ın Play mağaza görselleri. Oyunun kendisi kurulup denenmedi; diğer rakipler incelenmedi.

## Kapsam

Dört paket, bu sırayla. Her paket kendi başına çalışır durumda biter.

1. Ortak UI kiti ve kalıcı katman
2. Giriş ekranı (MainMenu sahnesi)
3. Lobi (LobbyMenu sahnesi)
4. Ayarlar ve profil

Oyun masası (Table sahnesi) bu belgenin dışında. Bu dört paket bittikten sonra kendi tasarım belgesiyle ele alınacak. Tek istisna: paket 1'in kalıcı katmanı masa sahnesinde de yaşar ve masadan çıkış onayı ile geri tuşu orada da çalışır.

## Hedefler

- Tek stil kaynağı: bir buton, bir pencere, bir bildirim, bir yükleme göstergesi. Hepsi prefab.
- Oyuncu her hatayı ekranda görür: bağlanamama, odaya girememe, yanlış şifre, dolu masa.
- Bekleme gerektiren her işte yükleme göstergesi: sahne geçişi, odaya girme, oda kurma, Google girişi.
- Android geri tuşu her ekranda tanımlı bir iş yapar.
- Üç sahne aynı ölçek ayarını kullanır, çentikli telefonlarda hiçbir buton kesilmez.
- Play zorunlulukları: uygulama içinden hesap silme, gizlilik politikası ve kullanım şartlarına erişim.

## Hedef olmayanlar

- Oyun masası HUD'ı (ayrı belge).
- Yeni çizim. Mevcut `Assets/Sprites/ui_kit` (dört renk buton, `panel_slate`, `row_slate`, `coin`) ve ChangaOne fontu kullanılır.
- Ses ve müzik. Projede hiç ses çalan kod yok (`AudioSource` kullanımı yok); ses dosyası gelene kadar eklenmez.
- Dil seçimi. Oyun yalnızca Türkçe.
- Arkadaş listesi, sohbet, günlük ödül, turnuva, görevler.
- MainMenu ve LobbyMenu sahnelerini birleştirmek.
- UI Toolkit'e geçiş.

## Olmaması gerekenler

- Giriş akışı, Google bağlama ve Photon bağlantı sırası davranış olarak değişmez. `EntryFlow`, `GameServices`, `GoogleLinkFlow` mantığına dokunulmaz; yalnızca ekran nesnelerine bağlandıkları yer değişir.
- Koltuk kuralları değişmez: masayı kuran ve `HEMEN OYNA` ile gelen ilk boş koltuğa oturur; dört kişi OTURUNCA geri sayım başlar; koltukları master verir.
- Mevcut testler (`Assets/Osman/Editor/Tests`) ve çok editörlü senkron testi bozulmaz.
- Yıkıcı buton (çıkış, hesap sil) sık kullanılan butonun yanına onaysız konmaz.

## Kısıtlar

- Unity uGUI 2.0 + TextMeshPro. Tween kütüphanesi yok; animasyon gerekiyorsa coroutine ile, yeni bağımlılık eklenmez.
- Yalnızca yatay ekran. Android min SDK 25.
- ChangaOne SDF'de emoji yok; simgeler sprite ya da düz metin olur.
- Sahneler çalışan Editor üzerinden `unity` CLI ile düzenlenir; sahne YAML'ı elle yazılmaz.
- Photon lobi listesi birkaç saniye gecikmeli güncellenir.
- Yeni `[PunRPC]` eklenirse RpcList'e kaydedilmelidir.

## Yaklaşımlar

**A. Ortak prefab kiti + kalıcı katman (seçilen).** Buton, pencere, bildirim ve yükleme prefab olur; sahneler bunlardan yeniden dizilir. Bildirim, yükleme, onay ve geri tuşu `DontDestroyOnLoad` tek bir katmanda yaşar. Mevcut akış kodu yerinde kalır. Bedeli: sahneler tek tek yeniden kurulur.

**B. Tek menü sahnesi.** Giriş ve lobi tek sahnede panel olur, aradaki yükleme kalkar. Bedeli: giriş akışı ve Photon bağlantı sırası yeniden yazılır; yeni oturmuş Google girişini riske atar. Yayından sonraya bırakıldı.

**C. UI Toolkit.** En temiz tema sistemi. Bedeli: tüm arayüz ve taş sürükle-bırak sıfırdan yazılır. Bu iş için fazla.

Osman yapım yolunu bana bıraktı; A benim kararım. Gerekçe: çalışan girişi ve senkronu bozmadan en çok görünür kazancı veriyor.

## Paket 1 — Ortak UI kiti ve kalıcı katman

### Prefab'lar (`Assets/Osman/Prefabs/UI/`)

- **Buton**: `ui_kit` 9-dilim sprite, ChangaOne etiket. Dört renk varyantı ve anlamları: yeşil = ana eylem, turuncu = ikincil eylem, teal = üçüncül, kırmızı = yıkıcı ya da kapat. Dokunma alanı en az 48dp; basılınca küçülme geri bildirimi.
- **Pencere**: `panel_slate` gövde, başlık satırı, sağ üstte kırmızı kapat, içerik alanı, altta buton satırı. Arkasında ekranı karartan ve dokunmayı yutan katman.
- **Satır**: `row_slate`, ayarlar ve profil satırları için.
- **Anahtar (toggle)**: ayarlardaki Ses ve Müzik, masa kurmadaki şifre anahtarı için.

### Kalıcı katman (`UIRoot`)

`SceneChangeManager` gibi sahneler arası yaşayan tek nesne, kendi Canvas'ı en üstte. Dört işi vardır:

- **Bildirim**: ekranın üstünde 2-3 saniye görünen tek satır. `UIRoot.Toast("Şifre yanlış")`.
- **Yükleme**: karartma + dönen gösterge + isteğe bağlı metin. `UIRoot.ShowLoading("Masaya giriliyor...")` / `HideLoading()`. 15 saniye içinde kapanmazsa kendini kapatır ve "Bağlantı zaman aşımına uğradı" bildirimi gösterir; oyuncu sonsuz yüklemede kalmaz.
- **Onay**: `UIRoot.Confirm(başlık, metin, evetEtiketi, yıkıcıMı)` bir `Task<bool>` döner. `LobbyAccountUI`'daki mevcut `ConfirmSwitchPanel` buna taşınır.
- **Geri tuşu**: açık pencereler bir yığında tutulur. Geri tuşu en üstteki pencereyi kapatır. Pencere yoksa sahneye göre: girişte ve lobide "Oyundan çıkılsın mı?" onayı, masada "Masadan ayrılmak istiyor musun?" onayı. Yükleme açıkken geri tuşu yutulur.

Masa sahnesindeki `UIManager.ShowMessage` ve oyun içi bildirimler bu pakette değiştirilmez.

### Yazı tipi

ChangaOne'da ğ Ğ ş Ş İ harfleri yok; bu harfler bugün yedek fonttan çiziliyor. Osman'ın seçimi: **Changa** (Google Fonts, OFL). Başlık ve butonlarda ExtraBold (800), uzun metinde Bold (700).

- TMP değişken fontu ağırlık ekseniyle kullanamaz; sabit ağırlıklı (static) iki dosya alınır ve her biri için ayrı TMP font varlığı üretilir.
- İndirilen dosyada Türkçe harflerin (ğĞşŞıİçÇöÖüÜ) varlığı, TMP varlığı üretilmeden önce doğrulanır.
- Üç menü sahnesindeki tüm metinler yeni varlığa geçer. Table sahnesi de geçer: yalnızca font değişir, yerleşime dokunulmaz; taşan metin varsa masa belgesine not düşülür.
- ChangaOne dosyaları, hiçbir sahne ve prefab kullanmadığı doğrulandıktan sonra silinir.

### Ölçek ve güvenli alan

Üç sahnenin Canvas'ı aynı ayara çekilir: Scale With Screen Size, referans 1920x1080, Match 0.5. MainMenu'ye `Crystal.SafeArea` eklenir. Table sahnesinin ölçek ayarı bu pakette değiştirilmez (masa yerleşimini kaydırır; masa belgesinin işi).

### Hataların bağlanması

- `PhotonManager.OnJoinRoomFailed` / `OnCreateRoomFailed`: yüklemeyi kapatır, Photon dönüş koduna göre bildirim gösterir (masa dolu, masa kapanmış, aynı isimde masa var, diğerleri için genel "Masaya girilemedi").
- `RoomManager.JoinRoomWithPassword` yanlış şifrede pencereyi kapatmaz; "Şifre yanlış" bildirimi gösterir, alanı temizler.
- `SceneChangeManager.ChangeScene` yüklemeyi açar, yeni sahne hazır olunca kapatır.

## Paket 2 — Giriş ekranı

Düzen: üstte oyun adı, ortada alt alta iki buton — beyaz "Google ile Giriş" ve yeşil "Misafir Olarak Oyna". Altta tek satır: "Devam ederek Kullanım Şartları ve Gizlilik Politikası'nı kabul edersin"; iki ifade dokunulabilir ve adresi tarayıcıda açar. Sağ altta sürüm.

- Daha önce giriş yapmış oyuncu bu ekranı görmez; yükleme göstergesiyle doğrudan lobiye geçer (bugünkü `EntryFlow` davranışı).
- Google butonu yalnızca `GameServices.UsesFirebase` iken görünür (bugünkü kural).
- İsim sorma ve "Bağlanılamadı / Tekrar dene" ortak pencere kalıbına taşınır. İsim penceresinde kapat butonu yoktur; isim zorunlu.
- "Bağlanıyor..." metni yerine ortak yükleme göstergesi.
- Sahnedeki reklam test nesneleri (`OdulReklami`, `GecisReklami`, `AdsButtons`, `BannerReklami`) ekrandan kaldırılır. `ADSManagers` nesnesi kalır; reklamlar geri gelecek.

## Paket 3 — Lobi

### Üst bar

Solda profil çipi (avatar, isim, seviye) — dokununca profil açılır. Sağda coin sayacı ve yanında yeşil `+` (mağazayı açar), en sağda ayarlar butonu. Bar güvenli alanın içindedir.

### Masa kartları

Ekranın ortasında yana kaydırılan masa kartları; ortadaki kart büyük, yanlardakiler kenardan görünür. Bir kart şunları gösterir:

- Masa adı, el sayısı etiketi, şifreliyse kilit simgesi.
- Dört koltuk, masadaki gerçek sırasıyla. Dolu koltukta oyuncu adı, boş koltukta yeşil `OTUR` butonu.
- Yalnızca boş koltuğu olan masalar gösterilir. Oyun başlayınca oda zaten gizleniyor (`SeatManager.StartGame`); ama içinde dört kişi olup biri henüz oturmamış oda bugün listede kalıyor. Kartlar `PlayerCount == MaxPlayers` olan odayı da eler. (Benim kararım.)

Hiç açık masa yoksa ortada tek kart: "Açık masa yok. İlk masayı sen aç." ve `MASA AÇ` butonu.

### Oda listesinin onarımı

Bugünkü liste kodu hatalı ve kartlardan önce düzeltilmesi gerekir (`ButtonFuncs.cs`):

- Photon `OnRoomListUpdate`'e yalnızca değişen odaları yollar; kod bunu tüm liste sanıp ekranı her seferinde sıfırdan kuruyor. Sonuç: değişmeyen odalar ekrandan kayboluyor.
- 1,5 saniyelik bekleme içinde gelen güncellemeler tamamen atılıyor.
- Yenile butonu hiç doldurulmayan boş bir listeyi çiziyor, yani listeyi siliyor.

Düzeltme: oda adına göre tutulan bir önbellek sözlüğü. Her güncelleme sözlüğe işlenir (`RemovedFromList` olan silinir), ekran sözlükten çizilir; bekleme yalnızca çizimi seyreltir, veriyi atmaz. Yenile butonu sözlükten yeniden çizer. Sözlüğün güncellenmesi saf mantıktır ve testi yazılır.

### Lobiden koltuk seçme

Bugün lobiye yalnızca `Password` ve `TotalRounds` gidiyor. Eklenen:

- Master, koltuk durumu her değiştiğinde dört koltuğun oyuncu adlarını tek bir oda özelliğine (`Seats`, dört elemanlı dizi, boş koltuk boş metin) yazar ve bu anahtar `CustomRoomPropertiesForLobby` listesine eklenir.
- Lobide boş koltuğa `OTUR`: oyuncu özelliğine istenen koltuk numarası yazılır (`WantedSeat`), `PickSeat = true` ile odaya girilir. Masa sahnesi açılınca `SeatManager` bu koltuğu master'dan ister (mevcut `RequestSeat`).
- Koltuk o arada dolmuşsa oyuncu masada oturmadan kalır ve bugünkü gibi masadaki `OTUR` etiketlerinden seçer. "Koltuk doldu, başka bir yer seç" bildirimi çıkar. Master bugün reddi sessizce yutuyor (`RequestSeatRPC` yanıt dönmüyor); yeni RPC eklenmez. İstemci, masaya girdiğinde istediği koltuğun başkasında olduğunu oyuncu özelliklerinden (`SeatNumber`) görürse isteği hiç yollamaz ve bildirimi gösterir; yolladıysa ve kısa bir süre içinde koltuk kendisine verilmediyse yine bildirimi gösterir.
- Şifreli masada `OTUR` önce şifre penceresini açar.
- `WantedSeat`, mevcut `SetPickSeat` kalıbıyla her oda kurma ve odaya girme öncesinde sıfırlanır.

`Seats` özelliğini master, koltuğun verildiği ve boşaldığı iki yerde yazar (`AssignSeatToPlayer`, `FreeSeat`). Master değişimi bugün hiç ele alınmıyor; `OnMasterClientSwitched` eklenir ve yeni master `Seats`'i yeniden yazar.

### Oda şifresi

Bugün şifre düz metin olarak lobideki herkese gidiyor ve karşılaştırma istemcide yapılıyor; her oyuncu her odanın şifresini okuyabilir. Yayın öncesi düzeltilir: lobiye şifrenin kendisi değil, oda adıyla tuzlanmış özeti (SHA-256) yazılır; istemci girilen şifrenin özetini karşılaştırır. Şifreli olup olmadığı özetin boş olup olmamasından anlaşılır. (Benim kararım. Sunucu tarafı denetim Photon eklentisi gerektirir, bu oyun için fazla.)

### Alt butonlar

- `HEMEN OYNA` (yeşil, en büyük): bugünkü `JoinRandomRoom`; ilk boş koltuğa oturtur.
- `MASA AÇ` (turuncu): masa kurma penceresi — masa adı, el sayısı, şifre anahtarı ve alanı. Ortak pencere kalıbında.
- `BOTLARLA OYNA` (teal): bugün masa kurma penceresinde çalışma anında kopyalanan anahtarın yaptığı işi tek dokunuşla yapar: varsayılan ayarlarla şifresiz masa kurar ve botları açar. `ButtonFuncs`'taki çalışma anı anahtar kopyalama kodu silinir. Bu butonla kurulan masa lobide gizlidir (`IsVisible = false`): yalnız oynamak isteyenin masasına yabancı düşmez (Osman'ın kararı). Gizlilik yalnızca bu butonun yoluna uygulanır; `FillWithBots` ile oda kuran test yolu (`SyncAutoTest`) görünür kalır.

### Mağaza

Mağazanın içeriği ve düzeni değişmez; yalnızca ortak pencere kalıbına ve butonlarına geçer, ayrı `ShopCanvas` ölçeği diğerleriyle eşitlenir.

## Paket 4 — Ayarlar ve profil

### Ayarlar penceresi

Satırlar: Ses, Müzik, Gizlilik Politikası, Kullanım Şartları, Destek, Hesabımı Sil. Altta sürüm.

- Ses ve Müzik anahtarları kalır ve kaydedilir. Bu, Osman'ın 2026-11-19 tarihli giriş-lobi planındaki kararıdır ("oyunda henüz ses yok, anahtarlar şimdiden var"); ilk taslakta kaldırılmıştı, geri alındı. `GameSettings` değişmez.
- Gizlilik ve şartlar adresi tarayıcıda açar (`Application.OpenURL`). Destek e-posta açar.
- Adresler `GameConfig`'te tutulur. Gerçek adresler Osman'dan gelecek; gelene kadar boş kalır ve boşken satır gizlenir.
- **Hesabımı Sil**: kırmızı yazılı satır. İki adımlı onay: önce ne silineceğini söyleyen onay ("Hesabın, ismin, seviyen ve coinlerin kalıcı olarak silinir. Geri alınamaz."), sonra silme. Başarılıysa giriş ekranına dönülür. Silme kodu zaten var (`IAuthService.DeleteAccountAsync`, `IProfileStore.DeleteAsync`); `GameServices`'te bunları sırayla çağıran tek bir giriş noktası açılır. Firebase "yakın zamanda giriş gerekli" hatası verirse: "Güvenlik için tekrar giriş yapıp yeniden dene" bildirimi.
- Aynı pencere ileride masadaki menüden de açılabilecek şekilde `UIRoot` üzerinden açılır.

### Profil penceresi

Bugünkü alanların hepsi kalır, yeniden dizilir: solda avatar, sağda isim (yanında "Değiştir"), seviye ve XP çubuğu, coin, hesap türü. Altta solda "Google'a Bağla, ilerlemeni koru" (misafirse), bağlıysa yerinde hesap e-postası; sağda kırmızı `ÇIKIŞ YAP`.

- İsim değiştirme mevcut doğrulamayı ve benzersiz isim kuralını kullanır; hata metni alanın altında görünür.
- Çıkış onay sorar. Misafir hesapta onay metni ilerlemenin kaybolacağını söyler.
- Avatar şimdilik ismin baş harfini taşıyan düz renkli daire. Avatar seçimi yok.

## Doğrulama

- Mevcut EditMode testleri geçer.
- Yeni saf mantık için küçük testler: geri tuşu pencere yığını, `Seats` dizisinin kurulması, lobiden istenen koltuğun reddinde geri düşüş.
- Çok editörlü senkron testi (`Run-SyncTest`) paket 3'ten sonra koşulur; bot modu bunun yerine geçmez.
- Her paket sonunda Editor'de ekran görüntüsü, en sonda telefonda (Redmi, Wi-Fi APK) elle kontrol: çentik, dokunma alanları, geri tuşu.

## Yapana bırakılanlar

- Prefab iç hiyerarşisi, tam ölçüler, aralıklar ve yazı boyutları; taslaklardaki oranlar yol göstericidir (`.superpowers/brainstorm/1377-1795058426/content/`).
- `UIRoot` API'sinin tam imzaları.
- Kart kaydırmanın nasıl yapılacağı (ScrollRect + yakalama ya da daha basiti).
- Photon dönüş kodlarının hangi bildirim metnine eşleneceği.

## Açık kalanlar (Osman'dan beklenen)

- Gizlilik politikası, kullanım şartları ve Play'in istediği web üzerinden hesap silme sayfasının adresleri; destek e-postası.

## Paket 1'den devreden notlar (2026-11-19)

Paket 1 (ortak UI kiti) bitti; son incelemede çıkan ve sonraki paketlerin planına girmesi gereken maddeler.

### Kullanılacak API (paket 1'in ürettiği)

- `UIRoot.Toast(metin)`, `UIRoot.ShowLoading(metin)` / `HideLoading()`, `UIRoot.Confirm(başlık, metin, evetEtiketi, yıkıcıMı)`.
- `UiWindow`: `closable` (false = geri tuşu yutulur), `closeOnBack` (varsayılan true), `onBack` (geri tuşu ve X butonu, her kapatma isteğinde çalışır), `Close()`.
- Prefab'lar `Assets/Osman/Prefabs/UI/` altında ve yalnızca `Okey/UI Kit/Prefabları Kur` ile üretilir; Inspector'da elle yapılan değişiklik bir sonraki kurulumda kaybolur.
- Yazı tipi: `Changa-ExtraBold SDF` (başlık, buton), `Changa-Bold SDF` (uzun metin), dış çizgi için `Changa-ExtraBold SDF Outline.mat`. `font` atandıktan sonra materyal yeniden atanmalı.

### Paket 2 — giriş

- Canvas 1920x1080 / Match 0.5 ve `Crystal.SafeArea` bu pakette MainMenu'ye uygulanır (paket 1'den ertelendi).
- Yükleme göstergesi 15 sn sonra kendiliğinden kapanıyor. Google hesap seçici açıkken bu yanlış "zaman aşımı" verir: ya zaman aşımsız bir yükleme türü eklenir ya da yükleme seçici döndükten sonra gösterilir.
- İsim penceresinde (`closable = false`) geri tuşu yutuluyor; oyuncu oradan çıkamıyor. Çıkış onayına düşmesi değerlendirilir. Bu pencerede X butonu gizlenir.
- İlk telefon derlemesinde gerçek Android geri tuşu denenir (Editörde yalnızca kod üzerinden tetiklendi).

### Paket 3 — lobi

- Canvas ölçeği ve SafeArea LobbyMenu ve `ShopCanvas` için bu pakette eşitlenir.
- `ShopCanvas` sahnede kapalı kayıtlı: `MARKET` butonu hiçbir şey açmıyor (paket 1'den önce de böyleydi). Mağaza ortak pencereye geçerken düzelir; açılışta etkin duran bir panele `UiWindow` konmaz.
- Sahne geçişi `ShowLoading()` çağırınca "Masaya giriliyor..." metni siliniyor: yükleme zaten açıksa metin korunur.
- `HEMEN OYNA` oda bulamayınca oyuncunun adıyla oda kuruyor; aynı adda oda varsa oyuncu "Bu isimde bir masa var" görüyor. Otomatik oda benzersiz ya da boş adla kurulur.
- Masa kurma ve şifreyle girme butonları `PhotonNetwork.InLobby` değilken kapalı olur.

### Paket 4 — ayarlar ve profil

- Anahtar prefab'ı yuvarlak köşeli dikdörtgen, tam hap biçimi değil.
- Bildirim metni tek satır, 40 punto, kaydırmasız: "Güvenlik için tekrar giriş yapıp yeniden dene" gibi uzun metinler için otomatik boyut ya da satır kaydırma açılır.
- Ayarlar penceresinin masadan da açılabilmesi için `UIRoot`'ta bir açma noktası yok; bu pakette eklenir.
- Onay penceresi açıkken açılan bir sahne penceresi yığında onun üstüne giriyor; geri tuşu görünmeyen pencereyi kapatıyor.

### Masa belgesi

- Masadaki buton metinlerinde Türkçe harf yok ("Çıkıs", "TAS ISLE", "SERI AÇ"): eski fontta ş ve İ olmadığı için böyle yazılmış, Changa destekliyor.
- Skor tablosundaki 16 metin (`PlayerColumn_1..4`) yer tutucu metinle kutusundan geniş ölçülüyor; gerçek içerikle bakılmalı.
- `UIRoot`, masadaki çıkış butonunu `FindFirstObjectByType<ExitButton>()` ile buluyor; buton gizlenirse geri tuşu masada işlemez.
- Yeni Changa materyalleri `TMP_SDF-Mobile` shader'ını kullanıyor (eski font tam `TMP_SDF` idi). Dış çizgi telefonda gözle kontrol edilmeli.

## Paket 2'den devreden notlar (2026-11-19)

Paket 2 (giriş ekranı) bitti: `207ef38..3afb223`. Sonraki paketlerin bilmesi gerekenler:

### API ekleri

- `UIRoot.ShowLoading(metin, timeout)`: `timeout: false` kendiliğinden kapanmaz. Yalnızca beklenen işin kendi sınırı varsa kullanılır. Bu durumda geri tuşu çıkış onayı açar; zaman aşımlı yüklemede geri tuşu yutulur.
- `ShowLoading(null)` açık yüklemenin metnini korur.
- `UiWindow.closable = false`: geri tuşu pencereyi kapatmaz, çıkış onayı açar. X butonu `closable`'a bakmaz; böyle pencerelerde kurucu X'i gizlemelidir.
- `GameConfig.PrivacyUrl`, `TermsUrl`, `AccountDeletionUrl`, `SupportEmail` (şimdilik boş), `HasLegalLinks`, `LegalUrl(id)`.
- Giriş ekranı yalnızca `Okey/UI Kit/Giriş Ekranını Kur` ile kurulur. Her kurulum sahnedeki tüm kimlikleri yeniden üretir (büyük fark); gerekmedikçe yeniden çalıştırılmaz. Sahne diskte değişince açık klon editörler "modified externally" penceresi açar ve testten önce `Reload` ile kapatılmalıdır.

### Paket 3 — lobi

- Yükleme katmanı tek ve sahipsiz: son `ShowLoading` zaman aşımı kipini belirler, herhangi bir `HideLoading` hepsini kapatır. `SceneChangeManager.ChangeScene` içindeki `ShowLoading()` girişteki zaman aşımsız "Bağlanıyor..."u 15 sn'lik kipe çevirir; LobbyMenu 15 sn'de yüklenmezse yanlış "zaman aşımı" çıkar. `ChangeScene` açık yüklemenin kipini korumalı.
- "Tekrar dene" penceresinde `RetryConnect` 20 sn'ye kadar yalnızca butonu soluklaştırıyor; yükleme göstergesi ve başarısızlık bildirimi yok.

### Paket 4 — ayarlar ve profil

- Şartlar satırının dokunma yüksekliği yaklaşık 50 birim (kural 120); adresler geldiğinde büyütülür.
- Masa sahnesinde `closable = false` pencere açılırsa geri tuşu masadan ayrılma onayını açar.

### Osman'ın kararı bekleyenler

- Google butonu şu an yeşil kit butonu, Misafir turuncu. Google'ın marka kuralı beyaz ya da koyu butonu "G" logosuyla ister; yayın öncesi logo görseli gerekir.
- Misafir oyuncu isim penceresini hiç görmez (`NeedsName`, misafir değilken geçerli): otomatik "Misafir-12345" ile lobiye girer.

### Telefonda bakılacaklar

- Gerçek geri tuşu: giriş ekranında, isim penceresinde, klavye açıkken.
- 20:9'da başlık ile butonlar arası boşluk (Editörde ölçüldü: 42 birim).
- Google hesap seçici açıkken arkadaki yükleme katmanı; iptalde bildirimin çıkması.
