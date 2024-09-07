using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
public class ButtonFuncs : MonoBehaviourPunCallbacks
{
    [Tooltip("The prefab for instantiating room items")]
    public RoomItem roomItemPrefab;

    [SerializeField]
    private Transform _content;

    List<RoomInfo> list = new List<RoomInfo>();
    private List<RoomItem> roomItemsList = new List<RoomItem>();
    //Odaları yenileme süresi
    private float timeBetweenUpdates = 1.5f;
    private float nextUpdateTime = 0.0f;
    //Oda sayısı
    private int roomCount;


    ///
    /// <summary>
    /// ////////////////////////////////////////////////////////////////////////////////////////
    /// </summary>
    /// 


    //Lobi Manager Scripti 
    void Start()
    {
        PhotonNetwork.JoinLobby();
    }


    public void CreateGame()
    {
        if (PhotonNetwork.InLobby)
            EventDispatcher.SummonEvent("CreateRoom");
    }



    public void JoinRoom(string _roomName)
    {
        if (PhotonNetwork.InLobby)
            EventDispatcher.SummonEvent("JoinRoom", _roomName);
    }
    public void JoinRandomRoom()
    {
        if (PhotonNetwork.InLobby)
            EventDispatcher.SummonEvent("JoinRandomRoomOrCreate", roomCount);
    }

    public void RefreshList()
    {
        if (PhotonNetwork.InLobby)
            UpdateRoomList(list);
    }


    //Burası Oda Listesini yenileme kısmı Oda bulmayla alakalı sorunları Buradan çözüceğiz.
    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        if (PhotonNetwork.InLobby)
        {
            if (Time.time >= nextUpdateTime)
            {
                roomCount = roomList.Count;
                UpdateRoomList(roomList);
                nextUpdateTime = Time.time + timeBetweenUpdates; // 1.5 saniye
            }
        }
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


    public override void OnJoinedLobby()
    {
        Debug.Log("Joined Lobby");
    }


}
