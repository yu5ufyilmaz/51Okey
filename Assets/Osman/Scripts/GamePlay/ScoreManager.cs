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
        ExitGames.Client.Photon.Hashtable resetProps = new ExitGames.Client.Photon.Hashtable();
        resetProps["PlayerScore"] = 0;
        PhotonNetwork.LocalPlayer.SetCustomProperties(resetProps);
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
            InitializeScores(4); // Odaya ilk girişte herkesi 0 puanla başlatır.
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

    // ScoreManager.cs içine

    public void ResetPlayerOpenStatus()
    {
        hasOpenedSeries = false;
        hasOpenedPairs = false;
        playersWhoOpened.Clear();
        pendingMeldInfos.Clear();
        pendingMeldedTiles.Clear();
        meldTileGO.Clear();
        actionHistory.Clear();
        pendingSyncActions.Clear();
        pendingJokersToTake.Clear();

        // YENİ: PenaltySystem'daki listeyi temizle (Manuel olarak listeye erişemeyiz, ama tur başı zaten boş olmalı)
        // Eğer PenaltySystem'da "ClearAll" gibi bir metodun yoksa, currentTurnPenalties public olduğu için:
        if (PenaltySystem.Instance != null)
        {
            PenaltySystem.Instance.currentTurnPenalties.Clear();
        }

        // ... (Kalan temizlik kodların aynen) ...
        pendingActivePlacements.Clear();

        // --- DİZİLERİ SIFIRLA (DİZME HATASINI ÖNLER) ---
        for (int i = 0; i < occupiedRows.Length; i++)
            occupiedRows[i] = false;
        for (int i = 0; i < occupiedRowsNumber.Length; i++)
            occupiedRowsNumber[i] = false;
        for (int i = 0; i < occupiedRowsPair.Length; i++)
            occupiedRowsPair[i] = false;

        // Sütun kilitlerini aç
        if (availableColumns != null)
        {
            for (int i = 0; i < availableColumns.Length; i++)
                availableColumns[i] = true;
        }

        Debug.Log("ScoreManager: Yeni el için tüm mantıksal veriler sıfırlandı.");
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
    // ScoreManager.cs içinde

    public void InitializeScores(int playerCount)
    {
        playerScores.Clear();

        // Yerel listeyi sıfırla
        for (int i = 1; i <= playerCount; i++)
        {
            playerScores[i] = 0;
        }

        // --- [YENİ EKLENEN KISIM] ---
        // İnternet üzerindeki "PlayerScore" etiketini de sıfırla.
        // Bunu yapmazsak, yeni odaya girse bile eski puanı görünür.
        ExitGames.Client.Photon.Hashtable initialProps = new ExitGames.Client.Photon.Hashtable();
        initialProps["PlayerScore"] = 0;
        PhotonNetwork.LocalPlayer.SetCustomProperties(initialProps);
        // ----------------------------

        Debug.Log("Skor tablosu ve PlayerScore etiketi sıfırlandı.");
    }

    // ScoreManager.cs içine:

    private void UpdatePlayerCustomProperties(int playerQue)
    {
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

        // Kural motoru için göstergeyi alıyoruz
        Tiles indicator = tileDistrubite.GetIndicatorTile();

        foreach (var per in groups)
        {
            if (ControlPer(new List<List<Tiles>> { per }))
            {
                if (!countedPers.Contains(per))
                {
                    countedPers.Add(per);
                    validPerss.Add(per);

                    // Hesaplamaları OkeyRuleEngine yapıyor
                    if (OkeyRuleEngine.CheckForDoublePer(per, indicator))
                    {
                        pairPerCountLocal++;
                        pairScore += OkeyRuleEngine.CalculateDoublePerScore(per);
                    }
                    else
                    {
                        perCount++;
                        score += OkeyRuleEngine.CalculateGroupScore(per);
                    }
                }
            }
        }

        totalScore = score;
        pairTotalScore = pairScore;
        totalPerCount = countedPers.Count;
        pairTotalPerCount = pairPerCountLocal;

        // UI Güncelleme
        if (UIManager.Instance != null && GameManager.Instance != null)
        {
            UIManager.Instance.UpdatePlayerStats(
                totalScore,
                pairTotalScore,
                GameManager.Instance.CurrentTableLimit
            );
        }
    }
    #endregion
    #region Is pers valid or not
    public bool ControlPer(List<List<Tiles>> perGroups)
    {
        Tiles indicator = tileDistrubite.GetIndicatorTile();

        foreach (var per in perGroups)
        {
            Debug.Log("Bu perde " + per.Count + " taş var.");

            // 1. Seri Kontrolü (RuleEngine üzerinden)
            if (
                OkeyRuleEngine.IsSingleColor(per, indicator) && OkeyRuleEngine.SingleColorCheck(per)
            )
            {
                Debug.Log("SingleColor per bulundu.");
                return true;
            }
            // 2. Sayı Grubu Kontrolü (RuleEngine üzerinden)
            else if (OkeyRuleEngine.MultiColorCheck(per))
            {
                Debug.Log("MultiColor per bulundu.");
                return true;
            }
            // 3. Çift Kontrolü (RuleEngine üzerinden)
            else if (
                OkeyRuleEngine.CheckForDoublePer(per, indicator)
                && OkeyRuleEngine.IsSingleColor(per, indicator)
            )
            {
                Debug.Log("Double per bulundu.");
                return true;
            }
        }
        return false;
    }

    // ScoreManager.cs içine uygun bir yere (örneğin IsSingleColor metodunun üstüne) ekle:

    private bool IsIndicator(Tiles tile)
    {
        Tiles indicator = tileDistrubite.GetIndicatorTile();
        return OkeyRuleEngine.IsIndicator(tile, indicator);
    }

    #endregion
    #region Calulate functions

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
        tempOpenedScore = totalScore;
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
        tempOpenedScore = pairTotalScore;
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
        // 1. Önce sıra kontrolü
        if (turnManager != null && !turnManager.IsPlayerTurn())
        {
            Debug.LogWarning("Sıra sizde değil, geri alma yapamazsınız.");
            return;
        }

        // 2. DURUM KONTROLÜ: Önce işlemeleri (Active Pers / Swap) kontrol et
        // Eğer oyuncu bu tur bir taş işlemişse veya joker değiştirmişse, önce bunları geri alıyoruz.
        if (actionHistory.Count > 0)
        {
            Debug.Log("Akıllı Buton: Son işleme geri alınıyor...");
            UndoLastProcess(); // Tekil işlemi geri al
            return; // Fonksiyondan çık, bir sonraki basışta diğerine bakar
        }

        // 3. DURUM KONTROLÜ: İşlemeler bittiyse (veya hiç yoksa), yeni açılan perlere bak
        // Eğer oyuncu bu tur yeni bir seri/çift açmışsa, onları komple geri topla.
        if (pendingMeldedTiles.Count > 0)
        {
            Debug.Log("Akıllı Buton: Açılan perler geri toplanıyor...");
            TakeBackPers(); // Tüm açılanları geri topla
            return;
        }

        Debug.Log("Geri alınacak herhangi bir işlem bulunamadı.");
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
        bool hasOpenedAnyMeld = false;

        // Kural motoru için göstergeyi al
        Tiles indicator = tileDistrubite.GetIndicatorTile();

        // ---------------------------------------------------------
        // 1. RENKLİ SIRALI PERLER (Single Color)
        // ---------------------------------------------------------
        foreach (var per in validPers)
        {
            // YENİ KONTROL: OkeyRuleEngine kullanılıyor
            if (
                OkeyRuleEngine.IsSingleColor(per, indicator) && OkeyRuleEngine.SingleColorCheck(per)
            )
            {
                int rowIndex = -1;

                // Boş satır bulma döngüsü (Senin kodun aynen kalıyor)
                for (int r = 0; r < 4; r++)
                {
                    if (occupiedRows[r] == false)
                    {
                        bool allColumnsFull = true;
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

                if (rowIndex != -1)
                {
                    hasOpenedAnyMeld = true;

                    foreach (var tile in per)
                    {
                        int columnIndex = rowIndex * 13 + (tile.number - 1);
                        if (columnIndex < colorPerPlaceHolders.Length)
                        {
                            positions.Add(new Vector2Int(rowIndex, columnIndex));

                            GameObject tileInstance = Instantiate(
                                tilePrefab,
                                colorPerPlaceHolders[columnIndex]
                            );
                            meldTileGO.Add(tileInstance);

                            TileUI tileUI = tileInstance.GetComponent<TileUI>();
                            tileUI.CheckRowColoumn(rowIndex, columnIndex);

                            if (tileUI != null)
                                tileUI.SetTileData(tile);

                            MeldedTileInfo newMeldInfo = new MeldedTileInfo(
                                tile,
                                MeldType.SingleColor,
                                rowIndex,
                                columnIndex
                            );
                            tileUI.FitToParent();

                            pendingMeldInfos.Add(newMeldInfo);
                            pendingMeldedTiles.Add(tile);

                            // GÖRSEL KAPATMA VE VERİ SİLME (Senin kodun aynen)
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

                            tileDistrubite.photonView.RPC(
                                "RemoveTileFromPlayerListByValue",
                                RpcTarget.AllBuffered,
                                GetPlayerQue(),
                                tile
                            );
                        }
                    }

                    occupiedRows[rowIndex] = true;
                    // Available slotları hesaplarken de per bilgisini gönderiyoruz
                    UpdateAvailableForPlaceholders(per, rowIndex);

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
        // 2. SAYI GRUBU PERLERİ (Multi Color)
        // ---------------------------------------------------------
        foreach (var per in validPers)
        {
            // YENİ KONTROL: OkeyRuleEngine kullanılıyor
            if (OkeyRuleEngine.MultiColorCheck(per))
            {
                int rowIndex = -1;

                // Boş satır bulma (Senin kodun aynen)
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
                    hasOpenedAnyMeld = true;

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
                                tileUI.SetTileData(tile);
                            tileUI.FitToParent();

                            MeldedTileInfo newMeldInfo = new MeldedTileInfo(
                                tile,
                                MeldType.MultiColor,
                                rowIndex,
                                columnIndex
                            );
                            pendingMeldInfos.Add(newMeldInfo);
                            pendingMeldedTiles.Add(tile);

                            // GÖRSEL KAPATMA VE VERİ SİLME
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

        // 3. OYUN KURALLARI VE CEZA KONTROLLERİ (Aynen Kalıyor)
        if (hasOpenedAnyMeld)
        {
            turnManager.hasOpenedThisTurn = true;
            GameManager.Instance.photonView.RPC(
                "SyncOpenedPlayerRPC",
                RpcTarget.AllBuffered,
                GetPlayerQue()
            );

            if (turnManager.hasPickedFromSide)
            {
                int tileNumber =
                    GameManager.Instance.CurrentSidePickTile != null
                        ? GameManager.Instance.CurrentSidePickTile.number
                        : 0;
                GameManager.Instance.photonView.RPC(
                    "ApplySidePickSuccessPenaltyRPC",
                    RpcTarget.MasterClient,
                    GetPlayerQue(),
                    tileNumber
                );
            }

            hasOpenedSeries = true;
            Debug.Log("Oyuncu başarıyla per açtı. Kısıtlamalar kaldırıldı.");
        }
    }

    private void PlacePairPers(List<List<Tiles>> validPairs)
    {
        List<Vector2Int> positions = new List<Vector2Int>();
        bool hasOpenedAnyPair = false;
        Tiles indicator = tileDistrubite.GetIndicatorTile();

        foreach (var pair in validPairs)
        {
            // YENİ: Puan hesabı RuleEngine'den
            int pairScore = OkeyRuleEngine.CalculateDoublePerScore(pair);

            // --- LİMİT KONTROLÜ (Değişmedi) ---
            bool canOpen = false;
            if (HasPlayerOpened(GetPlayerQue()))
                canOpen = true;
            else if (GameManager.Instance.IsDoubleOpenedOnTable)
                canOpen = true;
            else if (pairTotalScore >= GameManager.Instance.CurrentTableLimit)
                canOpen = true;

            if (!canOpen)
            {
                Debug.Log($"Çift limiti yetersiz. Çift Puanı: {pairTotalScore}");
                continue;
            }

            // YENİ KONTROL: RuleEngine ile Çift Kontrolü
            if (
                OkeyRuleEngine.IsSingleColor(pair, indicator)
                && OkeyRuleEngine.CheckForDoublePer(pair, indicator)
            )
            {
                int rowIndex = -1;

                // Boş satır bulma (Aynen kalıyor)
                for (int r = 0; r < 8; r++)
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
                            positions.Add(new Vector2Int(rowIndex, columnIndex));

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

                            MeldedTileInfo newMeldInfo = new MeldedTileInfo(
                                tile,
                                MeldType.Pair,
                                rowIndex,
                                columnIndex
                            );
                            pendingMeldInfos.Add(newMeldInfo);
                            pendingMeldedTiles.Add(tile);

                            // GÖRSEL KAPATMA VE SİLME
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
            GameManager.Instance.photonView.RPC(
                "SyncOpenedPlayerRPC",
                RpcTarget.AllBuffered,
                GetPlayerQue()
            );
            hasOpenedPairs = true;
            GameManager.Instance.SetDoubleOpened();

            if (turnManager.hasPickedFromSide)
            {
                int tileNumber =
                    (GameManager.Instance.CurrentSidePickTile != null)
                        ? GameManager.Instance.CurrentSidePickTile.number
                        : 0;
                GameManager.Instance.photonView.RPC(
                    "ApplySidePickSuccessPenaltyRPC",
                    RpcTarget.MasterClient,
                    GetPlayerQue(),
                    tileNumber
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

        Debug.Log($"Geri Topla Çalıştı: {pendingMeldedTiles.Count} adet taş geri alınıyor.");

        // --- 1. TAŞLARI OYUNCUYA GERİ VER (YENİDEN OLUŞTUR) ---
        foreach (var tile in pendingMeldedTiles)
        {
            // Joker ise fabrika ayarlarına döndür (Rengi/Numarası masada değişmiş olabilir)
            if (tile.type == TileType.Joker)
            {
                ResetJokerData(tile);
            }

            // TileDistrubite'a emri ver: "Bu taşı oyuncunun eline tekrar ekle!"
            // Hem veriyi listeye ekler, hem de görseli (prefab) tekrar oluşturur.
            tileDistrubite.photonView.RPC(
                "AddTileToPlayerHand",
                RpcTarget.AllBuffered,
                playerQue,
                tile
            );
        }

        // --- 2. MASADAKİ GÖRSELLERİ SİL ---
        foreach (GameObject meldTile in meldTileGO)
        {
            if (meldTile != null)
            {
                // Masadaki alanların (occupiedRows) kilitlerini açmamız lazım
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
                Destroy(meldTile); // Masadaki kopyayı yok et
            }
        }
        meldTileGO.Clear();

        // --- 3. JOKER TAKASLARINI GERİ AL (Varsa) ---
        // Eğer yerdeki bir jokeri alıp yerine taş koyduysak ve "Geri Al" dediysek:
        foreach (var pendingJoker in pendingJokersToTake)
        {
            if (pendingJoker.originalPlaceholder != null)
            {
                // Orijinal jokeri masada tekrar görünür yap
                // (Not: Eğer Swap işleminde Destroy ettiysek, burada Instantiate yapmamız gerekir.
                // Ancak mevcut yapında Swap sırasında sadece Destroy ediyorsan, buraya özel bir Re-Instantiate eklemeliyiz.
                // Şimdilik basitçe eldeki işlemi iptal ediyoruz.)

                // Basit çözüm: Swap işlemi "ActivePers" (İşleme) olduğu için buradaki TakeBackPers (Açma Geri Al)
                // genellikle onu etkilemez ama güvenli temizlik yapalım.
            }
        }
        pendingJokersToTake.Clear();

        // --- 4. SENKRONİZASYON VE TEMİZLİK ---
        // Diğer oyuncuların ekranındaki "bu oyuncu per açtı" bilgisini sil
        tileDistrubite.photonView.RPC("UnMergeValidPers", RpcTarget.AllBuffered, playerQue);

        // Listeleri temizle
        pendingMeldedTiles.Clear();
        pendingMeldInfos.Clear();

        // Oyuncunun "Açtı" durumunu iptal et (Eğer sadece bu taşlarla açtıysa)
        // Ama dikkat: Oyuncu daha önceki turda açmış olabilir.
        // Bu tur açtığı bayrağı (hasOpenedThisTurn) TurnManager'da sıfırlamalıyız.
        turnManager.hasOpenedThisTurn = false;

        // Eğer daha önce hiç açmamışsa, genel bayrakları da indir
        // (Burada mantığına göre; eğer commit edilmemişse zaten açılmamış sayılır)
        if (!committedMelds.Any())
        {
            hasOpenedSeries = false;
            hasOpenedPairs = false;
            // GameManager listesinden de çıkmak gerekebilir ama orası karışık, şimdilik UI düzelsin yeter.
        }

        // Skor hesaplamasını tetikle ki puanın düşsün
        CheckForPer();

        // Masadaki işlek ışıklarını tekrar hesapla (belki açtığım taşlar gidince masadaki durum değişmez ama olsun)
        tileDistrubite.RecalculateAllAvailableSlots();

        Debug.Log("Geri alma işlemi başarıyla tamamlandı.");
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

    // Bu taş yerden sökülüp alınabilir mi? (Joker veya Gösterge ise EVET)
    private bool IsSwappable(Tiles tile)
    {
        if (tile == null)
            return false;
        return tile.type == TileType.Joker || IsIndicator(tile);
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

    public void UpdateAvailableForPlaceholders(List<Tiles> per, int rowIndex)
    {
        if (per.Count == 0)
            return;

        // Hedef Container'ı bul (Aynen kalıyor)
        Transform targetContainer = null;
        Tiles firstTileData = per[0];
        foreach (var ui in FindObjectsOfType<TileUI>())
        {
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
        if (targetContainer == null)
            return;

        List<Tiles> availableTiles = tileDistrubite.GetAvailableTiles(per);
        Tiles indicator = tileDistrubite.GetIndicatorTile();

        // 1. SINGLE COLOR (RENKLİ SERİ)
        // YENİ: RuleEngine Kontrolü
        if (OkeyRuleEngine.IsSingleColor(per, indicator) && OkeyRuleEngine.SingleColorCheck(per))
        {
            var refTile = per.FirstOrDefault(t => t.type != TileType.Joker);
            if (refTile == null)
                return;

            TileColor perColor = refTile.color;
            var numbers = per.Select(tile => tile.number).ToList();
            bool hasJoker = per.Any(tile => tile.type == TileType.Joker);
            int minNumber = numbers.Min();
            int maxNumber = numbers.Max();

            // Sağ Taraf
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
                        rightPlaceholder.AvailableTileInfo =
                            availableTiles.FirstOrDefault(tile =>
                                tile.number == maxNumber + 1 && tile.color == perColor
                            ) ?? new Tiles(perColor, maxNumber + 1, TileType.Number);
                    }
                }
            }

            // Sol Taraf
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
                        leftPlaceholder.AvailableTileInfo =
                            availableTiles.FirstOrDefault(tile =>
                                tile.number == minNumber - 1 && tile.color == perColor
                            ) ?? new Tiles(perColor, minNumber - 1, TileType.Number);
                    }
                }
            }

            // Joker Swap
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
                        if (tUI != null && IsSwappable(tUI.tileDataInfo))
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
        // 2. MULTI COLOR (SAYI GRUBU)
        // YENİ: RuleEngine Kontrolü
        else if (OkeyRuleEngine.MultiColorCheck(per))
        {
            var refTile = per.FirstOrDefault(t => t.type != TileType.Joker);
            if (refTile != null)
            {
                int targetNumber = refTile.number;
                bool hasJoker = per.Any(tile => tile.type == TileType.Joker);
                int tileCountInPer = per.Count;
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

                // 4. Taşı Ekleme
                if (tileCountInPer == 3)
                {
                    int fourthIndex = (rowIndex * 4) + 3;
                    if (fourthIndex < targetContainer.childCount)
                    {
                        Transform phTransform = targetContainer.GetChild(fourthIndex);
                        Placeholder ph = phTransform.GetComponent<Placeholder>();
                        if (ph != null && phTransform.childCount == 0)
                        {
                            ph.available = true;
                            ph.AvailableTileInfo = new Tiles(
                                missingColors[0],
                                targetNumber,
                                TileType.Number
                            );
                        }
                    }
                }

                // Joker Swap
                if (hasJoker && tileCountInPer == 4)
                {
                    int start = rowIndex * 4;
                    for (int i = start; i < start + 4; i++)
                    {
                        if (i >= targetContainer.childCount)
                            break;
                        Transform phTransform = targetContainer.GetChild(i);
                        if (phTransform.childCount > 0)
                        {
                            TileUI tUI = phTransform.GetChild(0).GetComponent<TileUI>();
                            if (tUI != null && IsSwappable(tUI.tileDataInfo))
                            {
                                Placeholder jokerPh = phTransform.GetComponent<Placeholder>();
                                if (jokerPh != null)
                                {
                                    jokerPh.available = true;
                                    jokerPh.AvailableTileInfo = new Tiles(
                                        missingColors[0],
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
        // 3. PAIR (ÇİFT)
        // YENİ: RuleEngine Kontrolü
        else if (
            OkeyRuleEngine.CheckForDoublePer(per, indicator)
            && OkeyRuleEngine.IsSingleColor(per, indicator)
        )
        {
            if (per.Any(tile => IsSwappable(tile)))
            {
                var refTile = per.FirstOrDefault(t => !IsSwappable(t));
                if (refTile == null)
                    refTile = per[0];

                if (refTile != null)
                {
                    int start = rowIndex * 2;
                    int end = start + 2;
                    for (int i = start; i < end; i++)
                    {
                        if (i >= targetContainer.childCount)
                            break;
                        Transform phTransform = targetContainer.GetChild(i);
                        if (phTransform.childCount > 0)
                        {
                            TileUI tile = phTransform.GetChild(0).GetComponent<TileUI>();
                            if (tile != null && IsSwappable(tile.tileDataInfo))
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
                                // Joker Takası Kontrolü
                                bool isSwap = false;
                                if (phTransform.childCount > 0)
                                {
                                    var existing = phTransform.GetChild(0).GetComponent<TileUI>();

                                    if (existing != null && IsSwappable(existing.tileDataInfo))
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
        // 1. EL KONTROLÜ
        List<Tiles> myHand = tileDistrubite.GetPlayerTiles();
        Tiles actualTileInHand = myHand.FirstOrDefault(t => t.id == tileInHand.id);

        if (actualTileInHand == null)
        {
            Debug.LogError($"HATA: {tileInHand.id} ID'li taş elinizde bulunamadı!");
            return;
        }

        // 2. CEZA KONTROLÜ (Eğer başkasının masasına işliyorsam)
        if (playerQue != ownerQue)
        {
            // Gösterge kontrolü gerekip gerekmediğini belirle
            bool isIndicatorCheckNeeded = true;

            // PenaltySystem'den ceza miktarını hesaplat
            int penaltyAmount = PenaltySystem.Instance.CalculateProcessingPenalty(
                actualTileInHand,
                isIndicatorCheckNeeded
            );

            // Cezayı havuza ekle (Henüz yansıtma)
            PenaltySystem.Instance.AddPendingPenalty(ownerQue, penaltyAmount, "Processing Penalty");
        }

        // 3. JOKER SWAP MANTIĞI (Aynen Kalıyor)
        Tiles tileToGiveBack = null;
        if (isJokerSwap && targetPlaceholder.childCount > 0)
        {
            TileUI existingUI = targetPlaceholder.GetChild(0).GetComponent<TileUI>();
            if (existingUI != null)
            {
                if (existingUI.tileDataInfo.type == TileType.Joker)
                    tileToGiveBack = new Tiles(TileColor.black, 0, TileType.Joker);
                else
                    tileToGiveBack = new Tiles(
                        existingUI.tileDataInfo.color,
                        existingUI.tileDataInfo.number,
                        TileType.Number
                    );
            }
            Destroy(targetPlaceholder.GetChild(0).gameObject);
        }

        // 4. GÖRSEL OLUŞTURMA (Aynen Kalıyor)
        GameObject tempGO = Instantiate(tilePrefab, targetPlaceholder);
        tempGO.name = "PERMANENT_MELD_TILE";
        TileUI uiScript = tempGO.GetComponent<TileUI>();
        if (uiScript != null)
        {
            uiScript.SetTileData(actualTileInHand);
            uiScript.FitToParent();
        }

        // 5. SENKRONİZASYON VERİSİ (Aynen Kalıyor)
        PendingSyncData syncData = new PendingSyncData
        {
            ownerQue = ownerQue,
            tileData = actualTileInHand,
            meldType = (int)meldType,
            placeholderIndex = targetPlaceholder.GetSiblingIndex(),
        };
        pendingSyncActions.Add(syncData);

        // 6. VERİDEN SİLME VE GEÇMİŞE EKLEME
        if (playerQue == GetPlayerQue())
        {
            tileDistrubite.photonView.RPC(
                "RemoveTileFromPlayerListByValue",
                RpcTarget.AllBuffered,
                playerQue,
                actualTileInHand
            );

            ProcessAction action = new ProcessAction
            {
                type = isJokerSwap ? ActionType.JokerSwap : ActionType.NormalPlace,
                tilePlayed = actualTileInHand,
                tileTaken = tileToGiveBack,
                targetSlot = targetPlaceholder,
                penaltyVictimQue = (playerQue != ownerQue) ? ownerQue : -1, // Ceza yiyen varsa kaydet
                penaltyAmount =
                    (playerQue != ownerQue)
                        ? PenaltySystem.Instance.CalculateProcessingPenalty(actualTileInHand, true)
                        : 0,
                visualObject = tempGO,
            };
            actionHistory.Push(action);
        }

        // Joker İadesi
        if (isJokerSwap && tileToGiveBack != null)
        {
            tileDistrubite.photonView.RPC(
                "AddTileToPlayerHand",
                RpcTarget.AllBuffered,
                playerQue,
                tileToGiveBack
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
    #endregion
    #region Hide and remove tiles from the Board
    public void UndoLastProcess()
    {
        if (actionHistory.Count == 0)
            return;

        ProcessAction lastAction = actionHistory.Pop();
        int playerQue = GetPlayerQue();

        // 1. CEZA İADESİ (PenaltySystem Kullanılıyor)
        if (lastAction.penaltyVictimQue != -1 && lastAction.penaltyAmount > 0)
        {
            PenaltySystem.Instance.RemovePendingPenalty(
                lastAction.penaltyVictimQue,
                lastAction.penaltyAmount
            );
        }

        // 2. GÖRSELİ KALDIR (Aynen)
        if (lastAction.visualObject != null)
        {
            meldTileGO.Remove(lastAction.visualObject);
            Destroy(lastAction.visualObject);
        }

        // 3. TAŞI GERİ VER (Aynen)
        tileDistrubite.photonView.RPC(
            "AddTileToPlayerHand",
            RpcTarget.AllBuffered,
            playerQue,
            lastAction.tilePlayed
        );

        // 4. JOKER TAKASIYSA (Aynen)
        if (lastAction.type == ActionType.JokerSwap)
        {
            tileDistrubite.photonView.RPC(
                "RemoveTileFromPlayerListByValue",
                RpcTarget.AllBuffered,
                playerQue,
                lastAction.tileTaken
            );
            GameObject jokerGO = Instantiate(tilePrefab, lastAction.targetSlot);
            TileUI jokerUI = jokerGO.GetComponent<TileUI>();
            if (jokerUI != null)
            {
                jokerUI.SetTileData(lastAction.tileTaken);
                jokerUI.FitToParent();
            }
        }

        // 5. LISTELERİ TEMİZLE (Aynen)
        Placeholder ph = lastAction.targetSlot.GetComponent<Placeholder>();
        if (ph != null)
            ph.available = true;

        if (pendingActivePlacements.Count > 0)
            pendingActivePlacements.RemoveAt(pendingActivePlacements.Count - 1);

        if (pendingSyncActions.Count > 0)
        {
            pendingSyncActions.RemoveAt(pendingSyncActions.Count - 1);
            Debug.Log("Geri alma yapıldı: Senkronizasyon kuyruğundan işlem silindi.");
        }

        tileDistrubite.RecalculateAllAvailableSlots();
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
    public HashSet<int> playersWhoOpened = new HashSet<int>();

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

    #endregion
    #region ManuelMelding
    public bool ProcessManualDrop(Tiles tileData, Transform targetPlaceholder, int myPlayerQue)
    {
        Placeholder ph = targetPlaceholder.GetComponent<Placeholder>();

        // 1. TEMEL KONTROLLER
        if (ph == null)
            return false;

        // Yandan alınan taş kontrolü: Yandan alınan taş o tur işlenemez.
        if (turnManager.hasPickedFromSide)
        {
            Tiles sideTile = GameManager.Instance.CurrentSidePickTile;
            if (
                sideTile != null
                && tileData.color == sideTile.color
                && tileData.number == sideTile.number
                && tileData.id == sideTile.id
            ) // ID kontrolü en güvenlisidir
            {
                Debug.LogWarning("KURAL: Yandan aldığınız taşı işleyemezsiniz!");
                return false;
            }
        }

        bool isJokerSwapTarget = false;
        if (targetPlaceholder.childCount > 0)
        {
            TileUI existingTile = targetPlaceholder.GetChild(0).GetComponent<TileUI>();
            if (existingTile != null && IsSwappable(existingTile.tileDataInfo))
                isJokerSwapTarget = true;
        }

        // Eğer yer işlenebilir (available) değilse ve hedefte takas edilecek bir joker de yoksa iptal et
        if (!ph.available && !isJokerSwapTarget)
            return false;

        // 2. TAŞ EŞLEŞME KONTROLÜ
        Tiles req = ph.AvailableTileInfo;
        bool isMatch = false;
        MeldType meldType = (MeldType)targetPlaceholder.parent.GetSiblingIndex();

        // A) Oyuncu doğrudan bir Joker işliyorsa her yere uyar
        if (tileData.type == TileType.Joker)
        {
            isMatch = true;
        }
        // B) MULTICOLOR (Sayı Grubu) İÇİN ÖZEL KONTROLLER
        else if (meldType == MeldType.MultiColor)
        {
            Transform rowParent = targetPlaceholder.parent;
            int tileCountInRow = 0;

            // Satırdaki dolu yuva sayısını hesapla
            for (int i = 0; i < rowParent.childCount; i++)
            {
                if (rowParent.GetChild(i).childCount > 0)
                    tileCountInRow++;
            }

            // --- KRİTİK KURAL: MultiColor'da Joker takası SADECE 4 taş varken (full per) yapılabilir. ---
            if (isJokerSwapTarget && tileCountInRow < 4)
            {
                Debug.LogWarning(
                    "KURAL: Sayı gruplarında (MultiColor) joker almak için per 4'lü (tam) olmalıdır!"
                );
                return false;
            }

            // Sayı kontrolü (Hedeflenen sayı mı?)
            int targetNumber = -1;
            if (req != null)
            {
                targetNumber = req.number;
            }
            else if (isJokerSwapTarget) // Req null ise ama joker varsa yanındaki taştan sayıyı al
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

            // Sayı tutuyorsa renk kontrolüne geç
            if (targetNumber != -1 && tileData.number == targetNumber)
            {
                List<TileColor> usedColors = GetUsedColorsInMultiColorRow(targetPlaceholder);
                // Elimizdeki taşın rengi grupta henüz yoksa eşleşme sağlanır
                if (!usedColors.Contains(tileData.color))
                {
                    isMatch = true;
                }
                else
                {
                    Debug.LogWarning("KURAL: Sayı grubunda aynı renkten iki taş olamaz!");
                }
            }
        }
        // C) DİĞER PERLER (SingleColor ve Pair) İÇİN STANDART KONTROL
        else
        {
            if (req != null && tileData.color == req.color && tileData.number == req.number)
                isMatch = true;
        }

        if (!isMatch)
            return false;

        // 3. SAHİBİ VE SENKRONİZASYON VERİSİNİ BULMA
        int targetOwnerQue = GetOwnerQueFromPlaceholder(targetPlaceholder);
        if (targetOwnerQue == -1)
        {
            Debug.LogError("HATA: Hedef masanın sahibi bulunamadı!");
            return false;
        }

        // 4. İŞLEMİ GERÇEKLEŞTİR
        // PerformTileProcessing; görseli oluşturur, cezayı hesaplar ve taşı elden siler.
        PerformTileProcessing(
            myPlayerQue,
            targetOwnerQue,
            tileData,
            req ?? tileData,
            targetPlaceholder,
            meldType,
            isJokerSwapTarget
        );

        // 5. MASAYI VE IŞIKLARI (AVAILABLE) GÜNCELLE
        ph.available = false;
        if (tileDistrubite != null)
        {
            tileDistrubite.RecalculateAllAvailableSlots();
        }

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

    private int tempOpenedScore = 0;

    public void CommitFinalTableLimit()
    {
        // Eğer oyuncu bu tur Seri açmışsa
        if (hasOpenedSeries)
        {
            // Hafızadaki (açarkenki) puanı kullan!
            GameManager.Instance.TryUpdateTableLimit(tempOpenedScore);
            Debug.Log($"Tur bitti, Seri limiti güncellendi. Açılan Puan: {tempOpenedScore}");
        }
        // Eğer oyuncu bu tur Çift açmışsa
        else if (hasOpenedPairs)
        {
            GameManager.Instance.TryUpdateTableLimit(tempOpenedScore);
            Debug.Log($"Tur bitti, Çift limiti güncellendi. Açılan Puan: {tempOpenedScore}");
        }

        // İşlem bitince hafızayı sıfırla ki sonraki tura sarkmasın
        tempOpenedScore = 0;
    }
}
