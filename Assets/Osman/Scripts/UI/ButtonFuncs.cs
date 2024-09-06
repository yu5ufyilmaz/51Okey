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
    //Buraya oda ayarlarının değiştirilebilineceği ayar panelinin açılıp oradan RoomOptionsı değiştirebilinecek kodlar yazılabilir.
    public void CreateGame()
    {
        if (PhotonNetwork.InLobby)
        {
            SceneManager.LoadScene("Table");
            PhotonNetwork.CreateRoom(PhotonNetwork.NickName, new RoomOptions() { MaxPlayers = 4, IsOpen = true, IsVisible = true }, TypedLobby.Default);

        }
    }
    public void JoinRoom(string _roomName)
    {

        if (_roomName != null)
        {
            if (roomItemsList.Count > 0)
            {

                PhotonNetwork.JoinRandomRoom();
                SceneManager.LoadScene("Table");
            }
            else
            {

                PhotonNetwork.CreateRoom(PhotonNetwork.NickName, new RoomOptions() { MaxPlayers = 4, IsOpen = true, IsVisible = true }, TypedLobby.Default);
                SceneManager.LoadScene("Table");
            }
        }
        else
        {
            Debug.Log("zort");
            PhotonNetwork.JoinRoom(_roomName);
            SceneManager.LoadScene("Table");
        }
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
