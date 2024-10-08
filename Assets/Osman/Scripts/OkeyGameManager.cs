using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;

public class OkeyGameManager : MonoBehaviourPunCallbacks
{
    public GameObject playerPrefab;  // Oyuncu prefab'i
    public RectTransform[] spawnPositions;  // UI'daki oyuncu pozisyonları
    public GameObject tilePrefab;  // Okey taşı prefab'i
    public Transform playerTileContainer;  // Tüm oyuncuların taşlarının yerleştirileceği ana container (Grid Layout içeriyor)
    
    public List<Sprite> tileSprites; // Okey taşlarının sprite'ları

    private List<int> availablePositions = new List<int>();

    public override void OnJoinedRoom()
    {
        AssignRandomPositionAndInstantiate();  // Oyuncuyu rastgele bir pozisyona yerleştir ve instantiate et
        AssignRelativePlayerPositions();       // Oyunculara göreceli pozisyonlarını belirle
        if (PhotonNetwork.IsMasterClient)      // Sadece MasterClient taşları dağıtır
        {
            DistributeTiles();                 // Taşları oyunculara dağıt
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

            Vector3 spawnPosition = spawnPositions[spawnIndex].transform.position;
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
        }
    }

    private void DistributeTiles()
    {
        Player[] players = PhotonNetwork.PlayerList;
        int playerCount = players.Length;
        int tilesForFirstPlayer = 15;
        int tilesForOtherPlayers = 14;

        for (int playerIndex = 0; playerIndex < playerCount; playerIndex++)
        {
            int tilesToGive = (playerIndex == 0) ? tilesForFirstPlayer : tilesForOtherPlayers;

            // Her oyuncunun taşları için alt container oluştur
            Transform playerContainer = playerTileContainer.GetChild(playerIndex);  // PlayerTileContainer altındaki her bir placeholder

            for (int i = 0; i < tilesToGive; i++)
            {
                GameObject newTile = Instantiate(tilePrefab, playerContainer);
                newTile.GetComponent<Image>().sprite = tileSprites[Random.Range(0, tileSprites.Count)];  // Taşa rastgele bir sprite atanıyor
                newTile.name = "Tile_" + i;  // Taşın adını belirleme (debug için)
            }

            Debug.Log(players[playerIndex].NickName + " received " + tilesToGive + " tiles.");
        }
    }
}
