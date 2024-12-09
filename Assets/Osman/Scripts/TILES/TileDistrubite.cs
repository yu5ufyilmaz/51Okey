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
    public GameObject tilePrefab; // Tile prefab
    public List<Tiles> allTiles = new List<Tiles>();

    [Header("Player Tiles")]
    public List<Tiles> playerTiles1 = new List<Tiles>();
    public List<Tiles> playerTiles2 = new List<Tiles>();
    public List<Tiles> playerTiles3 = new List<Tiles>();
    public List<Tiles> playerTiles4 = new List<Tiles>();

    public Transform playerTileContainer; // Tile container for player
    private Transform[] playerTileContainers; // Player tile placeholders
    public Transform dropTileContainer; // Tile container for player
    private Transform[] dropTileContainers;
    public Transform indicatorTileContainer;
    public Transform middleTileContainer;

    #region Generate and Shuffle Tiles
    private void Awake()
    {
        TileSerialization.RegisterCustomTypes(); // Custom serialization for TileDataInfo
    }

    private void Start()
    {
        indicatorTileContainer = GameObject.Find("IndicatorTileContainer").transform;
        dropTileContainer = GameObject.Find("DropTileContainers").transform;
        playerTileContainer = GameObject.Find("PlayerTileContainer").transform;
        middleTileContainer = GameObject.Find("MiddleTileContainer").transform;
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
        InitializeDropPlaceholders();
    }

    private void InitializeDropPlaceholders()
    {
        int placeholderCount = dropTileContainer.childCount;
        dropTileContainers = new Transform[placeholderCount];

        for (int i = 0; i < placeholderCount; i++)
        {
            dropTileContainers[i] = dropTileContainer.GetChild(i);
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
        AddFakeJokerTiles();
    }

    private void AddFakeJokerTiles()
    {
        // İki sahte okey taşı ekle
        Tiles fakeJoker1 = new Tiles(TileColor.Green, 1, TileType.FakeJoker); // Renk None, numara 1
        Tiles fakeJoker2 = new Tiles(TileColor.Green, 2, TileType.FakeJoker); // Renk None, numara 2

        allTiles.Add(fakeJoker1);
        allTiles.Add(fakeJoker2);

        Debug.Log("İki sahte okey taşı eklendi: " + fakeJoker1.number + ", " + fakeJoker2.number);
    }

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

        // Joker taşlarını belirle

        SetIndicatorTile();

        // Karıştırılmış listeyi tüm istemcilere ilet
    }

    private void SetIndicatorTile()
    {
        if (allTiles.Count == 0)
            return;

        // İlk taşımızı gösterge taşı olarak seç
        Tiles indicatorTile = allTiles[0];
        allTiles.RemoveAt(0);
        GameObject indicatorTileObject = Instantiate(tilePrefab, indicatorTileContainer);
        TileUI tileUI = indicatorTileObject.GetComponent<TileUI>();
        if (tileUI != null)
        {
            tileUI.SetTileData(indicatorTile);
        }
        else
        {
            Debug.LogError("TileUI component missing on tilePrefab.");
        }
        


        Debug.Log("Gösterge taşımız budur: " + indicatorTile.color + " " + indicatorTile.number);

        // Gösterge taşının bir üst numarasını bul
        int upperNumber = indicatorTile.number + 1;
        if (upperNumber > 13)
        {
            upperNumber = 1; // 13 ise 1 olacak
        }

        // Sahte okey taşlarını güncelle
        UpdateFakeJokerTiles(upperNumber, indicatorTile.color);
    }

    private void UpdateFakeJokerTiles(int upperNumber, TileColor color)
    {
        foreach (var tile in allTiles)
        {
            // Eğer sahte okey taşları listede varsa ve numarası üst numaraya eşitse, tipini joker yap
            if (tile.type == TileType.Number && tile.number == upperNumber && tile.color == color)
            {
                tile.type = TileType.Joker; // Joker taşını ayarla
                tile.color = color;
                Debug.Log(
                    "Sahte okey taşı güncellendi: "
                        + tile.color
                        + " "
                        + tile.number
                        + " -> Joker taşına dönüştürüldü."
                );
            }
            else if (tile.type == TileType.FakeJoker)
            {
                tile.color = color;
                tile.number = upperNumber;
            }
        }
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

        Player player = PhotonNetwork.LocalPlayer;
        player.CustomProperties.TryGetValue("SeatNumber", out object seatNumber);

        for (int i = 0; i < 4; i++)
        {
            Debug.Log("Player: " + seatNumber);
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
                    if ((int)seatNumber == 1)
                    {
                        //Debug.Log("Player 1");
                        InstantiateTiles(j, tile);
                    }
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
                            if ((int)seatNumber == 2)
                            {
                                //Debug.Log("Player 2");
                                InstantiateTiles(j, tile);
                            }
                            break;
                        case 2:
                            playerTiles3.Add(tile);
                            //Debug.Log("Player 3");
                            if ((int)seatNumber == 3)
                            {
                                InstantiateTiles(j, tile);
                            }
                            break;
                        case 3:
                            playerTiles4.Add(tile);
                            //Debug.Log("Player 4");
                            if ((int)seatNumber == 4)
                            {
                                InstantiateTiles(j, tile);
                            }

                            break;
                    }
                }
            }
        }
        PlaceRemainingTilesInMiddleContainer();
    }

    void InstantiateTiles(int tilecount, Tiles tile)
    {
        GameObject tileInstance = Instantiate(tilePrefab, playerTileContainers[tilecount]);

        TileUI tileUI = tileInstance.GetComponent<TileUI>();
        if (tileUI != null)
        {
            tileUI.SetTileData(tile);
        }
        else
        {
            Debug.LogError("TileUI component missing on tilePrefab.");
        }
    }

    private void PlaceRemainingTilesInMiddleContainer()
    {
        for (int i = 0; i < allTiles.Count; i++)
        {
            GameObject tileInstance = Instantiate(tilePrefab, middleTileContainer);
        }
    }


    #endregion

    #region GamePlay
    /*public void DrawTileFromLeftOrCenter()
    {
        Player player = PhotonNetwork.LocalPlayer;
        player.CustomProperties.TryGetValue("SeatNumber", out object seatNumber);
        List<Tiles> sourceTiles = new List<Tiles>();

        // Sol veya merkezden taş çekme
        if ((int)seatNumber == 1) // Örnek: 1. oyuncu
        {
            // Sol drop container'dan veya merkezden taş çek
            if (playerTiles1.Count < 15) // Eğer 15 taş yoksa
            {
                if (dropTileContainers[0].childCount > 0) // Sol container'da taş varsa
                {
                    //Tiles tile = GetTileFromContainer(dropTileContainers[0]);
                    //playerTiles1.Add(tile);
                    //InstantiateTiles(playerTiles1.Count - 1, tile);
                }
                else if (dropTileContainers[1].childCount > 0) // Merkez container'da taş varsa
                {
                    //Tiles tile = GetTileFromContainer(dropTileContainers[1]);
                    //playerTiles1.Add(tile);
                    //InstantiateTiles(playerTiles1.Count - 1, tile);
                }
            }
        }
    }*/


    public void DropExcessTile()
    {
        Player player = PhotonNetwork.LocalPlayer;
        player.CustomProperties.TryGetValue("SeatNumber", out object seatNumber);

        if (playerTiles1.Count == 15) // Eğer 15 taş varsa
        {
            Tiles excessTile = playerTiles1[14]; // Fazla taş
            playerTiles1.RemoveAt(14); // Fazla taşı elden çıkar

            // Sağdaki drop container'a bırak
            if (dropTileContainers[2].childCount < 1) // Sağ container boşsa
            {
                GameObject tileInstance = Instantiate(tilePrefab, dropTileContainers[2]);
                TileUI tileUI = tileInstance.GetComponent<TileUI>();
                if (tileUI != null)
                {
                    tileUI.SetTileData(excessTile);
                }
                else
                {
                    Debug.LogError("TileUI component missing on tilePrefab.");
                }
            }
        }
    }
    /*
    private Tiles GetTileFromContainer(Transform container)
    {
        // Container'dan bir taş al
        Tiles tile = container.GetChild(0).GetComponent<TileUI>().GetTileData();
        Destroy(container.GetChild(0).gameObject); // Taşı yok et
        return tile;
    }*/

    #endregion
}
