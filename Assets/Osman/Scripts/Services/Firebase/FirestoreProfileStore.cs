using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;

// Tasarım 5.2: users/{uid} + users/{uid}/ledger/{txId}. Kurallar: firebase/firestore.rules (coins int >= 0, defter güncellenmez).
// İsim kaydı names/{isim} = { uid, createdAt }: ilk alan tutar, isim değişikliği tek transaction.
// Coin değişikliği tek transaction: bakiye oku, eksiye düşecekse reddet, coins güncelle, defter kaydı yaz.
// Cüzdan: istisna = sonuç bilinmiyor (aynı Id ile tekrar dene); false = uygulanmadı.
public class FirestoreProfileStore : IProfileStore, IWallet
{
    const string StarterTxId = "starter";
    // Firestore batch'i en çok 500 işlem alır: son batch'te isim ve kullanıcı belgesine 2 yer kalır.
    const int MaxLedgerInFinalBatch = 498;
    const int LedgerBatchSize = 450;

    readonly FirebaseBoot boot;

    public PlayerProfile Current { get; private set; }
    public long Coins => Current != null ? Current.Coins : 0;
    public event Action<PlayerProfile> Changed;

    public FirestoreProfileStore(FirebaseBoot boot) => this.boot = boot;

    async Task<DocumentReference> UserDoc(string userId) =>
        (await boot.FirestoreAsync()).Collection("users").Document(userId);

    // Belge kimliğinde '/' yol ayırıcıdır.
    static DocumentReference LedgerDoc(DocumentReference user, string txId) =>
        user.Collection("ledger").Document(txId.Replace('/', '_'));

    public async Task LoadOrCreateAsync(string userId, string defaultName)
    {
        if (Current != null && Current.UserId == userId)
            return;
        var db = await boot.FirestoreAsync();
        var userRef = db.Collection("users").Document(userId);
        // Yeni profil başlangıç coin'ini yerel depodaki gibi kendi defter kaydıyla ("starter") ve aynı anda alır.
        var fields = await db.RunTransactionAsync(async t =>
        {
            var snap = await t.GetSnapshotAsync(userRef);
            if (snap.Exists)
                return snap.ToDictionary();
            var created = new Dictionary<string, object>
            {
                { "displayName", defaultName },
                { "coins", GameConfig.StarterCoins },
                { "level", GameConfig.LevelForXp(0) },
                { "xp", 0 },
                { "noAds", false },
                { "schemaVersion", 1 },
                { "createdAt", FieldValue.ServerTimestamp },
                { "lastSeenAt", FieldValue.ServerTimestamp },
            };
            t.Set(userRef, created);
            t.Set(LedgerDoc(userRef, StarterTxId), LedgerFields(new CoinTx(StarterTxId, GameConfig.StarterCoins, CoinReason.StarterGrant)));
            return created;
        });
        Current = FromFields(userId, fields, defaultName);
        Notify();
    }

    // Tek transaction: yeni ismi al (başkasınınsa Taken), bizdeki eski kaydı bırak, displayName + nameClaimed yaz.
    // Ağ ya da izin hatası: Failed, bellekteki profil değişmez.
    public async Task<NameResult> SetDisplayNameAsync(string name)
    {
        name = name?.Trim();
        if (Current == null || !GameConfig.IsStorableName(name))
            return NameResult.Invalid;
        if (name == Current.DisplayName && Current.NameClaimed)
            return NameResult.Ok;
        var uid = Current.UserId;
        NameResult result;
        try
        {
            var db = await boot.FirestoreAsync();
            var userRef = db.Collection("users").Document(uid);
            var nameRef = db.Collection("names").Document(name);
            result = await db.RunTransactionAsync(async t =>
            {
                // Eski isim bellekten değil sunucudan: yanıtı kaybolan commit ya da ikinci cihaz isim sızdırmasın.
                // Kullanıcı belgesini okumak aynı kullanıcının eşzamanlı değişikliklerini çakıştırır (Firestore yeniden dener).
                var user = await t.GetSnapshotAsync(userRef);
                if (!user.Exists)
                    return NameResult.Failed;
                var fields = user.ToDictionary();
                var serverName = fields.TryGetValue("displayName", out var n) ? n as string : null;
                bool serverClaimed = fields.TryGetValue("nameClaimed", out var c) && c is bool b && b;
                if (serverClaimed && serverName == name)
                    return NameResult.Ok;
                // Kayıtlı olmayan eski isim kimsenin kaydı değildir (başkasının olabilir): yalnız kayıtlıysa bırakılır.
                var oldRef = serverClaimed && !string.IsNullOrEmpty(serverName) ? db.Collection("names").Document(serverName) : null;
                // Transaction'da önce bütün okumalar, sonra yazmalar.
                var taken = await t.GetSnapshotAsync(nameRef);
                if (taken.Exists && taken.GetValue<string>("uid") != uid)
                    return NameResult.Taken;
                var old = oldRef != null ? await t.GetSnapshotAsync(oldRef) : null;
                // Kayıt zaten bizimse yazılmaz: kural var olan kayda yazmaya izin vermez.
                if (!taken.Exists)
                    t.Set(nameRef, new Dictionary<string, object> { { "uid", uid }, { "createdAt", FieldValue.ServerTimestamp } });
                // Kural yalnız sahibine sildirir.
                if (old != null && old.Exists && old.GetValue<string>("uid") == uid)
                    t.Delete(oldRef);
                t.Update(userRef, new Dictionary<string, object>
                {
                    { "displayName", name },
                    { "nameClaimed", true },
                    { "lastSeenAt", FieldValue.ServerTimestamp },
                });
                return NameResult.Ok;
            });
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Profil] isim kaydedilemedi: " + e.GetType().Name + " " + uid);
            return NameResult.Failed;
        }
        if (result == NameResult.Failed)
            Debug.LogWarning("[Profil] isim kaydedilemedi: kullanıcı belgesi yok " + uid);
        if (result != NameResult.Ok)
            return result;
        // Sunucuda isim zaten bizimse de buraya gelinir: bellek eşitlenir.
        if (Current != null && Current.UserId == uid)
        {
            Current.DisplayName = name;
            Current.NameClaimed = true;
            Notify();
        }
        return NameResult.Ok;
    }

    public async Task AddXpAsync(int xp)
    {
        if (Current == null || xp <= 0)
            return;
        // shortcut: xp bellekteki değerden yazılır; aynı hesap iki cihazda aynı anda oynarsa biri kaybolur. Sunucu fazında transaction.
        int total = Current.Xp + xp, level = GameConfig.LevelForXp(total);
        await Update(p => { p.Xp = total; p.Level = level; }, new Dictionary<string, object> { { "xp", total }, { "level", level } });
    }

    // Yerel depo gibi: yazılamazsa uyarı, bellekteki profil değişmez, Changed tetiklenmez.
    async Task Update(Action<PlayerProfile> change, Dictionary<string, object> fields)
    {
        var uid = Current.UserId;
        fields["lastSeenAt"] = FieldValue.ServerTimestamp;
        try { await (await UserDoc(uid)).UpdateAsync(fields); }
        catch (Exception e)
        {
            Debug.LogWarning("[Profil] Firestore'a yazılamadı: " + e.GetType().Name + " " + e.Message);
            return;
        }
        if (Current == null || Current.UserId != uid)
            return;
        change(Current);
        Notify();
    }

    // Hesap silme akışı: okumalar sunucudan (çevrimdışıysa hiçbir yazımdan önce düşer), silmeler toplu yazımla.
    // Tekrar çağrılabilir: olmayan belgeyi silmek hata değildir.
    public async Task DeleteAsync()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        if (Current != null)
        {
            var uid = Current.UserId;
            var db = await boot.FirestoreAsync();
            var userRef = db.Collection("users").Document(uid);
            var ledger = new List<DocumentSnapshot>((await userRef.Collection("ledger").GetSnapshotAsync(Source.Server)).Documents);
            // İsim sunucudaki belgeden okunur (bellek eski olabilir); kayıt varsa ve bizimse silinir.
            DocumentReference ownedName = null;
            var user = await userRef.GetSnapshotAsync(Source.Server);
            if (user.Exists)
            {
                var fields = user.ToDictionary();
                var serverName = fields.TryGetValue("displayName", out var n) ? n as string : null;
                if (fields.TryGetValue("nameClaimed", out var c) && c is bool claimed && claimed && !string.IsNullOrEmpty(serverName))
                {
                    var nameRef = db.Collection("names").Document(serverName);
                    var name = await nameRef.GetSnapshotAsync(Source.Server);
                    if (name.Exists && name.GetValue<string>("uid") == uid)
                        ownedName = nameRef;
                }
            }
            // 498 kayıttan fazlasında ara batch'ler arasında düşerse geçmişin bir kısmı silinip coin/isim/seviye kalabilir; yeniden deneme tamamlar.
            int next = 0;
            for (; ledger.Count - next > MaxLedgerInFinalBatch; next += LedgerBatchSize)
            {
                var part = db.StartBatch();
                for (int i = next; i < next + LedgerBatchSize; i++)
                    part.Delete(ledger[i].Reference);
                ThrowIfPastCommitBudget(clock);
                await part.CommitAsync();
            }
            // Tek batch: isim silinip kullanıcı belgesi kalırsa misafir sahibi olmadığı ismi "kayıtlı" sanır ve hiç onarılmaz.
            var batch = db.StartBatch();
            for (; next < ledger.Count; next++)
                batch.Delete(ledger[next].Reference);
            if (ownedName != null)
                batch.Delete(ownedName);
            batch.Delete(userRef);
            ThrowIfPastCommitBudget(clock);
            await batch.CommitAsync();
        }
        // Kalan açık: commit 5 sn'den uzun sürerse çağıran Failed bildirmiş olur ama silme burada tamamlanır.
        Current = null;
        Notify();
    }

    // Çağıran AccountDeletion.StepTimeoutMs'te vazgeçer: bütçe dolduysa yeni commit başlamaz (sessiz silme olmasın).
    static void ThrowIfPastCommitBudget(System.Diagnostics.Stopwatch clock)
    {
        if (clock.ElapsedMilliseconds > AccountDeletion.CommitBudgetMs)
            throw new TimeoutException("silme süresi doldu");
    }

    // Çıkış: yalnız bellekteki profil düşer; belge yerinde kalır.
    public void Clear()
    {
        Current = null;
        Notify();
    }

    public async Task<bool> ApplyAsync(CoinTx tx)
    {
        if (Current == null || tx.Amount == 0 || string.IsNullOrEmpty(tx.Id))
            return false;
        var uid = Current.UserId;
        var db = await boot.FirestoreAsync();
        var userRef = db.Collection("users").Document(uid);
        var entryRef = LedgerDoc(userRef, tx.Id);
        // null = yetersiz bakiye. Defter kaydı varsa işlem uygulanmış sayılır, bakiye yalnızca okunur.
        long? coins = await db.RunTransactionAsync(async t =>
        {
            var entry = await t.GetSnapshotAsync(entryRef);
            var user = await t.GetSnapshotAsync(userRef);
            long have = user.GetValue<long>("coins");
            if (entry.Exists)
                return (long?)have;
            if (have + tx.Amount < 0)
                return null;
            t.Update(userRef, new Dictionary<string, object>
            {
                { "coins", have + tx.Amount },
                { "lastSeenAt", FieldValue.ServerTimestamp },
            });
            t.Set(entryRef, LedgerFields(tx));
            return have + tx.Amount;
        });
        if (coins == null)
            return false;
        if (Current != null && Current.UserId == uid && Current.Coins != coins.Value)
        {
            Current.Coins = coins.Value;
            Notify();
        }
        return true;
    }

    static Dictionary<string, object> LedgerFields(CoinTx tx) => new Dictionary<string, object>
    {
        { "amount", tx.Amount },
        { "reason", tx.Reason.ToString() },
        { "reference", tx.Reference ?? "" },
        { "createdAt", FieldValue.ServerTimestamp },
    };

    // Firestore tamsayıları long döner; eksik alan varsayılanla dolar.
    public static PlayerProfile FromFields(string userId, IDictionary<string, object> f, string defaultName) => new PlayerProfile
    {
        UserId = userId,
        DisplayName = f.TryGetValue("displayName", out var n) && n is string s && s.Length > 0 ? s : defaultName,
        Coins = Long(f, "coins", 0),
        Level = (int)Long(f, "level", 1),
        Xp = (int)Long(f, "xp", 0),
        NoAds = f.TryGetValue("noAds", out var a) && a is bool b && b,
        NameClaimed = f.TryGetValue("nameClaimed", out var c) && c is bool claimed && claimed,
        SchemaVersion = (int)Long(f, "schemaVersion", 1),
    };

    static long Long(IDictionary<string, object> f, string key, long fallback) =>
        f.TryGetValue(key, out var v) && v != null ?Convert.ToInt64(v) : fallback;

    void Notify() => ProfileEvents.Raise(Changed, Current);
}
