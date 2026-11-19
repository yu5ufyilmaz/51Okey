using Photon.Realtime;

// Photon'un oda katılma hata kodlarını oyuncuya gösterilecek metin anahtarına çevirir; gösteren yer Loc.T ile çevirir.
public static class JoinErrorText
{
    public static string For(short returnCode)
    {
        switch (returnCode)
        {
            case ErrorCode.GameFull: return "error.room_full";
            case ErrorCode.GameClosed: return "error.room_closed";
            case ErrorCode.GameDoesNotExist: return "error.room_gone";
            case ErrorCode.GameIdAlreadyExists: return "error.room_exists";
            default: return "error.room_join_failed";
        }
    }
}
