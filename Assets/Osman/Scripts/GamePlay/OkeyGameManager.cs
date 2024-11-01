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
      // TileManager scriptine referans

    public RectTransform[] spawnPositions;  // UI'daki oyuncu pozisyonları


    public Transform[] playerTileContainers; // Oyuncuların taşlarının yerleştirileceği Placeholder'lar

    public int spawnIndex;

    PhotonView playerPhotonView;

    public override void OnJoinedRoom()
    {
        AssignPositionAndInstantiate();  // Oyuncuyu rastgele bir pozisyona yerleştir ve instantiate et     
    }


    private void AssignPositionAndInstantiate()
    {

        if (playerPrefab != null)
        {

            Quaternion spawnRotation = Quaternion.identity;

            // Oyuncuyu belirlenen pozisyona yerleştir
            GameObject player = PhotonNetwork.Instantiate(playerPrefab.name, Vector3.zero, spawnRotation, 0);


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










}