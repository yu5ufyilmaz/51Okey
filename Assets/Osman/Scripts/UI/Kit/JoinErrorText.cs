using Photon.Realtime;

// Photon'un oda katılma hata kodlarını oyuncuya gösterilecek Türkçe metne çevirir.
public static class JoinErrorText
{
    public static string For(short returnCode)
    {
        switch (returnCode)
        {
            case ErrorCode.GameFull: return "Masa dolu";
            case ErrorCode.GameClosed: return "Masa kapanmış";
            case ErrorCode.GameDoesNotExist: return "Masa artık yok";
            case ErrorCode.GameIdAlreadyExists: return "Bu isimde bir masa var";
            default: return "Masaya girilemedi";
        }
    }
}
