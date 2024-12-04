using System;
using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Random = UnityEngine.Random;

public class TileDistrubite : MonoBehaviourPunCallbacks
{
    public List<Tiles> allTiles = new List<Tiles>();
    public Transform playerTileContainer; // Tile container for player
    private Transform[] playerTileContainers; // Player tile placeholders
    public Dictionary<int, GameObject> playerObjects = new Dictionary<int, GameObject>();
    #region Generate and Shuffle Tiles
    private void Awake()
    {
        TileSerialization.RegisterCustomTypes(); // Custom serialization for TileDataInfo
    }
    private void Start()
    {
        playerTileContainer = GameObject.Find("PlayerTileContainer").transform;
        InitializePlaceholders();
        GeneratePlayerTiles();
    }

    // Initialize placeholders for player tiles
    private void InitializePlaceholders()
    {
        int placeholderCount = playerTileContainer.childCount;
        playerTileContainers = new Transform[placeholderCount];

        for (int i = 0; i < placeholderCount; i++)
        {
            playerTileContainers[i] = playerTileContainer.GetChild(i);
        }
    }

    // Taşları oluşturma


    public void GeneratePlayerTiles()
    {
        allTiles.Clear();
        //List<Tiles> playerTiles = new List<Tiles>();
        foreach (TileColor color in Enum.GetValues(typeof(TileColor)))
        {

            for (int i = 1; i <= 13; i++)
            {
                // Add two copies of each tile
                allTiles.Add(new Tiles(color, i, TileType.Number));
                allTiles.Add(new Tiles(color, i, TileType.Number));
            }
        }

    }

    [PunRPC]
    public void ShuffleTiles()
    {
        Debug.Log("Shuffling tiles...");
        for (int i = 0; i < allTiles.Count; i++)
        {
            Tiles temp = allTiles[i];
            int randomIndex = Random.Range(i, allTiles.Count);
            allTiles[i] = allTiles[randomIndex];
            allTiles[randomIndex] = temp;
        }

        // Karıştırılmış listeyi tüm istemcilere ilet
        photonView.RPC("SyncShuffledTiles", RpcTarget.All, allTiles.ToArray());
    }

    [PunRPC]
    public void SyncShuffledTiles(Tiles[] shuffledTiles)
    {
        allTiles.Clear();
        allTiles.AddRange(shuffledTiles);
        DistributeTilesToAllPlayers();
    }
    #endregion 

    #region Distrubite Tiles


    public void DistributeTilesToAllPlayers()
    {
        // İlk oyuncuya 15, diğer oyunculara 14 taş verilecek
        int tilesForFirstPlayer = 15;
        int tilesForOtherPlayers = 14;

        // Check if all players have seats assigned
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (!player.CustomProperties.ContainsKey("SeatNumber"))
            {
                Debug.LogWarning($"Player {player.NickName} does not have a seat assigned. Distribution aborted.");
                return;
            }
        }
        // İlk oyuncuyu belirle
        Player firstPlayer = PhotonNetwork.PlayerList[0]; // İlk oyuncu
        int firstPlayerActorNumber = firstPlayer.ActorNumber;

        // İlk oyuncuya taşları dağıt
        List<Tiles> assignedTilesForFirstPlayer = new List<Tiles>();
        for (int j = 0; j < tilesForFirstPlayer; j++)
        {
            if (allTiles.Count == 0)
            {
                Debug.LogWarning("No more tiles left to distribute!");
                return;
            }

            Tiles tile = allTiles[0];
            allTiles.RemoveAt(0);
            assignedTilesForFirstPlayer.Add(tile);
        }

        // İlk oyuncunun nesnesini bul
        GameObject firstPlayerObject = PhotonView.Find(firstPlayerActorNumber)?.gameObject;
        if (firstPlayerObject == null)
        {
            Debug.LogError($"Player object not found for ActorNumber {firstPlayerActorNumber}");
            return;
        }

        // İlk oyuncuya taşları ata
        SetupPlayer firstPlayerSetup = firstPlayerObject.GetComponent<SetupPlayer>();
        if (firstPlayerSetup == null)
        {
            Debug.LogError($"SetupPlayer component not found on player object {firstPlayerObject.name}");
            return;
        }

        firstPlayerSetup.AssignTilesOnce(assignedTilesForFirstPlayer);
        Debug.Log($"Tiles distributed to Player {firstPlayer.NickName}: {assignedTilesForFirstPlayer.Count} tiles.");

        // Diğer oyunculara taşları dağıt
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (player.ActorNumber == firstPlayerActorNumber) // İlk oyuncuyu atla
            {
                continue;
            }

            int actorNumber = player.ActorNumber;

            // Prepare tiles for the player
            List<Tiles> assignedTiles = new List<Tiles>();
            for (int j = 0; j < tilesForOtherPlayers; j++)
            {
                if (allTiles.Count == 0)
                {
                    Debug.LogWarning("No more tiles left to distribute!");
                    return;
                }

                Tiles tile = allTiles[0];
                allTiles.RemoveAt(0);
                assignedTiles.Add(tile);
            }

            // Find player object
            GameObject playerObject = PhotonView.Find(actorNumber)?.gameObject;
            if (playerObject == null)
            {
                Debug.LogError($"Player object not found for ActorNumber {actorNumber}");
                continue;
            }

            // Assign tiles via SetupPlayer
            SetupPlayer setupPlayer = playerObject.GetComponent<SetupPlayer>();
            if (setupPlayer == null)
            {
                Debug.LogError($"SetupPlayer component not found on player object {playerObject.name}. Please ensure it is attached to the correct player prefab.");
                continue;
            }

            setupPlayer.AssignTilesOnce(assignedTiles);
            Debug.Log($"Tiles distributed to Player {player.NickName}: {assignedTiles.Count} tiles.");
        }



    }



    #endregion
}
