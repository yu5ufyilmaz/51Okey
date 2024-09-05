using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.UI;
using TMPro;
public class ButtonFuncs : MonoBehaviourPunCallbacks
{



    public RoomItem roomItemPrefab;
    public List<RoomItem> roomItemsList = new List<RoomItem>();

    public Transform _content;


    void Start()
    {
        PhotonNetwork.JoinLobby();
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            List<RoomInfo> roomList = new List<RoomInfo>();
            UpdateRoomList(roomList);
        }
    }
    //Buraya oda ayarlarının değiştirilebilineceği ayar panelinin açılıp oradan RoomOptionsı değiştirebilinecek kodlar yazılabilir.
    public void CreateGame()
    {
        if (PhotonNetwork.InLobby)
        {
            PhotonNetwork.CreateRoom(PhotonNetwork.NickName, new RoomOptions() { MaxPlayers = 4, IsOpen = true, IsVisible = true });
            SceneManager.LoadScene("Table");
        }
    }
    public void JoinGame()
    {
        if (PhotonNetwork.InLobby)
        {
            PhotonNetwork.JoinOrCreateRoom(PhotonNetwork.NickName, new RoomOptions() { MaxPlayers = 4, IsOpen = true, IsVisible = true },TypedLobby.Default);
            SceneManager.LoadScene("Table");
        }
    }
    public void JoinRoom(string _roomName)
    {
        PhotonNetwork.JoinRoom(_roomName);
    }


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
            newRoom.SetRoomName(room.Name);
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
    public override void OnJoinedRoom()
    {

        Debug.Log("Joined Room");
    }
    public override void OnLeftRoom()
    {
        Debug.Log("Left Room");
    }
}
