using UnityEngine;
using UnityEngine.UI;

public class RoomItem : MonoBehaviour
{
    public Text roomNameText;
    private string _roomOwnerName;
    public Text playerCountText;
    ButtonFuncs manager;

    void Start()
    {
        manager = FindObjectOfType<ButtonFuncs>();
    }

    public void SetRoomName(string _roomName, int _playerCount)
    {
        _roomOwnerName = _roomName;
        roomNameText.text = _roomName + "'s Room";
        playerCountText.text = _playerCount + " / 4";
    }

    public void OnClickItem()
    {
        manager.JoinRoom(_roomOwnerName);
    }
}