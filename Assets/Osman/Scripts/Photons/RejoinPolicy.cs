public enum RejoinStep { Retry, GiveUpKeep, GiveUpClear }

// Yeniden katılma kararları (saf). Kayıt yalnız oda ya da oyuncu sunucuda yoksa silinir; geçici hatalar (32746 dahil) pencere içinde yeniden denenir.
public static class RejoinPolicy
{
    public const float WindowSec = 30f, RetryDelaySec = 2f;

    public static RejoinStep OnFailure(short code, float elapsedSec)
    {
        if (code == 32758 || code == 32748 || code == -3) // -3: TTL 0 olan (biten) odaya dönüş
            return RejoinStep.GiveUpClear;
        return elapsedSec < WindowSec ? RejoinStep.Retry : RejoinStep.GiveUpKeep;
    }

    public static bool ShouldReconnect(bool deliberate, bool hasRecord, bool inGameScene) => !deliberate && hasRecord && inGameScene;
}
