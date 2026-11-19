# Ortak UI kiti ve kalıcı katman — uygulama planı (paket 1/4)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Oyunun tüm ekranlarının kullanacağı tek yazı tipi, tek buton/pencere prefab seti ve sahneler arası yaşayan bildirim / yükleme / onay / geri tuşu katmanı; mevcut hatalar bu katmanla oyuncuya gösterilir.

**Architecture:** `UIRoot`, `Resources/UIRoot.prefab`'dan ilk kullanımda kendini kuran `DontDestroyOnLoad` tekil nesnedir; sahnelere elle konmaz. Karar veren mantık (pencere yığını, yükleme zaman aşımı, hata metni) Unity'siz saf sınıflardadır ve EditMode testiyle sabitlenir. Prefab'lar ve font varlıkları tek bir Editor betiğiyle üretilir, böylece yeniden kurulabilir.

**Tech Stack:** Unity uGUI 2.0, TextMeshPro, Photon PUN 2, NUnit EditMode testleri (`Assets/Osman/Editor/Tests`, asmdef yok), `unity` CLI + `com.unity.pipeline`.

**Spec:** `docs/superpowers/specs/2026-11-19-menu-ui-yenileme-design.md`

Yöneten: ana oturum (Fable) planlar ve doğrular; görevleri alt ajanlar sırayla yapar. Aynı anda tek ajan Editörü ve sahneleri kullanır. Paket 2 (giriş), 3 (lobi) ve 4 (ayarlar, profil) bu paket bittikten sonra kendi planlarıyla yazılır: buradaki prefab ve API adlarına dayanırlar.

## Global Constraints

- Yeni paket ya da tween kütüphanesi eklenmez; animasyon coroutine ile.
- Sahne YAML'ı elle yazılmaz. Sahne ve prefab değişiklikleri çalışan ana Editörde `unity command script eval` ya da Editor betiğiyle yapılır. CLI: `C:\Users\osman\AppData\Local\Unity\bin\unity.exe`, proje yolu `F:\Documents\UnityProjects\51Okey`.
- `EntryFlow`, `GameServices`, `GoogleLinkFlow` mantığına ve koltuk kurallarına dokunulmaz.
- `Debug.LogError` eklenmez: senkron test koşucusu hata logunu FAIL sayar. Beklenen durumlar `LogWarning`.
- Yeni `[PunRPC]` yok.
- ChangaOne SDF'de ve Changa'da emoji yok: simgeler sprite ya da düz metin.
- Dokunma alanı en az 48dp: 1920x1080 referansta en az 120 birim yükseklik (buton), 96 birim (kapat butonu).
- Buton renk anlamları: yeşil = ana eylem, turuncu = ikincil, teal = üçüncül, kırmızı = yıkıcı ya da kapat.
- Kod yorumları ve oyuncuya görünen metinler Türkçe; mevcut kodun yorum yoğunluğu ve adlandırması izlenir.
- Bu pakette Canvas ölçeği ve SafeArea değiştirilmez: MainMenu ve LobbyMenu paket 2 ve 3'te yeniden dizilirken 1920x1080 / Match 0.5'e çekilir. Table sahnesinin yerleşimine dokunulmaz.
- Commit yalnızca Osman izin verdiyse atılır; mesaj biçimi repodaki gibi Türkçe tek satır.

## Review Focus

1. Yükleme açıldı, kapatan çağrı hiç gelmedi (Photon yanıt vermedi): 15 sn sonra kendiliğinden kapanır ve "Bağlantı zaman aşımına uğradı" bildirimi çıkar. → Görev 2, `UiLoadingTimerTests`.
2. Pencere açıkken sahne değişti ya da üst nesne kapatıldı: yığında ölü kayıt kalmaz, sonraki geri tuşu çıkış onayına düşmez ya da hata atmaz. → Görev 2, `UiBackStackTests.DeadEntriesAreSkipped`.
3. Yükleme açıkken geri tuşu: yutulur, pencere kapanmaz, çıkış onayı açılmaz. → Görev 2, `UiBackStackTests.BackIsSwallowedWhileLoading`.
4. Onay açıkken ikinci onay istenir ya da geri tuşuna basılır: ilk onay `false` ile çözülür, hiçbir `Task` askıda kalmaz. → Görev 3, PlayMode elle kontrol + `UiBackStackTests.BackClosesTopOnly`.
5. Çıkıştan sonra MainMenu yeniden yüklenir: ikinci bir `UIRoot` oluşmaz. → Görev 3, doğrulama adımı.
6. Büyük harfe çevrilen Türkçe metin ("MİSAFİR", "ŞİFRE", "ĞÜNEŞ"): hiçbir harf yedek fonttan gelmez. → Görev 1, glif testi.

---

### Task 1: Changa yazı tipi

**Files:**
- Create: `Assets/Fonts/Changa-Bold.ttf`, `Assets/Fonts/Changa-ExtraBold.ttf`, `Assets/Fonts/Changa-OFL.txt`
- Create: `Assets/Fonts/Changa-Bold SDF.asset`, `Assets/Fonts/Changa-ExtraBold SDF.asset`
- Create: `Assets/Osman/Editor/UiKitBuilder.cs` (bu görevde yalnızca font bölümü)
- Modify: `Assets/Osman/Scenes/MainMenu.unity`, `LobbyMenu.unity`, `Table.unity`, `Assets/Resources/RoomItem.prefab` (yalnızca TMP font başvuruları)
- Delete: `Assets/Fonts/ChangaOne-*` (son adımda, koşullu)

**Interfaces:**
- Produces: `UiKitBuilder.BuildFonts()` (menü: `Okey/UI Kit/Fontları Kur`), `UiKitBuilder.SwapFonts()` (menü: `Okey/UI Kit/Fontları Değiştir`). Font varlık yolları yukarıdaki gibi; sonraki görevler bu yolları kullanır.

- [ ] **Step 1: Osman'dan indirme izni al.** Tek dosya: `Changa[wght].ttf` (değişken font) ve `OFL.txt`, kaynak `https://github.com/google/fonts/tree/main/ofl/changa`. İzin gelmeden indirme yapılmaz.

- [ ] **Step 2: İndir ve sabit ağırlıkları üret.** Dosyalar önce boş bir geçici klasöre iner. `fontTools` kurulu (`python -c "import fontTools"`). Üretim:

```bash
python -I -m fontTools.varLib.instancer "<tmp>/Changa[wght].ttf" wght=700 -o Assets/Fonts/Changa-Bold.ttf
python -I -m fontTools.varLib.instancer "<tmp>/Changa[wght].ttf" wght=800 -o Assets/Fonts/Changa-ExtraBold.ttf
```

- [ ] **Step 3: Glifleri doğrula.** İki dosyada da `ğĞşŞıİçÇöÖüÜ` karakterlerinin on ikisi `getBestCmap()` içinde olmalı.

Run: `python -I -c "from fontTools.ttLib import TTFont; [print(f, all(ord(c) in TTFont(f).getBestCmap() for c in 'ğĞşŞıİçÇöÖüÜ')) for f in ['Assets/Fonts/Changa-Bold.ttf','Assets/Fonts/Changa-ExtraBold.ttf']]"`
Expected: iki satır da `True`. `False` çıkarsa dur ve ana oturuma bildir; font seçimi yeniden yapılır.

- [ ] **Step 4: `UiKitBuilder.BuildFonts()` yaz.** Her `.ttf` için `TMP_FontAsset.CreateFontAsset` ile dinamik (Atlas Population Mode: Dynamic) varlık üretir, 1024x1024 atlas, örnekleme boyutu 90, dolgu 9; yukarıdaki `.asset` yollarına kaydeder. Mevcut varlık varsa yeniden üretmez.

- [ ] **Step 5: `UiKitBuilder.SwapFonts()` yaz.** Üç sahneyi ve `RoomItem.prefab`'ı sırayla açar; her `TMP_Text`'in `font` alanını `Changa-ExtraBold SDF` yapar (`fontSize`, renk ve materyal ön ayarı dışındaki hiçbir alana dokunmaz), kaydeder. Kaç metin değiştirdiğini sahne başına `Debug.Log` ile yazar. Bold varlığı bu görevde hiçbir metne atanmaz; paket 2-4 uzun metinlerde kullanır.

- [ ] **Step 6: Editörde çalıştır ve say.**

Run: `unity command script eval "UiKitBuilder.BuildFonts(); UiKitBuilder.SwapFonts();" --project-path F:\Documents\UnityProjects\51Okey --result-only --no-banner`
Expected: konsolda MainMenu ≈ 16, LobbyMenu ≈ 63, Table ≈ 35 metin; `console status` derleme hatası yok.

- [ ] **Step 7: Görsel kontrol.** Üç sahneyi sırayla açıp Game görünümünden ekran görüntüsü al. Taşan ya da kesilen metinleri listele: menü sahnelerindekiler paket 2-3'te düzelir, Table'dakiler masa belgesine not olarak raporlanır. Bu görevde yerleşim düzeltilmez.

- [ ] **Step 8: ChangaOne'ı sil (koşullu).** `Assets` altında ChangaOne SDF guid'ine (`4ffacc50`) başvuru kalmadıysa `Assets/Fonts/ChangaOne-*` ve `OFL.txt` silinir, `Changa-OFL.txt` kalır. Başvuru kaldıysa silme, listeyi raporla.

Run: `grep -rl "4ffacc50" Assets --include=*.unity --include=*.prefab --include=*.asset --include=*.mat`
Expected: çıktı yok.

- [ ] **Step 9: Commit (izin varsa).** `Yazı tipi: Changa (Türkçe harfler), ChangaOne kaldırıldı`

---

### Task 2: Saf mantık — pencere yığını, yükleme zamanlayıcısı, hata metinleri

**Files:**
- Create: `Assets/Osman/Scripts/UI/Kit/UiBackStack.cs`, `UiLoadingTimer.cs`, `JoinErrorText.cs`
- Test: `Assets/Osman/Editor/Tests/UiBackStackTests.cs`, `UiLoadingTimerTests.cs`, `JoinErrorTextTests.cs`

**Interfaces:**
- Produces:
  - `public enum BackResult { Swallowed, ClosedWindow, AskExit }`
  - `public class UiBackStack<T> where T : class` — `void Push(T w)`, `void Remove(T w)`, `int Count`, `BackResult Back(bool loading, System.Func<T, bool> alive, System.Action<T> close)`.
  - `public class UiLoadingTimer` — `const float Timeout = 15f`, `void Show(float now)`, `void Hide()`, `bool Active`, `bool Tick(float now)` (zaman aşımına bu çağrıda uğradıysa `true` döner ve kendini kapatır).
  - `public static class JoinErrorText` — `static string For(short returnCode)`.

- [ ] **Step 1: Testleri yaz.**

```csharp
public class UiBackStackTests
{
    static BackResult Back(UiBackStack<string> s, bool loading, System.Collections.Generic.List<string> closed,
        System.Func<string, bool> alive = null) =>
        s.Back(loading, alive ?? (_ => true), closed.Add);

    [Test] public void EmptyStack_AsksExit()
    {
        var closed = new System.Collections.Generic.List<string>();
        Assert.AreEqual(BackResult.AskExit, Back(new UiBackStack<string>(), false, closed));
        Assert.IsEmpty(closed);
    }

    [Test] public void BackClosesTopOnly()
    {
        var s = new UiBackStack<string>(); s.Push("profil"); s.Push("onay");
        var closed = new System.Collections.Generic.List<string>();
        Assert.AreEqual(BackResult.ClosedWindow, Back(s, false, closed));
        CollectionAssert.AreEqual(new[] { "onay" }, closed);
        Assert.AreEqual(1, s.Count);
    }

    [Test] public void BackIsSwallowedWhileLoading()
    {
        var s = new UiBackStack<string>(); s.Push("profil");
        var closed = new System.Collections.Generic.List<string>();
        Assert.AreEqual(BackResult.Swallowed, Back(s, true, closed));
        Assert.IsEmpty(closed); Assert.AreEqual(1, s.Count);
        Assert.AreEqual(BackResult.Swallowed, Back(new UiBackStack<string>(), true, closed));
    }

    [Test] public void DeadEntriesAreSkipped()
    {
        var s = new UiBackStack<string>(); s.Push("canlı"); s.Push("ölü");
        var closed = new System.Collections.Generic.List<string>();
        Assert.AreEqual(BackResult.ClosedWindow, Back(s, false, closed, w => w != "ölü"));
        CollectionAssert.AreEqual(new[] { "canlı" }, closed);
        Assert.AreEqual(0, s.Count);
        Assert.AreEqual(BackResult.AskExit, Back(s, false, closed, w => w != "ölü"));
    }

    [Test] public void PushTwice_KeepsOneEntry_RemoveDropsIt()
    {
        var s = new UiBackStack<string>(); s.Push("a"); s.Push("a");
        Assert.AreEqual(1, s.Count);
        s.Remove("a"); s.Remove("yok");
        Assert.AreEqual(0, s.Count);
    }
}

public class UiLoadingTimerTests
{
    [Test] public void TimesOutOnceAfter15Seconds()
    {
        var t = new UiLoadingTimer(); t.Show(100f);
        Assert.IsFalse(t.Tick(114.9f)); Assert.IsTrue(t.Active);
        Assert.IsTrue(t.Tick(115f)); Assert.IsFalse(t.Active);
        Assert.IsFalse(t.Tick(200f));
    }

    [Test] public void HideBeforeTimeout_NeverFires()
    {
        var t = new UiLoadingTimer(); t.Show(0f); t.Hide();
        Assert.IsFalse(t.Tick(999f));
    }

    [Test] public void ShowAgain_RestartsClock()
    {
        var t = new UiLoadingTimer(); t.Show(0f); t.Show(10f);
        Assert.IsFalse(t.Tick(20f)); Assert.IsTrue(t.Tick(25f));
    }
}

public class JoinErrorTextTests
{
    [TestCase((short)32765, "Masa dolu")]                 // ErrorCode.GameFull
    [TestCase((short)32764, "Masa kapanmış")]             // ErrorCode.GameClosed
    [TestCase((short)32758, "Masa artık yok")]            // ErrorCode.GameDoesNotExist
    [TestCase((short)32766, "Bu isimde bir masa var")]    // ErrorCode.GameIdAlreadyExists
    [TestCase((short)-1, "Masaya girilemedi")]
    public void MapsCodes(short code, string expected) => Assert.AreEqual(expected, JoinErrorText.For(code));
}
```

- [ ] **Step 2: Testlerin derlenmediğini / kırmızı olduğunu gör.**

Run: `unity command test run --mode EditMode --filter "UiBackStackTests|UiLoadingTimerTests|JoinErrorTextTests" --project-path F:\Documents\UnityProjects\51Okey --result-only --no-banner`
Expected: derleme hatası (tipler yok).

- [ ] **Step 3: Üç sınıfı yaz.** `JoinErrorText` sabitleri `Photon.Realtime.ErrorCode` üzerinden okur (sayı gömülmez). `UiBackStack.Back` sırası: `loading` ise `Swallowed`; değilse tepeden başlayarak `alive` olmayanları atar, ilk canlıyı yığından çıkarıp `close` ile kapatır ve `ClosedWindow` döner; canlı kalmadıysa `AskExit`.

- [ ] **Step 4: Testler yeşil.** Aynı komut. Expected: 13 test geçer. Ardından tüm EditMode testleri: hepsi geçer.

- [ ] **Step 5: Commit (izin varsa).** `UI kiti: geri tuşu yığını, yükleme zaman aşımı, hata metinleri`

---

### Task 3: Prefab'lar ve `UIRoot`

**Files:**
- Create: `Assets/Osman/Scripts/UI/Kit/UIRoot.cs`, `UiWindow.cs`, `UiPressScale.cs`
- Modify: `Assets/Osman/Editor/UiKitBuilder.cs` (prefab bölümü)
- Create (betik üretir): `Assets/Osman/Prefabs/UI/Btn_Green.prefab`, `Btn_Orange.prefab`, `Btn_Teal.prefab`, `Btn_Red.prefab`, `Btn_Close.prefab`, `Window.prefab`, `Row.prefab`, `Toggle.prefab`, `Assets/Resources/UIRoot.prefab`
- Modify: `Assets/Sprites/ui_kit/*.png.meta` yalnızca 9-dilim kenarları eksikse

**Interfaces:**
- Consumes: Task 1 font varlıkları; Task 2 `UiBackStack<T>`, `UiLoadingTimer`, `BackResult`.
- Produces:
  - `UIRoot` (statik API, ilk çağrıda `Resources.Load("UIRoot")` ile kendini kurar, `DontDestroyOnLoad`):
    - `static void Toast(string text)` — üstte 2,5 sn; yeni çağrı eskisinin yerini alır.
    - `static void ShowLoading(string text = null)`, `static void HideLoading()`, `static bool IsLoading`.
    - `static System.Threading.Tasks.Task<bool> Confirm(string title, string body, string yesLabel = "Evet", bool destructive = false)` — "Vazgeç" her zaman ikinci buton; açıkken yeni `Confirm` gelirse eski `false` ile çözülür.
    - `static void Register(UiWindow w)`, `static void Unregister(UiWindow w)`.
  - `UiWindow : MonoBehaviour` — `OnEnable` → `UIRoot.Register(this)`, `OnDisable` → `UIRoot.Unregister(this)`; `public bool closable = true`; `public UnityEngine.Events.UnityEvent onBack` (boşsa `gameObject.SetActive(false)`). `closable == false` pencere (isim sorma) geri tuşunu yutar.
  - `UiPressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler` — basılıyken ölçek 0,95.
  - Menü: `Okey/UI Kit/Prefabları Kur` → `UiKitBuilder.BuildPrefabs()`.

- [ ] **Step 1: `UiKitBuilder.BuildPrefabs()` yaz.** Var olan prefab'ın üzerine yazar (yeniden kurulabilir). Ölçüler 1920x1080 referans biriminde:
  - Butonlar: `ui_kit/btn_*` Sliced, varsayılan 420x140, etiket `Changa-ExtraBold SDF` 48 beyaz, `UiPressScale`. `Btn_Close`: `btn_red`, 96x96, etiket "X".
  - `Window`: tam ekran siyah %60 karartma (`raycastTarget` açık) + ortada `panel_slate` Sliced gövde 1100x760; çocuklar `Title` (TMP 56), `Close` (`Btn_Close`, sağ üst), `Content` (boş RectTransform), `Buttons` (HorizontalLayoutGroup, alt). Kökte `UiWindow`.
  - `Row`: `row_slate` Sliced 900x120, `Label` (TMP 40, sol), `Value` (TMP 36, sağ).
  - `Toggle`: uGUI `Toggle`, 150x76 hap; açık yeşil `#4CAF2F`, kapalı `#3C4652`, beyaz topuz.
  - `UIRoot`: Canvas (Screen Space Overlay, `sortingOrder` 1000), CanvasScaler 1920x1080 Match 0.5, GraphicRaycaster, kökte `Crystal.SafeArea` taşıyan `Safe` çocuğu. Altında kapalı duran `Toast` (üstten 60, `row_slate`, TMP 40), `Loading` (karartma + dönen 96x96 halka + TMP 40 metin), `Confirm` (`Window` örneği, iki buton: `Btn_Green`/`Btn_Red` evet + `Btn_Teal` "Vazgeç").
  - Dönen halka için yeni görsel üretilmez: `ui_kit/coin` sprite'ı Z ekseninde döner.

- [ ] **Step 2: `UIRoot.cs`, `UiWindow.cs`, `UiPressScale.cs` yaz.** `UIRoot.Update`: `Input.GetKeyDown(KeyCode.Escape)` → `UiBackStack.Back(IsLoading, w => w != null && w.isActiveAndEnabled, ...)`; `AskExit` sonucunda aktif sahne `"Table"` ise `Confirm("Masadan ayrıl", "Masadan ayrılmak istiyor musun?", "Ayrıl", true)` → evet ise sahnedeki `ExitButton.ExitGame()`; diğer sahnelerde `Confirm("Çıkış", "Oyundan çıkılsın mı?", "Çık", true)` → evet ise `Application.Quit()`. Aynı `Update`, `UiLoadingTimer.Tick(Time.unscaledTime)` `true` dönünce yüklemeyi kapatır ve `Toast("Bağlantı zaman aşımına uğradı")`. Onay penceresinin geri tuşu ve kapat butonu `false` ile çözer.

- [ ] **Step 3: 9-dilim kenarlarını doğrula.** `btn_*` sol/sağ 62, `panel_slate` 48, `row_slate` 24. Üst/alt kenar 0 ise butonlar dikeyde esner ve bozulur: `btn_*` için üst/alt kenarı görsele bakarak ayarla ve değeri raporla.

- [ ] **Step 4: Prefab'ları kur.**

Run: `unity command script eval "UiKitBuilder.BuildPrefabs();" --project-path F:\Documents\UnityProjects\51Okey --result-only --no-banner`
Expected: dokuz prefab oluşur; `console status` derleme hatası yok.

- [ ] **Step 5: PlayMode'da elle doğrula (LobbyMenu sahnesi, `script eval` ile).**
  - `UIRoot.Toast("Şifre yanlış")` → üstte görünür, 2,5 sn sonra kaybolur; art arda iki çağrıda ikincisi görünür.
  - `UIRoot.ShowLoading("Masaya giriliyor...")` → görünür, arkası tıklanmaz; 15 sn beklenince kapanır ve zaman aşımı bildirimi çıkar.
  - `UIRoot.Confirm("Deneme", "İĞŞÜÖÇ ığşüöç", "Evet")` → Türkçe harfler tek fonttan; Escape → `Task` sonucu `false`.
  - Pencere yokken Escape → "Oyundan çıkılsın mı?" onayı.
  - MainMenu → LobbyMenu → (Profil'den çıkış) → MainMenu döngüsünden sonra `Object.FindObjectsOfType<UIRoot>().Length == 1`.
  Her madde için ekran görüntüsü ya da `script eval` çıktısı rapora girer.

- [ ] **Step 6: Tüm EditMode testleri yeşil; commit (izin varsa).** `UI kiti: buton, pencere, satır, anahtar prefab'ları ve UIRoot`

---

### Task 4: Mevcut akışı katmana bağla

**Files:**
- Modify: `Assets/Osman/Scripts/UI/SceneChangeManager.cs`
- Modify: `Assets/Osman/Scripts/Photons/PhotonManager.cs` (`OnJoinRoomFailed`, `OnCreateRoomFailed`)
- Modify: `Assets/Osman/Scripts/Photons/RoomManager.cs` (`JoinRoomWithPassword`, `JoinRoomManual`, `CreateCustomRoom`, `JoinRandomRoomOrCreateAuto`)
- Modify: `Assets/Osman/Scripts/UI/ButtonFuncs.cs` (`ConfirmPasswordAndJoin`)
- Modify: `Assets/Osman/Scripts/UI/LobbyAccountUI.cs` (`AskSwitchAsync`, `ConfirmSwitchYes/No`, `AnswerSwitch`, `SignOut`)
- Modify: `Assets/Osman/Scripts/UI/ExitButton.cs` (yeni `AskExit`)
- Modify: `Assets/Osman/Scenes/LobbyMenu.unity` (panellere `UiWindow`, `ConfirmSwitchPanel` kaldırılır), `Table.unity` ("Çıkış Butonu" `onClick`)

**Interfaces:**
- Consumes: Task 3 `UIRoot.Toast/ShowLoading/HideLoading/Confirm`, `UiWindow`; Task 2 `JoinErrorText.For`.
- Produces: `public bool RoomManager.JoinRoomWithPassword(string roomName, string entered, string actual)` (şifre doğruysa `true`); `public async void ExitButton.AskExit()`.

- [ ] **Step 1: Sahne geçişi.** `SceneChangeManager.LoadSceneAsync` başında `UIRoot.ShowLoading()`, yükleme bitince `UIRoot.HideLoading()`. İlerleme `Debug.Log` satırı kaldırılır.

- [ ] **Step 2: Odaya girme ve kurma.** `RoomManager`'daki dört giriş yolunda Photon çağrısından hemen önce `UIRoot.ShowLoading("Masaya giriliyor...")` (kurmada "Masa kuruluyor..."). `PhotonManager.OnJoinRoomFailed` ve `OnCreateRoomFailed`: `UIRoot.HideLoading(); UIRoot.Toast(JoinErrorText.For(returnCode));` ve `Debug.Log` yerine `Debug.LogWarning` ile kod + mesaj. `OnJoinRandomFailed` iki sınıfta da değişmez (`RoomManager` zaten oda kuruyor). Başarı yolunda yükleme, Table sahnesi yüklenince Step 1 ile kapanır.

- [ ] **Step 3: Yanlış şifre.** `JoinRoomWithPassword` `bool` döner; yanlışsa `Debug.LogError` satırı kaldırılır (şifreyi loga yazan `[Giriş Denemesi]` satırı da), `UIRoot.Toast("Şifre yanlış")` gösterilir, `false` döner. `ButtonFuncs.ConfirmPasswordAndJoin` yalnızca `true` dönünce şifre penceresini kapatır; `false` ise giriş alanını temizler ve pencere açık kalır.

- [ ] **Step 4: Hesap değiştirme onayı.** `LobbyAccountUI.AskSwitchAsync` gövdesi `UIRoot.Confirm(...)` çağrısına iner; başlık ve metin sahnedeki `ConfirmSwitchPanel` metinlerinden aynen alınır. `switchAnswer`, `ConfirmSwitchYes/No`, `AnswerSwitch`, `confirmSwitchPanel` alanı ve sahnedeki `ConfirmSwitchPanel` nesnesi silinir. `GoogleLinkFlowTests` değişmeden geçmelidir.

- [ ] **Step 5: Çıkış onayları.** `LobbyAccountUI.SignOut` başında `await UIRoot.Confirm("Çıkış yap", <metin>, "Çıkış yap", true)`; misafirde metin "Misafir hesabından çıkarsan ilerlemen kaybolur.", Google hesabında "Hesabından çıkılsın mı?". `ExitButton.AskExit`: `await UIRoot.Confirm("Masadan ayrıl", "Masadan ayrılmak istiyor musun?", "Ayrıl", true)` evet ise `ExitGame()`. Table sahnesindeki "Çıkış Butonu" `onClick`'i `ExitGame` yerine `AskExit`'e bağlanır. `ExitGame` imzası ve gövdesi değişmez: oyun sonu ve test oyuncuları onaysız çıkmaya devam eder.

- [ ] **Step 6: Geri tuşu için pencereler.** LobbyMenu'deki `CreateRoomPanel`, `PasswordPanel`, `ProfilePanel`, `SettingsPanel` ve mağaza kökünün her birine `UiWindow` eklenir; `onBack` olayı o panelin mevcut kapatma metoduna bağlanır (`CloseCreateRoomPanel`, `CancelPasswordPanel`, `CloseProfile`, `CloseSettings`, `CloseShop`). MainMenu'deki `NamePanel`'e `closable = false` ile eklenir.

- [ ] **Step 7: Doğrula.**
  - Tüm EditMode testleri geçer.
  - PlayMode, tek editör: yanlış şifre → bildirim, pencere açık; var olan isimle masa kur → "Bu isimde bir masa var"; her açık pencerede Escape o pencereyi kapatır; lobide Escape → çıkış onayı; masada "Çıkış Butonu" → onay, "Vazgeç" masada tutar.
  - `powershell -ExecutionPolicy Bypass -File Tools\SyncTest\Run-SyncTest.ps1 -Solo -Mode smart -Seed 11` (arka planda): hata ve `[BUG]` yok.
  - Klonlar açıksa `Run-SyncTest.ps1 -Deals 1`: geçer. Kapalıysa bu adım atlanır ve raporda "çok editörlü test koşulmadı" yazılır; bot modu onun yerine geçmez.

- [ ] **Step 8: Commit (izin varsa).** `UI kiti bağlandı: yükleme, hata bildirimleri, onaylar, geri tuşu`

---

## Paket sonu doğrulama (ana oturum)

- Dört görevin diff'i okunur; alt ajan raporuna güvenilmez.
- EditMode testleri ana oturumdan bir kez daha koşulur.
- Telefonda (Redmi, Wi-Fi APK) elle: Türkçe harfler, geri tuşu, çentik altında kalan bildirim var mı.
- Table sahnesinde font değişiminden taşan metinler masa belgesi için not edilir.
