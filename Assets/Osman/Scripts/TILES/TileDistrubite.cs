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

        DistributeTilesToAllPlayers(); // Taşları dağıt

        // --- BU SATIRI EKLE ---
        // Taşlar dağıtıldı, her şey hazır. Artık oyun başlayabilir.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameReady();
        }
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
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateTileCount(allTiles.Count);
        }
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
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateTileCount(allTiles.Count);
        }
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

    // TileDistrubite.cs

    void DestroySideTiles(int playerCount)
    {
        // --- GÜVENLİK KONTROLÜ (YENİ EKLENEN KISIM) ---
        if (droppedTiles == null || droppedTiles.Count == 0)
        {
            Debug.LogWarning(
                "DestroySideTiles: Silinecek görsel taş bulunamadı (Liste Boş). İşlem atlanıyor."
            );
            return;
        }
        // ----------------------------------------------

        Player player = PhotonNetwork.LocalPlayer;

        // Hata veren satır artık güvende:
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
                if (droppedTile != null) // Ekstra null kontrolü
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

    // TileDistrubite.cs

    [PunRPC]
    public void DeactivatePlayerTile(int playerQue, Tiles tileToDeactivate)
    {
        // 1. Sadece bu oyuncunun (Local Player) kendi ekranında işlem yapıyoruz.
        object localQue;
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out localQue))
        {
            if ((int)localQue != playerQue)
                return;
        }
        else
        {
            return;
        }

        if (playerTileContainers == null)
            return;

        bool found = false;

        foreach (Transform placeholder in playerTileContainers)
        {
            if (placeholder.childCount > 0)
            {
                GameObject tileObj = placeholder.GetChild(0).gameObject;

                // Zaten kapalı/gizli ise pas geç (KİLİT NOKTA BURASI)
                if (!tileObj.activeSelf)
                    continue;

                TileUI ui = tileObj.GetComponent<TileUI>();
                if (ui != null)
                {
                    bool isMatch = false;

                    // A) JOKER KONTROLÜ
                    if (
                        tileToDeactivate.type == TileType.Joker
                        && ui.tileDataInfo.type == TileType.Joker
                    )
                    {
                        isMatch = true;
                    }
                    // B) NORMAL TAŞ KONTROLÜ
                    else if (
                        ui.tileDataInfo.color == tileToDeactivate.color
                        && ui.tileDataInfo.number == tileToDeactivate.number
                        && ui.tileDataInfo.type == tileToDeactivate.type
                    )
                    {
                        isMatch = true;
                    }

                    if (isMatch)
                    {
                        // --- DÜZELTME BURADA ---
                        // Önce GİZLE (Anında çalışır), sonra YOK ET (Frame sonu çalışır)
                        // Böylece bir sonraki döngüde bu taşı görüp tekrar işlem yapmaz.
                        tileObj.SetActive(false);
                        Destroy(tileObj);

                        found = true;
                        return; // İlk bulduğunu sil ve çık
                    }
                }
            }
        }

        if (!found)
        {
            Debug.LogWarning(
                $"[DeactivatePlayerTile] Silinecek taş görseli bulunamadı: {tileToDeactivate.color} {tileToDeactivate.number}"
            );
        }
    }

    // --- [YENİ] KESİN ÇÖZÜM İÇİN EKLENEN RPC ---
    // Taşı özelliklerine göre değil, bulunduğu kutu sırasına (Index) göre kapatır.
    // Bu sayede veri değişse bile doğru kutu kapanır.
    [PunRPC]
    public void DeactivatePlayerTileByIndex(int playerQue, int tileIndex)
    {
        // 1. Bu işlem sadece ilgili oyuncunun ekranında çalışmalı
        object localQue;
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out localQue))
        {
            if ((int)localQue != playerQue)
                return;
        }

        // 2. İndeks kontrolü (Hata vermemesi için)
        if (
            playerTileContainer == null
            || tileIndex < 0
            || tileIndex >= playerTileContainer.childCount
        )
        {
            Debug.LogError($"[DeactivateByIndex] Geçersiz indeks: {tileIndex}");
            return;
        }

        // 3. O kutucuktaki taşı bul ve kapat
        Transform placeholder = playerTileContainer.GetChild(tileIndex);
        if (placeholder.childCount > 0)
        {
            GameObject tileObj = placeholder.GetChild(0).gameObject;
            tileObj.SetActive(false);
            // Debug.Log($"Istaka {tileIndex}. sıradaki taş başarıyla gizlendi.");
        }
    }

    [PunRPC]
    public void MeldTiles(int playerNumber, Tiles tileToRemove)
    {
        // 1. Önce görselleri oluştur (Eğer eksik varsa tamamlar)
        InstatiateMeldTiles(playerNumber);

        List<Tiles> targetList = null;

        // Hangi oyuncunun listesinden silinecek?
        switch (playerNumber)
        {
            case 1:
                targetList = playerTiles1;
                break;
            case 2:
                targetList = playerTiles2;
                break;
            case 3:
                targetList = playerTiles3;
                break;
            case 4:
                targetList = playerTiles4;
                break;
        }

        if (targetList != null)
        {
            // --- AKILLI SİLME MANTIĞI ---
            // 1. Tam Eşleşme Ara (Renk, Numara, Tip)
            Tiles foundTile = targetList.FirstOrDefault(t =>
                t.color == tileToRemove.color
                && t.number == tileToRemove.number
                && t.type == tileToRemove.type
            );

            // 2. Bulamazsa ve Silinecek Taş Joker İse -> Eldeki herhangi bir Jokeri bul
            // (Masaya Kırmızı 5 Joker gitmiş olabilir ama elde Siyah 0 Joker vardır)
            if (foundTile == null && tileToRemove.type == TileType.Joker)
            {
                foundTile = targetList.FirstOrDefault(t => t.type == TileType.Joker);
            }

            // 3. Bulamazsa -> Sadece Renk ve Numara tutanı bul (Tip farklı olabilir)
            if (foundTile == null)
            {
                foundTile = targetList.FirstOrDefault(t =>
                    t.color == tileToRemove.color && t.number == tileToRemove.number
                );
            }

            // Bulduysan sil
            if (foundTile != null)
            {
                targetList.Remove(foundTile);
            }
            else
            {
                // Debug.LogWarning($"Silinecek taş oyuncu listesinde bulunamadı: {tileToRemove.color} {tileToRemove.number}");
            }
        }

        // Temizlik
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

        // Kendi taşlarımızı zaten ScoreManager anında oluşturduğu için tekrar oluşturmuyoruz.
        // Sadece diğer oyuncular veya senkronizasyon için çalışır.
        if (localPlayerQueInt == playerCount)
            return;

        // Hangi listeyi kullanacağız?
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

        // Oyuncuları gez ve doğru container'ı bul
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

                    for (int j = 0; j < validMeltedTiles.Count; j++)
                    {
                        var per = validMeltedTiles[j];

                        // Pozisyon listesi senkron hatası yüzünden eksikse atla
                        if (j >= positions.Count)
                            continue;

                        List<Vector2Int> perPosition = positions[j];
                        if (per.Count != perPosition.Count)
                            continue;

                        // --- 1. SINGLE COLOR (RENKLİ SERİ) ---
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
                                    Transform targetSlot = colorTileMeldContainer.GetChild(
                                        columnIndex
                                    );

                                    // [YENİ] Eğer doluysa tekrar oluşturma, sadece available güncelle ve geç
                                    if (targetSlot.childCount > 0)
                                    {
                                        UpdateAvailableForPlaceholders(per, rowIndex, playerCount);
                                        continue;
                                    }

                                    GameObject tileInstanceColor = Instantiate(
                                        meldTilePrefab,
                                        targetSlot
                                    );
                                    TileUI tileUI = tileInstanceColor.GetComponent<TileUI>();
                                    tileUI.CheckRowColoumn(rowIndex, columnIndex);
                                    if (tileUI != null)
                                        tileUI.SetTileData(tiles);
                                    tileUI.FitToParent();
                                }
                                UpdateAvailableForPlaceholders(per, rowIndex, playerCount);
                            }
                        }
                        // --- 2. MULTI COLOR (SAYI GRUBU) ---
                        else if (scoreManager.MultiColorCheck(per))
                        {
                            foreach (var tiles in per)
                            {
                                Vector2Int position = perPosition[per.IndexOf(tiles)];
                                int rowIndex = position.x;
                                int columnIndex = position.y;

                                if (columnIndex < numberTileContainer.childCount)
                                {
                                    Transform targetSlot = numberTileContainer.GetChild(
                                        columnIndex
                                    );

                                    // [YENİ] Doluluk Kontrolü
                                    if (targetSlot.childCount > 0)
                                    {
                                        UpdateAvailableForPlaceholders(per, rowIndex, playerCount);
                                        continue;
                                    }

                                    GameObject tileInstanceColor = Instantiate(
                                        meldTilePrefab,
                                        targetSlot
                                    );
                                    TileUI tileUI = tileInstanceColor.GetComponent<TileUI>();
                                    tileUI.CheckRowColoumn(rowIndex, columnIndex);
                                    if (tileUI != null)
                                        tileUI.SetTileData(tiles);
                                    tileUI.FitToParent();
                                }
                                UpdateAvailableForPlaceholders(per, rowIndex, playerCount);
                            }
                        }
                        // --- 3. PAIR (ÇİFT) ---
                        else if (
                            scoreManager.IsSingleColor(per) && scoreManager.CheckForDoublePer(per)
                        )
                        {
                            foreach (var tiles in per)
                            {
                                Vector2Int position = perPosition[per.IndexOf(tiles)];
                                int rowIndex = position.x;
                                int columnIndex = position.y;

                                if (columnIndex < pairTileContainer.childCount)
                                {
                                    Transform targetSlot = pairTileContainer.GetChild(columnIndex);

                                    // [YENİ] Doluluk Kontrolü
                                    if (targetSlot.childCount > 0)
                                    {
                                        UpdateAvailableForPlaceholders(per, rowIndex, playerCount);
                                        continue;
                                    }

                                    GameObject tileInstancePair = Instantiate(
                                        meldTilePrefab,
                                        targetSlot
                                    );
                                    TileUI tileUI = tileInstancePair.GetComponent<TileUI>();
                                    tileUI.CheckRowColoumn(rowIndex, columnIndex);
                                    if (tileUI != null)
                                        tileUI.SetTileData(tiles);
                                    tileUI.FitToParent();
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

    // TileDistrubite.cs -> GetAvailableTiles Metodu (Komple Değiştir)

    public List<Tiles> GetAvailableTiles(List<Tiles> meld)
    {
        List<Tiles> availableTiles = new List<Tiles>();

        // -------------------------------------------------------
        // 1. SINGLE COLOR (Renkli Sıralı Per)
        // -------------------------------------------------------
        if (scoreManager.IsSingleColor(meld) && scoreManager.SingleColorCheck(meld))
        {
            if (meld.Count > 0)
            {
                var numbers = meld.Select(tile => tile.number).ToList();
                bool hasJoker = meld.Any(tile => tile.type == TileType.Joker);

                int minNumber = numbers.Min();
                int maxNumber = numbers.Max();

                // Sol tarafa ekleme
                if (minNumber > 1)
                {
                    var refTile = meld.FirstOrDefault(t => t.type != TileType.Joker);
                    if (refTile != null)
                        availableTiles.Add(
                            new Tiles(refTile.color, minNumber - 1, TileType.Number)
                        );
                }

                // Sağ tarafa ekleme
                if (maxNumber < 13)
                {
                    var refTile = meld.FirstOrDefault(t => t.type != TileType.Joker);
                    if (refTile != null)
                        availableTiles.Add(
                            new Tiles(refTile.color, maxNumber + 1, TileType.Number)
                        );
                }

                // Joker Takası
                if (hasJoker)
                {
                    var jokerTile = meld.First(tile => tile.type == TileType.Joker);
                    // Not: Burada jokerin numarasını baz alıyoruz, sıralı perlerde bu genelde doğrudur
                    availableTiles.Add(
                        new Tiles(jokerTile.color, jokerTile.number, TileType.Number)
                    );
                }
            }
        }
        // -------------------------------------------------------
        // 2. MULTI COLOR (Sayı Grubu) -- KRİTİK DÜZELTME BURADA
        // -------------------------------------------------------
        else if (scoreManager.MultiColorCheck(meld))
        {
            if (meld.Count >= 3)
            {
                // Joker olmayan bir taşı referans al (Numarayı bulmak için)
                var refTile = meld.FirstOrDefault(t => t.type != TileType.Joker);
                if (refTile != null)
                {
                    int targetNumber = refTile.number;

                    // Masadaki GERÇEK (Joker olmayan) renkleri bul
                    var realColors = meld.Where(t => t.type != TileType.Joker)
                        .Select(t => t.color)
                        .ToList();

                    // Tüm renkler havuzu
                    List<TileColor> allColors = new List<TileColor>
                    {
                        TileColor.yellow,
                        TileColor.blue,
                        TileColor.black,
                        TileColor.red,
                    };

                    // Masada OLMAYAN renkleri bul
                    // 3 taş varsa 1 renk eksiktir.
                    // 4 taş varsa (biri Joker) yine 1 renk eksiktir (Jokerin sakladığı renk).
                    var missingColors = allColors.Except(realColors).ToList();

                    // Eksik olan her rengi "Available" olarak ekle
                    foreach (var color in missingColors)
                    {
                        availableTiles.Add(new Tiles(color, targetNumber, TileType.Number));
                    }
                }
            }
        }
        // -------------------------------------------------------
        // 3. ÇİFT PER (Pair)
        // -------------------------------------------------------
        else if (scoreManager.CheckForDoublePer(meld) && scoreManager.IsSingleColor(meld))
        {
            if (meld.Any(tile => tile.type == TileType.Joker))
            {
                var refTile = meld.FirstOrDefault(t => t.type != TileType.Joker);
                if (refTile != null)
                {
                    availableTiles.Add(new Tiles(refTile.color, refTile.number, TileType.Number));
                }
            }
        }

        return availableTiles;
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
    public void InstantiateActiveTiles(ActiveTilePlacementInfo[] placements)
    {
        Debug.Log($"RPC Alındı: {placements.Length} adet işlek taş oluşturulacak.");

        foreach (var placement in placements)
        {
            // 1. Doğru oyuncunun meld alanını bul
            Transform targetMeldContainer = null;
            foreach (var player in PhotonNetwork.PlayerList)
            {
                if (
                    player.CustomProperties.TryGetValue("PlayerQue", out object playerQue)
                    && (int)playerQue == placement.ownerPlayerQue
                )
                {
                    targetMeldContainer = GameObject.Find(player.NickName + " meld")?.transform;
                    break;
                }
            }

            if (targetMeldContainer == null)
            {
                Debug.LogError($"Oyuncu {placement.ownerPlayerQue} için meld alanı bulunamadı!");
                continue;
            }

            // 2. Doğru per türü (renk, sayı, çift) alanını bul
            Transform typeContainer = targetMeldContainer.GetChild((int)placement.meldType);
            if (typeContainer == null)
            {
                Debug.LogError($"Meld türü için alan bulunamadı: {placement.meldType}");
                continue;
            }

            // 3. Doğru placeholder'ı (yuva) bul
            if (placement.placeholderIndex < typeContainer.childCount)
            {
                Transform placeholder = typeContainer.GetChild(placement.placeholderIndex);

                // Bu yuvada zaten bir taş varsa temizle (önlem olarak)
                foreach (Transform child in placeholder)
                {
                    Destroy(child.gameObject);
                }

                // 4. Taşı oluştur ve verisini ata
                GameObject tileInstance = Instantiate(meldTilePrefab, placeholder);
                List<Tiles> playerTile = GetPlayerTiles();
                TileUI tileUI = tileInstance.GetComponent<TileUI>();
                if (tileUI != null)
                {
                    tileUI.SetTileData(placement.tileData);
                }
                tileUI.FitToParent();

                Debug.Log(
                    $"{placement.tileData.color} {placement.tileData.number} taşı, Oyuncu {placement.ownerPlayerQue} için başarıyla oluşturuldu."
                );
            }
            else
            {
                Debug.LogError($"Placeholder indeksi geçersiz: {placement.placeholderIndex}");
            }
        }

        // 5. TÜM TAŞLAR YERLEŞTİKTEN SONRA, YENİ İŞLEK YUVALARI HESAPLA
        RecalculateAllAvailableSlots();
    }

    // TileDistrubite.cs

    // TileDistrubite.cs

    public void RecalculateAllAvailableSlots()
    {
        if (scoreManager == null)
            scoreManager = FindObjectOfType<ScoreManager>();

        // KURAL: Elini açmayan oyuncu (Seri veya Çift yoksa) HİÇBİR ŞEY işleyemez.
        bool hasOpenedHand = (
            scoreManager != null && (scoreManager.hasOpenedSeries || scoreManager.hasOpenedPairs)
        );

        availableTiles.Clear();

        // ----------------------------------------------------------------------
        // ADIM 1: ÖNCE MASADAKİ TÜM "AVAILABLE" IŞIKLARINI SÖNDÜR (RESETLEME)
        // ----------------------------------------------------------------------
        // Bu adım Joker dahil her türlü işleme ihtimalini görsel ve mantıksal olarak sıfırlar.

        List<List<Tiles>> currentBoardMelds = new List<List<Tiles>>();
        List<(List<Tiles> per, int row, ScoreManager.MeldType type)> meldDetails =
            new List<(List<Tiles>, int, ScoreManager.MeldType)>();

        foreach (var player in PhotonNetwork.PlayerList)
        {
            Transform meldContainer = GameObject.Find(player.NickName + " meld")?.transform;
            if (meldContainer == null)
                continue;

            // Renk(0), Sayı(1), Çift(2) kaplarını gez
            foreach (Transform typeContainer in meldContainer)
            {
                // O kaptaki her bir kutucuğu (Placeholder) gez
                foreach (Transform placeholder in typeContainer)
                {
                    Placeholder phComponent = placeholder.GetComponent<Placeholder>();
                    if (phComponent != null)
                    {
                        // KİLİTLE!
                        phComponent.available = false;
                        phComponent.AvailableTileInfo = null;
                    }
                }
            }
        }

        // ----------------------------------------------------------------------
        // ADIM 2: EĞER OYUNCU ELİNİ AÇMADIYSA, İŞLEMİ BURADA BİTİR!
        // ----------------------------------------------------------------------
        if (!hasOpenedHand)
        {
            Debug.Log(
                "Oyuncu elini açmadığı için masaya taş işleyemez (Joker dahil). Hesaplama durduruldu."
            );
            return; // Çıkış. Kimse yeşil yanmayacak.
        }

        // ----------------------------------------------------------------------
        // ADIM 3: SADECE ELİNİ AÇMIŞSA HESAPLAMAYA DEVAM ET
        // ----------------------------------------------------------------------
        Debug.Log("Oyuncu elini açmış, işlek taşlar hesaplanıyor...");

        // Masadaki taşları oku ve grupla (Mevcut mantık)
        foreach (var player in PhotonNetwork.PlayerList)
        {
            Transform meldContainer = GameObject.Find(player.NickName + " meld")?.transform;
            if (meldContainer == null)
                continue;

            for (int i = 0; i < meldContainer.childCount; i++)
            {
                Transform typeContainer = meldContainer.GetChild(i);
                ScoreManager.MeldType type = (ScoreManager.MeldType)i;
                int rowWidth =
                    (type == ScoreManager.MeldType.SingleColor)
                        ? 13
                        : (type == ScoreManager.MeldType.MultiColor ? 4 : 2);

                List<Tiles> currentMeld = new List<Tiles>();
                int currentRowIndex = 0;

                for (int j = 0; j < typeContainer.childCount; j++)
                {
                    Transform placeholder = typeContainer.GetChild(j);
                    int thisRow = j / rowWidth;

                    if (thisRow != currentRowIndex)
                    {
                        if (currentMeld.Count > 0)
                        {
                            currentBoardMelds.Add(new List<Tiles>(currentMeld));
                            meldDetails.Add((new List<Tiles>(currentMeld), currentRowIndex, type));
                            currentMeld.Clear();
                        }
                        currentRowIndex = thisRow;
                    }

                    if (placeholder.childCount > 0)
                    {
                        TileUI tileUI = placeholder.GetChild(0).GetComponent<TileUI>();
                        if (tileUI != null)
                            currentMeld.Add(tileUI.tileDataInfo);
                    }
                    else
                    {
                        if (currentMeld.Count > 0)
                        {
                            currentBoardMelds.Add(new List<Tiles>(currentMeld));
                            meldDetails.Add((new List<Tiles>(currentMeld), currentRowIndex, type));
                            currentMeld.Clear();
                        }
                    }
                }
                if (currentMeld.Count > 0)
                {
                    currentBoardMelds.Add(new List<Tiles>(currentMeld));
                    meldDetails.Add((new List<Tiles>(currentMeld), currentRowIndex, type));
                }
            }
        }

        // Matematiksel listeyi güncelle
        foreach (var per in currentBoardMelds)
        {
            var availableFromPer = GetAvailableTiles(per);
            foreach (var tile in availableFromPer)
            {
                bool exists = availableTiles.Any(t =>
                    t.color == tile.color && t.number == tile.number && t.type == tile.type
                );
                if (!exists)
                    availableTiles.Add(tile);
            }
        }

        // Görsel Placeholder'ları Güncelle (Sadece açanlar buraya kadar gelebilir)
        foreach (var detail in meldDetails)
        {
            int localQue = 0;
            if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object q))
                localQue = (int)q;

            // Eğer UpdateAvailableForPlaceholders 3 parametre alıyorsa:
            scoreManager.UpdateAvailableForPlaceholders(detail.per, detail.row);

            // Eğer 2 parametre alıyorsa (Önceki versiyon):
            // scoreManager.UpdateAvailableForPlaceholders(detail.per, detail.row);
        }
    }

    // TileDistrubite.cs

    [PunRPC]
    public void RemoveActiveTileFromPlayerList(int playerQue, Tiles tileToRemove)
    {
        List<Tiles> playerTiles = null;

        // Hangi oyuncunun listesi?
        switch (playerQue)
        {
            case 1:
                playerTiles = playerTiles1;
                break;
            case 2:
                playerTiles = playerTiles2;
                break;
            case 3:
                playerTiles = playerTiles3;
                break;
            case 4:
                playerTiles = playerTiles4;
                break;
        }

        if (playerTiles != null)
        {
            // 1. ADIM: Tam Eşleşme Ara (Renk, Numara, Tip)
            Tiles foundTile = playerTiles.FirstOrDefault(t =>
                t.color == tileToRemove.color
                && t.number == tileToRemove.number
                && t.type == tileToRemove.type
            );

            // 2. ADIM: Bulamazsa ve silinecek taş JOKER ise -> Eldeki herhangi bir Jokeri bul
            // (Çünkü masaya Kırmızı 5 Joker gitmiş olabilir ama elde Siyah 0 Joker vardır)
            if (foundTile == null && tileToRemove.type == TileType.Joker)
            {
                foundTile = playerTiles.FirstOrDefault(t => t.type == TileType.Joker);
            }

            // 3. ADIM: Bulamazsa ve sadece TİP farklıysa -> Renk/Numara tutuyorsa sil
            // (Örn: Sahte Okey - Normal Sayı karışıklığı için)
            if (foundTile == null)
            {
                foundTile = playerTiles.FirstOrDefault(t =>
                    t.color == tileToRemove.color && t.number == tileToRemove.number
                );
            }

            if (foundTile != null)
            {
                playerTiles.Remove(foundTile);
                Debug.Log(
                    $"[SYNC] Oyuncu {playerQue} listesinden taş silindi: {foundTile.color} {foundTile.number} ({foundTile.type})"
                );
            }
            else
            {
                Debug.LogWarning(
                    $"[SYNC HATASI] SİLİNEMEDİ! Oyuncu {playerQue} elinde {tileToRemove.color} {tileToRemove.number} bulunamadı."
                );
            }
        }
    }

    [PunRPC]
    public void AddTileToPlayerHand(int playerQue, Tiles tileData)
    {
        // 1. VERİ KISMI: Master Client, oyuncunun listesine (List<Tiles>) ekler
        if (PhotonNetwork.IsMasterClient)
        {
            List<Tiles> targetHand = null;
            switch (playerQue)
            {
                case 1:
                    targetHand = playerTiles1;
                    break;
                case 2:
                    targetHand = playerTiles2;
                    break;
                case 3:
                    targetHand = playerTiles3;
                    break;
                case 4:
                    targetHand = playerTiles4;
                    break;
            }

            if (targetHand != null)
            {
                targetHand.Add(tileData);
            }
        }

        // 2. GÖRSEL KISIM: Sadece o oyuncunun kendi ekranında (Local) görsel oluşturulur
        object localQue;
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out localQue))
        {
            // Eğer ben bu oyuncuysam, ıstakama görseli ekle
            if ((int)localQue == playerQue)
            {
                InstantiateTileInFirstEmptySlot(tileData);
            }
        }
    }

    private void InstantiateTileInFirstEmptySlot(Tiles tile)
    {
        if (playerTileContainers == null)
            return;

        for (int i = 0; i < playerTileContainers.Length; i++)
        {
            Transform slot = playerTileContainers[i];
            bool isSlotAvailable = false;

            // 1. Slot tamamen boşsa
            if (slot.childCount == 0)
            {
                isSlotAvailable = true;
            }
            // 2. Slot dolu ama içindeki taş "Silinmek Üzere" (ActiveSelf = false) ise
            // BURASI SENİN SORUNUNU ÇÖZEN YER!
            else
            {
                GameObject childObj = slot.GetChild(0).gameObject;
                if (!childObj.activeSelf)
                {
                    DestroyImmediate(childObj); // Engel olan hayalet taşı yok et
                    isSlotAvailable = true;
                }
            }

            if (isSlotAvailable)
            {
                GameObject tileInstance = Instantiate(tilePrefab, slot);
                TileUI tileUI = tileInstance.GetComponent<TileUI>();
                if (tileUI != null)
                {
                    tileUI.SetTileData(tile);
                    tileUI.FitToParent();
                }
                return;
            }
        }
        Debug.LogWarning("Istakada yer yok! Joker görseli oluşturulamadı.");
    }

    // TileDistrubite.cs içine:

    [PunRPC]
    public void SyncProcessedTileRPC(
        int ownerQue,
        Tiles tileData,
        int meldTypeInt,
        int placeholderIndex
    )
    {
        // 1. Bu ownerQue kime ait? O oyuncuyu bul.
        Photon.Realtime.Player targetPlayer = null;
        foreach (var p in PhotonNetwork.PlayerList)
        {
            if (p.CustomProperties.TryGetValue("PlayerQue", out object q) && (int)q == ownerQue)
            {
                targetPlayer = p;
                break;
            }
        }

        if (targetPlayer == null)
            return;

        // 2. O oyuncunun masasını (Meld Container) isminden bul.
        // SeatManager mantığına göre: NickName + " meld"
        GameObject meldContainer = GameObject.Find(targetPlayer.NickName + " meld");
        if (meldContainer == null)
            return;

        // 3. Doğru satırı (Row) bul (Color=0, Number=1, Pair=2)
        if (meldTypeInt >= meldContainer.transform.childCount)
            return;
        Transform rowTransform = meldContainer.transform.GetChild(meldTypeInt);

        // 4. Doğru kutucuğu (Placeholder) index ile bul
        if (placeholderIndex >= rowTransform.childCount)
            return;
        Transform targetPlaceholder = rowTransform.GetChild(placeholderIndex);

        // 5. Görseli Oluştur
        // Eğer orada eski bir taş varsa (Joker Swap durumu) onu yok et
        if (targetPlaceholder.childCount > 0)
        {
            Destroy(targetPlaceholder.GetChild(0).gameObject);
        }

        GameObject tileObj = Instantiate(tilePrefab, targetPlaceholder);
        TileUI tileUI = tileObj.GetComponent<TileUI>();

        // Remote clientlarda da doğru gözüksün
        tileUI.SetTileData(tileData);
        tileUI.FitToParent();

        // ScoreManager'ı güncelle (Senkronizasyon için önemli)
        // Eğer bu client MasterClient ise belki puan hesaplaması yapması gerekebilir
        // Ama görsel senkronizasyon için bu kadarı yeterli.

        Debug.Log($"Senkronizasyon Başarılı: {targetPlayer.NickName}'in masasına taş işlendi.");
    }
    #endregion
    #endregion
    #region SCORES
    // TileDistrubite.cs içine ekle:

    // Gösterge taşını ver (Score hesabı için)
    public Tiles GetIndicatorTile()
    {
        if (indicatorTileContainer.childCount > 0)
        {
            var ui = indicatorTileContainer.GetChild(0).GetComponent<TileUI>();
            if (ui)
                return ui.tileDataInfo;
        }
        return null;
    }

    // Belirli bir oyuncunun elinde kaç taş kaldığını ver (Ceza hesabı için)
    public int GetPlayerHandCount(int playerQue)
    {
        switch (playerQue)
        {
            case 1:
                return playerTiles1.Count;
            case 2:
                return playerTiles2.Count;
            case 3:
                return playerTiles3.Count;
            case 4:
                return playerTiles4.Count;
            default:
                return 0;
        }
    }

    // Örnek: Oyuncu taş attığında veya el değiştiğinde
    // TileDistrubite.cs içine:

    // TileDistrubite.cs

    [PunRPC]
    public void ReturnTileToSide(int returningPlayerQue, Tiles tileToReturn)
    {
        // 1. Taşı iade eden oyuncudan BİR ÖNCEKİ oyuncuyu bul
        int previousPlayerQue = returningPlayerQue - 1;
        if (previousPlayerQue < 1)
            previousPlayerQue = 4;

        // 2. O oyuncunun ismini bul
        string targetContainerName = "";
        foreach (var p in PhotonNetwork.PlayerList)
        {
            if (
                p.CustomProperties.TryGetValue("PlayerQue", out object q)
                && (int)q == previousPlayerQue
            )
            {
                targetContainerName = p.NickName;
                break;
            }
        }

        Transform targetContainer = null;
        if (!string.IsNullOrEmpty(targetContainerName))
            targetContainer = GameObject.Find(targetContainerName)?.transform;

        // Eğer önceki oyuncuyu bulamazsan (Test ortamı vs.) genel kutuya koy
        if (targetContainer == null)
            targetContainer = dropTileContainer;

        if (targetContainer != null)
        {
            GameObject returnedTileObj = Instantiate(tilePrefab, targetContainer);
            TileUI tileUI = returnedTileObj.GetComponent<TileUI>();
            if (tileUI != null)
            {
                tileUI.SetTileData(tileToReturn);
                tileUI.FitToParent();
                dropTile = tileToReturn; // Tekrar çekilebilir yap
            }
        }
    }

    [PunRPC]
    public void RemoveTileFromPlayerListByValue(int playerQue, Tiles tileToRemove)
    {
        // 1. Doğru oyuncunun listesini seç
        List<Tiles> targetHand = null;
        if (playerQue == 1)
            targetHand = playerTiles1;
        else if (playerQue == 2)
            targetHand = playerTiles2;
        else if (playerQue == 3)
            targetHand = playerTiles3;
        else if (playerQue == 4)
            targetHand = playerTiles4;

        if (targetHand != null)
        {
            // --- DÜZELTME: REFERANS DEĞİL, DEĞER KONTROLÜ ---
            // Listeyi tara ve özellikleri (Renk, Numara, Tip) eşleşen İLK taşı bul
            Tiles foundTile = null;

            foreach (var t in targetHand)
            {
                // Joker kontrolü (Jokerse tipi Joker olmalı)
                if (tileToRemove.type == TileType.Joker)
                {
                    if (t.type == TileType.Joker)
                    {
                        foundTile = t;
                        break;
                    }
                }
                // Normal taş kontrolü
                else if (
                    t.color == tileToRemove.color
                    && t.number == tileToRemove.number
                    && t.type == tileToRemove.type
                )
                {
                    foundTile = t;
                    break;
                }
            }

            if (foundTile != null)
            {
                targetHand.Remove(foundTile);
                // Debug.Log($"[TileDistrubite] Taş listeden silindi: {tileToRemove.color} {tileToRemove.number}");

                // --- OYUN BİTİŞ KONTROLÜNÜ TETİKLE ---
                // Taş silindikten sonra elin boşalıp boşalmadığını kontrol etmeliyiz
                if (targetHand.Count == 0)
                {
                    // GameManager'da CheckGameStatus zaten her hamlede çalışıyor ama
                    // burası manuel bir silme olduğu için garantiye almak isteyebilirsin.
                }
            }
            else
            {
                Debug.LogWarning(
                    $"[HATA] Silinecek taş listede bulunamadı! {tileToRemove.color} {tileToRemove.number} (Player: {playerQue})"
                );
            }
        }
    }

    // TileDistrubite.cs içerisinde
    #region New Hand Reset and Redistribute
    [PunRPC]
    public void ResetTableAndRedistribute()
    {
        // 1. Sahnedeki TÜM TileUI ve görsel objeleri temizle
        TileUI[] allTilesInScene = FindObjectsOfType<TileUI>();
        foreach (TileUI tUI in allTilesInScene)
        {
            Destroy(tUI.gameObject);
        }

        // 2. Mantıksal listeleri ve eldeki taşları temizle
        allTiles.Clear();
        playerTiles1.Clear();
        playerTiles2.Clear();
        playerTiles3.Clear();
        playerTiles4.Clear();

        // Meld (açılan per) listelerini temizle
        meltedTiles1.Clear();
        meltedTiles2.Clear();
        meltedTiles3.Clear();
        meltedTiles4.Clear();

        // 3. Placeholder (yuva) ışıklarını ve durumlarını sıfırla
        Placeholder[] allPhs = FindObjectsOfType<Placeholder>();
        foreach (var ph in allPhs)
        {
            ph.available = false;
            ph.AvailableTileInfo = null;
        }

        // 4. MASTER CLIENT: Taşları yeniden üret ve dağıtımı başlat
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("Master Client: Yeni el için taşlar hazırlanıyor...");
            GeneratePlayerTiles(); // Taşları oluştur (106 taş + 2 sahte)
            ShuffleTiles(); // Karıştır, göstergeyi seç ve SyncShuffledTiles'ı tetikle
        }
    }
    #endregion
    #endregion
}
