using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Random = UnityEngine.Random;

public class TileDistrubite : MonoBehaviourPunCallbacks
{
    public GameObject tilePrefab; // Tile prefab
    public List<Tiles> allTiles = new List<Tiles>();
    public List<int> availableQueues = new List<int> { 1, 2, 3, 4 };

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

    #region Generate Tiles
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
    #region Find Joker Tile
    private void SetIndicatorTile()
    {
        if (allTiles.Count == 0)
            return;

        // İlk taşımızı gösterge taşı olarak seç

        Tiles indicatorTile = allTiles[0];
        allTiles.RemoveAt(0);
        Debug.Log("Gösterge taşımız budur: " + indicatorTile.color + " " + indicatorTile.number);

        // Gösterge taşının bir üst numarasını bul
        int upperNumber = indicatorTile.number + 1;
        if (upperNumber > 13)
        {
            upperNumber = 1; // 13 ise 1 olacak
        }

        // Sahte okey taşlarını güncelle
        UpdateFakeJokerTiles(upperNumber, indicatorTile.color);

        photonView.RPC("SyncIndicatorTile", RpcTarget.All, indicatorTile);
    }
    [PunRPC]
    public void SyncIndicatorTile(Tiles indicatorTile)
    {
        // Gösterge taşını oluştur
        GameObject indicatorTileObject = Instantiate(tilePrefab, indicatorTileContainer);
        TileUI tileUI = indicatorTileObject.GetComponent<TileUI>();

        if (tileUI != null)
        {
            tileUI.SetTileData(indicatorTile);
            tileUI.isIndicatorTile = true; // Bu bir gösterge taşı
        }
        else
        {
            Debug.LogError("TileUI component missing on tilePrefab.");
        }
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
        AssignPlayerQueue();
    }
    #endregion
    [PunRPC]
    public void SyncShuffledTiles(Tiles[] shuffledTiles)
    {
        allTiles.Clear();
        allTiles.AddRange(shuffledTiles);

        DistributeTilesToAllPlayers();

    }
    #endregion

    #region Assign Player Queue


    public void AssignPlayerQueue()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            var players = PhotonNetwork.CurrentRoom.Players.Values.ToList();

            // Rastgele bir oyuncu seç
            int randomIndex = Random.Range(0, players.Count);
            Player selectedPlayer = players[randomIndex];

            // Seçilen oyuncunun koltuk numarasını al
            selectedPlayer.CustomProperties.TryGetValue("SeatNumber", out object seatNumberValue);
            int selectedPlayerSeat = (int)seatNumberValue;

            // Seçilen oyuncuya PlayerQue değerini 1 olarak ata
            selectedPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "PlayerQue", 1 } });
            Debug.Log("Seçilen oyuncu " + selectedPlayer.NickName + " sırası: 1");

            // Diğer oyunculara sıraları atama
            int queueValue = 2; // 2. sıradan başla

            // Oyuncuların sıralarını ayarlama
            for (int i = 0; i < players.Count; i++)
            {
                Player player = players[i];

                // Seçilen oyuncu hariç diğer oyuncular için
                if (player != selectedPlayer)
                {
                    // Koltuk numarasını al
                    player.CustomProperties.TryGetValue("SeatNumber", out object otherSeatNumberValue);
                    int otherSeatNumber = (int)otherSeatNumberValue;

                    // Seçilen oyuncunun koltuk numarasına göre sırayı ayarla
                    int position = (selectedPlayerSeat + (i % (players.Count - 1))) % players.Count;

                    // Eğer pozisyon 0 ise, bu 1. koltuk
                    if (position == 0)
                    {
                        position = players.Count; // 4. koltuk
                    }

                    player.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "PlayerQue", queueValue } });
                    Debug.Log("Oyuncu " + player.NickName + " sırası: " + queueValue);
                    queueValue++;
                }
            }
        }
    }


    #endregion
    #region Distrubite Tiles
    public void DistributeTilesToAllPlayers()
    {
        // İlk oyuncuya 15, diğer oyunculara 14 taş verilecek
        int tilesForFirstPlayer = 15;
        int tilesForOtherPlayers = 14;// Mevcut oyuncuları al
        var players = PhotonNetwork.CurrentRoom.Players.Values.ToList();

        // Oyuncuların sıralarını almak için bir liste oluştur
        List<Player> orderedPlayers = players.OrderBy(p =>
        {
            // PlayerQue anahtarının varlığını kontrol et
            if (p.CustomProperties.TryGetValue("PlayerQue", out object playerQueValue) && playerQueValue is int)
            {
                return (int)playerQueValue; // Eğer varsa, değeri döndür
            }
            return int.MaxValue; // Eğer yoksa, en yüksek değeri döndür (bu oyuncu en sona atılır)
        }).ToList();

        // Sıralı oyuncuları yazdır
        for (int i = 0; i < orderedPlayers.Count; i++)
        {
            Player player = orderedPlayers[i];
            player.CustomProperties.TryGetValue("SeatNumber", out object seatNumber);

            // Taşları dağıtım
            int tilesToGive = (i == 0) ? tilesForFirstPlayer : tilesForOtherPlayers; // İlk oyuncuya 15, diğerlerine 14 taş ver

            for (int j = 0; j < tilesToGive; j++)
            {
                if (allTiles.Count == 0)
                {
                    Debug.LogWarning("No more tiles left to distribute!");
                    return;
                }

                Tiles tile = allTiles[0];
                allTiles.RemoveAt(0);

                // Oyuncunun taşlarını ekle
                if ((int)seatNumber == (i + 1)) // SeatNumber'a göre kontrol et
                {
                    switch (i)
                    {
                        case 0:
                            playerTiles1.Add(tile);
                            InstantiateTiles(j, tile);
                            break;
                        case 1:
                            playerTiles2.Add(tile);
                            InstantiateTiles(j, tile);
                            break;
                        case 2:
                            playerTiles3.Add(tile);
                            InstantiateTiles(j, tile);
                            break;
                        case 3:
                            playerTiles4.Add(tile);
                            InstantiateTiles(j, tile);
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
