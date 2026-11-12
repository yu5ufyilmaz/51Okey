using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public class LocalProfileStore : IProfileStore, IWallet
{
    [Serializable]
    class StoreFile
    {
        public PlayerProfile profile;
        public List<string> appliedTxIds = new List<string>();
    }

    const int MaxRememberedTx = 200;
    const int MinNameLength = 2;
    const string StarterTxId = "starter";
    const int WriteAttempts = 5;
    const int WriteRetryDelayMs = 20;

    readonly string filePath;
    List<string> appliedTxIds = new List<string>();

    public PlayerProfile Current { get; private set; }
    public long Coins => Current != null ? Current.Coins : 0;
    public event Action<PlayerProfile> Changed;

    public LocalProfileStore(string filePath) => this.filePath = filePath;

    public Task LoadOrCreateAsync(string userId, string defaultName)
    {
        if (Current != null && Current.UserId == userId)
            return Task.CompletedTask;

        var loaded = TryRead(userId);
        if (loaded != null)
        {
            Current = loaded.profile;
            appliedTxIds = loaded.appliedTxIds ?? new List<string>();
            Notify();
            return Task.CompletedTask;
        }

        Current = new PlayerProfile { UserId = userId, DisplayName = defaultName };
        appliedTxIds = new List<string>();
        Credit(new CoinTx(StarterTxId, GameConfig.StarterCoins, CoinReason.StarterGrant));
        if (Write())
            Notify();
        else
        {
            Current = null;
            appliedTxIds = new List<string>();
        }
        return Task.CompletedTask;
    }

    public Task SetDisplayNameAsync(string name)
    {
        name = name?.Trim();
        if (Current == null || name == null || name.Length < MinNameLength)
            return Task.CompletedTask;
        Mutate(() => Current.DisplayName = name);
        return Task.CompletedTask;
    }

    public Task AddXpAsync(int xp)
    {
        if (Current == null || xp <= 0)
            return Task.CompletedTask;
        Mutate(() =>
        {
            Current.Xp += xp;
            Current.Level = GameConfig.LevelForXp(Current.Xp);
        });
        return Task.CompletedTask;
    }

    public Task DeleteAsync()
    {
        try { if (File.Exists(filePath)) File.Delete(filePath); }
        catch (Exception e) { Debug.LogWarning("[Profil] dosya silinemedi: " + e.Message); }
        Current = null;
        appliedTxIds = new List<string>();
        return Task.CompletedTask;
    }

    public Task<bool> ApplyAsync(CoinTx tx)
    {
        if (Current == null || tx.Amount == 0 || string.IsNullOrEmpty(tx.Id))
            return Task.FromResult(false);
        if (appliedTxIds.Contains(tx.Id))
            return Task.FromResult(true);
        if (Current.Coins + tx.Amount < 0)
            return Task.FromResult(false);
        return Task.FromResult(Mutate(() => Credit(tx)));
    }

    void Credit(CoinTx tx)
    {
        Current.Coins += tx.Amount;
        appliedTxIds.Add(tx.Id);
        // shortcut: yerel defter son 200 işlemi hatırlar; sunucu defteri gelince gerek kalmaz
        if (appliedTxIds.Count > MaxRememberedTx)
            appliedTxIds.RemoveRange(0, appliedTxIds.Count - MaxRememberedTx);
    }

    // Hepsi ya da hiçbiri: dosya yazılamazsa bellekteki durum geri alınır, Changed tetiklenmez.
    bool Mutate(Action change)
    {
        var before = JsonUtility.ToJson(Current);
        var beforeIds = new List<string>(appliedTxIds);
        change();
        if (Write())
        {
            Notify();
            return true;
        }
        JsonUtility.FromJsonOverwrite(before, Current);
        appliedTxIds = beforeIds;
        return false;
    }

    // Bir abonenin hatası diğerlerini ve çağıranı bozmaz.
    void Notify()
    {
        if (Changed == null)
            return;
        foreach (var handler in Changed.GetInvocationList())
        {
            try { ((Action<PlayerProfile>)handler)(Current); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    StoreFile TryRead(string userId)
    {
        if (!File.Exists(filePath))
            return null;
        try
        {
            var file = JsonUtility.FromJson<StoreFile>(File.ReadAllText(filePath));
            if (file?.profile != null && file.profile.UserId == userId)
                return file;
            Debug.LogWarning("[Profil] dosya başka bir oyuncuya ait, yeni profil kuruluyor.");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Profil] dosya okunamadı, yeni profil kuruluyor: " + e.Message);
        }
        return null;
    }

    // Geçici dosyaya yazıp değiştirir: yazım yarıda kesilirse eski dosya sağlam kalır.
    bool Write()
    {
        var tmp = filePath + ".tmp";
        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(tmp, JsonUtility.ToJson(new StoreFile { profile = Current, appliedTxIds = appliedTxIds }));
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    if (File.Exists(filePath))
                        File.Replace(tmp, filePath, null);
                    else
                        File.Move(tmp, filePath);
                    return true;
                }
                catch (IOException) when (attempt < WriteAttempts)
                {
                    // Windows'ta virüs tarayıcı/dizinleyici dosyayı anlık kilitleyebilir.
                    System.Threading.Thread.Sleep(WriteRetryDelayMs);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Profil] dosya yazılamadı: " + e.Message);
            try { File.Delete(tmp); } catch (Exception) { }
            return false;
        }
    }
}
