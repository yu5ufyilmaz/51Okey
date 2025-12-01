using System;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon.StructWrapping;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[System.Serializable]
public struct PendingJoker
{
    public Tiles jokerData;
    public Transform originalPlaceholder;
}

[System.Serializable]
public struct ActiveTilePlacementInfo
{
    public Tiles tileData;
    public int ownerPlayerQue; // Per'in sahibi olan oyuncunun sıra numarası
    public ScoreManager.MeldType meldType; // Hangi tür per alanına konulacağı (SingleColor, MultiColor, Pair)
    public int placeholderIndex; // O alandaki kaçıncı yuvaya konulacağı

    public ActiveTilePlacementInfo(Tiles tile, int ownerQue, ScoreManager.MeldType type, int index)
    {
        tileData = tile;
        ownerPlayerQue = ownerQue;
        meldType = type;
        placeholderIndex = index;
    }
}

public class ScoreManager : MonoBehaviourPunCallbacks
{
    public Dictionary<int, int> playerScores; // Oyuncu ID'si ve puanı
    private Transform playerMeldContainers;
    Transform pairPerPlaceHolder;
    private Transform[] pairPerPlaceHolders;
    Transform numberPerPlaceHolder;
    private Transform[] numberPerPlaceHolders; // Player tile placeholders
    Transform colorPerPlaceHolder;

    [SerializeField]
    private Transform[] colorPerPlaceHolders;
    private TileDistrubite tileDistrubite; // Taşları yöneten sınıf
    private List<PendingJoker> pendingJokersToTake = new List<PendingJoker>();

    [SerializeField]
    private TurnManager turnManager;
    public Transform playerTileContainer; // Oyuncu taşı bölmesi
    public GameObject tilePrefab;

    // Yeni değişkenler
    [Header("Per Count and Total Score")]
    public int totalPerCount; // Toplam per sayısı
    public int totalScore; // Toplam puan
    public int pairTotalScore;
    public int pairTotalPerCount;

    [Header("Player Status")]
    public bool hasOpenedSeries = false; // Oyuncu seri açtı mı?
    public bool hasOpenedPairs = false; // Oyuncu çift açtı mı?
    #region GENERATE_METHODS
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

    // ScoreManager.cs

    // Oyuncuların puanlarını tutan ana yapı
    // Key: Oyuncu Sırası (PlayerQue), Value: Ceza Puanı
    // ScoreManager.cs değişkenleri arasına:



    // Yeni tur başladığında bunları sıfırlamak için (GameManager veya TurnManager çağırabilir)
    public void ResetPlayerOpenStatus()
    {
        hasOpenedSeries = false;
        hasOpenedPairs = false;
    }

    public void UpdatePlayerScore(int playerQue, int penaltyPoints)
    {
        // Eğer oyuncu listede yoksa ekle
        if (!playerScores.ContainsKey(playerQue))
        {
            playerScores[playerQue] = 0;
        }

        // Cezayı ekle
        playerScores[playerQue] += penaltyPoints;

        // Photon ile herkese yay (UI güncellemeleri için)
        UpdatePlayerCustomProperties(playerQue);

        // --- LOGLAMA KISMI ---
        string logMessage =
            $"<color=red>CEZA! Oyuncu {playerQue} +{penaltyPoints} puan ceza aldı.</color>\n";
        logMessage += "--- GÜNCEL PUAN TABLOSU ---\n";

        // Puan tablosunu sıralı yazdırmak için
        foreach (var player in playerScores)
        {
            logMessage += $"Oyuncu {player.Key}: {player.Value} Puan\n";
        }

        Debug.Log(logMessage);
    }

    // Oyun başında tüm oyuncuları 0 puanla listeye ekle
    public void InitializeScores(int playerCount)
    {
        playerScores.Clear();
        for (int i = 1; i <= playerCount; i++)
        {
            playerScores[i] = 0;
        }
        Debug.Log("Skor tablosu sıfırlandı.");
    }

    private void UpdatePlayerCustomProperties(int playerId)
    {
        Photon.Realtime.Player player = PhotonNetwork.CurrentRoom.Players[playerId];
        player.SetCustomProperties(
            new ExitGames.Client.Photon.Hashtable { { "PlayerScore", playerScores[playerId] } }
        );
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
        List<List<Tiles>> perGroups = new List<List<Tiles>>(); // Per gruplarını bul ve sakla
        bool newSplittedGroup = true;

        // İlk 15 yer tutucular (0-14)
        for (int i = 0; i < 15; i++)
        {
            if (playerTileContainer.GetChild(i).childCount != 0)
            {
                if (
                    playerTileContainer.GetChild(i).transform.GetChild(0).gameObject.activeSelf
                    == true
                )
                {
                    if (newSplittedGroup)
                    {
                        perGroups.Add(new List<Tiles>());
                        perGroups
                            .Last()
                            .Add(
                                playerTileContainer
                                    .GetChild(i)
                                    .transform.GetChild(0)
                                    .GetComponent<TileUI>()
                                    .tileDataInfo
                            );
                        newSplittedGroup = false;
                    }
                    else
                    {
                        var lastTiles = perGroups.LastOrDefault();
                        if (lastTiles != null)
                        {
                            perGroups
                                .Last()
                                .Add(
                                    playerTileContainer
                                        .GetChild(i)
                                        .transform.GetChild(0)
                                        .GetComponent<TileUI>()
                                        .tileDataInfo
                                );
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
                if (
                    playerTileContainer.GetChild(i).transform.GetChild(0).gameObject.activeSelf
                    == true
                )
                {
                    if (newSplittedGroup)
                    {
                        perGroups.Add(new List<Tiles>());
                        perGroups
                            .Last()
                            .Add(
                                playerTileContainer
                                    .GetChild(i)
                                    .transform.GetChild(0)
                                    .GetComponent<TileUI>()
                                    .tileDataInfo
                            );
                        newSplittedGroup = false;
                    }
                    else
                    {
                        var lastTiles = perGroups.LastOrDefault();
                        if (lastTiles != null)
                        {
                            perGroups
                                .Last()
                                .Add(
                                    playerTileContainer
                                        .GetChild(i)
                                        .transform.GetChild(0)
                                        .GetComponent<TileUI>()
                                        .tileDataInfo
                                );
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
    #endregion
    #region Is pers valid or not
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
            else if (CheckForDoublePer(per) && IsSingleColor(per))
            {
                Debug.Log("Double per bulundu.");
                return true;
            }
        }
        return false; // Hiçbir per bulunamadı
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
            if (isFirstJoker && tile == tiles[0])
                continue;

            if (firstColor == null)
                firstColor = tile.color;
            else if (firstColor == tile.color)
                continue;
            else if (tile.type == TileType.Joker)
                continue;
            else
                return false;
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
            bool valid = true; // Geçerli bir desen kontrolü için
            for (int j = 0; j < tiles.Count; j++)
            {
                int expectedNumber = pattern[
                    (i + j > pattern.Length - 1 ? pattern.Length - 1 : i + j)
                ];
                if (tiles[j].type == TileType.Joker)
                {
                    // Joker taşının puanını, mevcut desenin numarasına eşit yap
                    bool isValidJoker = false;

                    // Joker taşının solundaki taş yoksa
                    if (j == 0)
                    {
                        // Joker taşının sağındaki taşın beklenen numaraya eşit olup olmadığını kontrol et
                        if (
                            tiles.Count > 1 && tiles[j + 1].number == expectedNumber + 1
                            || tiles.Count > 1 && tiles[j + 1].number == expectedNumber - 1
                        )
                        {
                            tiles[j].number = expectedNumber; // Joker taşının numarasını ayarla
                            tiles[j].color = tiles[j + 1].color;
                            isValidJoker = true; // Joker geçerli
                        }
                    }
                    // Joker taşının sağındaki taş yoksa
                    else if (j == tiles.Count - 1)
                    {
                        // Joker taşının solundaki taşın beklenen numaraya eşit olup olmadığını kontrol et
                        if (
                            tiles[j - 1].number == expectedNumber - 1
                            || tiles[j - 1].number == expectedNumber + 1
                        )
                        {
                            tiles[j].number = expectedNumber; // Joker taşının numarasını ayarla
                            tiles[j].color = tiles[j - 1].color;
                            isValidJoker = true; // Joker geçerli
                        }
                    }
                    else
                    {
                        // Joker taşının hem solundaki hem de sağındaki taşları kontrol et
                        if (
                            tiles[j - 1].number == expectedNumber - 1
                            || tiles[j - 1].number == expectedNumber + 1
                        )
                        {
                            tiles[j].number = expectedNumber; // Joker taşının numarasını ayarla
                            tiles[j].color = tiles[j - 1].color;
                            isValidJoker = true; // Joker geçerli
                        }
                        else if (
                            tiles[j + 1].number == expectedNumber + 1
                            || tiles[j + 1].number == expectedNumber - 1
                        )
                        {
                            tiles[j].number = expectedNumber; // Joker taşının numarasını ayarla
                            tiles[j].color = tiles[j + 1].color;
                            isValidJoker = true; // Joker gezocht
                        }
                    }

                    if (!isValidJoker)
                    {
                        valid = false; // Joker geçerli değil
                        break;
                    }
                    else
                        continue;
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
            if (valid)
                return true; // Eğer geçerli bir desen bulduysak
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

    int GetPlayerQue()
    {
        PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object queueValue);

        return (int)queueValue;
    }

  

    public void OnButtonClick()
    {
        if (hasOpenedPairs)
        {
            Debug.LogWarning("Çift açtığınız için Seri açamazsınız!");
            return;
        }

        if (turnManager.canDrop == true)
        {
            // --- YENİ EKLENEN: Bitiş Taşı Güvenlik Kontrolü ---
            // Açılacak (validPerss içindeki) toplam taş sayısını bul
            int tilesToMeldCount = 0;
            foreach (var group in validPerss)
                tilesToMeldCount += group.Count;

            // Oyuncunun şu an elindeki toplam taş sayısı
            int currentHandCount = tileDistrubite.GetPlayerHandCount(GetPlayerQue());

            // KURAL: Açtıktan sonra elde EN AZ 1 taş kalmalı (Atmak için)
            if (currentHandCount - tilesToMeldCount < 1)
            {
                Debug.LogWarning(
                    "HATA: Tüm taşları açamazsınız! Oyunu bitirmek için elinizde en az 1 taş kalmalı."
                );
                // İstersen buraya bir UI uyarısı ("Atacak taşınız kalmıyor!") ekleyebilirsin.
                return;
            }
            // --------------------------------------------------

            int limit = GameManager.Instance.currentTableLimit;
            bool limitPass = hasOpenedSeries
                ? true
                : (limit == 51 ? totalScore >= 51 : totalScore > limit);

            if (limitPass)
            {
                PlaceValidPers(validPerss);
                if (!hasOpenedSeries && totalScore > limit)
                {
                    GameManager.Instance.photonView.RPC(
                        "UpdateTableLimit",
                        RpcTarget.All,
                        totalScore
                    );
                }
            }
            else
            {
                Debug.LogWarning($"Yetersiz Puan! Eliniz: {totalScore}, Gereken: {limit} üzeri.");
            }
        }
        else
        {
            Debug.Log("Sıra sizde değil.");
        }
    }

    // ScoreManager.cs -> OnPairButtonClick (Çift Açma) GÜNCELLEMESİ

    public void OnPairButtonClick()
    {
        if (turnManager.canDrop == true)
        {
            // --- YENİ EKLENEN: Bitiş Taşı Güvenlik Kontrolü ---
            int tilesToMeldCount = 0;
            foreach (var group in validPerss)
                tilesToMeldCount += group.Count;

            int currentHandCount = tileDistrubite.GetPlayerHandCount(GetPlayerQue());

            // KURAL: Açtıktan sonra elde EN AZ 1 taş kalmalı
            // (Örn: Elde 2 taş var, Çift açarsan 0 kalır -> YASAK)
            if (currentHandCount - tilesToMeldCount < 1)
            {
                Debug.LogWarning("HATA: Çift açarsanız atacak taşınız kalmaz!");
                return;
            }
            // --------------------------------------------------

            if (hasOpenedSeries)
            {
                bool isAnyPairOnTable = CheckIfAnyPairOnTable();
                if (!isAnyPairOnTable)
                {
                    Debug.LogWarning("Masada çift yok, açamazsınız.");
                    return;
                }
                PlacePairPers(validPerss);
            }
            else
            {
                if (pairTotalPerCount >= 5)
                    PlacePairPers(validPerss);
                else if (hasOpenedPairs)
                    PlacePairPers(validPerss);
                else
                    Debug.LogWarning("En az 5 çift gerekli.");
            }
        }
        else
        {
            Debug.Log("Sıra sizde değil.");
        }
    }

    // Yardımcı Metot: Masada çift var mı?
    // ScoreManager.cs içine:

    // YENİ: Masada (Herhangi bir oyuncuda) Çift var mı kontrolü
    private bool CheckIfAnyPairOnTable()
    {
        // Tüm oyuncuları gez
        foreach (var player in PhotonNetwork.PlayerList)
        {
            // Oyuncunun meld alanını isminden bul
            GameObject meldObj = GameObject.Find(player.NickName + " meld");

            if (meldObj != null)
            {
                // Meld yapısında: Child(0)=Renk, Child(1)=Sayı, Child(2)=Çift
                // Eğer senin hiyerarşin farklıysa buradaki indeksi (2) düzeltmelisin.
                Transform pairContainer = meldObj.transform.GetChild(2);

                // O kaptaki tüm slotlara bak, dolu olan var mı?
                foreach (Transform slot in pairContainer)
                {
                    if (slot.childCount > 0)
                    {
                        // Bir tane bile çift bulursak yeterli
                        return true;
                    }
                }
            }
        }

        // Kimse çift açmamış
        return false;
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

    // ... diğer değişkenleriniz

    [Header("Meld Information Storage")]
    // Oyuncunun açtığı ama henüz ONAYLAMADIĞI taşların bilgilerini tutar.
    private List<MeldedTileInfo> pendingMeldInfos = new List<MeldedTileInfo>();

    // Oyuncunun onaylanmış ve kalıcı olarak açılmış tüm taşlarının bilgilerini tutar.
    public List<MeldedTileInfo> committedMelds = new List<MeldedTileInfo>();

    // Mevcut listenizin adını değiştiriyoruz:
    public List<Tiles> pendingMeldedTiles; // meldedTiles yerine bu ismi kullanalım
    public List<GameObject> meldTileGO = new List<GameObject>();

    private void PlaceValidPers(List<List<Tiles>> validPers)
    {
        List<Vector2Int> positions = new List<Vector2Int>();
        bool hasOpenedAnyMeld = false; // En az bir per açıldı mı kontrolü

        // ---------------------------------------------------------
        // 1. RENKLİ SIRALI PERLER (Single Color) İÇİN YERLEŞTİRME
        // ---------------------------------------------------------
        foreach (var per in validPers)
        {
            if (IsSingleColor(per) && SingleColorCheck(per)) // Renkli per kontrolü
            {
                int rowIndex = -1; // Satır indeksini başlat

                // Boş satır bul
                for (int r = 0; r < 4; r++)
                {
                    if (occupiedRows[r] == false)
                    {
                        bool allColumnsFull = true;
                        // O satırdaki sütunların doluluğunu kontrol et
                        for (int c = 0; c < 13; c++)
                        {
                            int columnIndex = r * 13 + c;
                            if (
                                columnIndex < colorPerPlaceHolders.Length
                                && colorPerPlaceHolders[columnIndex].childCount == 0
                            )
                            {
                                allColumnsFull = false;
                                break;
                            }
                        }
                        if (allColumnsFull == false)
                        {
                            rowIndex = r;
                            break;
                        }
                    }
                }

                // Eğer uygun bir satır bulunduysa, taşları yerleştir
                if (rowIndex != -1)
                {
                    hasOpenedAnyMeld = true; // Başarılı işlem bayrağı

                    foreach (var tile in per)
                    {
                        int columnIndex = rowIndex * 13 + (tile.number - 1); // Taşın numarasına göre sütun
                        if (columnIndex < colorPerPlaceHolders.Length)
                        {
                            // Pozisyonu kaydet
                            positions.Add(new Vector2Int(rowIndex, columnIndex));

                            // Görseli oluştur
                            GameObject tileInstance = Instantiate(
                                tilePrefab,
                                colorPerPlaceHolders[columnIndex]
                            );
                            meldTileGO.Add(tileInstance);

                            TileUI tileUI = tileInstance.GetComponent<TileUI>();
                            tileUI.CheckRowColoumn(rowIndex, columnIndex);

                            if (tileUI != null)
                            {
                                tileUI.SetTileData(tile);
                            }

                            // Geri alma ve onaylama işlemleri için veriyi sakla
                            MeldedTileInfo newMeldInfo = new MeldedTileInfo(
                                tile,
                                MeldType.SingleColor,
                                rowIndex,
                                columnIndex
                            );
                            tileUI.FitToParent();

                            pendingMeldInfos.Add(newMeldInfo);
                            pendingMeldedTiles.Add(tile);

                            // Oyuncunun elindeki taşı deaktif et (Gizle)
                            int playerTileIndex = tileDistrubite.GetPlayerTiles().IndexOf(tile);
                            int playerQue = GetPlayerQue();

                            tileDistrubite.photonView.RPC(
                                "DeactivatePlayerTile",
                                RpcTarget.AllBuffered,
                                playerQue,
                                playerTileIndex
                            );
                        }
                    }

                    occupiedRows[rowIndex] = true;

                    // Boyut kontrolü (Güvenlik)
                    if (per.Count != positions.Count)
                    {
                        Debug.LogError("Valid melted tiles and positions count mismatch!");
                        return;
                    }

                    // İşlek taşları güncelle (Otomatik hesaplama)
                    UpdateAvailableForPlaceholders(per, rowIndex);

                    // Diğer oyunculara bu peri göster (Senkronizasyon)
                    tileDistrubite.photonView.RPC(
                        "MergeValidpers",
                        RpcTarget.AllBuffered,
                        per,
                        GetPlayerQue(),
                        positions
                    );

                    positions.Clear(); // Bir sonraki per için temizle
                }
            }
        }

        // ---------------------------------------------------------
        // 2. SAYI GRUBU PERLERİ (Multi Color) İÇİN YERLEŞTİRME
        // ---------------------------------------------------------
        foreach (var per in validPers)
        {
            if (MultiColorCheck(per)) // Sayı per kontrolü
            {
                int rowIndex = -1;

                // Boş satır bul
                for (int r = 0; r < 4; r++)
                {
                    if (occupiedRowsNumber[r] == false)
                    {
                        bool allColumnsFull = true;
                        for (int c = 0; c < 4; c++)
                        {
                            int columnIndex = r * 4 + c;
                            if (
                                columnIndex < numberPerPlaceHolder.childCount
                                && numberPerPlaceHolder.GetChild(columnIndex).childCount == 0
                            )
                            {
                                allColumnsFull = false;
                                break;
                            }
                        }

                        if (!allColumnsFull)
                        {
                            rowIndex = r;
                            break;
                        }
                    }
                }

                if (rowIndex != -1)
                {
                    hasOpenedAnyMeld = true; // Başarılı işlem bayrağı

                    foreach (var tile in per)
                    {
                        int tileIndex = per.IndexOf(tile);
                        int columnIndex = rowIndex * 4 + (tileIndex);

                        if (columnIndex < numberPerPlaceHolder.childCount)
                        {
                            positions.Add(new Vector2Int(rowIndex, columnIndex));

                            GameObject tileInstance = Instantiate(
                                tilePrefab,
                                numberPerPlaceHolders[columnIndex]
                            );
                            TileUI tileUI = tileInstance.GetComponent<TileUI>();
                            meldTileGO.Add(tileInstance);

                            tileUI.CheckRowColoumn(rowIndex, columnIndex);

                            if (tileUI != null)
                            {
                                tileUI.SetTileData(tile);
                            }

                            tileUI.FitToParent();

                            MeldedTileInfo newMeldInfo = new MeldedTileInfo(
                                tile,
                                MeldType.MultiColor,
                                rowIndex,
                                columnIndex
                            );
                            pendingMeldInfos.Add(newMeldInfo);
                            pendingMeldedTiles.Add(tile);

                            int playerTileIndex = tileDistrubite.GetPlayerTiles().IndexOf(tile);
                            int playerQue = GetPlayerQue();

                            tileDistrubite.photonView.RPC(
                                "DeactivatePlayerTile",
                                RpcTarget.AllBuffered,
                                playerQue,
                                playerTileIndex
                            );
                        }
                    }

                    occupiedRowsNumber[rowIndex] = true;
                    UpdateAvailableForPlaceholders(per, rowIndex);

                    if (per.Count != positions.Count)
                    {
                        Debug.LogError("Valid melted tiles and positions count mismatch!");
                        return;
                    }

                    tileDistrubite.photonView.RPC(
                        "MergeValidpers",
                        RpcTarget.AllBuffered,
                        per,
                        GetPlayerQue(),
                        positions
                    );
                    positions.Clear();
                }
            }
        }

        // ---------------------------------------------------------
        // 3. OYUN KURALLARI VE CEZA KONTROLLERİ (YENİ EKLENDİ)
        // ---------------------------------------------------------

        if (hasOpenedAnyMeld)
        {
            // A) TurnManager'a oyuncunun bu el açtığını bildir.
            // Böylece taşı sağa atıp turu bitirmesine izin verilecek.
            turnManager.hasOpenedThisTurn = true;

            // B) GameManager'a bu oyuncunun artık "Açanlar" listesinde olduğunu bildir (Ceza hesaplamaları için).
            playersWhoOpened.Add(GetPlayerQue());

            // C) Yandan Taş Alma Cezası Kontrolü (PDF Source 15/16)
            // Eğer oyuncu bu tur yandan taş çektiyse ve şimdi başarıyla açtıysa -> Rakip ceza yer.
            if (turnManager.hasPickedFromSide)
            {
                // GameManager cezayı uygular ve loglar.
                GameManager.Instance.ApplySidePickSuccessPenalty();

                // Not: Ceza sadece bir kere uygulanmalı, flag'i burada kapatmıyoruz
                // çünkü oyuncu birden fazla per açabilir. Turn bitince TurnManager sıfırlayacak.
            }
            hasOpenedSeries = true;
            Debug.Log("Oyuncu başarıyla per açtı. Kısıtlamalar kaldırıldı.");
        }
    }

    // ScoreManager.cs -> PlacePairPers Metodu

    private void PlacePairPers(List<List<Tiles>> validPers)
    {
        List<Vector2Int> positions = new List<Vector2Int>();
        bool hasOpenedAnyPair = false; // İşlem sonunda en az bir çift açıldı mı?

        foreach (var per in validPers)
        {
            // Kontrol: Tek renk mi VE Çift Per mi? (Örn: Kırmızı 5-5)
            if (IsSingleColor(per) && CheckForDoublePer(per))
            {
                int rowIndex = -1; // Uygun satır ara

                // Çift alanı genellikle 8 satırdan oluşur (Tasarımına göre değişebilir)
                for (int r = 0; r < 8; r++)
                {
                    if (occupiedRowsPair[r] == false)
                    {
                        bool allColumnsEmpty = true;
                        // Çift alanında her satırda 2 sütun vardır
                        for (int c = 0; c < 2; c++)
                        {
                            int columnIndex = r * 2 + c;

                            // Bounds kontrolü ve Doluluk kontrolü
                            if (
                                columnIndex < pairPerPlaceHolder.childCount
                                && pairPerPlaceHolder.GetChild(columnIndex).childCount > 0
                            )
                            {
                                allColumnsEmpty = false;
                                break;
                            }
                        }

                        if (allColumnsEmpty)
                        {
                            rowIndex = r;
                            break;
                        }
                    }
                }

                // Uygun satır bulunduysa yerleştir
                if (rowIndex != -1)
                {
                    hasOpenedAnyPair = true;

                    foreach (var tile in per)
                    {
                        int tileIndex = per.IndexOf(tile);
                        int columnIndex = rowIndex * 2 + tileIndex; // 0 veya 1

                        if (columnIndex < pairPerPlaceHolder.childCount)
                        {
                            // Pozisyonu kaydet
                            positions.Add(new Vector2Int(rowIndex, columnIndex));

                            // Görseli oluştur
                            GameObject tileInstance = Instantiate(
                                tilePrefab,
                                pairPerPlaceHolders[columnIndex]
                            );
                            meldTileGO.Add(tileInstance);

                            TileUI tileUI = tileInstance.GetComponent<TileUI>();
                            tileUI.CheckRowColoumn(rowIndex, columnIndex);

                            if (tileUI != null)
                                tileUI.SetTileData(tile);

                            tileUI.FitToParent();

                            // Veriyi kaydet (Geri alma için)
                            MeldedTileInfo newMeldInfo = new MeldedTileInfo(
                                tile,
                                MeldType.Pair,
                                rowIndex,
                                columnIndex
                            );
                            pendingMeldInfos.Add(newMeldInfo);
                            pendingMeldedTiles.Add(tile);

                            // Oyuncunun elinden taşı gizle/deaktif et
                            int playerTileIndex = tileDistrubite.GetPlayerTiles().IndexOf(tile);
                            int playerQue = GetPlayerQue();

                            tileDistrubite.photonView.RPC(
                                "DeactivatePlayerTile",
                                RpcTarget.AllBuffered,
                                playerQue,
                                playerTileIndex
                            );
                        }
                    }

                    occupiedRowsPair[rowIndex] = true;

                    // Güvenlik Kontrolü
                    if (per.Count != positions.Count)
                    {
                        Debug.LogError("HATA: Çift peri boyutu ile yerleşen taş sayısı uyuşmuyor!");
                        return;
                    }

                    // İşlekleri hesapla (Çift perlere de işleme yapılabilir)
                    UpdateAvailableForPlaceholders(per, rowIndex);

                    // Senkronizasyon (Diğer oyunculara bildir)
                    tileDistrubite.photonView.RPC(
                        "MergeValidpers",
                        RpcTarget.AllBuffered,
                        per,
                        GetPlayerQue(),
                        positions
                    );

                    positions.Clear(); // Bir sonraki per için temizle
                }
            }
        }

        // ---------------------------------------------------------
        // DURUM GÜNCELLEMELERİ VE CEZA KONTROLÜ
        // ---------------------------------------------------------
        if (hasOpenedAnyPair)
        {
            // A) Oyuncu Çift Açtı olarak işaretle
            // (Bu sayede Seri Açma butonu kilitlenecek ve işleme izni açılacak)
            hasOpenedPairs = true;

            // B) TurnManager'a "Açtı" bilgisini ver (Turu bitirebilmesi için)
            if (turnManager != null)
                turnManager.hasOpenedThisTurn = true;

            // C) GameManager ve Ceza Sistemi
            if (GameManager.Instance != null)
            {
                // Açanlar listesine ekle
                playersWhoOpened.Add(GetPlayerQue());

                // Yandan taş aldıysa ve şimdi çift açtıysa -> RAKİBE CEZA
                if (turnManager != null && turnManager.hasPickedFromSide)
                {
                    GameManager.Instance.ApplySidePickSuccessPenalty();
                }
            }

            Debug.Log("Çiftler başarıyla açıldı. Çift durumu aktif edildi.");
        }
    }

    void TakeBackPers()
    {
        // Eğer geri alınacak taş yoksa, işlemi durdur
        if (pendingMeldedTiles.Count == 0)
            return;
        List<Tiles> playerTiles = tileDistrubite.GetPlayerTiles();
        Tiles tiles = pendingMeldedTiles[0];
        int playerQue = GetPlayerQue();
        // Oyuncunun taş listesini al
        // Geri alınan taşları tahtadan sil
        foreach (var tile in pendingMeldedTiles)
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

        meldTileGO.Clear();
        foreach (var pendingJoker in pendingJokersToTake)
        {
            if (
                pendingJoker.originalPlaceholder != null
                && pendingJoker.originalPlaceholder.childCount > 0
            )
            {
                Transform jokerTransform = pendingJoker.originalPlaceholder.GetChild(0);
                if (jokerTransform != null)
                {
                    jokerTransform.gameObject.SetActive(true);
                    Debug.Log("Gizlenmiş bir joker geri alındı.");
                }
            }
        }
        pendingJokersToTake.Clear();
        tileDistrubite.photonView.RPC("UnMergeValidPers", RpcTarget.AllBuffered, playerQue);
        pendingMeldedTiles.RemoveAll(x => x != null);
        // Geri alınan taşları temizle
        pendingMeldedTiles.Clear(); // meldedTiles.Clear() yerine
        pendingMeldInfos.Clear(); // YENİ EKLENDİ
        // Tahtadaki satırları sıfırla
    }

    public void CommitJokerTransactions()
    {
        if (pendingJokersToTake.Count == 0)
            return;

        int playerQue = GetPlayerQue();
        Debug.Log(
            $"Oyuncu {playerQue} için {pendingJokersToTake.Count} adet joker işlemi onaylanıyor."
        );

        foreach (var pendingJoker in pendingJokersToTake)
        {
            // TileDistrubite'a RPC göndererek jokeri oyuncunun eline (veri listesi ve görsel olarak) eklet.
            tileDistrubite.photonView.RPC(
                "AddTileToPlayerHand",
                RpcTarget.AllBuffered,
                playerQue,
                pendingJoker.jokerData
            );
        }

        pendingJokersToTake.Clear();
    }
    #endregion
    #region Taş işleme on locale
    private List<ActiveTilePlacementInfo> pendingActivePlacements =
        new List<ActiveTilePlacementInfo>();

    // Hangi türde bir per açıldığını belirtmek için bir enum
    public enum MeldType
    {
        SingleColor, // Renk sırası (örn: Siyah 1-2-3)
        MultiColor, // Sayı grubu (örn: Farklı renklerde 7-7-7)
        Pair, // Çift per (örn: Kırmızı 5-5)
    }

    // Açılan her bir taşın detaylı bilgisini tutacak olan yapı
    [System.Serializable] // Bu satır, yapının Unity Inspector'da görülebilmesini sağlar (isteğe bağlı)
    public struct MeldedTileInfo
    {
        public Tiles tileData;
        public MeldType meldType;
        public int row;
        public int column;

        public MeldedTileInfo(Tiles data, MeldType type, int r, int c)
        {
            tileData = data;
            meldType = type;
            row = r;
            column = c;
        }
    }

    private bool[] availableColumns;

    // PARAMETRE DEĞİŞMEDİ: (List<Tiles> per, int rowIndex)
    public void UpdateAvailableForPlaceholders(List<Tiles> per, int rowIndex)
    {
        if (per.Count == 0)
            return;

        // ------------------------------------------------------------------------
        // 1. ADIM: BU PER HANGİ KUTUDA (CONTAINER) DURUYOR? ONU BULALIM.
        // (Böylece Ahmet'in taşı Mehmet'in masasına gitmez)
        // ------------------------------------------------------------------------
        Transform targetContainer = null;
        Tiles firstTileData = per[0];

        foreach (var ui in FindObjectsOfType<TileUI>())
        {
            if (ui.tileDataInfo == firstTileData)
            {
                if (ui.transform.parent != null && ui.transform.parent.parent != null)
                {
                    targetContainer = ui.transform.parent.parent;
                }
                break;
            }
        }
        if (targetContainer == null)
            return;

        List<Tiles> availableTiles = tileDistrubite.GetAvailableTiles(per);

        // -------------------------------------------------------
        // 1. SINGLE COLOR (Renkli Sıralı Per) - [DÜZELTME BURADA]
        // -------------------------------------------------------
        if (IsSingleColor(per) && SingleColorCheck(per))
        {
            var refTile = per.FirstOrDefault(t => t.type != TileType.Joker);
            if (refTile == null)
                return;
            TileColor perColor = refTile.color;

            var numbers = per.Select(tile => tile.number).ToList();
            bool hasJoker = per.Any(tile => tile.type == TileType.Joker);

            int minNumber = numbers.Min();
            int maxNumber = numbers.Max();

            // SAĞ TARAFI AÇ
            if (maxNumber != 13)
            {
                int rightIndex = maxNumber + 13 * rowIndex;
                if (rightIndex < targetContainer.childCount)
                {
                    Transform phTransform = targetContainer.GetChild(rightIndex);
                    Placeholder rightPlaceholder = phTransform.GetComponent<Placeholder>();

                    if (rightPlaceholder != null && phTransform.childCount == 0)
                    {
                        rightPlaceholder.available = true;
                        rightPlaceholder.AvailableTileInfo = availableTiles.FirstOrDefault(tile =>
                            tile.number == maxNumber + 1 && tile.color == perColor
                        );
                    }
                }
            }

            // SOL TARAFI AÇ
            if (minNumber > 1)
            {
                int leftIndex = (minNumber - 2) + 13 * rowIndex;
                if (leftIndex >= 0 && leftIndex < targetContainer.childCount)
                {
                    Transform phTransform = targetContainer.GetChild(leftIndex);
                    Placeholder leftPlaceholder = phTransform.GetComponent<Placeholder>();

                    if (leftPlaceholder != null && phTransform.childCount == 0)
                    {
                        leftPlaceholder.available = true;
                        leftPlaceholder.AvailableTileInfo = availableTiles.FirstOrDefault(tile =>
                            tile.number == minNumber - 1 && tile.color == perColor
                        );
                    }
                }
            }

            // --- [KRİTİK DÜZELTME] JOKER YERİNİ AÇMA ---
            if (hasJoker)
            {
                // Jokerin per içindeki konumunu (indexini) bulmak için matematik değil,
                // sahnedeki konumunu taramak daha güvenlidir.

                // Sadece bu satırı (rowIndex) tara
                int start = rowIndex * 13;
                int end = start + 13;
                int jokerPlaceholderIndex = -1;

                for (int i = start; i < end; i++)
                {
                    if (i >= targetContainer.childCount)
                        break;

                    Transform phTransform = targetContainer.GetChild(i);
                    if (phTransform.childCount > 0)
                    {
                        TileUI tUI = phTransform.GetChild(0).GetComponent<TileUI>();
                        if (tUI != null && tUI.tileDataInfo.type == TileType.Joker)
                        {
                            jokerPlaceholderIndex = i;
                            break; // Jokeri bulduk
                        }
                    }
                }

                if (jokerPlaceholderIndex != -1)
                {
                    Placeholder jokerPlaceholder = targetContainer
                        .GetChild(jokerPlaceholderIndex)
                        .GetComponent<Placeholder>();

                    if (jokerPlaceholder != null)
                    {
                        jokerPlaceholder.available = true;

                        // Jokerin yerine geçecek taşı belirle
                        // Formül: (Index % 13) + 1 = Sayı
                        int requiredNumber = (jokerPlaceholderIndex % 13) + 1;

                        // Listeden veya yeni oluşturarak
                        var requiredTile = availableTiles.FirstOrDefault(tile =>
                            tile.number == requiredNumber && tile.color == perColor
                        );

                        if (requiredTile == null)
                        {
                            requiredTile = new Tiles(perColor, requiredNumber, TileType.Number);
                        }

                        jokerPlaceholder.AvailableTileInfo = requiredTile;
                    }
                }
            }
        }
        // -------------------------------------------------------
        // 2. MULTI COLOR (Sayı Grubu)
        // -------------------------------------------------------
        else if (MultiColorCheck(per))
        {
            if (per.Count >= 3)
            {
                var refTile = per.FirstOrDefault(t => t.type != TileType.Joker);
                if (refTile == null)
                    return;
                int targetNumber = refTile.number;

                bool hasJoker = per.Any(tile => tile.type == TileType.Joker);
                var realColorsOnBoard = per.Where(t => t.type != TileType.Joker)
                    .Select(t => t.color)
                    .ToList();

                Tiles tileToPlace = null;

                if (per.Count == 3)
                {
                    tileToPlace = availableTiles.FirstOrDefault(tile =>
                        tile.number == targetNumber && !realColorsOnBoard.Contains(tile.color)
                    );
                }
                else if (per.Count == 4 && hasJoker)
                {
                    List<TileColor> allColors = new List<TileColor>
                    {
                        TileColor.yellow,
                        TileColor.blue,
                        TileColor.black,
                        TileColor.red,
                    };
                    var missingColor = allColors.Except(realColorsOnBoard).FirstOrDefault();
                    tileToPlace = new Tiles(missingColor, targetNumber, TileType.Number);
                }

                if (tileToPlace != null)
                {
                    // 3 TAŞ DURUMU
                    if (per.Count == 3)
                    {
                        int fourthIndex = 3 + (4 * rowIndex);
                        if (fourthIndex < targetContainer.childCount)
                        {
                            Transform phTransform = targetContainer.GetChild(fourthIndex);
                            Placeholder ph = phTransform.GetComponent<Placeholder>();
                            if (ph != null && phTransform.childCount == 0)
                            {
                                ph.available = true;
                                ph.AvailableTileInfo = tileToPlace;
                            }
                        }
                    }
                    // 4 TAŞ + JOKER DURUMU
                    else if (per.Count == 4 && hasJoker)
                    {
                        int start = rowIndex * 4;
                        int end = start + 4;
                        for (int i = start; i < end; i++)
                        {
                            if (i >= targetContainer.childCount)
                                break;
                            Transform phTransform = targetContainer.GetChild(i);
                            Placeholder ph = phTransform.GetComponent<Placeholder>();

                            if (ph != null && phTransform.childCount > 0)
                            {
                                TileUI tUI = phTransform.GetChild(0).GetComponent<TileUI>();
                                if (tUI != null && tUI.tileDataInfo.type == TileType.Joker)
                                {
                                    ph.available = true;
                                    ph.AvailableTileInfo = tileToPlace;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }
        // -------------------------------------------------------
        // 3. ÇİFT PER (Pair) KONTROLÜ - TAM DÜZELTME
        // -------------------------------------------------------
        else if (CheckForDoublePer(per) && IsSingleColor(per))
        {
            // Eğer per içinde Joker varsa (Demek ki takas yapılabilir)
            if (per.Any(tile => tile.type == TileType.Joker))
            {
                // 1. Referans Taşı Bul (Joker olmayan gerçek taş)
                // Örnek: Masa [Joker] - [Kırmızı 5] ise, referans [Kırmızı 5]tir.
                var refTile = per.FirstOrDefault(t => t.type != TileType.Joker);

                if (refTile != null)
                {
                    int jokerIndex = -1;

                    // 2. Sadece O Satırı Tara (Çiftler 2'li olduğu için *2 yapıyoruz)
                    int start = rowIndex * 2;
                    int end = start + 2;

                    for (int i = start; i < end; i++)
                    {
                        // Hedef Container sınır kontrolü
                        if (i >= targetContainer.childCount)
                            break;

                        Transform phTransform = targetContainer.GetChild(i);

                        // Kutu DOLU olmalı (Çünkü içinde Joker var)
                        if (phTransform.childCount > 0)
                        {
                            TileUI tile = phTransform.GetChild(0).GetComponent<TileUI>();

                            // İçindeki taş Joker mi?
                            if (tile != null && tile.tileDataInfo.type == TileType.Joker)
                            {
                                jokerIndex = i;
                                break; // Jokeri bulduk
                            }
                        }
                    }

                    // 3. Joker Bulunduysa O Kutuyu "Müsait" Yap
                    if (jokerIndex != -1)
                    {
                        Placeholder jokerPlaceholder = targetContainer
                            .GetChild(jokerIndex)
                            .GetComponent<Placeholder>();

                        if (jokerPlaceholder != null)
                        {
                            jokerPlaceholder.available = true;

                            // KRİTİK NOKTA: Jokerin kendi numarasını (örn 1) değil,
                            // yanındaki REFERANS TAŞIN (Kırmızı 5) verisini istiyoruz.
                            jokerPlaceholder.AvailableTileInfo = new Tiles(
                                refTile.color,
                                refTile.number,
                                TileType.Number
                            );

                            // Debug.Log($"Çift Per Joker Takası Açıldı! Yer: {jokerIndex}, İstenen: {refTile.color} {refTile.number}");
                        }
                    }
                }
            }
        }
    }

    #region ActivePers
    public void ActivePers()
    {
        HashSet<Tiles> usedTilesInThisSession = new HashSet<Tiles>();

        // Rezerve Kontrolü (Önceki düzeltmelerden)
        HashSet<Tiles> reservedTiles = new HashSet<Tiles>();
        if (pendingMeldedTiles != null)
            foreach (var t in pendingMeldedTiles)
                reservedTiles.Add(t);
        if (validPerss != null)
            foreach (var g in validPerss)
            foreach (var t in g)
                reservedTiles.Add(t);

        List<Tiles> currentPlayerTiles = tileDistrubite.GetPlayerTiles();
        int playerQue = GetPlayerQue();
        bool actionTaken;

        do
        {
            actionTaken = false;

            foreach (var player in PhotonNetwork.PlayerList)
            {
                player.CustomProperties.TryGetValue("PlayerQue", out object ownerQueValue);
                int ownerQue = (int)ownerQueValue;

                Transform meldContainer = GameObject.Find(player.NickName + " meld")?.transform;
                if (meldContainer == null)
                    continue;

                for (int i = 0; i < meldContainer.childCount; i++)
                {
                    Transform typeContainer = meldContainer.GetChild(i);
                    MeldType currentMeldType = (MeldType)i;

                    foreach (Transform placeholderTransform in typeContainer)
                    {
                        Placeholder currentPlaceholder =
                            placeholderTransform.GetComponent<Placeholder>();

                        if (
                            currentPlaceholder != null
                            && currentPlaceholder.available
                            && currentPlaceholder.AvailableTileInfo != null
                        )
                        {
                            foreach (var tileInHand in currentPlayerTiles)
                            {
                                if (usedTilesInThisSession.Contains(tileInHand))
                                    continue;
                                if (reservedTiles.Contains(tileInHand))
                                    continue; // Rezerve kontrolü
                                if (currentPlayerTiles.IndexOf(tileInHand) == -1)
                                    continue;

                                Tiles req = currentPlaceholder.AvailableTileInfo;
                                bool isMatch = false;

                                // EŞLEŞME KONTROLLERİ
                                if (
                                    tileInHand.color == req.color
                                    && tileInHand.number == req.number
                                )
                                {
                                    isMatch = true;
                                }
                                else if (
                                    currentMeldType == MeldType.MultiColor
                                    && tileInHand.number == req.number
                                )
                                {
                                    // MultiColor Esnek Eşleşme (Aynı kod bloğu)
                                    List<TileColor> usedColorsInRow = new List<TileColor>();
                                    Transform parentRow = placeholderTransform.parent;
                                    int myIndex = placeholderTransform.GetSiblingIndex();
                                    int rowStart = (myIndex / 4) * 4;
                                    int rowEnd = rowStart + 4;

                                    for (int k = rowStart; k < rowEnd; k++)
                                    {
                                        if (k >= parentRow.childCount)
                                            break;
                                        if (k == myIndex)
                                            continue;
                                        Transform sibling = parentRow.GetChild(k);
                                        if (sibling.childCount > 0)
                                        {
                                            TileUI t = sibling.GetChild(0).GetComponent<TileUI>();
                                            if (t != null && t.tileDataInfo.type != TileType.Joker)
                                                usedColorsInRow.Add(t.tileDataInfo.color);
                                        }
                                    }
                                    if (!usedColorsInRow.Contains(tileInHand.color))
                                        isMatch = true;
                                }

                                if (isMatch)
                                {
                                    Debug.Log(
                                        $"İşlek bulundu: {tileInHand.color} {tileInHand.number}"
                                    );

                                    // ===================================================================
                                    // [KRİTİK DÜZELTME] JOKER TAKAS VE TEMİZLİK
                                    // ===================================================================
                                    if (placeholderTransform.childCount > 0)
                                    {
                                        TileUI existingTileUI = placeholderTransform
                                            .GetChild(0)
                                            .GetComponent<TileUI>();

                                        if (existingTileUI != null)
                                        {
                                            // Eğer taş JOKER ise: Oyuncuya ver ve YOK ET
                                            if (existingTileUI.tileDataInfo.type == TileType.Joker)
                                            {
                                                Debug.LogWarning(
                                                    "JOKER ALINDI, OYUNCUYA YOLLANIYOR VE SAHNEDEN SİLİNİYOR."
                                                );

                                                // 1. Veriyi oyuncuya yolla
                                                tileDistrubite.photonView.RPC(
                                                    "AddTileToPlayerHand",
                                                    RpcTarget.AllBuffered,
                                                    playerQue,
                                                    existingTileUI.tileDataInfo
                                                );

                                                // 2. [DÜZELTME] Objeyi HİYERARŞİDEN SÖK ve YOK ET
                                                // SetParent(null) yaparak kutunun içini anında boşaltıyoruz.
                                                // Böylece Recalculate metodu "Kutu boş" veya "Yeni taş var" diyecek.
                                                existingTileUI.transform.SetParent(null);
                                                Destroy(existingTileUI.gameObject);
                                            }
                                            // Eğer taş Joker değilse (Hata durumu): Gizle ve yok et
                                            else
                                            {
                                                Debug.LogWarning("Çakışan taş temizleniyor.");
                                                existingTileUI.transform.SetParent(null);
                                                Destroy(existingTileUI.gameObject);
                                            }
                                        }
                                    }
                                    // ===================================================================

                                    // Kaydet
                                    pendingActivePlacements.Add(
                                        new ActiveTilePlacementInfo(
                                            tileInHand,
                                            ownerQue,
                                            currentMeldType,
                                            placeholderTransform.GetSiblingIndex()
                                        )
                                    );

                                    // Görsel (Yeni taşı oluştur)
                                    GameObject tempGO = Instantiate(
                                        tilePrefab,
                                        placeholderTransform
                                    );
                                    tempGO.GetComponent<TileUI>().SetTileData(tileInHand);
                                    tempGO.transform.localPosition = Vector3.zero;
                                    meldTileGO.Add(tempGO);
                                    tempGO.GetComponent<TileUI>().FitToParent();
                                    // Deaktif Et
                                    int idx = currentPlayerTiles.IndexOf(tileInHand);
                                    tileDistrubite.photonView.RPC(
                                        "DeactivatePlayerTile",
                                        RpcTarget.AllBuffered,
                                        playerQue,
                                        idx
                                    );

                                    usedTilesInThisSession.Add(tileInHand);
                                    currentPlaceholder.available = false;

                                    // Zincirleme Reaksiyon (SingleColor için)
                                    if (currentMeldType == MeldType.SingleColor)
                                    {
                                        int currentIndex = placeholderTransform.GetSiblingIndex();
                                        if (currentIndex > 0)
                                        {
                                            Transform ln = typeContainer.GetChild(currentIndex - 1);
                                            Placeholder lp = ln.GetComponent<Placeholder>();
                                            if (
                                                lp != null
                                                && ln.childCount == 0
                                                && tileInHand.number > 1
                                            )
                                            {
                                                lp.available = true;
                                                lp.AvailableTileInfo = new Tiles(
                                                    tileInHand.color,
                                                    tileInHand.number - 1,
                                                    TileType.Number
                                                );
                                            }
                                        }
                                        if (currentIndex < typeContainer.childCount - 1)
                                        {
                                            Transform rn = typeContainer.GetChild(currentIndex + 1);
                                            Placeholder rp = rn.GetComponent<Placeholder>();
                                            if (
                                                rp != null
                                                && rn.childCount == 0
                                                && tileInHand.number < 13
                                            )
                                            {
                                                rp.available = true;
                                                rp.AvailableTileInfo = new Tiles(
                                                    tileInHand.color,
                                                    tileInHand.number + 1,
                                                    TileType.Number
                                                );
                                            }
                                        }
                                    }

                                    // Sahneyi güncelle
                                    tileDistrubite.RecalculateAllAvailableSlots();

                                    actionTaken = true;
                                    goto nextPlaceholder;
                                }
                            }
                        }
                    }
                    nextPlaceholder:
                    ;
                }
            }
        } while (actionTaken);
    }
    #endregion
    public List<ActiveTilePlacementInfo> GetAndClearPendingActivePlacements()
    {
        if (pendingActivePlacements.Count == 0)
        {
            return null;
        }

        List<ActiveTilePlacementInfo> placementsToSend = new List<ActiveTilePlacementInfo>(
            pendingActivePlacements
        );
        pendingActivePlacements.Clear();
        return placementsToSend;
    }

    #endregion
    #region Hide and remove tiles from the Board

    public void CommitAndStoreMelds()
    {
        if (pendingMeldInfos.Count == 0)
        {
            Debug.Log("Onaylanacak yeni açılmış per bulunmuyor.");
            return;
        }

        // 1. Geçici bilgileri kalıcı listeye aktar
        committedMelds.AddRange(pendingMeldInfos);

        Debug.Log(
            $"{pendingMeldInfos.Count} adet taş bilgisi kalıcı listeye eklendi. Toplam: {committedMelds.Count}"
        );

        // 2. Oyuncunun elinden taşları RPC ile kaldır (RemoveMeldedTiles'daki mantık)
        List<Tiles> playerTiles = tileDistrubite.GetPlayerTiles();
        int playerQue = GetPlayerQue();

        foreach (var tileInfo in pendingMeldInfos)
        {
            int tileIndex = playerTiles.IndexOf(tileInfo.tileData);
            if (tileIndex != -1)
            {
                // Bu RPC, oyuncunun elindeki taş listesinden bu taşı silmeli veya deaktif etmeli
                tileDistrubite.photonView.RPC(
                    "MeldTiles",
                    RpcTarget.AllBuffered,
                    playerQue,
                    tileIndex
                );

                // Oyuncunun ıstakasındaki GameObject'i yok et
                DestroyTileGameObject(tileInfo.tileData);
            }
        }

        // 3. Geçici listeleri temizle
        pendingMeldInfos.Clear();
        pendingMeldedTiles.Clear();
        meldTileGO.Clear(); // Masadaki geçici GameObject referanslarını da temizle

        // Tahtadaki satırların doluluk durumunu sıfırlamaya gerek YOK, çünkü bunlar kalıcı oldu.
        // Eğer bu method sonrası yeni per açılabilecekse bu satırlar sıfırlanmamalı.
        // Eğer oyun bitiyorsa veya yeni el başlıyorsa o zaman sıfırlanabilir.
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
    #region Game Score Manager
    // ScoreManager.cs içine bu değişkeni ve metotları ekle:

    // Hangi oyuncunun açtığını tutan liste
    // ScoreManager.cs içine class seviyesinde ekle:
    public HashSet<int> playersWhoOpened = new HashSet<int>();

    // PlaceValidPers veya PlacePairPers metodunun BAŞARILI olduğu yere ekle:
    // playersWhoOpened.Add(GetPlayerQue());

    public bool HasPlayerOpened(int playerQue)
    {
        return playersWhoOpened.Contains(playerQue);
    }

    // Oyuncunun elinde kalan taşların sayısal toplamını hesaplar

    #endregion
}
