public interface IAuthService
{
    string UserId { get; }              // giriş bitmeden null
    bool IsGuest { get; }
    event System.Action UserChanged;    // UserId değişince (giriş, hesaba geçiş, çıkış, silme); ana iş parçacığında
    System.Threading.Tasks.Task<bool> RestoreAsync();   // kayıtlı oturum varsa girer ve true döner; yoksa false, hesap AÇMAZ
    System.Threading.Tasks.Task SignInGuestAsync();     // anonim hesap açar (oturum varsa onu kullanır)
    System.Threading.Tasks.Task<LinkResult> LinkGoogleAsync();   // kullanıcı yoksa doğrudan Google kimliğiyle girer, varsa bağlar
    System.Threading.Tasks.Task SignOutAsync();
    System.Threading.Tasks.Task DeleteAccountAsync();   // yalnızca kimlik; profil IProfileStore.DeleteAsync ile silinir
    bool NeedsRecentLoginToDelete { get; }              // silmeden önce yeniden giriş gerekir mi (misafirde hep false)
    System.Threading.Tasks.Task AbandonGuestAsync();    // misafir kimliği silinemezse cihazdan bırakılır
}
