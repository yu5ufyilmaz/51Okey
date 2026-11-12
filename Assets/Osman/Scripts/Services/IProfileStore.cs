public interface IProfileStore
{
    PlayerProfile Current { get; }      // yüklenmeden null
    event System.Action<PlayerProfile> Changed;
    System.Threading.Tasks.Task LoadOrCreateAsync(string userId, string defaultName);
    System.Threading.Tasks.Task SetDisplayNameAsync(string name);
    System.Threading.Tasks.Task AddXpAsync(int xp);
    System.Threading.Tasks.Task DeleteAsync();
}
