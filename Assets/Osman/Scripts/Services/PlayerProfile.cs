[System.Serializable]
public class PlayerProfile
{
    public string UserId, DisplayName;
    public long Coins;
    public int Level = 1, Xp;
    public bool NoAds;
    public int SchemaVersion = 1;
}

public enum CoinReason { StarterGrant, RewardedAd, Purchase, TableFee, TablePayout }
public enum LinkResult { Linked, SwitchedToExistingAccount, Cancelled, Failed }

public readonly struct CoinTx
{
    public readonly string Id; public readonly long Amount; public readonly CoinReason Reason; public readonly string Reference;

    public CoinTx(string id, long amount, CoinReason reason, string reference = "")
    {
        Id = id; Amount = amount; Reason = reason; Reference = reference;
    }
}
