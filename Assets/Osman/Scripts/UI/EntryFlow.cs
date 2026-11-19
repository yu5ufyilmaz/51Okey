// Giriş ekranında hangi panelin görüneceği. Karar saf kodda, sahneden bağımsız test edilir (GoogleLinkFlow ile aynı desen).
public static class EntryFlow
{
    public enum Screen { Loading, Retry, Choose, AskName, Lobby }

    public static Screen Next(bool initDone, bool initFailed, bool hasSession, bool needsName)
    {
        if (!initDone)
            return Screen.Loading;
        if (initFailed)
            return Screen.Retry;
        if (!hasSession)
            return Screen.Choose;
        return needsName ? Screen.AskName : Screen.Lobby;
    }
}
