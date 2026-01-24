using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomItem : MonoBehaviour
{
    public TMP_Text roomNameText; // UI'daki metin objesi
    private string cleanRoomName; // Photon'un bildiği gerçek isim
    private string roomPassword;

    public void SetRoomName(string _roomName, int _playerCount, string _password)
    {
        cleanRoomName = _roomName; // Gerçek ismi burada bozmadan sakla
        roomPassword = _password;

        // Ekranda nasıl görüneceğini ayarla (Emoji yerine güvenli karakter)
        if (!string.IsNullOrEmpty(_password))
        {
            roomNameText.text = _roomName + " [P]"; // Font hatası vermemesi için [P] kullandık
        }
        else
        {
            roomNameText.text = _roomName;
        }
    }

    public void OnClick()
    {
        // Ekranda yazan metni değil, 'cleanRoomName' değişkenini gönderiyoruz
        FindObjectOfType<ButtonFuncs>()
            .OnRoomItemClicked(cleanRoomName, roomPassword);
    }
}
