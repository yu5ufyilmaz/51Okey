# Oyuna geri dönüş — tasarım

Tarih: 2026-11-20. Dal: MDC-001-firebase. Sahibi: Osman. Teknik döküm (bağlayıcı ek):
`docs/superpowers/specs/2026-11-20-geri-donus-envanter.md` (aşağıda "envanter").

## Amaç ve neden

Bağlantısı kopan oyuncu, süren kendi oyununa el ortasında geri dönebilsin. O yokken koltuğunda bot
oynasın; koltuğa başka kimse oturamasın. Bugün kopan oyuncu ölü masada kalıyor, 60 saniye sonra
odadan siliniyor, geri dönüş kodu yok.

## Osman'ın kararları (bağlayıcı)

1. Dönüş el ortasında olur; oyuncu kaldığı yerden devam eder.
2. Masadan **bilerek** çıkan dönemez. Çıkışta "emin misin" sorulur. Koltuk maç sonuna kadar botta kalır.
3. Bağlantısı kopan oyuncu oyunu açınca "Bağlı olduğun oyuna dönmek istiyor musun?" sorulur. Evet
   ise oyuna bağlanır.
4. Oyuncu **tam anlamıyla** bağlanana kadar bot onun yerine oynar. Sistem sağlam olmalı.
5. Koltuğa başka oyuncu oturamaz.

## Claude'un kararları (Osman görmedi, itiraz edebilir)

- Soruya "Hayır" diyen bilerek çıkmış sayılır.
- Uygulama açıkken kopmada soru sorulmaz; oyun kendiliğinden yeniden bağlanır.
- Koltuk maç bitene kadar saklanır.
- Masada etkin hiçbir insan kalmazsa (herkes koptu, ya da botlarla tek kişilik masada o kişi koptu)
  maç kurtarılamaz: masa durumu yalnız bağlı telefonların belleğinde yaşar. Dönen oyuncu "Oyun sona
  ermiş" mesajını görür. Bunu aşmak (durumu sunucuda saklamak) bu işin dışında.
- Bot basit kalır (çek, at). Dönen oyuncu, botun bıraktığı eli devralır.
- Oyuncu turunun ortasında koparsa o turda yaptığı yarım işler (açma, işleme, geri alma) geri
  alınır; el, tur başındaki hâline çektiği taş eklenmiş olarak döner ve bot yalnız taş atar.
- Aynı hesapla ikinci bir cihazdan dönüş desteklenmez (kayıt cihazda tutulur).

## Hedef değil

- Akıllı bot. Koltuk anahtarlı masa altyapısı (ad ile alan bulma) — bkz. "Kısıtlar".
- Oyun başlamadan (bekleme odasında) kopan oyuncunun dönüşü: oda açık, yeniden girer.
- Sunucu tarafında kalıcı oyun durumu.

## Başarısızlık sayılacaklar (çalışsa bile)

- Dönüş yüzünden taş kaybolması, çoğalması, sıranın takılması ya da iki tarafın aynı koltuğu oynaması.
- Dönen oyuncunun masası diğerlerinden farklıyken koltuğun ona verilmesi.
- Dönüş denemesinin masadaki diğer oyuncuları bekletmesi ya da onların masasını bozması.

## Yaklaşımlar

1. **Tam fotoğraf + doğrulama (seçilen).** Masa sahibi tur sınırında masanın tam fotoğrafını yollar;
   dönen istemci masayı kurar, sonraki tur sınırında masa özetini (hash) yollar; özet masa
   sahibininkiyle aynıysa koltuk verilir, değilse yeniden denenir. Bot bu sürede oynar. Bedeli: yeni
   bir "paketle/kur" katmanı. Kazancı: her yarış ve bilinmeyen hata, koltuk verilmeden yakalanır.
2. Arabellekteki RPC'leri baştan oynatmak. Elendi: dağıtım, gösterge ve insan atışı arabellekte
   yok; arabellek maçın ilk elinden başlıyor ve hiç temizlenmiyor (envanter §3). Masayı kuramaz.
3. Durumu her hamlede oda özelliğine yazmak. Elendi: her hamleyi ağırlaştırır, bütün hamle kodunu
   değiştirir; tek kazancı "herkes koptu" durumudur ve o hedef dışı.
4. Yalnız sonraki elde dönüş. Osman reddetti; ama seçilen yolun içinde kendiliğinden vardır (yeni
   dağıtım, kurulum bekleyen istemciyi eşitler).

## Tasarım

### 1. Kimlik ve oda ömrü

- Photon'a bağlanırken `AuthValues.UserId = GameServices.Auth.UserId` (Firebase uid; misafir dahil).
  Aynı kişi uygulamayı kapatıp açsa da aynı oyuncu olarak döner (aynı ActorNumber, aynı
  `SeatNumber`/`PlayerQue`).
- `PlayerTtl`: maç boyunca yetecek kadar uzun (hedef 2 saat). `EmptyRoomTtl`: izin verilen en yüksek
  değer. Photon bulutunun bu değerlere sınırı planın ilk işinde **denenerek** doğrulanır; sınır
  düşükse dönüş süresi o sınırdır ve Osman'a bildirilir.
- Oyun başlayınca oda kapalı ve gizli kalır (bugünkü gibi). Yeniden katılım yalnız `RejoinRoom` ile.
- Cihazda kayıt: oyun başlayınca (koltuklar kilitlenince) oda adı `PlayerPrefs`'e yazılır; maç
  bitince, bilerek çıkınca, "Hayır" denince ya da dönüş "oda yok" ile başarısız olunca silinir.

### 2. Koltuğu kim oynuyor: açık denetim bayrağı

Bugün bot, "o sırada etkin oyuncu yok" koşuluyla oynuyor; oyuncu geri bağlandığı an (masayı
kurmadan) denetim ona geçiyor ve çalışan bot coroutine'i durmuyor (envanter §4, risk 4-5).

- Yeni oda özelliği `Ctl` (int, koltuk başına bir bit): bit açıksa koltuğu insan oynar.
- Oyun başlarken insan oturan koltukların biti açılır. Oyuncu ayrılınca/kopunca masa sahibi biti
  kapatır. Bit yalnız aşağıdaki "geri verme" adımında yeniden açılır. Yeni masa sahibi, etkin
  olmayan oyuncuların bitlerini kapatır.
- Bot, sıradaki koltuğun biti kapalıysa oynar. İnsan istemcisi yalnız kendi biti açıkken hamle
  yapabilir (çekme, atma, açma, işleme girişleri kilitli; süre dolunca otomatik oynama da).
- Geri verme yalnız botun hamle ortasında olmadığı anda yapılır; yine de bit açılan koltuk için
  çalışan bir bot coroutine'i varsa iptal edilir.

### 3. Arabellekli RPC'lerin kaldırılması

Envanter §3'teki B1-B18'in hiçbiri bugün arabelleğe ihtiyaç duymuyor; dönen istemcide ise taş
çoğaltıyor, eski ellerin perlerini çiziyor, herkesin `PlayerQue` değerini eskiye çekiyor. Hepsi
arabelleksiz olur (`AllBuffered` → `All`, `OthersBuffered` → `Others`). Bekleme odasında geç gelen
oyuncu koltukları `SeatNumber` özelliğinden zaten okuyor. `SetupPlayer` süs nesnesi dönen oyuncu
için yeniden yaratılmaz.

### 4. Tur ortasında kopma: turu geri alma

Her istemci, her `NextTurn`'de sıradaki koltuk için "tur başı kaydı" tutar: o koltuğun eli, yerdeki
per verisi, açmış/çift açmış üyeliği, atık yığını sayısı. (Bütün istemciler bütün elleri zaten
tutuyor.)

Sıradaki oyuncu koparsa masa sahibi `RollbackTurn(que)` yollar; her istemci:
- o koltuğun atık sayısı tur başındakinden büyükse (atış ulaşmış, `NextTurn` gelmemiş): geri alma
  yok; masa sahibi turu `NextTurn` ile kapatır;
- değilse: el = tur başı eli + bu turda çekilen taşlar; per verisi ve açmış üyeliği tur başına
  döner; o turun yereldeki bekleyen cezaları, yandan alma yükümlülüğü ve işleme kayıtları zaten
  kopan istemciyle gitmiştir. Sonra bot (bit kapalı) yalnız atar.

Bilinen sınır: atış ile `NextTurn` arasındaki ~0,4 saniyede kopulursa ve o turda taş işlenmişse,
işlenen taşın görüntüsü gönderilmemiş olabilir; taş oyundan düşer (bütün istemcilerde aynı şekilde).
Kabul edildi; nota yazılır.

### 5. Masa fotoğrafı

- İçerik: envanter §1'de **S** işaretli her kalem; **D** kalemleri envanterin dediği gibi
  fotoğraftan türetilir; **P** kalemleri Photon'dan kendiliğinden gelir. Yerdeki perler
  `meltedTiles` verisinden değil **sahnedeki tahtadan** (yuva sırası, okeyin durduğu yuva) alınır
  (envanter risk 9, "Joker values"). Atık yığınları tam geçmişle ve zaman sırasıyla; `dropTile`
  aynen.
- Biçim: sürüm numarası ile başlayan tek `int[]` (gerekirse yanında `string[]`); bölüm bölüm,
  uzunluk önekli. `Encode`/`Decode` saf kod, EditMode testli (gidiş-dönüş eşitliği, bozuk veri
  reddi).
- Kurma: taze dağıtım yolunun animasyonsuz, coroutine'siz karşılığı. Envanter §2'deki adımlardan
  hangilerinin yeniden kullanılacağı plana bırakıldı; şart: kurma bitene kadar istemci "canlı"
  sayılmaz. El listeleri nesne olarak değiştirilmez, yeniden doldurulur (envanter "Hand list
  identity").
- Masa özeti: `DesyncDetector`'ın bugünkü özeti fotoğraftaki her kalemi kapsayacak şekilde
  genişletilir (envanter §1.7'de dışarıda kalanlar). Fotoğrafta olup özette olmayan kalem kalmaz;
  bunu bir test korur (fotoğraftaki bir alanı değiştirince özet değişmeli).

### 6. Dönüş akışı

**Bağlantı.** `OnDisconnected` için kalıcı (DontDestroyOnLoad) bir sahip: oyundayken koptuysa
"Yeniden bağlanılıyor…" katmanı gösterir, `ReconnectAndRejoin` dener; 30 saniye içinde olmazsa ana
menüye döner. Bugün `ExitButton.OnLeftRoom` kopmada da lobiye atıyor; bu yalnız bilerek çıkışta
çalışacak. Uygulama yeniden açıldıysa: Photon'a bağlanınca kayıtlı oda varsa soru sorulur
(`UIRoot.Confirm`); "Evet" → `RejoinRoom(oda)`. Hata (oda yok, süre dolmuş, maç bitmiş) → kayıt
silinir, "Oyun sona ermiş" bildirimi. "Hayır" → kayıt silinir.

**Masaya giriş, kurulum modunda.** Dönüşte (`LocalPlayer.HasRejoined`) Table sahnesi "kurulum
modu"nda açılır: taze başlangıcın yan etkileri çalışmaz (envanter §7: `PlayerScore = 0`, fazladan
oyuncu nesnesi, `TurnManager.StartGame`, `StartRoundVisuals`, çifte sahne yüklemesi, masa sahibi
dalları). İstemci "canlı değil" durumundadır: oyun RPC işleyicileri (envanter §3 listesi artı
dağıtım dışındaki arabelleksiz oyun RPC'leri) hiçbir şey yapmadan döner. Ekranda masa üstünde
"Oyuna bağlanılıyor…" katmanı durur.

Dönen istemci masa sahibi olarak dönmüşse (etkin başka insan yok): maç kurtarılamaz; odadan
tamamen çıkar, kayıt silinir, "Oyun sona ermiş".

**El sıkışma** (hepsi tur sınırlarında; anahtar = el numarası + tur sayacı):
1. İstemci sahnesi hazır olunca masa sahibine `RestoreRequest` yollar.
2. Masa sahibi bir sonraki `NextTurn`'ü işlediği anda (el bitmişse yeni dağıtımı bekler) fotoğrafı
   o istemciye yollar. O an masa sakindir (envanter §4, Q1).
3. İstemci fotoğrafı kurar, "canlı" olur; bundan sonra gelen oyun RPC'lerini normal işler.
   Fotoğraftan önce gelip yok sayılmış bir hamle varsa masası farklı kalır; bunu 4. adım yakalar.
4. İstemci her `NextTurn`'de masa özetini anahtarla birlikte masa sahibine yollar. Masa sahibi aynı
   anahtar için kendi özetiyle karşılaştırır (son birkaç turun özetini saklar).
   - Eşit **ve** sıra o koltukta değil: `Ctl` biti açılır (geri verme). İstemci katmanı kaldırır.
   - Eşit ama sıra o koltukta: bot turu bitirir, bir sonraki eşit özet beklenir.
   - Farklı: masa sahibi `RestoreAgain` yollar; istemci "canlı değil"e döner, 2. adımdan yeniden.
5. Kurulum sırasında yeni el dağıtılırsa (`SyncShuffledTiles`), istemci taze dağıtımla zaten eşit
   olur: "canlı" sayılır, 4. adımla devam eder.
6. Üst üste 5 başarısız denemeden sonra istemci katmanda "Bağlantı kurulamadı, yeniden deneniyor"
   gösterir ve denemeyi sürdürür; bot oynamaya devam eder. Her deneme `[DONUS]` satırı loglar.

Diğer oyuncular için hiçbir bekleme yoktur: bütün adımlar arka planda, bot oynarken yürür.

**Ekranda.** Kopan oyuncunun isimliği diğerlerinde "ad (bot)" olur (bugünkü `SeatNaming`); geri
verme anında ad eski hâline döner. Yeni metinler `Localization.csv`'den (tr ve en).

### 7. Bilerek çıkış

`ExitButton` bugünkü onayı korur; metin, dönüşün olmadığını söyler ("Masadan ayrılırsan bu oyuna
geri dönemezsin. Yerine bot oynar."). Onayda: kayıt silinir, oyuncu odadan **etkin olmayan** olarak
ayrılır (bugünkü gibi; Photon'daki oyuncu nesnesi kalır, ada bağlı alan aramaları çalışmayı
sürdürür), `Ctl` biti kapanır. Kayıt silindiği için geri dönüş sorusu çıkmaz.

### 8. Masa sahibi kopunca

- Yeni masa sahibi el içi ceza tablosunu (`playerScores`) her istemcide bulunan
  `MatchScores.current` kayıtlarından yeniden kurar (envanter §6).
- Yeni el hattı (0,2-0,7 saniyelik pencere) içinde masa sahibi koparsa oyun takılıyor: yeni masa
  sahibi, el bitmiş ve yeni el 15 saniye içinde hazır olmamışsa dağıtımı kendisi başlatır.
- Yeni masa sahibi oyun ortasında koltuk değişimi isteğini kabul etmez (`gameIsStart` yerine
  `SeatsLocked` oda özelliğine bakılır).
- `FinishGate` alanları yalnız masa sahibinde: yeni masa sahibi için `Hand{n}` zaten yazılmışsa o
  el için ikinci sonucu aynı kuralla süzer (kazanç beraberliği ezer).
- Eski masa sahibi döndüğünde sıradan bir oyuncudur; yukarıdaki akışla döner.

## Kısıtlar

- Alan aramaları ad ile yapılıyor (`GameObject.Find(NickName + " meld")`). Bu iş onu değiştirmez;
  bu yüzden ayrılan oyuncu maç boyunca Photon'da "etkin olmayan" olarak tutulur (ad ve koltuk
  korunur). `PlayerTtl` dolarsa o koltuğun perleri çizilmez (bugünkü hata); süre maçtan uzun
  seçilerek kaçınılır.
- Yeni her `[PunRPC]` RpcList'e kaydedilir. Oyun kodunda çağrı sırası değişmez; yalnız hedefler
  (arabelleksiz) ve denetim koşulu (bit) değişir.
- Görünür metin yalnız `Loc.T` / `LocText`. Ses çağrıları ses-yalnız satırlar.
- Sınırlar: Google girişi tetiklenmez, çıkış yapılmaz, profil adı değişmez, satın alma ve hesap
  silme çağrılmaz. Push yok.

## Doğrulama

- EditMode: fotoğraf `Encode`/`Decode` gidiş-dönüş; bozuk/eksik veri reddi; özetin her fotoğraf
  alanına duyarlılığı; tur geri alma kuralı (saf kısım); `Ctl` bit işlemleri; el sıkışma kararı
  (eşit / sıra bizde / farklı) saf fonksiyon olarak.
- Çok editörlü test, yeni anahtar: `Run-SyncTest.ps1 -DropRejoin` — bir klon belirli bir hamlede
  bağlantıyı keser (`PhotonNetwork.Disconnect`), N saniye sonra döner. Senaryolar: sırası değilken
  kopma; kendi turunda çekmeden önce; çektikten sonra; açtıktan sonra (geri alma); masa sahibinin
  kopması; el sonu ekranında kopma; iki oyuncunun aynı anda kopması; kopup dönmeden maçın bitmesi.
  Geçme ölçütü: `PASS`, `desync 0`, dönen editörde geri verme logu, dört editörde aynı `[SKOR]`.
- Mevcut testler bozulmaz: `-Deals 2 -Mode smart`, `-Deals 3 -Mode smart`, `-Solo`.
- Uygulama kapatıp açarak dönüş (soru penceresi) editörde tam denenemez; telefonda Osman dener.
  Editörde yalnız soru penceresi ve `RejoinRoom` yolu ayrı ayrı sınanır.

## Kurucuya bırakılanlar

- Fotoğrafın bölüm sırası ve alan kodlaması; kurmada hangi mevcut yöntemlerin yeniden kullanılacağı.
- "Canlı değil" kapısının nerede duracağı (işleyici başına koşul ya da ortak bir giriş noktası).
- `-DropRejoin` anahtarının ayrıntısı ve senaryo tohumları.
- Katmanların görünümü (mevcut `UIRoot` yükleme katmanı tarzında).

## İnceleme sonrası düzeltmeler (bağlayıcı; yukarıdaki metinle çelişirse bu bölüm geçerlidir)

Bağımsız bir incelemeci tasarımı koda karşı okudu; aşağıdakiler onun bulgularının çözümüdür.

**Özet ve anahtar**
- `DesyncDetector` yalnız geliştirme yapılarında çalışıyor ve sayacı dönüşte sıfırlanıyor. Masa
  özeti ayrı, yayın yapısında da çalışan bir bileşen olur. Anahtar = `CurrentRound` + **tur seri
  numarası**. Seri numarası `NextTurn`'ün parametresi olur ve fotoğrafın içinde taşınır.
- Masa sahibinin **yetki RPC'leri** (ceza duyurusu, bot çekme/atma, botun `NextTurn`'ü, el sonu ve
  yeni el RPC'leri) `AllViaServer` ile gider. Böylece masa sahibi kendi mesajını herkesle aynı
  sırada işler ve `NextTurn` K anındaki özeti herkesle aynı mantıksal noktadadır. Masa sahibinin
  insan olarak kendi hamleleri değişmez.

**El sıkışma**
- Dönen istemci, canlı olmadan gelen oyun RPC'lerini **atmaz, sıraya koyar**. Fotoğraf (seri K)
  gelince: sıradakilerden seri K'li `NextTurn` dahil öncesi atılır, sonrası sırayla oynatılır.
- Geri verme koşulu, masa sahibinin **karar anındaki** durumuna bakar: sıra o koltukta değil ve o
  koltuk için çalışan `PlayBotTurn` yok.
- Geri vermeden önce dönen istemci kendi ıstakasını veriden yeniden kurar; `RackMismatch() == null`
  ve kendi açmış bayraklarının tahtayla tutarlılığı şarttır. Geri vermede `canDrop = (el == 15)`.
- `Ctl` gecikmesi: masa sahibinin bot kapısı kendi **niyet ettiği** değeri hemen kullanır; insan
  istemcinin giriş kapısı sunucudan **dönen** değeri kullanır. Kapı `TurnManager.IsPlayerTurn()`
  içinde durur.
- Kurulum sırasında yeni el gelirse istemci `SyncShuffledTiles`'ta değil **`RPC_PrepareNextRound`**'da
  canlı olur (yeni el hattının tamamını işlemesi gerekir).
- Canlı olmayan istemci masa sahibi olursa görevi `SetMasterClient` ile canlı bir insana devreder;
  canlı insan yoksa maç kurtarılamaz. Masa sahibi değişince `RestoreRequest` yeniden yollanır.
- İstek, fotoğraf ve özet mesajları sahne nesnesi (GameManager) üzerinden ya da `RaiseEvent` ile
  gider; oda nesnelerinin (TileManager/ScoreManager) varlığına bağlı olmaz. El sıkışma
  coroutine'leri `TurnManager` üstünde durmaz (`ForceSkipBotTurn` hepsini durduruyor).

**Tur geri alma**
- Tur başı kaydına `IsDoubleOpened` eklenir. Dönen istemcide tur başı kaydı fotoğraftan başlar.
- Atış ulaşmış ama `NextTurn` gelmemişken kopulursa: her istemci o koltuğun tahtasını `meltedTiles`
  verisinden çizer (açılan perler kaybolmaz), sonra masa sahibi turu kapatır. Geriye kalan bilinen
  sınır yalnız o ~0,4 saniyede işlenmiş taştır.
- Yandan taş alıp kopan oyuncu: mevcut "yandan almayı iptal" yolu çalışır; taş atık yığınına döner,
  bot ortadan çeker, ceza yazılmaz. (Claude'un kararı: taşı alıp koparak yükümlülükten kaçılamasın.)
- Kopan oyuncu masa sahibiyse, onun koltuğu için ayrılma işlemini (geri alma dahil) yeni masa
  sahibi yapar.

**Bağlantı**
- Beklenmeyen her odadan düşmede istemci önce nötr bir sahneye geçer, oradan yeniden katılır
  (aynı sahnede kalınca PUN Table'ı yeniden yüklemiyor, oda nesneleri ölü sahneye düşüyor).
  `ExitButton` bilerek çıkışı bir niyet bayrağıyla işaretler; `OnLeftRoom` buna bakar.
- Yeniden katılma hatası 32746 (eski bağlantı sunucuda hâlâ etkin) geçicidir: yeniden denenir.
  Kayıt yalnız 32758 (oda yok) ve 32748 (oyuncu bulunamadı) hatalarında silinir. Uygulama açıkken
  30 saniyelik deneme başarısız olursa kayıt **kalır**; ana menüde soru çıkar.
- Maç, oyuncu yokken bittiyse yalnız "Oyun sona ermiş" gösterilir (Claude'un kararı).

**Diğer**
- İsimlikteki "(bot)" eki `IsInactive`'e değil `Ctl` bitine bakar.
- Çok editörlü testte beklenen istemci sayısı, canlı olmayan istemciyi saymaz.
- Planın ilk işi canlı Photon denemesidir: kapalı odaya `RejoinRoom`, `PlayerTtl`/`EmptyRoomTtl`
  sınırları, etkin oyuncu kalmayınca odanın ömrü. Sonuç tasarımı değiştirirse Osman'a dönülür.
