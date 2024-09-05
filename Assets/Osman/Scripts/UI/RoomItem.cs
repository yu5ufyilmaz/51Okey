using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RoomItem : MonoBehaviour
{
public Text roomNamme;
ButtonFuncs manager;

void Start()
{
    manager = FindObjectOfType<ButtonFuncs>();
}
public void SetRoomName(string _roomName)
{
    roomNamme.text = _roomName;
}

public void OnClickItem()
{
    manager.JoinRoom(roomNamme.text);
}
}
