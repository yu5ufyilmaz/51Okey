using UnityEngine;

// Cihazdaki "çalışan oyunum" kaydı. Anahtar kuruluma özel: ParrelSync klonları PlayerPrefs'i paylaşır.
public static class RejoinRecord
{
    public static string KeyFor(string instanceTag) => "Okey.Rejoin." + instanceTag;

    public static void Save(string roomName)
    {
        PlayerPrefs.SetString(KeyFor(GameServices.InstanceTag), roomName);
        PlayerPrefs.Save();
        Debug.Log("[DONUS] kayıt: " + roomName);
    }

    public static string Load()
    {
        string room = PlayerPrefs.GetString(KeyFor(GameServices.InstanceTag), "");
        return string.IsNullOrEmpty(room) ? null : room;
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(KeyFor(GameServices.InstanceTag));
        PlayerPrefs.Save();
        Debug.Log("[DONUS] kayıt silindi");
    }
}
