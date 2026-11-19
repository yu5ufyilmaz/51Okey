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
        AccountDeletion.RunAsync(HasSession(auth, store), store.DeleteAsync, auth.DeleteAccountAsync,
            auth.IsRecentLoginError, store.Clear).GetAwaiter().GetResult();

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

    // Yerel depoda isim benzersizliği yok: bu test silinen profilin ismi bırakmasını yerel arka uçta doğrular.
    [Test]
    public void DeleteAccount_Ok_NameCanBeTakenAgain()
    {
        var (auth, store) = SignedIn("51Okey");
        Assert.AreEqual(NameResult.Ok, store.SetDisplayNameAsync("Ahmet").GetAwaiter().GetResult());
        Assert.AreEqual(DeleteResult.Ok, Delete(auth, store));
        var (_, again) = SignedIn("51Okey_clone_0");
        Assert.AreEqual(NameResult.Ok, again.SetDisplayNameAsync("Ahmet").GetAwaiter().GetResult());
        Assert.AreEqual("Ahmet", again.Current.DisplayName);
    }

    [Test]
    public void DeleteAccount_NoSession_IsFailed()
    {
        int calls = 0;
        var result = AccountDeletion.RunAsync(false,
            () => { calls++; return Task.CompletedTask; },
            () => { calls++; return Task.CompletedTask; },
            e => false,
            () => calls++).GetAwaiter().GetResult();
        Assert.AreEqual(DeleteResult.Failed, result);
        Assert.AreEqual(0, calls, "oturum yoksa hiçbir adım çağrılmaz");
    }

    [Test]
    public void DeleteAccount_AuthFails_ReturnsFailed_DoesNotThrow()
    {
        int profiles = 0, drops = 0;
        var result = AccountDeletion.RunAsync(true,
            () => { profiles++; return Task.CompletedTask; },
            () => throw new InvalidOperationException("ağ yok"),
            e => false,
            () => drops++).GetAwaiter().GetResult();
        Assert.AreEqual(DeleteResult.Failed, result);
        Assert.AreEqual(1, profiles, "kimlikten önce profil silinir");
        Assert.AreEqual(0, drops, "kimlik duruyorsa yerel durum düşmez");
    }

    [Test]
    public void DeleteAccount_ProfileFails_KeepsIdentity()
    {
        int identities = 0, drops = 0;
        var result = AccountDeletion.RunAsync(true,
            () => throw new InvalidOperationException("yazılamadı"),
            () => { identities++; return Task.CompletedTask; },
            e => false,
            () => drops++).GetAwaiter().GetResult();
        Assert.AreEqual(DeleteResult.Failed, result);
        Assert.AreEqual(0, identities, "profil silinemezse kimlik silinmez");
        Assert.AreEqual(0, drops);
    }

    [Test]
    public void DeleteAccount_AuthNeedsRecentLogin_ReturnsNeedsRecentLogin()
    {
        int drops = 0;
        var recent = new InvalidOperationException("yakın zamanda giriş gerekli");
        var result = AccountDeletion.RunAsync(true,
            () => Task.CompletedTask,
            () => Task.FromException(new AggregateException(recent)),
            e => e == recent || e.InnerException == recent,
            () => drops++).GetAwaiter().GetResult();
        Assert.AreEqual(DeleteResult.NeedsRecentLogin, result);
        Assert.AreEqual(0, drops);
    }
}
