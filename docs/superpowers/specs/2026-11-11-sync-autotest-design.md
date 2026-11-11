# Sync AutoTest — Tasarım

Tarih: 2026-11-11 · Durum: onaylandı

## Amaç ve neden

Host'ta çalışıp guest'te bozulan sync hataları sadece birden fazla gerçek client ile görünüyor. Şu an bunu test etmek için kullanıcı 4 editörü (ana proje + ParrelSync `51Okey_clone_0..2`) elle Play'e alıp oyun oynuyor. Hedef: Claude'un (veya kullanıcının) **tek komutla** 4 client'lı bir oyunu insansız oynatıp "sync OK mi, değil mi, değilse nerede koptu" cevabını alması. Böylece bir mekanik değiştiğinde dakikalar içinde kontrol edilebilir.

İki aşama (kullanıcının seçimi: C, aşamalı):

- **Aşama A — Otopilot (bu dokümanın kapsamı):** 4 client otomatik aynı odaya girer, her client kendi koltuğunu gerçek insan kod yolundan oynar (desteden çek, at), N el sürer. Master'daki `DesyncDetector` her hamleyi kıyaslar; sonuç tek özet olarak döner.
- **Aşama B — Senaryo (sonra, ayrı tasarım turu):** sabit seed / kurulmuş eller + adım listesi (yandan al, aç, işle, at, bitir). A'nın altyapısı üstüne yalnızca senaryo katmanı eklenir. Bu doküman B'yi sadece A'nın ona kapı bırakması için anar.

## Hedefler

1. Tek komut: `Tools/SyncTest/Run-SyncTest.ps1` → 4 editörü sürer, bitince PASS/FAIL özeti basar, exit code 0/1.
2. Hamleler **insan oyuncunun kod yolundan** yapılır (`TileUI.OnBeginDrag` / `OnEndDrag`), RPC doğrudan çağrılmaz. Sebep: `NextTurnRoutine` gibi yerlerde RPC'den önce yerel hesap var (ör. atılan taşın index'i yerel listeden bulunuyor); desync büyük ihtimalle orada doğuyor.
3. Sonuç: master'daki `[SYNC OK]` / `[DESYNC]` sayıları, ilk desync'in tam metni, her client'ın yakaladığı Error/Exception logları, her client'ın `Logs/desync_*.log` yolu.
4. Kendini doğrulayan test: bilerek desync üreten bir mod (`-InjectDesync`) ile harness'in FAIL verdiği gösterilir.

## Hedef dışı

- Açma, işleme, yandan alma, ceza, okey atma (Aşama B).
- Bağlantı kopması, master değişimi, reconnect.
- Telefon / standalone build'de çalışmak. Sadece Editor'de çalışır.
- Bot koltuğu (otopilotta 4 koltuk da insan client).

## Anti-hedefler (çalışsa bile başarısızlık sayılır)

- Config dosyası yokken oyun davranışında **en ufak** değişiklik (oyuncular ve telefon build'leri etkilenmemeli).
- Sürücünün UI'ın göndermediği RPC'leri göndermesi veya UI'dan farklı sırada göndermesi — bu desync'leri gizler.
- Kullanıcının bir editöre tıklamasını gerektirmesi.

## Mevcut durum (kodda doğrulandı)

- Akış: `MainMenu` (`PhotonManager` bağlanır, `JoinLobby`; `MainMenuUIHandler` NickName'i set edip `LobbyMenu`'ye geçer) → `LobbyMenu` (`RoomManager.CreateCustomRoom` / `JoinRoomManual`) → `RoomManager.OnJoinedRoom` → `Table`.
- `Table`'da `SeatManager` koltukları otomatik atar; 4 oyuncu olunca master 3 sn geri sayım + `ShuffleTiles` ile başlatır. Koltuk seçimi gerekmez.
- El sonu: orta deste biterse `NextTurnRoutine` `FinishGameRPC(-1)` gönderir → `RestartSequence` 10 sn sonra `TotalRounds`'a göre yeni el veya `LeaveRoom`. UI girdisi gerekmez.
- Sıra: `TurnManager.currentTurnPlayer`, `IsPlayerTurn()`, `canDrop`. 1. sıradaki oyuncu taş çekmeden atar (`StartGame`'de `canDrop = true`).
- Taş çekme: orta destenin üstteki `TileUI`'si (`inMiddle`) eldeki boş bir `Placeholder`'a sürüklenir → `AddTileFromMiddlePlayerList`. Atma: eldeki `TileUI` `isRight` olan `Placeholder`'a sürüklenir → `CheckPenaltyRPC` + `ExecuteNextTurn`.
- `OnBeginDrag` `eventData.position` + `pressEventCamera` kullanır; `OnEndDrag` hedefi `transform.position`'a en yakın (<100) `Placeholder` olarak seçer.
- Atılacak taş seçimi için hazır: `ScoreManager.FindBestTileToDiscardForBot(que)` (eli kopyalayarak çalışır, gerçek listeyi bozmaz).
- `DesyncDetector` (yeni, commit edilmemiş): Deal / NextTurn / Finish anlarında snapshot alır, master kıyaslar, `[SYNC OK]` log veya `[DESYNC]` LogError basar, her client `Logs/desync_<zaman>_actor<N>.log` yazar. `forcedSeed` alanı var.
- Editör kontrolü: `unity` CLI (`C:\Users\osman\AppData\Local\Unity\bin\unity.exe`) + `com.unity.pipeline` paketi 4 editörde de kurulu. `unity command <grup> <komut> --project-path <proje> --result-only` ile: `editor.playmode start|stop`, `editor status`, `console get|status|clear`, `script recompile|recompile_status`, `editor set_auto_tick`, `script eval`.

## Yaklaşımlar

| | Ne | Artı | Eksi |
|---|---|---|---|
| **A. Otopilot** | Her client kendi koltuğunu çek-at ile oynar | Ucuz, hızlı (el ≈ 30 sn + 10 sn ara), çekme/atma/sıra/el sonu/yeni dağıtımı kapsar | Açma/işleme/yandan alma/ceza yok |
| **B. Senaryo** | Seed + kurulmuş eller + adım listesi | Belirli mekaniği hedefli test eder | `ScoreManager` açma/işleme giriş noktalarını sürmek gerekir; A'nın birkaç katı iş |
| **C. Aşamalı** ✅ | Önce A, sonra B onun üstüne | Altyapı ortak, A hemen işe yarar, B sıfırdan başlamaz | — |

Seçilen: **C** (kullanıcı). Bu doküman A'yı tanımlar.

Alt karar (Claude'un önerisi, kullanıcıya gösterildi): hamleler RPC ile değil **sürükle-bırak simülasyonu** ile yapılır (Hedef 2).

## Tasarım — Aşama A

### 1. Runtime: `SyncAutoTest` (MonoBehaviour)

Yer: `Assets/Osman/Scripts/GamePlay/SyncAutoTest.cs` (DesyncDetector'ın yanında, aynı stil).

**Açılış kapısı:** `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` ile; yalnızca `Application.isEditor` **ve** `<proje kökü>/Temp/SyncAutoTest/config.json` varsa kendini yaratır (`DontDestroyOnLoad`). Aksi halde hiçbir şey yapmaz. Proje kökü = `Directory.GetCurrentDirectory()` (DesyncDetector ile aynı). `Temp/` her editörün kendi klasörü olduğu için clone'lar birbirinin config'ini görmez (Assets junction'ı paylaşılır, Temp paylaşılmaz).

**Config (runner yazar):**

```json
{
  "runId": "20261111_031500",
  "role": "host",            // host | guest
  "roomName": "autotest_20261111_031500",
  "nickName": "AT-host",     // AT-host, AT-g1, AT-g2, AT-g3
  "deals": 1,                // TotalRounds
  "seed": 0,                 // 0 = rastgele; host DesyncDetector.forcedSeed'e yazar
  "injectDesyncAtMove": 0,   // 0 = kapalı; >0 ise bu guest o hamlede yerel state'i bozar
  "moveDelaySec": 0.3
}
```

**Durum makinesi:**

1. `Connecting` → `PhotonNetwork.IsConnectedAndReady && InLobby` bekle. `PhotonNetwork.NickName = nickName`.
2. `Lobby` → `SceneChangeManager.Instance.ChangeScene("LobbyMenu")`, `RoomManager` bulunana kadar bekle.
3. `Joining` → host: `RoomManager.CreateCustomRoom(roomName, runId, deals, true, false)` — **şifreli** (şifre = `runId`), böylece aynı AppId/region'daki bir telefon dev build'i `JoinRandomRoomOrCreateAuto` ile test odasına düşemez. Guest: `RoomManager.JoinRoomWithPassword(roomName, runId, runId)`; `OnJoinRoomFailed` gelirse 1 sn sonra tekrar. Odaya girince mevcut `RoomManager.OnJoinedRoom` Table'a geçirir.
   - `InTable` → `Table` sahnesi yüklendi ve yerel oyuncunun `SeatNumber` property'si geldi. Runner guest'leri bu duruma göre sıralar (bkz. Runner 4).
4. `Playing` → her frame: `GameManager.isGameReady && !isGameEnded && turnManager.IsPlayerTurn()` ve bir hamle sürmüyorsa hamle coroutine'ini başlat:
   - `canDrop == false` ise **çek**: orta destenin üst `TileUI`'sini, eldeki boş `Placeholder`'a sürükle. Yeni `TileUI` yaratılmaz: sürüklenen aynı obje `SetTileData(allTiles[0])` ile yeniden giydirilip 0.3 sn'de slota kayar. O objenin slota oturmasını ve `canDrop == true` olmasını bekle (zaman aşımı: 5 sn → hata).
   - `moveDelaySec` bekle.
   - **At**: `FindBestTileToDiscardForBot(myQue)` → id'si eşleşen eldeki `TileUI`'yi `isRight` `Placeholder`'a sürükle. `currentTurnPlayer` değişene veya el bitene kadar bekle (5 sn → hata).
5. El bitti (`isGameEnded`) → bekle; yeni el gelirse 4'e dön. `deals` el tamamlanıp oda terk edilince (`!InRoom`) → `Done`.
6. Her durum değişiminde ve her hamlede `status.json` yazılır (aşağıda). Toplam süre aşılırsa runner zaten keser; client tarafında ek watchdog yok.

**Sürükle simülasyonu (tek yardımcı):** `DragTo(TileUI tile, Transform target)`:
`var e = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(cam, tile.transform.position), pressPosition = ..., pointerDrag = tile.gameObject }` → `tile.OnBeginDrag(e)`; `e.pointerDrag == null` ise oyun hamleyi reddetti → hata olarak raporla → `tile.transform.position = target.position` → `tile.OnEndDrag(e)`. `cam`: Canvas Screen Space Overlay ise `null`. Böylece `OnBeginDrag`'deki tüm kural kontrolleri ve `OnEndDrag`'deki tüm yerel hesap çalışır.

Bilinen fark: gerçek bırakmada `OnEndDrag`'den önce `Placeholder.OnDrop` da çalışır (`Placeholder.cs:25-77`) ve taşı boş slota yeniden parent'lar. Çek/at için veri ve RPC değiştirmez (Aşama A güvenli), ama ıstaka dizilimini değiştirir. Aşama B'de per dizilimi önemli olduğu için `DragTo` orada `ExecuteEvents.Execute(target, e, ExecuteEvents.dropHandler)` ile `OnDrop`'u da sırasıyla çağırmalı.

**Desync enjeksiyonu (harness'in kendi testi):** `injectDesyncAtMove > 0` ise o guest, kendi `move` sayacı o değere geldiğinde **sadece yerel** olarak `TileDistrubite` üzerindeki bir oyuncunun taş listesinden bir taşı siler (RPC yok). Master sonraki snapshot'ta `[DESYNC]` görmeli. Bu kod da yalnızca config varken çalışır.

**Log yakalama:** `Application.logMessageReceived` ile Error/Exception/Assert kayıtları toplanır (ilk 30, mesaj + stack'in ilk satırı). `[DESYNC]` satırları ayrıca sayılır, genel hata listesine karışmaz.

### 2. `DesyncDetector`'a küçük ek

Master'da sayaç: `public int OkCount, DesyncCount; public string FirstDesync;` — `[SYNC OK]` basılan yerde `OkCount++`, `Compare` içinde fark bulunca `DesyncCount++` ve ilk mesajı sakla. `SyncAutoTest` master'da bunları `status.json`'a koyar. Desync **sadece bu sayaçtan** okunur, log metninden değil: `Write` her client'ta `[DESYNC] Log dosyası: ...` diye bir info satırı da basıyor.

Seed: config'te `seed != 0` ise host, dağıtımdan önce `DesyncDetector.Instance.forcedSeed = seed` yapar. `NextDealSeed` her eli `forcedSeed + (dağıtım sırası - 1)` ile dağıtır; bugünkü kod her ele aynı seed'i veriyor, `-Deals 2` aynı eli iki kez dağıtırdı. (Claude'un kararı.) Başka davranış değişmez.

### 3. Durum / sonuç dosyası

Her client: `<proje>/Temp/SyncAutoTest/status.json` (her değişimde baştan yazılır):

```json
{
  "runId": "...", "role": "host", "actor": 1, "que": 2,
  "state": "Playing",        // Connecting|Lobby|Joining|InTable|Playing|Done|Error
  "deal": 1, "myMoves": 7,
  "errors": ["..."],
  "desyncLog": "C:/.../Logs/desync_..._actor1.log",
  "sync": { "ok": 31, "desync": 0, "first": null }   // sadece master
}
```

### 4. Runner: `Tools/SyncTest/Run-SyncTest.ps1`

Parametreler: `-Deals 1 -Seed 0 -TimeoutSec 600 -InjectDesync 0 -KeepPlaying`.

Sıra:

1. **Ön kontrol:** 4 proje (`51Okey`, `51Okey_clone_0..2`) için `editor status`: erişilebilir, Play'de değil. Erişilemeyen varsa açıkça söyle ve çık (editörleri runner açmaz — açmak dakikalar sürer; bu ayrı bir adım).
2. **Kod tazele:** her editörde `script recompile` → `recompile_status` `completed|up_to_date` olana kadar bekle → `console status` `compilationFailed == false`. Değilse ilk derleme hatasını bas, çık.
3. **Hazırlık:** her editörde `editor set_auto_tick` (odak dışında da tick'lesin), `console clear`. Eski `Temp/SyncAutoTest/` içeriğini sil, yeni `config.json` yaz (ana proje = host, clone'lar = AT-g1..3; `-InjectDesync N` verilirse sadece AT-g1'e).
4. **Başlat (sıralı):** host'ta `editor.playmode start` → host `InTable` olana kadar bekle → AT-g1 → `InTable` → AT-g2 → `InTable` → AT-g3. Sebep: host diğerlerini sadece `SeatManager.OnPlayerEnteredRoom`'da oturtup oyunu başlatıyor (`SeatManager.cs:101-128`); host Table'ı yüklerken giren guest koltuksuz kalır ve oyun hiç başlamaz. Sıralı giriş ayrıca koltuk/sıra eşleşmesini her koşuda aynı yapar, böylece `-Seed N` bir hatayı aynı editör aynı koltukta olacak şekilde tekrarlar. (Pipeline playmode girişinde kısa süre "No Pipeline instance found" verebilir → birkaç kez tekrar dene.)
5. **İzle:** 2 sn'de bir 4 `status.json`'ı oku; hepsi `Done` veya biri `Error` veya `TimeoutSec` dolunca bitir. İlerlemeyi tek satırda bas (ör. `deal 1 · moves 12/9/9/8 · ok 31 desync 0`).
6. **Topla:** her editörde `console get --level error`; status dosyaları; desync log yolları.
7. **Kapat:** `-KeepPlaying` yoksa 4 editörde `playmode stop`. `Temp/SyncAutoTest/config.json`'ları sil (sonraki elle Play'de otopilot çalışmasın).
8. **Özet + exit code:** `PASS` = hepsi `Done`, `desync == 0`, hata listesi boş. Aksi `FAIL` + sebep (ilk desync metni / ilk hata / hangi client hangi state'te takıldı). Exit 0/1.

### 5. Ön koşul

Windows oturumu açık olmalı (kilitli ekranda Play Mode ilerlemeyebilir, `set_auto_tick` da kurtarmıyor). Kullanıcı başındayken Claude'un koşturacağı bir araç; kilitli ekranda / gece koşu hedef değil. (Claude'un kararı.)

### 6. Kabul kriterleri

- `Run-SyncTest.ps1 -Deals 1` uçtan uca koşar (4 client `Done` veya net bir `Error`/timeout sebebiyle biter) ve özet gerçeği yansıtır, toplam < 3 dk. Bugünkü kodda gerçek bir desync/hata çıkarsa bu harness'in başarısı sayılır, FAIL olarak raporlanır. Bulunan oyun hatalarının düzeltilmesi bu işin kapsamında değil: raporlanır, kullanıcı onayıyla ayrı iş olarak düzeltilir. (Kullanıcı soruyu cevaplamadan dokümanı onayladı; Claude'un önerdiği varsayılan.)
- `Run-SyncTest.ps1 -Deals 1 -InjectDesync 5` → FAIL ve özette `[DESYNC]` ile AT-g1 adı.
- `-Deals 2` → iki el oynanır, ikinci dağıtımda da sync kıyaslanır.
- Config yokken elle Play: davranış bugünküyle aynı (otopilot objesi yaratılmaz).
- Testten sonra 4 editör Play'den çıkmış ve config'ler silinmiş olur.

## Aşama B için bırakılan kapı (şimdi yapılmaz)

- Config'e ileride `scenario` alanı eklenir; `Playing` durumundaki "hamle seç" adımı otopilot yerine senaryo adımını uygular.
- `DragTo` yardımcısı yandan alma ve işleme (meld alanına sürükleme) için de aynen kullanılabilir; açma butonlarının giriş noktası B tasarımında belirlenecek.

## Builder'a bırakılanlar

- Coroutine / bekleme sürelerinin ince ayarı, status yazma sıklığı.
- Runner'ın çıktı biçimi (tek satır ilerleme dışında).
- `DragTo`'da kamera / canvas tespiti, boş `Placeholder` seçimi.
- Hata listesinden gürültü filtrelemesi (ör. Photon dev-region uyarısı Warning olduğu için zaten dışarıda).
