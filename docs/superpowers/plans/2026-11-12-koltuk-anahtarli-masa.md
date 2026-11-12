# Masayı Koltuğa Bağlama (Akıllı Bot Aşama 0) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Masa bir koltuğu oyuncu ismi ve Photon oyuncu numarası yerine sıra numarasıyla (que) tanısın: aynı isimli oyuncular sorunsuz oynasın, bot koltukları per alanı, puan, isim ve el sonu satırıyla masada "var" olsun; insan oyununun davranışı değişmesin.

**Architecture:** Atma alanının bugün kullandığı eşleme (`seatOfQue[que]` → yerel koltuğa göre `relativeIndex` → dizi elemanı) saf fonksiyonlara (`SeatMath`) çıkarılır ve `SeatManager` üzerinden per alanına da açılır (`MeldAreaOfQue`, `QueOfMeldArea`, `QuesInTableOrder`, `SeatName`). 14 isimli arama bu yardımcılara bağlanır. Anlık puan `SeatScores` oda özelliğine, el sonu çağrıları sıra numarasına taşınır. Test betiğine "aynı görünen isim" ve "N bot" seçenekleri eklenir.

**Tech Stack:** Unity 6000.3.10f1, C# (Assembly-CSharp, asmdef ve namespace yok), Photon PUN 2, NUnit EditMode (`Assets/Osman/Editor/Tests/`), PowerShell 5.1, `unity` CLI.

**Spec:** `docs/superpowers/specs/2026-11-12-koltuk-anahtarli-masa-design.md`

**Kim yapar:** Her görevin kod kısmı alt ajana verilir. Dört editörü süren canlı adımlar (**[canlı]** işaretli) oturumun sahibi (ana ajan) tarafından, ajanın commit'i üzerinde koşulur.

## Global Constraints

- **Sahne, prefab, `ProjectSettings/`, `Packages/` dosyalarına dokunulmaz.** Yeni `[PunRPC]` eklenmez; mevcut `[PunRPC]` adları değişmez (yalnızca Task 5'teki iki imza değişir).
- İnsan oyununun davranışı değişmez. Referans: `Run-SyncTest.ps1 -Mode smart -Seed 11 -Deals 1` → `PASS: 1 el, ok 52, desync 0`.
- "Her oyuncunun per alanı" döngüleri `SeatManager.QuesInTableOrder()` sırasıyla dolaşır: önce `PhotonNetwork.PlayerList` sırasındaki oyuncuların sıra numaraları, sonra kalan sıralar küçükten büyüğe. Bugünkü dolaşma sırası böylece korunur ("ilk uyan yuva" seçen kodlar aynı yuvayı seçer).
- Per alanı bulunamazsa (`null`) çağıran yer bugünkü gibi o alanı atlar ya da çıkar; yeni `NullReferenceException` olmaz.
- `PhotonNetwork.PlayerList`'i başka amaçla dolaşan yerlere (koltuk atama, oyuncu sayısı, sıra kimde) dokunulmaz.
- Beklenen durumlar `Debug.LogWarning`; `Debug.LogError` yalnızca gerçek hata için (test koşucusu her `LogError`'u FAIL sayar).
- Kod stili çevredeki kod gibi: global namespace, Türkçe tek satırlık "neden" yorumları; dokunulmayan yorumlar silinmez.
- Gizli bilgi yazılmaz; her commit'ten önce `powershell -ExecutionPolicy Bypass -File Tools/Security/Check-Secrets.ps1` (exit 0 şart).
- Çalışma ağacında ilgisiz değişiklikler var (`51Okey.slnx`, `ProjectSettings/PackageManagerSettings.asset`, `ProjectSettings/UnityConnectSettings.asset`): **asla `git add -A`**, dosyalar adıyla eklenir; yeni dosyaların `.meta`'ları da.
- EditMode: `powershell -ExecutionPolicy Bypass -File Tools\SyncTest\Run-EditModeTests.ps1 -Filter <Sınıf>`. Eksik tip derleme hatasıdır: RED çıktısı `COMPILE FAILED`. Hata satırı olmayan çıplak `COMPILE FAILED` yenileme yarışıdır; bir kez daha koşulur.
- Editör Play'deyse ya da ulaşılamıyorsa alt ajan durur ve BLOCKED bildirir; Play'i kendisi başlatıp durdurmaz.
- Commit mesajı sonu: `Co-Authored-By: Claude <noreply@anthropic.com>`. Push yok.

## Review Focus

1. **Aynı isimli dört oyuncu.** Beklenen: her oyuncunun perleri kendi köşesinde, her cihazda aynı. → Task 1 canlı kırmızı koşu, Task 3 canlı yeşil koşu (`-SameNames`).
2. **Eşleme gelmeden per alanı istenmesi** (`seatOfQue` henüz 0). Beklenen: `null`, istisna yok; yerel oyuncunun alanı her zaman bulunur. → Task 2 `MeldSeat_UnmappedQueGivesNoSeat`, Task 3 seed 11 koşusu.
3. **Bot koltuğu ikinci cihazda.** Beklenen: bot adı, bot puanı ve el sonu satırı iki cihazda aynı. → Task 4 ve 5 canlı `-Bots 2` koşusu.
4. **Dolaşma sırası değişirse farklı yuvaya işleme.** Beklenen: seed 11 sayacı `ok 52` kalır. → Task 3 canlı koşu.
5. **Kazanansız el (taş bitti).** Beklenen: dört satır, kimse kazanan sayılmaz. → Task 5 `-Bots 2` koşusunda `el sonu que1..4` kontrolleri; basit botlu elde taş çoğunlukla biter.

---

### Task 1: Test betiğine "aynı isim" ve "N bot" seçenekleri

**Files:**
- Modify: `Assets/Osman/Scripts/GamePlay/SyncAutoTestData.cs` (`SyncAutoTestConfig`)
- Modify: `Assets/Osman/Scripts/GamePlay/SyncAutoTest.cs` (satır 108 civarı; host oda kurmadan önce)
- Modify: `Assets/Osman/Scripts/GamePlay/SeatManager.cs` (`StartWithBotsAfterDelay`)
- Modify: `Tools/SyncTest/Run-SyncTest.ps1`
- Test: `Assets/Osman/Editor/Tests/SyncAutoTestDataTests.cs`

**Interfaces — Produces:**
- `SyncAutoTestConfig`: yeni alanlar `public string displayName;` ve `public float botFillDelaySec;`; yeni özellik `public string PhotonName => string.IsNullOrEmpty(displayName) ? nickName : displayName;`
- `SyncAutoTest`: `PhotonNetwork.NickName = Config.PhotonName;` (iç kimlik `Config.nickName` her yerde aynı kalır: seed, `AT-g1` dalı, günlük adı).
- `SeatManager`: `public static float? BotFillDelayOverride;` — `StartWithBotsAfterDelay` `BotFillDelayOverride ?? botFillDelay` kadar bekler. Host test oyuncusu `Config.botFillDelaySec > 0` ise odayı kurmadan önce bunu atar. Oyun kodunda başka hiçbir yer atamaz.
- `Run-SyncTest.ps1`: `-SameNames` (tüm config'lerde `displayName = 'Ayni'`) ve `-Bots <1..3>` (ilk `4 - Bots` proje kullanılır; her config'de `fillWithBots = true`; `botFillDelaySec = 20 + 25 × misafir sayısı`). `-Bots`, `-Solo` ve `-JoinRoom` birlikte verilemez (hata mesajı, exit 1). Örnekler betiğin başındaki `.EXAMPLE` bölümüne eklenir.

- [ ] **Step 1: Başarısız testi yaz** (`SyncAutoTestDataTests.cs` sınıfına ekle)

```csharp
[Test]
public void Config_PhotonNameIsDisplayNameWhenGiven()
{
    var c = JsonUtility.FromJson<SyncAutoTestConfig>("{\"nickName\":\"AT-g1\",\"displayName\":\"Ayni\"}");
    Assert.AreEqual("Ayni", c.PhotonName);
    Assert.AreEqual("AT-g1", c.nickName);
}

[Test]
public void Config_PhotonNameFallsBackToNickName()
{
    var c = JsonUtility.FromJson<SyncAutoTestConfig>("{\"nickName\":\"AT-host\"}");
    Assert.AreEqual("AT-host", c.PhotonName);
    Assert.AreEqual(0f, c.botFillDelaySec);
}
```

- [ ] **Step 2: Başarısız olduğunu gör** — Run: `Run-EditModeTests.ps1 -Filter SyncAutoTestDataTests` → Expected: `COMPILE FAILED` (`PhotonName` yok).
- [ ] **Step 3: Dört dosyayı yukarıdaki arayüzle değiştir.**
- [ ] **Step 4: Geçtiğini gör** — Run: aynı komut → Expected: `passed 13/13`.
- [ ] **Step 5: Commit**

```bash
git add Assets/Osman/Scripts/GamePlay/SyncAutoTestData.cs Assets/Osman/Scripts/GamePlay/SyncAutoTest.cs Assets/Osman/Scripts/GamePlay/SeatManager.cs Tools/SyncTest/Run-SyncTest.ps1 Assets/Osman/Editor/Tests/SyncAutoTestDataTests.cs
powershell -ExecutionPolicy Bypass -File Tools/Security/Check-Secrets.ps1
git commit -m "Sync test: same display name option and a humans-plus-bots mode"
```

- [ ] **Step 6 [canlı]: Referans koşular (değişiklikten önceki davranış)**

Run: `Run-SyncTest.ps1 -Mode smart -Seed 11 -Deals 1` → Expected: `PASS: 1 el, ok 52, desync 0`.
Run: `Run-SyncTest.ps1 -Mode smart -Seed 11 -Deals 1 -Bots 2` → sonuç ve `ok` sayısı deftere yazılır (bugünkü kodla beklenen: PASS).
Run: `Run-SyncTest.ps1 -Mode smart -Seed 11 -Deals 1 -SameNames` → **Expected: FAIL** (desync ya da `[BUG]`; aynı isimli alanlar karışır). İlk hata satırı deftere yazılır. PASS çıkarsa hata bu yolla üretilemiyor demektir: durulur, kullanıcıya bildirilir.

---

### Task 2: Koltuk eşlemesi yardımcıları

**Files:**
- Create: `Assets/Osman/Scripts/GamePlay/SeatMath.cs`
- Modify: `Assets/Osman/Scripts/GamePlay/SeatManager.cs`
- Modify: `Assets/Osman/Scripts/TILES/TileDistrubite.cs` (`InstatiateSideTiles`, satır 667-700: formül `SeatMath`'e bağlanır, davranış aynı)
- Test: `Assets/Osman/Editor/Tests/SeatMathTests.cs`

**Interfaces — Produces:**

```csharp
public static class SeatMath
{
    public static int RelativeIndex(int targetSeat, int localSeat);          // (target - local + 4) % 4
    public static int SeatAtRelativeIndex(int relativeIndex, int localSeat); // (local - 1 + rel) % 4 + 1
    public static int QueOfSeat(int[] seatOfQue, int seat);                  // seatOfQue[1..4] içinde arar; yoksa -1
    public static int MeldSeat(int[] seatOfQue, int que);                    // geçerli sıra ve koltuk 1..4 ise koltuk; değilse -1
    public static int BotNumber(bool[] seatHasPlayer, int seat);             // seatHasPlayer[1..4]; oyuncusuz koltuklar arasında 1'den; oyuncu varsa 0
}
```

`SeatManager` (hepsi public):
- `public static SeatManager Instance;` (`Awake`'te atanır).
- `int LocalSeat` — yerel oyuncunun koltuğu; yoksa 1 (`InstatiateSideTiles`'ın bugünkü varsayılanı).
- `Transform LocalMeldArea` — `meldTileOffsets[0].transform`.
- `Transform MeldAreaOfQue(int que)` — `SeatMath.MeldSeat(seatOfQue, que)` → `RelativeIndex(seat, LocalSeat)` → `meldTileOffsets[rel].transform`; koltuk -1 ise `null`.
- `int QueOfMeldArea(Transform area)` — `meldTileOffsets` içindeki yeri → `SeatAtRelativeIndex` → `QueOfSeat`; bulunamazsa -1.
- `IEnumerable<int> QuesInTableOrder()` — Global Constraints'teki sıra.
- `seatOfQue`, `TileDistrubite`'ten okunur (`GameManager.Instance.tileDistrubite`; yoksa `MeldAreaOfQue` `null`, `QuesInTableOrder` 1..4).

Bu görevde yardımcıları `InstatiateSideTiles` dışında kimse kullanmaz; davranış değişmez.

- [ ] **Step 1: Başarısız testi yaz** (`SeatMathTests.cs`)

```csharp
using NUnit.Framework;

public class SeatMathTests
{
    [Test]
    public void RelativeIndex_LocalSeatIsAlwaysZero()
    {
        for (int seat = 1; seat <= 4; seat++)
            Assert.AreEqual(0, SeatMath.RelativeIndex(seat, seat));
    }

    [TestCase(2, 1, 1)]
    [TestCase(1, 2, 3)]
    [TestCase(4, 1, 3)]
    [TestCase(3, 1, 2)]
    public void RelativeIndex_MatchesTheDropAreaFormula(int targetSeat, int localSeat, int expected) =>
        Assert.AreEqual(expected, SeatMath.RelativeIndex(targetSeat, localSeat));

    [Test]
    public void SeatAtRelativeIndex_IsTheInverseForAllSixteenPairs()
    {
        for (int local = 1; local <= 4; local++)
        for (int target = 1; target <= 4; target++)
            Assert.AreEqual(target, SeatMath.SeatAtRelativeIndex(SeatMath.RelativeIndex(target, local), local));
    }

    [Test]
    public void QueOfSeat_FindsTheQueOrMinusOne()
    {
        int[] seatOfQue = { 0, 3, 4, 1, 2 }; // sıra 1 -> koltuk 3 ...
        Assert.AreEqual(3, SeatMath.QueOfSeat(seatOfQue, 1));
        Assert.AreEqual(1, SeatMath.QueOfSeat(seatOfQue, 3));
        Assert.AreEqual(-1, SeatMath.QueOfSeat(new int[5], 2));
    }

    [Test]
    public void MeldSeat_UnmappedQueGivesNoSeat()
    {
        Assert.AreEqual(-1, SeatMath.MeldSeat(new int[5], 1)); // eşleme henüz gelmedi
        Assert.AreEqual(-1, SeatMath.MeldSeat(new[] { 0, 3, 4, 1, 2 }, 0));
        Assert.AreEqual(-1, SeatMath.MeldSeat(new[] { 0, 3, 4, 1, 2 }, 5));
        Assert.AreEqual(4, SeatMath.MeldSeat(new[] { 0, 3, 4, 1, 2 }, 2));
    }

    [Test]
    public void BotNumber_CountsEmptySeatsInSeatOrder()
    {
        bool[] has = { false, true, false, true, false }; // koltuk 1 ve 3 dolu
        Assert.AreEqual(0, SeatMath.BotNumber(has, 1));
        Assert.AreEqual(1, SeatMath.BotNumber(has, 2));
        Assert.AreEqual(0, SeatMath.BotNumber(has, 3));
        Assert.AreEqual(2, SeatMath.BotNumber(has, 4));
    }
}
```

- [ ] **Step 2: Başarısız olduğunu gör** — Run: `Run-EditModeTests.ps1 -Filter SeatMathTests` → Expected: `COMPILE FAILED` (`SeatMath` yok).
- [ ] **Step 3: `SeatMath.cs`'i yaz; `SeatManager` üyelerini ekle; `InstatiateSideTiles` formülünü `SeatMath.RelativeIndex`'e bağla.**
- [ ] **Step 4: Geçtiğini gör** — Run: aynı komut → Expected: `passed 9/9`.
- [ ] **Step 5: Commit**

```bash
git add Assets/Osman/Scripts/GamePlay/SeatMath.cs* Assets/Osman/Scripts/GamePlay/SeatManager.cs Assets/Osman/Scripts/TILES/TileDistrubite.cs Assets/Osman/Editor/Tests/SeatMathTests.cs*
powershell -ExecutionPolicy Bypass -File Tools/Security/Check-Secrets.ps1
git commit -m "Seat mapping helpers: meld area by queue number"
```

---

### Task 3: İsimli aramaları sıra numarasına bağla

**Files:**
- Modify: `Assets/Osman/Scripts/GamePlay/ScoreManager.cs` (74, 1598-1603, 2218-2243)
- Modify: `Assets/Osman/Scripts/GamePlay/ProcessAssist.cs` (108-112)
- Modify: `Assets/Osman/Scripts/TILES/TileDistrubite.cs` (923-970, 1231-1253, 1465-1474, 1546-1548, 1576-1578, 1810-1817, 2204-2215)
- Modify: `Assets/Osman/Scripts/GamePlay/SyncAutoTest.Smart.cs` (124-143, 190-192, 410-415)

**Interfaces — Consumes:** Task 2'deki `SeatManager.Instance` üyeleri.

Her yerde yapılacak:
| Yer | Bugün | Sonra |
|---|---|---|
| `ScoreManager.Start` :74 | `Find(local.NickName + " meld")` | `SeatManager.Instance.LocalMeldArea` |
| `ScoreManager` :1598 (`ActivePers` döngüsü) | `foreach player in PlayerList` + `Find` | `foreach que in QuesInTableOrder()` + `MeldAreaOfQue(que)`; gövdede sahip sırası gerekiyorsa `que` |
| `ScoreManager.GetOwnerQueFromPlaceholder` :2218 | ad karşılaştırma | `QueOfMeldArea(placeholder.parent.parent)` |
| `ProcessAssist.Collect` :108 | `PlayerList` + `Find` | `QuesInTableOrder()` + `MeldAreaOfQue` |
| `TileDistrubite.InstatiateMeldTiles` :958-968 ve `UpdateAvailableForPlaceholders` :1241-1253 | `PlayerQue == playerCount` olan oyuncuyu bul, `Find` | `MeldAreaOfQue(playerCount)`; `null` ise çık |
| `TileDistrubite.InstantiateActiveTiles` :1465 | oyuncu ara + `Find` | `MeldAreaOfQue(placement.ownerPlayerQue)` |
| `TileDistrubite.RecalculateAllAvailableSlots` :1546, :1576 | `PlayerList` + `Find` | `QuesInTableOrder()` + `MeldAreaOfQue` |
| `TileDistrubite.SyncProcessedTileRPC` :1810-1817 | oyuncu ara + `Find` | `MeldAreaOfQue(ownerQue)`; `null` ise çık |
| `TileDistrubite.AppendSyncState` :2204-2215 | oyuncular (actor sırası), `board#<actor>` | sıra 1..4 (küçükten büyüğe), `board#<que>`; alan `null` ise satır yazılmaz |
| `SyncAutoTest.Smart` :124-143 | `PlayerList` + `Find`; `Key = "{nick}|{type}|{idx}"`; `OwnerQue = PlayerQue` | `QuesInTableOrder()`; `Key = "{que}|{type}|{idx}"`; `OwnerQue = que` |
| `SyncAutoTest.Smart.BoardRows` :190 | `PlayerList` + `Find` | `QuesInTableOrder()` + `MeldAreaOfQue` |
| `SyncAutoTest.Smart.SlotByKey` :410 | `Find(parts[0] + " meld")` | `MeldAreaOfQue(int.Parse(parts[0]))` |

`SeatManager.UpdateSeatDisplay`'in nesne adlandırması (`NickName + " meld"`) hata ayıklama için kalır.

Bu görevde birim testi yok: değişen yerler sahne nesnelerine bağlı; eşleme Task 2'de test edildi. Kanıt canlı koşulardır.

- [ ] **Step 1: Yukarıdaki tablodaki her yeri değiştir.**
- [ ] **Step 2: Kalan isimli aramaları say**

Run: `grep -rn '" meld"' Assets/Osman/Scripts --include=*.cs`
Expected: tek satır, `SeatManager.cs` içindeki adlandırma.

- [ ] **Step 3: Derleme ve mevcut testler** — Run: `Run-EditModeTests.ps1` sırayla `-Filter SeatMathTests`, `TestPlayerBrainTests`, `RuleOracleTests`, `SyncAutoTestDataTests` → Expected: `passed 9/9`, `10/10`, `15/15`, `13/13`.
- [ ] **Step 4: Commit**

```bash
git add Assets/Osman/Scripts/GamePlay/ScoreManager.cs Assets/Osman/Scripts/GamePlay/ProcessAssist.cs Assets/Osman/Scripts/TILES/TileDistrubite.cs Assets/Osman/Scripts/GamePlay/SyncAutoTest.Smart.cs
powershell -ExecutionPolicy Bypass -File Tools/Security/Check-Secrets.ps1
git commit -m "Find meld areas by queue number instead of by nickname"
```

- [ ] **Step 5 [canlı]: Dört koşu**

Run: `Run-SyncTest.ps1 -Mode smart -Seed 11 -Deals 1` → Expected: `PASS: 1 el, ok 52, desync 0`.
Run: `... -Seed 11 -Deals 1 -SameNames` → Expected: `PASS`, `desync 0` (Task 1'de FAIL idi).
Run: `... -Seed 44 -Deals 1 -Mistakes 0.3` → Expected: `PASS`.
Run: `... -Seed 11 -Deals 1 -Bots 2` → Expected: `PASS`; `ok` sayısı Task 1'deki referansla aynı.

---

### Task 4: Koltuk adı ve anlık puan

**Files:**
- Modify: `Assets/Osman/Scripts/GamePlay/SeatManager.cs` (`SeatName`, `UpdateSeatDisplay` 4. adım)
- Modify: `Assets/Osman/Scripts/GamePlay/ScoreManager.cs` (66-70, 193-200, 219-238, 242-270)
- Modify: `Assets/Osman/Scripts/UI/UIManager.cs` (92-132)
- Modify: `Assets/Osman/Scripts/Photons/RoomManager.cs` (133-135)
- Modify: `Assets/Osman/Scripts/GamePlay/SyncAutoTest.Smart.cs` (240-250 `Observe`)
- Modify: `Assets/Osman/Scripts/TILES/TileDistrubite.cs` (`AppendSyncState`: `names=` satırı)

**Interfaces:**
- Consumes: `SeatMath.BotNumber`, `SeatManager.Instance`, `seatOfQue`.
- Produces:
  - `public string SeatManager.SeatName(int que)` — o sıranın koltuğunda `PhotonNetwork.PlayerList`'te bir oyuncu varsa (etkin olmasa da) `NickName`; yoksa `"Bot " + SeatMath.BotNumber(...)`; koltuk bilinmiyorsa `"-"`.
  - Oda özelliği `SeatScores`: `int[5]` (indeks = sıra numarası, 0 kullanılmaz). **Yalnızca master yazar**, `playerScores` her değiştiğinde ve `InitializeScores`'ta.
  - `public static int ScoreManager.SeatScore(int que)` — oda özelliğinden okur; yoksa 0.
- `PlayerScore` oyuncu özelliği hiçbir yerde okunmaz ve yazılmaz: `ScoreManager.cs` 67-70 ve 229-233 silinir, `UpdatePlayerCustomProperties` yerine master'ın `SeatScores` yazması gelir, `RoomManager.cs` 133-135'teki sıfırlama silinir (o metodun kalan satırları aynı kalır).
- `UIManager.UpdateLiveScoreboardText` sıra 1..4 için `SeatName(que): SeatScore(que)` yazar; yerel oyuncunun satırı yeşil. Panel `OnRoomPropertiesUpdate` içinde `SeatScores` değişince yenilenir; `OnPlayerPropertiesUpdate`'teki `PlayerScore` dalı kalkar.
- İsimlik: `UpdateSeatDisplay` 4. adımında koltuk seçilemiyorken (`canPick == false`) oyuncusuz koltuğun yazısı o koltuğun `Bot <n>` adıdır; koltuk seçilebilirken bugünkü gibi "OTUR". Oyun başlayınca ve bir oyuncu odadan çıkınca isimlikler yenilenir (mevcut çağrılar yetmiyorsa çağrı eklenir).
- `SyncAutoTest.Smart.Observe`: master değilse puanları `ScoreManager.SeatScore(1..4)`'ten okur (master bugünkü gibi `playerScores`'tan).
- `AppendSyncState`: `seats=` satırından sonra `names=` satırı (sıra 1..4 için `SeatName`, virgülle).

- [ ] **Step 1: Yukarıdaki değişiklikleri yap.** (Saf kısım `SeatMath.BotNumber` Task 2'de test edildi; bu görevin kanıtı canlı koşudur.)
- [ ] **Step 2: `PlayerScore` kalmadığını doğrula** — Run: `grep -rn "PlayerScore" Assets/Osman/Scripts --include=*.cs` → Expected: çıktı yok.
- [ ] **Step 3: Derleme ve testler** — Run: `Run-EditModeTests.ps1 -Filter SeatMathTests`, `-Filter TestPlayerBrainTests` → Expected: `passed 9/9`, `10/10`.
- [ ] **Step 4: Commit**

```bash
git add Assets/Osman/Scripts/GamePlay/SeatManager.cs Assets/Osman/Scripts/GamePlay/ScoreManager.cs Assets/Osman/Scripts/UI/UIManager.cs Assets/Osman/Scripts/Photons/RoomManager.cs Assets/Osman/Scripts/GamePlay/SyncAutoTest.Smart.cs Assets/Osman/Scripts/TILES/TileDistrubite.cs
powershell -ExecutionPolicy Bypass -File Tools/Security/Check-Secrets.ps1
git commit -m "Seat names and live scores for every seat, bots included"
```

- [ ] **Step 5 [canlı]: Koşular**

Run: `Run-SyncTest.ps1 -Mode smart -Seed 11 -Deals 1` → Expected: `PASS: 1 el, ok 52, desync 0`.
Run: `... -Seed 44 -Deals 1 -Mistakes 0.3` → Expected: `PASS` (cezalar `SeatScores` yolundan geçer; kural denetçisi `[BUG]` basmaz).
Run: `... -Seed 11 -Deals 1 -Bots 2` → Expected: `PASS`, `desync 0`; iki editörün `desync_*.log` dosyalarında `names=` satırı aynı ve iki `Bot` adı içeriyor.

---

### Task 5: El sonu sıra numarasıyla

**Files:**
- Modify: `Assets/Osman/Scripts/GamePlay/GameManager.cs` (411-421, 464-543, 562-600)
- Modify: `Assets/Osman/Scripts/UI/UIManager.cs` (`ShowDetailedGameOver` 363-…, `GetPlayerBySeat` 470-481)
- Modify: `Assets/Osman/Scripts/GamePlay/SyncAutoTest.Smart.cs` (`OnHandResult` 548-582)
- Modify: `Assets/Osman/Scripts/TILES/TileDistrubite.cs` ve `Assets/Osman/Scripts/GamePlay/TileUI.cs` (yalnızca `FinishGameRPC`'ye `-1` gönderen çağrılar değişmeden kalır; doğrulanır)

**Interfaces — Produces:**
- `[PunRPC] public void FinishGameRPC(int winnerQue, bool isOkeyShot, bool isKafa)` — `-1` = kazanan yok. `CheckGameStatus` (bitirenin cihazında çalışır) `data.actorNumber` yerine yerel oyuncunun sıra numarasını gönderir (`tileDistrubite.GetQueueNumberOfPlayer(PhotonNetwork.LocalPlayer)`). `HandData` ve serileştirmesi değişmez.
- `CalculateAndDistributeScores(int winnerQue, …)`: sıra 1..4'ü dolaşır; `isWinner = que == winnerQue`; çarpan `winnerQue != -1` ise `playersWhoOpenedPairs.Contains(winnerQue)` ile.
- `[PunRPC] public void OnGameEndedDetailedRPC(int[] ques, int[] rewards, int[] penalties, int[] netScores)`; sözlükler doğrudan `ques[i]` ile dolar.
- `public static event System.Action<int[], int[], int[], int[]> HandResult` — ilk dizi artık sıra numaraları.
- `UIManager.ShowDetailedGameOver`: dört satırın hepsi dolar; ad `SeatManager.Instance.SeatName(slot.seatNumber)`; "oyuncu bulunamadı" dalı ve kullanılmıyorsa `GetPlayerBySeat` kalkar. Kazanan adı da `SeatName` ile bulunur.
- `SyncAutoTest.Smart.OnHandResult(int[] ques, …)`: her `ques[i]` için `Check($"el sonu que{que}", …)`; oyuncu araması kalkar (bot sıraları da kontrol edilir).

- [ ] **Step 1: Yukarıdaki değişiklikleri yap.**
- [ ] **Step 2: Oyuncu numarasıyla el sonu kalmadığını doğrula** — Run: `grep -n "winnerActorNumber\|GetPlayer(actors" Assets/Osman/Scripts/GamePlay/*.cs` → Expected: çıktı yok.
- [ ] **Step 3: Derleme ve testler** — Run: `Run-EditModeTests.ps1 -Filter RuleOracleTests`, `-Filter TestPlayerBrainTests`, `-Filter SeatMathTests` → Expected: `passed 15/15`, `10/10`, `9/9`.
- [ ] **Step 4: Commit**

```bash
git add Assets/Osman/Scripts/GamePlay/GameManager.cs Assets/Osman/Scripts/UI/UIManager.cs Assets/Osman/Scripts/GamePlay/SyncAutoTest.Smart.cs
powershell -ExecutionPolicy Bypass -File Tools/Security/Check-Secrets.ps1
git commit -m "End of hand by queue number: every seat gets a result row"
```

- [ ] **Step 5 [canlı]: Koşular**

Run: `Run-SyncTest.ps1 -Mode smart -Seed 11 -Deals 1` → Expected: `PASS: 1 el, ok 52, desync 0`.
Run: `... -Seed 11 -Deals 1 -SameNames` → Expected: `PASS`.
Run: `... -Seed 11 -Deals 1 -Bots 2` → Expected: `PASS`; host günlüğünde (`Logs/autotest_*_AT-host.log`) `el sonu que1`…`que4` satırlarının dördü de var ve `[BUG]` yok.
Run: `... -Solo -Mode smart -Seed 11` → Expected: `PASS`.

---

### Task 6: Son kapı, telefon, kayıtlar

**Files:**
- Modify: `C:\Users\osman\.claude\projects\C--Users-osman-Documents-51Okey\memory\smart-bot-findings.md` (Aşama 0 bitti notu), `unity-cli-drive-editors.md` (`-SameNames`, `-Bots`)

- [ ] **Step 1 [canlı]: Tüm EditMode sınıfları** — `OkeyRulesTests`, `RuleOracleTests`, `TestPlayerBrainTests`, `HandSolverTests`, `RackLayoutTests`, `AutoTestJournalTests`, `SyncAutoTestDataTests`, `GameConfigTests`, `LocalServicesTests`, `GameServicesTests`, `SeatMathTests` → Expected: hepsi `passed N/N` (sırasıyla 18, 15, 10, 9, 3, 1, 13, 8, 20, 5, 9).
- [ ] **Step 2 [canlı]: PowerShell testleri** — Run: `powershell -ExecutionPolicy Bypass -Command "Invoke-Pester Tools"` → Expected: `Failed: 0`.
- [ ] **Step 3 [canlı]: Telefon** — `AndroidDevBuild.Build()`, `adb install -r Builds/Android/51Okey-dev.apk`. Expected: `[AndroidDevBuild] Succeeded`, `Success`. Kullanıcı telefondaysa: odayı kurar (1 el, botlar kapalı), `Run-SyncTest.ps1 -JoinRoom <oda>`; per alanları doğru köşelerde, sonuç tablosunda dört satır. Kullanıcı yoksa bu kısım "doğrulanmadı" olarak raporlanır. Telefonun ekran görüntüsü kullanıcıya sorulmadan alınmaz.
- [ ] **Step 4: Bellek dosyalarını güncelle** (yukarıdaki iki dosya).
