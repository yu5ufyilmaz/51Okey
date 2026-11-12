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
        }
        else
        {
            Current = new PlayerProfile { UserId = userId, DisplayName = defaultName };
            appliedTxIds = new List<string>();
            Credit(new CoinTx(StarterTxId, GameConfig.StarterCoins, CoinReason.StarterGrant));
        }
        Commit();
        return Task.CompletedTask;
    }

    public Task SetDisplayNameAsync(string name)
    {
        name = name?.Trim();
        if (Current == null || name == null || name.Length < MinNameLength)
            return Task.CompletedTask;
        Current.DisplayName = name;
        Commit();
        return Task.CompletedTask;
    }

    public Task AddXpAsync(int xp)
    {
        if (Current == null || xp <= 0)
            return Task.CompletedTask;
        Current.Xp += xp;
        Current.Level = GameConfig.LevelForXp(Current.Xp);
        Commit();
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
        Credit(tx);
        Commit();
        return Task.FromResult(true);
    }

    void Credit(CoinTx tx)
    {
        Current.Coins += tx.Amount;
        appliedTxIds.Add(tx.Id);
        // shortcut: yerel defter son 200 işlemi hatırlar; sunucu defteri gelince gerek kalmaz
        if (appliedTxIds.Count > MaxRememberedTx)
            appliedTxIds.RemoveRange(0, appliedTxIds.Count - MaxRememberedTx);
    }

    void Commit()
    {
        Write();
        Changed?.Invoke(Current);
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
    void Write()
    {
        var tmp = filePath + ".tmp";
        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(tmp, JsonUtility.ToJson(new StoreFile { profile = Current, appliedTxIds = appliedTxIds }));
            if (File.Exists(filePath))
                File.Replace(tmp, filePath, null);
            else
                File.Move(tmp, filePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Profil] dosya yazılamadı: " + e.Message);
        }
    }
}
