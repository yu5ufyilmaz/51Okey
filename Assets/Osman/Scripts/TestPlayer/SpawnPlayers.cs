using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpawnPlayers : MonoBehaviourPunCallbacks
{
    public GameObject _playerPrefab;
    public TextMeshProUGUI _playerNickName_P2, _playerNickName_P3, _playerNickName_P4;

    public int playerCount;
    private string nickName;

    private void Start()
    {
        EventDispatcher.RegisterFunction("SpawnPlayer", SpawnPlayer);
        nickName = PhotonNetwork.NickName;
    }
    public void SpawnPlayer()
    {
        GameObject _player = PhotonNetwork.Instantiate(_playerPrefab.name, new Vector3(0f, 0f, 0f), Quaternion.identity, 0);
        //player count kısmı oda içerisinde kaç kişi olduğuna göre değişmeyecek
        _player.GetComponent<PhotonView>().RPC("SetPlayerName", RpcTarget.AllBuffered, nickName);
        UpdatePlayerQueue();
        _player.GetComponent<PhotonView>().RPC("SetPlayerQueue", RpcTarget.AllBuffered, playerCount);
    }

    public void UpdatePlayerQueue()
    {
        int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;

        List<Player> players = new List<Player>(PhotonNetwork.PlayerList);
        players.Sort((x, y) => x.ActorNumber.CompareTo(y.ActorNumber));  // Oyuncuları ActorNumber’a göre sırala (sabit bir sıra için)

        // Oda içerisindeki oyuncuları yerleştir
        if (playerCount > 1) _playerNickName_P2.text = players[1 % playerCount].NickName;
        if (playerCount > 2) _playerNickName_P3.text = players[2 % playerCount].NickName;
        if (playerCount > 3) _playerNickName_P4.text = players[3 % playerCount].NickName;
    }

    // Odaya yeni oyuncu girdiğinde sırayı güncelle
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        UpdatePlayerQueue();
    }

    // Bir oyuncu çıktığında sırayı güncelle
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        UpdatePlayerQueue();
    }
}
