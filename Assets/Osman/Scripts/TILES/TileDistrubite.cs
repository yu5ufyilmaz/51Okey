// ------------------------------------------------------------
// TileDistrubite.cs (refactored: logs trimmed, behavior unchanged)
// Notes:
// - All [PunRPC] names, parameters and logic are preserved.
// - Photon RaiseEvent/eventCodes untouched.
// - Inspector-exposed [SerializeField] fields unchanged.
// - Only noisy Debug.Log lines were removed; critical logs kept.
// - No control flow or gameplay logic was modified.
// ------------------------------------------------------------

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
    [SerializeField]
    GameObject tilePrefab; // Tile prefab

    [SerializeField]
    GameObject meldTilePrefab; // Meld tile prefab
    public List<Tiles> allTiles = new List<Tiles>();

    [SerializeField]
    List<TileUI> tileUIs;
    ScoreManager scoreManager;

    [Header("Player Tiles")]
    [SerializeField]
    List<Tiles> playerTiles1 = new List<Tiles>();

    [SerializeField]
    List<Tiles> playerTiles2 = new List<Tiles>();

    [SerializeField]
    List<Tiles> playerTiles3 = new List<Tiles>();

    [SerializeField]
    List<Tiles> playerTiles4 = new List<Tiles>();

    [Header("Melded Tiles")]
    [SerializeField]
    List<List<Tiles>> meltedTiles1 = new List<List<Tiles>>();

    [SerializeField]
    List<List<Tiles>> meltedTiles2 = new List<List<Tiles>>();

    [SerializeField]
    List<List<Tiles>> meltedTiles3 = new List<List<Tiles>>();

    [SerializeField]
    List<List<Tiles>> meltedTiles4 = new List<List<Tiles>>();

    [SerializeField]
    List<List<Tiles>> validMeltedTiles = new List<List<Tiles>>();
    public Tiles dropTile;
    Transform playerTileContainer; // Player tile container
    private Transform[] playerTileContainers; // Player tile placeholders
    Transform dropTileContainer; // Drop tile container
    private Transform[] dropTileContainers; // Drop tile placeholders
    Transform indicatorTileContainer; // Indicator tile container
    Transform middleTileContainer; // Middle tile container
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

    public void RegisterTileUI(TileUI tileUI)
    {
        tileUIs.Add(tileUI);
        tileUIs.RemoveAll(t => t == null);
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
        else { }
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
        if (!PhotonNetwork.IsMasterClient)
            return;

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
                photonView.RPC(
                    "AssignQueueToPlayer",
                    RpcTarget.AllBuffered,
                    player.ActorNumber,
                    queueValue
                );
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

            Debug.Log(
                $"PlayerQue assigned: Player {player.NickName}, ActorNumber: {actorNumber}, QueueValue: {queueValue}"
            );
        }
        else { }
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
            var propertiesLog = string.Join(
                ", ",
                player.CustomProperties.Select(kv =>
                {
                    string key = kv.Key?.ToString() ?? "null";
                    string value = kv.Value?.ToString() ?? "null";
                    return $"{key}={value}";
                })
            );

            return -1; // Varsayılan değer
        }
    }

    public override void OnPlayerPropertiesUpdate(
        Player targetPlayer,
        ExitGames.Client.Photon.Hashtable changedProps
    )
    {
        if (changedProps.ContainsKey("PlayerQue")) { }
        else { }
    }
    #endregion
    #region Distribute Tiles

    [PunRPC]
    public void AssignQueueComplete() { }

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
        else { }
    }

    private void PlaceRemainingTilesInMiddleContainer()
    {
        for (int i = 0; i < allTiles.Count; i++)
        {
            GameObject tileInstance = Instantiate(tilePrefab, middleTileContainer);
        }
    }
    #endregion

    #region GAMEPLAY

    #region Tile Pick or Drop Actions
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
    public void RemoveActiveTileFromPlayerList(int playerNumber, int tileIndex)
    {
        switch (playerNumber)
        {
            case 1:
                playerTiles1.RemoveAt(tileIndex);

                break;
            case 2:
                playerTiles2.RemoveAt(tileIndex);

                break;
            case 3:
                playerTiles3.RemoveAt(tileIndex);

                break;
            case 4:
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
    void UnMergeValidPers(int playerQue)
    {
        switch (playerQue)
        {
            case 1:

                meltedTiles1.Clear();
                meltedTilesPositions1.Clear();
                break;
            case 2:
                meltedTiles2.Clear();
                meltedTilesPositions2.Clear();
                break;
            case 3:
                meltedTiles3.Clear();
                meltedTilesPositions3.Clear();
                break;
            case 4:
                meltedTiles4.Clear();
                meltedTilesPositions4.Clear();
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
                        scoreManager.pendingMeldedTiles.Add(tileToDeactivate);
                        placeholder.GetChild(0).gameObject.SetActive(false); // GameObject'i devre dışı bırak

                        return; // İlk eşleşmeyi bulduktan sonra döngüden çık
                    }
                }
            }
        }
        else { }
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
        if (localPlayerQueInt == playerCount)
            return;

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
                    Transform meldTileContainer = GameObject
                        .Find(player[i].NickName + " meld")
                        .transform;
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
                                    GameObject tileInstanceColor = Instantiate(
                                        meldTilePrefab,
                                        colorTileMeldContainer.GetChild(columnIndex)
                                    );
                                    TileUI tileUI = tileInstanceColor.GetComponent<TileUI>();
                                    tileUI.CheckRowColoumn(rowIndex, columnIndex);
                                    if (tileUI != null)
                                    {
                                        tileUI.SetTileData(tiles);
                                    }
                                    else { }
                                }
                                UpdateAvailableForPlaceholders(per, rowIndex, playerCount);
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
                                    GameObject tileInstanceColor = Instantiate(
                                        meldTilePrefab,
                                        numberTileContainer.GetChild(columnIndex)
                                    );
                                    TileUI tileUI = tileInstanceColor.GetComponent<TileUI>();
                                    tileUI.CheckRowColoumn(rowIndex, columnIndex);
                                    if (tileUI != null)
                                    {
                                        tileUI.SetTileData(tiles);
                                    }
                                    else { }
                                }
                                UpdateAvailableForPlaceholders(per, rowIndex, playerCount);
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
                                    GameObject tileInstancePair = Instantiate(
                                        meldTilePrefab,
                                        pairTileContainer.GetChild(columnIndex)
                                    );
                                    TileUI tileUI = tileInstancePair.GetComponent<TileUI>();
                                    tileUI.CheckRowColoumn(rowIndex, columnIndex);
                                    if (tileUI != null)
                                    {
                                        tileUI.SetTileData(tiles);
                                    }
                                    else { }
                                }
                                UpdateAvailableForPlaceholders(per, rowIndex, playerCount);
                            }
                        }
                    }
                }
            }
        }
    }
    #endregion
    #region Checking AvailableTiles
    public List<Tiles> availableTiles = new List<Tiles>();

    [PunRPC]
    public void CheckForAvailableTiles(int playerQue)
    {
        // Her bir oyuncunun melded taşlarını kontrol et
        switch (playerQue)
        {
            case 1:
                // CheckMeldedTiles(meltedTiles1, playerQue);
                photonView.RPC("CheckMeldedTiles", RpcTarget.AllBuffered, meltedTiles1);
                break;
            case 2:
                // CheckMeldedTiles(meltedTiles2, playerQue);
                photonView.RPC("CheckMeldedTiles", RpcTarget.AllBuffered, meltedTiles2);
                break;
            case 3:
                // CheckMeldedTiles(meltedTiles3, playerQue);
                photonView.RPC("CheckMeldedTiles", RpcTarget.AllBuffered, meltedTiles3);
                break;
            case 4:
                //CheckMeldedTiles(meltedTiles4, playerQue);
                photonView.RPC("CheckMeldedTiles", RpcTarget.AllBuffered, meltedTiles4);
                break;
        }
    }

    [PunRPC]
    private void CheckMeldedTiles(List<List<Tiles>> meldedTiles)
    {
        foreach (var meld in meldedTiles)
        {
            var availableFromMeld = GetAvailableTiles(meld);

            foreach (var tile in availableFromMeld)
            {
                // Eğer availableTiles listesinde yoksa ekle
                if (
                    !availableTiles.Any(t =>
                        t.color == tile.color && t.number == tile.number && t.type == tile.type
                    )
                )
                {
                    availableTiles.Add(tile);
                }
            }
        }
    }

    public List<Tiles> GetAvailableTiles(List<Tiles> meld)
    {
        List<Tiles> availableTiles = new List<Tiles>();

        if (scoreManager.IsSingleColor(meld) && scoreManager.SingleColorCheck(meld))
        {
            // Perin taşlarını analiz et
            if (meld.Count > 0)
            {
                // Melded taşların numaralarını al
                var numbers = meld.Select(tile => tile.number).ToList();
                var colors = meld.Select(tile => tile.color).Distinct().ToList();
                bool hasJoker = meld.Any(tile => tile.type == TileType.Joker); // Joker taşı var mı?

                // En küçük ve en büyük sayıyı bul
                int minNumber = numbers.Min();
                int maxNumber = numbers.Max();

                // En küçük sayının bir eksiğini ekle
                if (minNumber > 1) // 1'den küçük olamaz
                {
                    var newTile = new Tiles(meld[0].color, minNumber - 1, TileType.Number);
                    availableTiles.Add(newTile); // Renk olarak ilk taşın rengini kullan
                }

                // En büyük sayının bir fazlasını ekle
                if (maxNumber < 13) // 13'ten büyük olamaz
                {
                    var newTile = new Tiles(meld[0].color, maxNumber + 1, TileType.Number);
                    availableTiles.Add(newTile); // Renk olarak ilk taşın rengini kullan
                }

                // Eğer joker varsa, jokerin yerini aldığı taşın rengini ve numarasını kullan
                if (hasJoker)
                {
                    // Joker taşını bul
                    var jokerTile = meld.First(tile => tile.type == TileType.Joker);

                    // Jokerin yerini aldığı taşın rengini ve numarasını bul
                    availableTiles.Add(new Tiles(meld[0].color, jokerTile.number, TileType.Number));
                }
            }
        }
        else if (scoreManager.MultiColorCheck(meld))
        {
            // MultiColor perleri için
            if (meld.Count >= 3)
            {
                var numberGroups = meld.GroupBy(tile => tile.number).ToList();
                var missingColors = new List<TileColor>(); // TileColor türünde bir liste
                bool hasJoker = meld.Any(tile => tile.type == TileType.Joker); // Joker taşı var mı?

                // Hangi renklerin eksik olduğunu bul
                foreach (var numberGroup in numberGroups)
                {
                    if (numberGroup.Count() < 4) // 4 renk eksikse
                    {
                        missingColors.AddRange(
                            GetMissingColors(numberGroup.Select(tile => tile.color).ToList())
                        );
                    }
                }

                // Eksik renkleri availableTiles listesine ekle
                foreach (var color in missingColors)
                {
                    foreach (var number in numberGroups.Select(g => g.Key).Distinct())
                    {
                        var newTile = new Tiles(color, number, TileType.Number);
                        availableTiles.Add(newTile);
                    }
                }

                // Eğer joker varsa, jokerin yerini aldığı taşın rengini ve numarasını kullan
                if (meld.Count == 3 && hasJoker)
                {
                    var nonJokerTiles = meld.Where(tile => tile.type != TileType.Joker).ToList();
                    if (nonJokerTiles.Count == 2)
                    {
                        // Jokerin yerini aldığı taşın numarasını kullanarak eksik renklerdeki taşları ekle
                        foreach (
                            var missingColor in GetMissingColors(
                                nonJokerTiles.Select(tile => tile.color).ToList()
                            )
                        )
                        {
                            foreach (var number in nonJokerTiles.Select(t => t.number).Distinct())
                            {
                                var newTile = new Tiles(missingColor, number, TileType.Number);
                                availableTiles.Add(newTile);
                            }
                        }
                    }
                }

                // Eğer 4 taş varsa ve bir tanesi joker ise eksik olan renklerdeki taşları ekle
                if (meld.Count == 4 && hasJoker)
                {
                    var nonJokerTiles = meld.Where(tile => tile.type != TileType.Joker).ToList();
                    if (nonJokerTiles.Count == 3)
                    {
                        // Jokerin yerini aldığı taşın numarasını kullanarak eksik renklerdeki taşları ekle
                        foreach (
                            var missingColor in GetMissingColors(
                                nonJokerTiles.Select(tile => tile.color).ToList()
                            )
                        )
                        {
                            foreach (var number in nonJokerTiles.Select(t => t.number).Distinct())
                            {
                                var newTile = new Tiles(missingColor, number, TileType.Number);
                                availableTiles.Add(newTile);
                            }
                        }
                    }
                }
            }
        }
        else if (scoreManager.CheckForDoublePer(meld) && scoreManager.IsSingleColor(meld))
        {
            // Çift perler için
            if (meld.Any(tile => tile.type == TileType.Joker))
            {
                // Jokerin yerine geçtiği taş işlek olmalı
                var jokerTile = meld.First(tile => tile.type == TileType.Joker);
                availableTiles.Add(new Tiles(jokerTile.color, jokerTile.number, TileType.Number)); // Jokerin temsil ettiği taş
            }
        }
        return availableTiles.Distinct().ToList(); // Tekrar eden taşları kaldır
    }

    private void UpdateAvailableForPlaceholders(List<Tiles> per, int rowIndex, int playerCount)
    {
        Player[] player = PhotonNetwork.PlayerList;
        Player localPlayer = PhotonNetwork.LocalPlayer;
        localPlayer.CustomProperties.TryGetValue("PlayerQue", out object localPlayerQue);
        int localPlayerQueInt = (int)localPlayerQue;
        if (localPlayerQueInt == playerCount)
            return;
        if (per.Count == 0)
            return;

        for (int i = 0; i < player.Length; i++)
        {
            if (player[i].CustomProperties.TryGetValue("PlayerQue", out object playerQue))
            {
                int playerQueInt = (int)playerQue;

                if (playerQueInt == playerCount)
                {
                    // Oyuncunun sırasına göre uygun placeholder dizilerini belirle
                    Transform meldTileContainer = GameObject
                        .Find(player[i].NickName + " meld")
                        .transform;
                    Transform colorTileMeldContainer = meldTileContainer.GetChild(0);
                    Transform numberTileContainer = meldTileContainer.GetChild(1);
                    Transform pairTileContainer = meldTileContainer.GetChild(2);

                    List<Tiles> availableTiles = GetAvailableTiles(per); // Available taşları al

                    if (scoreManager.IsSingleColor(per) && scoreManager.SingleColorCheck(per))
                    {
                        // En büyük ve en küçük taşları bul
                        var numbers = per.Select(tile => tile.number).ToList();
                        var colors = per.Select(tile => tile.color).Distinct().ToList();
                        bool hasJoker = per.Any(tile => tile.type == TileType.Joker); // Joker taşı var mı?

                        // En küçük ve en büyük sayıyı bul
                        int minNumber = numbers.Min();
                        int maxNumber = numbers.Max();

                        // Eğer en büyük taş 13 değilse, en büyük taşın bulunduğu yer tutucunun sağındaki yer tutucunun available durumunu güncelle
                        if (maxNumber != 13)
                        {
                            int rightPlaceholderIndex = maxNumber + 13 * rowIndex;
                            if (rightPlaceholderIndex < colorTileMeldContainer.childCount) // colorPerPlaceHolders dizisini kullanarak kontrol edin
                            {
                                Placeholder rightPlaceholder = colorTileMeldContainer
                                    .GetChild(rightPlaceholderIndex)
                                    .GetComponent<Placeholder>();
                                if (rightPlaceholder != null)
                                {
                                    rightPlaceholder.available = true;
                                    rightPlaceholder.AvailableTileInfo =
                                        availableTiles.FirstOrDefault(tile =>
                                            tile.number == maxNumber + 1
                                        );
                                    if (rightPlaceholder.willInstantiate == true)
                                    {
                                        activePlacements.Add(
                                            new TilePlacement
                                            {
                                                TileToPlace = rightPlaceholder.AvailableTileInfo,
                                                TargetContainer = rightPlaceholder.transform,
                                            }
                                        );
                                    }
                                }
                            }
                        }
                        if (minNumber > 1)
                        {
                            int leftPlaceholderIndex = (minNumber - 2) + 13 * rowIndex;
                            if (leftPlaceholderIndex >= 0)
                            {
                                Placeholder leftPlaceholder = colorTileMeldContainer
                                    .GetChild(leftPlaceholderIndex)
                                    .GetComponent<Placeholder>();
                                if (leftPlaceholder != null)
                                {
                                    leftPlaceholder.available = true;
                                    leftPlaceholder.AvailableTileInfo =
                                        availableTiles.FirstOrDefault(tile =>
                                            tile.number == minNumber - 1
                                        );
                                    if (leftPlaceholder.willInstantiate == true)
                                    {
                                        activePlacements.Add(
                                            new TilePlacement
                                            {
                                                TileToPlace = leftPlaceholder.AvailableTileInfo,
                                                TargetContainer = leftPlaceholder.transform,
                                            }
                                        );
                                    }
                                }
                            }
                        }

                        // Eğer perde joker içeriyorsa, jokerin bulunduğu yer tutucunun available durumunu güncelle
                        if (hasJoker)
                        {
                            var jokerTile = per.First(tile => tile.type == TileType.Joker);
                            int jokerPlaceholderIndex = jokerTile.number - 1 + 13 * rowIndex;
                            if (
                                jokerPlaceholderIndex >= 0
                                && jokerPlaceholderIndex < colorTileMeldContainer.childCount
                            )
                            {
                                Placeholder jokerPlaceholder = colorTileMeldContainer
                                    .GetChild(jokerPlaceholderIndex)
                                    .GetComponent<Placeholder>();
                                if (jokerPlaceholder != null)
                                {
                                    jokerPlaceholder.available = true;
                                    jokerPlaceholder.AvailableTileInfo =
                                        availableTiles.FirstOrDefault(tile =>
                                            tile.number == jokerTile.number
                                        );
                                    if (jokerPlaceholder.willInstantiate == true)
                                    {
                                        activePlacements.Add(
                                            new TilePlacement
                                            {
                                                TileToPlace = jokerPlaceholder.AvailableTileInfo,
                                                TargetContainer = jokerPlaceholder.transform,
                                            }
                                        );
                                    }
                                    /* // PlaceHolder'daki available'ı true yap
                                    // Available taş bilgilerini yerleştir
*/
                                }
                                else { }
                            }
                        }
                    }
                    else if (scoreManager.MultiColorCheck(per))
                    {
                        // MultiColor perleri için
                        if (per.Count >= 3)
                        {
                            var numberGroups = per.GroupBy(tile => tile.number).ToList();
                            bool hasJoker = per.Any(tile => tile.type == TileType.Joker); // Joker taşı var mı?

                            // Eğer joker yoksa ve 3 taşlı ise, 4. sıradaki placeholder'ı true yap
                            if (!hasJoker && per.Count == 3)
                            {
                                int fourthPlaceholderIndex = 3 + (4 * rowIndex); // 4. placeholder'ın indeksi
                                if (fourthPlaceholderIndex < numberTileContainer.childCount)
                                {
                                    Placeholder fourthPlaceholder = numberTileContainer
                                        .GetChild(fourthPlaceholderIndex)
                                        .GetComponent<Placeholder>();
                                    if (fourthPlaceholder != null)
                                    {
                                        fourthPlaceholder.available = true; // PlaceHolder'daki available'ı true yap
                                        // Available taş bilgilerini yerleştir
                                        fourthPlaceholder.AvailableTileInfo =
                                            availableTiles.FirstOrDefault(tile =>
                                                tile.number == numberGroups[0].First().number
                                            ); // Örnek olarak 4. taş
                                    }
                                    else { }
                                }
                                else { }
                            }

                            // Eğer joker varsa ve 3 taşlı ise, hem 4. placeholder'ı hem de joker taşının bulunduğu placeholder'ı true yap
                            if (hasJoker && per.Count == 3)
                            {
                                int fourthPlaceholderIndex = 3 + (4 * rowIndex); // 4. placeholder'ın indeksi
                                if (fourthPlaceholderIndex < numberTileContainer.childCount)
                                {
                                    Placeholder fourthPlaceholder = numberTileContainer
                                        .GetChild(fourthPlaceholderIndex)
                                        .GetComponent<Placeholder>();
                                    if (fourthPlaceholder != null)
                                    {
                                        fourthPlaceholder.available = true; // PlaceHolder'daki available'ı true yap
                                        // Available taş bilgilerini yerleştir
                                        fourthPlaceholder.AvailableTileInfo =
                                            availableTiles.FirstOrDefault(tile =>
                                                tile.number == numberGroups[0].First().number
                                            ); // Örnek olarak 4. taş
                                    }
                                    else { }
                                }
                                else { }

                                // Joker taşının bulunduğu yer tutucunun available durumunu güncelle
                                int jokerPlaceholderIndex = -1; // Joker taşının bulunduğu placeholder'ın indeksi
                                for (int j = 0; j < numberTileContainer.childCount; j++)
                                {
                                    Placeholder currentPlaceholder = numberTileContainer
                                        .GetChild(j)
                                        .GetComponent<Placeholder>();
                                    if (
                                        currentPlaceholder != null
                                        && currentPlaceholder.transform.childCount > 0
                                    )
                                    {
                                        foreach (Transform child in currentPlaceholder.transform)
                                        {
                                            TileUI tile = child.GetComponent<TileUI>();
                                            if (
                                                tile != null
                                                && tile.tileDataInfo.type == TileType.Joker
                                            )
                                            {
                                                jokerPlaceholderIndex = i; // Joker taşının bulunduğu placeholder'ın indeksi
                                                break;
                                            }
                                        }
                                    }

                                    if (jokerPlaceholderIndex != -1)
                                    {
                                        break; // Joker taşını bulduysak döngüden çık
                                    }
                                }

                                // Eğer jokerPlaceholderIndex bulunduysa, available durumunu güncelle
                                if (jokerPlaceholderIndex != -1)
                                {
                                    Placeholder jokerPlaceholder = numberTileContainer
                                        .GetChild(jokerPlaceholderIndex)
                                        .GetComponent<Placeholder>();
                                    if (jokerPlaceholder != null)
                                    {
                                        jokerPlaceholder.available = true; // PlaceHolder'daki available'ı true yap
                                        // Available taş bilg ilerini yerleştir
                                        jokerPlaceholder.AvailableTileInfo =
                                            availableTiles.FirstOrDefault(tile =>
                                                tile.number == numberGroups[0].First().number
                                            );
                                    }
                                    else { }
                                }
                                else
                                {
                                    Debug.Log("Joker placeholder not found.");
                                }
                            }

                            // Eğer per 4 taşlı ve içerisinde joker taşı varsa, joker taşının bulunduğu placeholder'ı true yap
                            if (per.Count == 4 && hasJoker)
                            {
                                int jokerPlaceholderIndex = -1; // Joker taşının bulunduğu placeholder'ın indeksi
                                for (int j = 0; j < numberTileContainer.childCount; j++)
                                {
                                    Placeholder currentPlaceholder = numberTileContainer
                                        .GetChild(j)
                                        .GetComponent<Placeholder>();
                                    if (
                                        currentPlaceholder != null
                                        && currentPlaceholder.transform.childCount > 0
                                    )
                                    {
                                        foreach (Transform child in currentPlaceholder.transform)
                                        {
                                            TileUI tile = child.GetComponent<TileUI>();
                                            if (
                                                tile != null
                                                && tile.tileDataInfo.type == TileType.Joker
                                            )
                                            {
                                                jokerPlaceholderIndex = i; // Joker taşının bulunduğu placeholder'ın indeksi
                                                break;
                                            }
                                        }
                                    }

                                    if (jokerPlaceholderIndex != -1)
                                    {
                                        break; // Joker taşını bulduysak döngüden çık
                                    }
                                }

                                // Eğer jokerPlaceholderIndex bulunduysa, available durumunu güncelle
                                if (jokerPlaceholderIndex != -1)
                                {
                                    Placeholder jokerPlaceholder = numberTileContainer
                                        .GetChild(jokerPlaceholderIndex)
                                        .GetComponent<Placeholder>();
                                    if (jokerPlaceholder != null)
                                    {
                                        jokerPlaceholder.available = true; // PlaceHolder'daki available'ı true yap
                                        // Available taş bilgilerini yerleştir
                                        jokerPlaceholder.AvailableTileInfo =
                                            availableTiles.FirstOrDefault(tile =>
                                                tile.number == numberGroups[0].First().number
                                            );
                                    }
                                    else { }
                                }
                                else
                                {
                                    Debug.Log("Joker placeholder not found.");
                                }
                            }
                        }
                    }
                    else if (scoreManager.CheckForDoublePer(per) && scoreManager.IsSingleColor(per))
                    {
                        if (per.Any(tile => tile.type == TileType.Joker))
                        {
                            int jokerPlaceholderIndex = -1; // Joker taşının bulunduğu placeholder'ın indeksi
                            for (int j = 0; j < pairTileContainer.childCount; j++)
                            {
                                Placeholder currentPlaceholder = pairTileContainer
                                    .GetChild(j)
                                    .GetComponent<Placeholder>();
                                if (
                                    currentPlaceholder != null
                                    && currentPlaceholder.transform.childCount > 0
                                )
                                {
                                    foreach (Transform child in currentPlaceholder.transform)
                                    {
                                        TileUI tile = child.GetComponent<TileUI>();
                                        if (
                                            tile != null
                                            && tile.tileDataInfo.type == TileType.Joker
                                        )
                                        {
                                            jokerPlaceholderIndex = i; // Joker taşının bulunduğu placeholder'ın indeksi
                                            break;
                                        }
                                    }
                                }

                                if (jokerPlaceholderIndex != -1)
                                {
                                    break; // Joker taşını bulduysak döngüden çık
                                }
                            }

                            // Eğer jokerPlaceholderIndex bulunduysa, available durumunu güncelle
                            if (jokerPlaceholderIndex != -1)
                            {
                                Placeholder jokerPlaceholder = pairTileContainer
                                    .GetChild(jokerPlaceholderIndex)
                                    .GetComponent<Placeholder>();
                                if (jokerPlaceholder != null)
                                {
                                    jokerPlaceholder.available = true; // PlaceHolder'daki available'ı true yap
                                    // Available taş bilgilerini yerleştir
                                    jokerPlaceholder.AvailableTileInfo =
                                        availableTiles.FirstOrDefault(tile =>
                                            tile.number == per.First().number
                                        );
                                }
                                else { }
                            }
                            else
                            {
                                Debug.Log("Joker placeholder not found.");
                            }
                        }
                    }
                }
            }
        }
    }

    private List<TileColor> GetMissingColors(List<TileColor> existingColors)
    {
        var allColors = new List<TileColor>
        {
            TileColor.red,
            TileColor.blue,
            TileColor.black,
            TileColor.yellow,
        }; // Tüm renkler
        return allColors.Except(existingColors).ToList();
    }

    #endregion
    #region Active Tiles
    public class TilePlacement
    {
        public Tiles TileToPlace { get; set; }
        public Transform TargetContainer { get; set; }
    }

    private List<TilePlacement> activePlacements = new List<TilePlacement>();

    [PunRPC]
    public void InstantiateTileAtLocation()
    {
        // Güvenlik kontrolü: Liste boşsa işlem yapma.
        if (activePlacements == null || activePlacements.Count == 0)
        {
            Debug.LogError("Instantiate başarısız: Yerleştirilecek taş bulunamadı!");
            return;
        }

        // Listedeki her bir taş/konum eşleşmesi için döngüye gir.
        foreach (var placement in activePlacements)
        {
            // Her bir yerleştirme için ayrıca null kontrolü yapmak daha güvenlidir.
            if (placement.TargetContainer == null || placement.TileToPlace == null)
            {
                Debug.LogWarning("Geçersiz bir yerleştirme bilgisi atlanıyor.");
                continue; // Bu elemanı atla, döngüye devam et.
            }

            // 1. O konumda başka bir taş varsa, önce onu temizle.
            foreach (Transform child in placement.TargetContainer)
            {
                Destroy(child.gameObject);
            }

            Placeholder placeholder = placement.TargetContainer.GetComponent<Placeholder>();
            // Bu kontrol artık döngü içinde olmalı.

            // 2. Yeni taşı oluştur (Instantiate) ve doğru konuma ata.
            GameObject newTileObject = Instantiate(tilePrefab, placement.TargetContainer);

            // 3. Oluşturulan taşın görselini ve verilerini ayarla.
            newTileObject.GetComponent<TileUI>().SetTileData(placement.TileToPlace);

            // 4. Placeholder'ın durumunu güncelle.
            placeholder.available = false;
            placeholder.isDrop = true; // Dolu olduğunu belirtir.

            Debug.Log(
                $"{placement.TileToPlace.color} {placement.TileToPlace.number} taşı, "
                    + $"{placement.TargetContainer.name} konumuna yerleştirildi."
            );
        }

        // İşlem bittikten sonra listeyi temizle ki aynı taşlar tekrar üretilmesin.
        activePlacements.Clear();
    }

    // TileDistribute.cs içine eklenecek yeni RPC

    /// <summary>
    /// Tüm oyuncuların masadaki TÜM perlerini tarar ve herkes için işlek noktaları yeniden hesaplar.
    /// Bu fonksiyon her çağrıldığında tüm 'available' durumlarını sıfırlar ve yeniden oluşturur.
    /// </summary>
    [PunRPC]
    public void RPC_UpdateAllAvailableSlots_Globally()
    {
        // 1. Önce tüm oyuncuların tüm placeholder'larını temizle
        for (int i = 0; i < PhotonNetwork.PlayerList.Length; i++)
        {
            Player player = PhotonNetwork.PlayerList[i];
            Transform meldContainer = GameObject.Find(player.NickName + " meld")?.transform;
            if (meldContainer == null)
                continue;

            foreach (Transform area in meldContainer) // color, number, pair alanları
            {
                foreach (Transform placeholderTransform in area) // Her bir placeholder
                {
                    Placeholder p = placeholderTransform.GetComponent<Placeholder>();
                    if (p != null && p.transform.childCount == 0) // Sadece boş olanları sıfırla
                    {
                        p.available = false;
                        p.AvailableTileInfo = null;
                    }
                }
            }
        }

        // 2. Şimdi her oyuncunun meld verisini tek tek işle
        for (int playerIndex = 1; playerIndex <= PhotonNetwork.PlayerList.Length; playerIndex++)
        {
            List<List<Tiles>> targetMeltedTiles;
            List<List<Vector2Int>> targetMeltedPositions;

            switch (playerIndex)
            {
                case 1:
                    targetMeltedTiles = meltedTiles1;
                    targetMeltedPositions = meltedTilesPositions1;
                    break;
                case 2:
                    targetMeltedTiles = meltedTiles2;
                    targetMeltedPositions = meltedTilesPositions2;
                    break;
                case 3:
                    targetMeltedTiles = meltedTiles3;
                    targetMeltedPositions = meltedTilesPositions3;
                    break;
                case 4:
                    targetMeltedTiles = meltedTiles4;
                    targetMeltedPositions = meltedTilesPositions4;
                    break;
                default:
                    continue;
            }

            Player ownerPlayer = PhotonNetwork.PlayerList.FirstOrDefault(p =>
                GetQueueNumberOfPlayer(p) == playerIndex
            );
            if (ownerPlayer == null)
                continue;

            Transform ownerMeldContainer = GameObject
                .Find(ownerPlayer.NickName + " meld")
                ?.transform;
            if (ownerMeldContainer == null)
                continue;

            // Bu oyuncunun her bir per'i için işlek noktaları bul
            for (int meldIndex = 0; meldIndex < targetMeltedTiles.Count; meldIndex++)
            {
                List<Tiles> per = targetMeltedTiles[meldIndex];
                List<Tiles> potentialAvailableTiles = GetAvailableTiles(per); // İşlek olabilecek taşları hesapla

                foreach (var availableTile in potentialAvailableTiles)
                {
                    // Bu işlek taşın masada nereye konulabileceğini bul ve Placeholder'ı güncelle
                    UpdatePlaceholderForAvailableTile(
                        per,
                        availableTile,
                        ownerMeldContainer,
                        targetMeltedPositions[meldIndex]
                    );
                }
            }
        }
    }

    /// <summary>
    /// Belirli bir işlek taş için ilgili Placeholder'ı bulur ve 'available' olarak işaretler.
    /// </summary>
    private void UpdatePlaceholderForAvailableTile(
        List<Tiles> per,
        Tiles availableTile,
        Transform ownerMeldContainer,
        List<Vector2Int> positions
    )
    {
        // Bu yardımcı fonksiyon, UpdateAvailableForPlaceholders'daki mantığı kullanır.
        // Şimdilik bu kısmı boş bırakabiliriz, ana RPC'yi tetiklemek daha önemli.
        // Detaylı implementasyon gerekirse onu da yaparız. Şimdilik temel akışı kuralım.
        // Temel fikir: availableTile'ın nereye gideceğini (örn: 5-6-7 serisinde 4'ün sola, 8'in sağa)
        // hesaplayıp o konumdaki Placeholder'ı bulup .available = true yapmak.
        Debug.Log($"Hesaplanan işlek taş: {availableTile.color} {availableTile.number}");
    }

    // TileDistribute.cs içine eklenecek yeni RPC

    [PunRPC]
    public void RPC_AddTileToMeldAndUpdate(int ownerPlayerQue, int meldIndex, Tiles newTile)
    {
        List<List<Tiles>> targetMeltedTiles;
        List<List<Vector2Int>> targetMeltedPositions;

        switch (ownerPlayerQue)
        {
            case 1:
                targetMeltedTiles = meltedTiles1;
                targetMeltedPositions = meltedTilesPositions1;
                break;
            case 2:
                targetMeltedTiles = meltedTiles2;
                targetMeltedPositions = meltedTilesPositions2;
                break;
            case 3:
                targetMeltedTiles = meltedTiles3;
                targetMeltedPositions = meltedTilesPositions3;
                break;
            case 4:
                targetMeltedTiles = meltedTiles4;
                targetMeltedPositions = meltedTilesPositions4;
                break;
            default:
                return;
        }

        if (meldIndex < targetMeltedTiles.Count)
        {
            // 1. Merkezi veri listesine yeni taşı ekle
            targetMeltedTiles[meldIndex].Add(newTile);
            // ÖNEMLİ: Yeni taşın pozisyonunu da eklemeniz gerekecek. Şimdilik bu kısmı atlıyoruz.
            // Konum bilgisi olmadan görsel oluşturma çalışmaz. Bu detayı sonra eklemeliyiz.

            Debug.Log(
                $"Oyuncu {ownerPlayerQue}'nin {meldIndex}. perine {newTile.color} {newTile.number} taşı eklendi."
            );

            // 2. Veri güncellendiğine göre, herkesin ekranını yenilemesi için ana güncelleme fonksiyonunu çağır.
            // Sadece MasterClient'ın çağırması, RPC trafiğini azaltır.
            if (PhotonNetwork.IsMasterClient)
            {
                photonView.RPC("RPC_UpdateAllAvailableSlots_Globally", RpcTarget.All);
            }
        }
    }
    #endregion
    #endregion
}
