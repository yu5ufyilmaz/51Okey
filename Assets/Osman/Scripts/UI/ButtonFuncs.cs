using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.UI;
public class ButtonFuncs : MonoBehaviourPunCallbacks
{



    public RoomItem roomItemPrefab;
    List<RoomItem> roomItemsList = new List<RoomItem>();

    [SerializeField]
    private Transform _content;
    public void ConnectLobby()
    {
        if (PhotonNetwork.InLobby)
        {
            SceneManager.LoadScene("LobbyMenu");
        }
    }
    //Buraya oda ayarlarının değiştirilebilineceği ayar panelinin açılıp oradan RoomOptionsı değiştirebilinecek kodlar yazılabilir.
    public void CreateGame()
    {
        if (PhotonNetwork.InLobby)
        {
            PhotonNetwork.CreateRoom("new_room", new RoomOptions { MaxPlayers = 4, IsOpen = true, IsVisible = true }, null);
            // SceneManager.LoadScene("Table");
        }
    }
    public void JoinGame()
    {
        if (PhotonNetwork.InLobby)
        {
            PhotonNetwork.JoinOrCreateRoom("new_rom", new RoomOptions { MaxPlayers = 4, IsOpen = true, IsVisible = true }, null);
            //SceneManager.LoadScene("Table");
        }
    }


    public override void OnJoinedRoom()
    {
        
        Debug.Log("Joined Room");
    }
    public override void OnLeftRoom()
    {
        Debug.Log("Left Room");
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
}
