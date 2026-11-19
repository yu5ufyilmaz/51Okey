using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;

// Tasarım 5.2: users/{uid} + users/{uid}/ledger/{txId}. Kurallar: firebase/firestore.rules (coins int >= 0, defter güncellenmez).
// Coin değişikliği tek transaction: bakiye oku, eksiye düşecekse reddet, coins güncelle, defter kaydı yaz.
// Cüzdan: istisna = sonuç bilinmiyor (aynı Id ile tekrar dene); false = uygulanmadı.
public class FirestoreProfileStore : IProfileStore, IWallet
{
    const string StarterTxId = "starter";

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

    public async Task SetDisplayNameAsync(string name)
    {
        name = name?.Trim();
        if (Current == null || !GameConfig.IsValidName(name) || name == Current.DisplayName)
            return;
        await Update(p => p.DisplayName = name, new Dictionary<string, object> { { "displayName", name } });
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

    // Hesap silme akışı: önce defter, sonra kullanıcı belgesi (kural defteri yalnızca silmeye açar).
    public async Task DeleteAsync()
    {
        if (Current != null)
        {
            var userRef = await UserDoc(Current.UserId);
            var ledger = await userRef.Collection("ledger").GetSnapshotAsync();
            foreach (var entry in ledger.Documents)
                await entry.Reference.DeleteAsync();
            await userRef.DeleteAsync();
        }
        Current = null;
        Notify();
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
        SchemaVersion = (int)Long(f, "schemaVersion", 1),
    };

    static long Long(IDictionary<string, object> f, string key, long fallback) =>
        f.TryGetValue(key, out var v) && v != null ?Convert.ToInt64(v) : fallback;

    void Notify() => ProfileEvents.Raise(Changed, Current);
}
