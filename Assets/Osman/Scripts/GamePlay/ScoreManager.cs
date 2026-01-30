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
    public Transform[] pairPerPlaceHolders;
    Transform numberPerPlaceHolder;
    public Transform[] numberPerPlaceHolders; // Player tile placeholders
    Transform colorPerPlaceHolder;

    [SerializeField]
    public Transform[] colorPerPlaceHolders;
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
        if (PhotonNetwork.IsMasterClient)
        {
            // 4 kişilik oyun varsayımıyla (veya oda kapasitesine göre)
            InitializeScores(4);
        }
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
            // --- YENİ: Burası renkli per alanı, işaretle ---
            Placeholder ph = colorPerPlaceHolders[i].GetComponent<Placeholder>();
            if (ph != null)
                ph.isMeldArea = true;
            // ----------------------------------------------
        }

        for (int i = 0; i < placeholderCount; i++)
        {
            numberPerPlaceHolders[i] = numberPerPlaceHolder.GetChild(i);
            // --- YENİ: Burası sayı per alanı, işaretle ---
            Placeholder ph = numberPerPlaceHolders[i].GetComponent<Placeholder>();
            if (ph != null)
                ph.isMeldArea = true;
            // ---------------------------------------------
        }

        for (int i = 0; i < placeholderCount3; i++)
        {
            pairPerPlaceHolders[i] = pairPerPlaceHolder.GetChild(i);
            // --- YENİ: Burası çift per alanı, işaretle ---
            Placeholder ph = pairPerPlaceHolders[i].GetComponent<Placeholder>();
            if (ph != null)
                ph.isMeldArea = true;
            // --------------------------------------------
        }
    }

    // ScoreManager.cs

    // Oyuncuların puanlarını tutan ana yapı
    // Key: Oyuncu Sırası (PlayerQue), Value: Ceza Puanı
    // ScoreManager.cs değişkenleri arasına:



    // Yeni tur başladığında bunları sıfırlamak için (GameManager veya TurnManager çağırabilir)
    // ScoreManager.cs içindeki metodu şu şekilde güncelle:
    public void ResetPlayerOpenStatus()
    {
        hasOpenedSeries = false;
        hasOpenedPairs = false;
        playersWhoOpened.Clear();
        pendingMeldInfos.Clear();
        pendingMeldedTiles.Clear();
        meldTileGO.Clear();

        // --- DİZİLERİ SIFIRLA (DİZME HATASINI ÖNLER) ---
        for (int i = 0; i < occupiedRows.Length; i++)
            occupiedRows[i] = false;
        for (int i = 0; i < occupiedRowsNumber.Length; i++)
            occupiedRowsNumber[i] = false;
        for (int i = 0; i < occupiedRowsPair.Length; i++)
            occupiedRowsPair[i] = false;

        Debug.Log("ScoreManager: Dizme alanları ve satır kayıtları sıfırlandı.");
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

    // ScoreManager.cs içine:

    private void UpdatePlayerCustomProperties(int playerQue)
    {
        // --- [ESKİ HATALI KOD] ---
        // Bu satır PlayerQue (1,2,3,4) değerini ActorNumber sanıp yanlış kişiyi buluyordu:
        // Photon.Realtime.Player player = PhotonNetwork.CurrentRoom.Players[playerQue];

        // --- [YENİ DOĞRU KOD] ---
        // PlayerQue değerine sahip olan oyuncuyu tek tek arayıp buluyoruz:

        Photon.Realtime.Player targetPlayer = null;

        foreach (var p in PhotonNetwork.PlayerList)
        {
            if (p.CustomProperties.TryGetValue("PlayerQue", out object qVal))
            {
                if ((int)qVal == playerQue)
                {
                    targetPlayer = p;
                    break;
                }
            }
        }

        if (targetPlayer != null)
        {
            targetPlayer.SetCustomProperties(
                new ExitGames.Client.Photon.Hashtable { { "PlayerScore", playerScores[playerQue] } }
            );
            // Debug.Log($"Puan güncellendi -> Koltuk: {playerQue}, Oyuncu: {targetPlayer.NickName}");
        }
        else
        {
            Debug.LogWarning(
                $"HATA: PlayerQue {playerQue} değerine sahip oyuncu odada bulunamadı!"
            );
        }
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
        var groups = GetSplittedGroups();

        int perCount = 0;
        int pairPerCountLocal = 0;
        int score = 0;
        int pairScore = 0;

        HashSet<List<Tiles>> countedPers = new HashSet<List<Tiles>>();
        validPerss.Clear();

        foreach (var per in groups)
        {
            if (ControlPer(new List<List<Tiles>> { per }))
            {
                if (!countedPers.Contains(per))
                {
                    countedPers.Add(per);
                    validPerss.Add(per);

                    if (CheckForDoublePer(per))
                    {
                        pairPerCountLocal++;
                        pairScore += CalculateDoublePerScore(per);
                    }
                    else
                    {
                        perCount++;
                        score += CalculateGroupScore(per);
                    }
                }
            }
        }

        totalScore = score;
        pairTotalScore = pairScore;
        totalPerCount = countedPers.Count;
        pairTotalPerCount = pairPerCountLocal;

        // --- UI GÜNCELLEME ---
        if (UIManager.Instance != null && GameManager.Instance != null)
        {
            UIManager.Instance.UpdatePlayerStats(
                totalScore,
                pairTotalScore, // Sayı değil, PUAN gönderiyoruz
                GameManager.Instance.CurrentTableLimit
            );
        }
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

    // ScoreManager.cs içine uygun bir yere (örneğin IsSingleColor metodunun üstüne) ekle:

    private bool IsIndicator(Tiles tile)
    {
        Tiles indicator = tileDistrubite.GetIndicatorTile();
        if (indicator == null || tile == null)
            return false;

        // Rengi ve numarası tutuyorsa bu bir Gösterge taşıdır
        return tile.color == indicator.color && tile.number == indicator.number;
    }

    // MEVCUT CheckForDoublePer FONKSİYONUNU BUL VE ŞÖYLE DEĞİŞTİR:
    public bool CheckForDoublePer(List<Tiles> tiles)
    {
        // Çift per kontrolü için taş sayısı 2 olmalı
        if (tiles.Count != 2)
            return false;

        Tiles tile1 = tiles[0];
        Tiles tile2 = tiles[1];

        // 1. GÖSTERGE KONTROLÜ (YENİ KURAL)
        // Eğer taşlardan biri Gösterge ise, diğeri ne olursa olsun bu geçerli bir çifttir.
        if (IsIndicator(tile1) || IsIndicator(tile2))
        {
            // Gösterge Joker gibi her şeye uyar
            return true;
        }

        // --- Buradan sonrası standart kontroller ---

        Tiles normalTile = null;
        List<Tiles> jokerStones = new List<Tiles>();

        foreach (var tile in tiles)
        {
            if (tile.type == TileType.Joker)
                jokerStones.Add(tile);
            else
            {
                if (normalTile == null)
                    normalTile = tile;
                else if (normalTile.number != tile.number)
                    return false; // Numaralar uyuşmuyor
            }
        }

        // İki joker, Bir joker+Bir normal veya İki normal (aynı numara)
        if (jokerStones.Count == 2)
            return true;
        if (jokerStones.Count == 1 && normalTile != null)
            return true;

        return normalTile != null && tiles[0].color == tiles[1].color; // Renk kontrolü (Standart Çift)
    }

    // MEVCUT CalculateDoublePerScore FONKSİYONUNU BUL VE ŞÖYLE DEĞİŞTİR:
    private int CalculateDoublePerScore(List<Tiles> tiles)
    {
        // YENİ KURAL: Eğer per içinde Gösterge varsa
        // Puan = Gösterge Sayısı * 2 (Yanındaki taş 13 bile olsa puan artmaz)
        foreach (var tile in tiles)
        {
            if (IsIndicator(tile))
            {
                // Örn: Gösterge 5 ise, 5 + 5 = 10 puan sayılır.
                return tile.number * 2;
            }
        }

        // Standart Hesap (Taşların toplamı)
        int score = 0;
        foreach (var tile in tiles)
        {
            score += tile.number;
        }
        return score;
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

    #endregion
    #endregion


    #region MELD_PER_METHODS


    #region Button functions

    int GetPlayerQue()
    {
        PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object queueValue);

        return (int)queueValue;
    }

    // ScoreManager.cs içine, class seviyesine ekle:

    // Yapılan hamlenin türü
    public enum ActionType
    {
        NormalPlace,
        JokerSwap,
    }

    // Hamlenin detaylarını tutan yapı
    public struct ProcessAction
    {
        public ActionType type;
        public Tiles tilePlayed; // Senin koyduğun taş (Örn: Kırmızı 12)
        public Tiles tileTaken; // Aldığın Joker (Swap ise)
        public Transform targetSlot; // İşlemin yapıldığı kutucuk
        public int penaltyVictimQue;
        public int penaltyAmount;
        public GameObject visualObject; // Masada oluşturulan görsel obje
    }

    // Hamleleri sırasıyla tutacak yığın (En son yapılanı ilk geri almak için Stack kullanıyoruz)
    private Stack<ProcessAction> actionHistory = new Stack<ProcessAction>();

    public void OnButtonClick()
    {
        if (hasOpenedPairs && !hasOpenedSeries)
        {
            Debug.LogWarning("Sadece Çift açtığınız için Seri açamazsınız!");
            return;
        }

        if (turnManager.canDrop == true)
        {
            // Bitiş Taşı Güvenlik Kontrolü
            int tilesToMeldCount = 0;
            foreach (var group in validPerss)
                tilesToMeldCount += group.Count;
            int currentHandCount = tileDistrubite.GetPlayerHandCount(GetPlayerQue());

            if (currentHandCount - tilesToMeldCount < 1)
            {
                Debug.LogWarning(
                    "HATA: Tüm taşları açamazsınız! Oyunu bitirmek için elinizde en az 1 taş kalmalı."
                );
                return;
            }

            // --- ORTAK LİMİT KONTROLÜ ---
            int currentLimit = GameManager.Instance.CurrentTableLimit;
            int myCurrentScore = totalScore;

            // Daha önce açtıysam limit beni bağlamaz
            bool limitPass = hasOpenedSeries ? true : (myCurrentScore > currentLimit);

            if (limitPass)
            {
                // [DEĞİŞİKLİK] Limiti Yükseltme (Açmadan Önce)
                if (!hasOpenedSeries && myCurrentScore > currentLimit)
                {
                    GameManager.Instance.TryUpdateTableLimit(myCurrentScore);
                }

                PlaceValidPers(validPerss);
                tileDistrubite.RecalculateAllAvailableSlots();
            }
            else
            {
                Debug.LogWarning(
                    $"Yetersiz Puan! Eliniz: {myCurrentScore}, Gereken: {currentLimit} üzeri."
                );
            }
        }
        else
        {
            Debug.Log("Sıra sizde değil.");
        }
    }

    public void OnPairButtonClick()
    {
        // 1. SIRA KONTROLÜ
        if (turnManager.canDrop == true)
        {
            // -------------------------------------------------------------------------
            // [YENİ EKLENEN KISIM] SERİ AÇANLAR İÇİN KISITLAMA
            // -------------------------------------------------------------------------
            // Kural: Eğer oyuncu daha önce Seri açmışsa (ve henüz Çift açmamışsa),
            // Masada henüz kimse (GameManager kontrolüyle) Çift açmadıysa, buton çalışmaz.
            if (hasOpenedSeries && !hasOpenedPairs && !GameManager.Instance.IsDoubleOpenedOnTable)
            {
                Debug.LogWarning(
                    "Seri açtınız! Masada başkası Çift açmadığı sürece Çift açamazsınız."
                );
                return; // İşlemi burada durduruyoruz.
            }
            // -------------------------------------------------------------------------

            // 2. BİTİŞ TAŞI GÜVENLİK KONTROLÜ
            int tilesToMeldCount = 0;
            foreach (var group in validPerss)
                tilesToMeldCount += group.Count;
            int currentHandCount = tileDistrubite.GetPlayerHandCount(GetPlayerQue());

            if (currentHandCount - tilesToMeldCount < 1)
            {
                Debug.LogWarning("HATA: Çift açarsanız atacak taşınız kalmaz!");
                return;
            }

            // 3. LİMİT VE KURAL KONTROLLERİ
            int currentLimit = GameManager.Instance.CurrentTableLimit;
            int myPairScore = pairTotalScore;

            bool limitPass = false;

            // KURAL 1: Daha önce Çift açtıysam -> Limit Yok
            if (hasOpenedPairs)
                limitPass = true;
            // KURAL 2: Daha önce Seri açtıysam -> Limit Yok
            // (Yukarıdaki 'if' bloğundaki kısıtlamayı geçtiysek, yani masada çift varsa,
            // seri açan kişi puana bakılmaksızın çift açabilir.)
            else if (hasOpenedSeries)
                limitPass = true;
            // KURAL 3: Masada başkası çift açtıysa -> Limit Yok
            else if (GameManager.Instance.IsDoubleOpenedOnTable)
                limitPass = true;
            // KURAL 4: Hiçbiri yoksa -> Limit Kontrolü
            else if (myPairScore > currentLimit)
                limitPass = true;

            if (limitPass)
            {
                if (pairTotalPerCount < 1)
                {
                    Debug.LogWarning("Elinizde geçerli bir çift per yok!");
                    return;
                }

                // Limiti Yükseltme (Sadece ilk kez açıyorsam ve limit kuralıyla açıyorsam)
                if (
                    !hasOpenedPairs
                    && !hasOpenedSeries
                    && !GameManager.Instance.IsDoubleOpenedOnTable
                    && myPairScore > currentLimit
                )
                {
                    Debug.Log($"Çift ile Limit Yükseldi! Yeni Baraj: {myPairScore}");
                    GameManager.Instance.TryUpdateTableLimit(myPairScore);
                }

                PlacePairPers(validPerss);
                tileDistrubite.RecalculateAllAvailableSlots();
            }
            else
            {
                Debug.LogWarning(
                    $"Yetersiz Çift Puanı! Eliniz: {myPairScore}, Gereken: {currentLimit} üzeri veya masada çift açılmalı."
                );
            }
        }
        else
        {
            Debug.Log("Sıra sizde değil.");
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

                            // --- A) GÖRSEL KAPATMA (Istakadan) ---
                            int tileIndexToDeactivate = -1;
                            for (int i = 0; i < playerTileContainer.childCount; i++)
                            {
                                if (playerTileContainer.GetChild(i).childCount > 0)
                                {
                                    var uiScript = playerTileContainer
                                        .GetChild(i)
                                        .GetChild(0)
                                        .GetComponent<TileUI>();
                                    // Referans kontrolü
                                    if (uiScript != null && uiScript.tileDataInfo == tile)
                                    {
                                        tileIndexToDeactivate = i;
                                        break;
                                    }
                                }
                            }

                            if (tileIndexToDeactivate != -1)
                            {
                                // Görseli yok et (DeactivatePlayerTileByIndex artık Destroy yapıyor olmalı)
                                tileDistrubite.photonView.RPC(
                                    "DeactivatePlayerTileByIndex",
                                    RpcTarget.All,
                                    GetPlayerQue(),
                                    tileIndexToDeactivate
                                );
                            }
                            else
                            {
                                Debug.LogError(
                                    "HATA: SingleColor için kapatılacak taşın ıstakadaki yeri bulunamadı!"
                                );
                            }

                            // --- B) VERİ SİLME (Listeden) - [EKSİK OLAN KISIM EKLENDİ] ---
                            // Veriyi listeden anında siliyoruz ki "Hayalet Taş" (Elde var gözüküp ekranda olmayan) oluşmasın.
                            tileDistrubite.photonView.RPC(
                                "RemoveTileFromPlayerListByValue",
                                RpcTarget.AllBuffered,
                                GetPlayerQue(),
                                tile
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

                            // --- A) GÖRSEL KAPATMA (Index ile) ---
                            int tileIndexToDeactivate = -1;
                            for (int i = 0; i < playerTileContainer.childCount; i++)
                            {
                                if (playerTileContainer.GetChild(i).childCount > 0)
                                {
                                    var uiScript = playerTileContainer
                                        .GetChild(i)
                                        .GetChild(0)
                                        .GetComponent<TileUI>();
                                    if (uiScript != null && uiScript.tileDataInfo == tile)
                                    {
                                        tileIndexToDeactivate = i;
                                        break;
                                    }
                                }
                            }

                            if (tileIndexToDeactivate != -1)
                            {
                                tileDistrubite.photonView.RPC(
                                    "DeactivatePlayerTileByIndex",
                                    RpcTarget.All,
                                    GetPlayerQue(),
                                    tileIndexToDeactivate
                                );
                            }
                            else
                            {
                                Debug.LogError(
                                    "HATA: MultiColor için kapatılacak taşın ıstakadaki yeri bulunamadı!"
                                );
                            }

                            // --- B) VERİ SİLME (Listeden) - [EKSİK OLAN KISIM EKLENDİ] ---
                            tileDistrubite.photonView.RPC(
                                "RemoveTileFromPlayerListByValue",
                                RpcTarget.AllBuffered,
                                GetPlayerQue(),
                                tile
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
        // 3. OYUN KURALLARI VE CEZA KONTROLLERİ
        // ---------------------------------------------------------

        if (hasOpenedAnyMeld)
        {
            // A) TurnManager'a oyuncunun bu el açtığını bildir.
            turnManager.hasOpenedThisTurn = true;

            // B) GameManager'a bu oyuncunun artık "Açanlar" listesinde olduğunu bildir.

            GameManager.Instance.photonView.RPC(
                "SyncOpenedPlayerRPC",
                RpcTarget.AllBuffered,
                GetPlayerQue()
            );
            // C) Yandan Taş Alma Cezası Kontrolü
            if (turnManager.hasPickedFromSide)
            {
                GameManager.Instance.photonView.RPC(
                    "ApplySidePickSuccessPenaltyRPC",
                    RpcTarget.MasterClient,
                    GetPlayerQue()
                );
            }

            hasOpenedSeries = true;
            Debug.Log("Oyuncu başarıyla per açtı. Kısıtlamalar kaldırıldı.");
        }
    }

    // ScoreManager.cs -> PlacePairPers Metodu

    private void PlacePairPers(List<List<Tiles>> validPairs)
    {
        List<Vector2Int> positions = new List<Vector2Int>();
        bool hasOpenedAnyPair = false;

        foreach (var pair in validPairs)
        {
            // --- 1. LİMİT KONTROLÜ (GÜNCELLENDİ) ---
            // KURAL:
            // 1. Oyuncu daha önce açmışsa (HasPlayerOpened) -> Limit Yok.
            // 2. Masada BİRİ çift açmışsa (IsDoubleOpenedOnTable) -> Limit Yok.
            // 3. Hiçbiri yoksa -> Limit (CurrentTableLimit) geçerli.

            int pairScore = CalculateDoublePerScore(pair); // Çiftin puanı

            bool canOpen = false;

            if (HasPlayerOpened(GetPlayerQue()))
            {
                canOpen = true; // Zaten açmışım, istediğimi yaparım.
            }
            else if (GameManager.Instance.IsDoubleOpenedOnTable)
            {
                canOpen = true; // Masada çift kilidi açılmış, puanım düşük olsa da açabilirim.
            }
            else if (pairTotalScore >= GameManager.Instance.CurrentTableLimit)
            {
                canOpen = true; // Puanım limite yetiyor.
            }

            if (!canOpen)
            {
                Debug.Log(
                    $"Çift limiti yetersiz ve masa kilidi kapalı. Çift Puanı: {pairTotalScore}"
                );
                continue; // Bu çifti açma, diğerine bak.
            }

            // Çift kontrolü (Renkler aynı, numaralar aynı)
            if (IsSingleColor(pair) && CheckForDoublePer(pair))
            {
                int rowIndex = -1;

                // Boş satır bul
                for (int r = 0; r < 8; r++) // Çift alanı genelde daha uzundur
                {
                    if (occupiedRowsPair[r] == false)
                    {
                        bool allColumnsEmpty = true;
                        for (int c = 0; c < 2; c++)
                        {
                            int columnIndex = r * 2 + c;
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

                if (rowIndex != -1)
                {
                    hasOpenedAnyPair = true;

                    foreach (var tile in pair)
                    {
                        int tileIndex = pair.IndexOf(tile);
                        int columnIndex = rowIndex * 2 + tileIndex;

                        if (columnIndex < pairPerPlaceHolder.childCount)
                        {
                            // 1. Pozisyonu Kaydet
                            positions.Add(new Vector2Int(rowIndex, columnIndex));

                            // 2. Masada Görseli Oluştur
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

                            // 3. Verileri Kaydet
                            MeldedTileInfo newMeldInfo = new MeldedTileInfo(
                                tile,
                                MeldType.Pair,
                                rowIndex,
                                columnIndex
                            );
                            pendingMeldInfos.Add(newMeldInfo);
                            pendingMeldedTiles.Add(tile);

                            // --- [DÜZELTME] HAYALET TAŞ SORUNU İÇİN ---

                            // A) GÖRSEL KAPATMA (Istakadan)
                            // Index ile bulup kapatıyoruz ki yanlış taş kapanmasın
                            int tileIndexToDeactivate = -1;
                            for (int i = 0; i < playerTileContainer.childCount; i++)
                            {
                                if (playerTileContainer.GetChild(i).childCount > 0)
                                {
                                    var uiScript = playerTileContainer
                                        .GetChild(i)
                                        .GetChild(0)
                                        .GetComponent<TileUI>();
                                    // Referans Eşitliği
                                    if (uiScript != null && uiScript.tileDataInfo == tile)
                                    {
                                        tileIndexToDeactivate = i;
                                        break;
                                    }
                                }
                            }

                            if (tileIndexToDeactivate != -1)
                            {
                                // Görseli yok et (DeactivatePlayerTile fonksiyonun Destroy yapmalı)
                                tileDistrubite.photonView.RPC(
                                    "DeactivatePlayerTileByIndex",
                                    RpcTarget.All,
                                    GetPlayerQue(),
                                    tileIndexToDeactivate
                                );
                            }

                            // B) VERİ SİLME (Listeden) - [KRİTİK DÜZELTME]
                            // Çift açarken veriyi listeden de silmemiz şart!
                            tileDistrubite.photonView.RPC(
                                "RemoveTileFromPlayerListByValue",
                                RpcTarget.AllBuffered,
                                GetPlayerQue(),
                                tile
                            );
                        }
                    }

                    occupiedRowsPair[rowIndex] = true;
                    UpdateAvailableForPlaceholders(pair, rowIndex);

                    // Diğer oyunculara göster
                    tileDistrubite.photonView.RPC(
                        "MergeValidpers",
                        RpcTarget.AllBuffered,
                        pair,
                        GetPlayerQue(),
                        positions
                    );
                    positions.Clear();
                }
            }
        }

        if (hasOpenedAnyPair)
        {
            turnManager.hasOpenedThisTurn = true;

            // ESKİ KOD:
            // playersWhoOpened.Add(GetPlayerQue());

            // --- [YENİ] SENKRON KOD: ---
            GameManager.Instance.photonView.RPC(
                "SyncOpenedPlayerRPC",
                RpcTarget.AllBuffered,
                GetPlayerQue()
            );

            hasOpenedPairs = true;

            // Masaya "Çift Açıldı" bilgisini gönder
            GameManager.Instance.SetDoubleOpened();

            if (turnManager.hasPickedFromSide)
            {
                GameManager.Instance.photonView.RPC(
                    "ApplySidePickSuccessPenaltyRPC",
                    RpcTarget.MasterClient,
                    GetPlayerQue()
                );
            }

            Debug.Log("Çift açıldı ve sisteme işlendi.");
        }
    }

    void TakeBackPers()
    {
        // Eğer geri alınacak taş yoksa işlem yapma
        if (pendingMeldedTiles.Count == 0)
            return;

        int playerQue = GetPlayerQue();

        // --- 1. ISTAKADAKİ GÖRSELLERİ GERİ AÇ ---
        foreach (var tile in pendingMeldedTiles)
        {
            bool found = false;

            // Istakadaki tüm slotları gez
            foreach (Transform placeholder in playerTileContainer)
            {
                if (placeholder.childCount > 0)
                {
                    GameObject tileObj = placeholder.GetChild(0).gameObject;

                    // Sadece KAPALI (ActiveSelf == false) olanlara bakıyoruz
                    if (!tileObj.activeSelf)
                    {
                        TileUI tileUI = tileObj.GetComponent<TileUI>();
                        if (tileUI != null)
                        {
                            // --- JOKER KONTROLÜ (KRİTİK KISIM) ---
                            // Eğer geri almaya çalıştığımız taş Joker ise, numarasına/rengine bakma!
                            // Çünkü CheckPattern onu değiştirmiş olabilir. Sadece TİP kontrolü yap.
                            if (
                                tile.type == TileType.Joker
                                && tileUI.tileDataInfo.type == TileType.Joker
                            )
                            {
                                tileObj.SetActive(true);
                                // Joker'i fabrika ayarlarına döndür (Eğer per içindeyken Rengi/Numarası değiştiyse düzelt)
                                ResetJokerData(tile);
                                ResetJokerData(tileUI.tileDataInfo);

                                Debug.Log("Gizlenen Joker geri alındı ve sıfırlandı.");
                                found = true;
                                break;
                            }
                            // --- NORMAL TAŞ KONTROLÜ ---
                            else if (tileUI.tileDataInfo == tile) // Referans eşitliği veya değer eşitliği
                            {
                                tileObj.SetActive(true);
                                Debug.Log($"Tile {tile.color} {tile.number} geri alındı.");
                                found = true;
                                break;
                            }
                        }
                    }
                }
            }
            if (!found)
                Debug.LogWarning(
                    $"Geri alınacak taş ıstakada bulunamadı: {tile.color} {tile.number} type:{tile.type}"
                );
        }

        // --- 2. MASADAKİ GÖRSELLERİ SİL ---
        foreach (GameObject meldTile in meldTileGO)
        {
            if (meldTile != null)
            {
                TileUI tileUI = meldTile.GetComponent<TileUI>();
                if (tileUI != null)
                {
                    Transform grandParent = meldTile.transform.parent.parent;

                    if (grandParent == colorPerPlaceHolder)
                        occupiedRows[tileUI.tileRow] = false;
                    else if (grandParent == numberPerPlaceHolder)
                        occupiedRowsNumber[tileUI.tileRow] = false;
                    else if (grandParent == pairPerPlaceHolder)
                        occupiedRowsPair[tileUI.tileRow] = false;
                }
                Destroy(meldTile);
            }
        }
        meldTileGO.Clear();

        // --- 3. JOKER TAKASLARINI GERİ AL ---
        // (ActivePers sırasında yerden alınan jokerler varsa)
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

                    // Masadaki jokeri de sıfırla ki temiz kalsın
                    TileUI jUI = jokerTransform.GetComponent<TileUI>();
                    if (jUI != null)
                        ResetJokerData(jUI.tileDataInfo);

                    Debug.Log("Gizlenmiş bir joker (Swap) geri alındı.");
                }
            }
        }
        pendingJokersToTake.Clear();

        // --- 4. SENKRONİZASYON VE TEMİZLİK ---
        tileDistrubite.photonView.RPC("UnMergeValidPers", RpcTarget.AllBuffered, playerQue);

        pendingMeldedTiles.Clear();
        pendingMeldInfos.Clear();

        // Puan hesaplamalarını sıfırla ki tekrar açmaya çalışırsa yanlış hesaplamasın
        // (Bu kısım senin CheckForPer fonksiyonunda her update'de yapılıyor olabilir ama garanti olsun)
    }

    // --- YARDIMCI METOD (Joker Verisini Düzeltmek İçin) ---
    private void ResetJokerData(Tiles tile)
    {
        if (tile != null && tile.type == TileType.Joker)
        {
            // Jokerin orijinal değerleri neyse ona döndür.
            // Genelde Number 0 veya 13 üzeridir ve rengi yoktur.
            // ScriptableObject'indeki orijinal haline bakarak burayı düzenleyebilirsin.
            tile.number = 0;
            // Rengi değiştirmemize gerek olmayabilir ama TileData'da boşsa boş yap.
        }
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

    // ScoreManager.cs -> UpdateAvailableForPlaceholders Metodu (DÜZELTİLMİŞ HALİ)

    public void UpdateAvailableForPlaceholders(List<Tiles> per, int rowIndex)
    {
        if (per.Count == 0)
            return;

        // 1. HEDEF CONTAINER'I BUL (Hangi oyuncunun hangi perindeyiz?)
        // Perin ilk taşına sahip olan UI elementini sahneden bulup onun ebeveynine (MeldContainer) ulaşıyoruz.
        Transform targetContainer = null;
        Tiles firstTileData = per[0];

        // Sahnedeki tüm TileUI'ları tara (Bunu optimize etmek mümkün ama şimdilik mantığı düzeltiyoruz)
        foreach (var ui in FindObjectsOfType<TileUI>())
        {
            // Referans eşitliği ile doğru görseli bul
            if (
                ui.tileDataInfo == firstTileData
                && ui.transform.parent != null
                && ui.transform.parent.parent != null
            )
            {
                targetContainer = ui.transform.parent.parent;
                break;
            }
        }

        // Eğer container bulunamazsa işlem yapma
        if (targetContainer == null)
            return;

        List<Tiles> availableTiles = tileDistrubite.GetAvailableTiles(per);

        // ---------------------------------------------------------
        // 1. SINGLE COLOR (RENKLİ SERİ)
        // ---------------------------------------------------------
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

            // --- SAĞ TARAFA EKLEME ---
            if (maxNumber != 13)
            {
                int rightIndex = maxNumber + 13 * rowIndex;
                // DÜZELTME: Local dizi yerine targetContainer kullanıyoruz
                if (rightIndex < targetContainer.childCount)
                {
                    Transform phTransform = targetContainer.GetChild(rightIndex);
                    Placeholder rightPlaceholder = phTransform.GetComponent<Placeholder>();

                    if (rightPlaceholder != null && phTransform.childCount == 0)
                    {
                        rightPlaceholder.available = true;
                        rightPlaceholder.AvailableTileInfo =
                            availableTiles.FirstOrDefault(tile =>
                                tile.number == maxNumber + 1 && tile.color == perColor
                            ) ?? new Tiles(perColor, maxNumber + 1, TileType.Number);
                    }
                }
            }

            // --- SOL TARAFA EKLEME ---
            if (minNumber > 1)
            {
                int leftIndex = (minNumber - 2) + 13 * rowIndex;
                // DÜZELTME: Local dizi yerine targetContainer kullanıyoruz
                if (leftIndex >= 0 && leftIndex < targetContainer.childCount)
                {
                    Transform phTransform = targetContainer.GetChild(leftIndex);
                    Placeholder leftPlaceholder = phTransform.GetComponent<Placeholder>();

                    if (leftPlaceholder != null && phTransform.childCount == 0)
                    {
                        leftPlaceholder.available = true;
                        leftPlaceholder.AvailableTileInfo =
                            availableTiles.FirstOrDefault(tile =>
                                tile.number == minNumber - 1 && tile.color == perColor
                            ) ?? new Tiles(perColor, minNumber - 1, TileType.Number);
                    }
                }
            }

            // --- JOKER YERİ AÇMA (SWAP) ---
            if (hasJoker)
            {
                int start = rowIndex * 13;
                int end = start + 13;

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
                            Placeholder jokerPlaceholder = phTransform.GetComponent<Placeholder>();
                            if (jokerPlaceholder != null)
                            {
                                jokerPlaceholder.available = true;
                                int requiredNumber = (i % 13) + 1;

                                var requiredTile =
                                    availableTiles.FirstOrDefault(tile =>
                                        tile.number == requiredNumber && tile.color == perColor
                                    ) ?? new Tiles(perColor, requiredNumber, TileType.Number);

                                jokerPlaceholder.AvailableTileInfo = requiredTile;
                            }
                        }
                    }
                }
            }
        }
        // ---------------------------------------------------------
        // 2. MULTI COLOR (SAYI GRUBU) - [HATANIN OLDUĞU YER DÜZELTİLDİ]
        // ---------------------------------------------------------
        else if (MultiColorCheck(per))
        {
            var refTile = per.FirstOrDefault(t => t.type != TileType.Joker);
            if (refTile != null)
            {
                int targetNumber = refTile.number;
                bool hasJoker = per.Any(tile => tile.type == TileType.Joker);

                List<TileColor> existingColors = per.Where(t => t.type != TileType.Joker)
                    .Select(t => t.color)
                    .ToList();

                List<TileColor> allColors = new List<TileColor>
                {
                    TileColor.yellow,
                    TileColor.blue,
                    TileColor.black,
                    TileColor.red,
                };

                List<TileColor> missingColors = allColors.Except(existingColors).ToList();

                // --- DURUM A: 3 TAŞ VARSA 4. YUVAYI AÇ ---
                if (per.Count == 3)
                {
                    int fourthIndex = (rowIndex * 4) + 3;

                    // [KRİTİK DÜZELTME]: numberPerPlaceHolders YERİNE targetContainer KULLANILDI
                    if (fourthIndex < targetContainer.childCount)
                    {
                        Transform phTransform = targetContainer.GetChild(fourthIndex);
                        Placeholder ph = phTransform.GetComponent<Placeholder>();

                        if (ph != null && phTransform.childCount == 0)
                        {
                            ph.available = true;
                            TileColor targetColor =
                                (missingColors.Count > 0) ? missingColors[0] : TileColor.black;
                            ph.AvailableTileInfo = new Tiles(
                                targetColor,
                                targetNumber,
                                TileType.Number
                            );
                        }
                    }
                }

                // --- DURUM B: JOKER VARSA TAKAS (SWAP) YUVASINI AÇ ---
                if (hasJoker)
                {
                    int start = rowIndex * 4;
                    int end = start + 4;

                    for (int i = start; i < end; i++)
                    {
                        // [KRİTİK DÜZELTME]: numberPerPlaceHolders YERİNE targetContainer KULLANILDI
                        if (i >= targetContainer.childCount)
                            break;

                        Transform phTransform = targetContainer.GetChild(i);
                        if (phTransform.childCount > 0)
                        {
                            TileUI tUI = phTransform.GetChild(0).GetComponent<TileUI>();

                            if (tUI != null && tUI.tileDataInfo.type == TileType.Joker)
                            {
                                Placeholder jokerPh = phTransform.GetComponent<Placeholder>();
                                if (jokerPh != null)
                                {
                                    jokerPh.available = true;
                                    TileColor requiredColor =
                                        (missingColors.Count > 0)
                                            ? missingColors[0]
                                            : TileColor.black;
                                    jokerPh.AvailableTileInfo = new Tiles(
                                        requiredColor,
                                        targetNumber,
                                        TileType.Number
                                    );
                                }
                            }
                        }
                    }
                }
            }
        }
        // ---------------------------------------------------------
        // 3. PAIR (ÇİFT PER)
        // ---------------------------------------------------------
        else if (CheckForDoublePer(per) && IsSingleColor(per))
        {
            if (per.Any(tile => tile.type == TileType.Joker))
            {
                var refTile = per.FirstOrDefault(t => t.type != TileType.Joker);
                if (refTile != null)
                {
                    int start = rowIndex * 2;
                    int end = start + 2;
                    for (int i = start; i < end; i++)
                    {
                        // DÜZELTME: targetContainer kullanımı
                        if (i >= targetContainer.childCount)
                            break;

                        Transform phTransform = targetContainer.GetChild(i);
                        if (phTransform.childCount > 0)
                        {
                            TileUI tile = phTransform.GetChild(0).GetComponent<TileUI>();
                            if (tile != null && tile.tileDataInfo.type == TileType.Joker)
                            {
                                Placeholder jokerPh = phTransform.GetComponent<Placeholder>();
                                if (jokerPh != null)
                                {
                                    jokerPh.available = true;
                                    jokerPh.AvailableTileInfo = new Tiles(
                                        refTile.color,
                                        refTile.number,
                                        TileType.Number
                                    );
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    #region ActivePers
    // ScoreManager.cs içine bu yapıyı ekle:

    // Bekleyen Senkronizasyon İşlemleri İçin Veri Yapısı
    [System.Serializable]
    public struct PendingSyncData
    {
        public int ownerQue;
        public Tiles tileData;
        public int meldType;
        public int placeholderIndex;
    }

    // Bu listeyi sınıfın en başına (değişkenlerin olduğu yere) ekle
    public List<PendingSyncData> pendingSyncActions = new List<PendingSyncData>();

    // --- 1. YENİ ACTIVE PERS (İŞLEME) METODU ---
    // ScoreManager.cs -> ActivePers Metodu

    public void ActivePers()
    {
        // 1. KURAL: Elini açmamış oyuncu işlek taş işleyemez
        if (!hasOpenedSeries && !hasOpenedPairs)
        {
            Debug.LogWarning("Taş işlemek için önce elinizi açmalısınız!");
            return;
        }

        bool actionTaken;
        int safetyLoop = 0;

        do
        {
            actionTaken = false;
            safetyLoop++;
            if (safetyLoop > 50)
                break;

            List<Tiles> currentHand = tileDistrubite.GetPlayerTiles();
            int playerQue = GetPlayerQue();

            foreach (var player in PhotonNetwork.PlayerList)
            {
                if (actionTaken)
                    break;

                GameObject meldContainerGO = GameObject.Find(player.NickName + " meld");
                if (meldContainerGO == null)
                    continue;

                Transform meldContainer = meldContainerGO.transform;

                for (int i = 0; i < meldContainer.childCount; i++)
                {
                    if (actionTaken)
                        break;

                    Transform typeContainer = meldContainer.GetChild(i);
                    MeldType meldType = (MeldType)i;

                    foreach (Transform phTransform in typeContainer)
                    {
                        Placeholder ph = phTransform.GetComponent<Placeholder>();

                        if (ph != null && ph.available && ph.AvailableTileInfo != null)
                        {
                            Tiles needed = ph.AvailableTileInfo;

                            // ID Eşleşmesi (Normal taşlar veya Joker)
                            Tiles matchInHand = currentHand.FirstOrDefault(t =>
                                (
                                    t.color == needed.color
                                    && t.number == needed.number
                                    && t.type != TileType.Joker
                                ) || (t.type == TileType.Joker)
                            );

                            if (matchInHand != null)
                            {
                                // Joker Takası Kontrolü
                                bool isSwap = false;
                                if (phTransform.childCount > 0)
                                {
                                    var existing = phTransform.GetChild(0).GetComponent<TileUI>();
                                    if (
                                        existing != null
                                        && existing.tileDataInfo.type == TileType.Joker
                                    )
                                        isSwap = true;
                                }

                                // Joker verip Joker almak yasak
                                if (isSwap && matchInHand.type == TileType.Joker)
                                    continue;

                                int ownerQue = -1;
                                if (player.CustomProperties.TryGetValue("PlayerQue", out object q))
                                    ownerQue = (int)q;

                                if (ownerQue != -1)
                                {
                                    // --- İŞLEMİ YAP ---
                                    PerformTileProcessing(
                                        playerQue,
                                        ownerQue,
                                        matchInHand,
                                        needed,
                                        phTransform,
                                        meldType,
                                        isSwap
                                    );

                                    // Masayı güncelle
                                    ph.available = false;
                                    tileDistrubite.RecalculateAllAvailableSlots();

                                    // --- [DÜZELTME BURADA] ---
                                    // Eğer Joker Takası (Swap) yapıldıysa, zincirleme reaksiyonu durdur.
                                    // Çünkü elimize Joker geçti, onu hemen harcamak istemeyebiliriz.
                                    if (isSwap)
                                    {
                                        Debug.Log(
                                            "Joker takası yapıldı. Otomatik döngü durduruluyor."
                                        );
                                        return; // Fonksiyondan tamamen çık (break değil, return)
                                    }

                                    actionTaken = true; // Normal işlemse devam et
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        } while (actionTaken);
    }

    private void PerformTileProcessing(
        int playerQue,
        int ownerQue,
        Tiles tileInHand,
        Tiles req,
        Transform targetPlaceholder,
        ScoreManager.MeldType meldType,
        bool isJokerSwap
    )
    {
        // 1. EL KONTROLÜ (ID ÜZERİNDEN)
        List<Tiles> myHand = tileDistrubite.GetPlayerTiles();
        Tiles actualTileInHand = myHand.FirstOrDefault(t => t.id == tileInHand.id);

        if (actualTileInHand == null)
        {
            Debug.LogError($"HATA: {tileInHand.id} ID'li taş elinizde bulunamadı!");
            return;
        }

        if (playerQue != ownerQue)
        {
            int penaltyAmount = 0;

            // --- [YENİ KURAL EKLEMESİ] ---
            // Gösterge taşını belirle
            Tiles indicator = tileDistrubite.GetIndicatorTile();
            bool isIndicatorProcessed = (
                indicator != null
                && actualTileInHand.color == indicator.color
                && actualTileInHand.number == indicator.number
            );

            if (isIndicatorProcessed)
            {
                // KURAL: Gösterge işlenirse ceza taşın değerinin 20 katıdır.
                // Örn: Gösterge 6 ise -> 6 * 20 = 120 Ceza.
                penaltyAmount = actualTileInHand.number * 20;
                Debug.Log($"[ÖZEL CEZA] Gösterge taşı işlendi! Ceza 20 katı: {penaltyAmount}");
            }
            else
            {
                // Standart Kural: Taşın değerinin 10 katı.
                penaltyAmount = actualTileInHand.number * 10;
            }
            // -----------------------------

            // Daha önce yazdığımız AddPendingPenalty metodunu kullanıyoruz
            AddPendingPenalty(ownerQue, penaltyAmount, actualTileInHand);

            Debug.Log($"[CEZA] Oyuncu {ownerQue} masasına taş işlendi. Ceza: {penaltyAmount}");
        }

        if (isJokerSwap && targetPlaceholder.childCount > 0)
        {
            Destroy(targetPlaceholder.GetChild(0).gameObject);
        }

        // 2. GÖRSEL OLUŞTURMA - Orijinal taşın tüm verisini (ID dahil) aktar
        GameObject tempGO = Instantiate(tilePrefab, targetPlaceholder);
        tempGO.name = "PERMANENT_MELD_TILE";

        TileUI uiScript = tempGO.GetComponent<TileUI>();
        if (uiScript != null)
        {
            // Orijinal ID'yi koruyoruz!
            uiScript.SetTileData(actualTileInHand);
            uiScript.FitToParent();
        }

        // 3. SENKRONİZASYON VERİSİ
        PendingSyncData syncData = new PendingSyncData
        {
            ownerQue = ownerQue,
            tileData = actualTileInHand, // Orijinal nesne
            meldType = (int)meldType,
            placeholderIndex = targetPlaceholder.GetSiblingIndex(),
        };
        pendingSyncActions.Add(syncData);

        // 4. VERİDEN SİLME (Sadece bir kez ve doğru ID ile)
        if (playerQue == GetPlayerQue())
        {
            tileDistrubite.photonView.RPC(
                "RemoveTileFromPlayerListByValue",
                RpcTarget.AllBuffered,
                playerQue,
                actualTileInHand
            );

            // Geri alma (Undo) kaydı için orijinal ID'yi sakla
            ProcessAction action = new ProcessAction
            {
                type = isJokerSwap ? ActionType.JokerSwap : ActionType.NormalPlace,
                tilePlayed = actualTileInHand,
                targetSlot = targetPlaceholder,
                visualObject = tempGO,
            };
            actionHistory.Push(action);
        }

        if (isJokerSwap)
        {
            // Elimize gelen Joker'in yeni bir ID'si olması normaldir çünkü yerden yeni bir nesne gibi gelir
            Tiles cleanJoker = new Tiles(TileColor.black, 0, TileType.Joker);
            tileDistrubite.photonView.RPC(
                "AddTileToPlayerHand",
                RpcTarget.AllBuffered,
                playerQue,
                cleanJoker
            );
        }
    }

    // --- 3. BEKLEYEN SENKRONİZASYONLARI ÇALIŞTIR ---
    // Bu metodu TileUI -> NextTurnRoutine içinde çağıracağız.
    public void ExecutePendingSyncs()
    {
        if (pendingSyncActions.Count > 0)
        {
            foreach (var sync in pendingSyncActions)
            {
                tileDistrubite.photonView.RPC(
                    "SyncProcessedTileRPC",
                    RpcTarget.OthersBuffered,
                    sync.ownerQue,
                    sync.tileData,
                    sync.meldType,
                    sync.placeholderIndex
                );
            }
            pendingSyncActions.Clear(); // Listeyi temizle
            Debug.Log("Tüm işlemeler diğer oyunculara gönderildi.");
        }
    }

    // Yardımcı Metot: Hem Joker takası hem normal yerleştirme için ortak mantık
    private bool TryProcessTiles(
        List<Tiles> currentPlayerTiles,
        int playerQue,
        HashSet<Tiles> usedTiles,
        HashSet<Tiles> reservedTiles,
        bool searchOnlyJokerSwap
    )
    {
        foreach (var player in PhotonNetwork.PlayerList)
        {
            player.CustomProperties.TryGetValue("PlayerQue", out object ownerQueValue);
            int ownerQue = (int)ownerQueValue;

            // Oyuncunun meld alanını bul
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

                    // Sadece Available olanlara bak
                    if (
                        currentPlaceholder != null
                        && currentPlaceholder.available
                        && currentPlaceholder.AvailableTileInfo != null
                    )
                    {
                        // --- JOKER KONTROLÜ ---
                        bool isPlaceholderJoker = false;
                        if (placeholderTransform.childCount > 0)
                        {
                            TileUI existing = placeholderTransform
                                .GetChild(0)
                                .GetComponent<TileUI>();
                            if (existing != null && existing.tileDataInfo.type == TileType.Joker)
                                isPlaceholderJoker = true;
                        }

                        // Eğer sadece Joker arıyorsak ve burası Joker değilse -> GEÇ
                        if (searchOnlyJokerSwap && !isPlaceholderJoker)
                            continue;

                        // Eğer sadece Normal arıyorsak ve burası Joker ise -> GEÇ (Jokerlere öncelik verdik)
                        if (!searchOnlyJokerSwap && isPlaceholderJoker)
                            continue;
                        if (searchOnlyJokerSwap && currentMeldType == MeldType.MultiColor)
                        {
                            // O satırdaki dolu taş sayısını bul (Joker dahil)
                            int tilesInRow = GetTileCountInMultiColorRow(placeholderTransform);

                            // Eğer per daha dolmamışsa (3 veya daha az taş varsa), Joker takası YAPMA.
                            // Bırak 2. turda normal ekleme (4. taş olma) yapsın.
                            if (tilesInRow < 4)
                                continue;
                        }
                        // --- ELDEKİ TAŞLARI TARA ---
                        for (int tIndex = 0; tIndex < currentPlayerTiles.Count; tIndex++)
                        {
                            Tiles tileInHand = currentPlayerTiles[tIndex];

                            if (usedTiles.Contains(tileInHand))
                                continue;
                            if (reservedTiles.Contains(tileInHand))
                                continue;

                            Tiles req = currentPlaceholder.AvailableTileInfo;
                            bool isMatch = false;

                            // 1. Renk ve Numara Birebir Uyuyor (Normal Perler)
                            if (
                                tileInHand.color == req.color
                                && tileInHand.number == req.number
                                && (
                                    tileInHand.type == TileType.Number
                                    || tileInHand.type == TileType.FakeJoker
                                ) // Sahte Joker eklendi
                            )
                            {
                                isMatch = true;
                            }
                            // 2. Oyuncunun attığı taş JOKER ise (Her yere uyar)
                            else if (tileInHand.type == TileType.Joker)
                            {
                                isMatch = true;
                            }
                            // 3. MultiColor Özel Kontrolü (Numara tutuyor ama renk farklı olabilir)
                            else if (
                                currentMeldType == MeldType.MultiColor
                                && tileInHand.number == req.number
                                && (
                                    tileInHand.type == TileType.Number
                                    || tileInHand.type == TileType.FakeJoker
                                ) // Sahte Joker eklendi
                            )
                            {
                                // O satırda hali hazırda var olan renkleri bul
                                List<TileColor> usedColorsInRow = GetUsedColorsInMultiColorRow(
                                    placeholderTransform
                                );

                                // --- KRİTİK DÜZELTME: JOKER TAKASI MI YOKSA NORMAL EKLEME Mİ? ---

                                // Eğer hedef yer bir JOKER ise (Takas yapılıyor):
                                if (isPlaceholderJoker)
                                {
                                    // KURAL: Eğer masada 4 taş varsa (3 renk + Joker), oyuncu SADECE eksik olan rengi koyabilir.
                                    // Var olan (usedColorsInRow) bir rengi Joker'in üzerine koyamaz.
                                    if (!usedColorsInRow.Contains(tileInHand.color))
                                    {
                                        isMatch = true;
                                    }
                                }
                                // Eğer hedef yer BOŞ BİR SLOT ise (4. taş ekleniyor):
                                else
                                {
                                    // KURAL: Sadece o grupta olmayan bir rengi ekleyebilir.
                                    if (!usedColorsInRow.Contains(tileInHand.color))
                                    {
                                        isMatch = true;
                                    }
                                }
                            }
                            else if (currentMeldType == MeldType.Pair)
                            {
                                isMatch = false;

                                // A) Standart Eşleşme (Renk ve Numara aynı)
                                if (
                                    req != null
                                    && tileInHand.color == req.color
                                    && tileInHand.number == req.number
                                )
                                {
                                    isMatch = true;
                                }
                                // B) Joker (Okey) atıyorsa
                                else if (tileInHand.type == TileType.Joker)
                                {
                                    isMatch = true;
                                }
                                // C) GÖSTERGE İLE İŞLEME (YENİ KURAL)
                                // Eğer elimdeki taş Gösterge ise ve hedef bir Çift alanıysa -> İşlenebilir!
                                else if (IsIndicator(tileInHand))
                                {
                                    isMatch = true;
                                    // "Herhangi bir taş ile işleyebilirsiniz" kuralı gereği,
                                    // Gösterge taşı her çiftin yanına eklenebilir.
                                }

                                if (isMatch)
                                {
                                    // PerformTileProcessing çağrılır...
                                    // Ancak CEZA hesabını PerformTileProcessing içinde özelleştirmemiz lazım.
                                }
                            }
                            if (isMatch)
                            {
                                PerformTileProcessing(
                                    playerQue,
                                    ownerQue,
                                    tileInHand,
                                    req,
                                    placeholderTransform,
                                    currentMeldType,
                                    isPlaceholderJoker
                                );
                                usedTiles.Add(tileInHand);
                                currentPlaceholder.available = false; // Çifte işlemeyi engelle
                                return true; // Bir işlem yaptık, döngüyü kırıp available recalculate yapacağız
                            }
                        }
                    }
                }
            }
        }
        return false;
    }

    // ScoreManager.cs içine en alta ekle
    private int GetTileCountInMultiColorRow(Transform placeholder)
    {
        Transform parent = placeholder.parent;
        int index = placeholder.GetSiblingIndex();
        int start = (index / 4) * 4;
        int end = start + 4;
        int count = 0;

        for (int i = start; i < end; i++)
        {
            if (i < parent.childCount && parent.GetChild(i).childCount > 0)
            {
                count++;
            }
        }
        return count;
    }

    // Yardımcı Metot: MultiColor satırındaki renkleri bulur
    private List<TileColor> GetUsedColorsInMultiColorRow(Transform placeholder)
    {
        List<TileColor> usedColors = new List<TileColor>();
        Transform parentRow = placeholder.parent;
        int myIndex = placeholder.GetSiblingIndex();
        int rowStart = (myIndex / 4) * 4;
        int rowEnd = rowStart + 4;

        for (int k = rowStart; k < rowEnd; k++)
        {
            if (k == myIndex || k >= parentRow.childCount)
                continue;
            if (parentRow.GetChild(k).childCount > 0)
            {
                TileUI t = parentRow.GetChild(k).GetChild(0).GetComponent<TileUI>();
                if (t != null && t.tileDataInfo.type != TileType.Joker)
                    usedColors.Add(t.tileDataInfo.color);
            }
        }
        return usedColors;
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
    // ScoreManager.cs içine ekle:

    public void UndoLastProcess()
    {
        if (actionHistory.Count == 0)
        {
            Debug.Log("Geri alınacak işlem yok.");
            return;
        }

        // En son işlemi çek
        ProcessAction lastAction = actionHistory.Pop();
        int playerQue = GetPlayerQue();

        // --- 1. CEZA İADESİ ---
        // Eğer bu işlemde birine ceza kesildiyse, geri alıyoruz.
        if (lastAction.penaltyVictimQue != -1 && lastAction.penaltyAmount > 0)
        {
            RemovePendingPenalty(lastAction.penaltyVictimQue, lastAction.penaltyAmount);
        }

        // --- 2. GÖRSELİ KALDIR ---
        if (lastAction.visualObject != null)
        {
            meldTileGO.Remove(lastAction.visualObject); // Listeden sil
            Destroy(lastAction.visualObject); // Sahneden sil
        }

        // --- 3. ELDEKİ TAŞI GERİ AÇ ---
        ReactivatePlayerTile(lastAction.tilePlayed);

        // --- 4. JOKER TAKASIYSA JOKERİ GERİ KOY ---
        if (lastAction.type == ActionType.JokerSwap)
        {
            // Jokeri oyuncudan sil (Çünkü ActivePers'te vermiştik)
            tileDistrubite.photonView.RPC(
                "RemoveTileFromPlayerListByValue",
                RpcTarget.AllBuffered,
                playerQue,
                lastAction.tileTaken
            );

            // Jokeri masaya geri koy
            GameObject jokerGO = Instantiate(tilePrefab, lastAction.targetSlot);
            TileUI jokerUI = jokerGO.GetComponent<TileUI>();
            if (jokerUI != null)
            {
                jokerUI.SetTileData(lastAction.tileTaken);
                jokerUI.FitToParent();
            }
        }

        // --- 5. LİSTELERİ TEMİZLE ---
        // Placeholder'ı tekrar müsait yap
        Placeholder ph = lastAction.targetSlot.GetComponent<Placeholder>();
        if (ph != null)
            ph.available = true;

        // Pending listesinden son ekleneni sil (En son eklenen en sondadır)
        if (pendingActivePlacements.Count > 0)
        {
            pendingActivePlacements.RemoveAt(pendingActivePlacements.Count - 1);
        }

        tileDistrubite.RecalculateAllAvailableSlots();
    }

    // Yardımcı Fonksiyon: Kapanan taşı geri açmak için
    private void ReactivatePlayerTile(Tiles tileData)
    {
        // Oyuncunun ıstakasını tara, kapalı olan taşı bul ve aç
        foreach (Transform t in playerTileContainer)
        {
            if (t.childCount > 0)
            {
                GameObject obj = t.GetChild(0).gameObject;

                // Sadece kapalı olanlara bak
                if (!obj.activeSelf)
                {
                    TileUI ui = obj.GetComponent<TileUI>();
                    if (ui != null)
                    {
                        bool isMatch = false;

                        // --- JOKER KONTROLÜ ---
                        // Eğer aradığımız taş Joker ise, ıstakadaki kapalı herhangi bir Joker bizimdir.
                        if (
                            tileData.type == TileType.Joker
                            && ui.tileDataInfo.type == TileType.Joker
                        )
                        {
                            isMatch = true;
                            ResetJokerData(ui.tileDataInfo); // Veriyi temizle
                            ResetJokerData(tileData); // Referansı temizle
                        }
                        // --- NORMAL TAŞ ---
                        else if (
                            ui.tileDataInfo.color == tileData.color
                            && ui.tileDataInfo.number == tileData.number
                            && ui.tileDataInfo.type == tileData.type
                        )
                        {
                            isMatch = true;
                        }

                        if (isMatch)
                        {
                            obj.SetActive(true);

                            // pendingMeldedTiles listesinden silme işlemi
                            // Joker ise referans veya değer tutmayabilir, tiple arayıp siliyoruz.
                            if (tileData.type == TileType.Joker)
                            {
                                var jokerInList = pendingMeldedTiles.FirstOrDefault(x =>
                                    x.type == TileType.Joker
                                );
                                if (jokerInList != null)
                                    pendingMeldedTiles.Remove(jokerInList);
                            }
                            else
                            {
                                if (pendingMeldedTiles.Contains(tileData))
                                    pendingMeldedTiles.Remove(tileData);
                                else
                                {
                                    var toRemove = pendingMeldedTiles.FirstOrDefault(x =>
                                        x.color == tileData.color && x.number == tileData.number
                                    );
                                    if (toRemove != null)
                                        pendingMeldedTiles.Remove(toRemove);
                                }
                            }
                            return; // Bulduk ve açtık, çıkabiliriz.
                        }
                    }
                }
            }
        }
    }

    // ScoreManager.cs

    public void CommitAndStoreMelds()
    {
        if (pendingMeldInfos.Count == 0)
            return;

        int playerQue = GetPlayerQue();

        foreach (var tileInfo in pendingMeldInfos)
        {
            // Taşın verisini ve ID'sini diğer oyunculara göndererek sildir
            tileDistrubite.photonView.RPC(
                "MeldTiles",
                RpcTarget.AllBuffered,
                playerQue,
                tileInfo.tileData // tileData zaten orijinal ID'yi taşıyor
            );

            // Kendi ekranımızdaki pasif görseli ID ile bulup yok et
            DestroyTileGameObject(tileInfo.tileData);
        }

        pendingMeldInfos.Clear();
        pendingMeldedTiles.Clear();
        meldTileGO.Clear();
    }

    private void DestroyTileGameObject(Tiles tile)
    {
        foreach (Transform placeholder in playerTileContainer)
        {
            if (placeholder.childCount > 0)
            {
                GameObject tileObj = placeholder.GetChild(0).gameObject;
                TileUI tileUI = tileObj.GetComponent<TileUI>();

                // KRİTİK DÜZELTME: Sadece gizlenmiş (pasif) ve ID'si birebir tutan taşı sil
                if (tileUI != null && !tileObj.activeSelf)
                {
                    if (tileUI.tileDataInfo.id == tile.id)
                    {
                        Destroy(tileObj);
                        return; // Sadece ilgili ID'li taşı sildik, çıkıyoruz.
                    }
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

    public List<Tiles> GetAllTilesPendingCommit()
    {
        List<Tiles> allPending = new List<Tiles>();

        // 1. Yeni Açılan Perler
        if (pendingMeldedTiles != null)
            allPending.AddRange(pendingMeldedTiles);

        // 2. İşlenen Taşlar (Active Placements)
        if (pendingActivePlacements != null)
        {
            foreach (var placement in pendingActivePlacements)
            {
                allPending.Add(placement.tileData);
            }
        }

        return allPending;
    }

    // --- [YENİ] OYUN SONU CEZA HESAPLAMA ---

    // PDF Kuralı: "Açıp bitmeyen oyuncu elindeki taş başına 60 ceza yer." (Sarı/x6 örneğinde)
    // Bu demek oluyor ki Baz Puan = 10. (10 x Renk Çarpanı)
    public int GetHandPenaltyForOpenedPlayer(int playerQue, int colorMultiplier)
    {
        // 1. Oyuncunun elindeki taş sayısını TileDistrubite'den al
        int handCount = tileDistrubite.GetPlayerHandCount(playerQue);

        // 2. Ceza Hesabı: Taş başına 10 Baz Puan * Renk Çarpanı
        // Örn: Sarı(6) için -> 10 * 6 = 60 Puan (Taş başına)
        int penaltyPerTile = 10 * colorMultiplier;

        int totalPenalty = handCount * penaltyPerTile;

        Debug.Log(
            $"[OYUN SONU] Oyuncu {playerQue} AÇTI ama BİTMEDİ. Elinde {handCount} taş var. Ceza: {totalPenalty}"
        );

        return totalPenalty;
    }

    public void ResetGameData()
    {
        playersWhoOpened.Clear();
        playerScores.Clear();
        // Diğer temizlikler...
    }
    // Oyuncunun elinde kalan taşların sayısal toplamını hesaplar

    #endregion
    #region ManuelMelding
    public bool ProcessManualDrop(Tiles tileData, Transform targetPlaceholder, int myPlayerQue)
    {
        Placeholder ph = targetPlaceholder.GetComponent<Placeholder>();

        // 1. TEMEL KONTROLLER
        if (ph == null)
            return false;

        // Yandan alınan taş kontrolü
        if (turnManager.hasPickedFromSide)
        {
            Tiles sideTile = GameManager.Instance.CurrentSidePickTile;
            if (
                sideTile != null
                && tileData.color == sideTile.color
                && tileData.number == sideTile.number
                && tileData.type == sideTile.type
            )
            {
                Debug.LogWarning("KURAL: Yandan aldığınız taşı işleyemezsiniz!");
                return false;
            }
        }

        // Joker Takası mı? (Hedef dolu ve içinde Joker var)
        bool isJokerSwapTarget = false;
        if (targetPlaceholder.childCount > 0)
        {
            TileUI existingTile = targetPlaceholder.GetChild(0).GetComponent<TileUI>();
            if (existingTile != null && existingTile.tileDataInfo.type == TileType.Joker)
                isJokerSwapTarget = true;
        }

        // Eğer yer müsait değilse ve Joker takası da değilse iptal et
        if (!ph.available && !isJokerSwapTarget)
            return false;

        // 2. TAŞ EŞLEŞME KONTROLÜ
        Tiles req = ph.AvailableTileInfo;
        bool isMatch = false;

        if (tileData.type == TileType.Joker)
            isMatch = true; // Oyuncu Joker atıyorsa her yere uyar
        // Birebir eşleşme (Renk + Numara) varsa her zaman doğrudur (SingleColor ve Pair için)
        else if (req != null && tileData.color == req.color && tileData.number == req.number)
            isMatch = true;
        // --- MULTICOLOR (Sayı Grubu) İSTİSNASI ---
        else if (targetPlaceholder.parent.GetSiblingIndex() == (int)MeldType.MultiColor)
        {
            // ÖNCE TAŞ SAYISINI KONTROL ET
            Transform rowParent = targetPlaceholder.parent;
            int tileCountInRow = 0;
            for (int i = 0; i < rowParent.childCount; i++)
            {
                if (rowParent.GetChild(i).childCount > 0)
                    tileCountInRow++;
            }

            // KURAL: MultiColor'da Joker takası SADECE 4 taş varken (full per) yapılabilir.
            if (isJokerSwapTarget && tileCountInRow < 4)
            {
                Debug.LogWarning(
                    "KURAL: Sayı gruplarında (MultiColor) joker almak için per 4'lü (dolu) olmalıdır!"
                );
                return false;
            }

            // Buradan sonrası standart eşleşme mantığı (Plan B dahil)
            int targetNumber = -1;

            if (req != null)
            {
                targetNumber = req.number;
            }
            // Req yoksa (bozuk per vb.) ama sayı 4 ise (yukarıyı geçtiyse) komşuya bak
            else if (isJokerSwapTarget)
            {
                for (int i = 0; i < rowParent.childCount; i++)
                {
                    if (rowParent.GetChild(i).childCount > 0)
                    {
                        var siblingTile = rowParent.GetChild(i).GetChild(0).GetComponent<TileUI>();
                        if (siblingTile != null && siblingTile.tileDataInfo.type != TileType.Joker)
                        {
                            targetNumber = siblingTile.tileDataInfo.number;
                            break;
                        }
                    }
                }
            }

            if (targetNumber != -1 && tileData.number == targetNumber)
            {
                List<TileColor> usedColors = GetUsedColorsInMultiColorRow(targetPlaceholder);
                if (!usedColors.Contains(tileData.color))
                {
                    isMatch = true;
                }
            }
        }
        // ------------------------------------------------

        if (!isMatch)
            return false;

        // 3. SAHİBİ BULMA
        int targetOwnerQue = GetOwnerQueFromPlaceholder(targetPlaceholder);
        if (targetOwnerQue == -1)
        {
            Debug.LogError("HATA: Hedef masanın sahibi bulunamadı!");
            return false;
        }

        MeldType meldType = (MeldType)targetPlaceholder.parent.GetSiblingIndex();

        bool isPlaceholderJoker = false;
        if (targetPlaceholder.childCount > 0)
        {
            TileUI existing = targetPlaceholder.GetChild(0).GetComponent<TileUI>();
            if (existing != null && existing.tileDataInfo.type == TileType.Joker)
                isPlaceholderJoker = true;
        }

        // 4. İŞLEMİ YAP
        PerformTileProcessing(
            myPlayerQue,
            targetOwnerQue,
            tileData,
            req ?? tileData,
            targetPlaceholder,
            meldType,
            isPlaceholderJoker
        );

        // 5. IŞIKLARI GÜNCELLE
        ph.available = false;
        if (tileDistrubite != null)
            tileDistrubite.RecalculateAllAvailableSlots();

        return true;
    }

    private int GetOwnerQueFromPlaceholder(Transform placeholder)
    {
        // Hiyerarşi yapın: "NickName meld" -> "Color/Number Place" -> "Placeholder"
        // Bu yüzden placeholder'ın dedesine (parent.parent) bakarak masa ismini buluyoruz.

        if (placeholder.parent == null || placeholder.parent.parent == null)
            return -1;

        string containerName = placeholder.parent.parent.name; // Örn: "Ahmet meld"

        foreach (var player in Photon.Pun.PhotonNetwork.PlayerList)
        {
            // SeatManager'da oluştururken verdiğin isim formatı: player.NickName + " meld"
            string expectedName = player.NickName + " meld";

            if (containerName == expectedName)
            {
                if (player.CustomProperties.TryGetValue("PlayerQue", out object queVal))
                {
                    return (int)queVal;
                }
            }
        }

        return -1; // Bulunamadı
    }
    #endregion


    public int CalculatePenaltyForPlayer(
        int playerQue,
        bool hasOpened,
        bool isWinner,
        Tiles indicatorTile // Bu parametre artık sadece bilgi amaçlı durabilir veya silebilirsin, aşağıda GameManager kullanacağız.
    )
    {
        // --- DEĞİŞİKLİK BURADA ---
        // Eskiden: int multiplier = GetColorMultiplier(indicatorTile);
        // Yeni: Artık çarpanı doğrudan GameManager'dan (Ana Merkezden) soruyoruz.
        // Böylece "Roket" (Sahte Okey) durumunu GameManager tek yerden yönetiyor.

        int multiplier = GameManager.Instance.GetCurrentColorMultiplier();

        // 1. KAZANAN OYUNCU (Düşüm)
        if (isWinner)
        {
            return -600;
        }

        // 2. HİÇ AÇMAMIŞ OYUNCU (YENİ KURAL: SABİT 600)
        if (!hasOpened)
        {
            // Kural gereği açmayana sabit 600 yazıyoruz.
            return 600;
        }
        // 3. AÇMIŞ AMA BİTMEMİŞ OYUNCU
        else
        {
            // Elinde kaç taş kaldığını bul
            TileDistrubite td = FindObjectOfType<TileDistrubite>();
            int remainingTileCount = td.GetPlayerHandCount(playerQue);

            // KURAL: Taş Adedi x 10 x Renk Çarpanı
            // Örn: Roket (x8) ve 5 taş kaldıysa -> 5 x 10 x 8 = 400 Ceza
            return remainingTileCount * 10 * multiplier;
        }
    }

    [System.Serializable]
    public struct PendingPenaltyInfo
    {
        public int victimQue; // Cezayı yiyecek kişi
        public int penaltyAmount; // Ceza miktarı
        public Tiles relatedTile; // Hangi taş yüzünden
    }

    // Bu tur içinde birikmiş ama henüz kesinleşmemiş cezalar
    public List<PendingPenaltyInfo> currentTurnPenalties = new List<PendingPenaltyInfo>();

    // 2. CEZAYI HAVUZA EKLEME (PerformTileProcessing İÇİNDE KULLANACAĞIZ)
    public void AddPendingPenalty(int victimQue, int amount, Tiles tile)
    {
        PendingPenaltyInfo info = new PendingPenaltyInfo
        {
            victimQue = victimQue,
            penaltyAmount = amount,
            relatedTile = tile,
        };
        currentTurnPenalties.Add(info);
        // Debug.Log($"[BEKLEYEN CEZA] Oyuncu {victimQue} için {amount} puan sıraya alındı.");
    }

    // 3. CEZAYI HAVUZDAN SİLME (UndoLastProcess İÇİNDE KULLANACAĞIZ)
    public void RemovePendingPenalty(int victimQue, int amount)
    {
        // Listeyi sondan başa tara (En son ekleneni silmek için)
        for (int i = currentTurnPenalties.Count - 1; i >= 0; i--)
        {
            if (
                currentTurnPenalties[i].victimQue == victimQue
                && currentTurnPenalties[i].penaltyAmount == amount
            )
            {
                currentTurnPenalties.RemoveAt(i);
                // Debug.Log("[UNDO] Bekleyen ceza iptal edildi.");
                return; // Sadece bir tane sil ve çık
            }
        }
    }

    // 4. TUR SONUNDA CEZALARI KESİNLEŞTİRME (COMMIT)
    // Bu metodu TileUI.ExecuteNextTurn içinde çağıracağız.
    public void CommitAllTurnPenalties()
    {
        if (currentTurnPenalties.Count == 0)
            return;

        Debug.Log($"Tur bitiyor. {currentTurnPenalties.Count} adet ceza işleniyor...");

        foreach (var penalty in currentTurnPenalties)
        {
            // GameManager üzerinden RPC gönder ve cezayı HERKESE duyur
            if (GameManager.Instance != null)
            {
                GameManager.Instance.photonView.RPC(
                    "ApplyProcessingPenaltyRPC",
                    Photon.Pun.RpcTarget.MasterClient,
                    penalty.victimQue,
                    penalty.penaltyAmount
                );
            }
        }

        // Listeyi temizle ki sonraki turda tekrar yazmasın
        currentTurnPenalties.Clear();
    }
}
