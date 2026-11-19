// Ceza nedeni ağda numara olarak gider; metne her istemci kendi dilinde çevirir.
// Değerler ağ üzerinden gider: yalnızca sona ekle, sırayı değiştirme, numara yeniden kullanma.
public enum PenaltyReason : byte
{
    TookSideCouldNotOpen = 0,
    SideTileTaken = 1,
    DiscardedOkey = 2,
    DiscardedIndicator = 3,
    DiscardedIslek = 4,
    ProcessedOnMeld = 5,
}

public static class PenaltyReasonText
{
    // Bilinmeyen değer boş metin verir.
    public static string Of(PenaltyReason r)
    {
        switch (r)
        {
            case PenaltyReason.TookSideCouldNotOpen: return Loc.T("penalty.took_side_could_not_open");
            case PenaltyReason.SideTileTaken: return Loc.T("penalty.side_tile_taken");
            case PenaltyReason.DiscardedOkey: return Loc.T("penalty.discarded_okey");
            case PenaltyReason.DiscardedIndicator: return Loc.T("penalty.discarded_indicator");
            case PenaltyReason.DiscardedIslek: return Loc.T("penalty.discarded_islek");
            case PenaltyReason.ProcessedOnMeld: return Loc.T("penalty.processed_on_meld");
            default: return "";
        }
    }
}
