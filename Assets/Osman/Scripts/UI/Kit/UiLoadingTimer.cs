// Yükleme katmanı takılırsa 15 sn sonra zaman aşımı bildirir. Zamanı dışarıdan alır, saf mantık.
public class UiLoadingTimer
{
    public const float Timeout = 15f;

    float startedAt;

    public bool Active { get; private set; }

    public void Show(float now)
    {
        startedAt = now;
        Active = true;
    }

    public void Hide() => Active = false;

    // Zaman aşımına bu çağrıda uğradıysa true döner ve kendini kapatır.
    public bool Tick(float now)
    {
        if (!Active || now - startedAt < Timeout)
            return false;
        Active = false;
        return true;
    }
}
