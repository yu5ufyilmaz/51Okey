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
        playerCount = PhotonNetwork.CurrentRoom.PlayerCount - 1;
        Debug.Log(playerCount);
        _player.GetComponent<PhotonView>().RPC("SetPlayerName", RpcTarget.AllBuffered, nickName);
        _player.GetComponent<PhotonView>().RPC("SetPlayerQueue", RpcTarget.AllBuffered, playerCount);
    }

    public void PlayerPlacement()
    {

            switch (playerCount)
            {
                case 0:
                    _playerNickName_P2.text = PhotonNetwork.PlayerList[1].NickName;
                    _playerNickName_P3.text = PhotonNetwork.PlayerList[2].NickName;
                    _playerNickName_P4.text = PhotonNetwork.PlayerList[3].NickName;
                    break;
                case 1:
                    _playerNickName_P2.text = PhotonNetwork.PlayerList[2].NickName;
                    _playerNickName_P3.text = PhotonNetwork.PlayerList[3].NickName;
                    _playerNickName_P4.text = PhotonNetwork.PlayerList[0].NickName;
                    break;
                case 2:
                    _playerNickName_P2.text = PhotonNetwork.PlayerList[3].NickName;
                    _playerNickName_P3.text = PhotonNetwork.PlayerList[0].NickName;
                    _playerNickName_P4.text = PhotonNetwork.PlayerList[1].NickName;
                    break;
                case 3:
                    _playerNickName_P2.text = PhotonNetwork.PlayerList[0].NickName;
                    _playerNickName_P3.text = PhotonNetwork.PlayerList[1].NickName;
                    _playerNickName_P4.text = PhotonNetwork.PlayerList[2].NickName;
                    break;
            }
    }
}
