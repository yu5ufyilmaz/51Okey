public interface IAuthService
{
    string UserId { get; }              // giriş bitmeden null
    bool IsGuest { get; }
    System.Threading.Tasks.Task SignInAsync();
    System.Threading.Tasks.Task<LinkResult> LinkGoogleAsync();
    System.Threading.Tasks.Task DeleteAccountAsync();   // yalnızca kimlik; profil IProfileStore.DeleteAsync ile silinir
}
