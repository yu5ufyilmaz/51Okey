using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using TMPro;

public class OkeyGameManager : MonoBehaviourPunCallbacks
{
    public GameObject playerPrefab;  // Oyuncu prefab'i
    public RectTransform[] spawnPositions;  // UI'daki oyuncu pozisyonları
    public TileManager tileManager;  // TileManager scriptine referans
    public TextMeshProUGUI[] playerNameTexts; // Oyuncu isimlerinin yazdırılacağı UI elemanları
    public Transform[] playerTileContainers; // Oyuncuların taşlarının yerleştirileceği Placeholder'lar

    private List<int> availablePositions = new List<int>();

    public override void OnJoinedRoom()
    {
        AssignRandomPositionAndInstantiate();  // Oyuncuyu rastgele bir pozisyona yerleştir ve instantiate et
        AssignRelativePlayerPositions();       // Oyunculara göreceli pozisyonlarını belirle

        if (PhotonNetwork.IsMasterClient)  // Sadece MasterClient taşları dağıtır
        {
            if (tileManager == null)
            {
                Debug.LogError("TileManager is not assigned in OkeyGameManager!");
                return;
            }

            tileManager.DistributeTiles();  // Taşları dağıt (parametresiz)
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        AssignRelativePlayerPositions();  // Yeni bir oyuncu katıldığında oyuncuların göreceli pozisyonlarını güncelle
    }

    private void AssignRandomPositionAndInstantiate()
    {
        availablePositions.Clear();
        for (int i = 0; i < spawnPositions.Length; i++)
        {
            availablePositions.Add(i);
        }

        if (availablePositions.Count > 0 && playerPrefab != null)
        {
            int randomIndex = Random.Range(0, availablePositions.Count);
            int spawnIndex = availablePositions[randomIndex];
            availablePositions.RemoveAt(randomIndex);

            Vector3 spawnPosition = spawnPositions[spawnIndex].position;
            Quaternion spawnRotation = Quaternion.identity;

            PhotonNetwork.Instantiate(playerPrefab.name, spawnPosition, spawnRotation, 0);
            Debug.Log("Player instantiated at random position: " + spawnIndex);
        }
        else
        {
            Debug.LogError("Not enough spawn positions available or playerPrefab is not assigned!");
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
            int relativeIndex = (i - localPlayerIndex + players.Length) % players.Length;
            Debug.Log("Local player sees " + players[i].NickName + " at relative position " + (relativeIndex + 1));

            if (relativeIndex < playerNameTexts.Length)
            {
                playerNameTexts[relativeIndex].text = players[i].NickName;
            }
        }
    }
}
