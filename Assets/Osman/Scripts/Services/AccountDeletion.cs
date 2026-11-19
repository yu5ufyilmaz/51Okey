using System;
using System.Threading.Tasks;
using UnityEngine;

// Hesap silme kararları: servislere dokunmaz, adımlar dışarıdan verilir (EditMode testi var). İstisna fırlatmaz.
// Sıra profil → kimlik: Firestore kuralları profili yalnız kendi kimliğiyle sildirir.
public static class AccountDeletion
{
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
            Debug.LogWarning("[SERVICES] profil silinemedi: " + e.GetType().Name + " " + e.Message);
            return DeleteResult.Failed;
        }
        try { await deleteIdentity(); }
        catch (Exception e)
        {
            Debug.LogWarning("[SERVICES] kimlik silinemedi: " + e.GetType().Name + " " + e.Message);
            if (abandonIdentity == null)
                return DeleteResult.DataDeletedAccountRemains;
            try { await abandonIdentity(); }
            catch (Exception x)
            {
                Debug.LogWarning("[SERVICES] misafir kimliği bırakılamadı: " + x.GetType().Name + " " + x.Message);
                return DeleteResult.DataDeletedAccountRemains;
            }
        }
        dropLocal();
        return DeleteResult.Ok;
    }
}
