using System;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon.StructWrapping;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class ScoreManager : MonoBehaviourPunCallbacks
{
    public Dictionary<int, int> playerScores; // Oyuncu ID'si ve puanı
    private Transform playerMeldContainers;
    Transform pairPerPlaceHolder;
    private Transform[] pairPerPlaceHolders;
    Transform numberPerPlaceHolder;
    private Transform[] numberPerPlaceHolders; // Player tile placeholders
    Transform colorPerPlaceHolder;
    [SerializeField] private Transform[] colorPerPlaceHolders;
    private TileDistribute _tileDistribute; // Taşları yöneten sınıf
    [SerializeField] private TurnManager turnManager;
    public Transform playerTileContainer; // Oyuncu taşı bölmesi
    public GameObject tilePrefab;

    // Yeni değişkenler
    [Header("Per Count and Total Score")]
    public int totalPerCount; // Toplam per sayısı
    public int totalScore; // Toplam puan
    public int pairTotalScore;
    public int pairTotalPerCount;
    #region GENERATE_METHODS
    private void Start()
    {
        Player player = PhotonNetwork.LocalPlayer;

        turnManager = GameObject.Find("TurnManager").GetComponent<TurnManager>();
        _tileDistribute = GameObject.Find("TileManager(Clone)").GetComponent<TileDistribute>();
        playerTileContainer = GameObject.Find("PlayerTileContainer").transform;
        playerMeldContainers = GameObject.Find(player.NickName + " meld").transform;
        if (playerMeldContainers != null)
        {
            colorPerPlaceHolder = playerMeldContainers.GetChild(0);
            numberPerPlaceHolder = playerMeldContainers.GetChild(1);
            pairPerPlaceHolder = playerMeldContainers.GetChild(2);
        }

        turnManager.StartGame();
        playerScores = new Dictionary<int, int>();

        InitializeMeldPlaceholders();
    }
    private void InitializeMeldPlaceholders()
    {
        int placeholderCount = numberPerPlaceHolder.childCount;
        numberPerPlaceHolders = new Transform[placeholderCount];

        int placeholderCount2 = colorPerPlaceHolder.childCount;
        colorPerPlaceHolders = new Transform[placeholderCount2];

        int placeholderCount3 = pairPerPlaceHolder.childCount;
        pairPerPlaceHolders = new Transform[placeholderCount3];
        for (int i = 0; i < placeholderCount2; i++)
        {
            colorPerPlaceHolders[i] = colorPerPlaceHolder.GetChild(i);
        }

        for (int i = 0; i < placeholderCount; i++)
        {
            numberPerPlaceHolders[i] = numberPerPlaceHolder.GetChild(i);
        }

        for (int i = 0; i < placeholderCount3; i++)
        {
            pairPerPlaceHolders[i] = pairPerPlaceHolder.GetChild(i);
        }
    }
    public void UpdatePlayerScore(int playerId, int score)
    {
        if (playerScores.ContainsKey(playerId))
        {
            playerScores[playerId] += score; // Mevcut puanı güncelle
        }
        else
        {
            playerScores[playerId] = score; // Yeni oyuncu için puanı ayarla
        }

        // Photon Custom Properties ile puanı güncelle
        UpdatePlayerCustomProperties(playerId);
    }
    private void UpdatePlayerCustomProperties(int playerId)
    {
        Photon.Realtime.Player player = PhotonNetwork.CurrentRoom.Players[playerId];
        player.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "PlayerScore", playerScores[playerId] } });
    }
    private void UpdateAvailableColumns(int rowIndex, List<Tiles> per)
    {
        foreach (var tile in per)
        {
            int columnIndex = rowIndex * 13 + (tile.number - 1); // Taşın numarasına göre sütun indeksini al
            if (columnIndex < availableColumns.Length)
            {
                availableColumns[columnIndex] = false; // Bu sütunu kullanılmaz yap
            }
        }
    }
    #endregion






    #region PER_CONTROL_METHODS







    #region For all per groups check
    public List<List<Tiles>> validPerss = new List<List<Tiles>>();
    public List<List<Tiles>> GetSplittedGroups()
    {
        List<List<Tiles>> perGroups = new List<List<Tiles>>();
    
        // Process both sections of player tile containers (0-14 and 15-29)
        for (int startIndex = 0; startIndex < 30; startIndex += 15)
        {
            bool newSplittedGroup = true;
        
            for (int i = startIndex; i < startIndex + 15; i++)
            {
                Transform container = playerTileContainer.GetChild(i);
                bool hasActiveTile = container.childCount > 0 && container.GetChild(0).gameObject.activeSelf;
            
                if (hasActiveTile)
                {
                    if (newSplittedGroup)
                    {
                        perGroups.Add(new List<Tiles>());
                        newSplittedGroup = false;
                    }
                
                    perGroups.Last().Add(container.GetChild(0).GetComponent<TileUI>().tileDataInfo);
                }
                else
                {
                    newSplittedGroup = true;
                }
            }
        }
    
        return perGroups;
    }
    public void CheckForPer()
    {
        // Get player ID
        Player player = PhotonNetwork.LocalPlayer;
        player.CustomProperties.TryGetValue("PlayerQue", out object playerId);
        int playerIdInt = (int)playerId;
    
        // Get per groups
        var groups = GetSplittedGroups();
        Debug.Log($"Checking per groups... {groups.Count} groups found.");
    
        // Reset counters
        int score = 0;
        int pairScore = 0;
        int perCount = 0;
        int pairPerCount = 0;
    
        // Track counted pers to avoid duplicates
        HashSet<List<Tiles>> countedPers = new HashSet<List<Tiles>>();
        validPerss.Clear();
    
        foreach (var per in groups)
        {
            if (!ControlPer(new List<List<Tiles>> { per })) continue;
            if (countedPers.Contains(per)) continue;
        
            // Add valid per to counted list
            countedPers.Add(per);
            validPerss.Add(per);
        
            // Calculate score based on per type
            if (CheckForDoublePer(per))
            {
                pairPerCount++;
                pairScore += CalculateDoublePerScore(per);
            }
            else
            {
                perCount++;
                score += CalculateGroupScore(per);
            }
        
            UpdatePlayerScore(playerIdInt, score);
        }
    
        // Update total scores
        totalScore = score;
        pairTotalScore = pairScore;
        totalPerCount = countedPers.Count;
        pairTotalPerCount = pairPerCount;
    
        Debug.Log($"Total Valid Per Count: {totalPerCount}, Total Score: {totalScore}");
    }
    #endregion
    #region Is pers valid or not
    public bool ControlPer(List<List<Tiles>> perGroups)
{
    foreach (var per in perGroups)
    {
        Debug.Log($"This per has {per.Count} tiles.");
        
        if (IsSingleColor(per))
        {
            if (SingleColorCheck(per))
            {
                Debug.Log("SingleColor per found.");
                return true;
            }
            
            if (CheckForDoublePer(per))
            {
                Debug.Log("Double per found.");
                return true;
            }
        }
        else if (MultiColorCheck(per))
        {
            Debug.Log("MultiColor per found.");
            return true;
        }
    }
    
    return false;
}


    public bool IsSingleColor(List<Tiles> tiles)
    {

        TileColor? firstColor = null;
        bool isFirstJoker = false;
        if (tiles.Count > 0 && tiles[0].type == TileType.Joker)
        {
            isFirstJoker = true;
        }

        foreach (var tile in tiles)
        {
            if (isFirstJoker && tile == tiles[0]) continue;

            if (firstColor == null) firstColor = tile.color;
            else if (firstColor == tile.color) continue;
            else if (tile.type == TileType.Joker) continue;
            else return false;
        }
        return true;
    }
    public bool CheckForDoublePer(List<Tiles> tiles)
    {
        // Çift per kontrolü için taş sayısı 2 olmalı
        if (tiles.Count != 2)
            return false;

        Tiles normalTile = null;
        List<Tiles> jokerStones = new List<Tiles>();

        // Taşları kontrol et
        foreach (var tile in tiles)
        {
            if (tile.type == TileType.Joker)
            {
                jokerStones.Add(tile); // Joker taşını ekle
            }
            else
            {
                // Normal taş
                if (normalTile == null)
                {
                    normalTile = tile; // İlk normal taşı al
                }
                else if (normalTile.number != tile.number)
                {
                    // Eğer iki normal taşın numarası farklıysa, çift per değil
                    return false;
                }
            }
        }

        // Eğer iki joker varsa, bu da bir çift per sayılır
        if (jokerStones.Count == 2)
        {
            // Joker taşlarının numarasını normal taşın numarasına eşitle
            foreach (var joker in jokerStones)
            {
                joker.number = normalTile.number; // Normal taşın numarasını joker taşına ata
            }
            return true;
        }

        // Eğer bir joker ve bir normal taş varsa, bu da bir çift per sayılır
        if (jokerStones.Count == 1 && normalTile != null)
        {
            // Joker taşının numarasını normal taşın numarasına eşitle
            jokerStones[0].number = normalTile.number; // Normal taşın numarasını joker taşına ata
            jokerStones[0].color = normalTile.color;
            return true;
        }

        // Normal taşlar aynı numaraya sahipse, çift per
        return normalTile != null;
    }
    public bool SingleColorCheck(List<Tiles> tiles)
    {
        if (tiles.Count < 3)
        {
            return false;
        }

        int[] pattern = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };
        int[] revPattern = pattern.Reverse().ToArray();

        // İlk desen kontrolü
        if (CheckPattern(tiles, pattern) || CheckPattern(tiles, revPattern))
        {
            return true;
        }
        return false;
    }
    private bool CheckPattern(List<Tiles> tiles, int[] pattern)
    {
        for (int i = 0; i < pattern.Length; i++)
        {
            bool valid = true;
        
            for (int j = 0; j < tiles.Count; j++)
            {
                // Get expected number based on pattern
                int expectedNumber = pattern[Math.Min(i + j, pattern.Length - 1)];
                Tiles currentTile = tiles[j];
            
                if (currentTile.type == TileType.Joker)
                {
                    // Handle joker logic
                    bool isValidJoker = HandleJokerInPattern(currentTile, tiles, j, expectedNumber);
                    if (!isValidJoker)
                    {
                        valid = false;
                        break;
                    }
                }
                else if (currentTile.number != expectedNumber)
                {
                    valid = false;
                    break;
                }
            }
        
            if (valid) return true;
        }
    
        return false;
    }
    
    private bool HandleJokerInPattern(Tiles jokerTile, List<Tiles> tiles, int jokerIndex, int expectedNumber)
    {
        // Joker is first tile
        if (jokerIndex == 0)
        {
            if (tiles.Count > 1 && (tiles[1].number == expectedNumber + 1 || tiles[1].number == expectedNumber - 1))
            {
                jokerTile.number = expectedNumber;
                jokerTile.color = tiles[1].color;
                return true;
            }
        }
        // Joker is last tile
        else if (jokerIndex == tiles.Count - 1)
        {
            if (tiles[jokerIndex - 1].number == expectedNumber - 1 || tiles[jokerIndex - 1].number == expectedNumber + 1)
            {
                jokerTile.number = expectedNumber;
                jokerTile.color = tiles[jokerIndex - 1].color;
                return true;
            }
        }
        // Joker is in the middle
        else
        {
            bool validWithPrev = tiles[jokerIndex - 1].number == expectedNumber - 1 || tiles[jokerIndex - 1].number == expectedNumber + 1;
            bool validWithNext = tiles[jokerIndex + 1].number == expectedNumber + 1 || tiles[jokerIndex + 1].number == expectedNumber - 1;
        
            if (validWithPrev)
            {
                jokerTile.number = expectedNumber;
                jokerTile.color = tiles[jokerIndex - 1].color;
                return true;
            }
        
            if (validWithNext)
            {
                jokerTile.number = expectedNumber;
                jokerTile.color = tiles[jokerIndex + 1].color;
                return true;
            }
        }
    
        return false;
    }
    public bool MultiColorCheck(List<Tiles> tiles)
    {
        if (tiles.Count < 3 || tiles.Count > 4)
            return false;
    
        // Find a non-joker tile to use as reference
        Tiles notJokerStone = tiles.FirstOrDefault(t => t.type != TileType.Joker);
        List<Tiles> jokerStones = tiles.Where(t => t.type == TileType.Joker).ToList();
    
        if (notJokerStone == null && jokerStones.Count < 2)
            return false;
    
        // Check that all non-joker tiles have same number
        bool allSameNumber = tiles.All(t => t.type == TileType.Joker || t.number == notJokerStone.number);
        if (!allSameNumber)
            return false;
    
        // Check that all colors are unique
        var uniqueColors = tiles.Where(t => t.type != TileType.Joker)
            .GroupBy(t => t.color)
            .Select(g => g.First());
                          
        if (uniqueColors.Count() + jokerStones.Count != tiles.Count)
            return false;
    
        // Assign number to jokers
        foreach (var joker in jokerStones)
        {
            joker.number = notJokerStone.number;
        }
    
        return true;
    }
    #endregion
    #region Calulate functions
    private int CalculateGroupScore(List<Tiles> tiles)
    {
        int score = 0;
        foreach (var tile in tiles)
        {

            score += tile.number; // Normal taşın puanını ekle

        }
        return score;
    }
    private int CalculateDoublePerScore(List<Tiles> tiles)
    {
        int score = 0;
        foreach (var tile in tiles)
        {
            score += tile.number; // Normal taşın puanını ekle

        }
        return score;
    }
    #endregion
    #endregion



    #region MELD_PER_METHODS





    #region Button functions
    public bool isMeldPair = false;
    public bool isMeldStraight = false;

    int GetPlayerQue()
    {

        PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object queueValue);

        return (int)queueValue;
    }

    public void OnButtonClick()
    {
        // Oyuncunun sırasını kontrol et
        if (turnManager.canDrop == true)
        {  // Belirli bir puan değerinden fazla mı?
            if (totalScore >= 3) // Örneğin, 50 puan
            {
                PlaceValidPers(validPerss);

            }
            else
            {
                Debug.Log("Yeterli puan yok.");
            }
        }
        else
        {
            Debug.Log("Oyuncunun sırası degil.");
        }
    }
    public void OnPairButtonClick()
    {
        if (turnManager.canDrop == true)
        {  // Belirli bir puan değerinden fazla mı?
            if (pairTotalScore >= 2) // Örneğin, 50 puan
            {
                PlacePairPers(validPerss);
            }
            else
            {
                Debug.Log("Yeterli puan yok.");
            }
        }
        else
        {
            Debug.Log("Oyuncunun sırası degil.");
        }
    }
    public void OnTakeBackButtonClick()
    {
        if (turnManager.canDrop == true)
        {
            TakeBackPers();
        }
        else
        {
            Debug.Log("Oyuncunun sırası degil.");
        }
    }

    public void OnActiveButtonClick()
    {
        if (turnManager.canDrop == true)
        {
            ActivePers();
        }
        else
        {
            Debug.Log("Oyuncunun sırası degil.");
        }
    }
    #endregion
    #region Place pers on locale
    public bool[] occupiedRowsNumber = new bool[4];
    public bool[] occupiedRows = new bool[4];
    public bool[] occupiedRowsPair = new bool[8];
    public List<GameObject> meldTileGO = new List<GameObject>();
    public List<Tiles> meldedTiles = new List<Tiles>();

    private void PlaceValidPers(List<List<Tiles>> validPers)
    {
        List<Vector2Int> positions = new List<Vector2Int>();

        // Renkli perler için yerleştirme
        foreach (var per in validPers)
        {
            if (IsSingleColor(per) && SingleColorCheck(per)) // Renkli per kontrolü
            {
                int rowIndex = -1; // Satır indeksini başlat
                for (int r = 0; r < 4; r++) // 4 satır var
                {
                    if (occupiedRows[r] == false)
                    {
                        bool allColumnsFull = true; // O sıradaki tüm sütunların dolu olup olmadığını kontrol et
                        for (int c = 0; c < 13; c++) // Her satırda 13 sütun var
                        {
                            int columnIndex = r * 13 + c; // Sütun indeksini hesapla
                            if (columnIndex < colorPerPlaceHolders.Length && colorPerPlaceHolders[columnIndex].childCount == 0)
                            {
                                allColumnsFull = false;
                                break;
                            }
                        }
                        if (allColumnsFull == false) // Eğer o sıradaki sütunlar dolu değilse
                        {
                            // Bu satırı seç
                            rowIndex = r;
                            break;
                        }
                    }
                }

                // Eğer uygun bir satır bulunduysa, taşları yerleştir
                if (rowIndex != -1)
                {
                    foreach (var tile in per)
                    {
                        int columnIndex = rowIndex * 13 + (tile.number - 1); // Taşın numarasına göre sütun indeksini al
                        if (columnIndex < colorPerPlaceHolders.Length)
                        {
                            // Taşı yerleştir
                            positions.Add(new Vector2Int(rowIndex, columnIndex));

                            GameObject tileInstance = Instantiate(tilePrefab, colorPerPlaceHolders[columnIndex]);
                            meldTileGO.Add(tileInstance);
                            TileUI tileUI = tileInstance.GetComponent<TileUI>();
                            tileUI.CheckRowColoumn(rowIndex, columnIndex);

                            if (tileUI != null)
                            {
                                tileUI.SetTileData(tile);
                            }
                            else
                            {
                                Debug.LogError("TileUI component missing on tilePrefab.");
                            }
                            int playerTileIndex = _tileDistribute.GetPlayerTiles().IndexOf(tile);
                            int playerQue = GetPlayerQue();
                            _tileDistribute.photonView.RPC("DeactivatePlayerTile", RpcTarget.AllBuffered, playerQue, playerTileIndex);

                        }
                    }
                    occupiedRows[rowIndex] = true;

                    // Burada boyut kontrolü yapıyoruz
                    if (per.Count != positions.Count)
                    {
                        Debug.LogError("Valid melted tiles and positions count mismatch!");
                        return; // İşlemi durdur
                    }
                    UpdateAvailableForPlaceholders(per, rowIndex);
                    _tileDistribute.photonView.RPC("MergeValidpers", RpcTarget.AllBuffered, per, GetPlayerQue(), positions);
                    positions.Clear(); // Her per için pozisyonları temizle
                }
                else
                {
                    Debug.Log("Tüm renkli sütunlar dolu.");
                }

            }

            else
            {
                Debug.Log("Renkli per bulunamadı");
            }
        }

        // Sayı perleri için yerleştirme
        foreach (var per in validPers)
        {
            if (MultiColorCheck(per)) // Sayı per kontrolü
            {
                int rowIndex = -1; // Satır indeksini başlat
                for (int r = 0; r < 4; r++) // 4 satır var
                {
                    if (occupiedRowsNumber[r] == false)
                    {
                        bool allColumnsFull = true; // O sıradaki tüm sütunların dolu olup olmadığını kontrol et
                        for (int c = 0; c < 4; c++) // Her satırda 4 sütun var
                        {
                            int columnIndex = r * 4 + c; // Sütun indeksini hesapla
                            if (columnIndex < numberPerPlaceHolder.childCount && numberPerPlaceHolder.GetChild(columnIndex).childCount == 0)
                            {
                                allColumnsFull = false;
                                break;
                            }
                        }

                        if (!allColumnsFull) // Eğer o sıradaki sütunlar dolu değilse
                        {
                            rowIndex = r; // Bu satırı seç
                            break;
                        }
                    }
                }

                // Eğer uygun bir satır bulunduysa, taşları yerleştir
                if (rowIndex != -1)
                {
                    foreach (var tile in per)
                    {
                        int tileIndex = per.IndexOf(tile);
                        int columnIndex = rowIndex * 4 + (tileIndex); // Taşın numarasına göre sütun indeksini al
                        if (columnIndex < numberPerPlaceHolder.childCount)
                        {
                            // Taşı yerleştir
                            positions.Add(new Vector2Int(rowIndex, columnIndex));
                            GameObject tileInstance = Instantiate(tilePrefab, numberPerPlaceHolders[columnIndex]);
                            TileUI tileUI = tileInstance.GetComponent<TileUI>();
                            meldTileGO.Add(tileInstance);
                            tileUI.CheckRowColoumn(rowIndex, columnIndex);

                            if (tileUI != null)
                            {
                                tileUI.SetTileData(tile);
                            }
                            else
                            {
                                Debug.LogError("TileUI component missing on tilePrefab.");
                            }
                            int playerTileIndex = _tileDistribute.GetPlayerTiles().IndexOf(tile);
                            int playerQue = GetPlayerQue();
                            _tileDistribute.photonView.RPC("DeactivatePlayerTile", RpcTarget.AllBuffered, playerQue, playerTileIndex);
                        }
                    }
                    occupiedRowsNumber[rowIndex] = true;
                    UpdateAvailableForPlaceholders(per, rowIndex);
                    // Burada boyut kontrolü yapıyoruz
                    if (per.Count != positions.Count)
                    {
                        Debug.LogError("Valid melted tiles and positions count mismatch!");
                        return; // İşlemi durdur
                    }

                    _tileDistribute.photonView.RPC("MergeValidpers", RpcTarget.AllBuffered, per, GetPlayerQue(), positions);
                    positions.Clear(); // Her per için pozisyonları temizle
                }
                else
                {
                    Debug.Log("Tüm sayı sütunları dolu.");
                }
            }
            else
            {
                Debug.Log("Sayı per bulunamadı");
            }
        }
    }
    private void PlacePairPers(List<List<Tiles>> validPers)
    {
        List<Vector2Int> positions = new List<Vector2Int>();
        foreach (var per in validPers)

        {
            if (IsSingleColor(per) & CheckForDoublePer(per))
            {
                int rowIndex = -1; // Satır indeksini başlat
                for (int r = 0; r < 8; r++) // 8 satır var
                {
                    if (occupiedRowsPair[r] == false)
                    {
                        bool allColumnsFull = true; // O sıradaki tüm sütunların dolu olup olmadığını kontrol et
                        for (int c = 0; c < 2; c++) // Her satırda 4 sütun var
                        {
                            int columnIndex = r * 2 + c; // Sütun indeksini hesapla
                            if (columnIndex < pairPerPlaceHolder.childCount && pairPerPlaceHolder.GetChild(columnIndex).childCount == 0)
                            {
                                allColumnsFull = false;
                                break;
                            }
                        }

                        if (!allColumnsFull) // Eğer o sıradaki sütunlar dolu değilse
                        {
                            rowIndex = r; // Bu satırı seç
                            break;
                        }
                    }
                }

                if (rowIndex != -1)
                {
                    foreach (var tile in per)
                    {
                        int tileIndex = per.IndexOf(tile);
                        int columnIndex = rowIndex * 2 + (tileIndex); // Taşın numarasına göre sütun indeksini al
                        if (columnIndex < pairPerPlaceHolder.childCount)
                        {
                            // Taşı yerleştir
                            positions.Add(new Vector2Int(rowIndex, columnIndex));

                            GameObject tileInstance = Instantiate(tilePrefab, pairPerPlaceHolders[columnIndex]);
                            TileUI tileUI = tileInstance.GetComponent<TileUI>();
                            meldTileGO.Add(tileInstance);
                            tileUI.CheckRowColoumn(rowIndex, columnIndex);

                            if (tileUI != null)
                            {
                                tileUI.SetTileData(tile);
                            }
                            else
                            {
                                Debug.LogError("TileUI component missing on tilePrefab.");
                            }
                            int playerTileIndex = _tileDistribute.GetPlayerTiles().IndexOf(tile);
                            int playerQue = GetPlayerQue();
                            _tileDistribute.photonView.RPC("DeactivatePlayerTile", RpcTarget.AllBuffered, playerQue, playerTileIndex);
                        }
                    }
                    occupiedRowsPair[rowIndex] = true;
                    UpdateAvailableForPlaceholders(per, rowIndex);
                    // Burada boyut kontrolü yapıyoruz
                    if (per.Count != positions.Count)
                    {
                        Debug.LogError("Valid melted tiles and positions count mismatch!");
                        return; // İşlemi durdur
                    }

                    _tileDistribute.photonView.RPC("MergeValidpers", RpcTarget.AllBuffered, per, GetPlayerQue(), positions);
                    positions.Clear(); // Her per için pozisyonları temizle
                }
            }

        }
    }

    void TakeBackPers()
    {
        // Eğer geri alınacak taş yoksa, işlemi durdur
        if (meldedTiles.Count == 0) return;
        List<Tiles> playerTiles = _tileDistribute.GetPlayerTiles();
        Tiles tiles = meldedTiles[0];
        int playerQue = GetPlayerQue();
        // Oyuncunun taş listesini al
        // Geri alınan taşları tahtadan sil
        foreach (var tile in meldedTiles)
        {
            tiles = tile;
            // Taşın mevcut indeksini bul
            int tileIndex = playerTiles.IndexOf(tile);
            if (tileIndex != -1)
            {
                // Taşın GameObject'ini bul ve yok et
                foreach (Transform placeholder in playerTileContainer)
                {
                    if (placeholder.childCount > 0)
                    {
                        TileUI tileUI = placeholder.GetChild(0).GetComponent<TileUI>();
                        if (tileUI != null && tileUI.tileDataInfo == tile)
                        {
                            // Taşı görünür yap
                            placeholder.GetChild(0).gameObject.SetActive(true); // Taşı görünür yap

                            Debug.Log($"Tile {tile.color} {tile.number} geri alındı.");

                            break; // İlk eşleşmeyi bulduktan sonra döngüden çık
                        }
                    }
                }
            }

        }

        foreach (GameObject meldTile in meldTileGO)
        {

            if (meldTileGO.Count > 0)
            {
                TileUI tileUI = meldTile.GetComponent<TileUI>();
                if (meldTile.transform.parent.parent == colorPerPlaceHolder)
                {
                    occupiedRows[tileUI.tileRow] = false;
                    //tileDistrubite.photonView.RPC("UnMergeValidPers", RpcTarget.AllBuffered, tiles, GetPlayerQue());
                }
                else if (meldTile.transform.parent.parent == numberPerPlaceHolder)
                {
                    //tileDistrubite.photonView.RPC("UnMergeValidPers", RpcTarget.AllBuffered, tiles, GetPlayerQue());

                    occupiedRowsNumber[tileUI.tileRow] = false;
                }
                else if (meldTile.transform.parent.parent == pairPerPlaceHolder)
                {
                    //tileDistrubite.photonView.RPC("UnMergeValidPers", RpcTarget.AllBuffered, tiles, GetPlayerQue());
                    occupiedRowsPair[tileUI.tileRow] = false;
                }
                Destroy(meldTile); // Taşı yok et
            }
        }
        _tileDistribute.photonView.RPC("UnMergeValidPers", RpcTarget.AllBuffered, playerQue);
        meldedTiles.RemoveAll(x => x != null);
        meldedTiles.Clear();
        // Geri alınan taşları temizle

        meldTileGO.Clear();
        // Tahtadaki satırları sıfırla

    }
    #endregion

    #region Taş işleme
    private bool[] availableColumns;
    private void UpdateAvailableForPlaceholders(List<Tiles> per, int rowIndex)
    {
        if (per.Count == 0)
        {
            Debug.Log("per.Count == 0, method will return.");
            return;
        }

        List<Tiles> availableTiles = _tileDistribute.GetAvailableTiles(per, rowIndex); // Available taşları al

        if (IsSingleColor(per) && SingleColorCheck(per))
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
                if (rightPlaceholderIndex < colorPerPlaceHolders.Length) // colorPerPlaceHolders dizisini kullanarak kontrol edin
                {
                    Placeholder rightPlaceholder = colorPerPlaceHolders[rightPlaceholderIndex].GetComponent<Placeholder>();
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
                    Placeholder leftPlaceholder = colorPerPlaceHolders[leftPlaceholderIndex].GetComponent<Placeholder>();
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
                if (jokerPlaceholderIndex >= 0 && jokerPlaceholderIndex < colorPerPlaceHolders.Length)
                {
                    Placeholder jokerPlaceholder = colorPerPlaceHolders[jokerPlaceholderIndex].GetComponent<Placeholder>();
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
        else if (MultiColorCheck(per))
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
                    if (fourthPlaceholderIndex < numberPerPlaceHolders.Length)
                    {
                        Placeholder fourthPlaceholder = numberPerPlaceHolders[fourthPlaceholderIndex].GetComponent<Placeholder>();
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
                    if (fourthPlaceholderIndex < numberPerPlaceHolders.Length)
                    {
                        Placeholder fourthPlaceholder = numberPerPlaceHolders[fourthPlaceholderIndex].GetComponent<Placeholder>();
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
                    for (int i = 0; i < numberPerPlaceHolders.Length; i++)
                    {
                        Placeholder currentPlaceholder = numberPerPlaceHolders[i].GetComponent<Placeholder>();
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
                        Placeholder jokerPlaceholder = numberPerPlaceHolders[jokerPlaceholderIndex].GetComponent<Placeholder>();
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

                // Eğer per 4 taşlı ve içerisinde joker taşı varsa, joker taşının bulunduğu placeholder'ı true yap
                if (per.Count == 4 && hasJoker)
                {
                    int jokerPlaceholderIndex = -1; // Joker taşının bulunduğu placeholder'ın indeksi
                    for (int i = 0; i < numberPerPlaceHolders.Length; i++)
                    {
                        Placeholder currentPlaceholder = numberPerPlaceHolders[i].GetComponent<Placeholder>();
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
                        Placeholder jokerPlaceholder = numberPerPlaceHolders[jokerPlaceholderIndex].GetComponent<Placeholder>();
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
        else if (CheckForDoublePer(per) && IsSingleColor(per))
        {
            if (per.Any(tile => tile.type == TileType.Joker))
            {
                int jokerPlaceholderIndex = -1; // Joker taşının bulunduğu placeholder'ın indeksi
                for (int i = 0; i < pairPerPlaceHolders.Length; i++)
                {
                    Placeholder currentPlaceholder = pairPerPlaceHolders[i].GetComponent<Placeholder>();
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
                    Placeholder jokerPlaceholder = pairPerPlaceHolders[jokerPlaceholderIndex].GetComponent<Placeholder>();
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

 public void ActivePers()
{
    if (!turnManager.canDrop)
    {
        Debug.Log("[ActivePers] Not player's turn, cannot place tiles");
        return;
    }

    Debug.Log("[ActivePers] Starting tile activation process");
    List<Tiles> playerTiles = _tileDistribute.GetPlayerTiles();
    int playerQue = GetPlayerQue();
    
    // Track placements with explicit identifiers
    List<int> placedTileIndices = new List<int>();
    List<string> containerPaths = new List<string>();
    
    // Process each player's meld containers
    foreach (var player in PhotonNetwork.PlayerList)
    {
        string containerName = player.NickName + " meld";
        Transform meldContainer = GameObject.Find(containerName).transform;
        
        if (meldContainer == null)
        {
            Debug.LogWarning($"[ActivePers] Container {containerName} not found");
            continue;
        }
        
        // Check each type of container (color, number, pair)
        for (int containerIdx = 0; containerIdx < meldContainer.childCount; containerIdx++)
        {
            Transform typeContainer = meldContainer.GetChild(containerIdx);
            
            // Check each placeholder
            for (int placeholderIdx = 0; placeholderIdx < typeContainer.childCount; placeholderIdx++)
            {
                Transform placeholder = typeContainer.GetChild(placeholderIdx);
                Placeholder ph = placeholder.GetComponent<Placeholder>();
                
                // Skip if not available or has no expected tile
                if (ph == null || !ph.available || ph.AvailableTileInfo == null)
                    continue;
                
                // Look for matching tile in player's hand
                for (int tileIdx = 0; tileIdx < playerTiles.Count; tileIdx++)
                {
                    // Skip tiles already used in other placements
                    if (placedTileIndices.Contains(tileIdx))
                        continue;
                    
                    Tiles tile = playerTiles[tileIdx];
                    
                    // Check if tile matches what placeholder expects
                    if (ph.AvailableTileInfo.color == tile.color && 
                        ph.AvailableTileInfo.number == tile.number)
                    {
                        // Create local tile instance
                        GameObject tileInstance = Instantiate(tilePrefab, placeholder);
                        TileUI tileUI = tileInstance.GetComponent<TileUI>();
                        meldTileGO.Add(tileInstance);
                        
                        if (tileUI != null)
                        {
                            tileUI.SetTileData(tile);
                        }
                        
                        // Get full path to placeholder for precise identification
                        string path = GetFullPath(placeholder);
                        Debug.Log($"[ActivePers] Placed tile {tile.color} {tile.number} at path: {path}");
                        
                        // Track this placement
                        placedTileIndices.Add(tileIdx);
                        containerPaths.Add(path);
                        
                        // Mark placeholder as used
                        ph.available = false;
                        ph.AvailableTileInfo = null;
                        
                        // Deactivate tile in player's hand
                        _tileDistribute.photonView.RPC("DeactivatePlayerTile", RpcTarget.AllBuffered, 
                                                     playerQue, tileIdx);
                        
                        break; // Move to next placeholder
                    }
                }
            }
        }
    }
    
    // Synchronize placements to other clients
    if (placedTileIndices.Count > 0)
    {
        Debug.Log($"[ActivePers] Synchronizing {placedTileIndices.Count} placements");
        
        // Convert paths to string array
        string[] pathsArray = containerPaths.ToArray();
        
        // Send synchronization data
        _tileDistribute.photonView.RPC("SyncActiveTilesExact", RpcTarget.AllBuffered,
                                     playerQue,
                                     placedTileIndices.ToArray(),
                                     pathsArray);
    }
}

// Helper method to get the full path of a transform
private string GetFullPath(Transform transform)
{
    string path = transform.name;
    Transform parent = transform.parent;
    
    while (parent != null)
    {
        path = parent.name + "/" + path;
        parent = parent.parent;
    }
    
    return path;
}
    #endregion
    #region Hide and remove tiles from the Board
    public void RemoveMeldedTiles()
    {

        if (meldedTiles.Count == 0) return;

        List<Tiles> playerTiles = _tileDistribute.GetPlayerTiles();
        int playerQue = GetPlayerQue();

        meldTileGO.Clear();
        foreach (var tile in meldedTiles)
        {
            int tileIndex = playerTiles.IndexOf(tile);
            _tileDistribute.photonView.RPC("MeldTiles", RpcTarget.AllBuffered, playerQue, tileIndex);
            DestroyTileGameObject(tile);
        }

        occupiedRows.All(x => x = false);
        occupiedRowsNumber.All(x => x = false);
        occupiedRowsPair.All(x => x = false);
        meldedTiles.Clear();

    }

    private void DestroyTileGameObject(Tiles tile)
    {
        // Taşın GameObject'ini bul ve yok et
        foreach (Transform placeholder in playerTileContainer)
        {
            if (placeholder.childCount > 0)
            {
                TileUI tileUI = placeholder.GetChild(0).GetComponent<TileUI>();
                if (tileUI != null && tileUI.tileDataInfo == tile)
                {
                    Destroy(placeholder.GetChild(0).gameObject);
                    return; // İlk eşleşmeyi bulduktan sonra döngüden çık
                }
            }
        }
    }
    #endregion
    #endregion
}