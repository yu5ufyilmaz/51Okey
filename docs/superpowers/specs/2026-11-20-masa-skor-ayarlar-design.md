# Masa: skor tablosu, açma puanı, ayarlar

Tarih: 2026-11-20. Durum: onay bekliyor.

## Amaç ve neden

Osman telefonda test oyunu oynadı ve üç şey istedi:

1. Skor tablosu okunmuyor ve tek ele göre; oyunlar 2 ile 7 el arası olacak. Oyuncu ne kadar ceza
   yediğini, elinde ne kaldığını, bunların puanını anlamıyor.
2. Açan oyuncuların kaçla açtığı isimlerinin yanında yazmalı.
3. Masada ayarlar yok; eklenmeli.

İncelemede çıkan: okunmamanın nedeni yalnızca görünüş değil. Oyun el sonunda oyuncu başına üç
sayı yolluyor (ödül, ceza, net); "elde kalan" ile "el içinde yenen ceza" tek sayıda birleşik.
Eller arası toplam hiçbir yerde tutulmuyor, her el bir öncekini siliyor. Botlar tabloda yok.
Açma puanı diğer oyunculara hiç gitmiyor. Ayarlar penceresi yalnızca lobi sahnesinde.

## Hedefler

- El sonunda her oyuncu için ayrı ayrı okunur: elde kalan (taş sayısı ve puanı ya da "açmadı"),
  el içinde yenen cezalar, bitirme ödülü, bu elin toplamı.
- Kendi cezalarımın dökümü görünür (hangi sebepten kaç puan).
- Eller yan yana sütunlarda, sonda toplam; 1 ile 7 el arasında aynı düzen.
- Toplamı en düşük oyuncu önde gösterilir; son elden sonra maçı kazanan yazılır.
- Botlar tabloda oyuncu gibi yer alır.
- Aynı tablo el içinde TABLO düğmesiyle de açılır (o ana kadarki eller ve süren elin cezaları).
- Açan oyuncunun ilk açış puanı isimliğinde yazar.
- Masada Ses ve Müzik ayarı açılır.
- Lobide el sayısı 7'ye kadar seçilir.

## Hedef dışı

- Masanın geri kalanının yeniden tasarımı (düğmeler, ıstaka, isimlikler).
- Maç sonu ödülü (jeton, deneyim puanı). Bugün de verilmiyor.
- Düşen oyuncunun oyununa geri dönmesi. Osman istedi; ağ tarafı büyük ve ayrı bir tasarımdır
  (elin, perlerin, sıranın geri yüklenmesi; botun yeri geri vermesi). Bu belge yalnızca skor
  geçmişini ona hazır kurar.
- Masada dil değiştirme, hesap silme, yasal bağlantılar.
- Skor paylaşma, ekran görüntüsü.
- Puanlama kurallarının değişmesi. Sayılar bugünkü hesaptan gelir; yalnızca ayrı ayrı gösterilir.

## Yapılmaması gerekenler

- Puan hesabı değişmemeli: bir elin bugünkü "net" değeri ile yeni tablodaki "bu el" değeri her
  oyuncu için aynı olmalı.
- Çok editörlü senkron testi ve test oyuncuları bozulmamalı. `GameManager.HandResult` olayının
  imzası ve içeriği aynı kalır; test oyuncuları ona bağlı.
- Farklı ekranlarda farklı sayı görünmemeli: tablo her istemcide aynı veriden kurulur.
- Tabloda başlıksız sayı, "New Text", boş kutu kalmamalı.

## Kısıtlar

- `Table.unity` elle yapılmış bir sahnedir; ona dokunan tek builder `Assets/Osman/Editor/UiKitTable.cs`.
- Masa tuvali: 1920x1080 referans, eşleşme 0.443; güvenli alan kabı (`SafeArea`) alışılmadık
  ölçülerle kurulu, köşeye çapalanan nesnelerin kenar boşluğu en-boy oranına göre kayıyor.
- `UIRoot` (kalıcı, `Assets/Resources/UIRoot.prefab`, builder: `UiKitBuilder.BuildUIRoot`) bugün
  onay penceresi, yükleniyor ve bildirim taşıyor; pencereler `UiWindow` ile geri tuşuna kaydolur.
- Sıra numarası (que) her el yeniden dağıtılır; koltuk numarası maç boyunca sabittir. Geçmiş
  koltuğa göre tutulmalıdır.
- El sonu hesabı yalnızca masa sahibinde (master) yapılır: `GameManager.CalculateAndDistributeScores`.
  Elde kalan, okey sayısı, çarpanlar bugün yalnızca orada ve bir log satırında var.
- El içi cezalar master'da `ScoreManager.playerScores` içinde birikir; her ceza `ShowPenaltyRPC`
  ile herkese duyurulur (oyuncu sırası, miktar, sebep).
- Yeni bir `[PunRPC]` eklenince `PhotonServerSettings.RpcList` kaydedilip commit edilmelidir; yoksa
  klonlar ve telefon derlemesi RPC'yi bulamaz.
- Metinler `Assets/Resources/Localization.csv` tablosundan gelir (tr ve en); sesler `Sfx` üzerinden.

## Önkoşul: ikinci elde masa sahibinin takılması

Çok elli maçta bilinen ve düzeltilmemiş bir hata var (2026-11-11'de senkron testi buldu): el
sıfırlama mesajı dört kez gidiyor; masa sahibi o elde 1. sıradaysa ikinci elde taş çekemez ve
atamaz, oyun donar. 2-7 ellik maçlar ve skor tablosunun "eller" bölümü bu düzelmeden çalışmaz.
Planın ilk görevi budur: önce yeniden üretilir, sonra en küçük düzeltme yapılır.

## Yaklaşımlar

Veri için:

1. Master el dökümünü bir mesajla yollar, her istemci geçmişi kendinde tutar. En basiti; ama
   oyuna geri dönen oyuncu geçmişi göremez.
2. **Geçmiş oda özelliklerinde tutulur (seçilen).** Master her elin dökümünü oda özelliğine yazar
   (`Hand1` .. `Hand7`, el başına küçük bir sayı dizisi); her istemci oradan okur. Oyuna geri
   dönen oyuncuya kendiliğinden gelir, master değişince kaybolmaz. Var olan
   `OnGameEndedDetailedRPC` ve `HandResult` aynı kalır. Boyut küçük: 7 el x 4 koltuk x 8 sayı.
   Osman düşen oyuncunun oyununa geri dönebilmesini istedi (2026-11-20); o iş ayrı bir tasarımdır,
   ama skor geçmişi şimdiden ona hazır kurulur.
3. Her istemcinin dökümü kendisinin hesaplaması. Rakiplerin elindeki taşları her istemci
   bilmiyor; elendi.

Arayüz için:

1. **`UiKitTable` paneli kit parçalarından kurar, satır ve sütunlar çalışırken doldurulur (seçilen).**
   Eski panel nesneleri sahneden kaldırılır. Lobi ve ayarlarla aynı görünüm, aynı font.
2. Eski paneli elde düzeltmek. 16 başlıksız kutu ve yanlış bağlanmış alanlar üzerine kurulur;
   7 ele uymaz. Elendi.

Osman taslağı onayladı ("Evet, böyle olsun"); veri ve arayüz yaklaşımını Claude seçti.

## Tasarım

### El dökümü verisi

Master, el bitince `CalculateAndDistributeScores` içinde her koltuk için bir satır kurar ve
`Hand{n}` oda özelliğine yazar (var olan `OnGameEndedDetailedRPC`'den hemen önce). Her istemci
`OnRoomPropertiesUpdate` ile alır. Koltuk adları ayrı bir oda özelliğinde durur (`SeatNames`,
el başında master yazar).

Satır alanları (dizi olarak, koltuk sırasıyla 1-4; boş koltuk yok sayılır):

| Alan | Anlamı |
|---|---|
| `seat` | Koltuk numarası |
| `name` | Koltuğun adı (aşağıda "Bot adları") |
| `opened` | Açtı mı |
| `tilesLeft` | Elde kalan taş sayısı (okeyler dahil) |
| `handPoints` | Elde kalanın el sonu cezası (bugünkü `EndPenalty` sonucu, bitirme çarpanı dahil); bitirende 0 |
| `inHandPenalty` | El içinde yediği cezaların toplamı (`playerScores[que]`) |
| `finishReward` | Bitirenin ödülü (eksi sayı); diğerlerinde 0 |
| `handTotal` | `handPoints + inHandPenalty + finishReward` |

Mesajla birlikte tek sefer: `handIndex` (1'den), `winnerSeat` (berabere ise 0), `finishMultiplier`,
`okeyFinish`, `pairsFinish`, `kafa`.

Değişmezler:

- İnsan oyuncu için `handTotal`, aynı elde `OnGameEndedDetailedRPC`'nin `netScores` değerine eşittir.
  Bu bir testle sabitlenir (satırı kuran saf yöntem üzerinden).
- Ayrıştırma koddan doğrulandı: `net = playerScores[que] + EndPenalty`. Bitirenin ödülü
  `-100 x renk çarpanı`dır ve bitirme çarpanıyla çarpılmaz; bitirme çarpanı yalnızca kaybedenlere
  uygulanır. Berabere elde herkes kaybeden formülüyle hesaplanır.
- `FinishGameRPC` aynı el için iki kez gelebilir ve hesap iki kez koşabilir. El, el numaralı oda
  özelliğine yazıldığı için ikinci yazış aynı anahtarın üstüne gelir; toplam iki katına çıkmaz.
- Botun satırı insanla aynı formülle hesaplanır. Bot bugün hiç açmaz ve atış cezası yemez; satırı
  çoğunlukla "açmadı" olur. Yerine bot geçen oyuncunun o ele kadar açtıkları ve cezaları geçerlidir.
- Master el ortasında değişirse yeni master'da el içi ceza toplamları sıfırdır; o elin dökümü
  eksik çıkar. Bugünkü net puan da aynı durumda yanlıştır; bu işte düzeltilmez, belgeye yazılır. Botlar bugün `PhotonNetwork.PlayerList` döngüsünde
  olmadığı için hesap dışı; yeni satırlar koltuk döngüsünden kurulur. Var olan `netScores` ve
  `PlayerScore` davranışı değişmez.

Satırı kuran hesap saf bir yönteme çıkarılır (`HandBreakdown.Build(...)`): girdi olarak koltuk
başına (açtı mı, çiftle mi açtı, elde kalan taşlar, el içi ceza), kazanan, bayraklar, renk çarpanı;
çıktı satır listesi. `PenaltySystem.EndPenalty` ve `FinishMultiplier` olduğu gibi çağrılır.

### Bot adları

- Hiç insan oturmamış koltuğun adı "Bot 1", "Bot 2", "Bot 3" olur; numara koltuk sırasına göre
  botlar arasında 1'den verilir (koltuk numarası değil).
- İnsan oyuncu oyundan düşer ve yerine bot oynarsa koltuk oyuncunun adını korur, yanına "(bot)"
  eklenir: "Can (bot)". Oyuncu geri dönebileceği için adı silinmez. (Claude'un kararı.)
- Bu adlar masadaki isimliklerde de yazar (bugün bot koltuğunun isimliği boş) ve skor tablosunda
  aynı adla görünür.

### Geçmiş ve toplam

`MatchScores` (masa sahnesinde bir bileşen): `Hand{n}` oda özellikleri gelince eli kaydeder; masaya
sonradan giren ya da geri dönen istemci açılışta var olan özellikleri okur.
`Total(seat)` = o koltuğun `handTotal` toplamı. `Leader` = toplamı en düşük koltuk (eşitlikte
koltuk numarası küçük olan). `TotalRounds` oda özelliğinden okunur. Yeni el başlayınca silinmez;
masadan çıkınca sahneyle birlikte gider.

### Ceza dökümü

`UIManager.ShowPenaltyFloat` çağrısının geldiği yerde (`ShowPenaltyRPC`) her istemci cezayı
`MatchScores`'a da yazar: (oyuncu sırası → koltuk, miktar, sebep). Süren elin listesi el bitince
o elin kaydına taşınır. Tabloda yalnızca **yerel oyuncunun** kendi cezaları sebep sebep listelenir
("Okey attı +250"); diğer oyuncular için toplam yeter. Sebep metinleri var olan `penalty.*`
anahtarlarından gelir.

Not: `ShowPenaltyRPC` oyuncu sırası taşır. Koltuğa çeviri `TileDistrubite.seatOfQue` ile yapılır
(her istemcide var, bot koltuklarını da içerir; yardımcı: `SeatMath`). Kayıt, `ShowPenaltyFloat`'ın
"yerel sıra numarası yok" erken dönüşünden **önce** yapılır.

### Skor tablosu ekranı

Tek panel, iki durumda açılır: el bitince kendiliğinden; el içinde TABLO düğmesiyle (aç/kapa).

```
                    3. EL BİTTİ  ·  Ayşe bitirdi (okey ile, x2)

 OYUNCU      ELDE KALAN     CEZALAR      BİTİRME     BU EL
 Ayşe             -            -          -600        -600
 Sen          7 taş  240     +250           -         +490
 Can          açmadı 1200      -            -        +1200
 Bot 3        3 taş  120      +70           -         +190

 Cezaların:   Okey attı +250

 ──────────────────────────────────────────────────────────────
 OYUNCU      1. EL   2. EL   3. EL   4. EL   5. EL    TOPLAM
 Ayşe         +340    -400    -600     .       .        -660   önde
 Sen          -300    +120    +490     .       .        +310
 Can          +510    +600   +1200     .       .       +2310
 Bot 3        +180    +200    +190     .       .        +570

                     Sonraki el: 8 sn
```

- Üst bölüm (bu elin dökümü) yalnızca el bitince dolu gelir. El içinde açılınca üst bölümde
  süren elin o ana kadarki cezaları gösterilir ("CEZALAR" sütunu), diğer sütunlar boş.
- Alt bölüm: oynanan eller dolu, kalan eller nokta. Sütun sayısı `TotalRounds` kadar (1-7).
  7 elde sütunlar daralır; yazı otomatik küçülür, en küçük boyut okunur kalmalıdır.
- Sıra: satırlar koltuk sırasıyla, yerel oyuncu "Sen" olarak ve vurgulu. Toplamı en düşük olan
  "önde" işareti alır. Renk: eksi (iyi) yeşil, artı (kötü) kırmızı.
- Başlık satırı: "{n}. EL BİTTİ · {ad} bitirdi" ve varsa ekler: "okey ile", "çiftten", "kafa",
  çarpan "x{k}". Berabere: "{n}. EL BİTTİ · Taşlar bitti".
- Son elden sonra başlık "MAÇ BİTTİ · Kazanan: {ad}" olur (toplamı en düşük).
- Alt satır: "Sonraki el: {s} sn" geri sayımı; son elde "Lobiye dönüş: {s} sn". Bugün geri sayım
  yok (10 saniyelik bekleme her istemcide ayrı bir `WaitForSeconds`). Panel kendi sayacını döküm
  geldiği anda 10'dan başlatır, 0'da durur; paneli `RPC_PrepareNextRound` kapatır. Bekleme süresi
  ve sıradaki ele geçiş mantığı değişmez.
- Panel, elin oda özelliği gelince açılır (`OnGameEndedDetailedRPC`'yi beklemez).
- El içinde açılan tabloda X düğmesi ve geri tuşu kapatır; el sonunda kendiliğinden açılanı
  oyuncu kapatamaz (bugünkü davranış), sonraki el başlayınca kapanır.
- Kaldırılanlar: eski `ScorePanel` (16 kutu, `Timer`, `Ranking`, `RestartButton`) ve eski
  `CurrentScorePanel`. TABLO düğmesi kalır, yeni paneli açar (sahnedeki kalıcı çağrısı
  `UIManager.ToggleLiveScoreboard`'a bağlı; yeniden bağlanır).
- Eski panele bağlı kod birlikte temizlenir: `GameManager.RPC_PrepareNextRound` içindeki
  `UIManager.Instance.gameOverPanel.SetActive(false)` (null denetimi yok; panel kalkınca hata verip
  sıradaki eli kilitler), `UIManager.ShowDetailedGameOver`, canlı ceza listesi ve `UiKitTable`'ın
  "TEKRAR OYNA" etiketi beklentisi.
- Bütün metinler tablodan (`table.*` anahtarları, tr + en); açılış `UiClick`, el sonu `Win`/`Lose`
  sesleri bugünkü gibi.

### Açma puanı isimlikte

- Oyuncu ilk kez açtığı turda taşını atınca (`CommitFinalTableLimit`, `FirstOpenThisTurn`), açış
  puanı ve türü (seri/çift) yeni `OpenValueRPC(que, value, isPairs)` ile herkese gider. Rakibin
  perleri masaya zaten bu anda geliyor.
- Açış puanı `tempOpenedScore`'dan **alınmaz**. O alan düğmeye basılır basılmaz, hamle
  doğrulanmadan yazılıyor; açtıktan sonra öbür düğmeye (reddedilen) bir kez basmak değeri bozuyor.
  Puan, açmanın gerçekten yerleştiği yerde (`PlaceValidPers` / `PlacePairPers` başarı noktası)
  ayrı bir alana yazılır ve mesaj onu taşır.
- **Bulunan oyun hatası (bu işte düzeltilir):** aynı bozuk değer bugün barajı da belirliyor
  (`TryUpdateTableLimit(tempOpenedScore)`). Baraj da yeni alandan beslenir; yani baraj her zaman
  gerçekten açılan puana yükselir. Kural değişmez, yanlış değer düzelir.
- **Bulunan oyun hatası (bu işte düzeltilmez, belgeye yazılır):** oyuncu aynı turda açtığını geri
  alıp yeniden açarsa "ilk açış" sayılmıyor; baraj güncellenmiyor ve açış puanı gitmiyor.
- Her istemci değeri oyuncuya göre saklar; el başında (`ResetPlayerOpenStatus`) silinir.
- İsimliğin sağındaki boş alana küçük bir etiket: "Açtı 68" ya da "Çift 68". Yerel oyuncunun kendi
  isimliğinde de yazar. Sonraki turlarda eklenen perler değeri değiştirmez.
- Geri alma: oyuncu aynı turda açtıklarını geri alırsa mesaj hiç gitmemiş olur (mesaj atışta gider).
- Botlar bugün hiç açmıyor; isimliklerinde etiket çıkmaz.

### Masada ayarlar

- Ayarlar penceresi `UIRoot`'a taşınmaz; masa için ayrı, küçük bir pencere kurulur: başlık "Ayarlar",
  tek satırda Ses ve Müzik anahtarları (lobideki `HalfRow` + `ToggleRow` ile aynı parçalar), X.
- Yeri: `UiKitTable` Çıkış düğmesinin sağına dişli/"Ayarlar" düğmesi koyar (kit `Btn_Teal`, Çıkış
  ile aynı yükseklik). Güvenli alan tuhaflığı yüzünden düğme Çıkış'la aynı kaba ve aynı çapaya
  konur, köşeye değil.
- Çalışma zamanı: küçük bir `TableSettings` bileşeni; `GameSettings.SoundOn/MusicOn` okur ve yazar.
  `LobbyAccountUI` kullanılmaz (profil ve hesaba bağlı).
- `UiWindow` ile geri tuşu pencereyi kapatır.

### Lobi: el sayısı

El sayısı seçenekleri 1-7 olur (bugün 1-5). Değerler kodda değil `LobbyMenu.unity` sahnesinde
saklı ve `UiKitLobby.RoundOptions` onları sahneden geri okuyor; builder'da sabit 1-7 yapılır ve
lobi yeniden üretilir. "BOTLARLA OYNA" masası bugünkü gibi tek el kalır.

### Ağ ve senkron

- Bir yeni RPC (`OpenValueRPC`) ve yeni oda özellikleri (`Hand1`..`Hand7`, `SeatNames`). Hepsi
  yalnızca gösterim verisidir; oyun durumuna yazmaz, `DesyncDetector`'ın karşılaştırdığı alanlara
  dokunmaz.
- `PhotonServerSettings.RpcList` kaydedilir ve commit edilir.
- Eski ve yeni sürüm aynı masada oynayamaz (RPC listesi değişir). Oyun yayında değil.

## Claude'un kararları (Osman değiştirebilir)

- Geçmiş oda özelliklerinde tutulur (geri dönen oyuncuya hazır).
- Yerine bot geçen oyuncunun koltuğu "Ad (bot)" yazar.
- Yalnızca kendi cezalarımın sebep dökümü; diğerlerinin toplamı.
- Maçı kazanan = toplamı en düşük; eşitlikte koltuk numarası küçük olan.
- Açma puanı ilk açıştaki değer; atışta görünür.
- Masada yalnızca Ses ve Müzik; dil yok.
- TEKRAR OYNA düğmesi kalkar.
- El sayısı 1-7; botlu masa tek el kalır.
- Ayrılan oyuncunun koltuğu maçı kazanabilir (puanı en düşükse).
- Müzik anahtarı masada da durur (lobideki gibi; müzik sistemi yokken etkisizdir).
- Baraj, düğmeye basılan değil gerçekten açılan puandan beslenir (hata düzeltmesi).

## Uygulayana bırakılanlar

- Panelin tam ölçüleri, yazı boyutları, renk tonları (kit renkleri).
- RPC dizilerinin tam biçimi.
- `table.*` anahtar adları ve İngilizce karşılıkları.
- Eski panel nesnelerinin sahneden nasıl kaldırılacağı.

## Doğrulama

- EditMode: `HandBreakdown.Build` testleri (bitiren, açmış, açmamış, çiftle açmış, okeyle bitiş,
  çarpanlar, berabere; `handTotal` = bugünkü net), `MatchScores` testleri (toplam, önde olan,
  eşitlik, 7 el).
- Çok editörlü senkron testi geçer (`Run-SyncTest.ps1 -Deals 2`). Her editör, el dökümü gelince
  koltuk toplamlarını tek satır olarak loglar (`[SKOR] el 2: 1=-660 2=310 3=2310 4=570`); dört
  editörün satırları aynı olmalıdır.
- Editor'de botlarla 2 ellik masa: el sonu tablosu, ikinci elin sütunu, maç sonu başlığı, 7 elde
  sütunların sığması (sahte veriyle), tr ve en, 16:9 ve 20:9 ekran görüntüleri.
- Masada ayarlar: Ses kapatılınca ses kesilir; geri tuşu pencereyi kapatır.
- Telefonda deneme Osman'dadır.

## Uygulamadan devreden notlar (2026-11-20)

**Ağ üzerinde yeni olanlar**
- Oda özellikleri: `Hand1..Hand7` (int[], el dökümü; biçim yukarıdaki gibi), `SeatNames` (string[4]).
- RPC: `OpenValueRPC(int que, int value, bool isPairs)` (açış puanı isimlikte).
- `RPC_ResetTurnForNewRound` artık yalnız masa sahibinden gider (2. elde masa sahibinin takılması
  bunun kopyalarındandı).

**Kaldırılanlar**
- Eski skor paneli ve `table.leading` anahtarı. "önde" etiketi Osman'ın isteğiyle kalktı: alt bölüm
  (eller ve toplam) sıraya göre dizilir, en düşük toplam üstte, eşitlikte koltuk sırası.

**Claude'un yalnız verdiği kararlar (Osman onaylamadı)**
- CEZALAR ve BİTİRME hücreleri BU EL ile aynı renkte (eksi yeşil, artı kırmızı).
- Geri sayım 10'dan başlar, 0'da kalır; metin "{0} sn" / "{0} s".
- Aynı el dökümü farklı içerikle yeniden gelirse tablo yenilenir, sayaç sıfırlanmaz.
- Skor tablosu, `UiKitTable.cs` değişince kurucu tarafından baştan kurulur (dosyanın MD5'i
  `ScoreboardView.layoutSignature` içinde); sahnede elle yapılan düzenleme kaybolur.
- Aynı turda 68 açıp 24 daha yere konursa baraj ve etiket 68 kalır.
- İngilizcede "kafa" çevrilmedi.
- Masadaki ayarlar penceresi skor tablosu açıkken açılmaz.

**Doğrulanan**
- EditMode 376/376; dil ve ses denetimleri hatasız.
- Dört editör, `Run-SyncTest.ps1 -Deals 2 -Mode smart` (8abd208 üzerinde): `PASS: 2 el, ok 94,
  desync 0`; dört editörde `[SKOR] el 2: 1=1590 2=1530 3=1070 4=90` aynı. Editörler tek
  `Editor.log` paylaştığı için `[SKOR] el 1` satırı yalnız iki kez görüldü (ikisi aynı).
- Tek editör: `-Solo -Mode smart -Seed 11` PASS (99a9f04 üzerinde).
- Ekran görüntüleri: `.superpowers/shots-keep/masa/`.

**Doğrulanmayan**
- Sıralama değişikliğinden (99a9f04) sonra dört editörlü test koşulmadı; `-Deals 3 -Mode smart`
  ve düz `-Deals 2` hiç koşulmadı (klon editörler kapalıydı).
- İngilizce 7 ellik tablo görüntüsü çekilmedi.
- Masa sahibi değişimi ve oyun ortasında ayrılan oyuncunun "(bot)" adı canlı denenmedi.

**Bilinen açıklar**
- Masa sahibi el ortasında değişirse o elin ceza toplamları kaybolur.
- Aynı turda geri alıp yeniden açınca baraj güncellenmez, etiket çıkmaz.
- Son taşla bitirmede `FinishGameRPC` iki kez gider (berabere ve kazanan).
- Çıkış ve Ayarlar yazıları telefonda küçük (yaklaşık 15 px).

**Telefonda denenecekler**
- 2 ellik gerçek maç: el sonu tablosu, ikinci el sütunu, maç sonu.
- 7 ellik masada TABLO düğmesi.
- Açış etiketi isimlikte; masada ses kapatma; bot adları "Bot 1, Bot 2".
