using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

// Hesap silme kararları: yerel servisler geçici dosyada (gerçek editör profili kullanılmaz), hata durumları sahte adımlarla.
public class AccountDeletionTests
{
    string path;

    [SetUp]
    public void SetUp() =>
        path = Path.Combine(Path.GetTempPath(), "okey-test-" + Guid.NewGuid().ToString("N") + ".json");

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    static bool HasSession(IAuthService auth, IProfileStore store) =>
        auth.UserId != null && store.Current?.UserId == auth.UserId;

    static DeleteResult Delete(IAuthService auth, LocalProfileStore store) =>
        AccountDeletion.RunAsync(HasSession(auth, store), auth.NeedsRecentLoginToDelete, store.DeleteAsync,
            auth.DeleteAccountAsync, null, store.Clear).GetAwaiter().GetResult();

    int profiles, identities, abandons, drops;

    DeleteResult Run(bool hasSession = true, bool needsRecentLogin = false, bool profileFails = false,
        bool identityFails = false, bool guest = false, bool abandonFails = false)
    {
        profiles = identities = abandons = drops = 0;
        Func<Task> abandon = null;
        if (guest)
            abandon = () => { abandons++; return abandonFails ? Task.FromException(new InvalidOperationException("çıkılamadı")) : Task.CompletedTask; };
        return AccountDeletion.RunAsync(hasSession, needsRecentLogin,
            () => { profiles++; return profileFails ? Task.FromException(new InvalidOperationException("yazılamadı")) : Task.CompletedTask; },
            () => { identities++; if (identityFails) throw new InvalidOperationException("ağ yok"); return Task.CompletedTask; },
            abandon,
            () => drops++).GetAwaiter().GetResult();
    }

    (LocalAuthService, LocalProfileStore) SignedIn(string tag)
    {
        var auth = new LocalAuthService(tag);
        auth.SignInGuestAsync().GetAwaiter().GetResult();
        var store = new LocalProfileStore(path);
        store.LoadOrCreateAsync(auth.UserId, "Misafir-00001").GetAwaiter().GetResult();
        return (auth, store);
    }

    [Test]
    public void DeleteAccount_Ok_ClearsSessionAndProfile()
    {
        var (auth, store) = SignedIn("51Okey");
        Assert.IsTrue(File.Exists(path));
        Assert.AreEqual(DeleteResult.Ok, Delete(auth, store));
        Assert.IsNull(auth.UserId);
        Assert.IsNull(store.Current);
        Assert.IsFalse(File.Exists(path));
    }

    [Test]
    public void DeleteAccount_NoSession_IsFailed()
    {
        Assert.AreEqual(DeleteResult.Failed, Run(hasSession: false, guest: true));
        Assert.AreEqual(0, profiles + identities + abandons + drops, "oturum yoksa hiçbir adım çağrılmaz");
    }

    [Test]
    public void DeleteAccount_NeedsRecentLogin_DeletesNothing()
    {
        Assert.AreEqual(DeleteResult.NeedsRecentLogin, Run(needsRecentLogin: true, guest: true));
        Assert.AreEqual(0, profiles + identities + abandons + drops, "yakın giriş yoksa hiçbir şey silinmez");
    }

    [Test]
    public void DeleteAccount_ProfileFails_KeepsIdentity()
    {
        Assert.AreEqual(DeleteResult.Failed, Run(profileFails: true));
        Assert.AreEqual(0, identities, "profil silinemezse kimlik silinmez");
        Assert.AreEqual(0, drops);
    }

    [Test]
    public void DeleteAccount_ProfileFails_Guest_DoesNotAbandon()
    {
        Assert.AreEqual(DeleteResult.Failed, Run(profileFails: true, guest: true));
        Assert.AreEqual(0, identities + abandons + drops);
    }

    // Dosya silinemezse (kilitli) hesap silinmiş sayılmaz: hiçbir şey kaybolmaz.
    [Test]
    public void DeleteAccount_LocalFileLocked_IsFailedAndKeepsSession()
    {
        var (auth, store) = SignedIn("51Okey");
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.AreEqual(DeleteResult.Failed, Delete(auth, store));
        Assert.IsNotNull(auth.UserId);
        Assert.IsNotNull(store.Current);
        Assert.IsTrue(File.Exists(path));
    }

    [Test]
    public void WithTimeout_NeverCompletes_ThrowsTimeout() =>
        Assert.Throws<TimeoutException>(() =>
            AccountDeletion.WithTimeoutAsync(new TaskCompletionSource<bool>().Task, 50).GetAwaiter().GetResult());

    [Test]
    public void DeleteAccount_IdentityFails_NonGuest_ReportsDataDeleted()
    {
        Assert.AreEqual(DeleteResult.DataDeletedAccountRemains, Run(identityFails: true));
        Assert.AreEqual(1, profiles, "kimlikten önce profil silinir");
        Assert.AreEqual(1, identities);
        Assert.AreEqual(0, drops, "kimlik duruyorsa yerel durum düşmez");
    }

    [Test]
    public void DeleteAccount_IdentityFails_Guest_AbandonsAndIsOk()
    {
        Assert.AreEqual(DeleteResult.Ok, Run(identityFails: true, guest: true));
        Assert.AreEqual(1, abandons);
        Assert.AreEqual(1, drops);
    }

    [Test]
    public void DeleteAccount_IdentityAndAbandonFail_Guest_ReportsDataDeleted()
    {
        Assert.AreEqual(DeleteResult.DataDeletedAccountRemains, Run(identityFails: true, guest: true, abandonFails: true));
        Assert.AreEqual(1, abandons);
        Assert.AreEqual(0, drops);
    }

    [Test]
    public void DeleteAccount_Guest_IdentityOk_DoesNotAbandon()
    {
        Assert.AreEqual(DeleteResult.Ok, Run(guest: true));
        Assert.AreEqual(0, abandons);
        Assert.AreEqual(1, drops);
    }
}
