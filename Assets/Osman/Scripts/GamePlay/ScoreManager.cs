using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class ScoreManager : MonoBehaviourPunCallbacks
{
    private Dictionary<int, int> playerScores; // Oyuncu ID'si ve puanı
    private Dictionary<int, int> playerPerCounts; // Oyuncu ID'si ve per sayısı
    [SerializeField] private TileDistrubite tileDistrubite; // Taşları yöneten sınıf
    public Transform playerTileContainer; // Oyuncu taşı bölmesi

    // Editörde görünür hale getirmek için
    [Header("Player Scores")]
    public int player1Score;
    public int player2Score;
    public int player3Score;
    public int player4Score;

    [Header("Player Per Counts")]
    public int player1PerCount;
    public int player2PerCount;
    public int player3PerCount;
    public int player4PerCount;

    // Per gruplarını saklamak için
    [Header("Per Groups")]
    [SerializeField] private List<List<Tiles>> perGroups; // Editörde görünür hale getirmek için

    // Yeni değişkenler
    [Header("Per Count and Total Score")]
    public int totalPerCount; // Toplam per sayısı
    public int totalScore; // Toplam puan


    private Dictionary<Tiles, Tiles> replacedTiles = new Dictionary<Tiles, Tiles>(); // Jokerin yerini aldığı taşlar
    private Dictionary<Tiles, int> jokerReplacementNumbers = new Dictionary<Tiles, int>();
    private void Start()
    {
        tileDistrubite = GameObject.Find("TileManager(Clone)").GetComponent<TileDistrubite>();
        playerTileContainer = GameObject.Find("PlayerTileContainer").transform;
        playerScores = new Dictionary<int, int>();
        playerPerCounts = new Dictionary<int, int>();
        perGroups = new List<List<Tiles>>(); // Per gruplarını başlat
    }

    #region Per Islemleri
    public void CheckForPer()
    {
        Photon.Realtime.Player player = PhotonNetwork.LocalPlayer;
        player.CustomProperties.TryGetValue("PlayerQue", out object playerId);
        int playerIdInt = (int)playerId; // Per gruplarını başlat

        // Per gruplarını güncelle
        var groups = GetSplittedGroups();
        Debug.Log("Per gruplarını kontrol ediyor..." + groups.Count + " grup var.");

        int perCount = 0; // Geçerli per sayısını sıfırla
        int score = 0; // Geçerli puanı sıfırla

        // Geçerli perleri kontrol et
        HashSet<List<Tiles>> countedPers = new HashSet<List<Tiles>>(); // Daha önce sayılan perleri tutmak için
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
                    perCount++; // Geçerli per sayısını artır
                    score += CalculateGroupScore(per); // Geçerli puanı ekle
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
        totalPerCount = countedPers.Count;
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

        // İkinci 15 yer tutucular (15-29)
        newSplittedGroup = true; // Yeni grup başlangıcını sıfırla
        for (int i = 15; i < 30; i++)
        {
            if (playerTileContainer.GetChild(i).childCount != 0)
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

        return perGroups;
    }
    public bool IsSingleColor(List<Tiles> tiles)
    {
        TileColor? firstColor = null;
        foreach (var tile in tiles)
        {
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
                Debug.Log("SingleColor per bulundu.");
                return true; // Per bulundu
            }
            else if (MultiColorCheck(per))
            {
                Debug.Log("MultiColor per bulundu.");
                return true; // Per bulundu
            }
        }
        return false; // Hiçbir per bulunamadı
    }

    public bool SingleColorCheck(List<Tiles> tiles)
    {
        if (tiles.Count < 3)
        {
            Debug.Log("YEŞİLS DEŞIL " + tiles.Count);
            return false;
        }

        int[] pattern = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };
        int[] revPattern = pattern.Reverse().ToArray();

        // İlk desen kontrolü
        if (CheckPattern(tiles, pattern) || CheckPattern(tiles, revPattern))
        {
            Debug.Log("YEŞİLS");
            return true;
        }

        Debug.Log("YEŞİLS DEŞIL");
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
                        if (j < tiles.Count - 1 && tiles[j + 1].number == expectedNumber + 1)
                        {
                            tiles[j].number = expectedNumber; // Joker taşının numarasını ayarla
                            isValidJoker = true; // Joker geçerli
                        }
                    }
                    // Joker taşının sağındaki taş yoksa
                    else if (j == tiles.Count - 1)
                    {
                        // Joker taşının solundaki taşın beklenen numaraya eşit olup olmadığını kontrol et
                        if (tiles[j - 1].number == expectedNumber - 1)
                        {
                            tiles[j].number = expectedNumber; // Joker taşının numarasını ayarla
                            isValidJoker = true; // Joker geçerli
                        }
                    }
                    else
                    {
                        // Joker taşının hem solundaki hem de sağındaki taşları kontrol et
                        if (tiles[j - 1].number == expectedNumber - 1 || tiles[j + 1].number == expectedNumber + 1)
                        {
                            tiles[j].number = expectedNumber; // Joker taşının numarasını ayarla
                            isValidJoker = true; // Joker geçerli
                        }
                    }

                    if (!isValidJoker)
                    {
                        valid = false; // Joker geçerli değil
                        break;
                    }
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
            Debug.Log("KIRMIZI" + tiles.Count);
            return false;
        }
        if (tiles.Count > 4)
        {
            Debug.Log("KIRMIZI" + tiles.Count);
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
                Debug.Log("KIRMIZI");
                return false;
            }
        }
        IEnumerable<Tiles> filteredList = tiles
        .Where(x => x.type != TileType.Joker)
        .GroupBy(a => a.color)
        .Select(group => group.First());
        if (filteredList.Count() + (jokerStones.Count) != tiles.Count)
        {
            Debug.Log("KIRMIZI");
            return false;
        }

        foreach (var joker in jokerStones)
        {
            joker.number = notJokerStones.number;
        }
        Debug.Log("YEŞİL");
        return true;

    }
    private int CalculateGroupScore(List<Tiles> tiles)
    {
        int score = 0;
        foreach (var tile in tiles)
        {

            score += tile.number; // Normal taşın puanını ekle

        }

        Debug.Log($"Toplam Puan: {score}"); // Puanı kontrol etmek için
        return score;
    }
    #endregion
}