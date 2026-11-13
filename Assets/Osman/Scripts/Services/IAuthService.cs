public interface IAuthService
{
    string UserId { get; }              // giriş bitmeden null
    bool IsGuest { get; }
    event System.Action UserChanged;    // UserId değişince (giriş, hesaba geçiş, silme); ana iş parçacığında
    System.Threading.Tasks.Task SignInAsync();
    System.Threading.Tasks.Task<LinkResult> LinkGoogleAsync();
    System.Threading.Tasks.Task DeleteAccountAsync();   // yalnızca kimlik; profil IProfileStore.DeleteAsync ile silinir
}
