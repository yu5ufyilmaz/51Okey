using System;
using System.Threading.Tasks;
using UnityEngine;

// Hesap silme kararları: servislere dokunmaz, adımlar dışarıdan verilir (EditMode testi var). İstisna fırlatmaz.
// Sıra profil → kimlik: Firestore kuralları profili yalnız kendi kimliğiyle sildirir.
public static class AccountDeletion
{
    // Her silme adımının üst sınırı; Firestore son commit'i en geç bütçe dolunca başlatır (5 sn commit payı).
    public const int StepTimeoutMs = 20000;
    public const int CommitBudgetMs = StepTimeoutMs - 5000;
    // Firebase silmeyi son girişten 5 dk içinde kabul eder; 4 dk ile pay bırakılır.
    public const int RecentLoginSeconds = 240;

    // abandonIdentity: kimlik silinemezse cihazdan bırakma (misafir); yeniden girilebilen hesapta null.
    public static async Task<DeleteResult> RunAsync(
        bool hasSession,
        bool needsRecentLogin,
        Func<Task> deleteProfile,
        Func<Task> deleteIdentity,
        Func<Task> abandonIdentity,
        Action dropLocal)
    {
        if (!hasSession)
            return DeleteResult.Failed;
        // Silme yarıda kalıp veri kaybettirmesin: yeniden giriş gerekiyorsa hiçbir şeye dokunulmaz.
        if (needsRecentLogin)
            return DeleteResult.NeedsRecentLogin;
        // Profil silinemezse hiçbir şey kaybolmaz: hesap olduğu gibi kalır.
        try { await deleteProfile(); }
        catch (Exception e)
        {
            Debug.LogWarning("[SERVICES] profil silinemedi: " + e.GetType().Name);
            return DeleteResult.Failed;
        }
        try { await deleteIdentity(); }
        catch (Exception e)
        {
            Debug.LogWarning("[SERVICES] kimlik silinemedi: " + e.GetType().Name);
            if (abandonIdentity == null)
                return DeleteResult.DataDeletedAccountRemains;
            try { await abandonIdentity(); }
            catch (Exception x)
            {
                Debug.LogWarning("[SERVICES] misafir kimliği bırakılamadı: " + x.GetType().Name);
                return DeleteResult.DataDeletedAccountRemains;
            }
        }
        dropLocal();
        return DeleteResult.Ok;
    }

    // Süre dolunca sonuç bilinmez: Firestore'a verilmiş bir yazım sonradan sunucuya ulaşabilir (sunucudan okuma çevrimdışını yazımdan önce yakalar).
    public static async Task WithTimeoutAsync(Task task, int ms)
    {
        if (await Task.WhenAny(task, Task.Delay(ms)).ConfigureAwait(false) != task)
        {
            // Bırakılan adımın sonraki hatası gözlenmemiş istisna olarak kalmasın.
            _ = task.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
            throw new TimeoutException();
        }
        await task.ConfigureAwait(false);
    }

    // Monoton saat tıkları; kayıt yoksa ya da saat geri gittiyse yakın sayılmaz.
    public static bool RecentEnough(long? lastAuthTicks, long nowTicks, long ticksPerSecond) =>
        lastAuthTicks.HasValue && nowTicks >= lastAuthTicks.Value
        && nowTicks - lastAuthTicks.Value < RecentLoginSeconds * ticksPerSecond;
}
