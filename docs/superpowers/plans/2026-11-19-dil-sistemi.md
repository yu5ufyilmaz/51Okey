# Dil Sistemi Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Oyuncuya görünen bütün metinleri tek bir CSV tablosuna taşımak; Türkçe ve İngilizce eksiksiz, yeni dil yalnızca yeni sütunla eklenebilir.

**Architecture:** Saf C# `LocTable` CSV'yi ayrıştırır; statik `Loc` seçili dili tutar ve `Loc.T(key, args)` verir; `LocText` bileşeni sabit etiketleri dil değişince yeniler. Editor builder'ları düz metin yerine anahtar alır. Ağdan giden ceza nedeni metinden `byte` enum'a döner.

**Tech Stack:** Unity 6000.3.10f1, uGUI 2.0 + TextMeshPro, Photon PUN 2, NUnit EditMode testleri.

**Spec:** `docs/superpowers/specs/2026-11-19-dil-sistemi-design.md`

## Global Constraints

- Tablo: `Assets/Resources/Localization.csv`, BOM'lu UTF-8, başlık `key,tr,en,note`.
- Anahtar biçimi `bölüm.ad`, küçük harf + rakam + alt çizgi. İzinli bölümler: `common`, `login`, `lobby`, `room`, `settings`, `profile`, `account`, `shop`, `table`, `penalty`, `toast`, `error`, `lang`.
- Kodda anahtar her zaman çift tırnaklı sabit metindir (`Loc.T("lobby.play_now")`); değişkenden veya birleştirmeyle kurulmaz. Denetim kaynak dosyaları tarar.
- Her taşınan metin için aynı görevde hem `tr` hem `en` hücresi doldurulur. Türkçe metin bugünkü metnin aynısıdır (masadaki eksik harfler hariç, Görev 8).
- Çevrilmez: `Misafir-` öneki, oyuncu ve masa adları, mağaza ürün adı ve fiyatları, `Debug.Log`/journal/test metinleri, `Assets/Osman/Scripts/GamePlay/AutoTest/`, `SyncAutoTest*`, `DesyncDetector`.
- Menü sahneleri builder'la üretilir; `MainMenu.unity` ve `LobbyMenu.unity` elle düzenlenmez, builder değişir ve menü yeniden çalıştırılır.
- Her görev tek commit, Türkçe mesaj, sonunda `Co-Authored-By: Claude <noreply@anthropic.com>`. Push yok.
- Commit'ten önce her zaman: `git checkout -- "Assets/Fonts/Changa-Bold SDF.asset" "Assets/Fonts/Changa-ExtraBold SDF.asset"`. Bu iki dosya asla commit edilmez.
- Kullanıcının kendi değişmiş dosyalarına dokunulmaz: `51Okey.slnx`, `Assets/GooglePlayGames/.../GooglePlayGamesPlugin_v2.2.1.txt`, `ProjectSettings/PackageManagerSettings.asset`, `ProjectSettings/UnityConnectSettings.asset`, `docs/superpowers/plans/2026-11-11-sync-autotest.md`.
- Unity CLI (U diye kısaltılır): `C:\Users\osman\AppData\Local\Unity\bin\unity.exe command <grup> <komut> --project-path F:/Documents/UnityProjects/51Okey --result-only --no-banner`.
  - Test: `U test run --mode EditMode --filter <SınıfAdı>`; tamamı: filtresiz.
  - Menü çalıştırma: `U script eval 'UnityEditor.EditorApplication.ExecuteMenuItem("Okey/UI Kit/Lobiyi Kur"); return "ok";'`
  - Derleme hatası: `U console status`.
- Google girişi tetiklenmez, çıkış yapılmaz, kayıtlı profil adı değişmez, satın alma düğmesine basılmaz, hesap silme onaylanmaz.

## Review Focus

1. Türkçe Excel CSV'yi noktalı virgülle ve bazen ANSI ile kaydeder → tablo yine okunmalı; bozuk harf sessizce oyuna girmemeli (Görev 1 testi, Görev 3 denetimi).
2. Çevirmen `{0}` yer tutucusunu siler, fazladan `{` yazar → oyun düşmemeli, ham metin görünmeli (Görev 2 testi, Görev 3 denetimi).
3. Üçüncü bir dilde hücre boş → Türkçe görünmeli, boş düğme kalmamalı (Görev 2 testi).
4. Kayıtlı dil tablodan silinmiş veya telefon dili tabloda yok → varsayılan kurala dönülmeli (Görev 2 testi).
5. Biri Türkçe biri İngilizce iki oyuncu aynı masada → ceza yazısı her ekranda kendi dilinde (Görev 4 testi + Görev 9 gözle kontrol).

Testle sabitlenemeyen: İngilizce metnin düğmeye sığmaması. Görev 9'da gözle aranır.

---

## Dosya yapısı

| Dosya | Sorumluluk |
|---|---|
| `Assets/Osman/Scripts/Localization/LocTable.cs` (yeni) | CSV ayrıştırma, dil ve anahtar listesi, hücre okuma. Unity'siz. |
| `Assets/Osman/Scripts/Localization/Loc.cs` (yeni) | Seçili dil, `T`, geri düşüş, `Changed`. |
| `Assets/Osman/Scripts/Localization/LocText.cs` (yeni) | TMP metnini anahtardan yazan bileşen. |
| `Assets/Osman/Scripts/GamePlay/PenaltyReason.cs` (yeni) | Ceza nedeni enum'u ve anahtar eşlemesi. |
| `Assets/Resources/Localization.csv` (yeni) | Tablo. |
| `Assets/Osman/Editor/LocAudit.cs` (yeni) | Denetim mantığı + `Okey/Dil/Tabloyu Denetle` menüsü. |
| `Assets/Osman/Editor/UiKitTable.cs` (yeni) | `Okey/UI Kit/Masa Metinlerini Bağla`. |
| `Assets/Osman/Editor/Tests/LocTableTests.cs`, `LocTests.cs`, `LocalizationTests.cs` (yeni) | Testler. |
| `Assets/Osman/Scripts/Services/GameSettings.cs` | `Language` ayarı. |
| `Assets/Osman/Editor/UiKit*.cs` | Yardımcılar anahtar alır. |
| Çalışma zamanı betikleri | Düz metin → `Loc.T`. |

---

### Task 1: LocTable (CSV okuyucu)

**Files:**
- Create: `Assets/Osman/Scripts/Localization/LocTable.cs`
- Test: `Assets/Osman/Editor/Tests/LocTableTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public sealed class LocTable
  {
      public static LocTable Parse(string csv);
      public IReadOnlyList<string> Languages { get; }   // başlık sırasıyla, "key" ve "note" hariç, küçük harf
      public IReadOnlyList<string> Keys { get; }        // dosya sırasıyla
      public IReadOnlyList<string> DuplicateKeys { get; }
      public string Get(string lang, string key);       // yoksa veya boşsa null
  }
  ```

- [ ] **Step 1: Başarısız testleri yaz** (`LocTableTests`)

```csharp
const string Basic = "key,tr,en,note\nlang.name,Türkçe,English,ad\nlobby.play_now,HEMEN OYNA,PLAY NOW,\n";

[Test] public void Reads_Languages_And_Cells() {
    var t = LocTable.Parse(Basic);
    CollectionAssert.AreEqual(new[] { "tr", "en" }, t.Languages);
    CollectionAssert.AreEqual(new[] { "lang.name", "lobby.play_now" }, t.Keys);
    Assert.AreEqual("PLAY NOW", t.Get("en", "lobby.play_now"));
}
[Test] public void Semicolon_And_Tab_Delimiters() {
    Assert.AreEqual("B", LocTable.Parse("key;tr;en\na.b;A;B\n").Get("en", "a.b"));
    Assert.AreEqual("B", LocTable.Parse("key\ttr\ten\na.b\tA\tB\n").Get("en", "a.b"));
}
[Test] public void Quoted_Cell_With_Delimiter_Quote_And_Newline() {
    var t = LocTable.Parse("key,tr,en\na.b,\"Bir, iki\",\"He said \"\"hi\"\"\nline2\"\n");
    Assert.AreEqual("Bir, iki", t.Get("tr", "a.b"));
    Assert.AreEqual("He said \"hi\"\nline2", t.Get("en", "a.b"));
}
[Test] public void Bom_Crlf_Blank_And_Comment_Lines() {
    var t = LocTable.Parse("\uFEFFkey,tr,en\r\n\r\n# bölüm\r\na.b,A,B\r\n");
    CollectionAssert.AreEqual(new[] { "a.b" }, t.Keys);
    Assert.AreEqual("A", t.Get("tr", "a.b"));
}
[Test] public void Header_Is_Trimmed_And_Lowercased_Note_Excluded() {
    var t = LocTable.Parse("Key, TR ,En,Note\na.b,A,B,açıklama\n");
    CollectionAssert.AreEqual(new[] { "tr", "en" }, t.Languages);
}
[Test] public void Empty_Cell_Short_Row_Unknown_Key_Or_Language_Are_Null() {
    var t = LocTable.Parse("key,tr,en\na.b,A,\nc.d,C\n");
    Assert.IsNull(t.Get("en", "a.b"));
    Assert.IsNull(t.Get("en", "c.d"));
    Assert.IsNull(t.Get("tr", "x.y"));
    Assert.IsNull(t.Get("de", "a.b"));
}
[Test] public void Duplicate_Key_Is_Reported_First_Wins() {
    var t = LocTable.Parse("key,tr\na.b,İlk\na.b,İkinci\n");
    CollectionAssert.AreEqual(new[] { "a.b" }, t.DuplicateKeys);
    Assert.AreEqual("İlk", t.Get("tr", "a.b"));
}
[Test] public void Empty_Or_Null_Input_Gives_Empty_Table() {
    Assert.AreEqual(0, LocTable.Parse("").Keys.Count);
    Assert.AreEqual(0, LocTable.Parse(null).Languages.Count);
}
```

- [ ] **Step 2: Çalıştır, derleme hatasıyla düştüğünü gör.** `U test run --mode EditMode --filter LocTableTests`

- [ ] **Step 3: `LocTable`'ı yaz.** Ayırıcı: başlık satırında (tırnak dışında) en çok geçen `,` `;` `\t`. Anahtar hücresi kırpılır; değer hücreleri kırpılmaz. `#` ile başlayan satır yalnızca tırnak dışındaki satır başında yorumdur.

- [ ] **Step 4: Testler geçer.**

- [ ] **Step 5: Commit** — `Dil: CSV tablosu okuyucusu (LocTable)`

---

### Task 2: Loc, LocText, dil ayarı, tablonun ilk hali

**Files:**
- Create: `Assets/Osman/Scripts/Localization/Loc.cs`, `Assets/Osman/Scripts/Localization/LocText.cs`, `Assets/Resources/Localization.csv`
- Modify: `Assets/Osman/Scripts/Services/GameSettings.cs`
- Test: `Assets/Osman/Editor/Tests/LocTests.cs`

**Interfaces:**
- Consumes: `LocTable.Parse`, `Languages`, `Get` (Görev 1).
- Produces:
  ```csharp
  public static class Loc
  {
      public const string Fallback = "tr";
      public static event Action Changed;
      public static IReadOnlyList<string> Languages { get; }
      public static string Language { get; set; }          // set: tabloda yoksa yok sayılır; değişirse Changed
      public static CultureInfo Culture { get; }           // Language'dan; kurulamazsa InvariantCulture
      public static string T(string key);
      public static string T(string key, params object[] args);
      public static string NameOf(string lang);            // o dilin lang.name hücresi; yoksa kodu
      public static void Load(string csv, string language); // testler ve Editor için; tabloyu değiştirir
      public static string Pick(string saved, string systemCode, IReadOnlyList<string> available);
      public static string CodeOf(SystemLanguage lang);
  }
  public class LocText : MonoBehaviour { public string key; }   // [RequireComponent(typeof(TMP_Text))]
  // GameSettings
  public static string Language { get; set; }   // PlayerPrefs "Okey.Language", yoksa ""
  ```
- `Loc` ilk kullanımda `Resources.Load<TextAsset>("Localization")` ile yüklenir ve dili `Pick(GameSettings.Language, CodeOf(Application.systemLanguage), Languages)` ile seçer. `Loc.Language` setter'ı PlayerPrefs'e **yazmaz**; kaydı çağıran yapar (Görev 6).
- `LocText`: `OnEnable`'da metni yazar ve `Loc.Changed`'e abone olur, `OnDisable`'da ayrılır. Anahtar boşsa dokunmaz.

- [ ] **Step 1: Başarısız testleri yaz** (`LocTests`; her testte `Loc.Load` ile kendi tablosunu kurar, `[TearDown]` `Loc.Load(null, null)` ile gerçek tabloya dönmeyi zorlar — `Load(null, null)` "bir sonraki kullanımda Resources'tan yeniden yükle" demektir)

```csharp
const string Csv = "key,tr,en,de\nlang.name,Türkçe,English,Deutsch\na.hi,Merhaba,Hello,\na.n,{0} el,{0} rounds,\na.bad,{0} ve {1},{0} and {,\n";

[Test] public void T_Returns_Selected_Language() { Loc.Load(Csv, "en"); Assert.AreEqual("Hello", Loc.T("a.hi")); }
[Test] public void Empty_Cell_Falls_Back_To_Turkish() { Loc.Load(Csv, "de"); Assert.AreEqual("Merhaba", Loc.T("a.hi")); }
[Test] public void Unknown_Key_Returns_Key() { Loc.Load(Csv, "en"); LogAssert.ignoreFailingMessages = true; Assert.AreEqual("x.y", Loc.T("x.y")); }
[Test] public void Args_Are_Formatted() { Loc.Load(Csv, "en"); Assert.AreEqual("3 rounds", Loc.T("a.n", 3)); }
[Test] public void Broken_Format_Returns_Raw_Text_Without_Throwing() {
    Loc.Load(Csv, "en"); LogAssert.ignoreFailingMessages = true;
    Assert.AreEqual("{0} and {", Loc.T("a.bad", 1, 2));
}
[Test] public void Too_Few_Args_Returns_Raw_Text() {
    Loc.Load(Csv, "tr"); LogAssert.ignoreFailingMessages = true;
    Assert.AreEqual("{0} ve {1}", Loc.T("a.bad", 1));
}
[Test] public void Setting_Language_Fires_Changed_Once_And_Ignores_Unknown() {
    Loc.Load(Csv, "tr"); int n = 0; Action h = () => n++; Loc.Changed += h;
    Loc.Language = "en"; Loc.Language = "en"; Loc.Language = "xx";
    Loc.Changed -= h;
    Assert.AreEqual(1, n); Assert.AreEqual("en", Loc.Language);
}
[Test] public void NameOf_Reads_Lang_Name_Row() { Loc.Load(Csv, "tr"); Assert.AreEqual("Deutsch", Loc.NameOf("de")); }
[Test] public void Culture_Follows_Language() { Loc.Load(Csv, "tr"); Assert.AreEqual("İ", "i".ToUpper(Loc.Culture)); Loc.Language = "en"; Assert.AreEqual("I", "i".ToUpper(Loc.Culture)); }

static readonly string[] TrEn = { "tr", "en" };
[TestCase("en", "tr", "en")]   // kayıt geçerli → kayıt
[TestCase("", "tr", "tr")]     // kayıt yok, telefon Türkçe
[TestCase("", "de", "en")]     // telefon dili tabloda yok → en
[TestCase("fr", "tr", "tr")]   // kayıtlı dil tablodan silinmiş → telefon dili
[TestCase(null, null, "en")]
public void Pick_Cases(string saved, string system, string expected) => Assert.AreEqual(expected, Loc.Pick(saved, system, TrEn));
[Test] public void Pick_Without_English_Uses_Turkish() => Assert.AreEqual("tr", Loc.Pick("", "de", new[] { "tr" }));
[Test] public void CodeOf_Maps_Known_Languages() {
    Assert.AreEqual("tr", Loc.CodeOf(SystemLanguage.Turkish));
    Assert.AreEqual("en", Loc.CodeOf(SystemLanguage.English));
    Assert.AreEqual("de", Loc.CodeOf(SystemLanguage.German));
}
```

- [ ] **Step 2: Düştüğünü gör.** `U test run --mode EditMode --filter LocTests`

- [ ] **Step 3: `Loc`, `LocText`, `GameSettings.Language` yaz.** `CodeOf`: Turkish tr, English en, German de, French fr, Spanish es, Italian it, Portuguese pt, Russian ru, Arabic ar; diğerleri `""`. Eksik anahtar ve biçim hatası `Debug.LogWarning` ile anahtar başına bir kez loglanır (yalnızca `UNITY_EDITOR || DEVELOPMENT_BUILD`).

- [ ] **Step 4: `Localization.csv`'yi oluştur** (BOM'lu UTF-8, satır sonu CRLF):

```
key,tr,en,note
# Dil
lang.name,Türkçe,English,Dil seçicide görünen ad
```

- [ ] **Step 5: Testler geçer; ayrıca** `U script eval 'return Loc.NameOf("en") + "|" + string.Join(",", Loc.Languages);'` → `English|tr,en`.

- [ ] **Step 6: Commit** — `Dil: Loc, LocText, dil ayarı ve tablonun ilk hali`

---

### Task 3: Denetim (test + menü)

**Files:**
- Create: `Assets/Osman/Editor/LocAudit.cs`
- Test: `Assets/Osman/Editor/Tests/LocalizationTests.cs`

**Interfaces:**
- Consumes: `LocTable` (Görev 1).
- Produces:
  ```csharp
  public static class LocAudit
  {
      public sealed class Report { public List<string> Errors, Warnings, LeftoverLiterals, MissingGlyphs; }
      // saf: tablo metni + (dosya yolu, içerik) çiftleri
      public static Report Check(string csv, IEnumerable<(string path, string text)> sources);
      public static Report CheckProject();   // gerçek CSV + Assets/Osman/**/*.cs
      [MenuItem("Okey/Dil/Tabloyu Denetle")] static void Menu();   // raporu Console'a yazar
  }
  ```
- Kullanılan anahtar = kaynak dosyalarda şu desene uyan her sabit metin:
  `"((?:common|login|lobby|room|settings|profile|account|shop|table|penalty|toast|error|lang)\.[a-z0-9_]+)"`. `Assets/Osman/Editor/Tests/` taranmaz.
- Hatalar: yinelenen anahtar; `tr` veya `en` boş; bir dildeki `{n}` kümesi `tr`'dekinden farklı (boş hücre atlanır); hücrede `\uFFFD`; biçime uymayan anahtar; kullanılan anahtar tabloda yok.
- Uyarılar: tabloda olup kullanılmayan anahtar (`lang.name` hariç); `tr`/`en` dışı dilde boş hücre.
- `LeftoverLiterals` (yalnızca `CheckProject` ve menü): `Assets/Osman/Scripts` ve `Assets/Osman/Editor/UiKit*.cs` içinde `[çğıöşüÇĞİÖŞÜ]` içeren çift tırnaklı metin taşıyan satırlar; `Debug.Log`, `LogWarning`, `LogError`, `throw`, `//` ile başlayan satırlar ve Global Constraints'teki çevrilmeyen dosyalar hariç. `yol:satır: metin` biçiminde.
- `MissingGlyphs` (yalnızca menü): tablodaki bütün hücrelerin harfleri `Assets/Fonts/Changa-Bold SDF.asset` üzerinde `TMP_FontAsset.HasCharacters(text, out List<char> missing)` ile sınanır.

- [ ] **Step 1: Başarısız testleri yaz**

```csharp
static LocAudit.Report Run(string csv, string code = "") => LocAudit.Check(csv, new[] { ("A.cs", code) });

[Test] public void Clean_Table_Has_No_Errors() =>
    Assert.IsEmpty(Run("key,tr,en\nlang.name,Türkçe,English\nlobby.a,A,B\n", "Loc.T(\"lobby.a\")").Errors);
[Test] public void Missing_English_Is_Error() => StringAssert.Contains("lobby.a", Run("key,tr,en\nlobby.a,A,\n").Errors[0]);
[Test] public void Placeholder_Mismatch_Is_Error() => Assert.IsNotEmpty(Run("key,tr,en\nlobby.a,{0} el,rounds\n").Errors);
[Test] public void Replacement_Char_Is_Error() => Assert.IsNotEmpty(Run("key,tr,en\nlobby.a,Ta\uFFFD,Tile\n").Errors);
[Test] public void Duplicate_Key_Is_Error() => Assert.IsNotEmpty(Run("key,tr,en\nlobby.a,A,B\nlobby.a,C,D\n").Errors);
[Test] public void Bad_Key_Format_Is_Error() => Assert.IsNotEmpty(Run("key,tr,en\nLobby.PlayNow,A,B\n").Errors);
[Test] public void Used_Key_Missing_From_Table_Is_Error() =>
    StringAssert.Contains("toast.x", Run("key,tr,en\nlobby.a,A,B\n", "UIRoot.Toast(Loc.T(\"toast.x\"));").Errors[0]);
[Test] public void Unused_Key_Is_Warning_Not_Error() {
    var r = Run("key,tr,en\nlobby.a,A,B\n");
    Assert.IsEmpty(r.Errors); Assert.IsNotEmpty(r.Warnings);
}
[Test] public void Empty_Third_Language_Is_Warning() {
    var r = Run("key,tr,en,de\nlobby.a,A,B,\n", "\"lobby.a\"");
    Assert.IsEmpty(r.Errors); Assert.IsNotEmpty(r.Warnings);
}
[Test] public void Project_Table_Has_No_Errors() {
    var r = LocAudit.CheckProject();
    Assert.IsEmpty(r.Errors, string.Join("\n", r.Errors));
}
```

- [ ] **Step 2: Düştüğünü gör.** `U test run --mode EditMode --filter LocalizationTests`
- [ ] **Step 3: `LocAudit`'i yaz.**
- [ ] **Step 4: Testler geçer.** Menüyü çalıştır; `LeftoverLiterals` listesinin dolu geldiğini gör (taşınacak iş listesi budur; sayısını raporla).
- [ ] **Step 5: Commit** — `Dil: tablo denetimi (test ve Okey/Dil/Tabloyu Denetle)`

---

### Task 4: Ceza nedeni ağdan numara olarak gider

**Files:**
- Create: `Assets/Osman/Scripts/GamePlay/PenaltyReason.cs`
- Modify: `Assets/Osman/Scripts/GamePlay/GameManager.cs` (210, 216-224, 237, 347-357, 371-410), `Assets/Osman/Scripts/GamePlay/PenaltySystem.cs:208`, `Assets/Osman/Scripts/UI/UIManager.cs` (ceza yazısı, ~318-322), `Assets/Osman/Editor/Tests/OkeyRulesTests.cs:174-179`, `Assets/Resources/Localization.csv`
- Test: `Assets/Osman/Editor/Tests/LocTests.cs` (ek), `OkeyRulesTests.cs`

**Interfaces:**
- Consumes: `Loc.T`, `Loc.Load`.
- Produces:
  ```csharp
  public enum PenaltyReason : byte { TookSideCouldNotOpen, SideTileTaken, DiscardedOkey, DiscardedIndicator, DiscardedIslek, ProcessedOnMeld }
  public static class PenaltyReasonText { public static string Of(PenaltyReason r); }   // switch, her dalda sabit anahtarla Loc.T
  // GameManager
  public void AnnouncePenalty(int playerQue, int amount, PenaltyReason reason);
  [PunRPC] void ShowPenaltyRPC(int playerQue, int amount, byte reason);
  public static PenaltyReason? DiscardPenaltyReason(/* parametreler aynı */);
  ```

Tablo satırları (Türkçe sütun bugünkü metinlerin aynısı):

| key | tr | en |
|---|---|---|
| `penalty.took_side_could_not_open` | Yandan aldı, açamadı | Took from the side, could not open |
| `penalty.side_tile_taken` | Taşı yandan alındı | Tile taken from the side |
| `penalty.discarded_okey` | Okey attı | Discarded the okey |
| `penalty.discarded_indicator` | Gösterge attı | Discarded the indicator |
| `penalty.discarded_islek` | İşlek attı | Discarded a playable tile |
| `penalty.processed_on_meld` | Perine işlendi | Tile added to their meld |

- [ ] **Step 1: Testleri yaz/güncelle.** `OkeyRulesTests.cs:174-179` Türkçe metin yerine `PenaltyReason.DiscardedOkey`, `DiscardedIndicator`, `DiscardedIslek` ve ceza yokken `null` bekler. `LocTests`'e ekle:

```csharp
[Test] public void Every_Penalty_Reason_Has_Its_Own_Text_In_Both_Languages() {
    Loc.Load(null, null);
    foreach (var lang in new[] { "tr", "en" }) {
        Loc.Language = lang;
        var texts = Enum.GetValues(typeof(PenaltyReason)).Cast<PenaltyReason>().Select(PenaltyReasonText.Of).ToList();
        CollectionAssert.AllItemsAreUnique(texts);
        Assert.IsFalse(texts.Any(t => t.StartsWith("penalty.")), lang);
    }
}
[Test] public void Penalty_Text_Is_Picked_By_The_Viewer_Language() {
    Loc.Load(null, null);
    Loc.Language = "tr"; Assert.AreEqual("Okey attı", PenaltyReasonText.Of(PenaltyReason.DiscardedOkey));
    Loc.Language = "en"; Assert.AreNotEqual("Okey attı", PenaltyReasonText.Of(PenaltyReason.DiscardedOkey));
}
```

- [ ] **Step 2: Düştüğünü gör.**
- [ ] **Step 3: Uygula.** RPC `byte` alır; alıcı tarafta `PenaltyReasonText.Of((PenaltyReason)reason)` ile metne çevrilip bugünkü gösterim yoluna verilir. Bilinmeyen değer boş metin verir, hata fırlatmaz. `ScoreManager.cs:1730` `"Processing Penalty"` gösterilmiyor; dokunma.
- [ ] **Step 4:** `U test run --mode EditMode` tamamı geçer.
- [ ] **Step 5: Çok editörlü senkron testi** (RPC imzası değişti):
  `powershell -ExecutionPolicy Bypass -File Tools\SyncTest\Run-SyncTest.ps1 -Deals 1` → `PASS`, `desync 0`. Klonlarda "The open scene(s) have been modified externally" penceresi çıkarsa `Reload` düğmesine `PostMessage BM_CLICK (0x00F5)` gönderilir; ENTER işe yaramaz.
- [ ] **Step 6: Commit** — `Ceza nedeni ağdan numara olarak gider; her istemci kendi dilinde yazar`

---

### Task 5: Builder'lar anahtar alır, menü ekranları yeniden üretilir

**Files:**
- Modify: `Assets/Osman/Editor/UiKitPrefabs.cs` (187, 335-381), `UiKitLogin.cs` (13, 43-46, 81-111, 141-160), `UiKitLobby.cs` (168-217, 423-446), `UiKitAccount.cs` (106-295), `Assets/Resources/Localization.csv`
- Yeniden üretilen: `Assets/Osman/Prefabs/UI/*.prefab`, `Assets/Resources/UIRoot.prefab`, `Assets/Osman/Scenes/MainMenu.unity`, `LobbyMenu.unity`

**Interfaces:**
- Consumes: `LocText.key`, `LocTable`.
- Produces (builder içi; `UiKitBuilder` partial sınıfı):
  ```csharp
  // Düz metin (çevrilmez: "X", ">", "", çalışırken dolan alanlar)
  static TextMeshProUGUI Text(Transform parent, string name, string text, float size, TMP_FontAsset font);   // değişmez
  // Çevrilen etiket: LocText ekler, Editor önizlemesi için tablonun tr hücresini yazar
  static TextMeshProUGUI Label(Transform parent, string name, string key, float size, TMP_FontAsset font);
  static void SetLabel(TMP_Text target, string key);   // var olan iç içe prefab metnine anahtar bağlar
  ```
- `KitWindow`, `TextInput`, `ChoiceButton`, `LoginWindow`, `KitRow`, `ToggleRow`, `LinkRow`, `RoundButton`, `FormLabel` parametre adları `title/label/placeholderText` → `titleKey/labelKey/placeholderKey` olur ve içeride `Label`/`SetLabel` kullanır.
- `Label` ve `SetLabel`, anahtar tabloda yoksa `Debug.LogError` basar (Editor'de sessiz boş etiket kalmasın). Tabloyu `AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Localization.csv")` üzerinden `LocTable.Parse` ile okur.

- [ ] **Step 1: Tabloya satırları ekle.** Beş builder dosyasındaki her oyuncuya görünen metin için bir satır (yaklaşık 35). Bölümler: giriş `login.*`, lobi `lobby.*` ve `room.*`, ayarlar `settings.*`, profil `profile.*`, hesap `account.*`, ortak düğmeler `common.*` (`common.cancel` Vazgeç/Cancel, `common.ok_caps` TAMAM/OK, `common.retry_caps` TEKRAR DENE/TRY AGAIN, `common.yes` Evet/Yes, `common.ok` Tamam/OK). Aynı metin iki yerde geçiyorsa tek anahtar. Yasal metin (`UiKitLogin.cs:13`) `<link="terms">` ve `<link="privacy">` etiketleriyle tek satırdır; etiket adları çevrilmez. Oyun adı "51 Okey Online" çevrilmez, düz `Text` kalır.
- [ ] **Step 2: Yardımcıları ve çağrıları değiştir.** `UiKitLobby.cs:197` `rounds[0] + " el"` çalışırken yazılan alandır; builder'da düz `Text` ile boş bırakılır (Görev 7 doldurur).
- [ ] **Step 3: Menüleri sırayla çalıştır:** `Okey/UI Kit/Prefabları Kur`, `Giriş Ekranını Kur`, `Lobiyi Kur`, `Mağazayı Düzelt`, `Hesap Pencerelerini Kur`. Her birinden sonra `U console status` hatasız.
- [ ] **Step 4: Doğrula.**
  - `U test run --mode EditMode` tamamı geçer (`Project_Table_Has_No_Errors` dahil).
  - `Okey/Dil/Tabloyu Denetle`: `LeftoverLiterals` içinde `UiKit*.cs` satırı kalmadı (yalnızca `UiKitBuilder.cs:15` font harf listesi kalabilir).
  - `git diff --stat` sahne ve prefablarda yalnızca `LocText` eklemeleri ve bağlı fileID değişiklikleri gösterir; yerleşim değeri (`m_AnchoredPosition`, `m_SizeDelta`) değişmemiştir.
  - Play modunda MainMenu ve LobbyMenu Türkçe olarak eskisiyle aynı görünür; `U script eval 'Loc.Language = "en"; return "ok";'` sonrası sabit etiketler İngilizce olur. Ekran görüntüsü al.
- [ ] **Step 5: Commit** — `Dil: builder'lar anahtarla kurar, giriş ve lobi ekranları yeniden üretildi`

---

### Task 6: Ayarlarda dil seçici

**Files:**
- Modify: `Assets/Osman/Editor/UiKitAccount.cs` (ayar satırları ~246-270), `Assets/Osman/Scripts/UI/LobbyAccountUI.cs`, `Assets/Resources/Localization.csv`
- Test: `Assets/Osman/Editor/Tests/LocTests.cs` (ek)

**Interfaces:**
- Consumes: `Loc.Languages`, `Loc.Language`, `Loc.NameOf`, `Loc.Changed`, `GameSettings.Language`.
- Produces:
  ```csharp
  // Loc
  public static string Next(int step);   // seçili dilden step kadar ilerideki dil kodu, başa sarar
  // LobbyAccountUI
  public TMP_Text languageValue;
  public void LanguagePrev();   // Loc.Language = Loc.Next(-1); GameSettings.Language = Loc.Language;
  public void LanguageNext();
  ```
- Satır: etiket `settings.language` (Dil / Language), sağda `<` değer `>`. Görünüm lobideki el sayısı seçicisiyle aynı yardımcıdan gelir (`UiKitLobby.cs` içindeki stepper kurulumu ortak yardımcıya çıkarılır). Düğmeler en az 96 birim. Satır Ses/Müzik satırının altına girer; "Hesabımı Sil" kaydırmadan görünür kalmalıdır — sığmıyorsa pencere gövdesi satır yüksekliği kadar büyütülür, satırlar küçültülmez.

- [ ] **Step 1: Testi yaz**

```csharp
[Test] public void Next_Wraps_Both_Ways() {
    Loc.Load("key,tr,en,de\nlang.name,Türkçe,English,Deutsch\n", "tr");
    Assert.AreEqual("en", Loc.Next(1)); Assert.AreEqual("de", Loc.Next(-1));
    Loc.Language = "de"; Assert.AreEqual("tr", Loc.Next(1));
}
```

- [ ] **Step 2: Düştüğünü gör, `Loc.Next`'i yaz, geçtiğini gör.**
- [ ] **Step 3: Satırı builder'a ekle, `LobbyAccountUI`'ye işleyicileri ekle.** `languageValue.text = Loc.NameOf(Loc.Language)`; `OnEnable`'da `Loc.Changed += Refresh`, `OnDisable`'da çıkar. `Hesap Pencerelerini Kur` menüsünü çalıştır.
- [ ] **Step 4: Play modunda doğrula.** Ayarları aç, `>` bas: değer "English", pencere başlığı ve satır etiketleri İngilizce. Play'i durdur, yeniden başlat: İngilizce açılır (`Okey.Language` kaydedildi). Sonra Türkçe'ye geri al. Ekran görüntüsü: iki dilde ayarlar penceresi, silme satırı görünür.
- [ ] **Step 5: Commit** — `Ayarlar: dil seçici`

---

### Task 7: Menü tarafı çalışma zamanı metinleri

**Files:**
- Modify: `Assets/Osman/Scripts/UI/Kit/UIRoot.cs` (104, 108, 192, 225), `UI/Kit/JoinErrorText.cs`, `UI/MainMenuUIHandler.cs`, `UI/LobbyAccountUI.cs`, `UI/ButtonFuncs.cs`, `UI/ExitButton.cs`, `UI/Lobby/RoomCard.cs`, `UI/ShopController.cs:55`, `Photons/RoomManager.cs`, `Services/NameFlow.cs`, `GamePlay/SeatManager.cs:105`, `Assets/Resources/Localization.csv`
- Test: `Assets/Osman/Editor/Tests/JoinErrorTextTests.cs`, ilgili `NameFlow` testleri, `LocTests.cs` (ek)

**Interfaces:**
- Consumes: `Loc.T`, `Loc.Culture`, `Loc.Changed`.
- Produces:
  - `UIRoot.Confirm(title, body, yesLabel = null, destructive = false)` ve `Notice(title, body, okLabel = null)`: `null` → `common.yes` / `common.ok`. Çağıranlar çevrilmiş metin verir (`UIRoot` anahtar değil metin alır; imza tipi değişmez).
  - `JoinErrorText` ve `NameFlow` hata metni yerine **anahtar** döndürür (`error.room_full` vb.); gösteren yer `Loc.T` ile çevirir. Değişken anahtarla `Loc.T` çağrısı yalnızca bu iki dönüş değerinde serbesttir; anahtarlar döndürüldükleri yerde sabit metin olarak yazıldığı için denetim onları görür.
  - `RoomCard.Initial`: `tr-TR` sabiti yerine `Loc.Culture`.

Değerli metinler (tam liste; sol sütun bugünkü ifade):

| Yer | Anahtar | tr | en |
|---|---|---|---|
| `LobbyAccountUI.cs:49` | `settings.version` | Sürüm {0} | Version {0} |
| `LobbyAccountUI.cs:78` | `profile.chip` | {0} · Seviye {1} | {0} · Level {1} |
| `LobbyAccountUI.cs:80` | `profile.level` | Seviye {0} | Level {0} |
| `LobbyAccountUI.cs:81` | `profile.xp` | {0} / {1} XP | {0} / {1} XP |
| `ButtonFuncs.cs:147`, `UiKitLobby` el alanı | `room.rounds` | {0} el | {0} rounds |
| `RoomCard.cs:36` | `room.rounds` + `room.locked_suffix` | `   ·   Şifreli` | `   ·   Locked` |

`MainMenuUIHandler.cs:42` `"v" + Application.version` çevrilmez. `profile.chip` satırının `note` hücresi: "Ad ({0}) başta kalmalı; baş harf buradan okunur."

- [ ] **Step 1: Testleri güncelle/yaz.** `JoinErrorTextTests` beş Türkçe metin yerine beş anahtarı sabitler; `NameFlow` testleri aynı şekilde. `LocTests`'e ekle:

```csharp
[Test] public void Profile_Chip_Starts_With_The_Name_In_Every_Language() {
    Loc.Load(null, null);
    foreach (var lang in Loc.Languages) { Loc.Language = lang; StringAssert.StartsWith("Zeynep", Loc.T("profile.chip", "Zeynep", 3)); }
}
```

- [ ] **Step 2: Düştüğünü gör.**
- [ ] **Step 3: Metinleri taşı.** Dosya dosya; her sabit metin `Loc.T("...")` olur, tabloya `tr` + `en` satırı girer. Bildirimler `toast.*`, hata metinleri `error.*`, hesap silme akışı `account.*`.
- [ ] **Step 4: Dil değişince yenileme.** `LobbyAccountUI` (profil çipi, hesap satırları, sürüm) ve `ButtonFuncs` (masa kartları, boş liste yazısı, el sayısı alanı) `Loc.Changed`'e abone olup var olan yenileme yöntemlerini çağırır; `OnDestroy`/`OnDisable`'da ayrılır.
- [ ] **Step 5: Doğrula.**
  - `U test run --mode EditMode` tamamı geçer.
  - `Okey/Dil/Tabloyu Denetle`: `LeftoverLiterals` içinde bu görevin dosyalarından satır kalmadı.
  - Play modunda lobide dili İngilizce yap: profil çipi, masa kartı, boş liste yazısı, bir bildirim (`U script eval 'UIRoot.Toast(Loc.T("toast.connection_timeout")); return "ok";'`) İngilizce. Çıkış onayı penceresi İngilizce.
- [ ] **Step 6: Commit** — `Dil: giriş, lobi, ayarlar ve hesap metinleri tablodan`

---

### Task 8: Masa ve mağaza metinleri

**Files:**
- Create: `Assets/Osman/Editor/UiKitTable.cs`
- Modify: `Assets/Osman/Scripts/UI/UIManager.cs` (97, 111, 154, 162, 168, 322, 445, 459, 488), `GamePlay/ScoreManager.cs:2029`, `GamePlay/SeatManager.cs:471`, `Assets/Osman/Editor/UiKitLobby.cs` (`BuildShop`), `Assets/Osman/Scenes/Table.unity`, `LobbyMenu.unity`, `Assets/Resources/Localization.csv`

**Interfaces:**
- Consumes: `Loc.T`, `LocText`, `Label`/`SetLabel` (Görev 5).
- Produces: menü `Okey/UI Kit/Masa Metinlerini Bağla` (`UiKitBuilder` partial). `Table.unity`'yi açar, aşağıdaki metinleri **bugünkü metin içeriğinden** bulur, `LocText` ekler, kaydeder. Tekrar çalıştırılınca ikinci bileşen eklemez; bulamadığı her metni `Debug.LogError` ile adıyla bildirir.

Masa sabit etiketleri:

| Sahnedeki metin | Anahtar | tr | en |
|---|---|---|---|
| TEKRAR OYNA | `table.play_again` | TEKRAR OYNA | PLAY AGAIN |
| TASLARI TOPLA | `table.collect_tiles` | TAŞLARI TOPLA | COLLECT TILES |
| TAS ISLE | `table.add_tile` | TAŞ İŞLE | ADD TILE |
| TABLO | `table.scoreboard` | TABLO | SCORES |
| GERI BIRAK | `table.put_back` | GERİ BIRAK | PUT BACK |
| Çıkıs | `table.exit` | Çıkış | Exit |
| ÇIFT AÇ | `table.open_pairs` | ÇİFT AÇ | OPEN PAIRS |
| SERI \nAÇ | `table.open_runs` | SERİ\nAÇ | OPEN\nRUNS |
| Oyuncu Bekleniyor (4) | `table.waiting_player` | Oyuncu Bekleniyor | Waiting for player |

"Seri Puanın: 0" ve "Çift Sayın: 0" çalışırken yazılır; `LocText` almaz.

Değerli metinler:

| Yer | Anahtar | tr | en |
|---|---|---|---|
| `UIManager.cs:97` | `table.live_penalties` | ANLIK CEZA PUANLARI | LIVE PENALTY POINTS |
| `:154` | `table.run_score` | Seri Puan: <color={0}>{1}</color> | Run points: <color={0}>{1}</color> |
| `:162` | `table.pair_score` | Çift Puan: <color={0}>{1}</color> | Pair points: <color={0}>{1}</color> |
| `:168` | `table.threshold` | Baraj: {0} | Threshold: {0} |
| `:459` | `table.winner` | {0} Oyunu Kazandı! | {0} won the game! |
| `:488` | `table.back_to_lobby_in` | Lobiye dönüş: {0}s | Back to lobby: {0}s |
| `ScoreManager.cs:2029` | `table.limit_not_enough` | Limitin yetmiyor, açamazsın! (Elin: {0}, gereken: {1}) | Not enough to open! (Hand: {0}, needed: {1}) |
| `SeatManager.cs:471` | `table.sit` | OTUR | SIT |
| `SeatManager.cs:105` (Görev 7'de taşındıysa atla) | `toast.seat_taken` | Koltuk doldu, başka bir yer seç | Seat taken, pick another |

`UIManager.cs:111` (`ad: puan`) ve `:322` (`+{amount}` + neden) yalnızca değer ve işaret taşır; çevrilmez. `:445` `"-"` çevrilmez.

Mağaza: `BuildShop` içindeki sabit etiketler `SetLabel` ile bağlanır — `shop.buy` (Satın Al / Buy), `shop.remove_ads` (Reklamları Kaldır / Remove Ads); `ShopController.cs:55` `shop.active` (Aktif / Active). "Ürün N" adları ve fiyatlara dokunulmaz.

- [ ] **Step 1: Tablo satırlarını ekle.**
- [ ] **Step 2: `UiKitTable.cs` menüsünü yaz, çalıştır.** Console'da hata yok; `git diff --stat Assets/Osman/Scenes/Table.unity` yalnızca eklemeler gösterir. Menüyü ikinci kez çalıştır: diff değişmez.
- [ ] **Step 3: Kod metinlerini taşı; `Mağazayı Düzelt` menüsünü çalıştır.**
- [ ] **Step 4: Doğrula.**
  - `U test run --mode EditMode` tamamı geçer.
  - `Okey/Dil/Tabloyu Denetle`: `LeftoverLiterals` boş (ya da yalnızca Global Constraints'teki çevrilmeyenler; kalan her satırı raporda gerekçesiyle yaz).
  - Solo masa: `powershell -ExecutionPolicy Bypass -File Tools\SyncTest\Run-SyncTest.ps1 -Solo -Mode smart -Seed 11` geçer.
  - Play modunda masada düğmeler doğru Türkçe harflerle; `Okey.Language = en` ile yeniden girince İngilizce. "SERİ AÇ" / "OPEN RUNS" düğmeye sığıyor. Ekran görüntüsü, iki dil.
- [ ] **Step 5: Commit** — `Dil: masa ve mağaza metinleri tablodan; masadaki eksik Türkçe harfler düzeldi`

---

### Task 9: Bütünün doğrulanması

**Files:**
- Modify: `Assets/Resources/Localization.csv` (yalnızca taşma düzeltmesi gerekirse), `docs/superpowers/specs/2026-11-19-dil-sistemi-design.md` (sona "Uygulamadan devreden notlar")

- [ ] **Step 1:** `U test run --mode EditMode` — tamamı geçer; sayıyı yaz (başlangıç 248).
- [ ] **Step 2:** `Okey/Dil/Tabloyu Denetle` — hata 0; uyarıları ve `MissingGlyphs` listesini rapora yaz.
- [ ] **Step 3: Çok editörlü senkron testi** — `Run-SyncTest.ps1 -Deals 1` → `PASS`, `desync 0`.
- [ ] **Step 4: Karışık dil.** Ana editör `tr`, bir klon `en` (`U script eval 'GameSettings.Language = "en"; return "ok";' --project-path F:/Documents/UnityProjects/51Okey_clone_0`). Aynı masada bir ceza oluştur; ana editörde Türkçe, klonda İngilizce ceza yazısının ekran görüntüsünü al. Sonra klonu `tr`'ye geri al.
- [ ] **Step 5: Taşma taraması.** İngilizce'de giriş, lobi (boş ve kartlı), masa kurma penceresi, şifre penceresi, ayarlar, profil, hesap silme onayları (ikinci onay **verilmez**), mağaza, masa. Taşan her metin için önce İngilizce metni kısalt; yerleşim değiştirme. Ekran görüntülerini `.superpowers/shots-keep/dil/` altına koy.
- [ ] **Step 6: Excel gidiş-dönüş.** `Localization.csv`'nin bir kopyasını `;` ayırıcıyla yeniden yaz, `LocAudit.Check` ile sına: hata 0, anahtar sayısı aynı. (Kopya commit edilmez.)
- [ ] **Step 7: Notları yaz.** Spec'in sonuna: anahtar sayısı, yalnız verilen kararlar, bilinen açıklar, telefonda denenecekler (telefon dili İngilizce iken ilk açılış, dil değiştirip yeniden açma, iki telefon farklı dilde ceza yazısı).
- [ ] **Step 8: Commit** — `Dil: doğrulama notları`
