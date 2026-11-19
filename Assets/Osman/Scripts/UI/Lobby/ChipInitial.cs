using TMPro;
using UnityEngine;

// Profil kutucuğundaki dairenin harfi: kutucuk yazısını (LobbyAccountUI yazar) izler.
public class ChipInitial : MonoBehaviour
{
    [SerializeField] TMP_Text source;
    [SerializeField] TMP_Text target;

    string last;

    void LateUpdate()
    {
        if (source.text == last)
            return;
        last = source.text;
        target.text = RoomCard.Initial(last);
    }
}
