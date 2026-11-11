# Test Oyuncuları (Akıllı Otopilot) — Tasarım

Tarih: 2026-11-11 · Durum: onay bekliyor (builder check sonrası güncellendi)
Önceki iş: `docs/superpowers/specs/2026-11-11-sync-autotest-design.md` (Aşama A, bitti). Kurallar: `docs/superpowers/specs/2026-11-11-okey-kurallari.md` + kural kitabı (51 Okey.pdf, normal mod, katlamalı).

## Amaç ve neden

Oyundaki botlar bilerek basit kalacak (ortadan çek, at). Testler için ayrı, akıllı **test oyuncuları** yazılacak: 4 editörlü koşularda gerçek oyuna yakın tam oyunlar oynasınlar, açsınlar, işlesinler, yandan alsınlar, bitirsinler; oynadıkça bug bulsunlar. Kullanıcı ile Claude bu bugları birlikte düzeltecek.

Kullanıcının istekleri:
1. Eli otomatik seri ve çift olarak dizmeyi bilsinler.
2. Seri mi çift mi daha çok puan getiriyorsa o yolda kalsınlar; açabiliyorlarsa açsınlar.
3. Açtıktan sonra ellerinde işlek taş varsa sıraları gelince işlesinler.
4. Yandan da alsınlar (açmak/işlemek için).
5. Bazen bilerek yanlış hamle denesinler; her şey çok iyi kayıt altında olsun; istenince sadece doğru oynasınlar.
6. Test oyuncusu sonucu **kendisi kontrol etsin**: beklenen ile oyunun yaptığı farklıysa `[BUG]`.

Yaklaşım (kullanıcı seçti: **C**): önce akıllı oyuncu (A); belirli mekaniği zorlamak için "şu oyuncuya şu el" kancası sonra küçük bir ek (B).

## Hedefler

- Runner: `Run-SyncTest.ps1 -Mode smart -Deals 1 -Mistakes 0 -Seed N` (mevcut `simple` otopilot aynen kalır).
- Her client kendi koltuğunu insan kod yolundan oynar: sürükle-bırak (`TileUI.OnBeginDrag` → hedef → `Placeholder.OnDrop` → `TileUI.OnEndDrag`) ve gerçek buton işleyicileri (`ExitButton.MeldTileButton`, `MeldPairTileButton`, `ActiveTilesButton`). RPC doğrudan çağrılmaz.
- Her hamle için okunabilir **hamle günlüğü** + bağımsız **beklenen sonuç** kontrolü; fark = `[BUG]` = FAIL.
- Aynı seed + aynı `-Mistakes` = aynı oyun (kararlar seed'li RNG'den).

## Hedef dışı

- Oyundaki botları akıllandırmak.
- Hardcore mod, katlamasız mod.
- Bulunan oyun buglarını bu işte düzeltmek (raporlanır, ayrı iş).
- 2. el host donma hatası (açık, ayrı iş). Bu yüzden kabul koşuları `-Deals 1`.
- Senaryo kancasının (B) bu turda yapılması — sadece arayüzü tanımlanır.

## Anti-hedefler

- Config yokken oyunda en ufak davranış değişikliği.
- Beklenen sonucu oyunun kendi fonksiyonlarıyla hesaplamak (`PenaltySystem.EndPenalty`, `OkeyRuleEngine.PassesOpenLimit` vb.). Kod kendisiyle kıyaslanırsa hiçbir bug yakalanmaz. Beklenen sonuç kural kitabından **bağımsız** yazılır.
- Test oyuncusunun kural ihlalini oyunun reddetmesine güvenip "hata modu" dışında ihlal yapması (doğru modda her hamle kurala uygun olmalı; red = `[BUG]` veya beyin hatası).

## Mevcut durum (kodda doğrulandı)

- Istaka: `PlayerTileContainer` altında 30 slot (`PlaceHolder`..`PlaceHolder (32)`) + `RightTileContainer` (atma). Açma kontrolü `ScoreManager.GetSplittedGroups`: slot 0–14 ve 15–29 iki ayrı satır; satırda yan yana dolu slotlar bir grup, boş slot grubu böler. `CheckForPer` grupları `validPerss`'e, puanları `totalScore` (seri+grup) ve `pairTotalScore`'a yazar.
- Açma: `OnButtonClick` (seri), `OnPairButtonClick` (çift) — `canDrop` ister; limit `OkeyRuleEngine.PassesOpenLimit` (ilk açan 51, sonra yeri geçmek); elde ≥1 taş kalmalı; sadece çift açan seri açamaz; seri açan masada çift açılmadıkça çift açamaz.
- Yandan alma: `LeftTileContainer` üstündeki taşı ıstakaya sürükle (`TileUI.OnBeginDrag` kontrolleri; el <15). O tur açmak **veya işlemek** zorunlu (`TurnManager.CanFinishTurn`); yoksa atma denemesinde `HandleFailedSidePick`: +250, taş yana döner, ortadan 1 taş.
- İşleme: hand `TileUI`'yi masadaki `Placeholder`'a sürükle → `OnEndDrag` C şıkkı → `ScoreManager.ProcessManualDrop`. Ya da `ActiveTilesButton` → `ActivePers` (otomatik). Bir uçtan turda en fazla 2 taş. Başkasının perine işleyince per sahibine n×10 (gösterge ise n×20), atışta yazılır.
- Atma cezaları (master, `CheckPenaltyRPC`): okey, gösterge kopyası, işlek → +250; bitiren atış cezasız.
- El sonu (`CalculateAndDistributeScores` → `OnGameEndedDetailedRPC(actors, rewards, penalties, netScores)`): kazanan −600; açmayan 600; açan: taş×10×renk çarpanı (çift açtıysa ×2) + okey başına 250; özel bitiş (çift / okey atarak / başka kimse açmadan) kaybedenleri ×2'şer çarpar.
- El içi cezalar `ScoreManager.playerScores` → `PlayerScore` player property (tüm client'larda okunabilir).
- Tüm client'lar tüm elleri tutar (`TileDistrubite.playerTiles1..4`), masa perleri `meltedTiles1..4` + UI.
- `DesyncDetector` hamle anahtarı `deal.move` (lockstep, her client'ta aynı).

## Yaklaşımlar

| | Ne | Artı | Eksi |
|---|---|---|---|
| A | Akıllı oyuncu: saf C# beyin + sürücü + bağımsız beklenen sonuç | Gerçeğe yakın, her seed farklı el, oynadıkça bug bulur | En büyük iş (jokerli bölme, beklenen sonuç) |
| B | Senaryo: kurulmuş eller + adım listesi | Kesin sonuç, yazması kolay | Sadece yazılanı test eder |
| **C** ✅ | A ana yol; B sonradan "şu oyuncuya şu el" kancası | Oynayarak bug bulma + gerektiğinde mekanik zorlama | — |

## Tasarım

### 1. Beyin: `TestPlayerBrain` (saf C#, Unity'siz)

Yer: `Assets/Osman/Scripts/GamePlay/AutoTest/` (Assembly-CSharp; EditMode testleri `Assets/Osman/Editor/Tests/`). `UnityEngine.Random` değil `System.Random(seed)`.

**Girdi** (`TableView`, sürücü her karar anında doldurur): elim (id, renk, sayı, tip), gösterge, yandaki taş (varsa), masadaki işlenebilir slotlar (`Placeholder.available` + `AvailableTileInfo` + sahip que + per tipi + slot kimliği; okey değişimi yapılabilir slotlar), masa limiti, başkası açtı mı, ben seri/çift açtım mı, masada çift açıldı mı, `canDrop`, `hasPickedFromSide`, ortada kalan taş sayısı, sıram.

**Çıktı** (`TurnPlan`): sıralı adımlar — `DrawMiddle` | `DrawSide`, `Arrange(gruplar)`, `OpenSeries` | `OpenPairs`, `Process(taş, slot)`…, `Discard(taş)` — her adımda `reason` metni ve (varsa) `mistake` etiketi. Plan aşamalıdır: çekmeden sonra el değişeceği için beyin çekme sonrası **yeniden** çağrılır (çek → yeniden planla → diz/aç/işle/at).

**Çözücü:**
- Seri yolu: elden ayrık geçerli perler (renk serisi ≥3, ardışık, 13→1 yok; grup 3–4 farklı renk aynı sayı; okey boşluk doldurur, gösterge kopyası seride normal taş) — toplam puanı en yüksek seçim. Puan = taş sayıları toplamı (okey yerine geçtiği sayı).
- Çift yolu: aynı renk+sayı çiftler; okey+taş (taşın sayısı ×2); gösterge kopyası+herhangi taş (gösterge sayısı ×2); en yüksek puanlı çiftler.
- Her elde ilk kendi turunda yol seçilir (yüksek puan), el boyunca sabit kalır; açtıysa açtığı yol.
- Seçilen her per, karar verilmeden önce beynin kendi bağımsız kural kontrolünden geçer.

**Karar kuralları (doğru mod):**
1. **Çekme:** Yandaki taş, açmamışken açmayı (limit + ≥1 taş kalır) mümkün kılıyorsa veya açmışken bu tur işlenebiliyor / yeni per tamamlıyorsa → `DrawSide` (o tur açmak/işlemek planın parçası). Aksi halde `DrawMiddle`. El 15 iken çekme yok (1. sıra ilk tur).
2. **Açma:** Limiti geçiyor ve atacak taş kalıyorsa aç (yolun butonu). Açtıktan sonra yeni per oluşursa sonraki turlarda yine açar (limit yok).
3. **İşleme:** Açmışsa, atacak 1 taş bırakarak elindeki her işlenebilir taşı işle (okeyi işlemez; uç başına 2 sınırına uyar). Okey değişimi mümkünse yapar.
4. **Atma:** Okey, gösterge kopyası ve işlek taş atılmaz (eli başka taş yoksa: en az zararlı, kayda geçer). Kalanlardan pere en az katkı veren atılır. Atınca el biterse bu bitiştir.

**Hata modu** (`-Mistakes p`): her hamle başında `p` olasılıkla, o an mümkün olan hatalardan biri seçilir (seed'li):
- `SidePickNoOpen`: yandan al, açma, atmayı dene → beklenen +250, taş yana döner, ortadan 1 taş.
- `DiscardOkey` / `DiscardIndicator` / `DiscardIslek` → beklenen +250.
- `OpenBelowLimit`: limit altında aç butonu → beklenen red, hiçbir şey değişmez.
- `ProcessBeforeOpen`: açmadan işlemeyi dene → beklenen red.
- `ThirdOnSeriesEnd`: aynı uca 3. taş → beklenen red.
Hata sonrası oyuncu doğru oynamaya döner.

### 2. Beklenen sonuç: `RuleOracle` (saf C#, bağımsız)

Kural kitabından ayrı yazılır; oyunun `OkeyRuleEngine`/`PenaltySystem`'ini **çağırmaz**. EditMode testleri kural kitabı örnekleriyle (kırmızı 3 taş+1 okey=400, mavi çift 3 taş=180, gösterge 6 işlenince 120, …).

Her plan adımı bir `Expectation` üretir: hangi oyuncunun `PlayerScore`'u ne kadar değişmeli, benim elim kaç taş olmalı, açtım bayrağı, masa limiti, adım kabul mü red mi. El sonunda her client, kendi gördüğü son durumdan (4 el, açanlar, çift açanlar, kazanan, bitiş türü, renk çarpanı) herkesin beklenen el sonucunu hesaplar ve `OnGameEndedDetailedRPC` yüküyle karşılaştırır.

Gözlem: adımdan sonra durum oturana kadar beklenir (≤2 sn; RPC/property yankısı), sonra `PlayerScore` property'leri, `TileDistrubite` elleri, `ScoreManager.hasOpened*`, `GameManager.CurrentTableLimit` okunur. İşleme cezaları atışta yazıldığı için işleme beklentileri tur sonunda (atıştan sonra) kontrol edilir.

El sonu yükünü görmek için `GameManager`'a tek satırlık bir debug olayı eklenir: `public static event Action<int[], int[], int[], int[]> HandResult;` `OnGameEndedDetailedRPC` başında tetiklenir. Abonesi yokken etkisi yok.

### 3. Sürücü (`SyncAutoTest` içinde, `mode == "smart"`)

- `DrawSide`: `LeftTileContainer` üstündeki `TileUI` → boş ıstaka slotu (`DragTo`).
- `Arrange`: grupları satır 0–14 / 15–29'a, her grup bitişik, gruplar arası en az bir boş slot; perde olmayan taşlar ayrı. `DragTo` artık `ExecuteEvents.dropHandler` ile `Placeholder.OnDrop`'u da sırasıyla çağırır (gerçek bırakma ile aynı). Diziden sonra `ScoreManager.validPerss` beyinin planladığı gruplarla karşılaştırılır; farklıysa `[BUG]` (ya da sürücü hatası, günlükte ayrılır).
- `OpenSeries` / `OpenPairs`: sahnedeki `ExitButton` bileşeninde `MeldTileButton()` / `MeldPairTileButton()`.
- `Process`: elde `TileUI` → hedef meld `Placeholder` (`DragTo`). Seed'e göre bazı turlarda tek tek sürüklemek yerine `ActiveTilesButton()` (otomatik işle yolu da test edilsin; beklenti aynı).
- `Discard`: mevcut.
- Bekleme/zaman aşımları mevcut otopilottaki gibi; her adım sonrası gözlem.

### 4. Hamle günlüğü

Her client: `<proje>/Logs/autotest_<runId>_<nick>.log`. Satır biçimi:

```
1.17 | AT-g2 que3 | el(14): r3 r4 r5 b7 b7 J ... | karar: DrawSide k9 | sebep: k7-k8-k9 serisi, seri 54 > limit 51, açacağım
1.17 | AT-g2 que3 | [BİLEREK HATA] DiscardIslek r6 | beklenen: que3 +250 | gözlenen: que3 +250 | OK
1.21 | AT-host que1 | Process r10 -> AT-g1 serisi | beklenen: que2 +100 | gözlenen: que2 +0 | [BUG]
```

Anahtar `deal.move` = `DesyncDetector` sayacı (public okunur özellik eklenir). Status'a yeni sayaçlar: `opens`, `processes`, `sidePicks`, `mistakes`, `bugs`, `finishType`.

Runner FAIL'de: ilk `[BUG]` satırını ve aynı anahtardaki diğer client'ların satırlarını basar; desync'te desync anahtarındaki ve bir önceki hamlenin günlük satırlarını basar. PASS özetine istatistik eklenir (kaç açma, işleme, yandan alma, bitiş türü) — oyuncuların gerçekten açıp işlediği görünsün.

### 5. Runner ve config

- Yeni parametreler: `-Mode simple|smart` (varsayılan `simple`, mevcut davranış), `-Mistakes 0..1` (varsayılan 0).
- Config'e `mode`, `mistakeRate` eklenir.
- Harness öz-testi: `-InjectOracleBug N` → AT-g1 N. hamlesinde kasten yanlış beklenti üretir; koşu `[BUG]` ile FAIL olmalı (oracle'ın fark yakaladığının kanıtı).

### 6. Senaryo kancası (B, sonraki tur — sadece arayüz)

Config'e ileride `forcedHands: { "1": ["r7","r8","J",...], ... }`; master dağıtımdan sonra elleri bununla değiştirir (yalnız config varken). Bu turda yapılmaz.

## Kabul kriterleri

- EditMode: çözücü (sabit ellerde beklenen per seti/puan, jokerli durumlar, 13→1 yok), oracle (kural kitabı örnekleri), karar kuralları (yandan alma açtırıyorsa alır, işlek atmaz, ≥1 taş bırakır) — yeşil.
- Canlı `-Mode smart -Deals 1 -Mistakes 0 -Seed N` (3 farklı seed): koşu biter; özette en az bir koşuda açma ve işleme > 0. Çıkan her `[BUG]` gerçek oyun hatası olarak raporlanır (düzeltilmez) — harness/sürücü kaynaklı olmamalı.
- Canlı `-Mistakes 0.2`: günlükte `[BİLEREK HATA]` satırları, her birinin beklenen/gözlenen karşılaştırması var.
- `-InjectOracleBug 5` → FAIL, sebepte `[BUG]` ve AT-g1.
- `-Mode simple` davranışı ve mevcut testler değişmez; config yokken oyun aynı.

## Builder'a bırakılanlar

- Çözücünün algoritması (ıstaka ≤15 taş: geri izleme yeterli), atılacak taş sezgisi.
- Hata türü seçim dağılımı, bekleme süreleri.
- Günlük satırının tam biçimi (yukarıdaki alanlar korunarak).

## Builder check sonrası kararlar

Kullanıcı cevapları (kural dokümanındaki "Kullanıcı kararları" bölümü) ve bu tasarıma etkileri:

- Bloke eden oyun hataları bu tasarımdan önce düzeltildi: yandan alıp açamayan bir kez +250 yer ve kilitlenmez (#34); işleme cezaları sadece master'da uygulanır, `PlayerScore` güvenilir gözlemdir; otomatik işle elde 1 taş bırakır.
- Hata kataloğu: `SidePickNoOpen` → beklenen tek +250, taş yana döner, ortadan 1 taş, oyuncu atarak devam eder. Yeni hata `SidePickCancel`: yandan al, "geri bırak" (`SidePickUIManager.OnCancelButtonClicked`) → beklenen ceza yok, taş yana döner, oyuncu ortadan çeker. `OpenBelowLimit` → beklenen red + ekranda mesaj, ceza yok.
- Oracle kuralları: yandan alma cezası (önceki oyuncuya sayı × 10) atışta, tur açarak veya işleyerek tamamlandıysa; kazanan −100 × renk çarpanı; kafa = ilk açılışla aynı turda bitiş ve başka kimse açmamış; gösterge kopyası atmak sadece masada çift açılmışsa 250; sonradan açılış limiti yükseltmez.
- `ActiveTilesButton` sadece sonucu plana birebir eşitse kullanılır (elde okey yok, her taşın tek hedefi var); aksi halde tek tek sürükleme. (Claude'un kararı.)
- Yandan alma cezası `CurrentSidePickTile.number` × 10; okey alındıysa okeyin taşıdığı sayı.
