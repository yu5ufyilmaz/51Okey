using System;
using System.Threading.Tasks;
using UnityEngine;

// Hesap silme kararları: servislere dokunmaz, adımlar dışarıdan verilir (EditMode testi var). İstisna fırlatmaz.
// Sıra profil → kimlik: Firestore kuralları profili yalnız kendi kimliğiyle sildirir.
public static class AccountDeletion
{
    public static async Task<DeleteResult> RunAsync(
        bool hasSession,
        Func<Task> deleteProfile,
        Func<Task> deleteIdentity,
        Func<Exception, bool> isRecentLoginError,
        Action dropLocal)
    {
        if (!hasSession)
            return DeleteResult.Failed;
        // Profil silinemezse hiçbir şey kaybolmaz: hesap olduğu gibi kalır.
        try { await deleteProfile(); }
        catch (Exception e)
        {
            Debug.LogWarning("[SERVICES] profil silinemedi: " + e.GetType().Name + " " + e.Message);
            return DeleteResult.Failed;
        }
        try { await deleteIdentity(); }
        catch (Exception e)
        {
            Debug.LogWarning("[SERVICES] kimlik silinemedi: " + e.GetType().Name + " " + e.Message);
            return isRecentLoginError(e) ? DeleteResult.NeedsRecentLogin : DeleteResult.Failed;
        }
        dropLocal();
        return DeleteResult.Ok;
    }
}
