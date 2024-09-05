using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
public class ButtonFuncs : MonoBehaviour
{
public void ConnectLobby()
{
    if(PhotonNetwork.InLobby)
    {
        SceneManager.LoadScene("LobbyMenu");
    }
}
//Buraya oda ayarlarının değiştirilebilineceği ayar panelinin açılıp oradan RoomOptionsı değiştirebilinecek kodlar yazılabilir.
public void CreateGame()
{
    if(PhotonNetwork.InLobby)
    {
        PhotonNetwork.CreateRoom("new_room",new RoomOptions{MaxPlayers = 4,IsOpen = true,IsVisible = true},null);
        SceneManager.LoadScene("Table");
    }
}
public void JoinGame()
{
    if(PhotonNetwork.InLobby)
    {
        PhotonNetwork.JoinOrCreateRoom("new_room",new RoomOptions{MaxPlayers = 4,IsOpen = true,IsVisible = true},null);
        SceneManager.LoadScene("Table");
    }
}
}
