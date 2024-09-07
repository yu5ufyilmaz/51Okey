using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RoomItem : MonoBehaviour
{
    public Text roomNamme;
    //Bu oyunun sahibinin adı odayı bulabilmek için bu stringe ihtiyacımız var.
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
        roomNamme.text = _roomName + "'s Room";
        playerCountText.text = _playerCount + " / 4";
    }

    public void OnClickItem()
    {
        manager.JoinRoom(_roomOwnerName);
    }



}
