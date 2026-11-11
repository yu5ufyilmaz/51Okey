# Sync AutoTest (Aşama A) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tek komutla (`Tools/SyncTest/Run-SyncTest.ps1`) 4 açık editörü (ana proje + 3 ParrelSync clone) insansız bir oyun oynatıp PASS/FAIL sync özeti alan bir test düzeni.

**Architecture:** Runtime tarafında `SyncAutoTest` MonoBehaviour'u sadece Editor'de ve `Temp/SyncAutoTest/config.json` varken açılır: odaya girer, kendi koltuğunu `TileUI.OnBeginDrag/OnEndDrag` simülasyonuyla oynar (çek, at), durumunu `status.json`'a yazar. `DesyncDetector` master'da OK/DESYNC sayar. PowerShell runner, `unity` CLI ile 4 editörü sırayla Play'e alır, status dosyalarını izler, sonucu özetler.

**Tech Stack:** Unity 6000.3.10f1, Photon PUN 2, C# (Assembly-CSharp, asmdef yok), Unity Test Framework 1.6 (EditMode, NUnit), Windows PowerShell 5.1, Pester 3.4, `unity` CLI + `com.unity.pipeline`.

**Spec:** `docs/superpowers/specs/2026-11-11-sync-autotest-design.md`

## Global Constraints

- Otopilot yalnızca `Application.isEditor` **ve** `<proje kökü>/Temp/SyncAutoTest/config.json` geçerliyken çalışır. Yoksa hiçbir obje yaratılmaz, oyun davranışı birebir aynı kalır.
- Sürücü RPC çağırmaz. Hamleler yalnızca `TileUI.OnBeginDrag` / `OnEndDrag` üzerinden yapılır.
- Proje kökü = `Directory.GetCurrentDirectory()` (DesyncDetector ile aynı).
- Oda adı `autotest_<runId>`, oda şifresi = `runId`, `runId` = `yyyyMMdd_HHmmss`. Nick'ler: `AT-host`, `AT-g1`, `AT-g2`, `AT-g3`.
- Projeler (sıra = rol): `C:\Users\osman\Documents\51Okey` (host), `..._clone_0` (AT-g1), `..._clone_1` (AT-g2), `..._clone_2` (AT-g3).
- CLI: `C:\Users\osman\AppData\Local\Unity\bin\unity.exe command <grup> <komut> --project-path <proje> --result-only --no-banner`. Playmode girişinde gelen "No Pipeline instance found" geçicidir, tekrar denenir.
- PowerShell 5.1: `&&`, `??`, ternary yok. JSON dosyaları `[IO.File]::WriteAllText` ile (BOM'suz) yazılır.
- Pester 3.4 sözdizimi: `Should Be`, `Should Match`.
- C# stili DesyncDetector gibi: Türkçe, seyrek, sadece "neden" anlatan yorum.
- Çalışma ağacında kullanıcının bu işle ilgisiz commit edilmemiş değişiklikleri var (sahneler, prefab, ProjectSettings, font). **Asla `git add -A` / `git add .` yok**; her commit sadece o task'ın dosyalarını adıyla ekler.
- Kapsam dışı: oyun hatası düzeltmek. Test gerçek bir desync bulursa raporlanır, düzeltilmez.
- Ön koşul: Windows oturumu açık (kilitli ekranda Play Mode ilerlemeyebilir).

## Review Focus

1. **Kalmış config:** Önceki koşu yarıda kalırsa `config.json` Temp'te kalır ve kullanıcının sonraki elle Play'i otopilotla açılır. Beklenen: runner config'i `finally` içinde siler; otopilot açılırken console'a `[AUTOTEST]` uyarısı basar. → Task 3 (uyarı), Task 4 (timeout yolu da `finally`'den geçer).
2. **Editör kapalı / erişilemez:** 4 editörden biri açık değilse runner hiçbir editörü Play'e almadan hangi proje olduğunu söyleyip exit 1 vermeli. → Task 4.
3. **Kullanıcının kendi Play'i açık:** Bir editör zaten Play'deyse runner onu durdurmamalı, reddedip exit 1 vermeli. → Task 4.
4. **Paylaşılan Assets'te derleme hatası:** Runner Play'e girmeden ilk `error CS` satırını basıp exit 1 vermeli, beklemede kalmamalı. → Task 4.
5. **Yarım yazılmış status okuma:** Runner, client yazarken `status.json` okursa parse hatasıyla FAIL vermemeli. Beklenen: client atomik yazar (tmp + replace), runner parse hatasını "bu tur yok" sayıp tekrar okur. → Task 1 (atomik yazma testi), Task 4 (`Read-SyncStatus` parse hatasında `$null`).

---

### Task 0: Mevcut DesyncDetector işini baz commit yap (KULLANICI ONAYI GEREKİR)

`DesyncDetector.cs` henüz commit edilmemiş, `TileDistrubite/TurnManager/GameManager` içindeki çağrıları da öyle. Task 2 bu dosyayı değiştirecek; ayrı bir baz commit olmadan Task 2'nin commit'i kullanıcının işini de içine alır.

**Files:** `Assets/Osman/Scripts/GamePlay/DesyncDetector.cs`, `DesyncDetector.cs.meta`, `Assets/Osman/Scripts/GamePlay/GameManager.cs`, `Assets/Osman/Scripts/GamePlay/TurnManager.cs`, `Assets/Osman/Scripts/TILES/TileDistrubite.cs`

- [ ] **Step 1:** Kullanıcıdan onay al. Onay yoksa Task 0'ı atla ve sonraki task'larda bu dosyalar için commit yapma (sadece kendi yeni dosyalarını commit et).
- [ ] **Step 2:** `git diff -- Assets/Osman/Scripts/GamePlay/GameManager.cs Assets/Osman/Scripts/GamePlay/TurnManager.cs Assets/Osman/Scripts/TILES/TileDistrubite.cs` → sadece `DesyncDetector.Report`, `NextDealSeed`/`Random.InitState` ve `AppendSyncState` hunk'ları olmalı. Başka hunk varsa dur, kullanıcıya sor.
- [ ] **Step 3:** Bu 5 dosyayı adıyla `git add`; `git diff --cached --stat` sadece 5 dosya göstermeli.
- [ ] **Step 4:** Commit: `Add DesyncDetector snapshot compare for multi-client sync`

---

### Task 1: Config / status veri katmanı

**Files:**
- Create: `Assets/Osman/Scripts/GamePlay/SyncAutoTestData.cs`
- Test: `Assets/Osman/Editor/Tests/SyncAutoTestDataTests.cs` (Assembly-CSharp-Editor; NUnit referansı zaten var)

**Interfaces:**
- Produces:
  - `[Serializable] public class SyncAutoTestConfig { public string runId, role, roomName, nickName; public int deals = 1, seed, injectDesyncAtMove; public float moveDelaySec = 0.3f; public bool IsHost => role == "host"; }`
  - `[Serializable] public class SyncCounts { public int ok, desync; public string first; }`
  - `[Serializable] public class SyncAutoTestStatus { public string runId, role, nickName, state; public int actor, que, deal, myMoves; public List<string> errors = new List<string>(); public string desyncLog; public SyncCounts sync = new SyncCounts(); }`
  - `public static class SyncAutoTestFiles { public static string Dir(string projectRoot); public static bool TryLoadConfig(string projectRoot, out SyncAutoTestConfig config); public static void WriteStatus(string projectRoot, SyncAutoTestStatus status); }`
  - Dosya adları: `Dir` = `<root>/Temp/SyncAutoTest`, config = `config.json`, status = `status.json`.

- [ ] **Step 1: Failing testleri yaz** — `SyncAutoTestDataTests`, her test `Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())` altında sahte proje kökü kurar, `TearDown`'da siler:
  - `TryLoadConfig_ReadsRunnerJson`: `config.json` = `{"runId":"r1","role":"guest","roomName":"autotest_r1","nickName":"AT-g1","deals":2,"seed":7,"injectDesyncAtMove":5,"moveDelaySec":0.3}` → `true`; `runId=="r1"`, `nickName=="AT-g1"`, `deals==2`, `seed==7`, `injectDesyncAtMove==5`, `IsHost==false`.
  - `TryLoadConfig_MissingFile_ReturnsFalse` → `false`.
  - `TryLoadConfig_Malformed_ReturnsFalse`: içerik `{not json` → `false`, exception yok.
  - `TryLoadConfig_EmptyRunId_ReturnsFalse`: içerik `{}` → `false`.
  - `WriteStatus_ReplacesWholeFile`: önce `errors={"a","b"}`, `state="Playing"` yaz; sonra `state="Done"`, `errors` boş yaz → `JsonUtility.FromJson<SyncAutoTestStatus>(File.ReadAllText(...))` ile `state=="Done"`, `errors.Count==0`; `Dir` içinde `.tmp` dosyası kalmamış.
- [ ] **Step 2: Derle ve testin başarısız olduğunu gör**
  Run: `unity command editor invoke_menu --path "Assets/Refresh" --project-path C:\Users\osman\Documents\51Okey --result-only --no-banner`, sonra `script recompile`, `script recompile_status` → `completed`. `compilationFailed` true olmalı (tipler yok) — beklenen kırmızı.
- [ ] **Step 3: `SyncAutoTestData.cs`'i yaz.** `TryLoadConfig`: dosya yoksa / `JsonUtility` exception atarsa / sonuç null ya da `runId` boşsa `false`. `WriteStatus`: `Dir`'i oluştur, `status.json.tmp`'ye yaz, hedef varsa `File.Replace`, yoksa `File.Move`.
- [ ] **Step 4: Testleri koş**
  Run: Refresh + recompile (Step 2 gibi), sonra `unity command test run --mode EditMode --filter SyncAutoTestDataTests --project-path C:\Users\osman\Documents\51Okey --result-only --no-banner`
  Expected: 5 passed, 0 failed.
- [ ] **Step 5: Commit** — `git add` iki `.cs` ve iki `.cs.meta` + `Assets/Osman/Editor/Tests.meta` (yeni klasör meta'sı); mesaj: `Add SyncAutoTest config/status files`

---

### Task 2: DesyncDetector — sayaçlar, log yolu, el başına seed

**Files:**
- Modify: `Assets/Osman/Scripts/GamePlay/DesyncDetector.cs`
- Test: `Assets/Osman/Editor/Tests/SyncAutoTestDataTests.cs` (aynı dosyaya bir test eklenir)

**Interfaces:**
- Produces (DesyncDetector instance): `public int OkCount, DesyncCount; public string FirstDesync; public string LogPath { get; }` (log dosyası açılmadıysa null). Statik: `public static int SeedForDeal(int forcedSeed, int dealIndex)` → `forcedSeed + dealIndex - 1` (`dealIndex` 1'den başlar).
- Davranış: `OkCount++` `[SYNC OK]` basılan yerde; `DesyncCount++` ve ilk mesajı `FirstDesync`'e `Compare` fark bulduğunda; üçü ve seed sayacı `OnJoinedRoom`'da sıfırlanır. `NextDealSeed`: `forcedSeed != 0` ise `SeedForDeal(forcedSeed, ++seededDeals)` döner, değilse bugünkü gibi `Environment.TickCount`.

- [ ] **Step 1: Failing test** — `SeedForDeal_StepsPerDeal`: `SeedForDeal(42, 1) == 42`, `SeedForDeal(42, 2) == 43`.
- [ ] **Step 2:** Refresh + recompile → `compilationFailed` true (metot yok).
- [ ] **Step 3:** Yukarıdaki alanları/metodu ekle. Başka davranış değişmez.
- [ ] **Step 4:** `test run --mode EditMode --filter SyncAutoTestDataTests` → 6 passed, 0 failed.
- [ ] **Step 5: Commit** — sadece `DesyncDetector.cs` ve test dosyası (Task 0 atlandıysa `DesyncDetector.cs`'i commit etme, sadece test dosyasını); mesaj: `DesyncDetector: expose counters and per-deal seed`

---

### Task 3: `SyncAutoTest` — açılış, odaya giriş, durum yazma

**Files:**
- Create: `Assets/Osman/Scripts/GamePlay/SyncAutoTest.cs`

**Interfaces:**
- Consumes: Task 1 tipleri; Task 2 `DesyncDetector.Instance.OkCount/DesyncCount/FirstDesync/LogPath/forcedSeed`; mevcut `RoomManager.CreateCustomRoom(string, string, int, bool, bool)`, `RoomManager.JoinRoomWithPassword(string, string, string)`, `SceneChangeManager.Instance.ChangeScene(string)`, `GameManager.Instance.isGameReady/isGameEnded`.
- Produces: `public class SyncAutoTest : MonoBehaviourPunCallbacks`; `public static SyncAutoTest Instance`; `public enum State { Connecting, Lobby, Joining, InTable, Playing, Done, Error }`; `public SyncAutoTestConfig Config`; `protected void Fail(string reason)` (state=Error, sebebi errors'a ekler, status yazar); `int MyQue` (LocalPlayer `PlayerQue`, yoksa 0); `int myMoves`, `int deal` alanları; Task 5'in dolduracağı boş hook: `void TickPlaying()` (her frame `Playing` durumunda çağrılır).

- [ ] **Step 1: Açılış kapısı.** `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot()`: `!Application.isEditor` → çık; `Instance != null` → çık; `TryLoadConfig` false → çık. Aksi halde `DontDestroyOnLoad` obje `"SyncAutoTest"`, `Debug.LogWarning($"[AUTOTEST] config bulundu, otopilot aktif: {runId} {role} {nickName}")`.
- [ ] **Step 2: Durum makinesi** (`Update` içinde, her geçişte `WriteStatus`):
  - `Connecting` → `PhotonNetwork.InLobby` olunca `PhotonNetwork.NickName = nickName`, `ChangeScene("LobbyMenu")` → `Lobby`.
  - `Lobby` → `FindObjectOfType<RoomManager>()` bulununca host `CreateCustomRoom(roomName, runId, deals, true, false)`, guest `JoinRoomWithPassword(roomName, runId, runId)` → `Joining`.
  - `OnJoinRoomFailed`: guest 1 sn sonra tekrar dener. `OnCreateRoomFailed`: `Fail("CreateRoom: " + message)`.
  - `Joining` → aktif sahne `"Table"` ve LocalPlayer'da `"SeatNumber"` var → `InTable`. Host burada `cfg.seed != 0` ise `DesyncDetector.Instance.forcedSeed = cfg.seed`.
  - `InTable/Playing`: `isGameReady` false→true geçişinde `deal++`, `Playing`; `Playing`'de `TickPlaying()`.
  - `OnLeftRoom`: `deal >= cfg.deals` ise `Done`, değilse `Fail("odadan düştü")`.
- [ ] **Step 3: Log yakalama.** `Application.logMessageReceived`: `Error/Exception/Assert` → `errors`'a `mesaj + stack'in ilk satırı`, en fazla 30; `[DESYNC]` ile başlayanlar hariç. Errors değişince status yazılır. Host'ta her yazımda `sync` ve `desyncLog` DesyncDetector'dan doldurulur.
- [ ] **Step 4: Kapı kontrolü (config yokken)**
  Refresh + recompile ana editörde, `compilationFailed` false. `editor.playmode start` → 5 sn sonra `script eval --code "UnityEngine.GameObject.Find(\"SyncAutoTest\") == null"` → `true` → `editor.playmode stop`.
- [ ] **Step 5: Host tek başına odaya giriyor mu**
  Ana projede `Temp\SyncAutoTest\config.json` = host config (`runId` = o anki zaman, `deals` 1). `playmode start` → `status.json` 60 sn içinde `state == "InTable"`, `errors` boş, console'da `[AUTOTEST]` uyarısı var. `playmode stop`, config'i sil.
- [ ] **Step 6: Commit** — `SyncAutoTest.cs` + `.meta`; mesaj: `Add SyncAutoTest room join and status reporting`

---

### Task 4: Runner + kütüphane

**Files:**
- Create: `Tools/SyncTest/SyncTestLib.ps1`
- Create: `Tools/SyncTest/SyncTestLib.Tests.ps1`
- Create: `Tools/SyncTest/Run-SyncTest.ps1`

**Interfaces:**
- Consumes: Task 1 dosya yolları/alanları, Task 3 state adları.
- Produces (`SyncTestLib.ps1`, dot-source edilir):
  - `Invoke-UnityCmd -Project <string> -Arguments <string[]> [-Retries 5]` → `--result-only` JSON'unu parse edilmiş nesne olarak döner; "No Pipeline instance found" gelirse 2 sn arayla tekrar dener; diğer hatalarda throw.
  - `Read-SyncStatus -Project <string>` → `status.json` nesnesi; dosya yoksa veya parse hatasında `$null`.
  - `Get-SyncTestVerdict -Statuses <object[]> -Nicks <string[]> -TimedOut <bool>` → `[pscustomobject]@{ Pass = <bool>; Reason = <string> }`. Öncelik: (1) eksik status (`$null`) → `"<nick>: status yok"`; (2) host `sync.desync > 0` → `"DESYNC: <sync.first>"`; (3) `state == "Error"` → `"<nick>: Error: <errors[0]>"`; (4) `TimedOut` → ilk `Done` olmayan için `"zaman aşımı: <nick> <state> (deal <d>, moves <m>)"`; (5) boş olmayan `errors` → `"<nick>: <errors[0]>"`; yoksa PASS, `"<deal> el, ok <sync.ok>, desync 0"`.
- `Run-SyncTest.ps1` parametreleri: `-Deals 1 -Seed 0 -TimeoutSec 600 -InjectDesync 0 -KeepPlaying -Projects <4 yol, varsayılan Global Constraints'teki>`.

- [ ] **Step 1: Failing Pester testleri** (`SyncTestLib.Tests.ps1`, `. $PSScriptRoot\SyncTestLib.ps1`):
  - hepsi `Done`, host `sync.desync 0`, errors boş → `Pass | Should Be $true`
  - host `sync.desync 1`, `first "x"` → `Pass` false, `Reason | Should Match 'DESYNC'`
  - AT-g2 `state "Error"`, `errors @("boom")` → `Reason | Should Match 'AT-g2'` ve `'boom'`
  - `TimedOut $true`, AT-g3 `state "Playing"` → `Reason | Should Match 'AT-g3.*Playing'`
  - AT-g1 status `$null` → `Reason | Should Match 'AT-g1'`
  - host desync 1 **ve** AT-g1 Error → `Reason | Should Match 'DESYNC'` (öncelik)
  - `Read-SyncStatus`: `$TestDrive\p\Temp\SyncAutoTest\status.json` = `{"state":` (yarım yazılmış) → `$null`, exception yok; geçerli JSON → `state` okunur (Review Focus 5)
- [ ] **Step 2:** `Invoke-Pester -Script C:\Users\osman\Documents\51Okey\Tools\SyncTest\SyncTestLib.Tests.ps1` → başarısız (fonksiyon yok).
- [ ] **Step 3:** `SyncTestLib.ps1`'i yaz → Pester: 7 passed, 0 failed.
- [ ] **Step 4: `Run-SyncTest.ps1`'i yaz** — spec "Runner" adımları sırasıyla:
  1. Ön kontrol: her proje için `editor status`; hata → `"<proje>: erişilemiyor (<mesaj>)"`, `playMode == "playing"` → `"<proje>: zaten Play'de"`. Sorun varsa hepsini bas, **hiçbir editörü Play'e almadan** exit 1.
  2. Her editörde `editor invoke_menu --path "Assets/Refresh"`, `script recompile`, `script recompile_status` `completed|up_to_date` olana kadar (en fazla 180 sn), sonra `console status` → `groundTruth.compilationFailed` true ise `console get --level error --tail 5`'in ilk mesajını bas, exit 1.
  3. `editor set_auto_tick --enabled true`, `console clear`; her projede `Temp\SyncAutoTest\` içeriğini sil, `config.json` yaz (`-InjectDesync` sadece AT-g1'e).
  4. `try { ... } finally { ... }`: host `editor.playmode start` → host `InTable` (60 sn) → her guest sırayla `playmode start` → o guest `InTable` (60 sn). `InTable` gelmezse timeout yolu.
  5. 2 sn'de bir 4 status oku, tek satır ilerleme bas (`deal 1 · moves 12/9/9/8 · ok 31 desync 0`); hepsi `Done` / biri `Error` / `TimeoutSec` → döngüden çık.
  6. `finally`: `-KeepPlaying` yoksa 4 editörde `editor.playmode stop`; 4 `config.json`'ı sil.
  7. Her editörden `console get --level error --tail 20`; boş değilse ilk 3'ünü bas. `Get-SyncTestVerdict` → `PASS`/`FAIL: <Reason>`, desync log yolları, exit 0/1.
- [ ] **Step 5: Review Focus 2** — `-Projects` içinde bir yolu `C:\Users\osman\Documents\YokBoyleProje` yap → exit 1, mesajda bu yol; hiçbir editör Play'de değil (`editor status`).
- [ ] **Step 6: Review Focus 3** — `clone_2`'de `editor.playmode start`, runner'ı koş → exit 1, `51Okey_clone_2: zaten Play'de`; clone_2 hâlâ Play'de (runner durdurmadı). Sonra clone_2'yi durdur.
- [ ] **Step 7: Review Focus 4** — `Assets\Osman\Scripts\__BrokenProbe.cs` = `class __BrokenProbe {` → runner exit 1, çıktıda `error CS`, hiçbir editör Play'de değil. Dosyayı ve `.meta`'sını sil, Refresh.
- [ ] **Step 8: Uçtan uca iskelet (otopilot yokken)** — `Run-SyncTest.ps1 -TimeoutSec 60` → 4 client `InTable` olur, oyun dağıtılır ama kimse oynamaz → `FAIL: zaman aşımı: ...`, exit 1; bitince 4 editör Play'de değil ve hiçbir `Temp\SyncAutoTest\config.json` yok (Review Focus 1).
- [ ] **Step 9: Commit** — üç `.ps1`; mesaj: `Add Run-SyncTest runner for 4-editor sync test`

---

### Task 5: Otopilot — çek, at, el sonu, desync enjeksiyonu

**Files:**
- Modify: `Assets/Osman/Scripts/GamePlay/SyncAutoTest.cs`

**Interfaces:**
- Consumes: Task 3 `TickPlaying`, `Fail`, `MyQue`, `myMoves`, `Config`; mevcut `TileUI.OnBeginDrag/OnEndDrag(PointerEventData)`, `TileUI.tileDataInfo`, `Placeholder.isRight/isMeldArea`, `TurnManager.IsPlayerTurn()/canDrop/currentTurnPlayer`, `ScoreManager.FindBestTileToDiscardForBot(int)`, `TileDistrubite.allTiles`.
- Produces: `bool DragTo(TileUI tile, Transform target)` (Aşama B de kullanacak) — `OnBeginDrag` hamleyi reddederse (`e.pointerDrag == null`) `false`.

- [ ] **Step 1: `DragTo`** — spec "Sürükle simülasyonu": `PointerEventData(EventSystem.current)`, `position` = taşın ekran noktası (Overlay canvas'ta kamera `null`), `pointerDrag = tile.gameObject` → `OnBeginDrag` → reddedildiyse `false` → `tile.transform.position = target.position` → `OnEndDrag`.
- [ ] **Step 2: `TickPlaying` hamle coroutine'i** (aynı anda tek hamle; `GameManager.isGameReady && !isGameEnded && turnManager.IsPlayerTurn()`):
  - `!canDrop` ise çek: `MiddleTileContainer` altındaki üst `TileUI` → `PlayerTileContainer` altında `childCount == 0` olan, `isRight`/`isMeldArea` olmayan ilk `Placeholder`. Aynı TileUI slota oturana ve `canDrop` true olana kadar bekle; 5 sn → `Fail("çekme zaman aşımı")`. `DragTo` false → `Fail("çekme reddedildi")`.
  - `moveDelaySec` bekle.
  - At: `FindBestTileToDiscardForBot(MyQue)` id'li, `PlayerTileContainer` altındaki `TileUI` → `isRight` `Placeholder`. `currentTurnPlayer != MyQue` veya `isGameEnded` olana kadar bekle; 5 sn → `Fail("atma zaman aşımı")`. Taş bulunamazsa / `DragTo` false → `Fail`.
  - `myMoves++`, status yaz.
  - Enjeksiyon: `injectDesyncAtMove > 0 && myMoves == injectDesyncAtMove` → `tileDistrubite.allTiles.RemoveAt(allTiles.Count - 1)`, `Debug.LogWarning("[AUTOTEST] desync enjekte edildi")`.
  - "Üst taş" hangi child — Play'de `script eval` ile `MiddleTileContainer` çocuklarını sayarak doğrula, karar kodda tek satır yorumla.
- [ ] **Step 3: Refresh + recompile** 4 editörde (runner bunu zaten yapıyor; derleme hatası varsa runner exit 1 verir).
- [ ] **Step 4: Kabul 1** — `Run-SyncTest.ps1 -Deals 1` → 180 sn içinde biter. Beklenen `PASS`. `FAIL` ise sebep harness kaynaklı olmamalı (`zaman aşımı` / `reddedildi` / `status yok` değil); gerçek bir oyun desync'i/hatasıysa düzeltme yapma, sebebi ve desync log yolunu not et.
- [ ] **Step 5: Kabul 2** — `Run-SyncTest.ps1 -Deals 1 -InjectDesync 5` → `FAIL`, sebepte `DESYNC` ve `AT-g1`.
- [ ] **Step 6: Kabul 3** — `Run-SyncTest.ps1 -Deals 2 -Seed 1234` → host status `deal == 2`; host desync log'unda `SEED 1234` ve `SEED 1235` satırları.
- [ ] **Step 7: Kapı tekrar** — 4 projede `Temp\SyncAutoTest\config.json` yok; ana editörde config'siz Play → `GameObject.Find("SyncAutoTest") == null`.
- [ ] **Step 8: Commit** — `SyncAutoTest.cs`; mesaj: `SyncAutoTest: autopilot draw/discard via TileUI drag`
