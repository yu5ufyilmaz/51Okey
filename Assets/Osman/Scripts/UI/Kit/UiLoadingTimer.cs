// Yükleme katmanı takılırsa 15 sn sonra zaman aşımı bildirir. Zamanı dışarıdan alır, saf mantık.
public class UiLoadingTimer
{
    public const float Timeout = 15f;

    float startedAt;
    bool timeoutOn = true;

    public bool Active { get; private set; }
    public bool TimeoutOn => timeoutOn;

    // timeout false: giriş gibi uzun süren işlerde sayaç susar, katman Hide'a kadar kalır.
    public void Show(float now, bool timeout = true)
    {
        startedAt = now;
        timeoutOn = timeout;
        Active = true;
    }

    public void Hide() => Active = false;

    // Zaman aşımına bu çağrıda uğradıysa true döner ve kendini kapatır.
    public bool Tick(float now)
    {
        if (!Active || !timeoutOn || now - startedAt < Timeout)
            return false;
        Active = false;
        return true;
    }
}
