public interface IWallet
{
    long Coins { get; }
    System.Threading.Tasks.Task<bool> ApplyAsync(CoinTx tx);
}
