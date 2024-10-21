using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using TMPro;
using System.Linq;
using Unity.VisualScripting.Dependencies.Sqlite;

public class OkeyGameManager : MonoBehaviourPunCallbacks
{
    //[SerializeField] private GameObject seatManagerPrefab;
    public GameObject playerPrefab;  // Oyuncu prefab'i
    public TileManager tileManager;  // TileManager scriptine referans

    public RectTransform[] spawnPositions;  // UI'daki oyuncu pozisyonları


    public TextMeshProUGUI[] playerNameTexts; // Oyuncu isimlerinin yazdırılacağı UI elemanları
    public Transform[] playerTileContainers; // Oyuncuların taşlarının yerleştirileceği Placeholder'lar

    public int spawnIndex;

    PhotonView playerPhotonView;

    public override void OnJoinedRoom()
    {
        AssignPositionAndInstantiate();  // Oyuncuyu rastgele bir pozisyona yerleştir ve instantiate et
        AssignRelativePlayerPositions();       // Oyunculara göreceli pozisyonlarını belirle

        /*     if (PhotonNetwork.IsMasterClient)  // Sadece MasterClient taşları dağıtır
           {

              if (tileManager == null)
                {
                    Debug.LogError("TileManager is not assigned in OkeyGameManager!");
                    return;
                }

                tileManager.DistributeTiles();  // Taşları dağıt (parametresiz)
           }*/

    }


    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        AssignRelativePlayerPositions();  // Yeni bir oyuncu katıldığında oyuncuların göreceli pozisyonlarını güncelle
    }
    private void AssignPositionAndInstantiate()
    {

        if (playerPrefab != null)
        {

            Quaternion spawnRotation = Quaternion.identity;

            // Oyuncuyu belirlenen pozisyona yerleştir
            GameObject player = PhotonNetwork.Instantiate(playerPrefab.name, Vector3.zero, spawnRotation, 0);

            ;

            Vector3 spawnPosition = spawnPositions[spawnIndex].position;

            playerPhotonView = player.GetComponent<PhotonView>();


            playerPhotonView.RPC("SetPlayerName", RpcTarget.AllBuffered, PhotonNetwork.NickName);
            playerPhotonView.RPC("SetPlayerSeat", RpcTarget.AllBuffered, spawnPosition);

        }
        else
        {
            Debug.LogError("No available spawn positions found or playerPrefab is not assigned!");
        }
    }




    private void AssignRelativePlayerPositions()
    {
        Player[] players = PhotonNetwork.PlayerList;
        int localPlayerIndex = System.Array.IndexOf(players, PhotonNetwork.LocalPlayer);


        if (localPlayerIndex == -1)
        {
            Debug.LogError("Local player not found in the player list!");
            return;
        }

        for (int i = 0; i < players.Length; i++)
        {
            // Her oyuncu için relativeIndex, kendisini sıfırıncı indexte görmeli ve diğerlerini göreceli olarak sıralamalıdır
            int relativeIndex = (i - localPlayerIndex + players.Length) % players.Length;

            Debug.Log("Local player sees " + players[i].NickName + " at relative position " + (relativeIndex + 1));

            if (relativeIndex < playerNameTexts.Length)
            {
                //playerNameTexts[relativeIndex].text = players[i].NickName;
            }
        }
    }




}