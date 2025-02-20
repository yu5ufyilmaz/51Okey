using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using UnityEngine.Tilemaps;
using Random = UnityEngine.Random;

public class TileDistrubite : MonoBehaviourPunCallbacks
{
    public GameObject tilePrefab; // Tile prefab
    public GameObject meldTilePrefab; // Meld tile prefab
    public List<Tiles> allTiles = new List<Tiles>();
    public List<TileUI> tileUIs;
    private TurnManager turnManager;
    public ScoreManager scoreManager;

    [Header("Player Tiles")]
    public List<Tiles> playerTiles1 = new List<Tiles>();
    public List<Tiles> playerTiles2 = new List<Tiles>();
    public List<Tiles> playerTiles3 = new List<Tiles>();
    public List<Tiles> playerTiles4 = new List<Tiles>();
    [Header("Melded Tiles")]
    public List<List<Tiles>> meltedTiles1 = new List<List<Tiles>>();
    public List<List<Tiles>> meltedTiles2 = new List<List<Tiles>>();
    public List<List<Tiles>> meltedTiles3 = new List<List<Tiles>>();
    public List<List<Tiles>> meltedTiles4 = new List<List<Tiles>>();
    public List<List<Tiles>> validMeltedTiles = new List<List<Tiles>>();
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


        turnManager = GameObject.Find("TurnManager").GetComponent<TurnManager>();

        InitializePlaceholders(); // Initialize tile placeholders
        GeneratePlayerTiles(); // Generate player tiles
    }
    public void RegisterTileUI(TileUI tileUI)
    {
        tileUIs.Add(tileUI);
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
        Tiles fakeJoker1 = new Tiles(TileColor.black, 1, TileType.FakeJoker); // Fake joker tile 1
        Tiles fakeJoker2 = new Tiles(TileColor.black, 2, TileType.FakeJoker); // Fake joker tile 2

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
    [PunRPC]
    public void SyncShuffledTiles(Tiles[] shuffledTiles)
    {
        scoreManager = GameObject.Find("ScoreManager(Clone)").GetComponent<ScoreManager>();
        allTiles.Clear();
        allTiles.AddRange(shuffledTiles);
        DistributeTilesToAllPlayers(); // Distribute shuffled tiles to players
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

    #region Tile Pick or Drop Actiions
    public List<Tiles> GetPlayerTiles()
    {
        PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object queueValue);
        int playerNumber = (int)queueValue;
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
    [PunRPC]
    public void RemoveTileFromPlayerList(int playerNumber, int tileIndex)
    {

        switch (playerNumber)
        {
            case 1:
                InstatiateSideTiles(playerNumber, playerTiles1[tileIndex]);
                playerTiles1.RemoveAt(tileIndex);


                break;
            case 2:
                InstatiateSideTiles(playerNumber, playerTiles2[tileIndex]);
                playerTiles2.RemoveAt(tileIndex);

                break;
            case 3:
                InstatiateSideTiles(playerNumber, playerTiles3[tileIndex]);
                playerTiles3.RemoveAt(tileIndex);

                break;
            case 4:
                InstatiateSideTiles(playerNumber, playerTiles4[tileIndex]);
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


                break;
            case 2:
                playerTiles2.Add(allTiles[0]);


                break;
            case 3:
                playerTiles3.Add(allTiles[0]);


                break;
            case 4:
                playerTiles4.Add(allTiles[0]);

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

                DestroySideTiles(1);

                break;
            case 2:
                playerTiles2.Add(dropTile);

                DestroySideTiles(2);

                break;
            case 3:
                playerTiles3.Add(dropTile);

                DestroySideTiles(3);

                break;
            case 4:
                playerTiles4.Add(dropTile);
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
        Player player = PhotonNetwork.LocalPlayer;
        GameObject droppedTile = droppedTiles.Last();
        for (int j = 0; j < 4; j++)
        {
            player.CustomProperties.TryGetValue("PlayerQue", out object playerQue);
            int playerQueInt = (int)playerQue;
            if (droppedTiles.Count > 0)
            {
                if (droppedTiles[droppedTiles.Count - 1] == droppedTile)
                    droppedTiles.RemoveAt(droppedTiles.Count - 1);
            }
            if (playerQueInt != playerCount)
            {
                Destroy(droppedTile);

            }

        }
    }
    #endregion

    #region Meld Tiles
    private List<List<Vector2Int>> meltedTilesPositions1 = new List<List<Vector2Int>>();
    private List<List<Vector2Int>> meltedTilesPositions2 = new List<List<Vector2Int>>();
    private List<List<Vector2Int>> meltedTilesPositions3 = new List<List<Vector2Int>>();
    private List<List<Vector2Int>> meltedTilesPositions4 = new List<List<Vector2Int>>();
    [PunRPC]
    void MergeValidpers(List<Tiles> validMeltedTiless, int playerQue, List<Vector2Int> positions)
    {
        switch (playerQue)
        {
            case 1:
                meltedTiles1.Add(validMeltedTiless);
                meltedTilesPositions1.Add(positions);
                break;
            case 2:
                meltedTiles2.Add(validMeltedTiless);
                meltedTilesPositions2.Add(positions);
                break;
            case 3:
                meltedTiles3.Add(validMeltedTiless);
                meltedTilesPositions3.Add(positions);
                break;
            case 4:
                meltedTiles4.Add(validMeltedTiless);
                meltedTilesPositions4.Add(positions);
                break;
        }
    }
    [PunRPC]
    public void DeactivatePlayerTile(int playerQue, int tileIndex)
    { // Oyuncunun taş listesini al
        List<Tiles> playerTiless = new List<Tiles>();
        switch (playerQue)
        {
            case 1:
                playerTiless = playerTiles1;
                break;
            case 2:
                playerTiless = playerTiles2;
                break;
            case 3:
                playerTiless = playerTiles3;
                break;
            case 4:
                playerTiless = playerTiles4;
                break;
        }

        // Eğer indeks geçerli ise
        if (tileIndex >= 0 && tileIndex < playerTiless.Count)
        {
            Tiles tileToDeactivate = playerTiless[tileIndex];

            // TileUI bileşenini bulmak için tüm TileUI nesnelerini kontrol et
            foreach (Transform placeholder in playerTileContainer)
            {
                if (placeholder.childCount > 0)
                {
                    TileUI tileUI = placeholder.GetChild(0).GetComponent<TileUI>();
                    if (tileUI != null && tileUI.tileDataInfo == tileToDeactivate)
                    {
                        scoreManager.meldedTiles.Add(tileToDeactivate);
                        placeholder.GetChild(0).gameObject.SetActive(false); // GameObject'i devre dışı bırak

                        Debug.Log($"Tile {tileToDeactivate.color} {tileToDeactivate.number} devre dışı bırakıldı.");
                        return; // İlk eşleşmeyi bulduktan sonra döngüden çık
                    }
                }
            }
        }

        else
        {
            Debug.LogWarning("Geçersiz taş indeksi: " + tileIndex);
        }

    }


    [PunRPC]
    public void MeldTiles(int playerNumber, int tileIndex)
    {
        switch (playerNumber)
        {
            case 1:
                InstatiateMeldTiles(playerNumber);
                playerTiles1.RemoveAt(tileIndex);

                break;
            case 2:

                InstatiateMeldTiles(playerNumber);
                playerTiles2.RemoveAt(tileIndex);

                break;
            case 3:
                InstatiateMeldTiles(playerNumber);
                playerTiles3.RemoveAt(tileIndex);

                break;
            case 4:
                InstatiateMeldTiles(playerNumber);
                playerTiles4.RemoveAt(tileIndex);

                break;
        }
        validMeltedTiles.Clear();
        positions.Clear();

    }

    List<List<Vector2Int>> positions = new List<List<Vector2Int>>();

    void InstatiateMeldTiles(int playerCount)
    {
        Player[] player = PhotonNetwork.PlayerList;
        Player localPlayer = PhotonNetwork.LocalPlayer;
        localPlayer.CustomProperties.TryGetValue("PlayerQue", out object localPlayerQue);
        int localPlayerQueInt = (int)localPlayerQue;
        if (localPlayerQueInt == playerCount) return;


        switch (playerCount)
        {
            case 1:
                validMeltedTiles = meltedTiles1;
                positions = meltedTilesPositions1;
                break;
            case 2:
                validMeltedTiles = meltedTiles2;
                positions = meltedTilesPositions2;
                break;
            case 3:
                validMeltedTiles = meltedTiles3;
                positions = meltedTilesPositions3;
                break;
            case 4:
                validMeltedTiles = meltedTiles4;
                positions = meltedTilesPositions4;
                break;
        }
        // Boyut kontrolü
        for (int i = 0; i < player.Length; i++)
        {
            if (player[i].CustomProperties.TryGetValue("PlayerQue", out object playerQue))
            {
                int playerQueInt = (int)playerQue;

                if (playerQueInt == playerCount)
                {
                    Transform meldTileContainer = GameObject.Find(player[i].NickName + " meld").transform;
                    Transform colorTileMeldContainer = meldTileContainer.GetChild(0);
                    Transform numberTileContainer = meldTileContainer.GetChild(1);
                    Transform pairTileContainer = meldTileContainer.GetChild(2);
                    // Taşları konum bilgileri ile yerleştirin
                    for (int j = 0; j < validMeltedTiles.Count; j++)
                    {
                        var per = validMeltedTiles[j];
                        List<Vector2Int> perPosition = positions[j];

                        if (per.Count != perPosition.Count)
                        {
                            Debug.LogError("Per and perPosition count mismatch!");
                            continue; // Geçersiz durumu atla
                        }

                        if (scoreManager.IsSingleColor(per) && scoreManager.SingleColorCheck(per))
                        {
                            foreach (var tiles in per)
                            {
                                int perIndex = per.IndexOf(tiles);
                                Vector2Int position = perPosition[perIndex];
                                int rowIndex = position.x;
                                int columnIndex = rowIndex * 13 + (tiles.number - 1);
                                if (columnIndex < colorTileMeldContainer.childCount)
                                {
                                    GameObject tileInstanceColor = Instantiate(meldTilePrefab, colorTileMeldContainer.GetChild(columnIndex));
                                    TileUI tileUI = tileInstanceColor.GetComponent<TileUI>();
                                    tileUI.CheckRowColoumn(rowIndex, columnIndex);
                                    if (tileUI != null)
                                    {
                                        tileUI.SetTileData(tiles);
                                    }
                                    else
                                    {
                                        Debug.LogError("TileUI component missing on tilePrefab.");
                                    }
                                }
                            }
                        }
                        if (scoreManager.MultiColorCheck(per))
                        {

                            foreach (var tiles in per)
                            {
                                Vector2Int position = perPosition[per.IndexOf(tiles)];
                                int rowIndex = position.x;
                                int columnIndex = position.y;

                                if (columnIndex < numberTileContainer.childCount)
                                {
                                    // Taşı yerleştir
                                    GameObject tileInstanceColor = Instantiate(meldTilePrefab, numberTileContainer.GetChild(columnIndex));
                                    TileUI tileUI = tileInstanceColor.GetComponent<TileUI>();
                                    tileUI.CheckRowColoumn(rowIndex, columnIndex);
                                    if (tileUI != null)
                                    {
                                        tileUI.SetTileData(tiles);
                                    }
                                    else
                                    {
                                        Debug.LogError("TileUI component missing on tilePrefab.");
                                    }

                                }

                            }
                        }
                        if (scoreManager.IsSingleColor(per) && scoreManager.CheckForDoublePer(per))
                        {
                            foreach (var tiles in per)
                            {
                                Vector2Int position = perPosition[per.IndexOf(tiles)];
                                int rowIndex = position.x;
                                int columnIndex = position.y;

                                if (columnIndex < pairTileContainer.childCount)
                                {
                                    // Taşı yerleştir
                                    GameObject tileInstancePair = Instantiate(meldTilePrefab, pairTileContainer.GetChild(columnIndex));
                                    TileUI tileUI = tileInstancePair.GetComponent<TileUI>();
                                    tileUI.CheckRowColoumn(rowIndex, columnIndex);
                                    if (tileUI != null)
                                    {
                                        tileUI.SetTileData(tiles);
                                    }
                                    else
                                    {
                                        Debug.LogError("TileUI component missing on tilePrefab.");
                                    }

                                }
                            }
                        }

                    }
                }
            }

        }
    }


    #endregion
    #endregion
}