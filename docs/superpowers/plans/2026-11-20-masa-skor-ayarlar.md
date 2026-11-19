# Masa: Skor Tablosu, Açma Puanı, Ayarlar Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** El sonu ve maç puanlarını okunur, 1-7 ele uyan tek bir tabloda göstermek; açanların açış puanını isimliklerine yazmak; botlara ad vermek; masaya Ses/Müzik ayarı koymak.

**Architecture:** Master her elin dökümünü saf bir yöntemle (`HandBreakdown.Build`) kurar ve `Hand{n}` oda özelliğine sayı dizisi olarak yazar; koltuk adları `SeatNames` oda özelliğindedir. Her istemcide `MatchScores` bu özellikleri okuyup `MatchHistory`'ye (saf) koyar; `ScoreboardView` tabloyu oradan çizer. Var olan `OnGameEndedDetailedRPC`, `HandResult` ve puan hesabı değişmez.

**Tech Stack:** Unity 6000.3.10f1, uGUI 2.0 + TextMeshPro, Photon PUN 2, NUnit EditMode testleri.

**Spec:** `docs/superpowers/specs/2026-11-20-masa-skor-ayarlar-design.md`

## Global Constraints

- Puan hesabı değişmez: `PenaltySystem.EndPenalty`, `FinishMultiplier`, `CalculateAndDistributeScores` içindeki `netScores`/`PlayerScore` akışı ve `GameManager.HandResult` olayının imzası ile içeriği aynı kalır.
- Her insan oyuncu için `handTotal == netScores` (aynı el). Bitirenin ödülü bitirme çarpanıyla çarpılmaz.
- Yeni ağ verisi yalnızca gösterim içindir; oyun durumuna yazılmaz, `DesyncDetector`'ın karşılaştırdığı alanlara dokunulmaz.
- Oda özellikleri: `Hand1`..`Hand7` (`int[]`), `SeatNames` (`string[4]`, koltuk 1-4 sırasıyla). Yeni RPC: `OpenValueRPC(int que, int value, bool isPairs)`. Yeni `[PunRPC]` eklenince `PhotonServerSettings.RpcList` kaydedilir (`EditorUtility.SetDirty(PhotonNetwork.PhotonServerSettings); AssetDatabase.SaveAssets()`) ve varlık commit edilir.
- Geçmiş koltuk numarasına göre tutulur (que her el değişir). Que→koltuk: `TileDistrubite.seatOfQue` / `SeatMath`.
- Bot adı: hiç insan oturmamış koltuk, botlar arasında koltuk sırasına göre 1'den numaralanır: "Bot 1", "Bot 2", "Bot 3". Yerine bot geçen oyuncu: "{ad} (bot)".
- Bütün oyuncuya görünen metinler `Assets/Resources/Localization.csv`'den (`table.*`, `settings.*`; tr ve en dolu; anahtar kodda çift tırnaklı sabit). `LocText` taşıyan etikete `.text =` yazılmaz.
- Sesler: yeni düğmeler `UiSfx` alır (builder'da `Sound(...)`); oyun koduna eklenen ses satırı akışı değiştirmez.
- `Table.unity`'ye yalnızca `UiKitTable` dokunur; menü sahneleri builder'la üretilir, elle düzenlenmez.
- Dokunulmaz: `Assets/Osman/Scripts/GamePlay/AutoTest/`, `SyncAutoTest*`, `DesyncDetector`.
- Her görev tek commit, Türkçe mesaj, sonunda `Co-Authored-By: Claude <noreply@anthropic.com>`. Push yok.
- Commit'ten önce: `git checkout -- "Assets/Fonts/Changa-Bold SDF.asset" "Assets/Fonts/Changa-ExtraBold SDF.asset"`.
- Kullanıcının kendi değişmiş dosyalarına dokunulmaz: `51Okey.slnx`, `Assets/GooglePlayGames/.../GooglePlayGamesPlugin_v2.2.1.txt`, `ProjectSettings/PackageManagerSettings.asset`, `ProjectSettings/UnityConnectSettings.asset`, `docs/superpowers/plans/2026-11-11-sync-autotest.md`.
- Unity CLI: `C:\Users\osman\AppData\Local\Unity\bin\unity.exe command <grup> <komut> --project-path F:/Documents/UnityProjects/51Okey --result-only --no-banner`; metin sabiti içeren kod `script eval --file <yol>` ile.
- Play modunda: Google girişi, çıkış, profil adı değişikliği, satın alma, hesap silme çağrılmaz.

### `Hand{n}` dizisinin biçimi (bağlayıcı)

Baş (4 sayı): `handIndex`, `winnerSeat` (berabere 0), `finishMultiplier`, `flags` (bit0 okey ile, bit1 çiftten, bit2 kafa).
Sonra koltuk başına 7 sayı, koltuk sırasıyla (boş koltuk yazılmaz): `seat`, `opened` (0/1), `tilesLeft`, `handPoints`, `inHandPenalty`, `finishReward`, `handTotal`.
Uzunluk = 4 + 7 x (dolu koltuk sayısı).

## Review Focus

1. Aynı el iki kez hesaplanır (`FinishGameRPC` iki kez) → toplam iki katına çıkmamalı (Görev 1 `MatchHistory` testi).
2. Bir koltuk boş, bozuk ya da kısa dizi gelir → tablo çökmeden o satırı atlamalı (Görev 1 çözme testi).
3. 7 el, dört basamaklı eksi sayılar ("-1200"), uzun ad ("Misafir-58823 (bot)") → sütunlar taşmamalı (Görev 4 ekran görüntüsü, sahte veri).
4. Oyuncu açtıktan sonra öbür açma düğmesine yanlışlıkla basar → isimlikteki açış puanı ve baraj gerçek açış puanı kalmalı (Görev 3 testi).
5. Skor paneli açıkken sıradaki el başlar ya da masa kapanır → panel kapanmalı, geri sayım eksiye düşmemeli, eski panel kodundan null hatası çıkmamalı (Görev 4).

---

## Dosya yapısı

| Dosya | Sorumluluk |
|---|---|
| `Assets/Osman/Scripts/GamePlay/Score/HandBreakdown.cs` (yeni) | Bir elin satırlarını kurar; diziye çevirir ve diziden çözer. Saf C#. |
| `Assets/Osman/Scripts/GamePlay/Score/MatchHistory.cs` (yeni) | Eller, toplamlar, önde olan. Saf C#. |
| `Assets/Osman/Scripts/GamePlay/Score/SeatNaming.cs` (yeni) | Koltuk adları (bot numaralama, "(bot)" eki). Saf C#. |
| `Assets/Osman/Scripts/GamePlay/Score/MatchScores.cs` (yeni) | Oda özelliklerini okur/yazar, ceza kaydı tutar, değişince olay yayar. |
| `Assets/Osman/Scripts/UI/Table/ScoreboardView.cs` (yeni) | Tabloyu çizer, geri sayım. |
| `Assets/Osman/Scripts/UI/Table/OpenBadges.cs` (yeni) | İsimliklerde açış puanı etiketi. |
| `Assets/Osman/Scripts/UI/Table/TableSettings.cs` (yeni) | Masadaki ayarlar penceresi. |
| `Assets/Osman/Editor/UiKitTable.cs` | Paneli, etiketleri, ayarlar düğmesi ve penceresini kurar; eski paneli kaldırır. |
| `Assets/Osman/Editor/Tests/ScoreTests.cs` (yeni) | Testler. |
| `GameManager.cs`, `ScoreManager.cs`, `SeatManager.cs`, `UIManager.cs`, `UiKitLobby.cs` | Bağlantı noktaları. |

---

### Task 0: Önkoşul — ikinci elde masa sahibinin takılması

Çok elli maçın çalışması bu hataya bağlı; skor tablosunun "eller" bölümü onsuz denenemez.

**Bilinen hata (2026-11-11'de senkron testi buldu, düzeltilmedi; doğrulanmalı, kod o günden beri değişti):**
`GameManager.RPC_PrepareNextRound` dört istemcinin hepsinde çalışır ve her biri
`RPC_ResetTurnForNewRound`'u `RpcTarget.All` ile yeniden yollar; her istemci dört sıfırlama alır.
Masa sahibinde bunların üçü, `StartRoundVisuals` `canDrop = true` yaptıktan sonra gelir ve
`ResetTurnFlags` onu siler. Masa sahibi o elde 1. sıradaysa 15 taşla ve `canDrop == false` ile
kalır: çekemez, atamaz, oyun donar. Yeniden üretim: `Run-SyncTest.ps1 -Deals 2 -Seed 1234`.

**Files:**
- Modify: `Assets/Osman/Scripts/GamePlay/GameManager.cs` (`RPC_PrepareNextRound` ve çevresi)

- [ ] **Step 1: Yeniden üret** (kontrolcü koşar, dört editör): `Run-SyncTest.ps1 -Deals 2 -Seed 1234`. Donma ve günlükteki kanıt (hangi editör, hangi sıra, `canDrop`) rapora yazılır. Üretilemezse: kod okunarak hatanın hâlâ var olup olmadığı gösterilir; yoksa görev "gerek kalmadı" diye kapanır.
- [ ] **Step 2: Kök neden** koddan doğrulanır: `RPC_ResetTurnForNewRound`'u kim, kaç kez yolluyor; sıfırlama ile `StartRoundVisuals` hangi sırayla geliyor.
- [ ] **Step 3: En küçük düzeltme:** sıfırlama mesajını yalnızca masa sahibi yollar (ya da her istemci kendi bayraklarını mesajsız, yerelde sıfırlar); sıra, el hazırlığı ve diğer istemcilerin davranışı değişmez. Başka bir şey düzeltilmez.
- [ ] **Step 4: Doğrula:** `-Deals 2 -Seed 1234` → `PASS`, `desync 0`; ayrıca `-Deals 2` (tohumsuz) ve `-Deals 3 -Mode smart` → `PASS`. Tam EditMode paketi geçer.
- [ ] **Step 5: Commit** — `İkinci elde masa sahibinin takılması düzeltildi (el sıfırlama mesajı bir kez)`

---

### Task 1: Saf veri (HandBreakdown, MatchHistory, SeatNaming)

**Files:**
- Create: `Assets/Osman/Scripts/GamePlay/Score/HandBreakdown.cs`, `MatchHistory.cs`, `SeatNaming.cs`
- Test: `Assets/Osman/Editor/Tests/ScoreTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public struct HandRow { public int seat, tilesLeft, handPoints, inHandPenalty, finishReward, handTotal; public bool opened; }
  public sealed class HandResultData {
      public int handIndex, winnerSeat, finishMultiplier; public bool okeyFinish, pairsFinish, kafa;
      public List<HandRow> rows;                    // koltuk sırasıyla
  }
  public struct SeatInput { public int seat; public bool opened, openedPairs; public int normalTiles, okeys, inHandPenalty; }

  public static class HandBreakdown {
      // endPenalty: (isWinner, opened, openedPairs, normalTiles, okeys) -> PenaltySystem.EndPenalty'nin döndürdüğü değer (çarpanlar dahil)
      public static HandResultData Build(int handIndex, int winnerSeat, int finishMultiplier, bool okeyFinish, bool pairsFinish, bool kafa,
                                         IReadOnlyList<SeatInput> seats, Func<SeatInput, bool, int> endPenalty);
      public static int[] Encode(HandResultData d);
      public static HandResultData Decode(int[] a);   // null, kısa ya da bozuk dizi: null döner; eksik kalan son satır atılır
  }

  public sealed class MatchHistory {
      public void Set(HandResultData d);               // handIndex ile yerine yazar
      public IReadOnlyList<HandResultData> Hands { get; }   // handIndex sırasıyla
      public HandResultData Last { get; }
      public int Total(int seat);
      public int LeaderSeat { get; }                   // en düşük toplam; eşitlikte küçük koltuk; el yoksa 0
      public void Clear();
  }

  public static class SeatNaming {
      // humanNames[i]: koltuk i+1'deki insanın adı (hiç oturmadıysa null); botNow[i]: o koltukta şu an bot mu oynuyor
      public static string[] Names(string[] humanNames, bool[] botNow);
  }
  ```
- `Build` kuralı: bitiren koltuk için `finishReward = endPenalty(row, true)` (eksi sayı), `handPoints = 0`; diğerleri için `handPoints = endPenalty(row, false)`, `finishReward = 0`; `tilesLeft = normalTiles + okeys`; `handTotal = handPoints + inHandPenalty + finishReward`. `winnerSeat == 0` ise herkes kaybeden yoludur.
- `SeatNaming`: `humanNames[i] == null` → "Bot k" (k: böyle koltuklar arasında koltuk sırasıyla 1'den); `humanNames[i] != null && botNow[i]` → `"{ad} (bot)"`; aksi halde ad. "(bot)" eki ve "Bot" sözcüğü çevrilmez.

- [ ] **Step 1: Başarısız testleri yaz**

```csharp
static SeatInput S(int seat, bool opened, bool pairs, int tiles, int okeys, int pen) =>
    new SeatInput { seat = seat, opened = opened, openedPairs = pairs, normalTiles = tiles, okeys = okeys, inHandPenalty = pen };
// Sahte ceza işlevi: bitiren -600; açmış tiles*40 + okeys*250; açmamış 1200
static int Fake(SeatInput s, bool winner) => winner ? -600 : s.opened ? s.normalTiles * 40 + s.okeys * 250 : 1200;

[Test] public void Build_Winner_Opened_NotOpened() {
    var d = HandBreakdown.Build(3, 1, 2, true, false, false,
        new[] { S(1, true, false, 0, 0, 0), S(2, true, false, 6, 0, 250), S(3, false, false, 14, 0, 0), S(4, true, false, 2, 1, 70) }, Fake);
    Assert.AreEqual(-600, d.rows[0].finishReward); Assert.AreEqual(0, d.rows[0].handPoints); Assert.AreEqual(-600, d.rows[0].handTotal);
    Assert.AreEqual(240, d.rows[1].handPoints); Assert.AreEqual(250, d.rows[1].inHandPenalty); Assert.AreEqual(490, d.rows[1].handTotal);
    Assert.AreEqual(1200, d.rows[2].handTotal); Assert.IsFalse(d.rows[2].opened); Assert.AreEqual(14, d.rows[2].tilesLeft);
    Assert.AreEqual(3, d.rows[3].tilesLeft); Assert.AreEqual(80 + 250 + 70, d.rows[3].handTotal);
}
[Test] public void Build_Draw_Has_No_Winner_Row() {
    var d = HandBreakdown.Build(1, 0, 1, false, false, false, new[] { S(1, true, false, 3, 0, 0), S(2, false, false, 14, 0, 0) }, Fake);
    Assert.IsTrue(d.rows.All(r => r.finishReward == 0)); Assert.AreEqual(120, d.rows[0].handTotal);
}
[Test] public void Encode_Decode_Round_Trip() {
    var d = HandBreakdown.Build(2, 4, 4, true, true, true, new[] { S(1, true, true, 5, 0, 30), S(3, false, false, 14, 0, 0), S(4, true, false, 0, 0, 0) }, Fake);
    var e = HandBreakdown.Decode(HandBreakdown.Encode(d));
    Assert.AreEqual(4 + 7 * 3, HandBreakdown.Encode(d).Length);
    Assert.AreEqual(2, e.handIndex); Assert.AreEqual(4, e.winnerSeat); Assert.AreEqual(4, e.finishMultiplier);
    Assert.IsTrue(e.okeyFinish && e.pairsFinish && e.kafa);
    CollectionAssert.AreEqual(new[] { 1, 3, 4 }, e.rows.Select(r => r.seat));
    Assert.AreEqual(d.rows[0].handTotal, e.rows[0].handTotal); Assert.IsFalse(e.rows[1].opened);
}
[Test] public void Decode_Bad_Input_Is_Null_Or_Drops_Partial_Row() {
    Assert.IsNull(HandBreakdown.Decode(null)); Assert.IsNull(HandBreakdown.Decode(new int[2]));
    var good = HandBreakdown.Encode(HandBreakdown.Build(1, 0, 1, false, false, false, new[] { S(1, true, false, 3, 0, 0), S(2, false, false, 14, 0, 0) }, Fake));
    var cut = good.Take(good.Length - 3).ToArray();
    Assert.AreEqual(1, HandBreakdown.Decode(cut).rows.Count);
}
[Test] public void History_Replaces_Same_Hand_And_Sums() {
    var h = new MatchHistory();
    var a = HandBreakdown.Build(1, 1, 1, false, false, false, new[] { S(1, true, false, 0, 0, 0), S(2, true, false, 5, 0, 0) }, Fake);
    h.Set(a); h.Set(a);                                   // aynı el iki kez
    Assert.AreEqual(1, h.Hands.Count); Assert.AreEqual(-600, h.Total(1)); Assert.AreEqual(200, h.Total(2));
    h.Set(HandBreakdown.Build(2, 2, 1, false, false, false, new[] { S(1, false, false, 14, 0, 0), S(2, true, false, 0, 0, 0) }, Fake));
    Assert.AreEqual(600, h.Total(1)); Assert.AreEqual(-400, h.Total(2)); Assert.AreEqual(2, h.LeaderSeat); Assert.AreEqual(2, h.Last.handIndex);
}
[Test] public void History_Out_Of_Order_Hands_Are_Sorted_And_Tie_Goes_To_Lower_Seat() {
    var h = new MatchHistory();
    h.Set(HandBreakdown.Build(2, 0, 1, false, false, false, new[] { S(1, true, false, 5, 0, 0), S(2, true, false, 5, 0, 0) }, Fake));
    h.Set(HandBreakdown.Build(1, 0, 1, false, false, false, new[] { S(1, true, false, 5, 0, 0), S(2, true, false, 5, 0, 0) }, Fake));
    CollectionAssert.AreEqual(new[] { 1, 2 }, h.Hands.Select(x => x.handIndex)); Assert.AreEqual(1, h.LeaderSeat);
    Assert.AreEqual(0, new MatchHistory().LeaderSeat); Assert.AreEqual(0, h.Total(9));
}
[Test] public void History_Seven_Hands() {
    var h = new MatchHistory();
    for (int i = 1; i <= 7; i++) h.Set(HandBreakdown.Build(i, 0, 1, false, false, false, new[] { S(1, true, false, 1, 0, 0) }, Fake));
    Assert.AreEqual(7, h.Hands.Count); Assert.AreEqual(280, h.Total(1));
}
[Test] public void Names_Bots_Are_Numbered_Among_Bots_And_Leavers_Keep_Name() {
    CollectionAssert.AreEqual(new[] { "Bot 1", "Ayşe", "Bot 2", "Can (bot)" },
        SeatNaming.Names(new[] { null, "Ayşe", null, "Can" }, new[] { true, false, true, true }));
    CollectionAssert.AreEqual(new[] { "A", "B", "C", "D" }, SeatNaming.Names(new[] { "A", "B", "C", "D" }, new bool[4]));
}
```

- [ ] **Step 2: Düştüğünü gör.** `U test run --mode EditMode --filter ScoreTests`
- [ ] **Step 3: Üç sınıfı yaz.**
- [ ] **Step 4: Testler ve tam paket geçer.**
- [ ] **Step 5: Commit** — `Skor: el dökümü, maç geçmişi ve koltuk adları (saf veri)`

---

### Task 2: Master dökümü yazar, istemciler okur; bot adları; ceza kaydı

**Files:**
- Create: `Assets/Osman/Scripts/GamePlay/Score/MatchScores.cs`
- Modify: `Assets/Osman/Scripts/GamePlay/GameManager.cs` (`CalculateAndDistributeScores`, `ShowPenaltyRPC`), `Assets/Osman/Scripts/GamePlay/SeatManager.cs` (isimlikler, koltuk adları), `Assets/Osman/Editor/UiKitTable.cs` (bileşeni sahneye ekler), `Assets/Osman/Scenes/Table.unity`
- Test: `Assets/Osman/Editor/Tests/ScoreTests.cs` (ek)

**Interfaces:**
- Consumes: Görev 1'in tamamı; `PenaltySystem.EndPenalty`, `FinishMultiplier`; `TileDistrubite.seatOfQue`, `GetPlayerTilesForBot(que)`; `ScoreManager.playerScores`, `playersWhoOpened`, `playersWhoOpenedPairs`.
- Produces:
  ```csharp
  public class MatchScores : MonoBehaviourPunCallbacks {
      public static MatchScores Instance { get; }
      public MatchHistory History { get; }
      public string[] SeatNames { get; }                     // koltuk 1-4; yoksa boş dize
      public int TotalRounds { get; }
      public event Action Changed;                           // el ya da adlar değişince
      public event Action<HandResultData> HandArrived;       // yeni bir elin dökümü ilk geldiğinde
      public struct PenaltyEntry { public int seat, amount; public PenaltyReason reason; }
      public IReadOnlyList<PenaltyEntry> CurrentHandPenalties { get; }   // süren el; el dökümü gelince o ele taşınır
      public IReadOnlyList<PenaltyEntry> PenaltiesOf(int handIndex);
      public void RecordPenalty(int que, int amount, PenaltyReason reason);   // ShowPenaltyRPC'den
      // master
      public void WriteHand(HandResultData d);               // Hand{n} oda özelliği
      public void WriteSeatNames(string[] names);            // SeatNames oda özelliği
  }
  ```
- `CalculateAndDistributeScores` (yalnız master): var olan döngü ve `OnGameEndedDetailedRPC` gönderimi **değişmez**. Onların önüne: koltuk 1-4 için `SeatInput` kur (que = o koltuğun sırası; taşlar `GetPlayerTilesForBot(que)`'dan, okey sayımı `PenaltySystem`'deki mevcut sayımla aynı yöntemle; açtı/çift bilgisi `playersWhoOpened`/`playersWhoOpenedPairs`; el içi ceza `playerScores[que]`), `HandBreakdown.Build(...)` çağır (`endPenalty` olarak `PenaltySystem.EndPenalty`'yi mevcut argümanlarıyla saran bir lambda), `MatchScores.Instance.WriteHand(...)`. `handIndex` = `CurrentRound` oda özelliği.
- Koltuk adları: master, el başında (koltuklar kilitlenip sıralar dağıtıldığı yerde) ve bir oyuncu ayrılıp yerine bot geçtiğinde `SeatNaming.Names(...)` ile hesaplayıp `WriteSeatNames` çağırır. İnsan adları, o koltuğa oturmuş son insanın adıdır (ayrılsa da saklanır).
- İsimlikler: `SeatManager.UpdateSeatDisplay` insan olmayan koltukta, oyun başladıktan sonra, `MatchScores.SeatNames`'teki adı yazar ("Bot 1", "Can (bot)"). Oturma öncesi "OTUR" davranışı değişmez.
- `ShowPenaltyRPC`: `ShowPenaltyFloat` çağrısından **önce** `MatchScores.Instance?.RecordPenalty(playerQue, amount, (PenaltyReason)reason)`.
- Her istemci, yeni bir elin dökümü gelince tek satır loglar: `[SKOR] el {n}: 1={t1} 2={t2} 3={t3} 4={t4}` (koltuk toplamları; boş koltuk `-`).
- Değişmez (test): gerçek `PenaltySystem.EndPenalty` ile kurulan satırda, insan koltuğu için `handTotal == inHandPenalty + EndPenalty(...)` yani `netScores`.

- [ ] **Step 1: Başarısız testleri yaz**

```csharp
[Test] public void HandTotal_Equals_Todays_Net_With_Real_Penalty_Rules() {
    // PenaltySystem.EndPenalty'nin gerçek imzasını kullan; renk çarpanı 4, bitirme çarpanı 2
    // bitiren (koltuk 1), açmış 6 taşlı (koltuk 2, el içi 250), açmamış (koltuk 3), çiftle açmış 4 taş + 1 okey (koltuk 4, el içi 70)
    // her koltuk için: beklenen = elİçiCeza + PenaltySystem.EndPenalty(...)   ← GameManager'ın bugün netScores için yaptığı toplama
    // Assert.AreEqual(beklenen, row.handTotal) dört koltukta
}
[Test] public void Draw_With_Real_Rules_Everyone_Pays_Loser_Formula() { /* winnerSeat 0; hiçbir satırda finishReward yok */ }
```
(`EndPenalty` imzası kodda okunup testler ona göre tamamlanır; sayılar testte açıkça hesaplanır, formül kopyalanmaz: beklenen değerler elle yazılmış sabitlerdir.)

- [ ] **Step 2: Düştüğünü gör; `MatchScores`'u ve bağlantıları yaz; `UiKitTable` bileşeni sahneye ekler** (var olan kalıcı bir masa nesnesine; ikinci çalıştırmada çoğaltmaz).
- [ ] **Step 3:** Tam EditMode paketi geçer.
- [ ] **Step 4:** Tek başına masa testi (`Run-SyncTest.ps1 -Solo -Mode smart -Seed 11`) `PASS`; Editor log'unda `[SKOR] el 1:` satırı var ve test oyuncusunun satırındaki toplam, günlüğündeki `net` ile aynı.
- [ ] **Step 5:** Play modunda botlu masa: isimliklerde "Bot 1", "Bot 2", "Bot 3" okunur (`script eval` ile `seatTextFields` metinleri).
- [ ] **Step 6: Commit** — `Skor: el dökümü oda özelliğinde, bot adları, ceza kaydı`

---

### Task 3: Açış puanı (isimlik etiketi) ve baraj düzeltmesi

**Files:**
- Create: `Assets/Osman/Scripts/UI/Table/OpenBadges.cs`
- Modify: `Assets/Osman/Scripts/GamePlay/ScoreManager.cs` (`PlaceValidPers`, `PlacePairPers`, `CommitFinalTableLimit`, `ResetPlayerOpenStatus`), `Assets/Osman/Scripts/GamePlay/GameManager.cs` (`OpenValueRPC`), `Assets/Osman/Editor/UiKitTable.cs`, `Assets/Osman/Scenes/Table.unity`, `Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings.asset`, `Assets/Resources/Localization.csv`
- Test: `Assets/Osman/Editor/Tests/ScoreTests.cs` (ek)

**Interfaces:**
- Produces:
  ```csharp
  // ScoreManager
  public int OpenedValueThisTurn { get; }      // bu turda gerçekten yerleşen ilk açışın puanı; açış yoksa 0
  public bool OpenedPairsThisTurn { get; }
  // GameManager
  [PunRPC] void OpenValueRPC(int que, int value, bool isPairs);     // RpcTarget.All
  public static event Action<int, int, bool> OpenValueShown;        // que, value, isPairs
  // saf yardımcı (test edilir)
  public static class OpenValue { public static int Pick(int current, int placedScore); }   // current 0 ise placedScore, değilse current
  ```
- `OpenedValueThisTurn`, `PlaceValidPers` içinde `hasOpenedAnyMeld`'in doğru olduğu yerde seri toplamıyla (`totalScore`), `PlacePairPers` içinde `hasOpenedAnyPair`'in doğru olduğu yerde çift toplamıyla (`pairTotalScore`) `OpenValue.Pick` üzerinden yazılır; tur sonunda (`tempOpenedScore`'un sıfırlandığı yerde) sıfırlanır.
- `CommitFinalTableLimit`: `FirstOpenThisTurn` ise `TryUpdateTableLimit(tempOpenedScore)` yerine `TryUpdateTableLimit(OpenedValueThisTurn)`; aynı yerde `photonView`/`GameManager` üzerinden `OpenValueRPC(myQue, OpenedValueThisTurn, OpenedPairsThisTurn)` gönderilir. `OpenedValueThisTurn == 0` ise ikisi de yapılmaz (bugünkü davranışla aynı: açış yoksa baraj değişmez). `tempOpenedScore`'un diğer kullanımları değişmez.
- `OpenBadges`: `GameManager.OpenValueShown`'a abone; que→isimlik indeksini `UIManager`'ın tur göstergeleriyle aynı bağıl kuralla bulur; isimliğin sağındaki etikete `Loc.T("table.opened_runs", value)` / `Loc.T("table.opened_pairs", value)` yazar. `ResetPlayerOpenStatus`'ta (yeni el) etiketler boşalır. Etiketleri `UiKitTable` dört isimliğe ekler (düz `Text`, `LocText` yok; boş başlar).
- Tablo satırları: `table.opened_runs` = `Açtı {0}` / `Opened {0}`; `table.opened_pairs` = `Çift {0}` / `Pairs {0}`.

- [ ] **Step 1: Başarısız testleri yaz**

```csharp
[Test] public void OpenValue_First_Placement_Wins() {
    Assert.AreEqual(68, OpenValue.Pick(0, 68));
    Assert.AreEqual(68, OpenValue.Pick(68, 24));     // aynı turda sonradan eklenen değeri bozmaz
    Assert.AreEqual(0, OpenValue.Pick(0, 0));
}
```
- [ ] **Step 2: Düştüğünü gör; yaz; RPC listesini kaydet** (`PhotonServerSettings` varlığı değişmiş olmalı; `git diff --stat` ile göster).
- [ ] **Step 3:** Tam EditMode paketi geçer (dil denetimi dahil).
- [ ] **Step 4: Play modunda botlu masa**, yerel oyuncu olarak: seri açılabilir bir el beklemek yerine `script eval` ile `GameManager.Instance` üzerinden `OpenValueRPC`'yi kendi sıranla (68, false) ve başka bir sırayla (5... 72, true) tetikle; dört isimlikteki etiket metinlerini oku ("Opened 68" / "Pairs 72" İngilizcede). Sonra yanlış basma senaryosu için `OpenValue.Pick` testi yeterlidir; ayrıca `CommitFinalTableLimit`'in artık `tempOpenedScore` okumadığını `git grep` ile göster.
- [ ] **Step 5:** Tek başına masa testi `PASS` (test oyuncusu açar; günlükte baraj beklentisi hatası yok).
- [ ] **Step 6: Commit** — `Masa: açış puanı isimlikte; baraj gerçekten açılan puandan`

---

### Task 4: Skor tablosu ekranı

**Files:**
- Create: `Assets/Osman/Scripts/UI/Table/ScoreboardView.cs`
- Modify: `Assets/Osman/Editor/UiKitTable.cs`, `Assets/Osman/Scenes/Table.unity`, `Assets/Osman/Scripts/UI/UIManager.cs`, `Assets/Osman/Scripts/GamePlay/GameManager.cs` (`RPC_PrepareNextRound` içindeki eski panel satırı, `OnGameEndedDetailedRPC` içindeki eski gösterim çağrısı), `Assets/Resources/Localization.csv`
- Test: `Assets/Osman/Editor/Tests/ScoreTests.cs` (ek)

**Interfaces:**
- Consumes: `MatchScores` (Görev 2), `MatchHistory`, `Loc`, `PenaltyReasonText.Of`, kit prefabları (`Window`, `Btn_Close`), `Label`/`SetLabel`/`Sound`.
- Produces:
  ```csharp
  public class ScoreboardView : MonoBehaviour {
      public void ShowHandEnd(HandResultData d);     // el sonu: kapatılamaz, geri sayım başlar
      public void Toggle();                          // TABLO düğmesi: el içinde aç/kapa
      public void Hide();
  }
  // saf metin yardımcıları (test edilir), ScoreboardText statik sınıfı:
  public static string Signed(int v);                // 0 -> "0", 490 -> "+490", -600 -> "-600"
  public static string Dash(int v);                  // 0 -> "-", değilse Signed
  public static int Countdown(float startedAt, float now, int from);   // 10..0, eksiye düşmez
  ```
- Düzen (bağlayıcı içerik, ölçü serbest): üst bölüm "bu el" tablosu (sütunlar: OYUNCU, ELDE KALAN, CEZALAR, BİTİRME, BU EL), altında yerel oyuncunun ceza dökümü satırı, ayırıcı, alt bölüm eller tablosu (OYUNCU, 1..`TotalRounds`. EL, TOPLAM), en altta geri sayım satırı. Spec'teki taslak ve kurallar aynen: yerel oyuncu "Sen"/"You" ve vurgulu; eksi yeşil, artı kırmızı; "önde" işareti; oynanmamış el "."; ELDE KALAN hücresi `{n} taş {puan}` ya da "açmadı {puan}", bitirende "-".
- Başlık metinleri: el sonu `table.hand_over` ("{0}. EL BİTTİ") + bitiren varsa `table.finished_by` ("{0} bitirdi") + ekler `table.with_okey`, `table.from_pairs`, `table.kafa`, çarpan `x{k}` (çarpan 1 ise yazılmaz); berabere `table.pile_empty` ("Taşlar bitti"); son elden sonra `table.match_over` ("MAÇ BİTTİ · Kazanan: {0}"). El içinde açılınca başlık `table.scoreboard_title` ("SKOR TABLOSU").
- Geri sayım: `ShowHandEnd` anında başlar, `table.next_hand_in` ("Sonraki el: {0} sn") ya da son elde `table.back_to_lobby_in`; 0'da durur. `RPC_PrepareNextRound` `Hide()` çağırır. Bekleme süresi ve ele geçiş mantığı değişmez.
- Açılış: `MatchScores.HandArrived` → `ShowHandEnd`. TABLO düğmesinin kalıcı çağrısı `ScoreboardView.Toggle`'a bağlanır (`UiKitTable`), `UiSfx` kalır. El sonu açıkken `Toggle` etkisizdir.
- Kaldırılanlar (sahne ve kod): `ScorePanel` ve altındakiler, `CurrentScorePanel`, `UIManager`'daki ilgili alanlar ve yöntemler (`gameOverPanel`, sütun dizileri, `rankingText`, `restartTimerText`, `ShowDetailedGameOver`, `TimerRoutine`, `ToggleLiveScoreboard`, canlı ceza listesi, `PlayerScore` değişiminde canlı listeyi yenileyen kod), `GameManager.RPC_PrepareNextRound` içindeki `gameOverPanel.SetActive(false)` (yerine `ScoreboardView` gizlenir), `UiKitTable.TableLabels` içindeki "TEKRAR OYNA" girişi, `Localization.csv`'de artık kullanılmayan anahtarlar (`table.play_again`, `table.live_penalties`, `table.winner` vb.; denetim kullanılmayanları listeler). `GameManager.HandResult` olayı ve `OnGameEndedDetailedRPC` gönderimi/alımı **kalır**; yalnızca eski panele çizen çağrı kalkar.
- `UiKitTable` paneli `SafeArea` altında, ortaya çapalı kurar (köşe çapası kullanılmaz); satır ve hücreler çalışma zamanında `ScoreboardView` tarafından doldurulur (4 satır x en çok 9 sütun için hazır hücreler builder'da kurulur, kullanılmayan sütun gizlenir). Menü ikinci kez çalışınca çoğaltmaz, eski nesneleri yeniden aramaz.

- [ ] **Step 1: Başarısız testleri yaz**

```csharp
[Test] public void Signed_And_Dash() {
    Assert.AreEqual("+490", ScoreboardText.Signed(490)); Assert.AreEqual("-600", ScoreboardText.Signed(-600)); Assert.AreEqual("0", ScoreboardText.Signed(0));
    Assert.AreEqual("-", ScoreboardText.Dash(0)); Assert.AreEqual("+70", ScoreboardText.Dash(70));
}
[Test] public void Countdown_Clamps_At_Zero() {
    Assert.AreEqual(10, ScoreboardText.Countdown(100f, 100f, 10)); Assert.AreEqual(7, ScoreboardText.Countdown(100f, 102.5f, 10));
    Assert.AreEqual(0, ScoreboardText.Countdown(100f, 130f, 10));
}
```
- [ ] **Step 2: Düştüğünü gör; `ScoreboardView`'u, builder kısmını ve temizliği yaz; `Masa Metinlerini Bağla` menüsünü çalıştır.**
- [ ] **Step 3:** Tam EditMode paketi geçer; `Okey/Dil/Tabloyu Denetle` hata vermez (kullanılmayan anahtar uyarıları temizlenmiş).
- [ ] **Step 4: Ekran görüntüleri** (`.superpowers/shots-keep/masa/`; hepsine bak): Play modunda botlu masada `script eval` ile `MatchScores.Instance.History`'ye sahte eller koyup (`HandBreakdown.Build` + `Set`) `ScoreboardView.ShowHandEnd` çağır:
  - 3. el sonu, 5 ellik maç, Türkçe, 1920x1080 (spec'teki örnek sayılar, "Okey attı +250" dökümü dahil)
  - aynı, İngilizce
  - 7. el sonu, 7 ellik maç, en uzun değerler: adlar "Misafir-58823 (bot)", toplamlar "-12600"/"+12600"; 1920x1080 ve 2400x1080
  - berabere el başlığı; maç sonu başlığı
  - el içinde TABLO ile açılmış hali
  Her biri için: taşan, kesilen, üst üste binen, okunmayacak kadar küçülen metin var mı, yaz. Varsa düzeni düzelt (metni kısaltmak yerine sütun genişliği/yazı boyutu).
- [ ] **Step 5:** Tek başına masa testi `PASS`; el sonunda panelin açıldığı ve 10 sn sonra kapandığı (tek ellik masada lobiye dönüldüğü) Editor log'undan doğrulanır; konsolda null hatası yok.
- [ ] **Step 6: Commit** — `Masa: yeni skor tablosu (el dökümü, eller ve toplam, geri sayım); eski panel kaldırıldı`

---

### Task 5: Masada ayarlar ve lobide 7 el

**Files:**
- Create: `Assets/Osman/Scripts/UI/Table/TableSettings.cs`
- Modify: `Assets/Osman/Editor/UiKitTable.cs`, `Assets/Osman/Editor/UiKitLobby.cs` (`RoundOptions`), `Assets/Osman/Scenes/Table.unity`, `Assets/Osman/Scenes/LobbyMenu.unity`, `Assets/Resources/Localization.csv`

**Interfaces:**
- Produces:
  ```csharp
  public class TableSettings : MonoBehaviour {
      public UnityEngine.UI.Toggle soundToggle, musicToggle;
      public void Open(); public void Close();
      public void OnSoundToggle(bool on);   // GameSettings.SoundOn
      public void OnMusicToggle(bool on);   // GameSettings.MusicOn
  }
  ```
- `UiKitTable`: Çıkış düğmesinin (`"Çıkış Butonu"`) bulunduğu kapta, aynı çapayla, sağına kit `Btn_Teal` örneği "Ayarlar" düğmesi (etiket `lobby.settings` anahtarı varsa o; yoksa `settings.title`'ın küçük harfli karşılığı için yeni anahtar `table.settings`), aynı yükseklikte; `UiSfx` varsayılanı. Pencere: kit `Window`, başlık `settings.title`, içinde tek `HalfRow` + iki `ToggleRow` (`settings.sound`, `settings.music`; lobide kullanılan anahtarlarla aynı), X. `UiWindow` geri tuşuyla kapatır (`closeOnBack` lobideki ayarlarla aynı düzen). Açılınca anahtarlar `GameSettings`'ten okunur.
- `UiKitLobby.RoundOptions`: sahneden geri okumak yerine sabit `{1,2,3,4,5,6,7}`. Lobi yeniden üretilir.

- [ ] **Step 1: Yaz; menüleri çalıştır** (`Masa Metinlerini Bağla`, `Lobiyi Kur`); konsol temiz.
- [ ] **Step 2:** Tam EditMode paketi geçer. Lobi sahnesinde yerleşim değerleri değişmedi (çoklu küme karşılaştırması), yalnızca el seçenekleri dizisi 7 öğe.
- [ ] **Step 3: Play modunda:** lobide el sayısı oku 7'ye kadar çıkar (`ChangeRounds` çağırıp değeri oku). Botlu masada Ayarlar düğmesinin `Open`'ını çağır; ekran görüntüsü tr/en; `OnSoundToggle(false)` sonrası `AudioListener.volume == 0`, tekrar açınca 1; `UIRoot.HandleBack()` pencereyi kapatır. Ayarı başladığı haline getir.
- [ ] **Step 4: Commit** — `Masa: ayarlar (ses, müzik); lobide el sayısı 7'ye kadar`

---

### Task 6: Bütünün doğrulanması

**Files:**
- Modify: `docs/superpowers/specs/2026-11-20-masa-skor-ayarlar-design.md` (sona "Uygulamadan devreden notlar")

- [ ] **Step 1:** Tam EditMode paketi; sayı.
- [ ] **Step 2:** `Okey/Dil/Tabloyu Denetle` ve `Okey/Ses/Ses Listesini Denetle`: hata yok.
- [ ] **Step 3: Çok editörlü senkron testi, iki el** (kontrolcü koşar): `Run-SyncTest.ps1 -Deals 2 -Mode smart` → `PASS`, `desync 0`; dört editörün log'undaki `[SKOR] el 1:` ve `[SKOR] el 2:` satırları birebir aynı; ikinci eldeki toplam = iki elin `net` toplamı.
- [ ] **Step 4:** Notlar: yeni oda özellikleri ve RPC, kaldırılan nesneler, Claude'un yalnız verdiği kararlar, bilinen açıklar (master değişimi, geri alıp yeniden açma), telefonda denenecekler (2 ellik gerçek maç; 7 ellik masada tablo; açış etiketi; masada ses kapatma; bot adları).
- [ ] **Step 5: Commit** — `Masa skor tablosu: doğrulama notları`
