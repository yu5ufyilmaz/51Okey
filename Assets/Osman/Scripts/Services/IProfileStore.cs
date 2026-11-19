public interface IProfileStore
{
    PlayerProfile Current { get; }      // yüklenmeden null
    event System.Action<PlayerProfile> Changed;
    System.Threading.Tasks.Task LoadOrCreateAsync(string userId, string defaultName);
    // Aynı isim zaten kayıtlıysa yazmadan Ok. Biçim denetimi yalnız uzunluk ve '/': oyuncu girdisi GameServices.ChooseNameAsync'ten gelir.
    System.Threading.Tasks.Task<NameResult> SetDisplayNameAsync(string name);
    System.Threading.Tasks.Task AddXpAsync(int xp);
    System.Threading.Tasks.Task DeleteAsync();
    void Clear();                       // çıkışta: Current = null, Changed(null); kayıtlı profil silinmez
}
