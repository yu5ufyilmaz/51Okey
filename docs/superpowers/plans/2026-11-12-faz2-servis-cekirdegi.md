# Faz 2: Servis Çekirdeği Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Oyuncunun kimliği, profili (isim, coin, level) ve coin kapısı tek bir servis katmanından geçsin; bu fazda hepsi yerel çalışsın (Firebase yok), dört editörün her biri kendi yerel oyuncusu olsun ve mevcut testler aynen geçsin.

**Architecture:** `Assets/Osman/Scripts/Services/` altında arayüzler (`IAuthService`, `IProfileStore`, `IWallet`, `IAdsService`), yerel uygulamalar (`LocalAuthService`, `LocalProfileStore`, `NullAdsService`) ve tek başlangıç noktası `GameServices`. Oyun kodu yalnızca `GameServices` ve arayüzleri görür. `PhotonManager` bağlanmadan önce `GameServices.InitAsync()` bekler; yerel uygulamada bu aynı karede biter, yani açılış zamanlaması değişmez. Faz 3 aynı arayüzlere Firebase uygulamasını ekler.

**Tech Stack:** Unity 6000.3.10f1, C# (Assembly-CSharp, asmdef ve namespace yok), NUnit EditMode (`Assets/Osman/Editor/Tests/`), `JsonUtility`, PowerShell 5.1, Pester 3.4, `unity` CLI.

**Spec:** `docs/superpowers/specs/2026-11-12-google-play-yayin-design.md` (bölüm 5.1, 5.3, 6, 11, 13 bu fazı bağlar)

**Kim yapar:** Task 1-4 saf kod ve test; orta seviye modelle alt ajana verilebilir. Task 5 dört editörü ve telefonu sürer; oturumun sahibi (ana ajan) yapar.

## Global Constraints

- Bu fazda **sahne, prefab, `ProjectSettings/`, `Packages/` dosyalarına dokunulmaz.** Yeni `[PunRPC]` yok. `PhotonNetwork.AuthValues` / `UserId` ayarlanmaz.
- Yeni kodda `using Firebase`, `using GoogleMobileAds`, `using Unity.Services` yok.
- Beklenen durumlar (dosya yok, bozuk dosya, yetersiz bakiye) `Debug.LogWarning` ile loglanır, **asla `Debug.LogError`**: test koşucusu her `LogError`'u FAIL sayar.
- Kod stili çevredeki kod gibi: global namespace, Türkçe tek satırlık "neden" yorumları.
- Sayılar tek yerde: `GameConfig` (başlangıç 1000 coin, ödüllü reklam 50 coin, günde 5, geçiş reklamı arası 180 sn, oyun başı 10 XP, kazanana 25 XP, level başına 100 XP).
- Yerel kimlik `local-<etiket>`; etiket Editörde proje klasör adı (`51Okey`, `51Okey_clone_0`…), cihazda `device`.
- Yerel profil dosyası `Application.persistentDataPath/local-profile-<etiket>.json`. Dört editör aynı `persistentDataPath`'i paylaşır; dosya adı etiketsiz olamaz.
- **Gizli bilgi:** koda, teste, loga, commit'e anahtar / şifre / token yazılmaz. Loglanabilecek tek kimlik oyuncu ID'sidir. Her commit'ten önce `Tools/Security/Check-Secrets.ps1` çalışır.
- Çalışma ağacında kullanıcının ilgisiz değişiklikleri var (`ProjectSettings/PackageManagerSettings.asset`, `ProjectSettings/UnityConnectSettings.asset`): **asla `git add -A`**, dosyalar adıyla eklenir. Yeni dosyaların Unity'nin ürettiği `.meta` dosyaları da adıyla eklenir.
- PowerShell: `-ExecutionPolicy Bypass`; `.ps1` dosyaları UTF-8 BOM.
- EditMode test komutu: `powershell -ExecutionPolicy Bypass -File Tools\SyncTest\Run-EditModeTests.ps1 -Filter <Sınıf>`. C#'ta eksik tip derleme hatasıdır; RED adımında beklenen çıktı `COMPILE FAILED`'dır.
- Commit mesajı sonu: `Co-Authored-By: Claude <noreply@anthropic.com>`

## Review Focus

1. **Bozuk ya da yarım yazılmış profil dosyası** (editör çökmesi, elle düzenleme). Beklenen: oyun açılır, yeni profil oluşur, hata logu yok. → Task 3 `Profile_CorruptFile_StartsFresh`.
2. **Aynı işlem iki kez gelir** (satın alma geri çağrısı tekrarı, çift tıklama). Beklenen: coin bir kez eklenir. → Task 3 `Wallet_SameTxIdAppliesOnce`.
3. **Bakiyeden fazla harcama.** Beklenen: reddedilir, bakiye ve dosya değişmez. → Task 3 `Wallet_RejectsOverspendAndLeavesBalance`.
4. **Aynı makinede dört editör.** Beklenen: dört ayrı kimlik, dört ayrı dosya, dört ayrı varsayılan isim. → Task 2 `DefaultName_DiffersForTheFourEditors`, Task 4 `LocalProfilePath_DiffersPerTag`, Task 5 canlı adım.
5. **Aynı karede iki `InitAsync` çağrısı** (`PhotonManager.Start` ve `MainMenuUIHandler.Start`). Beklenen: tek giriş, başlangıç coin'i bir kez. → Task 3 `Profile_LoadTwiceGrantsStarterOnce`, Task 4 `Init_Twice_ReturnsSameTask`.

---

### Task 1: Gizli bilgi taraması

**Files:**
- Create: `Tools/Security/Check-Secrets.ps1`
- Test: `Tools/Security/Check-Secrets.Tests.ps1`
- Modify: `.gitignore`

**Interfaces — Produces:**
- `Check-Secrets.ps1 [-Paths <string[]>]`. `-Paths` yoksa commit'e hazırlanmış dosyaları tarar (`git diff --cached --name-only --diff-filter=ACM`). Her bulgu için `SECRET <yol>:<satır> <kural>` yazar; **eşleşen metni asla yazmaz.** Bulgu varsa exit 1; yoksa `secrets: clean (<n> files)` yazıp exit 0.
- Kurallar: `google-api-key` (`AIza` + 35 karakter), `private-key` (`-----BEGIN … PRIVATE KEY-----`), `service-account` (`"type": "service_account"`), `secret-file` (dosya adı: `.keystore`, `.jks`, `.p12`, `.pem`, `google-services.json`, `google-services-desktop.json`, `google-services.xml`, `GoogleService-Info.plist`), `admob-real-id` (`ca-app-pub-` + 16 hane; yayıncı `3940256099942544` Google'ın test kimliğidir, serbest), `assigned-secret` (`keystorePass` / `keyaliasPass` / `password` / `passwd` / `secret` / `api_key` adına tırnak içinde en az 6 karakterlik değer atanması).
- Satırda `secret-scan: allow` geçiyorsa o satır atlanır. İkili dosyalar (`.png`, `.jpg`, `.dll`, `.so`, `.aar`, `.jar`, `.apk`, `.aab`) yalnızca ada göre taranır.

- [ ] **Step 1: Başarısız testi yaz** (`Tools/Security/Check-Secrets.Tests.ps1`)

```powershell
$script = Join-Path $PSScriptRoot 'Check-Secrets.ps1'
function Scan([string]$name, [string]$text) {
    $f = Join-Path $TestDrive $name
    Set-Content -Path $f -Value $text -Encoding UTF8
    $out = & powershell -NoProfile -ExecutionPolicy Bypass -File $script -Paths $f 2>&1 | Out-String
    @{ Code = $LASTEXITCODE; Out = $out }
}

Describe 'Check-Secrets' {
    # Sahte değerler parça parça kurulur: bu dosyanın kendisi taramaya takılmasın.
    $apiKey = 'AI' + 'za' + ('x' * 35)

    It 'temiz dosyada 0 döner' { (Scan 'a.cs' 'int coins = 5;').Code | Should Be 0 }
    It 'Google API anahtarını yakalar ve değeri yazdırmaz' {
        $r = Scan 'a.cs' "string k = `"$apiKey`";"
        $r.Code | Should Be 1
        $r.Out | Should Match 'google-api-key'
        $r.Out | Should Not Match $apiKey
    }
    It 'özel anahtar bloğunu yakalar' { (Scan 'k.txt' ('-----BEGIN ' + 'PRIVATE KEY-----')).Code | Should Be 1 }
    It 'servis hesabı JSON''unu yakalar' { (Scan 's.json' ('{ "type": "service' + '_account" }')).Code | Should Be 1 }
    It 'keystore dosyasını adından yakalar' { (Scan 'x.keystore' 'bin').Code | Should Be 1 }
    It 'google-services.json dosyasını adından yakalar' { (Scan 'google-services.json' '{}').Code | Should Be 1 }
    It 'gerçek AdMob kimliğini yakalar' { (Scan 'ads.cs' ('"ca-app-pub-' + '1234567890123456/1234567890"')).Code | Should Be 1 }
    It 'Google test AdMob kimliğine izin verir' { (Scan 'ads.cs' '"ca-app-pub-3940256099942544/5224354917"').Code | Should Be 0 }
    It 'şifre atamasını yakalar' { (Scan 'b.cs' ('string keystore' + 'Pass = "hunter2222";')).Code | Should Be 1 }
    It 'izin işaretli satırı atlar' { (Scan 'b.cs' ('string pass' + 'word = "ornek-deger"; // secret-scan: allow')).Code | Should Be 0 }
}
```

- [ ] **Step 2: Testin başarısız olduğunu gör**

Run: `powershell -ExecutionPolicy Bypass -Command "Invoke-Pester Tools\Security\Check-Secrets.Tests.ps1"`
Expected: 10 test FAIL (betik yok).

- [ ] **Step 3: `Tools/Security/Check-Secrets.ps1` yaz** (yukarıdaki arayüz ve kurallar; dosya UTF-8 BOM)

- [ ] **Step 4: Testin geçtiğini gör**

Run: aynı komut. Expected: `Passed: 10 Failed: 0`.

- [ ] **Step 5: `.gitignore`'a ekle** (dosyanın sonuna, başlık yorumu `# Gizli bilgi ve hesap kimlikleri (tasarım bölüm 13)`)

```
*.keystore
*.keystore.meta
*.jks
google-services.json
google-services.json.meta
google-services-desktop.json
google-services-desktop.json.meta
/Assets/Plugins/Android/FirebaseApp.androidlib/
/Assets/Plugins/Android/FirebaseApp.androidlib.meta
GoogleService-Info.plist
*service-account*.json
```

- [ ] **Step 6: `user.keystore` dosyasını takipten çıkar** (dosya diskte kalır; yayında kullanılmayacak)

Run: `git rm --cached user.keystore`
Expected: `rm 'user.keystore'`; `ls user.keystore` dosyayı hâlâ gösterir.

- [ ] **Step 7: Commit**

```bash
git add Tools/Security/Check-Secrets.ps1 Tools/Security/Check-Secrets.Tests.ps1 .gitignore
powershell -ExecutionPolicy Bypass -File Tools/Security/Check-Secrets.ps1
git commit -m "Secret scan before commits; stop tracking the keystore"
```
Expected (tarama): `secrets: clean (3 files)`. Silinen `user.keystore` taranmaz (`--diff-filter=ACM`).

---

### Task 2: Modeller, ayarlar, arayüzler

**Files:**
- Create: `Assets/Osman/Scripts/Services/GameConfig.cs`
- Create: `Assets/Osman/Scripts/Services/PlayerProfile.cs` (`PlayerProfile`, `CoinTx`, `CoinReason`, `LinkResult`)
- Create: `Assets/Osman/Scripts/Services/IAuthService.cs`, `IProfileStore.cs`, `IWallet.cs`, `IAdsService.cs`
- Test: `Assets/Osman/Editor/Tests/GameConfigTests.cs`

**Interfaces — Produces:**

```csharp
public static class GameConfig
{
    public const long StarterCoins = 1000;
    public const long RewardedAdCoins = 50;
    public const int RewardedAdsPerDay = 5;
    public const int InterstitialMinSeconds = 180;
    public const int XpPerGame = 10;
    public const int XpWinBonus = 25;
    public const int XpPerLevel = 100;
    public static int LevelForXp(int xp);            // 1 + xp / XpPerLevel
    public static string DefaultName(string userId); // "Oyuncu" + 4 hane; aynı ID her zaman aynı ismi verir
}

[System.Serializable]
public class PlayerProfile
{
    public string UserId, DisplayName;
    public long Coins;
    public int Level = 1, Xp;
    public bool NoAds;
    public int SchemaVersion = 1;
}

public enum CoinReason { StarterGrant, RewardedAd, Purchase, TableFee, TablePayout }
public enum LinkResult { Linked, SwitchedToExistingAccount, Cancelled, Failed }

public readonly struct CoinTx
{
    public readonly string Id; public readonly long Amount; public readonly CoinReason Reason; public readonly string Reference;
    public CoinTx(string id, long amount, CoinReason reason, string reference = "");
}

public interface IAuthService
{
    string UserId { get; }              // giriş bitmeden null
    bool IsGuest { get; }
    System.Threading.Tasks.Task SignInAsync();
    System.Threading.Tasks.Task<LinkResult> LinkGoogleAsync();
    System.Threading.Tasks.Task DeleteAccountAsync();   // yalnızca kimlik; profil IProfileStore.DeleteAsync ile silinir
}

public interface IProfileStore
{
    PlayerProfile Current { get; }      // yüklenmeden null
    event System.Action<PlayerProfile> Changed;
    System.Threading.Tasks.Task LoadOrCreateAsync(string userId, string defaultName);
    System.Threading.Tasks.Task SetDisplayNameAsync(string name);
    System.Threading.Tasks.Task AddXpAsync(int xp);
    System.Threading.Tasks.Task DeleteAsync();
}

public interface IWallet
{
    long Coins { get; }
    System.Threading.Tasks.Task<bool> ApplyAsync(CoinTx tx);
}

public interface IAdsService
{
    System.Threading.Tasks.Task InitializeAsync();
    bool RewardedReady { get; }
    System.Threading.Tasks.Task<bool> ShowRewardedAsync(string placement);
    void TryShowInterstitial(string placement);
}
```

- [ ] **Step 1: Başarısız testi yaz** (`GameConfigTests.cs`)

```csharp
using System.Linq;
using NUnit.Framework;

public class GameConfigTests
{
    [Test]
    public void DefaultName_IsOyuncuPlusFourDigits() =>
        StringAssert.IsMatch(@"^Oyuncu\d{4}$", GameConfig.DefaultName("local-51Okey"));

    [Test]
    public void DefaultName_IsStableForSameUser() =>
        Assert.AreEqual(GameConfig.DefaultName("abc"), GameConfig.DefaultName("abc"));

    [Test]
    public void DefaultName_DiffersForTheFourEditors()
    {
        var ids = new[] { "local-51Okey", "local-51Okey_clone_0", "local-51Okey_clone_1", "local-51Okey_clone_2" };
        Assert.AreEqual(4, ids.Select(GameConfig.DefaultName).Distinct().Count());
    }

    [TestCase(0, 1)]
    [TestCase(99, 1)]
    [TestCase(100, 2)]
    [TestCase(250, 3)]
    public void LevelForXp_OneLevelPerHundredXp(int xp, int level) =>
        Assert.AreEqual(level, GameConfig.LevelForXp(xp));

    [Test]
    public void CoinTx_KeepsItsFields()
    {
        var tx = new CoinTx("t1", -40, CoinReason.TableFee, "oda-7");
        Assert.AreEqual("t1", tx.Id);
        Assert.AreEqual(-40, tx.Amount);
        Assert.AreEqual(CoinReason.TableFee, tx.Reason);
        Assert.AreEqual("oda-7", tx.Reference);
    }
}
```

- [ ] **Step 2: Başarısız olduğunu gör**

Run: `powershell -ExecutionPolicy Bypass -File Tools\SyncTest\Run-EditModeTests.ps1 -Filter GameConfigTests`
Expected: `COMPILE FAILED` (`GameConfig` yok).

- [ ] **Step 3: Altı dosyayı yukarıdaki imzalarla yaz.** `DefaultName` için `string.GetHashCode` kullanma (çalışma zamanları arasında sabit değil); karakterler üzerinden sabit bir özet (FNV-1a) al, `% 10000`, `"D4"`.

- [ ] **Step 4: Geçtiğini gör**

Run: aynı komut. Expected: `passed 8/8`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Osman/Scripts/Services.meta Assets/Osman/Scripts/Services/GameConfig.cs* Assets/Osman/Scripts/Services/PlayerProfile.cs* Assets/Osman/Scripts/Services/IAuthService.cs* Assets/Osman/Scripts/Services/IProfileStore.cs* Assets/Osman/Scripts/Services/IWallet.cs* Assets/Osman/Scripts/Services/IAdsService.cs* Assets/Osman/Editor/Tests/GameConfigTests.cs*
powershell -ExecutionPolicy Bypass -File Tools/Security/Check-Secrets.ps1
git commit -m "Service interfaces, player profile model and game config"
```

---

### Task 3: Yerel kimlik ve yerel profil / cüzdan

**Files:**
- Create: `Assets/Osman/Scripts/Services/Local/LocalAuthService.cs`
- Create: `Assets/Osman/Scripts/Services/Local/LocalProfileStore.cs`
- Test: `Assets/Osman/Editor/Tests/LocalServicesTests.cs`

**Interfaces:**
- Consumes: Task 2'deki tüm tipler.
- Produces:
  - `public class LocalAuthService : IAuthService` — `LocalAuthService(string instanceTag)`. `SignInAsync` sonrası `UserId == "local-" + instanceTag`, `IsGuest == true`. `LinkGoogleAsync` → `LinkResult.Failed`. `DeleteAccountAsync` → `UserId = null`.
  - `public class LocalProfileStore : IProfileStore, IWallet` — `LocalProfileStore(string filePath)`. Tüm metotlar tamamlanmış `Task` döner (bekleme yok).
  - Davranış: `LoadOrCreateAsync` dosya varsa okur; yoksa ya da okunamıyorsa yeni profil kurar ve başlangıç coin'ini `"starter"` kimlikli işlemle verir. Aynı `userId` ile ikinci çağrı hiçbir şeyi değiştirmez. Her değişiklik dosyaya yazılır ve `Changed` tetiklenir.
  - `ApplyAsync`: profil yüklü değilse `false`. `tx.Id` daha önce uygulandıysa bakiyeye dokunmadan `true`. Bakiye eksiye düşecekse `false`. Aksi halde uygular, `true`.
  - `SetDisplayNameAsync`: baştaki/sondaki boşluğu atar; 2 karakterden kısaysa değiştirmez. `AddXpAsync`: `xp <= 0` ise değiştirmez; `Level = GameConfig.LevelForXp(Xp)`. `DeleteAsync`: dosyayı siler, `Current = null`.
  - Uygulanan işlem kimlikleri dosyada tutulur; yalnızca son 200'ü saklanır (`// shortcut: yerel defter son 200 işlemi hatırlar; sunucu defteri gelince gerek kalmaz`).

- [ ] **Step 1: Başarısız testi yaz** (`LocalServicesTests.cs`)

```csharp
using System.IO;
using NUnit.Framework;

public class LocalServicesTests
{
    string path;

    [SetUp]
    public void SetUp() =>
        path = Path.Combine(Path.GetTempPath(), "okey-test-" + System.Guid.NewGuid().ToString("N") + ".json");

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    LocalProfileStore Loaded()
    {
        var s = new LocalProfileStore(path);
        s.LoadOrCreateAsync("u1", "Oyuncu0001").GetAwaiter().GetResult();
        return s;
    }

    static bool Apply(IWallet w, string id, long amount) =>
        w.ApplyAsync(new CoinTx(id, amount, CoinReason.Purchase)).GetAwaiter().GetResult();

    [Test]
    public void Auth_UserIdIsNullBeforeSignIn_ThenLocalPlusTag()
    {
        var auth = new LocalAuthService("51Okey_clone_0");
        Assert.IsNull(auth.UserId);
        auth.SignInAsync().GetAwaiter().GetResult();
        Assert.AreEqual("local-51Okey_clone_0", auth.UserId);
        Assert.IsTrue(auth.IsGuest);
    }

    [Test]
    public void Profile_NewProfileGetsStarterCoinsNameAndLevelOne()
    {
        var s = Loaded();
        Assert.AreEqual("u1", s.Current.UserId);
        Assert.AreEqual("Oyuncu0001", s.Current.DisplayName);
        Assert.AreEqual(GameConfig.StarterCoins, s.Coins);
        Assert.AreEqual(1, s.Current.Level);
        Assert.IsTrue(File.Exists(path));
    }

    [Test]
    public void Profile_SecondStoreOnSameFileKeepsState()
    {
        Assert.IsTrue(Apply(Loaded(), "t1", 50));
        Assert.AreEqual(GameConfig.StarterCoins + 50, Loaded().Coins);
    }

    [Test]
    public void Profile_LoadTwiceGrantsStarterOnce()
    {
        var s = Loaded();
        s.LoadOrCreateAsync("u1", "Oyuncu0001").GetAwaiter().GetResult();
        Assert.AreEqual(GameConfig.StarterCoins, s.Coins);
    }

    [Test]
    public void Profile_CorruptFile_StartsFresh()
    {
        File.WriteAllText(path, "{ \"profile\": { \"Coins\": 99");
        Assert.AreEqual(GameConfig.StarterCoins, Loaded().Coins); // LogError basılırsa Unity testi kendiliğinden düşürür
    }

    [Test]
    public void Wallet_AddsCoinsAndFiresChanged()
    {
        var s = Loaded();
        long seen = -1;
        s.Changed += p => seen = p.Coins;
        Assert.IsTrue(Apply(s, "t1", 200));
        Assert.AreEqual(GameConfig.StarterCoins + 200, s.Coins);
        Assert.AreEqual(s.Coins, seen);
    }

    [Test]
    public void Wallet_RejectsOverspendAndLeavesBalance()
    {
        var s = Loaded();
        Assert.IsFalse(Apply(s, "t1", -(GameConfig.StarterCoins + 1)));
        Assert.AreEqual(GameConfig.StarterCoins, s.Coins);
        Assert.AreEqual(GameConfig.StarterCoins, Loaded().Coins);
        Assert.IsTrue(Apply(s, "t2", -GameConfig.StarterCoins), "tam bakiye harcanabilir");
        Assert.AreEqual(0, s.Coins);
    }

    [Test]
    public void Wallet_SameTxIdAppliesOnce()
    {
        var s = Loaded();
        Assert.IsTrue(Apply(s, "satin-alma-1", 100));
        Assert.IsTrue(Apply(s, "satin-alma-1", 100));
        Assert.AreEqual(GameConfig.StarterCoins + 100, s.Coins);
        Assert.IsTrue(Apply(Loaded(), "satin-alma-1", 100), "yeniden açılışta da hatırlanır");
        Assert.AreEqual(GameConfig.StarterCoins + 100, Loaded().Coins);
    }

    [Test]
    public void Wallet_BeforeLoad_ReturnsFalse() =>
        Assert.IsFalse(Apply(new LocalProfileStore(path), "t1", 10));

    [Test]
    public void Profile_AddXp_RaisesLevel()
    {
        var s = Loaded();
        s.AddXpAsync(0).GetAwaiter().GetResult();
        s.AddXpAsync(-5).GetAwaiter().GetResult();
        Assert.AreEqual(0, s.Current.Xp);
        s.AddXpAsync(GameConfig.XpPerLevel).GetAwaiter().GetResult();
        Assert.AreEqual(GameConfig.XpPerLevel, s.Current.Xp);
        Assert.AreEqual(2, s.Current.Level);
        Assert.AreEqual(2, Loaded().Current.Level);
    }

    [Test]
    public void Profile_SetDisplayName_TrimsAndIgnoresTooShort()
    {
        var s = Loaded();
        s.SetDisplayNameAsync("  Osman ").GetAwaiter().GetResult();
        Assert.AreEqual("Osman", s.Current.DisplayName);
        s.SetDisplayNameAsync(" a ").GetAwaiter().GetResult();
        Assert.AreEqual("Osman", s.Current.DisplayName);
        Assert.AreEqual("Osman", Loaded().Current.DisplayName);
    }

    [Test]
    public void Profile_Delete_RemovesFileAndNextLoadIsFresh()
    {
        var s = Loaded();
        Apply(s, "t1", 500);
        s.DeleteAsync().GetAwaiter().GetResult();
        Assert.IsNull(s.Current);
        Assert.IsFalse(File.Exists(path));
        Assert.AreEqual(GameConfig.StarterCoins, Loaded().Coins);
    }
}
```

- [ ] **Step 2: Başarısız olduğunu gör**

Run: `powershell -ExecutionPolicy Bypass -File Tools\SyncTest\Run-EditModeTests.ps1 -Filter LocalServicesTests`
Expected: `COMPILE FAILED` (`LocalProfileStore` yok).

- [ ] **Step 3: İki sınıfı yaz.** Dosya biçimi: `JsonUtility` ile `{ profile, appliedTxIds }` taşıyan `[Serializable]` bir sarmalayıcı. Okuma hatası ve `UserId` uyuşmazlığı "dosya yok" gibi ele alınır, `Debug.LogWarning` ile.

- [ ] **Step 4: Geçtiğini gör**

Run: aynı komut. Expected: `passed 12/12`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Osman/Scripts/Services/Local.meta Assets/Osman/Scripts/Services/Local/LocalAuthService.cs* Assets/Osman/Scripts/Services/Local/LocalProfileStore.cs* Assets/Osman/Editor/Tests/LocalServicesTests.cs*
powershell -ExecutionPolicy Bypass -File Tools/Security/Check-Secrets.ps1
git commit -m "Local identity, profile store and wallet"
```

---

### Task 4: `GameServices` başlangıç noktası ve boş reklam servisi

**Files:**
- Create: `Assets/Osman/Scripts/Services/GameServices.cs`
- Create: `Assets/Osman/Scripts/Services/Ads/NullAdsService.cs`
- Test: `Assets/Osman/Editor/Tests/GameServicesTests.cs`

**Interfaces:**
- Consumes: Task 2 arayüzleri, Task 3 sınıfları.
- Produces:

```csharp
public static class GameServices
{
    public static IAuthService Auth { get; }
    public static IProfileStore Profile { get; }
    public static IWallet Wallet { get; }
    public static IAdsService Ads { get; }
    public static string InstanceTag { get; }                    // Editör: proje klasör adı; cihaz: "device"
    public static string LocalProfilePath(string instanceTag);   // persistentDataPath/local-profile-<etiket>.json
    public static System.Threading.Tasks.Task InitAsync();       // giriş + profil yükleme; her çağrı aynı Task'ı döner
}

public class NullAdsService : IAdsService   // RewardedReady = true; ShowRewardedAsync → true; TryShowInterstitial hiçbir şey yapmaz
```

  - Servis nesneleri ilk erişimde, beklemeden kurulur (abonelikler `InitAsync`'ten önce yapılabilsin). `Wallet` ile `Profile` aynı `LocalProfileStore` nesnesidir.
  - `InitAsync` bitince tek satır log: `[SERVICES] <UserId> <DisplayName> coins=<Coins>` (`Debug.Log`).
  - Bu fazda Editörde de cihazda da yerel uygulamalar seçilir; Firebase seçimi Faz 3'te eklenir.

- [ ] **Step 1: Başarısız testi yaz** (`GameServicesTests.cs`)

```csharp
using System.IO;
using NUnit.Framework;
using UnityEngine;

// Not: bu testler ana editörün gerçek yerel profilini kullanır (yoksa oluşturur); bakiyeyi değiştirmez.
public class GameServicesTests
{
    [Test]
    public void InstanceTag_IsProjectFolderName() =>
        Assert.AreEqual(new DirectoryInfo(Application.dataPath).Parent.Name, GameServices.InstanceTag);

    [Test]
    public void LocalProfilePath_DiffersPerTag()
    {
        string a = GameServices.LocalProfilePath("51Okey"), b = GameServices.LocalProfilePath("51Okey_clone_0");
        Assert.AreNotEqual(a, b);
        StringAssert.EndsWith("local-profile-51Okey_clone_0.json", b);
        StringAssert.StartsWith(Application.persistentDataPath, a);
    }

    [Test]
    public void Init_Twice_ReturnsSameTask() =>
        Assert.AreSame(GameServices.InitAsync(), GameServices.InitAsync());

    [Test]
    public void Init_SignsInAndLoadsProfile()
    {
        GameServices.InitAsync().GetAwaiter().GetResult();
        Assert.AreEqual("local-" + GameServices.InstanceTag, GameServices.Auth.UserId);
        Assert.IsNotNull(GameServices.Profile.Current);
        Assert.AreEqual(GameServices.Profile.Current.Coins, GameServices.Wallet.Coins);
        Assert.AreSame(GameServices.Profile, GameServices.Wallet);
    }

    [Test]
    public void NullAds_RewardedSucceedsAndInterstitialDoesNothing()
    {
        IAdsService ads = new NullAdsService();
        ads.InitializeAsync().GetAwaiter().GetResult();
        Assert.IsTrue(ads.RewardedReady);
        Assert.IsTrue(ads.ShowRewardedAsync("shop").GetAwaiter().GetResult());
        Assert.DoesNotThrow(() => ads.TryShowInterstitial("game_end"));
    }
}
```

- [ ] **Step 2: Başarısız olduğunu gör**

Run: `powershell -ExecutionPolicy Bypass -File Tools\SyncTest\Run-EditModeTests.ps1 -Filter GameServicesTests`
Expected: `COMPILE FAILED` (`GameServices` yok).

- [ ] **Step 3: İki dosyayı yukarıdaki imzalarla yaz.**

- [ ] **Step 4: Geçtiğini gör; önceki sınıflar da yeşil**

Run: aynı komut, sonra `-Filter LocalServicesTests` ve `-Filter GameConfigTests`.
Expected: `passed 5/5`, `passed 12/12`, `passed 8/8`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Osman/Scripts/Services/GameServices.cs* Assets/Osman/Scripts/Services/Ads.meta Assets/Osman/Scripts/Services/Ads/NullAdsService.cs* Assets/Osman/Editor/Tests/GameServicesTests.cs*
powershell -ExecutionPolicy Bypass -File Tools/Security/Check-Secrets.ps1
git commit -m "GameServices entry point and a no-op ads service"
```

---

### Task 5: Açılışa bağlama ve canlı doğrulama

**Files:**
- Modify: `Assets/Osman/Scripts/Photons/PhotonManager.cs` (`Start`)
- Modify: `Assets/Osman/Scripts/UI/MainMenuUIHandler.cs`
- Modify: `Assets/Osman/Scripts/Shop&Coins/ShopController.cs`

**Interfaces — Consumes:** `GameServices.InitAsync()`, `GameServices.Profile` (`Current`, `Changed`, `SetDisplayNameAsync`), `GameServices.Wallet.ApplyAsync`, `CoinTx`.

Bu görevde birim testi yok: üç dosya sahneye bağlı `MonoBehaviour`; mantık Task 2-4'te test edildi. Doğrulama canlı koşudur.

- [ ] **Step 1: `PhotonManager.Start`** — `Start` `async void` olur; mevcut satırların sırası aynen kalır, yalnızca `PhotonNetwork.ConnectUsingSettings();` satırından hemen önce `await GameServices.InitAsync();` eklenir. Başka koşul ya da `null` kontrolü eklenmez (ana menüye dönüşte kopya nesnenin yeniden bağlanma çağrısı bugünkü gibi çalışmalı).

- [ ] **Step 2: `MainMenuUIHandler`** — yeni `async void Start()`: `await GameServices.InitAsync();` sonra `playerNameInput` boşsa `GameServices.Profile.Current.DisplayName` yazılır. `ConnectLobby` içinde `PhotonNetwork.NickName = playerNameInput.text;` satırından sonra `_ = GameServices.Profile.SetDisplayNameAsync(playerNameInput.text);`.

- [ ] **Step 3: `ShopController`** — `Start`'ta `GameServices.Profile.Changed`'e abone olur ve mevcut profili gösterir (`Current` null ise yazıya dokunmaz); `OnDestroy`'da aboneliği bırakır. `AddCoins(int coinAmount)` ekrandaki yazıyı parse etmeyi bırakır: `GameServices.Wallet.ApplyAsync(new CoinTx(System.Guid.NewGuid().ToString("N"), coinAmount, CoinReason.Purchase))` çağırır; yazı `Changed` ile güncellenir. `CoinPurchaseButton`, `RemoveAdsButton`, panel aç/kapa aynı kalır.

- [ ] **Step 4: Derleme ve tüm yeni testler**

Run: `Run-EditModeTests.ps1` sırayla `-Filter GameConfigTests`, `LocalServicesTests`, `GameServicesTests`, `TestPlayerBrainTests`.
Expected: `passed 8/8`, `12/12`, `5/5`, `10/10`.

- [ ] **Step 5: Dört editörde ayrı kimlik** — klonları kısa bir `script eval` ile ısıt; dört editörde `editor.playmode start`, 15 sn sonra her birinde `console get` çıktısında `[SERVICES]` satırını oku, sonra `editor.playmode stop`.

Expected: dört satır, kimlikler `local-51Okey`, `local-51Okey_clone_0`, `local-51Okey_clone_1`, `local-51Okey_clone_2`; dört farklı `Oyuncu####` ismi; her birinde `coins=1000` (testler bakiyeyi değiştirmez). Hiçbir editörde yeni hata satırı yok (bilinen iki EDM hatası dışında).

- [ ] **Step 6: Senkron testi** (arka planda, yaklaşık 5 dk)

Run: `powershell -ExecutionPolicy Bypass -File Tools\SyncTest\Run-SyncTest.ps1 -Mode smart -Seed 11 -Deals 1`
Expected: `PASS`, `desync 0`. Fazdan önceki son koşu: `ok 52 desync 0`; `ok` sayısı aynı olmalı (aynı seed).

- [ ] **Step 7: Telefon** — `AndroidDevBuild.Build()` (ana editörde `script eval`), `adb install -r Builds/Android/51Okey-dev.apk`, uygulamayı aç, `adb logcat -d -s Unity | grep "\[SERVICES\]"`.

Expected: `[AndroidDevBuild] Succeeded`; logcat'te `[SERVICES] local-device Oyuncu#### coins=1000`; ana menüde isim kutusu `Oyuncu####` ile dolu. Telefon ağda değilse bu adım "doğrulanmadı" olarak raporlanır, atlanmış sayılmaz.

- [ ] **Step 8: Commit**

```bash
git add Assets/Osman/Scripts/Photons/PhotonManager.cs Assets/Osman/Scripts/UI/MainMenuUIHandler.cs "Assets/Osman/Scripts/Shop&Coins/ShopController.cs"
powershell -ExecutionPolicy Bypass -File Tools/Security/Check-Secrets.ps1
git commit -m "Start-up goes through GameServices; menu name and shop coins come from the profile"
```
