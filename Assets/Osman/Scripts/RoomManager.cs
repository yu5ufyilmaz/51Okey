using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class RoomManager : MonoBehaviourPunCallbacks
{

    [Tooltip("Oda boş kaldığında ne kadar süre sonra otomatik olarak kapanacağını ayarlayan milisaniye cinsinden bir değerdir.")]
    [SerializeField] private int emptyRoomTtl = 0;

    [Tooltip("Bir oyuncu odadan ayrıldığında, o oyuncunun oyunla ilgili bilgileri (RPC çağrıları gibi) temizlenir.")]
    [SerializeField] private bool cleanupCacheOnLeave = true;

    [SerializeField] private int maxPlayers = 4;
    [SerializeField] bool isOpen = true;
    [SerializeField] bool isVisible = true;

    void Start()
    {
        EventDispatcher.RegisterListener("CreateRoom", CreateRoom);
        EventDispatcher.RegisterListener<string>("JoinRoom", JoinRoom);
    }

    public void CreateRoom()
    {
        RoomOptions roomOptions = new RoomOptions();
        roomOptions.MaxPlayers = maxPlayers;
        roomOptions.CleanupCacheOnLeave = cleanupCacheOnLeave;
        roomOptions.EmptyRoomTtl = emptyRoomTtl;
        roomOptions.IsOpen = isOpen;
        roomOptions.IsVisible = isVisible;
        PhotonNetwork.CreateRoom(PhotonNetwork.NickName, roomOptions, TypedLobby.Default);
        SceneChangeManager.Instance.ChangeScene("Table");
    }

    public void JoinRoom(string _roomName)
    {
        List<RoomInfo> list = new List<RoomInfo>();
        if (_roomName != null)
        {
            if (list.Count > 0)
            {

                PhotonNetwork.JoinRandomRoom();
                SceneChangeManager.Instance.ChangeScene("Table");
            }
            else
            {
                EventDispatcher.InvokeEvent("CreateRoom");
            }
        }
        else
        {
            PhotonNetwork.JoinRoom(_roomName);
            SceneChangeManager.Instance.ChangeScene("Table");
        }
    }


}
