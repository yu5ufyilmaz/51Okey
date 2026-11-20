# Oyuna Geri Dönüş Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bağlantısı kopan oyuncu süren eline geri dönebilsin; o yokken koltuğunda bot oynasın, masası diğerleriyle aynı olduğu kanıtlanmadan koltuk ona verilmesin.

**Architecture:** Koltuğu kimin oynadığını yeni `Ctl` oda özelliği (koltuk başına bit) söyler; bot ve insan girişleri bu bite bakar. Masa sahibi, dönen istemciye bir tur sınırında masanın tam fotoğrafını (`TableSnapshot`, saf kodlayıcıyla `int[]`+`string[]`) yollar; istemci kurar, sonraki tur sınırlarında masa özetini (`TableDigest`, anahtar = `CurrentRound` + tur seri numarası) yollar; özet eşitse ve sıra o koltukta değilse bit açılır. Arabellekli RPC'ler kalkar, masa sahibinin yetki RPC'leri `AllViaServer` ile gider, tur ortasında kopan oyuncunun yarım turu `RollbackTurn` ile geri alınır.

**Tech Stack:** Unity 6000.3.10f1, Photon PUN 2 (Realtime), uGUI + TextMeshPro, NUnit EditMode, PowerShell + Pester 3.4 (senkron testi).

**Spec:** `docs/superpowers/specs/2026-11-20-geri-donus-design.md` (son bölümü "İnceleme sonrası düzeltmeler" öncekilerin üstündedir) ve teknik eki `docs/superpowers/specs/2026-11-20-geri-donus-envanter.md` (S/D/P/N işaretleri bağlayıcı).

## Global Constraints

- Plan dili Türkçe; kod adları İngilizce. Commit mesajları Türkçe, her biri boş satır ve `Co-Authored-By: Claude <noreply@anthropic.com>` ile biter; her görev tek commit; push yok.
- Her commit'ten önce: `git checkout -- "Assets/Fonts/Changa-Bold SDF.asset" "Assets/Fonts/Changa-ExtraBold SDF.asset"`. Asla stage edilmez: `51Okey.slnx`, `Assets/GooglePlayGames/**`, `ProjectSettings/PackageManagerSettings.asset`, `ProjectSettings/UnityConnectSettings.asset`, `docs/superpowers/plans/2026-11-11-sync-autotest.md`.
- Görünür metin yalnız `Loc.T("section.key")` / `LocText` ile, anahtar çift tırnaklı sabit; satırlar `Assets/Resources/Localization.csv`'de tr ve en dolu (UTF-8 BOM, LF, sütunlar `key,tr,en,note`). `LocAudit.CheckProject()` 0 hatada kalır.
- `Sfx.Play` çağrıları yalnız ses satırıdır (akış koşulu taşımaz).
- Yeni her `[PunRPC]` PUN'un RpcList'ine (`Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings.asset`) kaydedilir (`EditorUtility.SetDirty(PhotonNetwork.PhotonServerSettings); AssetDatabase.SaveAssets()`) ve varlık commit edilir. Klon projelerin de bu varlığı görmesi gerekir; eksik kayıt editörler arası RPC kısayollarını bozar (kontrolcü klonda `PhotonNetwork.PhotonServerSettings.RpcList.Contains("RollbackTurn")` → `true` doğrular).
- `Assets/Osman/Editor/UiKitTable.cs` el yapımı `Assets/Osman/Scenes/Table.unity`'yi değiştirebilen tek koddur; üretilen diğer arayüz `UiKit*` kurucularından gelir; kurucular ikinci çalıştırmada çoğaltmaz.
- Unity CLI: `C:\Users\osman\AppData\Local\Unity\bin\unity.exe command <group> <cmd> --project-path F:/Documents/UnityProjects/51Okey --result-only --no-banner` (`editor status`, `script eval --file <path>`, `test run --mode EditMode [--filter X]`). Aşağıda `U <group> <cmd>` bu satırın kısaltmasıdır. EditMode tabanı bugün 379 geçen; hiçbir görev bunu düşürmez.
- Senkron testi: `powershell -ExecutionPolicy Bypass -File Tools\SyncTest\Run-SyncTest.ps1 -Deals N [-Seed S] [-Mode smart] [-Solo]`; aşağıda `ST`. Çok editörlü koşuları (ana + `51Okey_clone_0..2`) **kontrolcü** koşar, görev uygulayıcısı yalnız `-Solo` koşabilir. ~8 dakikadan uzun koşular arka planda başlatılır. Koşu sürerken editöre `script eval` yapılmaz.
- Tuzak: "The open scene(s) have been modified externally" penceresi editörü kilitler; uygulayıcı durur ve raporlar, pencereyi kapatmaz; Editor'ün açık tuttuğu sahneye `git checkout` yapılmaz.
- Play modunda kesin sınırlar: Google girişi tetiklenmez, çıkış yapılmaz, kayıtlı profil adı değişmez, satın alma çağrılmaz, hesap silme çağrılmaz.
- Geçerli sahip kararları: son taşla biten kazanır; çiftte masadaki okey alınabilir, çiftteki gösterge kopyası alınamaz; baraj, bir turda ilk açılan değerde kalır.
- Tasarımdan: alan aramaları ada bağlı kalır (`GameObject.Find(NickName + " meld")` değişmez); oyun kodunda çağrı sırası değişmez, yalnız hedefler (arabelleksiz, `AllViaServer`) ve denetim koşulu (bit) değişir. Masada etkin insan kalmazsa maç kurtarılmaz ("Oyun sona ermiş"). Aynı hesapla ikinci cihazdan dönüş yoktur.
- `PlayerTtl` hedefi 2 saat (7200000 ms), `EmptyRoomTtl` bulutun izin verdiği en yüksek değer; kesin değerler Görev 0'ın sonuç dosyasından alınır.
- Yeni kalıcı log önekleri: `[CTL]`, `[TUR]`, `[GERIAL]`, `[OZET]`, `[DONUS]`, `[ELYENI]`. Doğrulamalar bu satırları okur.

## Review Focus

1. Dönen oyuncu, koltuk ona verilmeden (bot hâlâ oynarken) "Per aç / Çift aç / İşle" düğmesine basar → hiçbir şey gönderilmemeli; düğmeler bugün yalnız `canDrop`'a bakıyor (`ScoreManager.cs:559,609`). Test: Görev 3 `TurnGate_Requires_Live_Control_And_Turn`.
2. Oyuncu yandan taş alıp açamadan kopar → taş aynı atık yığınına geri dönmeli, hiçbir elde ikinci kopyası kalmamalı, ceza yazılmamalı. Test: Görev 5 `Side_Taken_Tile_Is_Not_Kept`.
3. Fotoğraf gelmeden önce bir insanın K'den sonraki çekişi sıraya düşmüş olur → K'den sonraki hamle bir kez oynanmalı, K ve öncesi hiç oynanmamalı (çifte çekiş yok). Test: Görev 9 `Gate_Replays_Only_After_Snapshot_Serial`.
4. İki oyuncu aynı anda kopar ya da el sıkışma sırasında masa sahibi değişir → aynı koltuk iki kez verilmemeli, istek yeni masa sahibine yeniden gitmeli. Test: Görev 11 `Book_Grants_Once_And_Forgets_Leavers`.
5. Oyuncu uygulamayı kapatır, maç o yokken biter ya da oda kapanır; açınca "Evet" der → "Oyun sona ermiş" görmeli, kayıt silinmeli, soru bir daha çıkmamalı; klon editörler aynı kaydı paylaşmamalı. Test: Görev 10 `Policy_Clears_Record_Only_On_Room_Or_Actor_Gone` ve Görev 2 `Record_Key_Is_Per_Instance`.

---

## Dosya yapısı

| Dosya | Sorumluluk |
|---|---|
| `Assets/Osman/Scripts/GamePlay/Rejoin/CtlBits.cs` (yeni) | `Ctl` bit işlemleri, `TurnGate`. Saf. |
| `Assets/Osman/Scripts/GamePlay/Rejoin/TurnSerialRule.cs` (yeni) | Tur seri numarası kabul kuralı. Saf. |
| `Assets/Osman/Scripts/GamePlay/Rejoin/TurnRollback.cs` (yeni) | `TurnStartRecord`, geri alma kuralı. Saf. |
| `Assets/Osman/Scripts/GamePlay/Rejoin/TableSnapshot.cs`, `SnapshotCodec.cs` (yeni) | Fotoğraf modeli, kanonik metin, kodlama/çözme. Saf. |
| `Assets/Osman/Scripts/GamePlay/Rejoin/TableDigest.cs` (yeni) | FNV-1a özeti, `DigestRing`. Saf. |
| `Assets/Osman/Scripts/GamePlay/Rejoin/TableDigestLog.cs`, `SnapshotBuilder.cs`, `SnapshotApplier.cs` (yeni) | Sahneden fotoğraf, tur sınırı özeti, fotoğrafı sahneye kurma. |
| `Assets/Osman/Scripts/GamePlay/Rejoin/RestoreGate.cs` (yeni) | "Canlı değil" kapısı ve RPC kuyruğu. Saf. |
| `Assets/Osman/Scripts/GamePlay/Rejoin/HandshakeRule.cs` (yeni) | El sıkışma kararı ve masa sahibi defteri. Saf. |
| `Assets/Osman/Scripts/GamePlay/Rejoin/RejoinHandshake.cs` (yeni) | El sıkışma olayları (RaiseEvent), DDOL. |
| `Assets/Osman/Scripts/GamePlay/Rejoin/MasterRecovery.cs` (yeni) | `PenaltyRebuild`, `NewHandRecovery`. Saf. |
| `Assets/Osman/Scripts/Photons/RejoinRecord.cs`, `PhotonIdentity.cs`, `RejoinPolicy.cs` (yeni) | Cihaz kaydı, kimlik, hata kodu politikası. |
| `Assets/Osman/Scripts/Photons/ReconnectFlow.cs` (yeni) | `OnDisconnected` sahibi, yeniden katılma döngüsü, DDOL. |
| `Assets/Osman/Editor/UiKitReconnect.cs` (yeni) → `Assets/Osman/Scenes/Reconnect.unity` | Nötr sahne kurucusu. |
| `Assets/Osman/Scripts/GamePlay/AutoTest/DropTrigger.cs` (yeni) | Senkron testinde kopma anı kuralı. Saf. |
| `Assets/Osman/Editor/Tests/RejoinTests.cs`, `SnapshotTests.cs`, `DropTriggerTests.cs` (yeni) | Testler. |
| `TurnManager.cs`, `GameManager.cs`, `SeatManager.cs`, `ScoreManager.cs`, `TileUI.cs`, `TileDistrubite.cs`, `DesyncDetector.cs`, `MatchScores.cs`, `FinishGate.cs`, `RoomManager.cs`, `PhotonManager.cs`, `ExitButton.cs`, `MainMenuUIHandler.cs`, `SyncAutoTest*.cs`, `Tools/SyncTest/*.ps1` | Bağlantı noktaları. |

**Sıra gerekçesi.** 0 (deneme) tasarımın Photon varsayımlarını doğrular; sonuç çelişirse iş durur. 1-4 dönüşten bağımsızdır ve bugünkü kopmayı da düzeltir: arabelleksizlik (her dönüşün önkoşulu), kimlik/TTL (dönüş süresi), `Ctl` (bot ve girişin tek kapısı), seri numarası ve `AllViaServer` (özet anahtarı ve geri almanın eski mesaj koruması). 5 (geri alma) yalnız 3-4'e dayanır ve kopup dönmeyen oyuncuda da taş kaybını keser. 6 (harness) dönüş yokken bile "kopup gitme" senaryolarıyla 3-5'i çok editörde sınar; sonraki görevlerin doğrulama aracıdır. 7-8 (saf fotoğraf modeli, sonra özet ve sahneden kurma) özetin duyarlılık testinin modele ihtiyacı yüzünden bu sıradadır. 9 kurulumu ağsız (`-SelfRestore`) kanıtlar. 10 bağlantıyı, 11 el sıkışmayı ekler; 12 masa sahibi kopmasını sağlamlaştırır; 13 bütünü doğrular. Her görevden sonra oyun çalışır, mevcut testler yeşildir.

---

### Task 0: Canlı Photon denemesi (kontrolcü yürütür, iki editör)

Tasarımın Photon varsayımları burada denenir. **Kural: aşağıdaki sorulardan birinin sonucu tasarımla çelişirse (kapalı odaya dönülemiyor, `PlayerTtl` 2 saatin çok altında kırpılıyor, dönüşte ActorNumber/özellikler korunmuyor) iş durur ve kontrolcü Osman'a döner; sonraki görevlere geçilmez.**

**Files:**
- Create (geçici, commit edilmez, sonunda silinir): `Assets/_Spike/RejoinSpike.cs`, `Assets/_Spike/Spike.unity`
- Create: `docs/superpowers/specs/2026-11-20-geri-donus-photon-deneme.md`

**Interfaces:**
- Produces: sonuç dosyasında `PlayerTtlMs` ve `EmptyRoomTtlMs` için bulutun kabul ettiği değerler (Görev 2 kullanır); `OnLeftRoom`/`OnDisconnected` sırası (Görev 10 kullanır); 32746'nın kaç saniye sürdüğü (Görev 10'un 30 sn penceresi).

- [ ] **Step 1: Deneme bileşenini yaz.** `RejoinSpike : MonoBehaviourPunCallbacks, IOnEventCallback`; her geri çağrıyı `[SPIKE] {Time.realtimeSinceStartup:F2} <ad> <parametreler>` ile loglar (`OnConnectedToMaster`, `OnJoinedRoom` + `LocalPlayer.ActorNumber/HasRejoined/CustomProperties`, `OnJoinRoomFailed(code)`, `OnLeftRoom`, `OnDisconnected(cause)`, `OnPlayerLeftRoom(p.IsInactive)`, `OnPlayerEnteredRoom(p.HasRejoined)`, `OnMasterClientSwitched`, `OnEvent(code)`). `script eval` ile çağrılacak statik yöntemler: `Connect(string userId)` (`AuthValues = new AuthenticationValues(userId)`, `ConnectUsingSettings`), `Create(string room, int playerTtl, int emptyTtl)`, `Join(string room)`, `CloseRoom()` (`IsOpen=false; IsVisible=false`), `SetProps()` (`SeatNumber=2, PlayerQue=3`), `CacheEvents()` (`RaiseEvent(201, Others + AddToRoomCache)` ve `RaiseEvent(202, All + AddToRoomCache)`), `Drop()` (`PhotonNetwork.Disconnect()`), `Lose()` (`NetworkingClient.SimulateConnectionLoss(true)`, `DisconnectTimeout = 5000`), `Reconnect()` (`ReconnectAndRejoin()` sonucunu loglar), `Rejoin(string room)`, `LeaveInactive()` (`LeaveRoom(true)`), `Room()` (`CurrentRoom.PlayerTtl`, `EmptyRoomTtl`, `PlayerCount`, oyuncuların `IsInactive` listesi). `Spike.unity`: kamera + bu bileşen. İki editörde `playModeStartScene` bu sahne yapılır (Run-SyncTest'teki `$setStart` kalıbı), sonda geri alınır.
- [ ] **Step 2: Soruları sırayla sına** (A = ana editör, B = `51Okey_clone_0`; her adımın `[SPIKE]` satırları sonuç dosyasına aynen):
  - S1 Sınırlar: A `Create("spike1", 7200000, 300000)`; B `Join`; B `Room()` → okunan `PlayerTtl`/`EmptyRoomTtl`. Ayrıca A `Create("spike1b", 7200000, 600000)` ve `Create("spike1c", -1, 300000)`: hata kodu ya da kırpılan değer.
  - S2 Kapalı odaya dönüş: B `SetProps()`, A `CloseRoom()`, B `CacheEvents()`, B `Drop()`; A'da `OnPlayerLeftRoom(IsInactive=True)`; B `Reconnect()` → `OnJoinedRoom` ile aynı ActorNumber, `HasRejoined=True`, `SeatNumber=2`, `PlayerQue=3`; B'ye 201 ve/veya 202 olayı geri geldi mi.
  - S3 Sıra: S2'deki `Drop()` sonrası B'de `OnLeftRoom` ile `OnDisconnected`'ın sırası ve aradaki süre.
  - S4 Soğuk dönüş: B Play'den çıkar, yeniden girer, `Connect` (aynı userId), `Rejoin("spike1")` → başarı ve aynı ActorNumber.
  - S5 Hata kodları: `Rejoin("yok-oda")` → 32758 bekleniyor; B başka userId ile `Rejoin("spike1")` → 32748 bekleniyor; B `Lose()`, `OnDisconnected` gelir gelmez `Reconnect()` her 2 sn'de → ilk denemelerde 32746 mı, kaç saniye sonra başarı.
  - S6 Etkin oyuncusuz oda: A ve B `Drop()`; 60 sn sonra B `Rejoin` → başarı (B tek ve master) mı 32758 mi; `EmptyRoomTtl` + 30 sn sonra yeniden → 32758.
  - S7 Masa sahibi kopması: A (master) `Drop()`; B'de `OnMasterClientSwitched` ve `OnPlayerLeftRoom` sırası; A dönünce master değil.
  - S8 Bilerek etkin olmayan ayrılma: B `LeaveInactive()`, sonra `Rejoin` → sunucu izin veriyor mu (bizim kuralımız kaydı silerek engeller; yalnız bilgi).
- [ ] **Step 3: Sonuç dosyasını yaz.** Tablo: soru, tasarımın varsayımı, gözlenen, uyum (evet/hayır). Altında `PlayerTtlMs = …`, `EmptyRoomTtlMs = …`, "32746 penceresi ≈ N sn", "geri çağrı sırası: …". Çelişki varsa en üste **DURDU** satırı ve çelişkinin tasarımdaki yeri.
- [ ] **Step 4: Temizle.** `Assets/_Spike/` ve `.meta`'ları silinir; iki editörde `playModeStartScene = null`; `git status --short Assets` boş.
- [ ] **Step 5: Commit** — `Geri dönüş: canlı Photon denemesinin sonuçları`

---

### Task 1: Arabellekli RPC'ler kalkar

**Files:**
- Modify: `Assets/Osman/Scripts/TILES/TileDistrubite.cs` (`:327-332`, `:351-352`, `:2149`, `:2160`), `Assets/Osman/Scripts/GamePlay/TileUI.cs` (`:763-767`, `:775-779`, `:1053`), `Assets/Osman/Scripts/GamePlay/ScoreManager.cs` (`:869-886`, `:987-1004`, `:1020`, `:1153-1171`, `:1187`, `:1193`, `:1219`, `:1250`, `:1306`, `:1808`, `:1837`, `:1854`, `:1934`, `:1944`, `:1993`), `Assets/Osman/Scripts/GamePlay/GameManager.cs` (`:263`, `:276`), `Assets/Osman/Scripts/GamePlay/SeatManager.cs` (`:98`, `:227`, `:250`, `:269`, `:311`, `:358-359`), `Assets/Osman/Scripts/GamePlay/TurnManager.cs` (`:478`, `:505`)
- Test: `Assets/Osman/Editor/Tests/RejoinTests.cs` (yeni)

**Interfaces:**
- Produces: `Assets/Osman/Scripts` altında hiçbir `RpcTarget.AllBuffered`, `RpcTarget.OthersBuffered`, `RpcTarget.AllBufferedViaServer` kalmaz. Hedef dönüşümü: `AllBuffered` → `All`, `OthersBuffered` → `Others`. Başka hiçbir şey değişmez (sıra, parametre, gönderen).

- [ ] **Step 1: Başarısız testi yaz**

```csharp
[Test] public void Game_Scripts_Send_No_Buffered_Rpc() {
    var hits = Directory.GetFiles("Assets/Osman/Scripts", "*.cs", SearchOption.AllDirectories)
        .SelectMany(f => File.ReadAllLines(f).Select((l, i) => (f, i, l)))
        .Where(x => Regex.IsMatch(x.l, @"RpcTarget\.(AllBuffered|OthersBuffered|AllBufferedViaServer)"))
        .Select(x => $"{x.f}:{x.i + 1}").ToList();
    CollectionAssert.IsEmpty(hits, string.Join("\n", hits));
}
```
- [ ] **Step 2: Düştüğünü gör.** `U test run --mode EditMode --filter RejoinTests` → FAIL, listede envanter §3'teki B1-B18 gönderim satırları.
- [ ] **Step 3: Hedefleri değiştir** (yukarıdaki satırlar; `NextTurn` gönderimleri şimdilik `All`, Görev 4 yeniden ele alır).
- [ ] **Step 4: Testler ve tam paket geçer.** `U test run --mode EditMode` → 0 başarısız, 379 + 1.
- [ ] **Step 5: Tek editör.** `ST -Solo -Mode smart -Seed 11` → `PASS`.
- [ ] **Step 6: Kontrolcü.** `ST -Deals 2 -Mode smart` ve `ST -Deals 3 -Mode smart` → `PASS`, `desync 0`; `ST -Bots 2 -Mode smart -Seed 11` → `PASS` (bekleme odasına sonradan gelen, koltukları `SeatNumber` özelliğinden okur).
- [ ] **Step 7: Commit** — `Geri dönüş: oyun RPC'leri arabelleksiz`

---

### Task 2: Kimlik, oda ömrü, cihaz kaydı

**Files:**
- Create: `Assets/Osman/Scripts/Photons/RejoinRecord.cs`, `Assets/Osman/Scripts/Photons/PhotonIdentity.cs`
- Modify: `Assets/Osman/Scripts/Photons/PhotonManager.cs` (`ConnectWhenReadyAsync`), `Assets/Osman/Scripts/UI/MainMenuUIHandler.cs` (`EnterLobby` `:90-115`), `Assets/Osman/Scripts/Photons/RoomManager.cs` (`:9-10`, `:50-59`), `Assets/Osman/Scripts/GamePlay/SeatManager.cs` (`OnRoomPropertiesUpdate` `:331-335`), `Assets/Osman/Scripts/UI/ExitButton.cs` (`ExitGame`), `Assets/Osman/Scripts/GamePlay/GameManager.cs` (`RestartSequence` son el dalı `:688-693`)
- Test: `Assets/Osman/Editor/Tests/RejoinTests.cs`

**Interfaces:**
- Consumes: Görev 0 sonuç dosyasındaki TTL değerleri; `GameServices.Auth.UserId`, `GameServices.InstanceTag`.
- Produces:
  ```csharp
  public static class RejoinRecord {
      public static string KeyFor(string instanceTag);   // "Okey.Rejoin." + instanceTag
      public static void Save(string roomName);           // PlayerPrefs, KeyFor(GameServices.InstanceTag); loglar "[DONUS] kayıt: {room}"
      public static string Load();                        // yoksa ya da boşsa null
      public static void Clear();                         // loglar "[DONUS] kayıt silindi"
  }
  public static class PhotonIdentity {
      public static bool NeedsReconnect(string photonUserId, string authUserId); // authUserId != null && photonUserId != authUserId
  }
  // RoomManager
  public const int PlayerTtlMs = 7200000;     // Görev 0'ın kabul edilen değeri
  public const int EmptyRoomTtlMs = 300000;   // Görev 0'ın kabul edilen değeri
  ```
- Anahtar kurulum adını içerir: ParrelSync klonları aynı ürün adıyla aynı PlayerPrefs'i paylaşır; telefonda `InstanceTag == "device"`.
- `ConnectWhenReadyAsync`: `ConnectUsingSettings`'ten önce `Auth.UserId != null` ise `PhotonNetwork.AuthValues = new AuthenticationValues(Auth.UserId)`.
- `EnterLobby`: lobi oturunca, `PhotonIdentity.NeedsReconnect(PhotonNetwork.AuthValues?.UserId, Auth.UserId)` ise bir kez `Disconnect` → `ConnectWhenReadyAsync` → yeniden lobi beklenir (ilk açılışta Photon girişten önce bağlanıyor, `PhotonManager.cs:54-55`).
- `CreateCustomRoom`: `PlayerTtl = PlayerTtlMs`, `EmptyRoomTtl = EmptyRoomTtlMs`; serileştirilmiş `emptyRoomTtl` alanı kalkar; oda kurulunca `[ODA] PlayerTtl={..} EmptyRoomTtl={..}` loglanır.
- Kayıt yazılır: `SeatManager.OnRoomPropertiesUpdate` `SeatsLocked` true geldiğinde ve yerel oyuncu oturmuşsa. Silinir: `ExitButton.ExitGame` başında; `RestartSequence` son elde `LeaveRoom`'dan önce.

- [ ] **Step 1: Başarısız testleri yaz**

```csharp
[Test] public void Record_Key_Is_Per_Instance() {
    Assert.AreEqual("Okey.Rejoin.device", RejoinRecord.KeyFor("device"));
    Assert.AreNotEqual(RejoinRecord.KeyFor("51Okey"), RejoinRecord.KeyFor("51Okey_clone_0"));
}
[Test] public void Identity_Reconnect_Only_When_Auth_Known_And_Different() {
    Assert.IsFalse(PhotonIdentity.NeedsReconnect("a", "a"));
    Assert.IsTrue(PhotonIdentity.NeedsReconnect("x", "a"));
    Assert.IsTrue(PhotonIdentity.NeedsReconnect(null, "a"));
    Assert.IsFalse(PhotonIdentity.NeedsReconnect("x", null));
}
```
- [ ] **Step 2: Düştüğünü gör** (`--filter RejoinTests`, derleme hatası).
- [ ] **Step 3: Yaz.**
- [ ] **Step 4: Tam paket geçer.**
- [ ] **Step 5: Tek editör.** `ST -Solo -Mode smart -Seed 11` → `PASS`; ardından `U console get --tail 2000` çıktısında sırasıyla `[ODA] PlayerTtl=7200000`, `[DONUS] kayıt:`, `[DONUS] kayıt silindi` var.
- [ ] **Step 6: Commit** — `Geri dönüş: Photon kimliği Firebase uid, uzun PlayerTtl, cihazda oda kaydı`

---

### Task 3: `Ctl` denetim biti ve bot kapısı

**Files:**
- Create: `Assets/Osman/Scripts/GamePlay/Rejoin/CtlBits.cs`
- Modify: `Assets/Osman/Scripts/GamePlay/SeatManager.cs` (`StartGame` `:655-678`, `OnPlayerLeftRoom` `:259-272`, `OnMasterClientSwitched` `:202-211`, `PublishSeatNames` `:148-181`, `UpdateSeatDisplay` `:453-546`), `Assets/Osman/Scripts/GamePlay/TurnManager.cs` (`Update` `:60-119`, `PlayBotTurn` `:440-517`, `IsPlayerTurn` `:225-238`), `Assets/Osman/Scripts/GamePlay/ScoreManager.cs` (`OnButtonClick`, `OnPairButtonClick`, `OnActiveButtonClick`)
- Test: `Assets/Osman/Editor/Tests/RejoinTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public static class CtlBits {
      public const string Key = "Ctl";                   // oda özelliği, int; bit (seat-1) açık = insan oynar
      public static bool Has(int ctl, int seat);         // seat 1..4 dışı -> false
      public static int Set(int ctl, int seat, bool on); // seat 1..4 dışı -> ctl aynen
      public static int Of(IEnumerable<int> seats);
      public static bool[] BotNow(int ctl);              // [i] = !Has(ctl, i+1)
      public static int Read(ExitGames.Client.Photon.Hashtable props); // yok/int değil -> 0
  }
  public static class TurnGate { public static bool MayAct(bool live, bool humanControl, bool myQueIsCurrent); } // üçü de true
  // SeatManager
  public int IntendedCtl { get; }                         // masa sahibinin niyet ettiği değer; bot kapısı bunu hemen kullanır
  public void SetSeatControl(int seat, bool human);      // yalnız master: IntendedCtl + oda özelliği + PublishSeatNames; "[CTL] koltuk {s} insan|bot"
  public Transform MeldAreaOfSeat(int seat);              // MeldAreaOfQue'nun koltuk sürümü
  public Transform DropAreaOfSeat(int seat);              // tiledropOffset[RelativeIndex(seat, LocalSeat)]
  // TurnManager
  public bool IsSeatHuman(int que);                       // master: CtlBits.Has(SeatManager.Instance.IntendedCtl, SeatMath.MeldSeat(seatOfQue, que))
  public bool BotRunningFor(int que);                     // çalışan PlayBotTurn bu sıra için mi
  ```
- `StartGame`: `SeatsLocked` ile aynı `SetCustomProperties`'te `Ctl = CtlBits.Of(oturan insanların koltukları)`; `IntendedCtl` aynı değer; `[CTL] başlangıç {ctl ikilik}` loglanır.
- Master `OnPlayerLeftRoom` (etkin olmayan ya da tamamen ayrılan): `SetSeatControl(seat, false)`. Yeni master (`OnMasterClientSwitched`, yerel): `IntendedCtl = CtlBits.Read(oda)`, sonra etkin olmayan her oyuncunun koltuğu için `SetSeatControl(seat, false)`.
- `TurnManager.Update`: `IsInactive` döngüsü yerine `IsSeatHuman(currentTurnPlayer)`. `PlayBotTurn` `Coroutine botRoutine` / `int botQue` ile tutulur; `Update` sıradaki koltuğu insan görürse çalışan `botRoutine`'i `StopCoroutine` eder ve `[BOT] iptal sıra {q}` loglar; `PlayBotTurn` çekmeden ve atmadan hemen önce `IsSeatHuman(que)` ise çıkar.
- `IsPlayerTurn` = `TurnGate.MayAct(true, CtlBits.Has(CtlBits.Read(oda), SeatManager.Instance.LocalSeat), que == currentTurnPlayer)` (insan kapısı sunucudan dönen değere bakar; `live` Görev 9'da `RestoreGate.Live` olur). Süre dolunca otomatik oynama da buradan kilitlenir.
- Üç `ScoreManager` düğmesi başta `if (!turnManager.IsPlayerTurn()) { Sfx.Play(SfxId.Error); return; }`.
- İsimlik: `PublishSeatNames`, `SeatsLocked` iken `botNow = CtlBits.BotNow(IntendedCtl)`; `UpdateSeatDisplay`, `SeatsLocked` iken **her** koltuk için `MatchScores.SeatNames[seat-1]` yazar ("Can (bot)" `IsInactive`'e değil bite bakar).

- [ ] **Step 1: Başarısız testleri yaz** (dosyada `using Hashtable = ExitGames.Client.Photon.Hashtable;`)

```csharp
[Test] public void Ctl_Bits() {
    Assert.IsTrue(CtlBits.Has(0b0101, 1)); Assert.IsFalse(CtlBits.Has(0b0101, 2)); Assert.IsTrue(CtlBits.Has(0b0101, 3));
    Assert.IsFalse(CtlBits.Has(5, 0)); Assert.IsFalse(CtlBits.Has(5, 5));
    Assert.AreEqual(8, CtlBits.Set(0, 4, true)); Assert.AreEqual(13, CtlBits.Set(15, 2, false)); Assert.AreEqual(5, CtlBits.Set(5, 9, true));
    Assert.AreEqual(5, CtlBits.Of(new[] { 1, 3 })); Assert.AreEqual(0, CtlBits.Of(new int[0]));
    CollectionAssert.AreEqual(new[] { false, true, false, true }, CtlBits.BotNow(5));
}
[Test] public void Ctl_Read() {
    Assert.AreEqual(0, CtlBits.Read(new Hashtable()));
    Assert.AreEqual(6, CtlBits.Read(new Hashtable { { "Ctl", 6 } }));
    Assert.AreEqual(0, CtlBits.Read(new Hashtable { { "Ctl", "x" } }));
}
[Test] public void TurnGate_Requires_Live_Control_And_Turn() {
    Assert.IsTrue(TurnGate.MayAct(true, true, true));
    Assert.IsFalse(TurnGate.MayAct(false, true, true));
    Assert.IsFalse(TurnGate.MayAct(true, false, true));
    Assert.IsFalse(TurnGate.MayAct(true, true, false));
}
[Test] public void Names_From_Ctl() {
    CollectionAssert.AreEqual(new[] { "A", "Bot 1", "C (bot)", "D" },
        SeatNaming.Names(new[] { "A", null, "C", "D" }, CtlBits.BotNow(0b1001)));
}
```
- [ ] **Step 2: Düştüğünü gör.**
- [ ] **Step 3: Yaz.**
- [ ] **Step 4: Tam paket geçer.**
- [ ] **Step 5: Tek editör.** `ST -Solo -Mode smart -Seed 11` → `PASS`; konsolda tek bir `[CTL] başlangıç` satırı (yerel koltuğun biti açık), hiç `[BOT] iptal` yok (kimse kopmadı).
- [ ] **Step 6: Kontrolcü.** `ST -Deals 2 -Mode smart` ve `ST -Bots 2 -Mode smart -Seed 11` → `PASS`, `desync 0`.
- [ ] **Step 7: Commit** — `Geri dönüş: koltuğu kimin oynadığı Ctl bitinde; bot ve giriş kapısı bite bakar`

---

### Task 4: Yetki RPC'leri `AllViaServer`, tur seri numarası

**Files:**
- Create: `Assets/Osman/Scripts/GamePlay/Rejoin/TurnSerialRule.cs`
- Modify: `Assets/Osman/Scripts/GamePlay/TurnManager.cs` (`NextTurn`, `ForceSkipBotTurn`, `RPC_ResetTurnForNewRound`, `PlayBotTurn` gönderimleri), `Assets/Osman/Scripts/GamePlay/TileUI.cs` (`:1053`), `Assets/Osman/Scripts/TILES/TileDistrubite.cs` (`:225`, `:284`, `:291`, `:327-332`, `:341-345`, `:351-352`, `:2128-2134`, `:2149`, `:2160`), `Assets/Osman/Scripts/GamePlay/GameManager.cs` (`AnnouncePenalty` `:217`, `CheckGameStatus` beraberlik dalı `:432`, `CalculateAndDistributeScores` `:545`, `NextRoundSetup` `:701`, `RPC_PrepareNextRound` `:720`, `WaitAndRedistribute` `:747`)
- Test: `Assets/Osman/Editor/Tests/RejoinTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public static class TurnSerialRule { public static bool Accept(int current, int incoming); }   // incoming > current (ad, TurnManager.TurnSerial özelliğiyle çakışmasın)
  // TurnManager
  public int TurnSerial { get; private set; }            // el başında 0; her kabul edilen NextTurn'de = serial
  public void SendNextTurn(bool authority);              // RPC("NextTurn", authority ? AllViaServer : All, TurnSerial + 1)
  [PunRPC] void NextTurn(int serial, PhotonMessageInfo info);
  ```
- `NextTurn`: `!TurnSerialRule.Accept(TurnSerial, serial)` ise `[TUR] eski seri {serial} <= {TurnSerial}` uyarısı ve çık (bugün ikinci bir `NextTurn` bir turu atlatıyor); `serial > TurnSerial + 1` ise `[TUR] seri atladı` uyarısı ve kabul. `RPC_ResetTurnForNewRound` `TurnSerial = 0`.
- Gönderenler: insan atışı `TileUI.cs:1053` → `SendNextTurn(false)`; bot atışı ve bot hatası `TileDistrubite.cs:2149,2160`, watchdog `ForceSkipBotTurn` → `SendNextTurn(true)`.
- `AllViaServer` olur (yalnız masa sahibinin yetki mesajları): `ShowPenaltyRPC`; bot `AddTileFromMiddlePlayerList` ve `BotDiscardTileRPC`; masa sahibinin `FinishGameRPC`'leri (beraberlik ve bot atışında taş bitti); `OnGameEndedDetailedRPC`; `RPC_PrepareNextRound`; `RPC_ResetTurnForNewRound`; `ResetTableAndRedistribute`; `SyncIndicatorTile`, `AssignPlayerQueue`, `SyncShuffledTiles`, `AssignQueueToPlayer`, `SetTurnDirectly`, `SyncQueToSeatMapping`, `AssignQueueComplete`. İnsanın kendi hamleleri (masa sahibi dahil) `All` kalır.
- RpcList'e yeni ad eklenmez (`NextTurn` adı aynı, imzası değişti).

- [ ] **Step 1: Başarısız test**

```csharp
[Test] public void Serial_Accepts_Only_Newer() {
    Assert.IsTrue(TurnSerialRule.Accept(0, 1)); Assert.IsFalse(TurnSerialRule.Accept(3, 3));
    Assert.IsFalse(TurnSerialRule.Accept(3, 2)); Assert.IsTrue(TurnSerialRule.Accept(3, 5));
}
```
- [ ] **Step 2: Düştüğünü gör. Step 3: Yaz. Step 4: Tam paket geçer.**
- [ ] **Step 5: Tek editör.** `ST -Solo -Mode smart -Seed 11` → `PASS`; konsolda `[TUR]` satırı yok.
- [ ] **Step 6: Kontrolcü.** `ST -Deals 2 -Mode smart` ve `ST -Deals 3 -Mode smart` → `PASS`, `desync 0`. `[TUR] eski seri` görülürse rapora yazılır (bugün turu atlatan çift `NextTurn`'ün kanıtıdır).
- [ ] **Step 7: Commit** — `Geri dönüş: masa sahibinin yetki mesajları sunucu sırasıyla; NextTurn seri numaralı`

---

### Task 5: Tur başı kaydı ve `RollbackTurn`

**Files:**
- Create: `Assets/Osman/Scripts/GamePlay/Rejoin/TurnRollback.cs`
- Modify: `Assets/Osman/Scripts/TILES/TileDistrubite.cs` (`SyncShuffledTiles` sonu, `AddTileFromMiddlePlayerList`, `AddTileFromDropPlayerList`, `RemoveTileFromPlayerListByValue`; yeni yöntemler), `Assets/Osman/Scripts/GamePlay/TurnManager.cs` (`NextTurn` sonu, `Update` bot başlatma), `Assets/Osman/Scripts/GamePlay/GameManager.cs` (yeni RPC ve `HandleSeatLoss`), `Assets/Osman/Scripts/GamePlay/SeatManager.cs` (`OnPlayerLeftRoom`, `OnMasterClientSwitched`), `Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings.asset`
- Test: `Assets/Osman/Editor/Tests/RejoinTests.cs`

**Interfaces:**
- Consumes: Görev 3 `SetSeatControl`, `DropAreaOfSeat`, `IsSeatHuman`; Görev 4 `TurnSerial`, `SendNextTurn`.
- Produces:
  ```csharp
  public sealed class TurnStartRecord {
      public int round, serial, que, discards, tableLimit;      // discards: o koltuğun atık alanındaki TileUI sayısı
      public bool opened, openedPairs, doubleOpened;
      public List<Tiles> hand;                                   // kopya, liste sırasıyla
      public List<List<Tiles>> melds; public List<List<Vector2Int>> positions;
      public bool hasOpenValue; public int openValue; public bool openValuePairs;
      public List<Tiles> drawn = new List<Tiles>();              // bu turda ortadan çekilenler, sırayla
      public Tiles sideTaken;                                     // yandan alınıp iptal edilmemiş taş; yoksa null
  }
  public enum RollbackKind { CloseTurn, Rollback }
  public static class TurnRollback {
      public static RollbackKind Decide(int discardsAtStart, int discardsNow);   // now > start -> CloseTurn
      public static List<Tiles> HandAfterRollback(IReadOnlyList<Tiles> startHand, IReadOnlyList<Tiles> drawn); // başlangıç sırası + çekilenler; id tekrarlanmaz
  }
  // TileDistrubite
  public TurnStartRecord TurnStart { get; }
  public void CaptureTurnStart(int que);
  public int DiscardCountOfSeat(int seat);
  public void ApplyRollback(TurnStartRecord r);           // eli yerinde yeniden doldurur, perleri/açma üyeliğini/OpenValues'u geri koyar, sideTaken'ı ReturnTileToSide gövdesiyle yığına döndürür
  public void RenderSeatMeldsFromData(int que);           // InstatiateMeldTiles(que)
  // GameManager
  [PunRPC] void RollbackTurn(int que, int serial);        // master AllViaServer ile yollar
  public void HandleSeatLoss(Photon.Realtime.Player p);   // yalnız master; aynı seri için bir kez
  ```
- Kayıt: her istemci `NextTurn` sonunda ve `SyncShuffledTiles` sonunda (el başında `NextTurn` yok) `CaptureTurnStart(currentTurnPlayer)`. `AddTileFromMiddlePlayerList(q)` q kayıttaki sıraysa `drawn`'a ekler; `AddTileFromDropPlayerList(q)` `sideTaken = dropTile`; `RemoveTileFromPlayerListByValue` aynı id'yi `sideTaken`'dan siler (yandan almayı iptal yolu).
- `HandleSeatLoss(p)`: koltuğu bul, `SetSeatControl(seat, false)`; o koltuk sıradaysa, `isGameReady && !isGameEnded` ise `pendingRollbackSerial = TurnSerial` ve `RollbackTurn(que, TurnSerial)` `AllViaServer`. `SeatManager.OnPlayerLeftRoom` (master) bunu çağırır; yeni master `OnMasterClientSwitched`'te biti hâlâ açık olan her etkin olmayan oyuncu için çağırır (iki geri çağrının sırası ne olursa olsun bir kez çalışır).
- `RollbackTurn` her istemcide: `serial == TurnSerial && que == currentTurnPlayer && TurnStart.que == que` değilse yok sayar. `Decide(TurnStart.discards, DiscardCountOfSeat(seat))`: `CloseTurn` → `RenderSeatMeldsFromData(que)`; master `SendNextTurn(true)`. `Rollback` → `ApplyRollback(TurnStart)`; master `TableLimit`/`IsDoubleOpened` oda özelliklerini kayıttakine döndürür (farklıysa). Her iki dalda `[GERIAL] sıra {que} seri {serial} {kind}`. Master `pendingRollbackSerial`'ı temizler; `TurnManager.Update`, `pendingRollbackSerial == TurnSerial` iken bot başlatmaz. Sonra bot (bit kapalı) elde 15 taş varsa yalnız atar, 14 ise çeker ve atar; yandan alınan taşın cezası yazılmaz.
- `RollbackTurn` RpcList'e kaydedilir.

- [ ] **Step 1: Başarısız testler**

```csharp
static Tiles T(string id, int n = 5) => new Tiles { id = id, color = TileColor.red, number = n, type = TileType.Number };
[Test] public void Rollback_Decide() {
    Assert.AreEqual(RollbackKind.CloseTurn, TurnRollback.Decide(3, 4));
    Assert.AreEqual(RollbackKind.Rollback, TurnRollback.Decide(3, 3));
    Assert.AreEqual(RollbackKind.Rollback, TurnRollback.Decide(0, 0));
}
[Test] public void Hand_After_Rollback_Is_Start_Plus_Drawn() {
    CollectionAssert.AreEqual(new[] { "a", "b", "c" }, TurnRollback.HandAfterRollback(new[] { T("a"), T("b") }, new[] { T("c") }).Select(t => t.id));
    CollectionAssert.AreEqual(new[] { "a", "b" }, TurnRollback.HandAfterRollback(new[] { T("a"), T("b") }, new Tiles[0]).Select(t => t.id));
    CollectionAssert.AreEqual(new[] { "a", "b" }, TurnRollback.HandAfterRollback(new[] { T("a"), T("b") }, new[] { T("b") }).Select(t => t.id));
}
[Test] public void Side_Taken_Tile_Is_Not_Kept() {
    var r = new TurnStartRecord { hand = new List<Tiles> { T("a"), T("b") }, sideTaken = T("x") };
    var hand = TurnRollback.HandAfterRollback(r.hand, r.drawn);
    Assert.IsFalse(hand.Any(t => t.id == "x")); Assert.AreEqual(2, hand.Count);
}
```
- [ ] **Step 2: Düştüğünü gör. Step 3: Yaz; RpcList'i kaydet** (`git diff --stat` varlığı gösterir).
- [ ] **Step 4: Tam paket geçer.**
- [ ] **Step 5: Tek editör.** `ST -Solo -Mode smart -Seed 11` → `PASS`, konsolda `[GERIAL]` yok.
- [ ] **Step 6: Kontrolcü.** `ST -Deals 2 -Mode smart` → `PASS`. Çok editörlü kopma sınaması Görev 6'nın `-NoReturn` senaryolarıyla yapılır (orada listelenmiştir).
- [ ] **Step 7: Commit** — `Geri dönüş: tur ortasında kopanın yarım turu geri alınır`

---

### Task 6: Senkron testinde kopma senaryoları (`-DropRejoin`)

**Files:**
- Create: `Assets/Osman/Scripts/GamePlay/AutoTest/DropTrigger.cs`
- Modify: `Assets/Osman/Scripts/GamePlay/SyncAutoTestData.cs`, `Assets/Osman/Scripts/GamePlay/SyncAutoTest.cs` (durum makinesi, `OnLeftRoom`, `OnLog`), `Assets/Osman/Scripts/GamePlay/SyncAutoTest.Smart.cs` (açma sonrası tetik noktası), `Tools/SyncTest/Run-SyncTest.ps1`, `Tools/SyncTest/SyncTestLib.ps1`, `Tools/SyncTest/SyncTestLib.Tests.ps1`
- Test: `Assets/Osman/Editor/Tests/DropTriggerTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public struct DropView { public bool myTurn, isMaster, handEnded, openedThisTurn; public int handCount, ownTurnsSeen; }
  public static class DropTrigger {
      // "offturn","before-draw","after-draw","after-open","master","master-hand-end","hand-end","two"; bilinmeyen -> false
      public static bool ShouldDrop(string scenario, DropView v);
  }
  // SyncAutoTestConfig (yeni alanlar)
  public string dropScenario;   // "" = kopma yok
  public bool dropThis;         // bu editör kopar
  public float dropOfflineSec;  // dönüşten önce çevrimdışı süre
  public string dropMode;       // "return" | "noreturn" | "afterend"
  // SyncAutoTestStatus (yeni alanlar)
  public int drops, rejoinGrants; public bool restoreEntered, gameOverSeen; public List<string> scoreLines;
  // SyncAutoTest.State: Dropped, Gone eklenir
  ```
- Kurallar (`ShouldDrop`): `offturn`/`two`/`master` → `!myTurn && ownTurnsSeen >= 2` (`master` ayrıca `isMaster`); `before-draw` → `myTurn && handCount == 14 && ownTurnsSeen >= 2`; `after-draw` → `myTurn && handCount == 15 && !openedThisTurn && ownTurnsSeen >= 2`; `after-open` → `myTurn && openedThisTurn`; `hand-end` → `handEnded`; `master-hand-end` → `isMaster && handEnded` (yeni el hattında masa sahibi kopması). Tek atış: ilk `true`'da bir kez.
- `SyncAutoTest`: `dropThis` iken her karede `DropView` kurar; `true` olunca `drops++`, durum `Dropped`, `PhotonNetwork.Disconnect()`; `Dropped`'ta `OnLeftRoom` hata sayılmaz. `noreturn` → durum `Gone` (koşucu Play'i durdurur). `afterend` → `Temp/SyncAutoTest/release.json` görünene kadar bekler. `[SKOR] el` ile başlayan her log `scoreLines`'a eklenir. `rejoinGrants`, `restoreEntered`, `gameOverSeen` Görev 10-11'de beslenir (o zamana kadar 0/false).
- `Run-SyncTest.ps1`: `[ValidateSet('', 'offturn','before-draw','after-draw','after-open','master','master-hand-end','hand-end','two')][string]$DropRejoin = ''`, `[switch]$NoReturn`, `[switch]$ReturnAfterEnd`, `[int]$DropOfflineSec = 8`. Kopan editörler: `master`, `master-hand-end` → 0; `two` → 1 ve 2; diğerleri → 1. Yalnız dört editörle (`-Solo`, `-Bots`, `-JoinRoom` ile birlikte verilemez). İzleme döngüsü `Gone` editörünü bitmiş sayar ve Play'den çıkarır; `-ReturnAfterEnd`'de diğerleri `Done` olunca kopan projeye `release.json` yazar.
- `Get-SyncTestVerdict` yeni parametreler `[int[]]$DropEditors`, `[string]$DropMode`; varsa: `desync` ve `ok` bütün durumların toplamıdır (masa sahibi de kopabilir); her kopan editörde `drops -ge 1` değilse "senaryo tetiklenmedi"; `return` → `rejoinGrants -ge 1` değilse "geri verme yok" (`restoreEntered` true ise sebebe "kurulum modunda kaldı" eklenir); `afterend` → `gameOverSeen` değilse "oyun sona ermiş görülmedi"; `noreturn` kopanın durumu dışarıda kalır; maçı bitiren editörlerin `scoreLines`'ı birebir aynı değilse "skor farklı".

- [ ] **Step 1: Başarısız testler**

```csharp
static DropView V(bool myTurn = false, int hand = 14, bool opened = false, int seen = 2, bool master = false, bool ended = false) =>
    new DropView { myTurn = myTurn, handCount = hand, openedThisTurn = opened, ownTurnsSeen = seen, isMaster = master, handEnded = ended };
[Test] public void Offturn() { Assert.IsTrue(DropTrigger.ShouldDrop("offturn", V())); Assert.IsFalse(DropTrigger.ShouldDrop("offturn", V(myTurn: true))); Assert.IsFalse(DropTrigger.ShouldDrop("offturn", V(seen: 1))); }
[Test] public void Own_Turn_Points() {
    Assert.IsTrue(DropTrigger.ShouldDrop("before-draw", V(true, 14))); Assert.IsFalse(DropTrigger.ShouldDrop("before-draw", V(true, 15)));
    Assert.IsTrue(DropTrigger.ShouldDrop("after-draw", V(true, 15))); Assert.IsFalse(DropTrigger.ShouldDrop("after-draw", V(true, 15, opened: true)));
    Assert.IsTrue(DropTrigger.ShouldDrop("after-open", V(true, 12, opened: true)));
}
[Test] public void Master_HandEnd_Unknown() {
    Assert.IsTrue(DropTrigger.ShouldDrop("master", V(master: true))); Assert.IsFalse(DropTrigger.ShouldDrop("master", V()));
    Assert.IsTrue(DropTrigger.ShouldDrop("hand-end", V(ended: true))); Assert.IsFalse(DropTrigger.ShouldDrop("x", V()));
    Assert.IsTrue(DropTrigger.ShouldDrop("master-hand-end", V(master: true, ended: true))); Assert.IsFalse(DropTrigger.ShouldDrop("master-hand-end", V(ended: true)));
}
```
  Pester (`SyncTestLib.Tests.ps1`, `Describe 'Get-SyncTestVerdict -DropEditors'`): `return` + `drops 1, rejoinGrants 1`, aynı `scoreLines` → Pass; `rejoinGrants 0` → Reason `geri verme yok`; bir editörün `scoreLines`'ı farklı → `skor farklı`; `noreturn` + kopanın `state 'Gone'` → Pass; `afterend` + `gameOverSeen $false` → `oyun sona ermiş görülmedi`; host `desync 0` ama g2 `desync 1` → `DESYNC`; `drops 0` → `senaryo tetiklenmedi`.
- [ ] **Step 2: Düştüğünü gör.** `U test run --mode EditMode --filter DropTriggerTests`; `Invoke-Pester -Script Tools\SyncTest\SyncTestLib.Tests.ps1`.
- [ ] **Step 3: Yaz. Step 4: EditMode tam paket ve Pester geçer.**
- [ ] **Step 5: Tek editör.** `ST -Solo -Mode smart -Seed 11` → `PASS` (yeni alanlar boşken eski davranış).
- [ ] **Step 6: Kontrolcü (Görev 3-5'in çok editörlü kanıtı).** Hepsi `-Mode smart -Seed 11 -NoReturn`: `-DropRejoin offturn`, `-DropRejoin after-draw`, `-DropRejoin after-open` (tetiklenmezse `-Seed 1234`, sonra `-Seed 7`), `-DropRejoin master` → `PASS`, `desync 0`; `after-draw` ve `after-open`'da kalan editörlerin konsolunda `[GERIAL]` satırı (`Rollback`), `[CTL] koltuk N bot`; isimlikte "(bot)".
- [ ] **Step 7: Commit** — `Senkron testi: -DropRejoin kopma senaryoları`

---

### Task 7: Masa fotoğrafı modeli ve kodlayıcı (saf)

**Files:**
- Create: `Assets/Osman/Scripts/GamePlay/Rejoin/TableSnapshot.cs`, `Assets/Osman/Scripts/GamePlay/Rejoin/SnapshotCodec.cs`
- Test: `Assets/Osman/Editor/Tests/SnapshotTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public struct SnapDiscard { public int seat; public Tiles tile; }
  public struct SnapBoardTile { public Tiles tile; public int meldType, slot; }   // meldType = alanın child(0..2) sırası, slot = yuva sırası
  public struct SnapOpenValue { public int que, value; public bool pairs; }
  public struct SnapPenalty { public int seat, amount, reason; }
  public sealed class TableSnapshot {
      public const int Version = 1;
      public int round, turnSerial, currentTurn; public bool ready, ended;
      public double turnStartTime;               // meta: özete girmez
      public int devDeal, devMove;               // meta: DesyncDetector anahtarı, özete girmez
      public int[] seatOfQue = new int[5];       // [1..4]
      public Tiles indicator, dropTile;          // ikisi de null olabilir
      public List<Tiles> pile;
      public List<Tiles>[] hands;                // [0..3] = sıra 1..4, liste sırasıyla
      public List<List<Tiles>>[] melds; public List<List<Vector2Int>>[] meldPositions;   // meltedTiles verisi aynen
      public List<SnapDiscard> discards;         // bütün koltuklar, zaman sırasıyla
      public List<SnapBoardTile>[] boards;       // [0..3] = koltuk 1..4, sahnedeki tahtadan
      public List<int> opened, openedPairs;      // sıra, artan
      public List<SnapOpenValue> openValues;     // sıra artan
      public List<SnapPenalty> penalties;        // MatchScores.current, geliş sırası
      public static TableSnapshot Empty();       // bütün listeler boş ve hazır
      public string Canonical(bool withMeta);    // her alan bir satır "ad=değer"; taş: Number "{renk harfi}{sayı}:{tam id}", Joker "J:{id}", FakeJoker "F:{id}", null "-"
  }
  public static class SnapshotCodec {
      public static object[] Encode(TableSnapshot s);       // { int[] ints, string[] strs }; RaiseEvent içeriği budur
      public static TableSnapshot Decode(object[] payload); // bozuk/eksik/fazla veri: null
  }
  ```
- Kodlama (bağlayıcı): `ints` = `Version, round, turnSerial, currentTurn, ready, ended, devDeal, devMove, seatOfQue[1..4], indicator, dropTile, pile, hands×4, melds×4, discards, boards×4, opened, openedPairs, openValues, penalties`. Taş = 4 int `[idIndex, type, color, number]`, null taş `[-1,0,0,0]`; liste = sayı + öğeler; meld = taş listesi + konum sayısı + `row,col` çiftleri; discard = `seat` + taş; board öğesi = `meldType, slot` + taş; openValue = `que, value, pairs`; penalty = `seat, amount, reason`. `strs[0]` = `turnStartTime.ToString("R", CultureInfo.InvariantCulture)`, `strs[1..]` = taş id'leri (her id bir kez, ilk görülme sırasıyla). `Decode` reddeder: payload null ya da uzunluk ≠ 2, `ints[0] != Version`, eksi sayı, dizi sonunu aşan okuma, `idIndex` aralık dışı, `type > 2`, `color > 3`, okuma bittiğinde artan int, çözülemeyen zaman.

- [ ] **Step 1: Başarısız testler** (`Sample()`: sıra 3, seri 12, el 2; `seatOfQue {0,3,4,1,2}`; gösterge kırmızı 7; `dropTile` mavi 4; ortada 5 taş; eller 14/14/15/14, birinde bir Joker; sıra 2'de iki per, ilkinin konumlarıyla; 6 atık koltuk 1-3'ten, ilki koltuk 1; koltuk 3 tahtasında 4 taş, biri Joker; `opened {2}`, `openedPairs {}`; `openValues {(2,68,false)}`; `penalties {(1,250,1),(3,40,4)}`; `turnStartTime 1234.5678`, `devDeal 2, devMove 31`)

```csharp
[Test] public void RoundTrip_Keeps_Everything() {
    var s = Sample(); var d = SnapshotCodec.Decode(SnapshotCodec.Encode(s));
    Assert.AreEqual(s.Canonical(true), d.Canonical(true));
    Assert.AreEqual(1234.5678, d.turnStartTime); Assert.AreEqual(31, d.devMove);
}
[Test] public void Encode_Is_Deterministic() {
    var a = SnapshotCodec.Encode(Sample()); var b = SnapshotCodec.Encode(Sample());
    CollectionAssert.AreEqual((int[])a[0], (int[])b[0]); CollectionAssert.AreEqual((string[])a[1], (string[])b[1]);
}
[Test] public void Null_Tiles_RoundTrip() {
    var s = Sample(); s.dropTile = null; s.indicator = null;
    var d = SnapshotCodec.Decode(SnapshotCodec.Encode(s)); Assert.IsNull(d.dropTile); Assert.IsNull(d.indicator);
}
[Test] public void Decode_Rejects_Bad_Data() {
    var p = SnapshotCodec.Encode(Sample()); var ints = (int[])p[0]; var strs = (string[])p[1];
    Assert.IsNull(SnapshotCodec.Decode(null)); Assert.IsNull(SnapshotCodec.Decode(new object[] { ints }));
    Assert.IsNull(SnapshotCodec.Decode(new object[] { With(ints, 0, 2), strs }));                       // sürüm
    Assert.IsNull(SnapshotCodec.Decode(new object[] { ints.Take(ints.Length - 1).ToArray(), strs }));   // kısa
    Assert.IsNull(SnapshotCodec.Decode(new object[] { ints.Concat(new[] { 0 }).ToArray(), strs }));     // fazla
    Assert.IsNull(SnapshotCodec.Decode(new object[] { ints, strs.Take(2).ToArray() }));                 // id yok
    Assert.IsNull(SnapshotCodec.Decode(new object[] { ints, new[] { "x" }.Concat(strs.Skip(1)).ToArray() })); // zaman
    Assert.IsNull(SnapshotCodec.Decode(new object[] { With(ints, 12, 9999), strs }));                   // gösterge idIndex aralık dışı
    Assert.IsNull(SnapshotCodec.Decode(new object[] { With(ints, 13, 7), strs }));                      // gösterge type > 2
}
```
(`With(a, i, v)`: kopya, `a[i] = v`. Gösterge taşı `ints[12]`'de başlar.)
- [ ] **Step 2: Düştüğünü gör. Step 3: Yaz. Step 4: Testler ve tam paket geçer.**
- [ ] **Step 5: Commit** — `Geri dönüş: masa fotoğrafı modeli ve kodlayıcı`

---

### Task 8: Üretim masa özeti ve sahneden fotoğraf

**Files:**
- Create: `Assets/Osman/Scripts/GamePlay/Rejoin/TableDigest.cs`, `Assets/Osman/Scripts/GamePlay/Rejoin/SnapshotBuilder.cs`, `Assets/Osman/Scripts/GamePlay/Rejoin/TableDigestLog.cs`
- Modify: `Assets/Osman/Scripts/TILES/TileDistrubite.cs` (okuyucular), `Assets/Osman/Scripts/GamePlay/DesyncDetector.cs` (`CurrentDeal`, `CurrentMove` okuyucuları), `Assets/Osman/Scripts/GamePlay/TurnManager.cs` (`NextTurn` sonu)
- Test: `Assets/Osman/Editor/Tests/SnapshotTests.cs`

**Interfaces:**
- Consumes: Görev 7 model; Görev 4 `TurnSerial`; Görev 3 `MeldAreaOfSeat`, `DropAreaOfSeat`.
- Produces:
  ```csharp
  public static class TableDigest { public static uint Of(TableSnapshot s); }   // FNV-1a 32, UTF-8 Canonical(false)
  public sealed class DigestRing {
      public DigestRing(int capacity = 16);
      public void Put(int round, int serial, uint digest);   // aynı anahtar üzerine yazar; doluysa en eskisi düşer
      public bool TryGet(int round, int serial, out uint digest);
  }
  public static class SnapshotBuilder { public static TableSnapshot FromScene(); }
  public static class TableDigestLog {
      public static readonly DigestRing Ring;          // master'ın kendi özetleri
      public static TableSnapshot Last { get; }        // son tur sınırında kurulan
      public static uint LastDigest { get; }
      public static void AtTurnBoundary();             // master ya da Debug.isDebugBuild: kurar; master Ring.Put; geliştirme yapısında "[OZET] {round}.{serial} {digest:x8}"
  }
  // TileDistrubite okuyucuları
  public List<List<Tiles>> MeldsOfQue(int que); public List<List<Vector2Int>> PositionsOfQue(int que);
  public List<(int seat, Tiles tile)> DiscardHistory();   // droppedTiles sırasıyla, yalnız hâlâ bir koltuğun atık alanında duranlar
  ```
- `FromScene`: tahta, koltuk alanının `GetComponentsInChildren<TileUI>()`'inden (`meldType = parent.parent.GetSiblingIndex()`, `slot = parent.GetSiblingIndex()`, `AppendSyncState` ile aynı); ad ile alan araması yok, bot koltukları da dahil. `round` = `CurrentRound` oda özelliği. `penalties` = `MatchScores.Instance.CurrentHandPenalties`.
- `TurnManager.NextTurn` sonunda, `DesyncDetector.Report`'tan **sonra** `TableDigestLog.AtTurnBoundary()`.
- Özete girmeyen yalnız iki meta alan vardır: `turnStartTime`, `devDeal/devMove`. Diğer her alan özettedir; aşağıdaki test bunu korur.

- [ ] **Step 1: Başarısız testler**

```csharp
static readonly (string name, Action<TableSnapshot> change)[] Mutations = {
    ("turn", s => s.currentTurn = 4), ("serial", s => s.turnSerial++), ("round", s => s.round++),
    ("ready", s => s.ready = !s.ready), ("ended", s => s.ended = !s.ended), ("seats", s => s.seatOfQue[1] = 2),
    ("ind", s => s.indicator.number = 8), ("drop", s => s.dropTile = null), ("pileOrder", s => s.pile.Reverse()),
    ("pileCount", s => s.pile.RemoveAt(0)), ("handOrder", s => s.hands[0].Reverse()), ("handMove", s => { s.hands[1].Add(s.hands[0][0]); s.hands[0].RemoveAt(0); }),
    ("meldTile", s => s.melds[1][0].RemoveAt(0)), ("meldPos", s => s.meldPositions[1][0][0] = new Vector2Int(9, 9)),
    ("discardSeat", s => s.discards[0] = new SnapDiscard { seat = 4, tile = s.discards[0].tile }), ("discardOrder", s => s.discards.Reverse()),
    ("boardSlot", s => s.boards[2][0] = new SnapBoardTile { tile = s.boards[2][0].tile, meldType = s.boards[2][0].meldType, slot = 7 }),
    ("boardType", s => s.boards[2][0] = new SnapBoardTile { tile = s.boards[2][0].tile, meldType = 2, slot = s.boards[2][0].slot }),
    ("opened", s => s.opened.Add(3)), ("pairs", s => s.openedPairs.Add(1)),
    ("openValue", s => s.openValues[0] = new SnapOpenValue { que = 2, value = 69 }), ("penalty", s => s.penalties[0] = new SnapPenalty { seat = 1, amount = 251, reason = 1 }),
};
[Test] public void Digest_Changes_For_Every_Field() {
    uint baseD = TableDigest.Of(Sample());
    foreach (var (name, change) in Mutations) { var s = Sample(); change(s); Assert.AreNotEqual(baseD, TableDigest.Of(s), name); }
}
[Test] public void Digest_Ignores_Meta_And_Joker_Values() {
    var s = Sample(); uint d = TableDigest.Of(s);
    s.turnStartTime += 3; s.devDeal = 9; s.devMove = 1;
    s.hands.SelectMany(h => h).First(t => t.type == TileType.Joker).number = 12;
    Assert.AreEqual(d, TableDigest.Of(s));
}
[Test] public void Ring_Keeps_Last_Sixteen() {
    var r = new DigestRing(16); for (int i = 1; i <= 20; i++) r.Put(1, i, (uint)i);
    Assert.IsFalse(r.TryGet(1, 4, out _)); Assert.IsTrue(r.TryGet(1, 5, out uint v)); Assert.AreEqual(5u, v);
    r.Put(1, 20, 99); Assert.IsTrue(r.TryGet(1, 20, out v)); Assert.AreEqual(99u, v); Assert.IsFalse(r.TryGet(2, 20, out _));
}
```
- [ ] **Step 2: Düştüğünü gör. Step 3: Yaz. Step 4: Tam paket geçer.**
- [ ] **Step 5: Tek editör.** `ST -Solo -Mode smart -Seed 11` → `PASS`; konsolda `[OZET]` satırları her `NextTurn`'de.
- [ ] **Step 6: Kontrolcü.** `ST -Deals 2 -Mode smart` → `PASS`; dört editörün `U console get --tail 4000` çıktısındaki `[OZET]` satırları her anahtar için birebir aynı (sahneden kurmanın istemciden bağımsız olduğunun kanıtı). Fark varsa rapora o anahtarın dört `Canonical` dökümü (geçici log) yazılır, iş durur.
- [ ] **Step 7: Commit** — `Geri dönüş: masa özeti (yayın yapısında da) ve sahneden fotoğraf`

---

### Task 9: Kurulum modu ve fotoğrafı sahneye kurma

**Files:**
- Create: `Assets/Osman/Scripts/GamePlay/Rejoin/RestoreGate.cs`, `Assets/Osman/Scripts/GamePlay/Rejoin/SnapshotApplier.cs`
- Modify: `Assets/Osman/Scripts/TILES/TileDistrubite.cs` (aşağıdaki RPC'lerin ilk satırı; çıkarılan yöntemler), `Assets/Osman/Scripts/GamePlay/TurnManager.cs` (`NextTurn`, `SetTurnDirectly`, `RPC_ResetTurnForNewRound` kapıları; `IsPlayerTurn`; `RestoreTurn`), `Assets/Osman/Scripts/GamePlay/GameManager.cs` (`Awake`, `Start`, oyun RPC kapıları, `RPC_PrepareNextRound`), `Assets/Osman/Scripts/GamePlay/ScoreManager.cs` (`Start`, `RestoreOpenState`, `OpenFlagsConsistent`), `Assets/Osman/Scripts/GamePlay/SeatManager.cs` (`OnJoinedRoom`), `Assets/Osman/Scripts/GamePlay/PenaltySystem.cs` (`ApplyPenaltyRPC` kapısı), `Assets/Osman/Scripts/Photons/RoomManager.cs` (`OnJoinedRoom`), `Assets/Osman/Scripts/GamePlay/Score/MatchScores.cs` (`RestoreCurrent`), `Assets/Osman/Scripts/GamePlay/DesyncDetector.cs` (`AdoptKey`), `Assets/Osman/Scripts/GamePlay/SyncAutoTestData.cs` + `SyncAutoTest.cs` + `Tools/SyncTest/Run-SyncTest.ps1` (`-SelfRestore`), `Assets/Resources/Localization.csv`
- Test: `Assets/Osman/Editor/Tests/RejoinTests.cs`

**Interfaces:**
- Consumes: Görev 7 `SnapshotCodec`, Görev 8 `SnapshotBuilder`, `TableDigest`; Görev 5 `CaptureTurnStart`.
- Produces:
  ```csharp
  public static class RestoreQueue {
      // nextTurnSerials[i] = i. kuyruk öğesi NextTurn ise serisi, değilse -1. Seri K'li NextTurn'den sonraki ilk indeks; K yoksa 0.
      public static int FirstToReplay(IReadOnlyList<int> nextTurnSerials, int snapshotSerial);
  }
  public static class RestoreGate {
      public static bool Live { get; }                           // varsayılan true
      public static void Enter();                                // Live=false, kuyruk boş
      public static void Reset();                                // Live=true, kuyruk boş (normal masa girişi, SubsystemRegistration)
      public static bool Defer(Action replay, int nextTurnSerial = -1);   // !Live: sıraya koy, true döner; Live: false
      public static int GoLive(int snapshotSerial);              // K dahil öncesini atar, Live=true, kalanları sırayla oynatır; oynatılan sayı
      public static void GoLiveFromNewHand();                    // kuyruğu atar, Live=true
  }
  public static class SnapshotApplier { public static void Apply(TableSnapshot s); }
  // TileDistrubite (çıkarılan, sessiz, animasyonsuz)
  public void ClearTableVisuals(bool immediate);              // ResetTableAndRedistribute'un adım 1 ve 4'ü + droppedTiles; o RPC bunu false ile çağırır
  public void LoadData(TableSnapshot s);                       // allTiles, eller (aynı List nesneleri, Clear+AddRange), meltedTiles+konumlar, seatOfQue
  public void PlaceIndicator(Tiles t);                         // SyncIndicatorTile gövdesi
  public void PlaceBoardTile(int seat, int meldType, int slot, Tiles t);   // SyncProcessedTileRPC'nin yerleştirme kısmı, MeldAreaOfSeat ile, sessiz
  public void RebuildLocalRack();                              // ıstaka TileUI'lerini hemen yok eder, InstantiateTiles(j, hand[j])
  // TurnManager
  public void RestoreTurn(int currentTurn, int serial, double turnStartTime);   // + ResetTurnFlags, autoPlaying=false, UpdateTurnIndicators
  // ScoreManager
  public void RestoreOpenState(TableSnapshot s, int myQue);    // envanter §4.3: kümeler, hasOpened*, occupiedRows*, committedMelds (kendi tahtasından), bekleyen listeler boş
  public bool OpenFlagsConsistent();                           // hasOpenedPairs == openedPairs ∋ myQue && hasOpenedSeries == (kendi tahtasında seri/grup taşı var)
  // MatchScores
  public void RestoreCurrent(IEnumerable<PenaltyEntry> entries);
  // DesyncDetector
  public static void AdoptKey(int deal, int move);
  ```
- Kapı (ortak kalıp, işleyicinin ilk satırı): `if (RestoreGate.Defer(() => Ad(args...))) return;` (`NextTurn` serisini verir). Kapılanan işleyiciler: `TurnManager.NextTurn`, `SetTurnDirectly`, `RPC_ResetTurnForNewRound`; `TileDistrubite.SyncShuffledTiles`, `SyncIndicatorTile`, `AssignPlayerQueue`, `SyncQueToSeatMapping`, `AssignQueueToPlayer`, `AssignQueueComplete`, `RemoveTileFromPlayerList`, `AddTileFromMiddlePlayerList`, `AddTileFromDropPlayerList`, `MergeValidpers`, `UnMergeValidPers`, `DeactivatePlayerTile`, `DeactivatePlayerTileByIndex`, `MeldTiles`, `AddTileToPlayerHand`, `SyncProcessedTileRPC`, `ReturnTileToSide`, `RemoveTileFromPlayerListByValue`, `ResetTableAndRedistribute`, `BotDiscardTileRPC`, `InstantiateActiveTiles`, `RemoveActiveTileFromPlayerList`; `GameManager.ShowPenaltyRPC`, `SyncOpenedPairsRPC`, `SyncOpenedPlayerRPC`, `OpenValueRPC`, `FinishGameRPC`, `OnGameEndedDetailedRPC`, `RollbackTurn`; `PenaltySystem.ApplyPenaltyRPC`. `RPC_PrepareNextRound` kapılanmaz: `!Live` ise önce `GoLiveFromNewHand()` (yeni el hattının tamamı işlenir).
- Giriş: `GameManager.Awake`: `PhotonNetwork.InRoom && LocalPlayer.HasRejoined && SeatsLocked` ise `RestoreGate.Enter()` ve `UIRoot.ShowLoading(Loc.T("rejoin.joining_game"), false)`; değilse `RestoreGate.Reset()`. `IsPlayerTurn`'deki `live` = `RestoreGate.Live`.
- Taze başlangıç yan etkileri kurulumda çalışmaz: `RoomManager.OnJoinedRoom` `HasRejoined` ise hiçbir şey yapmaz; `SeatManager.OnJoinedRoom` `HasRejoined` ise `HideLoading`, `AssignPositionAndInstantiate`, `RequestWantedSeat` ve master dalını atlar (yalnız `UpdateSeatDisplay`); `ScoreManager.Start` `!Live` iken `PlayerScore = 0`, `turnManager.StartGame()` ve `InitializeScores`'u atlar; `GameManager.Start` `!Live` iken `SetTableLimit(51)`'i atlar.
- `Apply` sırası (envanter §2 sıralama kısıtları): `ClearTableVisuals(true)` → `LoadData` → `SeatManager.UpdateSeatDisplay` → `PlaceIndicator` → `PlaceRemainingTilesInMiddleContainer` → atıklar zaman sırasıyla `InstatiateSideTiles(QueOfSeat(seat), tile)` → `dropTile = s.dropTile` → `PlaceBoardTile` her koltuk → ıstaka `InstantiateTiles(j, hand[j])` → `RecalculateAllAvailableSlots` → `ScoreManager.RestoreOpenState` → `GameManager` `isGameReady/isGameEnded`, `OpenValues` + `OpenValueShown`, yandan alma kaydı null → `MatchScores.RestoreCurrent` → `TurnManager.RestoreTurn` → `DesyncDetector.AdoptKey` → `CaptureTurnStart(currentTurn)` → `UIManager.UpdateTileCount/UpdatePlayerStats`. Kurma sırasında ses çalınmaz; coroutine yok.
- Ağsız kanıt `-SelfRestore`: config `selfRestoreEveryTurns = 3`; test oyuncusu kendi turunun başında (çekmeden) her 3. turda `a = FromScene()` → `Apply(Decode(Encode(a)))` → `b = FromScene()`; `Of(a) == Of(b) && RackMismatch() == null` ise `[DONUS] öz-kurulum eşit`, değilse `Debug.LogError("[DONUS] öz-kurulum fark ...")` (test FAIL olur).
- Metin: `rejoin.joining_game` = `Oyuna bağlanılıyor…` / `Joining the game…`.

- [ ] **Step 1: Başarısız testler**

```csharp
[Test] public void Queue_First_To_Replay() {
    Assert.AreEqual(2, RestoreQueue.FirstToReplay(new[] { -1, 5, -1, 6, -1 }, 5));
    Assert.AreEqual(4, RestoreQueue.FirstToReplay(new[] { -1, 5, -1, 6 }, 6));
    Assert.AreEqual(0, RestoreQueue.FirstToReplay(new[] { -1, -1 }, 5));
    Assert.AreEqual(0, RestoreQueue.FirstToReplay(new int[0], 5));
}
[Test] public void Gate_Replays_Only_After_Snapshot_Serial() {
    var log = new List<string>(); RestoreGate.Enter();
    Assert.IsTrue(RestoreGate.Defer(() => log.Add("draw6")));
    Assert.IsTrue(RestoreGate.Defer(() => log.Add("nt7"), 7));
    Assert.IsTrue(RestoreGate.Defer(() => log.Add("draw7")));
    CollectionAssert.IsEmpty(log);
    Assert.AreEqual(1, RestoreGate.GoLive(7));
    CollectionAssert.AreEqual(new[] { "draw7" }, log); Assert.IsTrue(RestoreGate.Live);
    Assert.IsFalse(RestoreGate.Defer(() => log.Add("x"))); RestoreGate.Reset();
}
[Test] public void Gate_New_Hand_Drops_Queue() {
    var log = new List<string>(); RestoreGate.Enter(); RestoreGate.Defer(() => log.Add("old"));
    RestoreGate.GoLiveFromNewHand(); CollectionAssert.IsEmpty(log); Assert.IsTrue(RestoreGate.Live); RestoreGate.Reset();
}
```
- [ ] **Step 2: Düştüğünü gör. Step 3: Yaz; metin satırını ekle.**
- [ ] **Step 4: Tam paket ve dil denetimi.** `U test run --mode EditMode` geçer; `U script eval` ile `LocAudit.CheckProject().Errors.Count == 0` → `true`.
- [ ] **Step 5: Tek editör.** `ST -Solo -Mode smart -Seed 11 -SelfRestore` → `PASS`; konsolda en az 3 `[DONUS] öz-kurulum eşit`, hiç `fark` yok. Ayrıca `ST -Solo -Mode smart -Seed 11` → `PASS`.
- [ ] **Step 6: Kontrolcü.** `ST -Deals 2 -Mode smart` → `PASS` (kapılar canlı istemcide etkisiz).
- [ ] **Step 7: Commit** — `Geri dönüş: kurulum modu (RPC sırası), fotoğrafı sahneye kurma`

---

### Task 10: Bağlantı katmanı

**Files:**
- Create: `Assets/Osman/Scripts/Photons/RejoinPolicy.cs`, `Assets/Osman/Scripts/Photons/ReconnectFlow.cs`, `Assets/Osman/Editor/UiKitReconnect.cs`, `Assets/Osman/Scenes/Reconnect.unity` (kurucu üretir)
- Modify: `Assets/Osman/Scripts/Photons/PhotonManager.cs` (`Start`: `AddComponent<ReconnectFlow>()`; `OnJoinRoomFailed`, `OnConnectedToMaster`), `Assets/Osman/Scripts/UI/ExitButton.cs` (`ExitGame`, `OnLeftRoom`), `Assets/Osman/Scripts/UI/MainMenuUIHandler.cs` (`EnterLobby`), `Assets/Osman/Scripts/GamePlay/GameManager.cs` (`RestartSequence` son el), `Assets/Osman/Scripts/GamePlay/SyncAutoTest.cs` (kopma bekletmesi, `restoreEntered`, `gameOverSeen`), `ProjectSettings/EditorBuildSettings.asset` (kurucu ekler), `Assets/Resources/Localization.csv`
- Test: `Assets/Osman/Editor/Tests/RejoinTests.cs`

**Interfaces:**
- Consumes: Görev 2 `RejoinRecord`, `PhotonIdentity`; Görev 9 `RestoreGate`; Görev 0 geri çağrı sırası ve 32746 süresi.
- Produces:
  ```csharp
  public enum RejoinStep { Retry, GiveUpKeep, GiveUpClear }
  public static class RejoinPolicy {
      public const float WindowSec = 30f, RetryDelaySec = 2f;
      public static RejoinStep OnFailure(short code, float elapsedSec);   // 32758, 32748 -> GiveUpClear; diğerleri (32746 dahil) pencere içinde Retry, sonra GiveUpKeep
      public static bool ShouldReconnect(bool deliberate, bool hasRecord, bool inGameScene);   // !deliberate && hasRecord && inGameScene
  }
  public class ReconnectFlow : MonoBehaviourPunCallbacks {
      public static bool DeliberateLeave;              // ExitButton.ExitGame ve maç sonu LeaveRoom öncesi true
      public static bool Active { get; }
      public static void RejoinSaved(string roomName); // soğuk açılış "Evet"
      public static void EndUnrecoverable();           // LeaveRoom(false) (DeliberateLeave=true), kayıt silinir, Notice("rejoin.game_over"), MainMenu
  #if UNITY_EDITOR
      public static float TestHoldSeconds;             // harness: ilk denemeden önce bekleme
  #endif
  }
  ```
- Uygulama açıkken kopma: `OnLeftRoom` ya da `OnDisconnected` (hangisi önce gelirse, bir kez) `ShouldReconnect` ise: `Active = true`, `ChangeScene("Reconnect")`, sahne yüklenince `ShowLoading(Loc.T("rejoin.reconnecting"), false)`; döngü: `ReconnectAndRejoin()`; `false` dönerse `ConnectUsingSettings()` ve `OnConnectedToMaster`'da `RejoinRoom(kayıt)`; `OnJoinRoomFailed(code)` → `OnFailure`; her deneme `[DONUS] deneme {n} {code}`. `GiveUpKeep` → `Active = false`, `ChangeScene("MainMenu")` (kayıt kalır, ana menüde soru çıkar). `GiveUpClear` → `EndUnrecoverable()`.
- Soğuk açılış: `EnterLobby`, lobi oturduktan ve kimlik denetiminden sonra `RejoinRecord.Load()` doluysa `UIRoot.Confirm(Loc.T("rejoin.ask_title"), Loc.T("rejoin.ask_body"), Loc.T("rejoin.ask_yes"))`; Evet → `RejoinSaved(oda)` (`AutomaticallySyncScene = true`, `ChangeScene("Reconnect")`, `RejoinRoom`); Hayır → `RejoinRecord.Clear()`, lobiye.
- Katılınca (`OnJoinedRoom`, `Active`): oda özelliği `MatchOver == true` ya da yerel dışında etkin oyuncu yoksa → `EndUnrecoverable()`; değilse `Active = false`, Table'ı PUN yükler (yalnız PUN yüklemesi; `Reconnect` sahnesinde `RoomManager` yok).
- `PhotonManager.OnJoinRoomFailed` ve `OnConnectedToMaster`: `ReconnectFlow.Active` iken hiçbir şey yapmaz.
- `ExitButton.ExitGame`: `DeliberateLeave = true`, `RejoinRecord.Clear()`, sonra bugünkü `LeaveRoom`. `ExitButton.OnLeftRoom`: yalnız `DeliberateLeave` ise lobiye (bayrak sıfırlanır). `RestartSequence` son el: master `MatchOver = true` oda özelliği yazar; herkes `DeliberateLeave = true`, kayıt silinir, `LeaveRoom()`.
- `UiKitReconnect`: menü `Okey/Sahne/Yeniden Bağlanma Sahnesi`; yalnız `Main Camera` içeren `Reconnect.unity` yazar ve `EditorBuildSettings`'e (yoksa) ekler; ikinci çalıştırmada değişiklik yok.
- Metinler (tr / en): `rejoin.reconnecting` `Yeniden bağlanılıyor…` / `Reconnecting…`; `rejoin.ask_title` `Oyuna dön` / `Return to game`; `rejoin.ask_body` `Bağlı olduğun oyuna dönmek istiyor musun?` / `Do you want to return to the game you were in?`; `rejoin.ask_yes` `Dön` / `Return`; `rejoin.game_over` `Oyun sona ermiş` / `The game has ended`; `table.leave_body` satırı `Masadan ayrılırsan bu oyuna geri dönemezsin. Yerine bot oynar.` / `If you leave, you cannot return to this game. A bot will play for you.` olur.
- `SyncAutoTest`: kopmadan önce `ReconnectFlow.TestHoldSeconds = dropOfflineSec` (`afterend`: `release.json` görünene kadar); Table'a `!RestoreGate.Live` ile girilince `restoreEntered = true`; `rejoin.game_over` bildirimi görülünce `gameOverSeen = true`, durum `Done`.

- [ ] **Step 1: Başarısız testler**

```csharp
[Test] public void Policy_Clears_Record_Only_On_Room_Or_Actor_Gone() {
    Assert.AreEqual(RejoinStep.GiveUpClear, RejoinPolicy.OnFailure(32758, 1f));
    Assert.AreEqual(RejoinStep.GiveUpClear, RejoinPolicy.OnFailure(32748, 1f));
    Assert.AreEqual(RejoinStep.Retry, RejoinPolicy.OnFailure(32746, 5f));
    Assert.AreEqual(RejoinStep.GiveUpKeep, RejoinPolicy.OnFailure(32746, 31f));
    Assert.AreEqual(RejoinStep.Retry, RejoinPolicy.OnFailure(32765, 5f));
    Assert.AreEqual(RejoinStep.GiveUpKeep, RejoinPolicy.OnFailure(0, 30f));
}
[Test] public void Policy_Reconnect_Only_For_Unexpected_Drop_In_Game() {
    Assert.IsTrue(RejoinPolicy.ShouldReconnect(false, true, true));
    Assert.IsFalse(RejoinPolicy.ShouldReconnect(true, true, true));
    Assert.IsFalse(RejoinPolicy.ShouldReconnect(false, false, true));
    Assert.IsFalse(RejoinPolicy.ShouldReconnect(false, true, false));
}
```
- [ ] **Step 2: Düştüğünü gör. Step 3: Yaz; kurucuyu iki kez çalıştır** (ikincide `git status` değişiklik göstermez).
- [ ] **Step 4: Tam paket; `LocAudit.CheckProject().Errors.Count == 0` → `true`.**
- [ ] **Step 5: Tek editör.** `ST -Solo -Mode smart -Seed 11` → `PASS` (maç sonu bilerek çıkış lobiye götürür; `[DONUS] deneme` yok).
- [ ] **Step 5b: Soru penceresi ve `RejoinRoom` yolu, tek editör.** Play dışında `script eval`: `RejoinRecord.Save("yok-oda-0000"); true`. Play'e gir (MainMenu); lobiye geçişte soru penceresi açılır → ekran görüntüsü (`.superpowers/shots-keep/donus/soru-tr.png`; metin tr ve en okunur, taşma yok). "Dön" `script eval` ile tıklatılır (UIRoot onay penceresinin evet düğmesi `onClick.Invoke()`) → konsolda `[DONUS] deneme 1 32758`, "Oyun sona ermiş" bildirimi, `[DONUS] kayıt silindi`; Play'den çık, tekrar gir → soru yok.
- [ ] **Step 6: Kontrolcü.** `ST -DropRejoin offturn -Mode smart -Seed 11` → bu aşamada beklenen **FAIL**, sebep `geri verme yok … kurulum modunda kaldı`; kopan editörün durumunda `restoreEntered=true`, konsolunda `[DONUS] deneme 1` ve `OnJoinedRoom` sonrası `Oyuna bağlanılıyor…` katmanı; diğer üç editörde hata yok, `desync 0`, maç biter. `ST -DropRejoin offturn -ReturnAfterEnd -Mode smart -Seed 11` → `PASS` (`gameOverSeen`).
- [ ] **Step 7: Commit** — `Geri dönüş: bağlantı katmanı (yeniden bağlanma, açılışta soru, bilerek çıkış)`

---

### Task 11: El sıkışma ve koltuğu geri verme

**Files:**
- Create: `Assets/Osman/Scripts/GamePlay/Rejoin/HandshakeRule.cs`, `Assets/Osman/Scripts/GamePlay/Rejoin/RejoinHandshake.cs`
- Modify: `Assets/Osman/Scripts/GamePlay/TurnManager.cs` (`NextTurn` sonu), `Assets/Osman/Scripts/GamePlay/GameManager.cs` (`RPC_PrepareNextRound`), `Assets/Osman/Scripts/GamePlay/DesyncDetector.cs` (`Store`), `Assets/Osman/Scripts/GamePlay/SyncAutoTest.cs` (`rejoinGrants`, `Dropped` → `Playing`), `Assets/Resources/Localization.csv`
- Test: `Assets/Osman/Editor/Tests/RejoinTests.cs`

**Interfaces:**
- Consumes: Görev 3 `SetSeatControl`, `BotRunningFor`; Görev 7 codec; Görev 8 `TableDigestLog`; Görev 9 `RestoreGate`, `SnapshotApplier`, `RebuildLocalRack`, `OpenFlagsConsistent`; Görev 10 `EndUnrecoverable`.
- Produces:
  ```csharp
  public enum HandshakeDecision { Grant, Wait, Again }
  public static class HandshakeRule {
      public static HandshakeDecision Decide(uint? masterDigest, uint clientDigest, bool clientReady, bool seatIsCurrentTurn, bool botRunningForSeat);
      public static string OverlayKey(int consecutiveAgains);   // < 5 -> "rejoin.joining_game", >= 5 -> "rejoin.retrying"
  }
  public sealed class HandshakeBook {                           // masa sahibinin defteri
      public void Request(int actor);                            // fotoğraf bekleyenlere
      public IReadOnlyList<int> TakePending();                   // bekleyenler -> kuruluyor; listeyi döner
      public bool IsRestoring(int actor);                        // bekliyor ya da kuruluyor
      public bool OnDecision(int actor, HandshakeDecision d);    // true = mesaj yolla. Grant: defterden çıkar; Again: bekleyenlere; Wait: false; kurulmayan aktör: false
      public void PendingToRestoring();                          // yeni el: istemci kendiliğinden canlı olur
      public void Forget(int actor);
  }
  public class RejoinHandshake : MonoBehaviourPunCallbacks, IOnEventCallback {   // DDOL, RuntimeInitializeOnLoadMethod(AfterSceneLoad); TurnManager üstünde değil
      public const byte RequestEvent = 160, SnapshotEvent = 161, ReportEvent = 162, GrantEvent = 163, AgainEvent = 164;
      public static bool Granted { get; }                         // bu odada koltuk geri verildi
      public static event Action GrantedNow;
      public static bool IsRestoring(int actor);                  // master: defterde mi
      public static void OnTurnBoundary();                        // TurnManager.NextTurn sonu, TableDigestLog.AtTurnBoundary()'den sonra
      public static void OnPrepareNextRound(bool wasLive);        // RPC_PrepareNextRound başı
  }
  ```
- Olay içerikleri: 160 `{round}` → MasterClient; 161 `SnapshotCodec.Encode` → `TargetActors`; 162 `{round, serial, (int)digest, ready}` → MasterClient; 163/164 `{round, serial}` → `TargetActors`. Hepsi güvenilir.
- İstemci: Table'da `!Live` iken `TileManager(Clone)` ve `ScoreManager(Clone)` bulunana kadar bekler; masa sahibiyse görevi `SetMasterClient` ile biti açık etkin bir insana verir, yoksa `EndUnrecoverable()`; değilse 160 yollar. 161: `Live` ise yok sayar; çözülemezse `[DONUS] bozuk fotoğraf` ve 160 yeniden; değilse `Apply` → `GoLive(s.turnSerial)` → `[DONUS] kuruldu seri {k}`. `OnTurnBoundary` (canlı, verilmemiş): `RebuildLocalRack()`, `ready = RackMismatch() == null && OpenFlagsConsistent()`, özet `TableDigestLog.LastDigest` (yoksa kurulur), 162. 163: `Granted = true`, `canDrop = IsPlayerTurn() && el == 15`, `HideLoading`, `[DONUS] geri alındı`, `GrantedNow`. 164: `Enter()`, sayaç++, `ShowLoading(Loc.T(OverlayKey(n)), false)`, `[DONUS] yeniden {n}`. `OnMasterClientSwitched` ve verilmemişse: yeni master bensem devret (yukarıdaki gibi), değilsem `Enter()` + 160.
- Masa sahibi: 160 → biti kapalı koltuk sahibiyse `Request`. `OnTurnBoundary`: `TakePending()` doluysa `TableDigestLog.Last` bir kez kodlanıp hepsine 161. 162 → `Ring.TryGet(round, serial)`; karar **o anki** durumla: `seatIsCurrentTurn = currentTurnPlayer == o koltuğun sırası`, `botRunning = BotRunningFor(sıra)`; `OnDecision` true ise: Grant → `SetSeatControl(seat, true)`, 163, `[DONUS] geri verildi koltuk {s} seri {k}`; Again → 164. `OnPlayerLeftRoom` → `Forget`. `RPC_PrepareNextRound` ilk satırı `bool wasLive = RestoreGate.Live;` (Görev 9'un `GoLiveFromNewHand`'inden önce), sonra `RejoinHandshake.OnPrepareNextRound(wasLive)`: master'da `PendingToRestoring()`; istemcide `!wasLive` ise `[DONUS] yeni elle canlı` (özet raporları sonraki `NextTurn`'lerde başlar).
- `DesyncDetector.Store`: `RejoinHandshake.IsRestoring(actor)` ise kaydı yok sayar; `expected` bu aktörleri saymaz.
- Metin: `rejoin.retrying` = `Bağlantı kurulamadı, yeniden deneniyor` / `Could not connect, retrying`.
- `SyncAutoTest`: `GrantedNow` → `rejoinGrants++`, durum `Playing`.

- [ ] **Step 1: Başarısız testler**

```csharp
[Test] public void Decide_Cases() {
    Assert.AreEqual(HandshakeDecision.Grant, HandshakeRule.Decide(0xAB, 0xAB, true, false, false));
    Assert.AreEqual(HandshakeDecision.Wait, HandshakeRule.Decide(0xAB, 0xAB, true, true, false));
    Assert.AreEqual(HandshakeDecision.Wait, HandshakeRule.Decide(0xAB, 0xAB, true, false, true));
    Assert.AreEqual(HandshakeDecision.Again, HandshakeRule.Decide(0xAB, 0xAC, true, false, false));
    Assert.AreEqual(HandshakeDecision.Again, HandshakeRule.Decide(null, 0xAB, true, false, false));
    Assert.AreEqual(HandshakeDecision.Again, HandshakeRule.Decide(0xAB, 0xAB, false, false, false));
}
[Test] public void Overlay_After_Five_Agains() {
    Assert.AreEqual("rejoin.joining_game", HandshakeRule.OverlayKey(4)); Assert.AreEqual("rejoin.retrying", HandshakeRule.OverlayKey(5));
}
[Test] public void Book_Grants_Once_And_Forgets_Leavers() {
    var b = new HandshakeBook(); b.Request(5); b.Request(5);
    Assert.IsTrue(b.IsRestoring(5)); CollectionAssert.AreEqual(new[] { 5 }, b.TakePending()); CollectionAssert.IsEmpty(b.TakePending());
    Assert.IsFalse(b.OnDecision(5, HandshakeDecision.Wait));
    Assert.IsTrue(b.OnDecision(5, HandshakeDecision.Again)); CollectionAssert.AreEqual(new[] { 5 }, b.TakePending());
    Assert.IsTrue(b.OnDecision(5, HandshakeDecision.Grant)); Assert.IsFalse(b.IsRestoring(5));
    Assert.IsFalse(b.OnDecision(5, HandshakeDecision.Grant));   // ikinci geri verme yok
    b.Request(7); b.Forget(7); Assert.IsFalse(b.IsRestoring(7)); CollectionAssert.IsEmpty(b.TakePending());
    b.Request(8); b.PendingToRestoring(); CollectionAssert.IsEmpty(b.TakePending()); Assert.IsTrue(b.OnDecision(8, HandshakeDecision.Grant));
}
```
- [ ] **Step 2: Düştüğünü gör. Step 3: Yaz; metni ekle. Step 4: Tam paket; dil denetimi 0 hata.**
- [ ] **Step 5: Tek editör.** `ST -Solo -Mode smart -Seed 11 -SelfRestore` → `PASS`.
- [ ] **Step 6: Kontrolcü.** `-Mode smart -Seed 11` ile: `-DropRejoin offturn`, `before-draw`, `after-draw`, `after-open`, `two`, ve `-Deals 2 -DropRejoin hand-end` → hepsi `PASS`, `desync 0`; kopan editörde `[DONUS] kuruldu` (ya da `hand-end`'de `yeni elle canlı`) ve `[DONUS] geri alındı`; dört editörde `[SKOR]` satırları aynı; `[DONUS] yeniden` sayısı rapora yazılır. Uzun koşular arka planda.
- [ ] **Step 7: Commit** — `Geri dönüş: el sıkışma (fotoğraf, özet raporu, koltuğu geri verme)`

---

### Task 12: Masa sahibi kopmasının sağlamlaştırılması

**Files:**
- Create: `Assets/Osman/Scripts/GamePlay/Rejoin/MasterRecovery.cs`
- Modify: `Assets/Osman/Scripts/GamePlay/GameManager.cs` (`OnMasterClientSwitched` yeni; `RPC_PrepareNextRound` `preparedRound`; watchdog coroutine), `Assets/Osman/Scripts/GamePlay/Score/FinishGate.cs`, `Assets/Osman/Scripts/GamePlay/SeatManager.cs` (`RequestSeatRPC` `:307`, `OnPlayerEnteredRoom` `:239-244`)
- Test: `Assets/Osman/Editor/Tests/RejoinTests.cs`

**Interfaces:**
- Consumes: `MatchScores.CurrentHandPenalties`, `TileDistrubite.seatOfQue`, `HandBreakdown.Decode`.
- Produces:
  ```csharp
  public static class PenaltyRebuild {
      public static Dictionary<int, int> FromLog(IEnumerable<(int seat, int amount)> log, int[] seatOfQue);   // sıra 1..4 hep var
  }
  public enum RecoveryAction { None, SendPrepare, AdvanceAndPrepare, SendRedeal }
  public static class NewHandRecovery {
      public static RecoveryAction Decide(int currentRound, int totalRounds, int finishedRound, int preparedRound, bool isGameReady);
  }
  // FinishGate
  public static void Seed(int round, int[] handArray, ref int doneRound, ref int doneWinner);   // Hand{round} varsa: doneRound = round, doneWinner = winnerSeat > 0 ? winnerSeat : -1
  ```
- `Decide` kuralları sırasıyla: `isGameReady` → None; `preparedRound == currentRound` → SendRedeal (`ResetTableAndRedistribute` AllViaServer); `finishedRound == currentRound && currentRound < totalRounds` → AdvanceAndPrepare (`NextRoundSetup(currentRound + 1)`); `finishedRound == currentRound - 1 && preparedRound != currentRound` → SendPrepare (`RPC_PrepareNextRound` AllViaServer, `CurrentRound`'a dokunmadan); diğer → None. `finishedRound` = `restartRound`, `preparedRound` = `RPC_PrepareNextRound`'un işlendiği `CurrentRound`.
- `GameManager.OnMasterClientSwitched` (yeni master yerelse): `scoreManager.playerScores = PenaltyRebuild.FromLog(...)` ve her sıra için `PlayerScore` aynası; `FinishGate.Seed(CurrentRound, Hand{CurrentRound}, ref finishedRound, ref finishedWinner)`; el bitmiş ya da eller arasıysa (`isGameEnded || !isGameReady`) 15 sn bekleyen coroutine `Decide` → eylem, `[ELYENI] {action}`. Görev 5'in koltuk kaybı taraması ve Görev 11'in devri zaten bu callback'tedir; sıra: puan, kapı, tarama, watchdog.
- `RequestSeatRPC` ve `OnPlayerEnteredRoom` koltuk atama koşulu: `gameIsStart || SeatsLocked` ise reddeder.
- Bilinen sınır (nota): masa sahibine giden ve yolda kalan ceza RPC'leri kaybolur; ilk elin `CountdownAndShuffle` penceresinde masa sahibi koparsa el başlamaz.

- [ ] **Step 1: Başarısız testler**

```csharp
[Test] public void Penalties_From_Log_By_Que() {
    var d = PenaltyRebuild.FromLog(new[] { (3, 250), (1, 40), (3, -100) }, new[] { 0, 3, 4, 1, 2 });   // sıra1=koltuk3, sıra3=koltuk1
    Assert.AreEqual(150, d[1]); Assert.AreEqual(0, d[2]); Assert.AreEqual(40, d[3]); Assert.AreEqual(0, d[4]);
}
[Test] public void New_Hand_Recovery() {
    Assert.AreEqual(RecoveryAction.None, NewHandRecovery.Decide(1, 3, -1, -1, true));
    Assert.AreEqual(RecoveryAction.SendRedeal, NewHandRecovery.Decide(2, 3, 1, 2, false));
    Assert.AreEqual(RecoveryAction.AdvanceAndPrepare, NewHandRecovery.Decide(1, 3, 1, -1, false));
    Assert.AreEqual(RecoveryAction.SendPrepare, NewHandRecovery.Decide(2, 3, 1, 1, false));
    Assert.AreEqual(RecoveryAction.None, NewHandRecovery.Decide(3, 3, 3, 2, false));
    Assert.AreEqual(RecoveryAction.None, NewHandRecovery.Decide(1, 3, -1, -1, false));
}
[Test] public void FinishGate_Seed_From_Hand_Property() {
    int r = -1, w = -1;
    var win = HandBreakdown.Encode(HandBreakdown.Build(2, 3, 1, false, false, false, new[] { new SeatInput { seat = 3 } }, (s, x) => 0));
    FinishGate.Seed(2, win, ref r, ref w);
    Assert.IsFalse(FinishGate.Accept(2, -1, ref r, ref w)); Assert.IsFalse(FinishGate.Accept(2, 9, ref r, ref w));
    int r2 = -1, w2 = -1;
    var draw = HandBreakdown.Encode(HandBreakdown.Build(2, 0, 1, false, false, false, new[] { new SeatInput { seat = 1 } }, (s, x) => 0));
    FinishGate.Seed(2, draw, ref r2, ref w2);
    Assert.IsFalse(FinishGate.Accept(2, -1, ref r2, ref w2)); Assert.IsTrue(FinishGate.Accept(2, 9, ref r2, ref w2));
    int r3 = -1, w3 = -1; FinishGate.Seed(2, null, ref r3, ref w3); Assert.AreEqual(-1, r3);
}
```
- [ ] **Step 2: Düştüğünü gör. Step 3: Yaz. Step 4: Tam paket geçer.**
- [ ] **Step 5: Tek editör.** `ST -Solo -Mode smart -Seed 11` → `PASS`.
- [ ] **Step 6: Kontrolcü.** `-Mode smart -Seed 11`: `-DropRejoin master` (dönüşlü) → `PASS`, eski master'da `[DONUS] geri alındı`; `-DropRejoin master -NoReturn` → `PASS`; `-Deals 2 -DropRejoin master-hand-end -NoReturn` → `PASS` (ikinci el dağıtıldı, `[SKOR] el 2` üç editörde aynı); `-Deals 2 -DropRejoin master-hand-end` (dönüşlü) → `PASS`. Yeni masa sahibindeki `[ELYENI]` satırları rapora yazılır.
- [ ] **Step 7: Commit** — `Geri dönüş: masa sahibi koparsa cezalar, el sonucu kapısı ve yeni el kurtarılır; oyunda koltuk değişmez`

---

### Task 13: Bütünün doğrulanması ve notlar

**Files:**
- Modify: `docs/superpowers/specs/2026-11-20-geri-donus-design.md` (sona "Uygulamadan devreden notlar")

- [ ] **Step 1:** `U test run --mode EditMode` → 0 başarısız; sayı rapora (379 + eklenenler). `Invoke-Pester -Script Tools\SyncTest\SyncTestLib.Tests.ps1` geçer. `LocAudit.CheckProject().Errors.Count == 0`.
- [ ] **Step 2: Kontrolcü, mevcut testler:** `ST -Deals 2 -Mode smart`, `ST -Deals 3 -Mode smart` (arka planda), `ST -Solo -Mode smart -Seed 11` → hepsi `PASS`, `desync 0`.
- [ ] **Step 3: Kontrolcü, tasarımın senaryo listesi** (`-Mode smart -Seed 11`, gerekirse 1234): sırası değilken (`offturn`), kendi turunda çekmeden önce (`before-draw`), çektikten sonra (`after-draw`), açtıktan sonra (`after-open`), masa sahibinin kopması (`master`), el sonu ekranında (`-Deals 2 hand-end`), iki oyuncu aynı anda (`two`), kopup dönmeden maçın bitmesi (`offturn -ReturnAfterEnd`). Geçme ölçütü: `PASS`, `desync 0`, dönen editörde `[DONUS] geri alındı` (sonuncuda `gameOverSeen`), dört editörde aynı `[SKOR]`.
- [ ] **Step 4: Klonlarda RpcList:** her klonda `script eval` → `PhotonNetwork.PhotonServerSettings.RpcList.Contains("RollbackTurn")` → `true`.
- [ ] **Step 5: Notlar** spec'in sonuna: Görev 0'ın değerleri; yeni oda özellikleri (`Ctl`, `MatchOver`), RPC (`RollbackTurn`, `NextTurn(int serial)`), olay kodları 160-164; Claude'un bu planda verdiği kararlar (aşağıdaki rapor listesi); bilinen sınırlar (atış ile `NextTurn` arasındaki ~0,4 sn'de işlenen taş; atıştan önceki 0,3 sn kayışta yazılmış işleme cezaları ve baraj; masa sahibinin insan hamleleri ile kendi yetki mesajları arasındaki sıra farkı yüzünden ara sıra `[DONUS] yeniden`; masa sahibine yoldaki ceza RPC'leri; ilk elin geri sayımında masa sahibi kopması; ikinci cihaz yok); telefonda Osman'ın deneyecekleri:
  - el ortasında uygulamayı kapatıp 2 dk içinde aç → soru → "Dön" → masa geri gelir, koltuk bir tur sınırında verilir;
  - aynı, "Hayır" → lobi; sonraki açılışta soru yok;
  - uçak modu ~10 sn → "Yeniden bağlanılıyor…", sonra masa;
  - uçak modu ~40 sn → ana menü → soru;
  - "Çıkış" (yeni metin) → sonraki açılışta soru yok;
  - o yokken maç biter → "Oyun sona ermiş".
- [ ] **Step 6: Commit** — `Oyuna geri dönüş: doğrulama notları`
