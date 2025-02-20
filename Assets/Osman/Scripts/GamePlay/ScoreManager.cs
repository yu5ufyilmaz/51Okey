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
    private Transform[] colorPerPlaceHolders;
    private TileDistrubite tileDistrubite; // Taşları yöneten sınıf
    [SerializeField] private TurnManager turnManager;
    public Transform playerTileContainer; // Oyuncu taşı bölmesi
    public GameObject tilePrefab;

    // Yeni değişkenler
    [Header("Per Count and Total Score")]
    public int totalPerCount; // Toplam per sayısı
    public int totalScore; // Toplam puan
    public int pairTotalScore;
    public int pairTotalPerCount;
    #region Generated Methods
    private void Start()
    {
        Player player = PhotonNetwork.LocalPlayer;

        turnManager = GameObject.Find("TurnManager").GetComponent<TurnManager>();
        tileDistrubite = GameObject.Find("TileManager(Clone)").GetComponent<TileDistrubite>();
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
    #endregion
    #region Per Kontrol İslemleri
    public List<List<Tiles>> validPerss = new List<List<Tiles>>();
    public void CheckForPer()
    {
        Photon.Realtime.Player player = PhotonNetwork.LocalPlayer;
        player.CustomProperties.TryGetValue("PlayerQue", out object playerId);
        int playerIdInt = (int)playerId; // Per gruplarını başlat

        // Per gruplarını güncelle
        var groups = GetSplittedGroups();
        Debug.Log("Per gruplarını kontrol ediyor..." + groups.Count + " grup var.");

        int perCount = 0; // Geçerli per sayısını sıfırla
        int pairPerCount = 0;
        int score = 0; // Geçerli puanı sıfırla
        int pairScore = 0;

        // Geçerli perleri kontrol et
        HashSet<List<Tiles>> countedPers = new HashSet<List<Tiles>>(); // Daha önce sayılan perleri tutmak için
        validPerss.Clear();

        foreach (var per in groups)
        {

            // Her grup için kontrol et
            if (ControlPer(new List<List<Tiles>> { per })) // Geçerli per kontrolü
            {

                // Eğer bu per daha önce sayılmadıysa
                if (!countedPers.Contains(per))
                {
                    countedPers.Add(per); // Bu peri sayılanlar listesine ekle
                    Debug.Log(countedPers + " TAŞ VAR.");
                    validPerss.Add(per);
                    if (CheckForDoublePer(per))
                    {
                        pairPerCount++;
                        pairScore += CalculateDoublePerScore(per); // Çift per puanını ekle
                    }
                    else
                    {
                        perCount++; // Geçerli per sayısını artır
                        score += CalculateGroupScore(per); // Geçerli puanı ekle
                    }
                    UpdatePlayerScore(playerIdInt, score);
                }
                else
                {
                    Debug.Log("Bu per daha önce sayılmış.");
                }
            }
            else
            {
                Debug.Log("Geçerli Per bulunamadı.");
            }
        }
        totalScore = score;
        pairTotalScore = pairScore;
        totalPerCount = countedPers.Count;
        pairTotalPerCount = pairPerCount;
        Debug.Log($"Toplam Geçerli Per Sayısı: {totalPerCount}, Toplam Puan: {totalScore}");
    }

    public List<List<Tiles>> GetSplittedGroups()
    {
        List<List<Tiles>> perGroups = new List<List<Tiles>>(); // Per gruplarını bul ve sakla
        bool newSplittedGroup = true;

        // İlk 15 yer tutucular (0-14)
        for (int i = 0; i < 15; i++)
        {
            if (playerTileContainer.GetChild(i).childCount != 0)
            {
                if (playerTileContainer.GetChild(i).transform.GetChild(0).gameObject.activeSelf == true)
                {
                    if (newSplittedGroup)
                    {

                        perGroups.Add(new List<Tiles>());
                        perGroups.Last().Add(playerTileContainer.GetChild(i).transform.GetChild(0).GetComponent<TileUI>().tileDataInfo);
                        newSplittedGroup = false;

                    }
                    else
                    {
                        var lastTiles = perGroups.LastOrDefault();
                        if (lastTiles != null)
                        {
                            perGroups.Last().Add(playerTileContainer.GetChild(i).transform.GetChild(0).GetComponent<TileUI>().tileDataInfo);
                        }
                    }
                }
                else
                {
                    newSplittedGroup = true;
                }
            }
            else
            {
                newSplittedGroup = true;
            }
        }

        // İkinci 15 yer tutucular (15-29)
        newSplittedGroup = true; // Yeni grup başlangıcını sıfırla
        for (int i = 15; i < 30; i++)
        {
            if (playerTileContainer.GetChild(i).childCount != 0)
            {
                if (playerTileContainer.GetChild(i).transform.GetChild(0).gameObject.activeSelf == true)
                {
                    if (newSplittedGroup)
                    {


                        perGroups.Add(new List<Tiles>());
                        perGroups.Last().Add(playerTileContainer.GetChild(i).transform.GetChild(0).GetComponent<TileUI>().tileDataInfo);
                        newSplittedGroup = false;

                    }
                    else
                    {
                        var lastTiles = perGroups.LastOrDefault();
                        if (lastTiles != null)
                        {

                            perGroups.Last().Add(playerTileContainer.GetChild(i).transform.GetChild(0).GetComponent<TileUI>().tileDataInfo);
                        }
                    }
                }
                else
                {
                    newSplittedGroup = true;
                }
            }
            else
            {
                newSplittedGroup = true;
            }
        }

        return perGroups;
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
            return true;
        }

        // Normal taşlar aynı numaraya sahipse, çift per
        return normalTile != null;
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
    public bool ControlPer(List<List<Tiles>> perGroups)
    {
        foreach (var per in perGroups)
        {
            Debug.Log("Bu perde " + per.Count + " taş var.");

            if (IsSingleColor(per) && SingleColorCheck(per))
            {
                foreach (var tile in per)
                {
                    tile.perType = TilePerType.Color;
                }
                Debug.Log("SingleColor per bulundu.");
                return true; // Per bulundu
            }
            else if (MultiColorCheck(per))
            {
                foreach (var tile in per)
                {
                    tile.perType = TilePerType.Number;
                }
                Debug.Log("MultiColor per bulundu.");
                return true; // Per bulundu
            }
            else if (CheckForDoublePer(per) && IsSingleColor(per))
            {
                foreach (var tile in per)
                {
                    tile.perType = TilePerType.Pair;
                }
                Debug.Log("Double per bulundu.");
                return true;
            }
        }
        return false; // Hiçbir per bulunamadı
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
            bool valid = true; // Geçerli bir desen kontrolü için
            for (int j = 0; j < tiles.Count; j++)
            {
                int expectedNumber = pattern[(i + j > pattern.Length - 1 ? pattern.Length - 1 : i + j)];
                if (tiles[j].type == TileType.Joker)
                {
                    // Joker taşının puanını, mevcut desenin numarasına eşit yap
                    bool isValidJoker = false;

                    // Joker taşının solundaki taş yoksa
                    if (j == 0)
                    {
                        // Joker taşının sağındaki taşın beklenen numaraya eşit olup olmadığını kontrol et
                        if (tiles.Count > 1 && tiles[j + 1].number == expectedNumber + 1 || tiles.Count > 1 && tiles[j + 1].number == expectedNumber - 1)
                        {
                            tiles[j].number = expectedNumber; // Joker taşının numarasını ayarla
                            isValidJoker = true; // Joker geçerli
                        }
                    }
                    // Joker taşının sağındaki taş yoksa
                    else if (j == tiles.Count - 1)
                    {
                        // Joker taşının solundaki taşın beklenen numaraya eşit olup olmadığını kontrol et
                        if (tiles[j - 1].number == expectedNumber - 1 || tiles[j - 1].number == expectedNumber + 1)
                        {
                            tiles[j].number = expectedNumber; // Joker taşının numarasını ayarla
                            isValidJoker = true; // Joker geçerli
                        }
                    }
                    else
                    {
                        // Joker taşının hem solundaki hem de sağındaki taşları kontrol et
                        if (tiles[j - 1].number == expectedNumber - 1 || tiles[j - 1].number == expectedNumber + 1)
                        {
                            tiles[j].number = expectedNumber; // Joker taşının numarasını ayarla
                            isValidJoker = true; // Joker geçerli
                        }
                        else if (tiles[j + 1].number == expectedNumber + 1 || tiles[j + 1].number == expectedNumber - 1)
                        {
                            tiles[j].number = expectedNumber; // Joker taşının numarasını ayarla
                            isValidJoker = true; // Joker gezocht
                        }
                    }

                    if (!isValidJoker)
                    {
                        valid = false; // Joker geçerli değil
                        break;
                    }
                    else continue;
                }
                else if (tiles[j].number == expectedNumber)
                {
                    continue; // Geçerli taş
                }
                else
                {
                    valid = false; // Geçersiz
                    break;
                }
            }
            if (valid) return true; // Eğer geçerli bir desen bulduysak
        }
        return false; // Hiçbir geçerli desen bulamadık
    }
    public bool MultiColorCheck(List<Tiles> tiles)
    {
        if (tiles.Count < 3)
        {
            return false;
        }
        if (tiles.Count > 4)
        {
            return false;
        }
        Tiles notJokerStones = null;
        List<Tiles> jokerStones = new List<Tiles>();
        foreach (var tile in tiles)
        {
            if (tile.type != TileType.Joker)
            {
                notJokerStones = tile;

            }
            else
            {
                jokerStones.Add(tile);
            }

        }
        foreach (var tile in tiles)
        {
            if (tile.type == TileType.Joker)
            {
                continue;
            }
            else if (tile.number == notJokerStones.number)
            {
                continue;
            }
            else
            {
                return false;
            }
        }
        IEnumerable<Tiles> filteredList = tiles
        .Where(x => x.type != TileType.Joker)
        .GroupBy(a => a.color)
        .Select(group => group.First());
        if (filteredList.Count() + (jokerStones.Count) != tiles.Count)
        {
            return false;
        }

        foreach (var joker in jokerStones)
        {
            joker.number = notJokerStones.number;
        }
        return true;

    }
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

    #region Per Açma İşlemleri
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
            if (totalScore > 10) // Örneğin, 50 puan
            {
                ShowValidPers();
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
            if (pairTotalScore > 10) // Örneğin, 50 puan
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
    public void ShowValidPers()
    {
        // Geçerli perleri kontrol et
        PlaceValidPers(validPerss); // Geçerli perleri yerleştir
        //tileDistrubite.photonView.RPC("MergeValidpers", RpcTarget.AllBuffered);
    }
    public bool[] occupiedRowsNumber = new bool[4];
    public bool[] occupiedRows = new bool[4];
    public bool[] occupiedRowsPair = new bool[8];
    public List<GameObject> meldTileGO = new List<GameObject>();

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
                            int playerTileIndex = tileDistrubite.GetPlayerTiles().IndexOf(tile);
                            int playerQue = GetPlayerQue();
                            tileDistrubite.photonView.RPC("DeactivatePlayerTile", RpcTarget.AllBuffered, playerQue, playerTileIndex);

                        }
                    }
                    occupiedRows[rowIndex] = true;

                    // Burada boyut kontrolü yapıyoruz
                    if (per.Count != positions.Count)
                    {
                        Debug.LogError("Valid melted tiles and positions count mismatch!");
                        return; // İşlemi durdur
                    }

                    tileDistrubite.photonView.RPC("MergeValidpers", RpcTarget.AllBuffered, per, GetPlayerQue(), positions);
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
                            int playerTileIndex = tileDistrubite.GetPlayerTiles().IndexOf(tile);
                            int playerQue = GetPlayerQue();
                            tileDistrubite.photonView.RPC("DeactivatePlayerTile", RpcTarget.AllBuffered, playerQue, playerTileIndex);
                        }
                    }
                    occupiedRowsNumber[rowIndex] = true;

                    // Burada boyut kontrolü yapıyoruz
                    if (per.Count != positions.Count)
                    {
                        Debug.LogError("Valid melted tiles and positions count mismatch!");
                        return; // İşlemi durdur
                    }

                    tileDistrubite.photonView.RPC("MergeValidpers", RpcTarget.AllBuffered, per, GetPlayerQue(), positions);
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
                            int playerTileIndex = tileDistrubite.GetPlayerTiles().IndexOf(tile);
                            int playerQue = GetPlayerQue();
                            tileDistrubite.photonView.RPC("DeactivatePlayerTile", RpcTarget.AllBuffered, playerQue, playerTileIndex);
                        }
                    }
                    occupiedRowsPair[rowIndex] = true;

                    // Burada boyut kontrolü yapıyoruz
                    if (per.Count != positions.Count)
                    {
                        Debug.LogError("Valid melted tiles and positions count mismatch!");
                        return; // İşlemi durdur
                    }

                    tileDistrubite.photonView.RPC("MergeValidpers", RpcTarget.AllBuffered, per, GetPlayerQue(), positions);
                    positions.Clear(); // Her per için pozisyonları temizle
                }
            }

        }
    }

    public List<Tiles> meldedTiles = new List<Tiles>();
    public void RemoveMeldedTiles()
    {

        if (meldedTiles.Count == 0) return;

        List<Tiles> playerTiles = tileDistrubite.GetPlayerTiles();
        int playerQue = GetPlayerQue();

        meldTileGO.Clear();
        foreach (var tile in meldedTiles)
        {
            int tileIndex = playerTiles.IndexOf(tile);
            tileDistrubite.photonView.RPC("MeldTiles", RpcTarget.AllBuffered, playerQue, tileIndex);
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
}