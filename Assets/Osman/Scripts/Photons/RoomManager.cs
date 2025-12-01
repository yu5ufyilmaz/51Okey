using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class RoomManager : MonoBehaviourPunCallbacks
{
    [SerializeField]
    private int emptyRoomTtl = 0;

    [SerializeField]
    private bool cleanupCacheOnLeave = true;

    [SerializeField]
    private bool isOpen = true;

    [SerializeField]
    private bool isVisible = true;
    private int maxPlayers = 4;

    [SerializeField]
    private GameObject tileManagerPrefab;

    void Start()
    {
        EventDispatcher.RegisterFunction("CreateRoom", CreateRoom);
        EventDispatcher.RegisterFunction<string>("JoinRoom", JoinRoom);
        EventDispatcher.RegisterFunction<int>("JoinRandomRoomOrCreate", JoinRandomRoomOrCreate);
        PhotonNetwork.AutomaticallySyncScene = true;
    }

    // Oda oluşturma işlemi
    public void CreateRoom()
    {
        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = (byte)maxPlayers,
            CleanupCacheOnLeave = cleanupCacheOnLeave,
            EmptyRoomTtl = emptyRoomTtl,
            IsOpen = isOpen,
            IsVisible = isVisible,
        };
        PhotonNetwork.CreateRoom(PhotonNetwork.NickName, roomOptions, TypedLobby.Default);

        SceneChangeManager.Instance.ChangeScene("Table");
    }

    // Odaya katılma işlemi
    public void JoinRoom(string _roomName)
    {
        PhotonNetwork.JoinRoom(_roomName);
        SceneChangeManager.Instance.ChangeScene("Table");
    }

    // Rastgele oda bulma veya oluşturma işlemi
    public void JoinRandomRoomOrCreate(int roomCount)
    {
        if (roomCount > 0)
        {
            PhotonNetwork.JoinRandomRoom();
            SceneChangeManager.Instance.ChangeScene("Table");
        }
        else
        {
            CreateRoom();
        }
    }

    // RoomManager.cs içine ekle:

    public override void OnLeftRoom()
    {
        Debug.Log("Odadan çıkıldı, Lobiye dönülüyor...");

        // Odadan çıkma işlemi tamamlanınca Ana Menü sahnesini yükle.
        // BURAYA DİKKAT: "Lobby" yerine senin ana menü sahnennin adı neyse onu yaz!
        SceneChangeManager.Instance.ChangeScene("Lobby");
    }
}
