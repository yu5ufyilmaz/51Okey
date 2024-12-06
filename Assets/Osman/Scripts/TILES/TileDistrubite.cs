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
    public List<Tiles> playerTiles1 = new List<Tiles>();
    public List<Tiles> playerTiles2 = new List<Tiles>();
    public List<Tiles> playerTiles3 = new List<Tiles>();
    public List<Tiles> playerTiles4 = new List<Tiles>();
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

        for (int i = 0; i < 4; i++)
        {
            if (i == 0)
            {
                for (int j = 0; j < tilesForFirstPlayer; j++)
                {
                    if (allTiles.Count == 0)
                    {
                        Debug.LogWarning("No more tiles left to distribute!");
                        return;
                    }

                    Tiles tile = allTiles[0];
                    allTiles.RemoveAt(0);
                    playerTiles1.Add(tile);
                }
            }
            else
            {

                for (int j = 0; j < tilesForOtherPlayers; j++)
                {
                    if (allTiles.Count == 0)
                    {
                        Debug.LogWarning("No more tiles left to distribute!");
                        return;
                    }

                    Tiles tile = allTiles[0];
                    allTiles.RemoveAt(0);

                    switch (i)
                    {
                        case 1:
                            playerTiles2.Add(tile);
                            break;
                        case 2:
                            playerTiles3.Add(tile);
                            break;
                        case 3:
                            playerTiles4.Add(tile);
                            break;
                    }
                }
            }
        }

    }



    #endregion
}
