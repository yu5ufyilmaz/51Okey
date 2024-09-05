using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RoomItem : MonoBehaviour
{
public Text roomNamme;
public void SetRoomName(string _roomName)
{
    roomNamme.text = _roomName;
}
}
