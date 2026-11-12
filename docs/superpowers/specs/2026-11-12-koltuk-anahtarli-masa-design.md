# Masayı koltuğa bağlama (akıllı bot, Aşama 0) — tasarım

Tarih: 2026-11-12. Kaynak: kullanıcıyla sohbet + aynı gün yapılan kod incelemesi.
Durum: kullanıcı onayı bekliyor.

## 1. Ne ve neden

Kullanıcı üç editör açmadan üç akıllı rakiple oynamak istiyor; belki ileride oyunculara da açılacak. Seçilen yol:
test oyuncusunun karar kodunu (`TestPlayerBrain`) oyunun botuna taşımak.

Kod incelemesi bunun önünde bir temel engel gösterdi: masa bir koltuğu **Photon oyuncusuyla ve oyuncunun ismiyle**
tanıyor. Bot koltuğunda ikisi de yok. Sonuç:
- Per alanı `GameObject.Find(NickName + " meld")` ile aranıyor (14 yerde). Bot koltuğunun perleri hiçbir cihazda çizilmez,
  bota işlenemez, işlek kontrolü bot perlerini görmez.
- El sonu puanlama oyuncu numarasıyla (actor) çalışıyor ve yalnızca `PhotonNetwork.PlayerList`'i dolaşıyor. Bot kazanamaz,
  sonuç tablosunda satırı olmaz.
- Anlık ceza puanı oyuncunun Photon özelliğine (`PlayerScore`) yazılıyor. Bot koltuğunun cezası diğer cihazlara gitmez.

Aynı isim araması, yayından önce çözülmesi gereken bilinen hatanın da sebebi: aynı isimli iki oyuncu aynı masaya
oturunca per alanları karışıyor.

Bu aşama masayı isme ve oyuncuya değil **sıra numarasına (que) / koltuğa** bağlar. Bot bu aşamada yeni hiçbir hamle
öğrenmez; yalnızca masada "var" olur.

## 2. Hedefler

- Per alanı her yerde sıra numarasıyla bulunur; isimle arama kalmaz.
- Aynı isimli iki, üç ya da dört oyuncu aynı masada sorunsuz oynar.
- El sonu puanlama ve sonuç tablosu dört koltuğu da kapsar (bot koltukları dahil); kazanan sıra numarasıyla bildirilir.
- Bot koltuğunun anlık ceza puanı ve adı ("Bot 1"…) tüm cihazlarda görünür.
- İnsan oyununun davranışı değişmez: aynı seed aynı sonucu verir.

Hedef olmayanlar:
- Botun açması, işlemesi, yandan alması, bitirmesi (Aşama 1-4).
- Sıra bayraklarının (`canDrop`, `hasPickedFromSide`, açtı/işledi), `occupiedRows`'un ve bekleyen cezaların koltuk başına
  çoğaltılması (Aşama 1).
- `SyncProcessedTileRPC`'nin hedefinin değişmesi, işlenmiş taşların veri listesine yazılması (Aşama 2).
- Oyundan çıkan insanın koltuğunu akıllı botun devralması, yeniden bağlanma, okey değiştirme.

Anti-hedefler:
- Yeni bir desync ya da taş kaybı.
- Mevcut test oyuncularının ve `Run-SyncTest.ps1` koşularının bozulması.
- Sahne ya da prefab değişikliği gerektiren bir çözüm.

## 3. Kısıtlar (koddan)

- Atma alanı zaten koltukla bulunuyor: `TileDistrubite.InstatiateSideTiles` → `seatOfQue[que]` → yerel koltuğa göre
  `relativeIndex = (hedefKoltuk - yerelKoltuk + 4) % 4` → `seatManager.tiledropOffset[relativeIndex]`. Per alanları aynı
  dizilimde `seatManager.meldTileOffsets[relativeIndex]` içinde duruyor. Yani gereken eşleme kodda var, sadece per alanı
  için kullanılmıyor.
- `seatOfQue` dört sırayı da koltuğa eşliyor (bot koltukları dahil) ve `SyncQueToSeatMapping` ile tüm cihazlara gidiyor.
- Yerel oyuncunun per alanı her zaman `meldTileOffsets[0]` (yerel oyuncu hep altta).
- İsimle arama yapan yerler: `ScoreManager.cs` 74, 1603, 2231; `ProcessAssist.cs` 110; `TileDistrubite.cs` 967, 1250,
  1472, 1548, 1578, 1816, 2206; `SyncAutoTest.Smart.cs` 126, 192, 413 (toplam 14). Bunların çoğu "her oyuncunun per
  alanı" için `PlayerList` dolaşıyor. İsimle kurulan iki anahtar daha var: `SyncAutoTest.Smart.cs` 143 (slot anahtarı)
  ve `TileDistrubite.cs` 2204-2215 (`DesyncDetector`'a giden `board#<actor>` satırları).
- `AssignPlayerQueue` her elde başlangıç koltuğunu rastgele seçiyor: sıra → koltuk eşlemesi elden ele döner. Koltuk sabittir,
  sıra numarası değildir.
- İsimlikleri yalnızca `SeatManager.UpdateSeatDisplay` yazıyor ve `PlayerList` dolaşıyor; bot koltuklarına bugün metin
  yazılmıyor.
- Sonuç tablosu: sahnede dört `playerResultSlots` var (`seatNumber` 1-4, aslında sıra numarası). Bot satırı bugün
  "oyuncu bulunamadı" dalına düşüp "-" gösteriyor. Sahne değişikliği gerekmiyor.
- `PlayerScore` şuralarda da geçiyor: `ScoreManager.cs` 67, 233; `RoomManager.cs` 134; `UIManager.cs` 123 (puan panelini
  yenileyen `OnPlayerPropertiesUpdate`); `SyncAutoTest.Smart.cs` 249 (test oyuncusunun gözlemi).
- Bir koltuğun "bot gibi oynatılması" bugün `TurnManager.Update`'te: o sırada etkin (`!IsInactive`) oyuncu yoksa master oynar.
- `SeatManager.UpdateSeatDisplay` per ve atma alanı nesnelerini `NickName + " meld"` / `NickName` diye adlandırıyor.
- Puanlama: `GameManager.FinishGameRPC(int winnerActorNumber, …)` → `CalculateAndDistributeScores` (`PlayerList` döngüsü,
  kazanan `CurrentRoom.GetPlayer(actor)`) → `OnGameEndedDetailedRPC(int[] actors, …)` → `UIManager.ShowDetailedGameOver`
  (sıra numaralı sözlükler). `GameManager.HandResult` olayını `SyncAutoTest` dinliyor.
- Anlık puan: `ScoreManager.UpdatePlayerCustomProperties(que)` → oyuncunun `PlayerScore` özelliği → `UIManager` (satır 99)
  `PlayerList` üzerinden okuyor.
- `[PunRPC]` adları değişmezse `PhotonServerSettings.RpcList` değişmez. İmza değişikliği tüm cihazların aynı sürümde
  olmasını ister (telefon APK'sı yeniden kurulur).
- Test altyapısı: `Tools/SyncTest/Run-SyncTest.ps1` (dört editör, `-Mode smart`, `-Seed`, `-Mistakes`, `-Solo`),
  `DesyncDetector` (cihazlar arası veri karşılaştırması), `[ISTAKA]` ıstaka kontrolü. Faz öncesi referans:
  `-Mode smart -Seed 11 -Deals 1` → `PASS: 1 el, ok 52, desync 0`.

## 4. Yaklaşımlar

### 4.1 Per alanını bulma
- **A. Sıra numarasından koltuğa, koltuktan alana (seçilen).** Tek yardımcı: `MeldAreaOfQue(int que)` ve tersi
  `QueOfMeldArea(Transform)`. Atma alanının bugün kullandığı eşlemenin aynısı. Tüm isim aramaları bununla değişir;
  "her oyuncu" döngüleri "sıra 1..4" olur. Sahne değişmez.
- **B. Bot koltuklarına sahte isim verip isim aramasını sürdürmek** ("Bot 1 meld"). En az kod. Ama aynı isim hatası
  kalır ve bir oyuncu kendine "Bot 1" adını verirse yine karışır: elendi.
- **C. Per alanı nesnelerine kalıcı bir bileşen eklemek.** Sahne/prefab değişikliği ister; alanın hangi sıraya ait
  olduğu her cihazda farklı olduğu için yine çalışma anında hesaplanır: elendi.

### 4.2 Anlık puan
- **A. Tek kaynak: oda özelliği `SeatScores` (sıra numarasına göre dizi), master yazar (seçilen).** İnsan ve bot aynı
  yoldan gider; arayüz tek yerden okur.
- **B. İnsanlar için `PlayerScore` kalsın, botlar için ayrı yol.** İki kaynak, iki okuma yolu: elendi.

### 4.3 El sonu
- **A. Sıra numarası (seçilen).** `FinishGameRPC(int winnerQue, …)` (-1 = kazanan yok), puanlama sıra 1..4'ü dolaşır,
  `OnGameEndedDetailedRPC(int[] ques, …)`. Bitiren insan kendi sıra numarasını gönderir.
- **B. Botlara sahte oyuncu numarası.** Photon'un gerçek numaralarıyla çakışma riski, her kullanımda özel durum: elendi.

## 5. Tasarım

### 5.1 Koltuk eşlemesi
- Saf fonksiyonlar (EditMode testli): `RelativeIndex(int targetSeat, int localSeat)` ve tersi. `SeatManager` ve
  `TileDistrubite.InstatiateSideTiles` içindeki tekrar eden formül bunlara bağlanır.
- `MeldAreaOfQue(int que)`: `seatOfQue[que]` → yerel koltuğa göre `relativeIndex` → `meldTileOffsets[relativeIndex]`.
  Eşleme henüz gelmediyse ya da sıra geçersizse `null` döner; çağıran yer bugünkü "bulunamadı" davranışını korur.
- `QueOfMeldArea(Transform area)`: alanın `meldTileOffsets` içindeki yerinden koltuk, koltuktan sıra. Bulunamazsa -1.
- Yerel oyuncunun alanı için `meldTileOffsets[0]` kullanılır (`ScoreManager.Start` eşleme gelmeden çalışabilir).
- Nesne adları (`NickName + " meld"`) hata ayıklama için yazılmaya devam eder ama hiçbir kod onlara güvenmez.

### 5.2 İsim aramalarının değişmesi
Bölüm 3'teki 14 yerin her biri `MeldAreaOfQue` / `QueOfMeldArea` kullanır. Bir döngü "her oyuncunun per alanı" için
`PlayerList` dolaşıyorsa "sıra 1..4" olur; böylece bot koltuklarının ve odadan çıkmış oyuncuların alanları da kapsanır.
`PlayerList`'i başka amaçla dolaşan yerlere (koltuk atama, oyuncu sayısı, sıra kimde) dokunulmaz.

Test oyuncusu sürücüsündeki slot anahtarı (`"Nick|tip|idx"`) `"<sıra>|tip|idx"` olur. `DesyncDetector`'a giden pano
satırları `board#<sıra>` olur ve dört sırayı da yazar (bot koltukları dahil).

### 5.3 Koltuk adı
`SeatName(int que)`: o sıranın koltuğunda `PlayerList`'te bir oyuncu varsa (bağlantısı kopmuş ama süresi dolmamış olsa
bile) `NickName`; yoksa `Bot <n>`. `n` **koltuğa** göre verilir (oyuncusuz koltuklar arasında koltuk numarası sırasıyla
1'den başlar); sıra numarası elden ele döndüğü için aynı sandalye el boyunca ve eller arasında aynı adı taşır.
İsimlik, anlık puan paneli ve sonuç tablosu bunu kullanır. İsimlikler oyun başladıktan sonra oyuncusuz koltuklara
`Bot <n>` yazar (oyun başlamadan önce bugünkü gibi "OTUR"); oyuncu odadan çıkınca isimlik yenilenir.

### 5.4 Anlık puan
`ScoreManager.playerScores[que]` her değiştiğinde master `SeatScores` oda özelliğini yazar. Arayüz dört koltuğu
`SeatName` + `SeatScores` ile gösterir. `PlayerScore` oyuncu özelliği artık okunmaz ve yazılmaz (bölüm 3'teki altı yer);
puan paneli oda özelliği değişince yenilenir. Test oyuncusu gerçekleşen puanı `SeatScores`'tan okur; beklediği puanı
bugünkü gibi kendi kural denetçisinden hesaplar.

### 5.5 El sonu
- `FinishGameRPC(int winnerQue, bool isOkeyShot, bool isKafa)`.
- `CalculateAndDistributeScores` sıra 1..4'ü dolaşır; "açtı mı", "çiftten açtı mı", kalan taşlar zaten sıra numarasıyla
  tutuluyor.
- `OnGameEndedDetailedRPC(int[] ques, int[] rewards, int[] penalties, int[] netScores)`; `HandResult` olayı sıra
  numaralarını taşır; `SyncAutoTest` buna uyar.
- Sonuç tablosu dört satır gösterir; satır adı `SeatName(que)`.

### 5.6 Değişmeyenler
Hamlelerin ağ çağrıları, taş kimlikleri, işleme senkronu, ceza hesapları, sıra akışı, koltuk seçimi.

## 6. Test

1. **Aynı isim testi (önce kırmızı):** `Run-SyncTest.ps1`'e dört test oyuncusuna aynı **görünen ismi** veren bir seçenek
   eklenir. Test oyuncusunun iç kimliği (`AT-host`, `AT-g1`…; seed ve rol buna bağlı) değişmez, yalnızca Photon'a giden
   isim aynı olur; böylece seed 11 sonucu kıyaslanabilir kalır. Değişiklikten önce koşulur ve bozulduğu görülür
   (beklenen: FAIL ya da `[BUG]`); değişiklikten sonra PASS olmalı.
2. **Davranış aynı kaldı:** `-Mode smart -Seed 11 -Deals 1` → `PASS`, `ok 52`, `desync 0`. `-Seed 44 -Mistakes 0.3` → PASS.
3. **Bot koltukları, iki cihazda:** bugün bot koltuğu yalnızca `-Solo`'da (tek cihaz) oluşuyor. Betiğe "N editör + kalanı
   bot" seçeneği eklenir (örnek: iki editör test oyuncusu + oyunun iki botu). Bu koşuda:
   - her sıranın adı (`SeatName`) `DesyncDetector` anlık görüntüsüne girer ve iki cihazda aynı olmalı;
   - puanlar anlık görüntüye girmez (oda özelliği gecikmeli gelir, sahte desync üretir); test oyuncusunun kural
     denetçisi beklediği puanı `SeatScores`'tan okuduğuyla bugünkü bekleme mantığıyla karşılaştırır;
   - el sonunda dört sıranın (botlar dahil) sonucu kural denetçisiyle karşılaştırılır;
   - insan koltuklarının per alanı eşlemesi pano satırlarıyla (`board#<sıra>`) cihazlar arasında doğrulanır.
   Bot koltuğunun per alanı bu aşamada boş kaldığı için canlı doğrulanamaz; eşlemesi saf testle sınanır, canlı
   doğrulaması botun ilk açışına (Aşama 1) kalır. `-Solo -Mode smart` de PASS kalır.
4. **Saf eşleme:** `RelativeIndex` ve tersi için EditMode testi (dört yerel koltuk × dört hedef koltuk).
5. **Telefon:** yeni APK ile test oyuncularının odasına girip bir el; per alanları ve sonuç tablosu doğru.

Her iş paketinin kapısı: `Check-Secrets.ps1`, EditMode testleri, yukarıdaki 1-3.

## 7. Riskler
- **Zamanlama:** bir çağrı eşleme (`seatOfQue`) gelmeden per alanı isterse `null` alır. Bugün isim araması da aynı
  durumda `null` dönüyordu; çağıran yerlerin `null` davranışı korunmalı, yeni `NullReferenceException` olmamalı.
- **İşlek ve boş slot hesabı** (`RecalculateAllAvailableSlots`) artık bot koltuklarının alanlarını da tarar. Bu aşamada
  bot perleri boş olduğu için sonuç değişmemeli; seed 11 koşusu bunu doğrular.
- **İmza değişikliği:** eski APK ile yeni editör aynı odada oynayamaz.
- **Odadan çıkan oyuncu:** alanı artık sıra numarasıyla bulunduğu için perleri görünür kalır. Bu bir davranış
  değişikliği (iyileşme); el sonu puanlamasında o koltuk da artık satır alır.

## 8. Benim verdiğim kararlar (kullanıcı değiştirebilir)
- Bot adı `Bot 1`, `Bot 2`, `Bot 3`.
- Anlık puan tek kaynaktan (`SeatScores`) gider; `PlayerScore` kaldırılır.
- Odadan çıkmış oyuncunun koltuğu el sonu tablosunda satır alır.
- Nesne adlandırması hata ayıklama için kalır.

## 9. Sonraki aşamalar (ayrı tasarım ve plan)
1. Bot koltuğu durumu (sıra bayrakları, açtı bilgisi, satır takibi, bekleyen cezalar) + bot açar ve ceza kontrolüyle atar.
2. Bot işler: işleme senkronu tüm cihazlara gider, işlenmiş taşlar veri olarak tutulur.
3. Bot yandan alır (geri bırakma ve ceza yolları veri üzerinden).
4. Bot bitirir ve çiftten açar; akıllı bot oda seçeneği olur, eski basit bot varsayılan kalır.

## 10. Yapıcıya bırakılanlar
- Yardımcıların hangi sınıfta duracağı ve adları (bölüm 5'teki davranış dışında).
- 13 arama yerinin her birinde döngünün nasıl sadeleşeceği.
- `SeatScores`'un yazılma sıklığı ve arayüzün yenilenme anı.
- Aynı isim seçeneğinin test betiğindeki adı.
