using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Lobideki masa kartı: ad, etiketler ve dört koltuk (dizi sırası koltuk 1..4). Boş koltukta OTUR, dolu koltukta baş harf + ad.
public class RoomCard : MonoBehaviour
{
    static readonly CultureInfo Tr = new CultureInfo("tr-TR");

    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text tagsText;
    [SerializeField] GameObject[] taken;
    [SerializeField] TMP_Text[] initials;
    [SerializeField] TMP_Text[] names;
    [SerializeField] Button[] sit;

    LobbyRoom room;
    Action<LobbyRoom, int> onSit;

    void Awake()
    {
        for (int i = 0; i < sit.Length; i++)
        {
            int seat = i + 1;
            sit[i].onClick.AddListener(() => onSit?.Invoke(room, seat));
        }
    }

    public void Show(LobbyRoom room, Action<LobbyRoom, int> onSit)
    {
        this.room = room;
        this.onSit = onSit;
        nameText.text = room.Name;
        tagsText.text = room.Rounds + " el" + (room.HasPassword ? "   ·   Şifreli" : "");
        for (int i = 0; i < 4; i++)
        {
            bool free = room.SeatFree(i + 1);
            taken[i].SetActive(!free);
            sit[i].gameObject.SetActive(free);
            if (!free)
            {
                names[i].text = room.Seats[i];
                initials[i].text = Initial(room.Seats[i]);
            }
        }
    }

    // Adın ilk harfi, Türkçe büyük harf ("i" -> "İ"); boş adda boş.
    public static string Initial(string name)
    {
        string n = name == null ? "" : name.Trim();
        return n.Length == 0 ? "" : n.Substring(0, 1).ToUpper(Tr);
    }
}
