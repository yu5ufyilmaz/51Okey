using UnityEngine;

// Oyuncu ayarları (PlayerPrefs, varsayılan açık). Ses sistemi yok: Ses AudioListener.volume'u ayarlar, Müzik yalnız kaydedilir.
public static class GameSettings
{
    const string SoundKey = "Okey.Sound";
    const string MusicKey = "Okey.Music";
    const string LanguageKey = "Okey.Language";

    public static bool SoundOn
    {
        get => PlayerPrefs.GetInt(SoundKey, 1) == 1;
        set
        {
            Save(SoundKey, value);
            Apply();
        }
    }

    public static bool MusicOn
    {
        get => PlayerPrefs.GetInt(MusicKey, 1) == 1;
        set => Save(MusicKey, value);
    }

    public static string Language
    {
        get => PlayerPrefs.GetString(LanguageKey, "");
        set
        {
            PlayerPrefs.SetString(LanguageKey, value ?? "");
            PlayerPrefs.Save();
        }
    }

    // Açılışta kendiliğinden koşar; ayar sahne yüklenince geçerli olsun.
    [RuntimeInitializeOnLoadMethod]
    public static void Apply() => AudioListener.volume = SoundOn ? 1f : 0f;

    static void Save(string key, bool on)
    {
        PlayerPrefs.SetInt(key, on ? 1 : 0);
        PlayerPrefs.Save();
    }
}
