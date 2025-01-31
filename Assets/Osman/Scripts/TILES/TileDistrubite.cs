using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.Tilemaps;
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
    public Tiles dropTile;
    public Transform playerTileContainer; // Player tile container
    private Transform[] playerTileContainers; // Player tile placeholders
    public Transform dropTileContainer; // Drop tile container
    private Transform[] dropTileContainers; // Drop tile placeholders
    public Transform indicatorTileContainer; // Indicator tile container
    public Transform middleTileContainer; // Middle tile container

    #region Generate Tiles
    private void Awake()
    {
        TileSerialization.RegisterCustomTypes(); // Custom serialization for TileDataInfo
    }

    private void Start()
    {
        // Find and assign containers
        indicatorTileContainer = GameObject.Find("IndicatorTileContainer").transform;
        dropTileContainer = GameObject.Find("DropTileContainers").transform;
        playerTileContainer = GameObject.Find("PlayerTileContainer").transform;
        middleTileContainer = GameObject.Find("MiddleTileContainer").transform;

        InitializePlaceholders(); // Initialize tile placeholders
        GeneratePlayerTiles(); // Generate player tiles
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
        InitializeDropPlaceholders(); // Initialize drop tile placeholders
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

    // Generate player tiles
    public void GeneratePlayerTiles()
    {
        allTiles.Clear(); // Clear existing tiles
        foreach (TileColor color in Enum.GetValues(typeof(TileColor)))
        {
            for (int i = 1; i <= 13; i++)
            {
                // Add two copies of each tile
                allTiles.Add(new Tiles(color, i, TileType.Number));
                allTiles.Add(new Tiles(color, i, TileType.Number));
            }
        }
        AddFakeJokerTiles(); // Add fake joker tiles
    }

    private void AddFakeJokerTiles()
    {
        // Add two fake joker tiles
        Tiles fakeJoker1 = new Tiles(TileColor.Green, 1, TileType.FakeJoker); // Fake joker tile 1
        Tiles fakeJoker2 = new Tiles(TileColor.Green, 2, TileType.FakeJoker); // Fake joker tile 2

        allTiles.Add(fakeJoker1);
        allTiles.Add(fakeJoker2);

        Debug.Log("Two fake joker tiles added: " + fakeJoker1.number + ", " + fakeJoker2.number);
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

        // Set the indicator tile
        SetIndicatorTile();
    }
    #endregion
    #region Find Joker Tile
    private void SetIndicatorTile()
    {
        if (allTiles.Count == 0)
            return;

        // Select the first tile as the indicator tile
        Tiles indicatorTile = allTiles[0];
        allTiles.RemoveAt(0);
        Debug.Log("Indicator tile is: " + indicatorTile.color + " " + indicatorTile.number);

        // Find the upper number for the joker tile
        int upperNumber = indicatorTile.number + 1;
        if (upperNumber > 13)
        {
            upperNumber = 1; // Wrap around to 1 if it exceeds 13
        }

        // Update fake joker tiles
        UpdateFakeJokerTiles(upperNumber, indicatorTile.color);

        // Sync the indicator tile across all clients
        photonView.RPC("SyncIndicatorTile", RpcTarget.All, indicatorTile);
    }

    [PunRPC]
    public void SyncIndicatorTile(Tiles indicatorTile)
    {
        // Instantiate the indicator tile
        GameObject indicatorTileObject = Instantiate(tilePrefab, indicatorTileContainer);
        TileUI tileUI = indicatorTileObject.GetComponent<TileUI>();

        if (tileUI != null)
        {
            tileUI.SetTileData(indicatorTile);
            tileUI.isIndicatorTile = true; // Mark as indicator tile
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
            // Update the tile type to Joker if it matches the upper number and color
            if (tile.type == TileType.Number && tile.number == upperNumber && tile.color == color)
            {
                tile.type = TileType.Joker; // Set as joker tile
                tile.color = color;
                Debug.Log("Fake joker tile updated: " + tile.color + " " + tile.number + " -> converted to joker tile.");
            }
            else if (tile.type == TileType.FakeJoker)
            {
                tile.color = color;
                tile.number = upperNumber;
            }
        }
        photonView.RPC("AssignPlayerQueue", RpcTarget.All);
        photonView.RPC("SyncShuffledTiles", RpcTarget.All, allTiles.ToArray());
    }
    #endregion

    [PunRPC]
    public void SyncShuffledTiles(Tiles[] shuffledTiles)
    {
        allTiles.Clear();
        allTiles.AddRange(shuffledTiles);
        DistributeTilesToAllPlayers(); // Distribute shuffled tiles to players
    }

    #region Assign Player Queue
    [PunRPC]
    public void AssignPlayerQueue()
    {
        // Only the MasterClient will assign the queue
        if (!PhotonNetwork.IsMasterClient) return;

        // Get the list of players in the room
        var players = PhotonNetwork.CurrentRoom.Players.Values.ToList();

        // Randomly select a player
        int randomIndex = Random.Range(0, players.Count);
        Player selectedPlayer = players[randomIndex];

        // Get the seat number of the selected player
        selectedPlayer.CustomProperties.TryGetValue("SeatNumber", out object seatNumberValue);
        int selectedPlayerSeat = (int)seatNumberValue;

        // Assign PlayerQue value of 1 to the selected player
        photonView.RPC("AssignQueueToPlayer", RpcTarget.AllBuffered, selectedPlayer.ActorNumber, 1);

        // Assign queue values to other players in a circular manner
        int queueValue = 2; // Start from 2
        int playerCount = players.Count; // Total number of players

        // Loop through players to assign queue values
        for (int i = 1; i < playerCount; i++)
        {
            // Calculate the next seat number in a circular manner
            int nextSeat = (selectedPlayerSeat - 1 + i) % playerCount + 1;

            // Find the player with the next seat number
            Player player = players.FirstOrDefault(p =>
            {
                p.CustomProperties.TryGetValue("SeatNumber", out object otherSeatNumberValue);
                return (int)otherSeatNumberValue == nextSeat;
            });

            // If the player is found and is not the selected player, assign the queue value
            if (player != null && player != selectedPlayer)
            {
                photonView.RPC("AssignQueueToPlayer", RpcTarget.AllBuffered, player.ActorNumber, queueValue);
                queueValue++;
            }
        }
        photonView.RPC("AssignQueueComplete", RpcTarget.AllBuffered);
    }

    [PunRPC]
    private void AssignQueueToPlayer(int actorNumber, int queueValue)
    {
        Player player = PhotonNetwork.CurrentRoom.GetPlayer(actorNumber);
        if (player != null)
        {
            ExitGames.Client.Photon.Hashtable newProperties = player.CustomProperties;
            newProperties["PlayerQue"] = queueValue;
            player.SetCustomProperties(newProperties);

            Debug.Log($"PlayerQue assigned: Player {player.NickName}, ActorNumber: {actorNumber}, QueueValue: {queueValue}");
        }
        else
        {
            Debug.LogError($"Player with ActorNumber {actorNumber} not found in the room.");
        }
    }

    public int GetQueueNumberOfPlayer(Player player)
    {
        if (player.CustomProperties.TryGetValue("PlayerQue", out object queueValue))
        {
            return (int)queueValue;
        }
        else
        {
            // Güvenli loglama
            var propertiesLog = string.Join(", ", player.CustomProperties.Select(kv =>
            {
                string key = kv.Key?.ToString() ?? "null";
                string value = kv.Value?.ToString() ?? "null";
                return $"{key}={value}";
            }));

            Debug.LogError($"PlayerQue not found for player: {player.NickName}. Current properties: {propertiesLog}");
            return -1; // Varsayılan değer
        }
    }
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (changedProps.ContainsKey("PlayerQue"))
        {
            Debug.Log($"PlayerQue updated: Player {targetPlayer.NickName}, PlayerQue: {changedProps["PlayerQue"]}");
        }
        else
        {
            Debug.Log($"Properties updated for {targetPlayer.NickName}, but PlayerQue not found.");
        }
    }
    #endregion

    #region Distribute Tiles

    [PunRPC]
    public void AssignQueueComplete()
    {
        Debug.Log("Player queue assignment completed.");
    }
    public void DistributeTilesToAllPlayers()
    {


        int tilesForFirstPlayer = 15; // Number of tiles for the first player
        int tilesForOtherPlayers = 14; // Number of tiles for other players

        for (int i = 0; i < 4; i++)
        {
            Player player = PhotonNetwork.LocalPlayer;

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
                    if (GetQueueNumberOfPlayer(player) == 1)
                    {
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
                            if (GetQueueNumberOfPlayer(player) == 2)
                            {
                                InstantiateTiles(j, tile);
                            }
                            break;
                        case 2:
                            playerTiles3.Add(tile);
                            if (GetQueueNumberOfPlayer(player) == 3)
                            {
                                InstantiateTiles(j, tile);
                            }
                            break;
                        case 3:
                            playerTiles4.Add(tile);
                            if (GetQueueNumberOfPlayer(player) == 4)
                            {
                                InstantiateTiles(j, tile);
                            }
                            break;
                    }
                }
            }
        }
        PlaceRemainingTilesInMiddleContainer(); // Place remaining tiles in the middle container
    }

    void InstantiateTiles(int tileCount, Tiles tile)
    {
        GameObject tileInstance = Instantiate(tilePrefab, playerTileContainers[tileCount]);

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
    public List<Tiles> GetPlayerTiles(int playerNumber)
    {
        switch (playerNumber)
        {
            case 1:
                return playerTiles1;
            case 2:
                return playerTiles2;
            case 3:
                return playerTiles3;
            case 4:
                return playerTiles4;
            default:
                Debug.LogError($"Invalid player number: {playerNumber}");
                return new List<Tiles>(); // Boş bir liste döndür
        }
    }
    public void DropExcessTile()
    {
        Player player = PhotonNetwork.LocalPlayer;
        player.CustomProperties.TryGetValue("SeatNumber", out object seatNumber);

        if (playerTiles1.Count == 15) // If the player has 15 tiles
        {
            Tiles excessTile = playerTiles1[14]; // Excess tile
            playerTiles1.RemoveAt(14); // Remove the excess tile

            // Drop the tile in the right drop container
            if (dropTileContainers[2].childCount < 1) // If the right container is empty
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


    [PunRPC]
    public void RemoveTileFromPlayerList(int playerNumber, int tileIndex)
    {

        switch (playerNumber)
        {
            case 1:
                InstatiateSideTiles(playerNumber, playerTiles1[tileIndex]);
                Debug.Log($"Player {playerNumber} removed tile: {playerTiles1[tileIndex]}");
                playerTiles1.RemoveAt(tileIndex);


                break;
            case 2:
                InstatiateSideTiles(playerNumber, playerTiles2[tileIndex]);
                Debug.Log($"Player {playerNumber} removed tile: {playerTiles2[tileIndex]}");
                playerTiles2.RemoveAt(tileIndex);

                break;
            case 3:
                InstatiateSideTiles(playerNumber, playerTiles3[tileIndex]);
                Debug.Log($"Player {playerNumber} removed tile: {playerTiles3[tileIndex]}");
                playerTiles3.RemoveAt(tileIndex);

                break;
            case 4:
                InstatiateSideTiles(playerNumber, playerTiles4[tileIndex]);
                Debug.Log($"Player {playerNumber} removed tile: {playerTiles4[tileIndex]}");
                playerTiles4.RemoveAt(tileIndex);

                break;
        }

    }
    [PunRPC]
    public void AddTileFromMiddlePlayerList(int playerNumber)
    {
        switch (playerNumber)
        {
            case 1:
                playerTiles1.Add(allTiles[0]);
                Debug.Log($"Player {playerNumber} added tile: {allTiles[0]}");
                break;
            case 2:
                playerTiles2.Add(allTiles[0]);
                Debug.Log($"Player {playerNumber} added tile: {allTiles[0]}");
                break;
            case 3:
                playerTiles3.Add(allTiles[0]);
                Debug.Log($"Player {playerNumber} added tile: {allTiles[0]}");
                break;
            case 4:
                playerTiles4.Add(allTiles[0]);
                Debug.Log($"Player {playerNumber} added tile: {allTiles[0]}");
                break;
        }
        allTiles.RemoveAt(0);
    }

    List<GameObject> droppedTiles = new List<GameObject>();
    [PunRPC]
    public void AddTileFromDropPlayerList(int playerNumber)
    {
        switch (playerNumber)
        {
            case 1:
                playerTiles1.Add(dropTile);
                Debug.Log($"Player {playerNumber} added tile: {dropTile}");
                DestroySideTiles(1);
                break;
            case 2:
                playerTiles2.Add(dropTile);
                Debug.Log($"Player {playerNumber} added tile: {dropTile}");
                DestroySideTiles(2);
                break;
            case 3:
                playerTiles3.Add(dropTile);
                Debug.Log($"Player {playerNumber} added tile: {dropTile}");
                DestroySideTiles(3);
                break;
            case 4:
                playerTiles4.Add(dropTile);
                Debug.Log($"Player {playerNumber} added tile: {dropTile}");
                DestroySideTiles(4);
                break;
        }

    }

    void InstatiateSideTiles(int playerCount, Tiles tile)
    {
        Player[] player = PhotonNetwork.PlayerList;
        for (int i = 0; i < player.Length; i++)
        {
            if (player[i].CustomProperties.TryGetValue("PlayerQue", out object playerQue))
            {
                int playerQueInt = (int)playerQue;
                if (playerQueInt == playerCount)
                {

                    Transform sideTileContainer = GameObject.Find(player[i].NickName).transform;

                    GameObject tileInstance = Instantiate(tilePrefab, sideTileContainer);
                    TileUI tileUI = tileInstance.GetComponent<TileUI>();
                    dropTile = tile;
                    droppedTiles.Add(tileInstance);
                    if (tileUI != null)
                    {
                        tileUI.SetTileData(tile);
                    }

                }
            }
        }
    }
    void DestroySideTiles(int playerCount)
    {
        for (int i = droppedTiles.Count - 1; i >= 0; i--) // Ters döngü kullanarak güvenli bir şekilde silme işlemi yapıyoruz
        {
            GameObject tile = droppedTiles[i];
            Transform sideTileContainer = tile.transform.parent;
            string playerName = sideTileContainer.name;
            Player[] players = PhotonNetwork.PlayerList;

            bool shouldDestroy = false;

            for (int j = 0; j < players.Length; j++)
            {
                if (players[j].NickName == playerName && players[j].CustomProperties.TryGetValue("PlayerQue", out object playerQue))
                {
                    shouldDestroy = true; // Eğer bu oyuncunun sırası ise, yok etme işlemi yapılacak

                }
            }

            if (shouldDestroy)
            {
                Debug.Log($"Destroying tile: {tile.name} for player {playerName} (Player Queue: {playerCount})");
                Destroy(tile);
                droppedTiles.RemoveAt(i);
            }
            else
            {
                Debug.Log($"Not destroying tile: {tile.name} for player {playerName} (Player Queue: {playerCount})");
            }
        }
    }
    #endregion
}