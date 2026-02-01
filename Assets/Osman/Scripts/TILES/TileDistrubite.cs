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

        // --- [YENİ MANTIK BAŞLANGICI] ---

        // Eğer çekilen gösterge taşı "Sahte Okey" (Resimli) ise;
        if (indicatorTile.type == TileType.FakeJoker)
        {
            Debug.Log(
                "ÖZEL DURUM: Gösterge Sahte Okey geldi. Diğer Sahte Okey 'Gerçek Joker' oluyor."
            );
            // Yeni yazdığımız fonksiyonu çağır
            SetFakeJokerAsRealJoker();
        }
        else
        {
            // STANDART DURUM: Gösterge normal sayı.
            // Bir fazlasını bul, o sayıları Joker yap.

            int upperNumber = indicatorTile.number + 1;
            if (upperNumber > 13)
            {
                upperNumber = 1; // 13'ten sonra 1'e dön
            }

            // Eski fonksiyonu çağır
            UpdateFakeJokerTiles(upperNumber, indicatorTile.color);
        }
        // --- [YENİ MANTIK BİTİŞİ] ---

        // Sync the indicator tile across all clients
        photonView.RPC("SyncIndicatorTile", RpcTarget.All, indicatorTile);
    }

    // Bu fonksiyon SADECE gösterge taşı Sahte Okey (Resimli) olduğunda çalışır.
    private void SetFakeJokerAsRealJoker()
    {
        foreach (var tile in allTiles)
        {
            // Listede kalan (henüz dağıtılmamış) diğer sahte okeyi bul
            if (tile.type == TileType.FakeJoker)
            {
                // Bu taşı "Gerçek Joker" tipine çevir.
                // ScoreManager, Type.Joker olan taşı her şeyin yerine sayacaktır.
                tile.type = TileType.Joker;

                // Not: Rengini veya numarasını değiştirmemize gerek yok.
                // Çünkü bu taş artık "Wildcard" oldu, her renge ve her sayıya uyum sağlar.
            }

            // ÖNEMLİ: Normal sayı taşlarına (Type.Number) dokunmuyoruz.
            // Çünkü bu senaryoda hiçbir sayı taşı Joker olmamalı.
        }

        // Dağıtımı ve senkronizasyonu tetikle (Aynen diğer fonksiyondaki gibi)
        photonView.RPC("AssignPlayerQueue", RpcTarget.All);
        photonView.RPC("SyncShuffledTiles", RpcTarget.All, allTiles.ToArray());
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
        // 1. İşlem yapılacak listeyi seç
        List<Tiles> targetList = null;
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

        // Liste kontrolü ve İndeks güvenliği
        if (targetList != null && tileIndex >= 0 && tileIndex < targetList.Count)
        {
            // A. Yana Atılan Görseli Oluştur (Side Tile)
            // Bu, hem atan kişide hem de rakiplerde çalışır. Böylece herkes atılan taşı görür.
            InstatiateSideTiles(playerNumber, targetList[tileIndex]);

            // B. VERİYİ SİL (Herkes kendi hafızasındaki listeden silmeli)
            targetList.RemoveAt(tileIndex);

            // --- GÖRSEL SİLME İPTAL EDİLDİ ---
            // Buradaki tüm Destroy/GameObject arama kodlarını sildik.
            // Sebebi:
            // 1. Local Player için: TileUI zaten animasyonla siliyor. (Burada silersek ikiz taş gidiyor)
            // 2. Remote Player için: playerTileContainer "Benim" ıstakamdır.
            //    Rakip taş attı diye benim ıstakamdan taş arayıp silmemeli.
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

    [PunRPC]
    public void DeactivatePlayerTile(int playerQue, Tiles tileToDeactivate)
    {
        object localQue;
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out localQue))
        {
            if ((int)localQue != playerQue)
                return;
        }

        if (playerTileContainer == null)
            return;

        // Istakadaki tüm kutucukları (placeholder) gez
        for (int i = 0; i < playerTileContainer.childCount; i++)
        {
            Transform slot = playerTileContainer.GetChild(i);
            if (slot.childCount > 0)
            {
                TileUI ui = slot.GetChild(0).GetComponent<TileUI>();

                // Indicator (Gösterge) değilse kontrol et
                if (ui != null && !ui.isIndicatorTile)
                {
                    // ESKİ: Renk, Numara, Tip kontrolü
                    // YENİ: Sadece ID kontrolü
                    if (ui.tileDataInfo.id == tileToDeactivate.id)
                    {
                        // Sadece ıstaka içindeyse yok et
                        Destroy(ui.gameObject);
                        Debug.Log($"[SILME] Istakadaki taş ID ile silindi: {tileToDeactivate.id}");
                        return; // Bir tane sildik, işimiz bitti.
                    }
                }
            }
        }
    }

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

    // TileDistrubite.cs içindeki MeldTiles metodunu şu şekilde güncelle:
    [PunRPC]
    public void MeldTiles(int playerNumber, Tiles tileToRemove)
    {
        // 1. Masadaki görselleri oluştur (Tüm oyuncular için)
        InstatiateMeldTiles(playerNumber);

        // 2. İşlem yapılacak doğru listeyi seç
        List<Tiles> targetList = null;
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

        /* if (targetList != null)
         {
             // --- DÜZELTME: SADECE ID KONTROLÜ ---
             // Asla renk/sayı eşleşmesine (fallback) düşmemeli.
             // ID benzersizdir, varsa vardır, yoksa hata vermelidir.
 
             Tiles foundTile = targetList.FirstOrDefault(t => t.id == tileToRemove.id);
 
             if (foundTile != null)
             {
                 targetList.Remove(foundTile);
                 // Debug.Log($"[MELD] Taş ID ile silindi: {tileToRemove.id}");
             }
             else
             {
                 Debug.LogError(
                     $"[MELD HATASI] Oyuncu {playerNumber} elinde {tileToRemove.id} ID'li taş bulunamadı! (İkiz taş silinmesi engellendi)"
                 );
             }
         }*/

        // Temizlik işlemleri
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

    // TileDistrubite.cs içerisindeki GetAvailableTiles Metodu
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

                // Referans taş (renk ve numara hesaplaması için joker olmayan bir taş)
                var refTile = meld.FirstOrDefault(t => t.type != TileType.Joker);
                if (refTile != null)
                {
                    // Sol tarafa ekleme (Yeni taş, yeni ID olması normal)
                    if (minNumber > 1)
                    {
                        availableTiles.Add(
                            new Tiles(refTile.color, minNumber - 1, TileType.Number)
                        );
                    }

                    // Sağ tarafa ekleme (Yeni taş, yeni ID olması normal)
                    if (maxNumber < 13)
                    {
                        availableTiles.Add(
                            new Tiles(refTile.color, maxNumber + 1, TileType.Number)
                        );
                    }

                    // Joker Takası - KRİTİK DÜZELTME
                    if (hasJoker)
                    {
                        var jokerTile = meld.First(tile => tile.type == TileType.Joker);

                        // Yeni bir taş oluşturuyoruz ama eşleşme mantığında bu taşın
                        // "bir jokerin yerine geçeceği" bilgisini saklamalıyız.
                        Tiles swapTarget = new Tiles(
                            jokerTile.color,
                            jokerTile.number,
                            TileType.Number
                        );

                        // Takas edilecek taşın ID'sini masadaki jokerin ID'sine bağlayarak
                        // ScoreManager'ın doğru taşı bulmasını sağlıyoruz.
                        swapTarget.id = "SWAP_" + jokerTile.id;

                        availableTiles.Add(swapTarget);
                    }
                }
            }
        }
        // -------------------------------------------------------
        // 2. MULTI COLOR (Sayı Grubu)
        // -------------------------------------------------------
        else if (scoreManager.MultiColorCheck(meld))
        {
            if (meld.Count >= 3)
            {
                var refTile = meld.FirstOrDefault(t => t.type != TileType.Joker);
                if (refTile != null)
                {
                    int targetNumber = refTile.number;
                    var realColors = meld.Where(t => t.type != TileType.Joker)
                        .Select(t => t.color)
                        .ToList();

                    List<TileColor> allColors = new List<TileColor>
                    {
                        TileColor.yellow,
                        TileColor.blue,
                        TileColor.black,
                        TileColor.red,
                    };

                    var missingColors = allColors.Except(realColors).ToList();

                    // Eksik renkleri ekle
                    foreach (var color in missingColors)
                    {
                        availableTiles.Add(new Tiles(color, targetNumber, TileType.Number));
                    }

                    // Joker Takası varsa işaretle
                    if (meld.Any(t => t.type == TileType.Joker))
                    {
                        var jokerTile = meld.First(t => t.type == TileType.Joker);
                        // Çoklu renk perlerinde joker herhangi bir eksik rengin yerine geçebilir.
                        // Bu yüzden jokerin ID'sini koruyarak available listesine ekliyoruz.
                        foreach (var color in missingColors)
                        {
                            Tiles swapTarget = new Tiles(color, targetNumber, TileType.Number);
                            swapTarget.id = "SWAP_" + jokerTile.id;
                            availableTiles.Add(swapTarget);
                        }
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
                var jokerTile = meld.First(tile => tile.type == TileType.Joker);
                var refTile = meld.FirstOrDefault(t => t.type != TileType.Joker);
                if (refTile != null)
                {
                    Tiles swapTarget = new Tiles(refTile.color, refTile.number, TileType.Number);
                    swapTarget.id = "SWAP_" + jokerTile.id;
                    availableTiles.Add(swapTarget);
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

    public void RecalculateAllAvailableSlots()
    {
        if (scoreManager == null)
            scoreManager = FindObjectOfType<ScoreManager>();

        // Oyuncunun elini açıp açmadığını kontrol et
        bool hasOpenedHand = (
            scoreManager != null && (scoreManager.hasOpenedSeries || scoreManager.hasOpenedPairs)
        );

        // ----------------------------------------------------------------------
        // ADIM 1: LİSTEYİ TEMİZLE VE GÖRSEL HAZIRLIK
        // ----------------------------------------------------------------------
        availableTiles.Clear();

        List<List<Tiles>> currentBoardMelds = new List<List<Tiles>>();
        List<(List<Tiles> per, int row, ScoreManager.MeldType type)> meldDetails =
            new List<(List<Tiles>, int, ScoreManager.MeldType)>();

        // Önce sahnede yanan tüm ışıkları söndür (Resetleme)
        foreach (var player in PhotonNetwork.PlayerList)
        {
            Transform meldContainer = GameObject.Find(player.NickName + " meld")?.transform;
            if (meldContainer == null)
                continue;

            foreach (Transform typeContainer in meldContainer)
            {
                foreach (Transform placeholder in typeContainer)
                {
                    Placeholder phComponent = placeholder.GetComponent<Placeholder>();
                    if (phComponent != null)
                    {
                        if (placeholder.childCount > 0)
                        {
                            phComponent.available = false;
                            phComponent.AvailableTileInfo = null;
                        }
                    }
                }
            }
        }

        // ----------------------------------------------------------------------
        // ADIM 2: MASADAKİ TAŞLARI TARA (MANTIKSAL HESAPLAMA)
        // ----------------------------------------------------------------------
        // DÜZELTME: Bu tarama işlemi artık "hasOpenedHand" kontrolünden ÖNCE yapılıyor.
        // Böylece Master Client elini açmasa bile "availableTiles" listesi doluyor ve
        // ceza kontrolünü doğru yapabiliyor.

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

        // ----------------------------------------------------------------------
        // ADIM 3: MANTIKSAL LİSTEYİ DOLDUR (availableTiles)
        // ----------------------------------------------------------------------
        foreach (var per in currentBoardMelds)
        {
            var availableFromPer = GetAvailableTiles(per);
            foreach (var tile in availableFromPer)
            {
                // ID önemli değil, Renk ve Sayı olarak listede yoksa ekle
                bool exists = availableTiles.Any(t =>
                    t.color == tile.color && t.number == tile.number && t.type == tile.type
                );
                if (!exists)
                    availableTiles.Add(tile);
            }
        }

        // ----------------------------------------------------------------------
        // ADIM 4: GÖRSEL GÜNCELLEME (SADECE ELİNİ AÇANLAR İÇİN)
        // ----------------------------------------------------------------------
        // Ceza sistemi için gerekli veriyi yukarıda hazırladık.
        // Ancak oyuncu elini açmadıysa, masadaki yerlerin yeşil yanmasını (available olmasını) engelliyoruz.

        if (!hasOpenedHand)
        {
            // Debug.Log("Oyuncu elini açmadığı için görsel placeholder güncellemesi atlanıyor.");
            return;
        }

        foreach (var detail in meldDetails)
        {
            // ScoreManager'daki ilgili metodu çağırarak görselleri güncelle
            scoreManager.UpdateAvailableForPlaceholders(detail.per, detail.row);
        }
    }

    // TileDistrubite.cs

    [PunRPC]
    public void RemoveActiveTileFromPlayerList(int playerQue, Tiles tileToRemove)
    {
        List<Tiles> targetList = null;
        switch (playerQue)
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
            // --- DÜZELTME: ID KONTROLÜNE GEÇİŞ ---
            // ESKİ KOD: t.color == tileToRemove.color && t.number == tileToRemove.number
            // YENİ KOD: t.id == tileToRemove.id

            Tiles foundTile = targetList.FirstOrDefault(t => t.id == tileToRemove.id);

            if (foundTile != null)
            {
                targetList.Remove(foundTile);
                Debug.Log(
                    $"[SYNC] Oyuncu {playerQue} listesinden ID:{tileToRemove.id} ({tileToRemove.color} {tileToRemove.number}) başarıyla silindi."
                );
            }
            else
            {
                // Eğer ID ile bulamazsak, sakın "benzerini" silmeye kalkma.
                Debug.LogError(
                    $"[SYNC HATASI] Oyuncu {playerQue} elinde {tileToRemove.id} ID'li taş YOK! İşlem iptal edildi."
                );
            }
        }
    }

    [PunRPC]
    public void AddTileToPlayerHand(int playerQue, Tiles tileData)
    {
        // -----------------------------------------------------------
        // 1. VERİ KISMI (DÜZELTME BURADA)
        // -----------------------------------------------------------
        // ESKİ KOD: if (PhotonNetwork.IsMasterClient) { ... }
        // YENİ KOD: Kontrolü kaldırdık. Herkes bu veriyi kendi listesine eklemeli.

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
            // Debug.Log($"[VERİ EKLENDİ] Oyuncu {playerQue} listesine Joker eklendi. Yeni adet: {targetHand.Count}");
        }

        // -----------------------------------------------------------
        // 2. GÖRSEL KISMI (AYNEN KALIYOR)
        // -----------------------------------------------------------
        object localQue;
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out localQue))
        {
            // Eğer bu oyuncu bensem, ıstakama görseli ekle
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

            // Slotun içinde pasif (gizlenmiş ama silinmemiş) bir obje varsa hemen temizle
            if (slot.childCount > 0)
            {
                GameObject child = slot.GetChild(0).gameObject;
                if (!child.activeSelf)
                {
                    // Destroy yerine DestroyImmediate kullanarak slotun anında boşalmasını sağla
                    DestroyImmediate(child);
                }
            }

            // Slot şimdi gerçekten boşsa
            if (slot.childCount == 0)
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
        Debug.LogWarning("Istakada yer yok! Alınan Joker görseli oluşturulamadı.");
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
        // 1. Hedef Oyuncuyu Bul
        Photon.Realtime.Player targetPlayer = PhotonNetwork.PlayerList.FirstOrDefault(p =>
            p.CustomProperties.TryGetValue("PlayerQue", out object q) && (int)q == ownerQue
        );
        if (targetPlayer == null)
            return;

        GameObject meldContainer = GameObject.Find(targetPlayer.NickName + " meld");
        if (meldContainer == null)
            return;

        // 2. Doğru Yuvayı Bul
        Transform rowTransform = meldContainer.transform.GetChild(meldTypeInt);
        if (placeholderIndex >= rowTransform.childCount)
            return;
        Transform targetPlaceholder = rowTransform.GetChild(placeholderIndex);

        // 3. Sadece MASADAKİ Görseli Oluştur
        if (targetPlaceholder.childCount > 0)
            Destroy(targetPlaceholder.GetChild(0).gameObject);

        GameObject tileObj = Instantiate(tilePrefab, targetPlaceholder);
        tileObj.name = "PERMANENT_MELD_TILE";

        TileUI tileUI = tileObj.GetComponent<TileUI>();
        if (tileUI != null)
        {
            tileUI.SetTileData(tileData);
            tileUI.FitToParent();
        }

        // NOT: RemoveActiveTileFromPlayerList burada ÇAĞRILMAZ.
        // Çift taş silinme hatasının ana sebebi buradaki fazladan çağrıydı.

        RecalculateAllAvailableSlots();
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
        // ----------------------------------------------------------------
        // 1. VERİ SİLME (LİSTEDEN) - SIKI ID KONTROLÜ
        // ----------------------------------------------------------------
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
            // Sadece ID ile eşleşen taşı bul
            Tiles foundTile = targetHand.FirstOrDefault(t => t.id == tileToRemove.id);

            if (foundTile != null)
            {
                targetHand.Remove(foundTile);
            }
            else
            {
                // Eğer ID ile bulunamazsa ASLA Rengine/Numarasına bakıp silme!
                // Çünkü elinde aynısından bir tane daha olabilir.
                Debug.LogWarning(
                    $"[SYNC KORUMA] Silinecek taş ID ile bulunamadı ({tileToRemove.id}). İkiz taş silinmemesi için işlem iptal edildi."
                );
            }
        }

        // ----------------------------------------------------------------
        // 2. GÖRSEL SİLME (SADECE O OYUNCUNUN EKRANINDA)
        // ----------------------------------------------------------------
        object localQue;
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out localQue))
        {
            if ((int)localQue == playerQue && playerTileContainer != null)
            {
                GameObject tileToDelete = null;

                foreach (Transform placeholder in playerTileContainer)
                {
                    if (placeholder.childCount > 0)
                    {
                        GameObject tileObj = placeholder.GetChild(0).gameObject;
                        TileUI ui = tileObj.GetComponent<TileUI>();

                        // --- KRİTİK DEĞİŞİKLİK ---
                        // "else if" ile değer kontrolü yapan kısmı SİLDİK.
                        // Sadece ID eşleşirse silinecek.
                        if (ui != null && ui.tileDataInfo.id == tileToRemove.id)
                        {
                            tileToDelete = tileObj;
                            break;
                        }
                    }
                }

                if (tileToDelete != null)
                {
                    Destroy(tileToDelete);
                }
            }
        }
    }

    // Eşleştirme Yardımcısı (Eğer sınıfta yoksa en alta ekle)
    private bool IsTileMatch(Tiles t1, Tiles t2)
    {
        if (t1 == null || t2 == null)
            return false;

        // Joker kontrolü
        if (t1.type == TileType.Joker && t2.type == TileType.Joker)
            return true;

        // Normal kontrol
        return (t1.color == t2.color && t1.number == t2.number && t1.type == t2.type);
    }

    // TileDistrubite.cs içerisinde
    #region New Hand Reset and Redistribute
    // TileDistrubite.cs içine

    [PunRPC]
    public void ResetTableAndRedistribute()
    {
        Debug.Log("MASA SIFIRLANIYOR: Tüm listeler ve görseller temizleniyor...");

        // 1. Sahnedeki TÜM TileUI (Taş) nesnelerini bul ve yok et
        TileUI[] allTilesInScene = FindObjectsOfType<TileUI>();
        foreach (TileUI tUI in allTilesInScene)
        {
            if (tUI.gameObject != null)
                Destroy(tUI.gameObject);
        }

        // Yere atılan taşların referanslarını tutan listeyi temizle (Görseller yukarıda silindi ama liste dolu kalmasın)
        droppedTiles.Clear();
        dropTile = null;

        // 2. Ana taş havuzunu ve oyuncu ellerini temizle
        allTiles.Clear();
        playerTiles1.Clear();
        playerTiles2.Clear();
        playerTiles3.Clear();
        playerTiles4.Clear();

        // 3. Meld (Açılan Per) VERİLERİNİ temizle
        meltedTiles1.Clear();
        meltedTiles2.Clear();
        meltedTiles3.Clear();
        meltedTiles4.Clear();

        // --- KRİTİK DÜZELTME: POZİSYON LİSTELERİNİ TEMİZLE ---
        // Eğer bunları temizlemezsen, yeni eldeki taşları eski elin koordinatlarına koymaya çalışır.
        meltedTilesPositions1.Clear();
        meltedTilesPositions2.Clear();
        meltedTilesPositions3.Clear();
        meltedTilesPositions4.Clear();

        validMeltedTiles.Clear();
        positions.Clear();

        // Available (İşlek) hesaplamalarını sıfırla
        availableTiles.Clear();
        activePlacements.Clear();

        // 4. Placeholder (yuva) ışıklarını ve durumlarını sıfırla
        Placeholder[] allPhs = FindObjectsOfType<Placeholder>();
        foreach (var ph in allPhs)
        {
            ph.available = false;
            ph.AvailableTileInfo = null;
            // Eğer placeholder içinde child kaldıysa (Destroy'dan kaçan) onu da temizle
            foreach (Transform child in ph.transform)
            {
                Destroy(child.gameObject);
            }
        }

        // 5. MASTER CLIENT: Taşları yeniden üret ve dağıtımı başlat
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("Master Client: Yeni el için taşlar hazırlanıyor...");
            GeneratePlayerTiles();
            ShuffleTiles();
        }
    }
    #endregion
    #endregion
}
