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

public class TileDistribute : MonoBehaviourPunCallbacks
{
    [SerializeField] GameObject tilePrefab; // Tile prefab
    [SerializeField] GameObject meldTilePrefab; // Meld tile prefab
    public List<Tiles> allTiles = new List<Tiles>();
    [SerializeField] List<TileUI> tileUIs;
    private TurnManager turnManager;
    ScoreManager scoreManager;

    [Header("Player Tiles")]
    [SerializeField] List<Tiles> playerTiles1 = new List<Tiles>();
    [SerializeField] List<Tiles> playerTiles2 = new List<Tiles>();
    [SerializeField] List<Tiles> playerTiles3 = new List<Tiles>();
    [SerializeField] List<Tiles> playerTiles4 = new List<Tiles>();
    [Header("Melded Tiles")]
    [SerializeField] List<List<Tiles>> meltedTiles1 = new List<List<Tiles>>();
    [SerializeField] List<List<Tiles>> meltedTiles2 = new List<List<Tiles>>();
    [SerializeField] List<List<Tiles>> meltedTiles3 = new List<List<Tiles>>();
    [SerializeField] List<List<Tiles>> meltedTiles4 = new List<List<Tiles>>();
    [SerializeField] List<List<Tiles>> validMeltedTiles = new List<List<Tiles>>();
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
        allTiles.Clear();
    
        // Create two copies of each tile for each color and number
        foreach (TileColor color in Enum.GetValues(typeof(TileColor)))
        {
            for (int i = 1; i <= 13; i++)
            {
                allTiles.Add(new Tiles(color, i, TileType.Number));
                allTiles.Add(new Tiles(color, i, TileType.Number));
            }
        }
    
        // Add joker tiles
        allTiles.Add(new Tiles(TileColor.black, 1, TileType.FakeJoker));
        allTiles.Add(new Tiles(TileColor.black, 2, TileType.FakeJoker));
    
        Debug.Log("Tiles generated: " + allTiles.Count);
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
    
        // Fisher-Yates shuffle algorithm
        for (int i = allTiles.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Tiles temp = allTiles[i];
            allTiles[i] = allTiles[j];
            allTiles[j] = temp;
        }
    
        // Set indicator tile after shuffling
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
        if (allTiles.Count == 0) return;
    
        // Take first tile as indicator
        Tiles indicatorTile = allTiles[0];
        allTiles.RemoveAt(0);
    
        // Calculate joker number
        int jokerNumber = (indicatorTile.number % 13) + 1;
    
        // Update joker tiles
        UpdateJokerTiles(jokerNumber, indicatorTile.color);
    
        // Sync across all clients
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

    private void UpdateJokerTiles(int jokerNumber, TileColor jokerColor)
    {
        foreach (var tile in allTiles)
        {
            // Convert matching tiles to jokers
            if (tile.type == TileType.Number && tile.number == jokerNumber && tile.color == jokerColor)
            {
                tile.type = TileType.Joker;
            }
            else if (tile.type == TileType.FakeJoker)
            {
                // Update fake joker properties
                tile.color = jokerColor;
                tile.number = jokerNumber;
            }
        }
    
        // Assign player queue and sync shuffled tiles
        photonView.RPC("AssignPlayerQueue", RpcTarget.All);
        photonView.RPC("SyncShuffledTiles", RpcTarget.All, allTiles.ToArray());
    }
    #endregion

    #region Assign Player Queue
    [PunRPC]
    public void AssignPlayerQueue()
    {
        // Only master client assigns queue
        if (!PhotonNetwork.IsMasterClient) return;
    
        var players = PhotonNetwork.CurrentRoom.Players.Values.ToList();
    
        // Randomly select first player
        int randomIndex = Random.Range(0, players.Count);
        Player firstPlayer = players[randomIndex];
    
        // Get first player's seat number
        firstPlayer.CustomProperties.TryGetValue("SeatNumber", out object seatNumberValue);
        int firstPlayerSeat = (int)seatNumberValue;
    
        // Assign player queue 1 to first player
        photonView.RPC("AssignQueueToPlayer", RpcTarget.AllBuffered, firstPlayer.ActorNumber, 1);
    
        // Assign queue values to other players in order
        int queueValue = 2;
        int playerCount = players.Count;
    
        for (int i = 1; i < playerCount; i++)
        {
            // Calculate next seat in order
            int nextSeat = (firstPlayerSeat - 1 + i) % playerCount + 1;
        
            // Find player with that seat
            Player nextPlayer = players.FirstOrDefault(p => 
            {
                p.CustomProperties.TryGetValue("SeatNumber", out object seat);
                return (int)seat == nextSeat;
            });
        
            // Assign queue if player found
            if (nextPlayer != null && nextPlayer != firstPlayer)
            {
                photonView.RPC("AssignQueueToPlayer", RpcTarget.AllBuffered, nextPlayer.ActorNumber, queueValue);
                queueValue++;
            }
        }
    
        // Notify completion
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
    Player localPlayer = PhotonNetwork.LocalPlayer;
    int playerQue = GetQueueNumberOfPlayer(localPlayer);
    
    // First player gets 15 tiles, others get 14
    int firstPlayerTiles = 15;
    int otherPlayerTiles = 14;
    
    // Distribute tiles to all players
    for (int playerIndex = 0; playerIndex < 4; playerIndex++)
    {
        int tilesToDistribute = (playerIndex == 0) ? firstPlayerTiles : otherPlayerTiles;
        List<Tiles> targetList = GetPlayerTileList(playerIndex + 1);
        
        for (int tileIndex = 0; tileIndex < tilesToDistribute; tileIndex++)
        {
            if (allTiles.Count == 0)
            {
                Debug.LogWarning("No more tiles left to distribute!");
                return;
            }
            
            Tiles tile = allTiles[0];
            allTiles.RemoveAt(0);
            targetList.Add(tile);
            
            // Instantiate tile for local player
            if (playerIndex + 1 == playerQue)
            {
                InstantiateTiles(tileIndex, tile);
            }
        }
    }
    
    // Place remaining tiles in middle container
    PlaceRemainingTilesInMiddleContainer();
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
    
    private List<Tiles> GetPlayerTileList(int playerNumber)
    {
        switch (playerNumber)
        {
            case 1: return playerTiles1;
            case 2: return playerTiles2;
            case 3: return playerTiles3;
            case 4: return playerTiles4;
            default: return new List<Tiles>();
        }
    }
    
    [PunRPC]
    public void HandleTileAction(string action, int playerNumber, int tileIndex = -1, Tiles tile = null)
    {
        List<Tiles> playerTiles = GetPlayerTileList(playerNumber);
    
        switch (action)
        {
            case "RemoveTile":
                if (tileIndex >= 0 && tileIndex < playerTiles.Count)
                {
                    InstantiateSideTiles(playerNumber, playerTiles[tileIndex]);
                    playerTiles.RemoveAt(tileIndex);
                }
                break;
            
            case "AddFromMiddle":
                if (allTiles.Count > 0)
                {
                    playerTiles.Add(allTiles[0]);
                    allTiles.RemoveAt(0);
                }
                break;
            
            case "AddFromDrop":
                if (dropTile != null)
                {
                    playerTiles.Add(dropTile);
                    DestroySideTiles(playerNumber);
                }
                break;
            
            case "DeactivateTile":
                if (tileIndex >= 0 && tileIndex < playerTiles.Count)
                {
                    DeactivatePlayerTileUI(playerTiles[tileIndex]);
                }
                break;
        }
    }
    private void DeactivatePlayerTileUI(Tiles tile)
    {
        foreach (Transform placeholder in playerTileContainer)
        {
            if (placeholder.childCount > 0)
            {
                TileUI tileUI = placeholder.GetChild(0).GetComponent<TileUI>();
                if (tileUI != null && tileUI.tileDataInfo == tile)
                {
                    scoreManager.meldedTiles.Add(tile);
                    placeholder.GetChild(0).gameObject.SetActive(false);
                    return;
                }
            }
        }
    }

    #endregion


[PunRPC]
public void SyncActiveTilesExact(int playerQue, int[] tileIndices, string[] placeholderPaths)
{
    // Skip for originating client
    if (GetQueueNumberOfPlayer(PhotonNetwork.LocalPlayer) == playerQue)
    {
        Debug.Log("[SyncActiveTilesExact] This is the originating client, skipping");
        return;
    }
    
    Debug.Log($"[SyncActiveTilesExact] Received sync for player {playerQue} with {tileIndices.Length} tiles");
    
    List<Tiles> playerTiles = GetPlayerTileList(playerQue);
    
    // Find all meld containers by name
    Transform[] allContainers = GameObject.FindObjectsOfType<Transform>(true)
        .Where(t => t.name.EndsWith(" meld"))
        .ToArray();
        
    Debug.Log($"[SyncActiveTilesExact] Found {allContainers.Length} meld containers");
    
    // Process each placement
    for (int i = 0; i < tileIndices.Length && i < placeholderPaths.Length; i++)
    {
        int tileIdx = tileIndices[i];
        string path = placeholderPaths[i];
        
        // Validate tile index
        if (tileIdx >= playerTiles.Count)
        {
            Debug.LogError($"[SyncActiveTilesExact] Invalid tile index {tileIdx}");
            continue;
        }
        
        Tiles tile = playerTiles[tileIdx];
        Debug.Log($"[SyncActiveTilesExact] Processing tile {tile.color} {tile.number} at path: {path}");
        
        // Parse path components
        string[] pathParts = path.Split('/');
        if (pathParts.Length < 3)
        {
            Debug.LogError($"[SyncActiveTilesExact] Invalid path format: {path}");
            continue;
        }
        
        string containerName = pathParts[0];
        string containerType = pathParts[1];
        string placeholderName = pathParts[2];
        
        // Find the matching container
        Transform meldContainer = null;
        foreach (Transform container in allContainers)
        {
            if (container.name == containerName)
            {
                meldContainer = container;
                break;
            }
        }
        
        if (meldContainer == null)
        {
            Debug.LogError($"[SyncActiveTilesExact] Could not find container: {containerName}");
            continue;
        }
        
        // Get the correct container type index
        int containerIndex = -1;
        if (containerType.Contains("Color"))
            containerIndex = 0;
        else if (containerType.Contains("Number"))
            containerIndex = 1;
        else if (containerType.Contains("Pair"))
            containerIndex = 2;
            
        if (containerIndex == -1 || containerIndex >= meldContainer.childCount)
        {
            Debug.LogError($"[SyncActiveTilesExact] Invalid container type: {containerType}");
            continue;
        }
        
        Transform typeContainer = meldContainer.GetChild(containerIndex);
        
        // Determine the placeholder index - this is the critical part for fixing right-side placement
        int placeholderIndex = -1;
        
        // For color containers, use the tile number to determine exact position
        Transform placeholder;
        if (containerIndex == 0) // ColorPer container
        {
            // Try to find the placeholder based on the tile's number and row
            // The formula is: row * 13 + (number - 1)
            for (int row = 0; row < 4; row++) // Check up to 4 rows
            {
                int calculatedIndex = row * 13 + (tile.number - 1);
                if (calculatedIndex < typeContainer.childCount)
                {
                    placeholder = typeContainer.GetChild(calculatedIndex);
                    if (placeholder.childCount == 0) // It's empty
                    {
                        placeholderIndex = calculatedIndex;
                        break;
                    }
                }
            }
        }
        
        // If we couldn't determine by tile number or for other container types
        if (placeholderIndex == -1)
        {
            // Extract from path if possible
            if (placeholderName.Contains("(") && placeholderName.Contains(")"))
            {
                string indexStr = placeholderName.Substring(
                    placeholderName.IndexOf('(') + 1,
                    placeholderName.IndexOf(')') - placeholderName.IndexOf('(') - 1
                );
                
                int.TryParse(indexStr, out placeholderIndex);
            }
            
            // If still not found, find first empty placeholder
            if (placeholderIndex == -1 || placeholderIndex >= typeContainer.childCount)
            {
                for (int j = 0; j < typeContainer.childCount; j++)
                {
                    if (typeContainer.GetChild(j).childCount == 0)
                    {
                        placeholderIndex = j;
                        break;
                    }
                }
            }
        }
        
        if (placeholderIndex == -1 || placeholderIndex >= typeContainer.childCount)
        {
            Debug.LogError($"[SyncActiveTilesExact] Could not find valid placeholder");
            continue;
        }
        
        placeholder = typeContainer.GetChild(placeholderIndex);
        
        // Create the tile
        GameObject tileInstance = Instantiate(meldTilePrefab, placeholder);
        TileUI tileUI = tileInstance.GetComponent<TileUI>();
        
        if (tileUI != null)
        {
            tileUI.SetTileData(tile);
            
            // Calculate row and column for organizations
            int row = 0;
            int col = placeholderIndex;
            
            if (containerIndex == 0) // ColorPer
            {
                row = placeholderIndex / 13;
                col = placeholderIndex % 13; 
            }
            else if (containerIndex == 1) // NumberPer
            {
                row = placeholderIndex / 4;
                col = placeholderIndex % 4;
            }
            else if (containerIndex == 2) // PairPer
            {
                row = placeholderIndex / 2;
                col = placeholderIndex % 2;
            }
            
            tileUI.CheckRowColoumn(row, col);
            Debug.Log($"[SyncActiveTilesExact] Successfully placed tile at {containerType}/{placeholderIndex}");
        }
        
        // Mark placeholder as used
        Placeholder ph = placeholder.GetComponent<Placeholder>();
        if (ph != null)
        {
            ph.available = false;
            ph.AvailableTileInfo = null;
        }
    }
}


private int FindPlaceholderIndex(string placeholderName, Transform container)
{
    // First try to find by exact name
    for (int i = 0; i < container.childCount; i++)
    {
        if (container.GetChild(i).name == placeholderName)
        {
            return i;
        }
    }
    
    // If that fails, try to extract index from name (e.g., "PlaceHolder (10)" -> 10)
    if (placeholderName.Contains("(") && placeholderName.Contains(")"))
    {
        int startIdx = placeholderName.IndexOf('(') + 1;
        int endIdx = placeholderName.IndexOf(')');
        if (startIdx < endIdx)
        {
            string indexStr = placeholderName.Substring(startIdx, endIdx - startIdx);
            if (int.TryParse(indexStr, out int result))
            {
                // Validate the index is in bounds
                if (result >= 0 && result < container.childCount)
                {
                    return result;
                }
            }
        }
    }
    
    // If all else fails, try numbered pattern (PlaceHolder_10)
    if (placeholderName.Contains("_"))
    {
        string[] parts = placeholderName.Split('_');
        if (parts.Length > 1 && int.TryParse(parts[1], out int result))
        {
            if (result >= 0 && result < container.childCount)
            {
                return result;
            }
        }
    }
    
    return -1; // Could not determine index
}

private int DetermineRow(Transform container, int placeholderIndex)
{
    // Calculate row based on container type
    string containerName = container.name.ToLower();
    
    if (containerName.Contains("color"))
    {
        return placeholderIndex / 13; // Each row has 13 tiles (1-13)
    }
    else if (containerName.Contains("number"))
    {
        return placeholderIndex / 4; // Each row has 4 tiles (for different colors)
    }
    else if (containerName.Contains("pair"))
    {
        return placeholderIndex / 2; // Each row has 2 tiles
    }
    
    return 0; // Default
}

private int DetermineColumn(Transform container, int placeholderIndex)
{
    // Calculate column based on container type
    string containerName = container.name.ToLower();
    
    if (containerName.Contains("color"))
    {
        return placeholderIndex % 13; // Each row has 13 tiles (1-13)
    }
    else if (containerName.Contains("number"))
    {
        return placeholderIndex % 4; // Each row has 4 tiles (for different colors)
    }
    else if (containerName.Contains("pair"))
    {
        return placeholderIndex % 2; // Each row has 2 tiles
    }
    
    return 0; // Default
}

// Add these properties and methods to TileDistribute.cs

// Add these variables to the class
private int[] storedTileIndices;
private string[] storedContainerPaths;
private int storedPlayerQue;
private bool hasPendingActiveTileSync = false;

// Add this method to store active tile sync data
public void StoreActiveTileSyncData(int playerQue, int[] tileIndices, string[] containerPaths)
{
    storedTileIndices = tileIndices;
    storedContainerPaths = containerPaths;
    storedPlayerQue = playerQue;
    hasPendingActiveTileSync = true;
    
    Debug.Log($"[TileDistribute] Stored sync data for {tileIndices.Length} active tiles from player {playerQue}");
}

// Modify the NextTurnEvents method in TileUI.cs to call this
// This method should be called AFTER a tile is dropped
public void SyncPendingActiveTiles()
{
    if (hasPendingActiveTileSync)
    {
        Debug.Log($"[TileDistribute] Syncing pending active tiles: {storedTileIndices.Length} tiles");
        
        // First deactivate all the stored tiles in player hand for all clients
        for (int i = 0; i < storedTileIndices.Length; i++)
        {
            photonView.RPC("DeactivatePlayerTile", RpcTarget.AllBuffered, 
                storedPlayerQue, storedTileIndices[i]);
        }
        
        // Then send the sync data to place them on the board for others
        photonView.RPC("SyncActiveTilesExact", RpcTarget.AllBuffered,
            storedPlayerQue, 
            storedTileIndices,
            storedContainerPaths);
        
        // Clear pending data
        hasPendingActiveTileSync = false;
        storedTileIndices = null;
        storedContainerPaths = null;
    }
}


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
                InstantiateSideTiles(playerNumber, playerTiles1[tileIndex]);
                playerTiles1.RemoveAt(tileIndex);


                break;
            case 2:
                InstantiateSideTiles(playerNumber, playerTiles2[tileIndex]);
                playerTiles2.RemoveAt(tileIndex);

                break;
            case 3:
                InstantiateSideTiles(playerNumber, playerTiles3[tileIndex]);
                playerTiles3.RemoveAt(tileIndex);

                break;
            case 4:
                InstantiateSideTiles(playerNumber, playerTiles4[tileIndex]);
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

    void InstantiateSideTiles(int playerCount, Tiles tile)
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

    #region MELD_TILES




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
                        scoreManager.meldedTiles.Add(tileToDeactivate);
                        placeholder.GetChild(0).gameObject.SetActive(false); // GameObject'i devre dışı bırak

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
                                UpdateAvailableForPlaceholders(per, rowIndex, playerCount);
                            }
                        }

                    }
                }
            }

        }
    }
    #region Taş İşleme
    public List<Tiles> availableTiles = new List<Tiles>();


    [PunRPC]
    public void CheckForAvailableTiles(int playerQue)
    {
        // Her bir oyuncunun melded taşlarını kontrol et
        switch (playerQue)
        {
            case 1:
                photonView.RPC("CheckMeldedTiles", RpcTarget.AllBuffered, meltedTiles1, playerQue);
                break;
            case 2:
                photonView.RPC("CheckMeldedTiles", RpcTarget.AllBuffered, meltedTiles2, playerQue);
                break;
            case 3:
                photonView.RPC("CheckMeldedTiles", RpcTarget.AllBuffered, meltedTiles3, playerQue);
                break;
            case 4:
                photonView.RPC("CheckMeldedTiles", RpcTarget.AllBuffered, meltedTiles4, playerQue);
                break;
        }
    }

    [PunRPC]
    private void CheckMeldedTiles(List<List<Tiles>> meldedTiles, int playerQue)
    {
        foreach (var meld in meldedTiles)
        {
            var availableFromMeld = GetAvailableTiles(meld, playerQue);

            foreach (var tile in availableFromMeld)
            {
                // Eğer availableTiles listesinde yoksa ekle
                if (!availableTiles.Any(t => t.color == tile.color && t.number == tile.number && t.type == tile.type))
                {
                    availableTiles.Add(tile);

                }
            }
        }
    }

    public List<Tiles> GetAvailableTiles(List<Tiles> meld, int rowIndex)
{
    List<Tiles> availableTiles = new List<Tiles>();
    
    // Handle single color straight melds
    if (scoreManager.IsSingleColor(meld) && scoreManager.SingleColorCheck(meld))
    {
        // Get min/max numbers
        int minNumber = meld.Min(t => t.number);
        int maxNumber = meld.Max(t => t.number);
        TileColor meldColor = meld.First(t => t.type != TileType.Joker).color;
        
        // Add lower number if possible
        if (minNumber > 1)
        {
            availableTiles.Add(new Tiles(meldColor, minNumber - 1, TileType.Number));
        }
        
        // Add higher number if possible
        if (maxNumber < 13)
        {
            availableTiles.Add(new Tiles(meldColor, maxNumber + 1, TileType.Number));
        }
        
        // Add replacements for jokers
        foreach (var joker in meld.Where(t => t.type == TileType.Joker))
        {
            availableTiles.Add(new Tiles(meldColor, joker.number, TileType.Number));
        }
    }
    // Handle multi-color sets
    else if (scoreManager.MultiColorCheck(meld))
    {
        // Get the number and find missing colors
        int setNumber = meld.First(t => t.type != TileType.Joker).number;
        var existingColors = meld.Where(t => t.type != TileType.Joker).Select(t => t.color).ToList();
        var missingColors = GetMissingColors(existingColors);
        
        // Add missing color tiles
        foreach (var color in missingColors)
        {
            availableTiles.Add(new Tiles(color, setNumber, TileType.Number));
        }
    }
    // Handle double pairs
    else if (scoreManager.CheckForDoublePer(meld) && scoreManager.IsSingleColor(meld))
    {
        // Add replacement for joker if present
        var joker = meld.FirstOrDefault(t => t.type == TileType.Joker);
        if (joker != null)
        {
            availableTiles.Add(new Tiles(joker.color, joker.number, TileType.Number));
        }
    }
    
    return availableTiles.Distinct().ToList();
}
    private void UpdateAvailableForPlaceholders(List<Tiles> per, int rowIndex, int playerCount)
    {
        Player[] player = PhotonNetwork.PlayerList;
        Player localPlayer = PhotonNetwork.LocalPlayer;
        localPlayer.CustomProperties.TryGetValue("PlayerQue", out object localPlayerQue);
        int localPlayerQueInt = (int)localPlayerQue;
        if (localPlayerQueInt == playerCount) return;
        if (per.Count == 0) return;

        for (int i = 0; i < player.Length; i++)
        {
            if (player[i].CustomProperties.TryGetValue("PlayerQue", out object playerQue))
            {
                int playerQueInt = (int)playerQue;

                if (playerQueInt == playerCount)
                {
                    // Oyuncunun sırasına göre uygun placeholder dizilerini belirle
                    Transform meldTileContainer = GameObject.Find(player[i].NickName + " meld").transform;
                    Transform colorTileMeldContainer = meldTileContainer.GetChild(0);
                    Transform numberTileContainer = meldTileContainer.GetChild(1);
                    Transform pairTileContainer = meldTileContainer.GetChild(2);

                    List<Tiles> availableTiles = GetAvailableTiles(per, rowIndex); // Available taşları al

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
                            Debug.Log("rightPlaceholderIndex: " + rightPlaceholderIndex);
                            if (rightPlaceholderIndex < colorTileMeldContainer.childCount) // colorPerPlaceHolders dizisini kullanarak kontrol edin
                            {
                                Placeholder rightPlaceholder = colorTileMeldContainer.GetChild(rightPlaceholderIndex).GetComponent<Placeholder>();
                                if (rightPlaceholder != null)
                                {
                                    rightPlaceholder.available = true; // PlaceHolder'daki available'ı true yap
                                                                       // Available taş bilgilerini yerleştir
                                    rightPlaceholder.AvailableTileInfo = availableTiles.FirstOrDefault(tile => tile.number == maxNumber + 1);
                                }
                                else
                                {
                                    Debug.Log("rightPlaceholder == null, right placeholder will not be updated.");
                                }
                            }
                            else
                            {
                                Debug.Log("rightPlaceholderIndex >= colorPerPlaceHolders.Length, right placeholder will not be updated.");
                            }
                        }
                        else
                        {
                            Debug.Log("maxTileNumber == 13, right placeholder will not be updated.");
                        }

                        // Eğer en küçük taş 1'den büyükse, en küçük taşın bulunduğu yer tutucunun solundaki yer tutucunun available durumunu güncelle
                        if (minNumber > 1)
                        {
                            int leftPlaceholderIndex = (minNumber - 2) + 13 * rowIndex;
                            Debug.Log("leftPlaceholderIndex: " + leftPlaceholderIndex);
                            if (leftPlaceholderIndex >= 0)
                            {
                                Placeholder leftPlaceholder = colorTileMeldContainer.GetChild(leftPlaceholderIndex).GetComponent<Placeholder>();
                                if (leftPlaceholder != null)
                                {
                                    leftPlaceholder.available = true; // PlaceHolder'daki available'ı true yap
                                                                      // Available taş bilgilerini yerleştir
                                    leftPlaceholder.AvailableTileInfo = availableTiles.FirstOrDefault(tile => tile.number == minNumber - 1);
                                }
                                else
                                {
                                    Debug.Log("leftPlaceholder == null, left placeholder will not be updated.");
                                }
                            }
                            else
                            {
                                Debug.Log("leftPlaceholderIndex < 0, left placeholder will not be updated.");
                            }
                        }
                        else
                        {
                            Debug.Log("minTileNumber <= 1, left placeholder will not be updated.");
                        }

                        // Eğer perde joker içeriyorsa, jokerin bulunduğu yer tutucunun available durumunu güncelle
                        if (hasJoker)
                        {
                            var jokerTile = per.First(tile => tile.type == TileType.Joker);
                            int jokerPlaceholderIndex = jokerTile.number - 1 + 13 * rowIndex;
                            if (jokerPlaceholderIndex >= 0 && jokerPlaceholderIndex < colorTileMeldContainer.childCount)
                            {
                                Placeholder jokerPlaceholder = colorTileMeldContainer.GetChild(jokerPlaceholderIndex).GetComponent<Placeholder>();
                                if (jokerPlaceholder != null)
                                {
                                    jokerPlaceholder.available = true; // PlaceHolder'daki available'ı true yap
                                                                       // Available taş bilgilerini yerleştir
                                    jokerPlaceholder.AvailableTileInfo = availableTiles.FirstOrDefault(tile => tile.number == jokerTile.number);
                                }
                                else
                                {
                                    Debug.Log("jokerPlaceholder == null, joker placeholder will not be updated.");
                                }
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
                                    Placeholder fourthPlaceholder = numberTileContainer.GetChild(fourthPlaceholderIndex).GetComponent<Placeholder>();
                                    if (fourthPlaceholder != null)
                                    {
                                        fourthPlaceholder.available = true; // PlaceHolder'daki available'ı true yap
                                                                            // Available taş bilgilerini yerleştir
                                        fourthPlaceholder.AvailableTileInfo = availableTiles.FirstOrDefault(tile => tile.number == numberGroups[0].First().number); // Örnek olarak 4. taş
                                    }
                                    else
                                    {
                                        Debug.Log("fourthPlaceholder == null, fourth placeholder will not be updated.");
                                    }
                                }
                                else
                                {
                                    Debug.Log("fourthPlaceholderIndex >= numberPerPlaceHolders.Length, fourth placeholder will not be updated.");
                                }
                            }

                            // Eğer joker varsa ve 3 taşlı ise, hem 4. placeholder'ı hem de joker taşının bulunduğu placeholder'ı true yap
                            if (hasJoker && per.Count == 3)
                            {
                                int fourthPlaceholderIndex = 3 + (4 * rowIndex); // 4. placeholder'ın indeksi
                                if (fourthPlaceholderIndex < numberTileContainer.childCount)
                                {
                                    Placeholder fourthPlaceholder = numberTileContainer.GetChild(fourthPlaceholderIndex).GetComponent<Placeholder>();
                                    if (fourthPlaceholder != null)
                                    {
                                        fourthPlaceholder.available = true; // PlaceHolder'daki available'ı true yap
                                                                            // Available taş bilgilerini yerleştir
                                        fourthPlaceholder.AvailableTileInfo = availableTiles.FirstOrDefault(tile => tile.number == numberGroups[0].First().number); // Örnek olarak 4. taş
                                    }
                                    else
                                    {
                                        Debug.Log("fourthPlaceholder == null, fourth placeholder will not be updated.");
                                    }
                                }
                                else
                                {
                                    Debug.Log("fourthPlaceholderIndex >= numberPerPlaceHolders.Length, fourth placeholder will not be updated.");
                                }

                                // Joker taşının bulunduğu yer tutucunun available durumunu güncelle
                                int jokerPlaceholderIndex = -1; // Joker taşının bulunduğu placeholder'ın indeksi
                                for (int j = 0; j < numberTileContainer.childCount; j++)
                                {
                                    Placeholder currentPlaceholder = numberTileContainer.GetChild(j).GetComponent<Placeholder>();
                                    if (currentPlaceholder != null && currentPlaceholder.transform.childCount > 0)
                                    {
                                        foreach (Transform child in currentPlaceholder.transform)
                                        {
                                            TileUI tile = child.GetComponent<TileUI>();
                                            if (tile != null && tile.tileDataInfo.type == TileType.Joker)
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
                                    Placeholder jokerPlaceholder = numberTileContainer.GetChild(jokerPlaceholderIndex).GetComponent<Placeholder>();
                                    if (jokerPlaceholder != null)
                                    {
                                        jokerPlaceholder.available = true; // PlaceHolder'daki available'ı true yap
                                                                           // Available taş bilg ilerini yerleştir
                                        jokerPlaceholder.AvailableTileInfo = availableTiles.FirstOrDefault(tile => tile.number == numberGroups[0].First().number);
                                    }
                                    else
                                    {
                                        Debug.Log("jokerPlaceholder == null, joker placeholder will not be updated.");
                                    }
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
                                    Placeholder currentPlaceholder = numberTileContainer.GetChild(j).GetComponent<Placeholder>();
                                    if (currentPlaceholder != null && currentPlaceholder.transform.childCount > 0)
                                    {
                                        foreach (Transform child in currentPlaceholder.transform)
                                        {
                                            TileUI tile = child.GetComponent<TileUI>();
                                            if (tile != null && tile.tileDataInfo.type == TileType.Joker)
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
                                    Placeholder jokerPlaceholder = numberTileContainer.GetChild(jokerPlaceholderIndex).GetComponent<Placeholder>();
                                    if (jokerPlaceholder != null)
                                    {
                                        jokerPlaceholder.available = true; // PlaceHolder'daki available'ı true yap
                                                                           // Available taş bilgilerini yerleştir
                                        jokerPlaceholder.AvailableTileInfo = availableTiles.FirstOrDefault(tile => tile.number == numberGroups[0].First().number);
                                    }
                                    else
                                    {
                                        Debug.Log("jokerPlaceholder == null, joker placeholder will not be updated.");
                                    }
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
                                Placeholder currentPlaceholder = pairTileContainer.GetChild(j).GetComponent<Placeholder>();
                                if (currentPlaceholder != null && currentPlaceholder.transform.childCount > 0)
                                {
                                    foreach (Transform child in currentPlaceholder.transform)
                                    {
                                        TileUI tile = child.GetComponent<TileUI>();
                                        if (tile != null && tile.tileDataInfo.type == TileType.Joker)
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
                                Placeholder jokerPlaceholder = pairTileContainer.GetChild(jokerPlaceholderIndex).GetComponent<Placeholder>();
                                if (jokerPlaceholder != null)
                                {
                                    jokerPlaceholder.available = true; // PlaceHolder'daki available'ı true yap
                                                                       // Available taş bilgilerini yerleştir
                                    jokerPlaceholder.AvailableTileInfo = availableTiles.FirstOrDefault(tile => tile.number == per.First().number);
                                }
                                else
                                {
                                    Debug.Log("jokerPlaceholder == null, joker placeholder will not be updated.");
                                }
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
        var allColors = new List<TileColor> { TileColor.red, TileColor.blue, TileColor.black, TileColor.yellow }; // Tüm renkler
        return allColors.Except(existingColors).ToList();
    }


    #endregion

    #endregion
    #endregion
}