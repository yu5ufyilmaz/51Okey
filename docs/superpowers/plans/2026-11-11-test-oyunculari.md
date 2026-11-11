# Test Oyuncuları Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `Run-SyncTest.ps1 -Mode smart` ile 4 editörde akıllı test oyuncuları oynasın (diz, aç, işle, yandan al, bit), istenince bilerek hata yapsın, her hamleyi günlüğe yazsın ve bağımsız beklenen sonuçla karşılaştırıp farkı `[BUG]` olarak raporlasın.

**Architecture:** Saf C# katman (Unity'siz, EditMode test edilir): `HandSolver` (seri/çift bölme), `RackLayout` (ıstaka yerleşimi), `RuleOracle` (kural kitabından bağımsız beklenen sonuç), `TestPlayerBrain` (karar + hata modu). Unity katmanı: `SyncAutoTest` içinde `smart` sürücü (sahneden `TableView` kurar, planı insan kod yolundan uygular, gözler, karşılaştırır, `AutoTestJournal`'a yazar). Runner yeni parametreler ve özet/alıntı alır.

**Tech Stack:** Unity 6000.3.10f1, C# (Assembly-CSharp, asmdef yok), NUnit EditMode, Photon PUN 2, PowerShell 5.1, Pester 3.4, `unity` CLI.

**Spec:** `docs/superpowers/specs/2026-11-11-test-oyunculari-design.md` (kurallar: `docs/superpowers/specs/2026-11-11-okey-kurallari.md`, "Kullanıcı kararları" bölümü bağlayıcı)

## Global Constraints

- Config yokken oyun davranışı aynı. Oyun koduna tek ekleme: `GameManager.HandResult` statik olayı (abonesi yoksa etkisiz).
- Sürücü RPC çağırmaz; hamleler: `TileUI.OnBeginDrag` → `Placeholder.OnDrop` (`ExecuteEvents.dropHandler`) → `TileUI.OnEndDrag`; butonlar `ExitButton.MeldTileButton/MeldPairTileButton/ActiveTilesButton`, `SidePickUIManager.OnCancelButtonClicked`.
- `RuleOracle` oyunun `OkeyRuleEngine`, `PenaltySystem`, `GameManager.DiscardPenalty` fonksiyonlarını **çağırmaz**; kural kitabı + kullanıcı kararlarından ayrı yazılır.
- Beyin `System.Random(seed)` kullanır, `UnityEngine.Random` değil. Seed = config `seed` (0 ise runId'den türet) + que.
- Beyin ve çözücü girdiyi **değiştirmez**: `Tiles` nesnelerini kopyalar (oyunun `OkeyRuleEngine.CheckPattern`'i okey taşlarının sayı/rengini yerinde değiştirir).
- Okey = `TileType.Joker`. Sahte okey (`FakeJoker`) = okeyin renk/sayısını taşıyan normal taş. Gösterge kopyası = gösterge ile aynı renk+sayıdaki `Number` taş: seride normal taş, çiftte her taşla eşleşir ve gösterge sayısı × 2 sayılır.
- Kurallar (oracle): ilk açan ≥51, sonra > masa limiti; açmış oyuncuya limit yok; sadece ilk açılış limiti yükseltir (yeni limit = açılış puanı, eğer ≥ mevcut limit); okey/işlek atmak 250, gösterge kopyası atmak sadece masada çift açılmışsa 250, bitiren atış cezasız; yandan alıp açmadan/işlemeden atmaya çalışmak tek 250 (taş yana döner, ortadan 1 taş); geri bırak cezasız; yandan alınan taş için önceki oyuncuya sayı × 10 (tur açarak veya işleyerek tamamlanırsa, atışta); başkasının perine işleme per sahibine sayı × 10 (gösterge kopyası × 20), atışta; uç başına turda en fazla 2 işleme; el sonu: kazanan −100 × çarpan; açmayan 600; açan kalan normal taş × 10 × çarpan (çift açtıysa ×2) + okey başına 250; çarpanlar sahte okey 8, sarı 6, kırmızı 5, siyah 4, mavi 3; özel bitiş (çiftten / okey atarak / kafa) kaybedenleri ×2'şer.
- Kafa = masada başka kimse açmamışken aynı turda ilk açılış + bitiş.
- Istaka: `PlayerTileContainer` çocukları 0–29 slot (satır 0–14, 15–29), 30. çocuk `RightTileContainer`. Grup = satırda bitişik dolu slotlar.
- Çalışma ağacında kullanıcının ilgisiz değişiklikleri var: **asla `git add -A`**, dosyaları adıyla ekle.
- PowerShell 5.1 kuralları ve `-ExecutionPolicy Bypass`; JSON BOM'suz; `.ps1` dosyaları UTF-8 BOM.
- Kabul koşuları `-Deals 1` (2. el host donma hatası açık).

## Review Focus

1. **Okey mutasyonu:** Çözücü oyunun `Tiles` nesnelerini değiştirirse ellerin kopyaları client'lar arasında ayrışır (sahte desync). Beklenen: girdi değişmez. → Task 1 testi `Solver_DoesNotMutateInput`.
2. **Istakaya sığmayan dizilim:** 15 taşlık el, uzun seriler ve aralar 2×15'e sığmayabilir. Beklenen: `RackLayout` sığdıramazsa `null` döner, sürücü o tur dizmeden oynar ve bunu günlüğe "sürücü" notu olarak yazar (`[BUG]` değil). → Task 2 testi `Layout_ReturnsNullWhenItCannotFit`.
3. **Gözlem zamanlaması:** Cezalar master RPC'siyle geç gelir; erken okunursa sahte `[BUG]`. Beklenen: sürücü beklenen değer gelene ya da 3 sn dolana kadar bekler, sonra karşılaştırır. → Task 6 canlı adımı (aynı seed iki kez → aynı `[BUG]` listesi).
4. **15 taşla başlayan oyuncu / deste biterken:** İlk turda çekmez; deste bitince el beraberlikle biter. Beklenen: beyin çekme adımı üretmez; el sonu oracle'ı kazanansız hesaplar. → Task 4 testi `Brain_FirstPlayerDoesNotDraw`, Task 3 testi `EndResults_DrawHasNoWinner`.
5. **Hata sonrası devam:** Bilerek hata yapan oyuncu sonra normal oynamalı; oyun kilitlenmemeli. → Task 7 canlı `-Mistakes 0.3` koşusu bitmeli.

---

### Task 1: EditMode test koşucusu + `HandSolver`

**Files:**
- Create: `Tools/SyncTest/Run-EditModeTests.ps1` (ana editörde Refresh + recompile + `test run`; derleme hatasında ilk 8 hatayı, test hatasında adları basar; exit 0/1). Parametre `-Filter` (sınıf adı).
- Create: `Assets/Osman/Scripts/GamePlay/AutoTest/HandSolver.cs`
- Test: `Assets/Osman/Editor/Tests/HandSolverTests.cs`

**Interfaces — Produces:**
- `public class MeldSet { public List<List<Tiles>> Melds; public int Score; }`
- `public static class HandSolver`
  - `public static MeldSet BestSeries(IList<Tiles> hand, Tiles indicator)` — ayrık geçerli seri (tek renk ardışık ≥3, 13→1 yok) ve grup (3–4 farklı renk aynı sayı) seçimi, toplam puan en yüksek; okey boşluk doldurur ve yerine geçtiği sayıyı sayar.
  - `public static MeldSet BestPairs(IList<Tiles> hand, Tiles indicator)` — ayrık çiftler: aynı renk+sayı (sayı×2), okey+taş (taş×2), gösterge kopyası+taş (gösterge×2), iki okey (okey sayısı×2); puanı yüksek olanlar.
  - `public static List<Tiles> Copy(IEnumerable<Tiles> tiles)` — id korunarak derin kopya.
- Dönen perlerdeki taşlar girdideki **aynı id**'leri taşır (sürücü id ile eşler).

- [ ] **Step 1:** `Run-EditModeTests.ps1`'i yaz (scratchpad `utest.ps1` ile aynı mantık; ana proje sabit). `-Filter OkeyRulesTests` → `passed 16/16`, exit 0.
- [ ] **Step 2: Failing testler** (`HandSolverTests`; yardımcı `T(c,n)`, `Okey()`):
  - `Series_PicksBestDisjointMelds`: el `r1 r2 r3 r4 b4 k4 y9` → 2 per `r1 r2 r3` + `r4 b4 k4`, skor 18 (tek `r2 r3 r4` = 9'dan iyi; r4 iki perde kullanılamaz).
  - `Series_OkeyFillsGap`: `r6 J r8` → 1 per, skor 21.
  - `Series_NoWrap`: `r12 r13 r1` → 0 per.
  - `Series_IndicatorCopyIsNormal`: gösterge `b5`; `r4 b5 r6` → 0 per.
  - `Pairs_ScoreRules`: gösterge `b5`; `r6 r6 y9 y9 k13 b5` → 3 çift, skor 12+18+10 = 40.
  - `Pairs_OkeyTakesPartner`: `r6 J` → 12.
  - `Solver_DoesNotMutateInput`: okeyli el çözülünce girdideki okeyin `number/color` aynı kalır.
- [ ] **Step 3:** `Run-EditModeTests.ps1 -Filter HandSolverTests` → derleme hatası (tip yok).
- [ ] **Step 4:** `HandSolver`'ı yaz (≤15 taş: aday perleri üret + geri izleme ile ayrık en iyi seçim).
- [ ] **Step 5:** `-Filter HandSolverTests` → 7/7; `-Filter OkeyRulesTests` → 16/16.
- [ ] **Step 6: Commit** — 2 `.cs` + `.meta`'lar + `AutoTest` klasör meta + `Run-EditModeTests.ps1`: `Add HandSolver for test players`

---

### Task 2: `RackLayout`

**Files:** Create `Assets/Osman/Scripts/GamePlay/AutoTest/RackLayout.cs`; Test `Assets/Osman/Editor/Tests/RackLayoutTests.cs`

**Interfaces — Produces:** `public static class RackLayout { public const int RowSize = 15; public static Dictionary<string, int> Place(List<List<Tiles>> groups, List<Tiles> rest); }` — taş id → slot (0–29). Her grup tek satırda bitişik; gruplar ve "rest" bloğu arasında ≥1 boş slot; `rest` taşları birbirine **bitişik olmayacak** şekilde (araya boşluk) ya da sığmazsa bitişik ama perlerden ayrı satır kısmında. Sığmazsa `null`.

- [ ] **Step 1: Failing testler:** `Layout_GroupsAreContiguousAndSeparated` (2 per + 3 taş → per taşları ardışık slotlarda, aralarında boş slot, satır sınırını aşmıyor); `Layout_RestDoesNotFormAccidentalGroup` (rest taşları arasında boşluk varsa korunur); `Layout_ReturnsNullWhenItCannotFit` (15 taşlık iki uzun seri + rest sığmıyorsa `null`).
- [ ] **Step 2:** `-Filter RackLayoutTests` → derleme hatası.
- [ ] **Step 3:** `RackLayout.Place`'i yaz.
- [ ] **Step 4:** `-Filter RackLayoutTests` → 3/3.
- [ ] **Step 5: Commit** `Add RackLayout for test players`

---

### Task 3: `RuleOracle`

**Files:** Create `Assets/Osman/Scripts/GamePlay/AutoTest/RuleOracle.cs`; Test `Assets/Osman/Editor/Tests/RuleOracleTests.cs`

**Interfaces — Produces:** `public static class RuleOracle`:
- `int DiscardPenalty(Tiles thrown, Tiles indicator, bool pairsOpenedOnTable, IEnumerable<Tiles> islek, bool finishing)`
- `int ProcessPenalty(Tiles processed, int representedNumber, Tiles indicator, bool ontoOwnMeld)` — kendi perine 0; gösterge kopyası × 20; diğer `representedNumber` × 10. Okey işlenirse `representedNumber` = yerine geçtiği sayı (kitap: "işlenen sayının 10 katı"; Claude'un kararı — oyun okeyin kayıtlı sayısını kullanıyor, fark çıkarsa `[BUG]` olarak raporlanır).
- `int SidePickPenalty(int tileNumber)` → × 10.
- `bool OpenAllowed(int score, int tableLimit, bool anyoneElseOpened, bool iHaveOpened)`
- `int NewTableLimit(int tableLimit, int openScore, bool firstOpen)`
- `int ColorMultiplier(Tiles indicator)` — sahte okey 8, sarı 6, kırmızı 5, siyah 4, mavi 3.
- `Dictionary<int,int> EndResults(EndState s)` — que → net (el içi cezalar dahil). `public class EndState { public int WinnerQue; /* -1 beraberlik */ public Tiles Indicator; public Dictionary<int,List<Tiles>> Hands; public HashSet<int> Opened, OpenedPairs; public bool OkeyFinish, Kafa; public Dictionary<int,int> InHandPenalties; }`

- [ ] **Step 1: Failing testler** (kural kitabı örnekleri + kullanıcı kararları): `Discard_*` (okey 250, işlek 250, gösterge kopyası çift yokken 0 / varken 250, sahte okey işlek değil, bitiren atış 0); `Process_*` (başkasına r7 → 70, gösterge 6 → 120, kendi perine 0); `Open_*` (51/51 ilk → true, 51/51 biri açtıysa → false, 62/61 → true, açmışa limit yok); `NewLimit_OnlyFirstOpen`; `EndResults_RulebookExamples` (kırmızı 3 taş+1 okey = 400; mavi çift 3 taş = 180; sarı kazanan −600; açmayan 600); `EndResults_FinishMultipliers` (okey+kafa = ×4 kaybedenlerde, kazanan değişmez); `EndResults_DrawHasNoWinner`.
- [ ] **Step 2:** `-Filter RuleOracleTests` → derleme hatası.
- [ ] **Step 3:** `RuleOracle`'ı yaz (oyun fonksiyonlarını çağırmadan).
- [ ] **Step 4:** `-Filter RuleOracleTests` → hepsi yeşil.
- [ ] **Step 5: Commit** `Add independent RuleOracle for test players`

---

### Task 4: `TestPlayerBrain`

**Files:** Create `Assets/Osman/Scripts/GamePlay/AutoTest/TestPlayerBrain.cs`; Test `Assets/Osman/Editor/Tests/TestPlayerBrainTests.cs`

**Interfaces:**
- Consumes: `HandSolver`, `RackLayout`, `RuleOracle`.
- Produces:
  - `public class TableView { public int MyQue; public List<Tiles> Hand; public Tiles Indicator; public Tiles SideTile; public List<SlotView> Slots; public int TableLimit; public bool AnyoneElseOpened, IOpenedSeries, IOpenedPairs, PairsOpenedOnTable, CanDrop, PickedFromSide; public int PileCount; }`
  - `public class SlotView { public string Key; public int OwnerQue; public int MeldType; /* 0 seri,1 grup,2 çift (ScoreManager.MeldType) */ public Tiles Needed; public bool JokerSwap; }`
  - `public enum StepKind { DrawMiddle, DrawSide, CancelSide, Arrange, OpenSeries, OpenPairs, Process, AutoProcess, Discard }`
  - `public enum Mistake { None, SidePickNoOpen, SidePickCancel, DiscardOkey, DiscardIndicator, DiscardIslek, OpenBelowLimit, ProcessBeforeOpen, ThirdOnSeriesEnd }`
  - `public class PlanStep { public StepKind Kind; public string TileId; public string SlotKey; public Dictionary<string,int> Layout; public string Reason; public Mistake Mistake; }`
  - `public class TestPlayerBrain { public TestPlayerBrain(int seed, double mistakeRate); public PlanStep PlanDraw(TableView v); public List<PlanStep> PlanAfterDraw(TableView v); public bool PathIsPairs { get; } public void NewDeal(); }`

- [ ] **Step 1: Failing testler:** `Brain_TakesSideTileWhenItEnablesOpening`; `Brain_DrawsMiddleOtherwise`; `Brain_FirstPlayerDoesNotDraw` (`CanDrop` true → `PlanDraw` null); `Brain_OpensWhenLimitPassesAndKeepsOneTile`; `Brain_KeepsChosenPath` (ilk turda çift seçildiyse sonraki turda seri planlamaz); `Brain_ProcessesIslekAfterOpening` (okeyi işlemez, 1 taş bırakır, uç başına 2); `Brain_NeverDiscardsOkeyOrIslekWhenAlternative`; `Brain_MistakesAreSeeded` (aynı seed + oran → aynı hata dizisi; oran 0 → hiç hata yok).
- [ ] **Step 2:** `-Filter TestPlayerBrainTests` → derleme hatası.
- [ ] **Step 3:** `TestPlayerBrain`'i yaz (tasarım §1 karar kuralları + hata kataloğu + kullanıcı kararlarına göre `SidePickNoOpen`/`SidePickCancel`).
- [ ] **Step 4:** `-Filter TestPlayerBrainTests` → yeşil; önceki 3 sınıf yeşil.
- [ ] **Step 5: Commit** `Add TestPlayerBrain decisions and mistake mode`

---

### Task 5: Günlük, sayaçlar, `HandResult`

**Files:**
- Create: `Assets/Osman/Scripts/GamePlay/AutoTest/AutoTestJournal.cs`
- Modify: `Assets/Osman/Scripts/GamePlay/DesyncDetector.cs` (`public static string CurrentKey` → `"{deal}.{move}"`, yoksa `"0.0"`)
- Modify: `Assets/Osman/Scripts/GamePlay/SyncAutoTestData.cs` (config: `public string mode = "simple"; public float mistakeRate; public int injectOracleBugAtMove;` · status: `public int opens, processes, sidePicks, mistakes, bugs; public string finishType;`)
- Modify: `Assets/Osman/Scripts/GamePlay/GameManager.cs` (`public static event System.Action<int[], int[], int[], int[]> HandResult;` — `OnGameEndedDetailedRPC` başında `HandResult?.Invoke(actors, rewards, penalties, netScores);`)
- Test: `Assets/Osman/Editor/Tests/SyncAutoTestDataTests.cs` (yeni alanların JSON round-trip'i) ve `AutoTestJournalTests.cs`

**Interfaces — Produces:** `public class AutoTestJournal { public AutoTestJournal(string path); public void Line(string key, string who, string text); public void Check(string key, string who, string what, string expected, string observed, bool ok); public string Path { get; } }` — satır biçimi `"{key} | {who} | {text}"`, kontrol satırı `"... | beklenen: {e} | gözlenen: {o} | OK"` veya `"| [BUG]"`.

- [ ] **Step 1: Failing testler:** `Config_ReadsSmartFields`, `Status_WritesCounters`, `Journal_FormatsCheckLine` (geçici dosyaya yazıp okur).
- [ ] **Step 2:** derleme hatası. **Step 3:** yaz. **Step 4:** ilgili sınıflar yeşil + önceki tüm sınıflar yeşil.
- [ ] **Step 5: Commit** `Add autotest journal, counters and HandResult hook`

---

### Task 6: Akıllı sürücü (`SyncAutoTest`, `mode == "smart"`)

**Files:** Modify `Assets/Osman/Scripts/GamePlay/SyncAutoTest.cs` (sürücü kodu uzunsa aynı sınıfı `SyncAutoTest.Smart.cs` partial dosyasına ayır — dosya bölme kararı builder'a)

**Interfaces — Consumes:** Task 1–5 tipleri; sahne: `PlayerTileContainer`, `RightTileContainer`, `MiddleTileContainer`, `LeftTileContainer` (tag, son çocuk = yandaki taş), meld konteynerleri `"<NickName> meld"` (çocuk sırası = `ScoreManager.MeldType`, slotlar `Placeholder`), `ExitButton`, `SidePickUIManager`, `ScoreManager` (`validPerss`, `hasOpenedSeries/Pairs`, `playersWhoOpened(Pairs)`), `GameManager` (`CurrentTableLimit`, `IsDoubleOpenedOnTable`, `HandResult`), `TileDistrubite` (`dropTile`, `availableTiles`, `GetPlayerTilesForBot`, `GetIndicatorTile`, `allTiles`), `PlayerScore` player property.

- [ ] **Step 1: `DragTo` güncelle:** `OnBeginDrag` → pozisyon → `ExecuteEvents.Execute(target.gameObject, e, ExecuteEvents.dropHandler)` → `OnEndDrag` (simple mod davranışı değişmemeli: simple koşu yine PASS).
- [ ] **Step 2: `BuildTableView()`** — sahne/oyun durumundan `TableView` (kopyalarla).
- [ ] **Step 3: Adım yürütücüleri:** her `StepKind` için sürücü + gözlem: `DrawSide` (yandaki taşı ıstakaya), `CancelSide`, `Arrange` (`RackLayout` slotlarına sırayla `DragTo`, sonra 0.5 sn + `validPerss` planla eşleşti mi — eşleşmezse "sürücü" notu, `[BUG]` değil), `Open*` (butonlar), `Process` (hedef slota `DragTo`), `AutoProcess` (yalnız tasarımdaki eşitlik koşulunda), `Discard`.
- [ ] **Step 4: Oracle karşılaştırma:** her adım önce `RuleOracle` ile beklenen (`PlayerScore` deltaları, el sayısı, açtı bayrağı, limit, kabul/red) → adımdan sonra beklenen gelene ya da 3 sn dolana kadar bekle → `journal.Check(...)`. `[BUG]` → `status.bugs++`, `status.errors`'a `"[BUG] {key} ..."` (koşu devam eder). Atış sonrası işleme/yandan alma cezaları kontrolü. `HandResult` gelince `RuleOracle.EndResults` ile her que karşılaştırılır.
- [ ] **Step 5: Config bağlantıları:** `mistakeRate` → beyin; `injectOracleBugAtMove` → o hamlede beklenen değer bilerek +1 (öz-test).
- [ ] **Step 6: Derleme + tüm EditMode sınıfları yeşil;** `Run-SyncTest.ps1 -Deals 1` (simple) PASS (davranış korunmuş).
- [ ] **Step 7: Commit** `Add smart test player driver`

---

### Task 7: Runner + kabul koşuları

**Files:** Modify `Tools/SyncTest/Run-SyncTest.ps1`, `Tools/SyncTest/SyncTestLib.ps1`, `Tools/SyncTest/SyncTestLib.Tests.ps1`

**Interfaces — Produces:** parametreler `-Mode simple|smart` (varsayılan simple), `-Mistakes 0..1`, `-InjectOracleBug N` → config'e; `Get-JournalExcerpt -Projects <4> -Key <"d.m">` → her client günlüğünden o anahtardaki satırlar; verdict: `errors` içindeki `[BUG]` FAIL sebebi olur (mevcut "yakalanan hata" kuralı); PASS özetine `opens/processes/sidePicks/mistakes/finishType`.

- [ ] **Step 1: Failing Pester testleri:** `Get-JournalExcerpt` (geçici günlüklerden anahtar satırları), verdict `[BUG]` → FAIL ve sebepte anahtar.
- [ ] **Step 2–3:** yaz, Pester yeşil (önceki 13 + yeniler).
- [ ] **Step 4: Kabul (canlı):**
  - `-Mode smart -Deals 1 -Seed 11`, `-Seed 22`, `-Seed 33` → hepsi biter; en az birinde `opens > 0` ve `processes > 0`. `[BUG]` çıkarsa günlük alıntısıyla gerçek oyun hatası mı sürücü mü ayır; gerçekse raporla, düzeltme.
  - Aynı seed iki kez → aynı `[BUG]` listesi (Review Focus 3).
  - `-Mode smart -Mistakes 0.3 -Seed 44` → biter, günlükte `[BİLEREK HATA]` satırları ve her biri için `beklenen/gözlenen`.
  - `-Mode smart -InjectOracleBug 5` → FAIL, sebepte `[BUG]` ve AT-g1.
  - `-Mode simple -Deals 1` → PASS.
- [ ] **Step 5: Commit** `Runner: smart mode, mistakes, journal excerpts`
