using UnityEngine;
using UnityEngine.UI;

// Masa ayar penceresi (ses, müzik). Pencere nesnesinde durur; kapalıyken de düğmeden çağrılır.
public class TableSettings : MonoBehaviour
{
    public Toggle soundToggle, musicToggle;

    // Anahtarlar kayıtlı ayardan okunur; işleyiciler yeniden yazmasın diye bildirimsiz
    public void Open()
    {
        // el sonu tablosu açıkken ayarlar açılmaz
        if (ScoreboardView.Instance != null && ScoreboardView.Instance.IsShown) return;
        soundToggle.SetIsOnWithoutNotify(GameSettings.SoundOn);
        musicToggle.SetIsOnWithoutNotify(GameSettings.MusicOn);
        gameObject.SetActive(true);
    }

    public void Close() => gameObject.SetActive(false);

    public void OnSoundToggle(bool on) => GameSettings.SoundOn = on;

    public void OnMusicToggle(bool on) => GameSettings.MusicOn = on;
}
