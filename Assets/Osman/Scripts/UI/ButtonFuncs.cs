using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
public class ButtonFuncs : MonoBehaviourPunCallbacks
{

    public RoomItem roomItemPrefab;
    private List<RoomItem> roomItemsList = new List<RoomItem>();
    [SerializeField]
    private Transform _content;


    void Start()
    {
        PhotonNetwork.JoinLobby();
    }




    //Buraya oda ayarlarının değiştirilebilineceği ayar panelinin açılıp oradan RoomOptionsı değiştirebilinecek kodlar yazılabilir.
    public void CreateGame()
    {
        if (PhotonNetwork.InLobby)
        {
            EventDispatcher.InvokeEvent("CreateRoom");
        }
    }





    //Oda kurma ya da odaya girme kısmı buradan ama daha optimize yazılınabilinir.
    public void JoinRoom(string _roomName)
    {
        EventDispatcher.InvokeEvent("JoinRoom", _roomName);
    }




    //Buraası Oda Listesini yenileme kısmı Oda bulmayla alakalı sorunları Buradan çözücez.
    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        UpdateRoomList(roomList);
    }
    void UpdateRoomList(List<RoomInfo> list)
    {

        foreach (RoomItem item in roomItemsList)
        {
            Destroy(item.gameObject);
        }
        roomItemsList.Clear();

        foreach (RoomInfo room in list)
        {

            RoomItem newRoom = Instantiate(roomItemPrefab, _content);
            newRoom.SetRoomName(room.Name, room.PlayerCount);
            roomItemsList.Add(newRoom);
        }
    }





    public override void OnConnectedToMaster()
    {
        PhotonNetwork.JoinLobby();
    }
    public override void OnJoinedLobby()
    {

        Debug.Log("Joined Lobby");
    }

    public override void OnLeftRoom()
    {
        Debug.Log("Left Room");
    }
}
