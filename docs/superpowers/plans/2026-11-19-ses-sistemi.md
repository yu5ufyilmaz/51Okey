# Ses Sistemi Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Her oyun olayına kendi sesini bağlamak; sesler Unity'de tek bir "Ses Listesi" dosyasından eşlenir ve koda dokunmadan değiştirilir.

**Architecture:** `Sfx.Play(SfxId)` tek giriş noktasıdır; kalıcı bir `AudioSource` üzerinden `SfxLibrary` (ScriptableObject) satırındaki sesi çalar, `SfxGate` aynı sesin üst üste çalmasını keser. Düğmeler `UiSfx` bileşeniyle, oyun olayları var olan RPC ve olay yöntemlerine eklenen tek satırlık çağrılarla bağlanır. Oyun mantığı ve ağ mesajları değişmez.

**Tech Stack:** Unity 6000.3.10f1, uGUI 2.0, Photon PUN 2, NUnit EditMode testleri.

**Spec:** `docs/superpowers/specs/2026-11-19-ses-sistemi-design.md`

## Global Constraints

- Ses yalnızca yerel yan etkidir: hiçbir `Sfx.Play` çağrısı bir koşulu, dönüş değerini, RPC'yi ya da akışı değiştirmez. Var olan `return` satırlarının önüne eklenir, sırayı bozmaz.
- `Sfx.Play`, `SyncAutoTest.Instance != null` iken çalmaz ve `Sfx.Recent`'e yazmaz.
- Eksik satır ya da eksik ses dosyası hata değildir: sessiz geçilir, Editor'de olay başına bir kez `Debug.LogWarning`.
- `GameSettings.SoundOn` davranışı (`AudioListener.volume` 0/1) değişmez. Müzik, titreşim, ses kaydırıcısı yok.
- `SfxId` numaraları aşağıdaki tablodaki gibidir; yalnızca sona eklenir, sıra değişmez, numara yeniden kullanılmaz.
- Rakip/bot hamlelerinde çarpan `Sfx.OtherVolume = 0.5f`.
- Menü sahneleri builder'la üretilir; `MainMenu.unity`, `LobbyMenu.unity` ve kit prefabları elle düzenlenmez. `Table.unity`'ye yalnızca `Okey/UI Kit/Masa Metinlerini Bağla` menüsü dokunur.
- Dokunulmaz: `Assets/Osman/Scripts/GamePlay/AutoTest/`, `SyncAutoTest*`, `DesyncDetector`.
- Ses dosyaları yalnızca Kenney CC0 paketlerinden gelir; her dosya `Assets/Osman/Audio/KAYNAK.md`'ye yazılır. İndirme Osman'ın ayrı onayıyla yapılır (Görev 5 buna bağlıdır; Görev 1-4 dosyasız çalışır).
- Her görev tek commit, Türkçe mesaj, sonunda `Co-Authored-By: Claude <noreply@anthropic.com>`. Push yok.
- Commit'ten önce: `git checkout -- "Assets/Fonts/Changa-Bold SDF.asset" "Assets/Fonts/Changa-ExtraBold SDF.asset"`.
- Kullanıcının kendi değişmiş dosyalarına dokunulmaz: `51Okey.slnx`, `Assets/GooglePlayGames/.../GooglePlayGamesPlugin_v2.2.1.txt`, `ProjectSettings/PackageManagerSettings.asset`, `ProjectSettings/UnityConnectSettings.asset`, `docs/superpowers/plans/2026-11-11-sync-autotest.md`.
- Unity CLI: `C:\Users\osman\AppData\Local\Unity\bin\unity.exe command <grup> <komut> --project-path F:/Documents/UnityProjects/51Okey --result-only --no-banner`. İçinde metin sabiti olan kod `script eval --file <yol>` ile çalıştırılır (PowerShell iç çift tırnakları yutar).
- Play modunda: Google girişi tetiklenmez, çıkış yapılmaz, profil adı değişmez, satın alma çağrılmaz, hesap silme çağrılmaz.

### SfxId tablosu (bağlayıcı)

| No | SfxId | volume | minInterval |
|---|---|---|---|
| 0 | TilePickUp | 0.7 | 0.05 |
| 1 | TileDrop | 0.8 | 0.05 |
| 2 | TileSnapBack | 0.6 | 0.05 |
| 3 | TileDraw | 1 | 0.05 |
| 4 | TileTakeSide | 1 | 0.05 |
| 5 | TileDiscard | 1 | 0.05 |
| 6 | TilesDeal | 1 | 0.5 |
| 7 | OpenRuns | 1 | 0.3 |
| 8 | OpenPairs | 1 | 0.3 |
| 9 | MeldShown | 0.8 | 0.3 |
| 10 | TileProcess | 0.9 | 0.3 |
| 11 | TilesCollect | 0.9 | 0.3 |
| 12 | PutBack | 0.9 | 0.05 |
| 13 | Error | 0.8 | 0.2 |
| 14 | PenaltyTaken | 1 | 0.3 |
| 15 | PenaltyGiven | 0.9 | 0.3 |
| 16 | MyTurn | 0.9 | 0.5 |
| 17 | TimeLow | 0.9 | 1 |
| 18 | Win | 1 | 1 |
| 19 | Lose | 1 | 1 |
| 20 | SeatTaken | 0.7 | 0.3 |
| 21 | UiClick | 0.7 | 0.05 |
| 22 | UiClose | 0.7 | 0.05 |
| 23 | UiToggle | 0.7 | 0.05 |
| 24 | UiPlayNow | 0.9 | 0.2 |
| 25 | UiCreateTable | 0.9 | 0.2 |
| 26 | UiPlayBots | 0.9 | 0.2 |
| 27 | UiEnterRoom | 0.9 | 0.2 |

`pitchJitter`: 0-5 ve 10, 12 numaralı taş sesleri için 0.06; diğerleri 0.

## Review Focus

1. Bir karede aynı olay çok kez gelir (rakip el açınca taş başına RPC, toplu ceza) → tek ses (Görev 1 `SfxGate` testi; Görev 4 çağrı yerleri).
2. Ses listesi dosyası ya da bir ses dosyası yokken / silinmişken oyun açılır → hata yok, sessiz (Görev 1 testi).
3. Etkin olmayan (soluk) düğmeye basılır ya da parmak düğmeden dışarı kaydırılır → ses yok (Görev 2 testi).
4. Otomatik test koşarken → hiçbir ses, `Sfx.Recent` boş (Görev 1 testi, Görev 6 senkron testi).
5. Bir ses çağrısı oyun akışını değiştirir (erken `return`'ün önüne yanlış yerleştirme, koşulun içine alma) → senkron bozulur (Görev 3-4 incelemesi, Görev 6 senkron testi).

---

## Dosya yapısı

| Dosya | Sorumluluk |
|---|---|
| `Assets/Osman/Scripts/Audio/SfxId.cs` (yeni) | Olay adları, açık numaralı enum. |
| `Assets/Osman/Scripts/Audio/SfxGate.cs` (yeni) | "Şimdi çalabilir mi" kararı. Saf C#. |
| `Assets/Osman/Scripts/Audio/SfxLibrary.cs` (yeni) | Satır listesi (ScriptableObject) ve satır arama. |
| `Assets/Osman/Scripts/Audio/Sfx.cs` (yeni) | `Play`, kalıcı `AudioSource`, `Recent`. |
| `Assets/Osman/Scripts/Audio/UiSfx.cs` (yeni) | Düğme tıklamasında ses. |
| `Assets/Osman/Editor/SfxSetup.cs` (yeni) | `Okey/Ses/Ses Listesini Kur`, `Okey/Ses/Ses Listesini Denetle`, varsayılanlar tablosu. |
| `Assets/Resources/SfxLibrary.asset` (yeni) | Ses listesi. |
| `Assets/Osman/Audio/Sfx/*`, `Assets/Osman/Audio/KAYNAK.md` (yeni, Görev 5) | Ses dosyaları ve kaynakları. |
| `Assets/Osman/Editor/Tests/SfxTests.cs` (yeni) | Testler. |
| `Assets/Osman/Editor/UiKit*.cs`, `UiKitTable.cs` | Düğmelere `UiSfx`. |
| Oyun betikleri | Tek satırlık `Sfx.Play` çağrıları. |

---

### Task 1: Çekirdek (SfxId, SfxGate, SfxLibrary, Sfx, liste dosyası)

**Files:**
- Create: `Assets/Osman/Scripts/Audio/SfxId.cs`, `SfxGate.cs`, `SfxLibrary.cs`, `Sfx.cs`, `Assets/Osman/Editor/SfxSetup.cs`, `Assets/Resources/SfxLibrary.asset`
- Test: `Assets/Osman/Editor/Tests/SfxTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public enum SfxId { TilePickUp = 0, /* … tablodaki sırayla … */ UiEnterRoom = 27 }

  public sealed class SfxGate {
      public bool Allow(int id, float now, float minInterval);   // izin verirse zamanı kaydeder
      public void Reset();
  }

  public class SfxLibrary : ScriptableObject {
      [Serializable] public class Entry { public SfxId id; public AudioClip[] clips; public float volume = 1f; public float pitchJitter; public float minInterval = 0.05f; }
      public List<Entry> entries;
      public Entry Find(SfxId id);                                // yoksa null
  }

  public static class Sfx {
      public const float OtherVolume = 0.5f;
      public static void Play(SfxId id, float volumeScale = 1f);
      public static IReadOnlyList<SfxId> Recent { get; }          // en çok 32, en yenisi sonda
      public static void ClearRecent();
      public static void UseLibrary(SfxLibrary library);          // test için; null = Resources'tan yeniden yükle
  }

  // Editor
  public static class SfxSetup {
      public static readonly (SfxId id, float volume, float minInterval, float pitchJitter, string[] files)[] Defaults;
      [MenuItem("Okey/Ses/Ses Listesini Kur")] public static void Build();
  }
  ```
- `Sfx.Play` sırası: otomatik test → çık; satır bul (yoksa uyar, çık); `SfxGate.Allow(id, Time.unscaledTime, entry.minInterval)` (hayırsa çık); `Recent`'e ekle; `clips` içinden boş olmayan rastgele biri (yoksa çık — `Recent`'e yazılmış kalır, doğrulama dosyasız da çalışsın diye); `AudioSource.pitch = 1 ± pitchJitter`; `PlayOneShot(clip, entry.volume * volumeScale)`.
- `AudioSource` nesnesi ilk `Play`'de kurulur: `DontDestroyOnLoad`, `playOnAwake = false`, `spatialBlend = 0`. Edit modunda (`!Application.isPlaying`) nesne kurulmaz, yalnızca `Recent` güncellenir.
- `SfxSetup.Build`: `Assets/Resources/SfxLibrary.asset` yoksa oluşturur; `Defaults`'taki her `id` için satır **yoksa** ekler (`files` adlarını `Assets/Osman/Audio/Sfx/` altında arar, bulamadığını atlar); var olan satıra dokunmaz; `EditorUtility.SetDirty` + `AssetDatabase.SaveAssets`. Bu görevde `files` dizileri boştur.

- [ ] **Step 1: Başarısız testleri yaz**

```csharp
[Test] public void Gate_Blocks_Repeat_Inside_Interval_And_Allows_After() {
    var g = new SfxGate();
    Assert.IsTrue(g.Allow(5, 10f, 0.3f));
    Assert.IsFalse(g.Allow(5, 10.1f, 0.3f));
    Assert.IsTrue(g.Allow(5, 10.31f, 0.3f));
}
[Test] public void Gate_Ids_Do_Not_Block_Each_Other() {
    var g = new SfxGate();
    Assert.IsTrue(g.Allow(5, 10f, 0.3f));
    Assert.IsTrue(g.Allow(6, 10f, 0.3f));
}
[Test] public void Gate_Zero_Interval_Still_Blocks_Same_Timestamp() {
    var g = new SfxGate();
    Assert.IsTrue(g.Allow(1, 3f, 0f));
    Assert.IsFalse(g.Allow(1, 3f, 0f));   // aynı karede ikinci çağrı
    Assert.IsTrue(g.Allow(1, 3.02f, 0f));
}
[Test] public void Gate_Reset_Forgets() {
    var g = new SfxGate(); g.Allow(1, 3f, 5f); g.Reset();
    Assert.IsTrue(g.Allow(1, 3.1f, 5f));
}
[Test] public void SfxId_Numbers_Are_Fixed() {
    Assert.AreEqual(0, (int)SfxId.TilePickUp);
    Assert.AreEqual(5, (int)SfxId.TileDiscard);
    Assert.AreEqual(14, (int)SfxId.PenaltyTaken);
    Assert.AreEqual(21, (int)SfxId.UiClick);
    Assert.AreEqual(27, (int)SfxId.UiEnterRoom);
    Assert.AreEqual(28, Enum.GetValues(typeof(SfxId)).Length);
}
[Test] public void Play_With_Missing_Entry_Does_Not_Throw() {
    var lib = ScriptableObject.CreateInstance<SfxLibrary>(); lib.entries = new List<SfxLibrary.Entry>();
    Sfx.UseLibrary(lib); Sfx.ClearRecent(); LogAssert.ignoreFailingMessages = true;
    Assert.DoesNotThrow(() => Sfx.Play(SfxId.TileDraw));
    Assert.IsEmpty(Sfx.Recent);
}
[Test] public void Play_With_Entry_But_No_Clip_Is_Recorded_And_Silent() {
    var lib = ScriptableObject.CreateInstance<SfxLibrary>();
    lib.entries = new List<SfxLibrary.Entry> { new SfxLibrary.Entry { id = SfxId.TileDraw, clips = new AudioClip[] { null } } };
    Sfx.UseLibrary(lib); Sfx.ClearRecent();
    Assert.DoesNotThrow(() => Sfx.Play(SfxId.TileDraw));
    CollectionAssert.AreEqual(new[] { SfxId.TileDraw }, Sfx.Recent);
}
[Test] public void Recent_Keeps_Last_32() {
    var lib = ScriptableObject.CreateInstance<SfxLibrary>();
    lib.entries = new List<SfxLibrary.Entry> { new SfxLibrary.Entry { id = SfxId.UiClick, minInterval = 0f } };
    Sfx.UseLibrary(lib); Sfx.ClearRecent();
    // kapıyı aşmak için farklı zamanlar gerekir: Sfx testte zamanı dışarıdan alabilmeli
    for (int i = 0; i < 40; i++) Sfx.PlayAt(SfxId.UiClick, i);
    Assert.AreEqual(32, Sfx.Recent.Count);
}
[Test] public void Project_Library_Has_Exactly_One_Entry_Per_Id() {
    var lib = Resources.Load<SfxLibrary>("SfxLibrary");
    Assert.IsNotNull(lib);
    foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
        Assert.AreEqual(1, lib.entries.Count(e => e.id == id), id.ToString());
}
[TearDown] public void Restore() { Sfx.UseLibrary(null); Sfx.ClearRecent(); LogAssert.ignoreFailingMessages = false; }
```

`Sfx.PlayAt(SfxId id, float now, float volumeScale = 1f)` `internal`/`public` yardımcıdır; `Play` onu `Time.unscaledTime` ile çağırır.

- [ ] **Step 2: Çalıştır, derleme hatasıyla düştüğünü gör.** `U test run --mode EditMode --filter SfxTests`
- [ ] **Step 3: Sınıfları ve `SfxSetup.Build`'i yaz; menüyü çalıştırıp `SfxLibrary.asset`'i üret** (28 satır, tablodaki `volume`/`minInterval`/`pitchJitter`, `clips` boş).
- [ ] **Step 4: Testler geçer; tam EditMode paketi geçer (başlangıç 305).**
- [ ] **Step 5: Commit** — `Ses: çekirdek (Sfx, ses listesi, tekrar kapısı)`

---

### Task 2: Düğme sesleri (UiSfx, builder'lar, masa bağlayıcı)

**Files:**
- Create: `Assets/Osman/Scripts/Audio/UiSfx.cs`
- Modify: `Assets/Osman/Editor/UiKitPrefabs.cs`, `UiKitLogin.cs`, `UiKitLobby.cs`, `UiKitAccount.cs`, `UiKitTable.cs`
- Yeniden üretilen: kit prefabları, `Assets/Resources/UIRoot.prefab`, `MainMenu.unity`, `LobbyMenu.unity`; `Table.unity` (yalnızca iki `UiSfx`)
- Test: `Assets/Osman/Editor/Tests/SfxTests.cs` (ek)

**Interfaces:**
- Consumes: `Sfx.Play`, `SfxId`.
- Produces:
  ```csharp
  public class UiSfx : MonoBehaviour, IPointerClickHandler {
      public SfxId id = SfxId.UiClick;
      public void OnPointerClick(PointerEventData e);   // Selectable yoksa ya da IsInteractable() ise çalar
  }
  // UiKitBuilder (Editor)
  static void Sound(GameObject button, SfxId id);       // UiSfx ekler ya da var olanın id'sini değiştirir (TryGetComponent)
  ```
- Eşleme:

| Nerede | SfxId |
|---|---|
| `Btn_Green`, `Btn_Orange`, `Btn_Teal`, `Btn_Red` prefabları | `UiClick` |
| `Btn_Close` prefabı; onay penceresinin Vazgeç düğmesi | `UiClose` |
| `Toggle` prefabı; `Stepper` yardımcısının iki ok düğmesi | `UiToggle` |
| Profil çipi; `LinkRow` satırları (Gizlilik, Şartlar, Destek, Hesabımı Sil); jeton "+" düğmesi | `UiClick` |
| HEMEN OYNA | `UiPlayNow` |
| MASA AÇ (lobi düğmesi) | `UiCreateTable` |
| BOTLARLA OYNA | `UiPlayBots` |
| MASAYI KUR, şifre penceresindeki GİR, oda kartındaki OTUR | `UiEnterRoom` |
| `Table.unity`: Çıkış ve TABLO düğmeleri (menüde, nesne adından) | `UiClick` |

- Masadaki `MeldTiles_B`, `MeldPairTiles_B`, `Active Tiles_B`, `TakeBackTiles_B`, `Btn_CancelSidePick` `UiSfx` **almaz** (sesleri Görev 4'te sonuçtan gelir).

- [ ] **Step 1: Başarısız testleri yaz**

```csharp
static GameObject ButtonWith(bool interactable) {
    var go = new GameObject("b", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button), typeof(UiSfx));
    go.GetComponent<UnityEngine.UI.Button>().interactable = interactable; return go;
}
static SfxLibrary AllIds() {
    var lib = ScriptableObject.CreateInstance<SfxLibrary>(); lib.entries = new List<SfxLibrary.Entry>();
    foreach (SfxId id in Enum.GetValues(typeof(SfxId))) lib.entries.Add(new SfxLibrary.Entry { id = id, minInterval = 0f });
    return lib;
}
[Test] public void UiSfx_Plays_Its_Id_On_Click() {
    Sfx.UseLibrary(AllIds()); Sfx.ClearRecent();
    var go = ButtonWith(true); go.GetComponent<UiSfx>().id = SfxId.UiPlayNow;
    go.GetComponent<UiSfx>().OnPointerClick(new PointerEventData(null));
    CollectionAssert.AreEqual(new[] { SfxId.UiPlayNow }, Sfx.Recent);
    UnityEngine.Object.DestroyImmediate(go);
}
[Test] public void UiSfx_Is_Silent_On_Disabled_Button() {
    Sfx.UseLibrary(AllIds()); Sfx.ClearRecent();
    var go = ButtonWith(false);
    go.GetComponent<UiSfx>().OnPointerClick(new PointerEventData(null));
    Assert.IsEmpty(Sfx.Recent);
    UnityEngine.Object.DestroyImmediate(go);
}
[TestCase("Btn_Green", SfxId.UiClick)] [TestCase("Btn_Orange", SfxId.UiClick)] [TestCase("Btn_Teal", SfxId.UiClick)]
[TestCase("Btn_Red", SfxId.UiClick)] [TestCase("Btn_Close", SfxId.UiClose)] [TestCase("Toggle", SfxId.UiToggle)]
public void Kit_Prefab_Has_UiSfx(string prefab, SfxId expected) {
    var go = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Osman/Prefabs/UI/" + prefab + ".prefab");
    var s = go.GetComponentInChildren<UiSfx>(true);
    Assert.IsNotNull(s, prefab); Assert.AreEqual(expected, s.id);
}
```

"Parmak dışarı kaydırıldı" durumu uGUI'nin kendisindedir (`OnPointerClick` yalnızca basılan ve bırakılan nesne aynıysa gelir); ayrıca test edilmez, `UiSfx` `OnPointerDown` kullanmaz.

- [ ] **Step 2: Düştüğünü gör.**
- [ ] **Step 3: `UiSfx`'i, `Sound` yardımcısını yaz; builder'ları eşleme tablosuna göre değiştir; `UiKitTable`'a iki düğmeyi ekle** (ikinci çalıştırmada bileşen çoğalmaz; bulunamayan düğme `Debug.LogError`).
- [ ] **Step 4: Menüleri çalıştır:** `Prefabları Kur`, `Giriş Ekranını Kur`, `Lobiyi Kur`, `Mağazayı Düzelt`, `Hesap Pencerelerini Kur`, `Masa Metinlerini Bağla`. Her birinden sonra konsol temiz.
- [ ] **Step 5: Doğrula.**
  - Tam EditMode paketi geçer (dil denetimi `Project_Table_Has_No_Errors` dahil).
  - Sahne/prefab farkında yerleşim değeri değişmedi: `m_AnchoredPosition` / `m_SizeDelta` / `m_fontSize` / `m_text` satırlarının çoklu kümesi HEAD ile aynı (betikle karşılaştır, sonucu yaz).
  - Üretilen varlıklarda `UiSfx` sayısı ve özel `id` değerleri: HEMEN OYNA, MASA AÇ, BOTLARLA OYNA, MASAYI KUR, GİR, kart OTUR, iki Stepper, Vazgeç için sahne/prefab içinden okuyup raporla.
  - Play modunda lobide: `UiSfx`'li bir düğmenin `OnPointerClick`'ini `script eval` ile çağırıp `Sfx.Recent`'te doğru olayı gör (HEMEN OYNA'ya **basma**; MASA AÇ, Ayarlar, X, bir Toggle ve dil oku yeter).
- [ ] **Step 6: Commit** — `Ses: düğme sesleri (UiSfx), builder'lar ve masa düğmeleri`

---

### Task 3: Taş olayları

**Files:**
- Modify: `Assets/Osman/Scripts/GamePlay/TileUI.cs`, `Assets/Osman/Scripts/TILES/TileDistrubite.cs`

**Interfaces:**
- Consumes: `Sfx.Play(SfxId, float)`, `Sfx.OtherVolume`.
- Produces: yok (yalnızca çağrılar).

Yerel sıra numarası için dosyada zaten kullanılan ifade kullanılır (`PhotonNetwork.LocalPlayer.CustomProperties["PlayerQue"]`); yeni yardımcı yazılmaz, varsa dosyadaki mevcut yardımcı/alan kullanılır.

| Olay | Yer | Kural |
|---|---|---|
| `TilePickUp` | `TileUI.OnBeginDrag`, sürüklemenin gerçekten başladığı satır (`held = this`) | `SimulateDrag` içinden gelindiyse çalmaz |
| `TileDrop` | `TileUI.OnEndDrag`, ıstaka içi yer değiştirme ve boş yuvaya bırakma dalları | Ortadan/yandan çekme dallarında çalmaz; `SimulateDrag`'de çalmaz |
| `TileSnapBack` | `TileUI.OnEndDrag`, ıstaka içi geçersiz bırakmanın geri dönüş dalları | Reddedilen atış ve reddedilen işleme dallarında çalmaz (onlar Görev 4'te `Error`); `SimulateDrag`'de çalmaz |
| `TileDraw` | `TileDistrubite.AddTileFromMiddlePlayerList(int playerNumber)` | Benimse tam, değilse `OtherVolume` |
| `TileTakeSide` | `TileDistrubite.AddTileFromDropPlayerList(int playerNumber)` | Benimse tam, değilse `OtherVolume` |
| `TileDiscard` (ben) | `TileUI.OnEndDrag`, kabul edilen atış dalı (bırakma anı; eli bitiren atış dahil) | tam |
| `TileDiscard` (rakip) | `TileDistrubite.RemoveTileFromPlayerList(playerNumber, …)` | Yalnızca `playerNumber != myQue`, `OtherVolume` |
| `TileDiscard` (bot) | `TileDistrubite.BotDiscardTileRPC` | `OtherVolume` |
| `PutBack` | `TileDistrubite.ReturnTileToSide(returningPlayerQue, …)` | Benimse tam, değilse `OtherVolume` |
| `TilesDeal` | `TileDistrubite.DistributeTilesToAllPlayers` | tam |

`SimulateDrag` ayrımı: `TileUI`'de `SimulateDrag` süresince doğru olan bir `static bool simulating` alanı (try/finally ile kapatılır). Başka hiçbir davranış bu alana bakmaz.

- [ ] **Step 1: Çağrıları ekle.** Her ekleme tek satırdır; koşul ifadeleri, dönüşler ve sıra değişmez.
- [ ] **Step 2: Farkı kendin denetle:** `git diff` içinde `Sfx.Play` ve `simulating` dışında eklenen/silinen mantık satırı olmamalı; her çağrının hangi dalda olduğunu rapora yaz (dosya:satır, olay, kural).
- [ ] **Step 3: Tam EditMode paketi geçer.**
- [ ] **Step 4: Tek başına masa:** `powershell -ExecutionPolicy Bypass -File Tools\SyncTest\Run-SyncTest.ps1 -Solo -Mode smart -Seed 11` → `PASS` (otomatik testte ses kapalıdır; bu adım akışın bozulmadığını gösterir).
- [ ] **Step 5: Play modunda botlarla bir masa** (`ButtonFuncs.PlayWithBots` yolu, otomatik test yok): dağıtımdan sonra `Sfx.Recent`'te `TilesDeal`; bir bot atışından sonra `TileDiscard` gör. Kendi hamlen için `TileUI.SimulateDrag` kullanma (sürükleme seslerini bilerek susturur); `TileDraw`'ı süre dolunca oyunun otomatik hamlesinden sonra oku (30 sn) ya da atla ve raporda belirt.
- [ ] **Step 6: Commit** — `Ses: taş çekme, atma, sürükleme, dağıtım`

---

### Task 4: Açma, işleme, hata, ceza, sıra, el sonu, koltuk

**Files:**
- Modify: `Assets/Osman/Scripts/GamePlay/ScoreManager.cs`, `GameManager.cs`, `TurnManager.cs`, `SeatManager.cs`, `Assets/Osman/Scripts/UI/UIManager.cs`, `Assets/Osman/Scripts/GamePlay/TileUI.cs`, `Assets/Osman/Scripts/TILES/TileDistrubite.cs`

**Interfaces:**
- Consumes: `Sfx.Play`, `Sfx.OtherVolume`.

| Olay | Yer | Kural |
|---|---|---|
| `OpenRuns` | `ScoreManager.PlaceValidPers` içinde `hasOpenedAnyMeld`'in `true` yapıldığı yer | tam |
| `OpenPairs` | `ScoreManager.PlacePairPers` içinde `hasOpenedAnyPair`'in `true` yapıldığı yer | tam |
| `MeldShown` | `TileDistrubite.MeldTiles` RPC'si | Yalnızca `playerNumber != myQue`, `OtherVolume` |
| `TileProcess` (ben) | `ScoreManager.PerformTileProcessing` | tam (döngüde çok kez çağrılır; kapı tek sese indirir) |
| `TileProcess` (rakip) | `TileDistrubite.SyncProcessedTileRPC` | `OtherVolume` |
| `TilesCollect` | `ScoreManager.OnTakeBackButtonClick`'in üç başarı dalı | tam |
| `Error` | `ScoreManager.OnButtonClick` ve çift açma yönteminin her sessiz `return`'ü ve `ShowLimitMessage`; `OnActiveButtonClick`/`ActivePers` hiçbir taş işlemeden bittiğinde; `OnTakeBackButtonClick`'in hiçbir şey yapmayan dalı; `ProcessManualDrop` `false` döndüğünde ve ondan önceki ret dalları (`TileUI`); reddedilen atış dalları (`TileUI`) | tam. Bir harekette `Error` ve `TileSnapBack` birlikte çalmaz |
| `PenaltyTaken` / `PenaltyGiven` | `UIManager.ShowPenaltyFloat(playerQue, …)` | `playerQue == myQue` ise `PenaltyTaken`, değilse `PenaltyGiven`. `myQue == 0` iken (yöntem zaten erken döner) çalmaz |
| `MyTurn` | `TurnManager.NextTurn` (sıra numarası güncellendikten sonra) ve `TurnManager.StartRoundVisuals` | Yalnızca `currentTurnPlayer == myQue` |
| `TimeLow` | `UIManager.Update` içindeki süre çubuğu bloğu | Sıra bende ve `TurnRemaining` 5 sn'nin üstünden altına indiğinde bir kez (önceki değeri tutan bir alanla kenar algılama) |
| `Win` / `Lose` | `GameManager.FinishGameRPC(winnerActorNumber, …)` | `winnerActorNumber == PhotonNetwork.LocalPlayer.ActorNumber` ise `Win`, değilse `Lose` (berabere `-1` dahil) |
| `SeatTaken` | `SeatManager.AssignSeatToPlayer` | tam |

Saf karar yardımcıları (testlenebilir, `Assets/Osman/Scripts/Audio/Sfx.cs` içinde):
```csharp
public static SfxId PenaltyFor(int playerQue, int myQue);            // eşitse PenaltyTaken
public static SfxId FinishFor(int winnerActor, int myActor);         // eşitse Win
public static bool CrossedBelow(float previous, float current, float threshold);   // previous > threshold && current <= threshold
```

- [ ] **Step 1: Başarısız testleri yaz** (`SfxTests`)

```csharp
[Test] public void PenaltyFor_Mine_And_Others() {
    Assert.AreEqual(SfxId.PenaltyTaken, Sfx.PenaltyFor(2, 2));
    Assert.AreEqual(SfxId.PenaltyGiven, Sfx.PenaltyFor(3, 2));
}
[Test] public void FinishFor_Win_Lose_Draw() {
    Assert.AreEqual(SfxId.Win, Sfx.FinishFor(7, 7));
    Assert.AreEqual(SfxId.Lose, Sfx.FinishFor(4, 7));
    Assert.AreEqual(SfxId.Lose, Sfx.FinishFor(-1, 7));
}
[Test] public void CrossedBelow_Fires_Once_At_The_Edge() {
    Assert.IsTrue(Sfx.CrossedBelow(5.2f, 4.9f, 5f));
    Assert.IsFalse(Sfx.CrossedBelow(4.9f, 4.5f, 5f));   // zaten altında
    Assert.IsFalse(Sfx.CrossedBelow(30f, 29f, 5f));
    Assert.IsFalse(Sfx.CrossedBelow(0.2f, 30f, 5f));    // yeni tur, süre yenilendi
}
[Test] public void Burst_Of_Same_Event_Plays_Once() {
    Sfx.UseLibrary(AllIds()); // AllIds minInterval 0: aynı zaman damgası yine tek
    Sfx.ClearRecent();
    for (int i = 0; i < 14; i++) Sfx.PlayAt(SfxId.MeldShown, 12.5f, Sfx.OtherVolume);
    Assert.AreEqual(1, Sfx.Recent.Count);
}
```

- [ ] **Step 2: Düştüğünü gör; yardımcıları yaz; geçtiğini gör.**
- [ ] **Step 3: Çağrıları ekle.** Tek satırlık eklemeler; `Error` için her sessiz `return`'ün hemen önüne. Koşullar ve sıra değişmez. `TimeLow` için eklenen tek durum alanı `UIManager`'da özel bir `float`'tır.
- [ ] **Step 4: Farkı kendin denetle** (Görev 3 Step 2 ile aynı ölçüt) ve her `Error` yerini rapora yaz.
- [ ] **Step 5:** Tam EditMode paketi geçer. Tek başına masa testi (`-Solo -Mode smart -Seed 11`) `PASS`.
- [ ] **Step 6: Play modunda botlarla bir masa:** `Sfx.Recent`'te `SeatTaken`, `TilesDeal`, sıra sana geçince `MyTurn`, 25 sn sonra `TimeLow` görülür. Ceza için `script eval` ile `GameManager.Instance.AnnouncePenalty(<kendi sıran>, 100, PenaltyReason.DiscardedOkey)` ve başka bir sıra numarasıyla bir kez daha çağır; `PenaltyTaken` ve `PenaltyGiven` görülür (bu çağrı yalnızca ekrana yazı basar, puanı değiştirmez).
- [ ] **Step 7: Commit** — `Ses: açma, işleme, hata, ceza, sıra, el sonu, koltuk`

---

### Task 5: Ses dosyaları ve liste (indirme onayından sonra)

**Önkoşul:** Kenney paket zip'leri kontrolcü tarafından, Osman'ın onayıyla, proje dışındaki bir klasöre indirilmiş ve açılmıştır; klasör yolu görev dağıtımında verilir. Bu görev internetten bir şey indirmez.

**Files:**
- Create: `Assets/Osman/Audio/Sfx/*.ogg` (+ `.meta`), `Assets/Osman/Audio/KAYNAK.md`
- Modify: `Assets/Osman/Editor/SfxSetup.cs` (`Defaults` dosya adları, içe aktarma ayarı, denetim menüsü), `Assets/Resources/SfxLibrary.asset`
- Test: `Assets/Osman/Editor/Tests/SfxTests.cs` (ek)

**Interfaces:**
- Produces:
  ```csharp
  // SfxSetup
  public static readonly SfxId[] SilentOnPurpose;                       // uygun ses bulunamayan olaylar
  public sealed class Report { public List<string> NoClip, UnusedIds, OrphanFiles; }
  public static Report Check();
  [MenuItem("Okey/Ses/Ses Listesini Denetle")] static void CheckMenu();
  ```
- Seçim kuralları:
  - Yalnızca verilen paket klasörlerindeki dosyalar. Dosya adı, süre ve türe göre seç; süreyi dosyadan oku (ör. `ffprobe` varsa onunla, yoksa Unity'de `AudioClip.length`).
  - Taş sesleri (0-5, 10, 12): 0.05-0.4 sn, sert kısa vuruş. Olay başına 2-3 çeşit (aynı ailenin numaralı dosyaları) tercih edilir.
  - `TilesDeal`: 0.5-1.5 sn, çok parçalı karıştırma/dökülme sesi.
  - Düğme sesleri (21-27): 0.05-0.3 sn; yedi olayın her biri **farklı** dosya.
  - `Error`, `PenaltyTaken`: olumsuz/alçalan kısa ses; birbirinden farklı. `PenaltyGiven`: `PenaltyTaken`'dan belirgin biçimde farklı (daha hafif).
  - `Win` / `Lose`: 1-3 sn jingle; `MyTurn`: kısa uyarı; `TimeLow`: kısa tik/uyarı.
  - Aynı dosya iki olaya verilmez.
  - Uygun aday yoksa olay `SilentOnPurpose`'a girer ve rapora yazılır; uydurma seçim yapılmaz.
- Dosyalar projeye olay adına göre kopyalanır: `tile_draw_1.ogg`, `ui_click.ogg` … (küçük harf, alt çizgi). `.wav` gelirse olduğu gibi kopyalanır.
- `KAYNAK.md` satırı: proje dosya adı | paket | paketteki özgün ad | lisans (CC0) | süre.
- İçe aktarma (menü ayarlar): `forceToMono = true`, `loadType = DecompressOnLoad`, `Win`/`Lose`/`TilesDeal` için `CompressedInMemory`.
- `SfxSetup.Build` bu görevde bir kez "boş satırları doldur" kuralıyla çalışır: `clips` uzunluğu 0 olan satıra `Defaults` dosyalarını yazar; `clips` dolu satıra dokunmaz.

- [ ] **Step 1: Başarısız testleri yaz**

```csharp
[Test] public void Every_Event_Has_A_Clip_Or_Is_Silent_On_Purpose() {
    var lib = Resources.Load<SfxLibrary>("SfxLibrary");
    foreach (var e in lib.entries) {
        bool silent = SfxSetup.SilentOnPurpose.Contains(e.id);
        bool has = e.clips != null && e.clips.Length > 0;
        Assert.IsTrue(has != silent, e.id + (silent ? " sessiz denmiş ama sesi var" : " sesi yok"));
        if (has) Assert.IsFalse(e.clips.Any(c => c == null), e.id + " kayıp dosya");
    }
}
[Test] public void No_Clip_Is_Shared_Between_Events() {
    var lib = Resources.Load<SfxLibrary>("SfxLibrary");
    var all = lib.entries.Where(e => e.clips != null).SelectMany(e => e.clips).Where(c => c != null).ToList();
    Assert.AreEqual(all.Count, all.Distinct().Count());
}
[Test] public void Check_Reports_No_Orphan_Files_And_No_Unused_Ids() {
    var r = SfxSetup.Check();
    Assert.IsEmpty(r.OrphanFiles, string.Join(", ", r.OrphanFiles));
    Assert.IsEmpty(r.UnusedIds, string.Join(", ", r.UnusedIds));
}
```

`UnusedIds`: `Assets/Osman/Scripts/**/*.cs` ve builder dosyalarında `SfxId.<Ad>` olarak hiç geçmeyen olaylar (`Sfx.cs` ve `SfxId.cs` hariç).

- [ ] **Step 2: Düştüğünü gör.**
- [ ] **Step 3: Dosyaları seç, kopyala, `KAYNAK.md`'yi yaz, `Defaults`'u doldur, menüyü çalıştır.**
- [ ] **Step 4: Testler ve tam EditMode paketi geçer.** `Okey/Ses/Ses Listesini Denetle` çıktısını rapora yaz.
- [ ] **Step 5: Play modunda** bir düğme sesi ve bir taş sesi için `AudioSource`'un gerçekten çaldığını oku (`isPlaying` ya da `Sfx`'in son çaldığı klibin adı), hata olmadığını konsoldan doğrula.
- [ ] **Step 6: Commit** — `Ses: ses dosyaları (Kenney CC0), kaynak listesi ve denetim menüsü` (ses dosyaları ve `.meta`'ları dahil)

---

### Task 6: Bütünün doğrulanması

**Files:**
- Modify: `docs/superpowers/specs/2026-11-19-ses-sistemi-design.md` (sona "Uygulamadan devreden notlar")

- [ ] **Step 1:** Tam EditMode paketi geçer; sayıyı yaz.
- [ ] **Step 2:** `Okey/Ses/Ses Listesini Denetle` ve `Okey/Dil/Tabloyu Denetle`: hata yok.
- [ ] **Step 3: Çok editörlü senkron testi** kontrolcü tarafından koşulur (`Run-SyncTest.ps1 -Deals 1` → `PASS`, `desync 0`); test sonunda ana editörde `Sfx.Recent` boştur.
- [ ] **Step 4: Notları yaz:** olay → dosya tablosu (hangi olay hangi Kenney dosyası), bilerek sessiz bırakılanlar, Claude'un yalnız verdiği kararlar, bilinen açıklar, Osman'ın kulakla dinleyeceği liste (Editor'de `SfxLibrary` Inspector'ında tek tek; telefonda bir el: çekme, atma, sürükleme, dağıtım, seri/çift açma, işleme, ceza yeme/yedirme, sıra, süre, kazanma/kaybetme, bütün menü düğmeleri; Ses düğmesi kapalıyken tam sessizlik).
- [ ] **Step 5: Commit** — `Ses: doğrulama notları`
