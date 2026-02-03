using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class GameManager : MonoBehaviourPunCallbacks
{
    public static GameManager Instance;

    [Header("Game States")]
    public bool isGameReady = false;
    public bool isGameEnded = false;

    // --- ORTAK LİMİT (HEM SERİ HEM ÇİFT İÇİN) ---
    public int CurrentTableLimit
    {
        get
        {
            if (
                PhotonNetwork.CurrentRoom != null
                && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(
                    "TableLimit",
                    out object limit
                )
            )
            {
                return (int)limit;
            }
            return 51; // Varsayılan Başlangıç Limiti
        }
    }

    // --- REFERANSLAR ---
    private ScoreManager _scoreManager;
    public ScoreManager scoreManager
    {
        get
        {
            if (_scoreManager == null)
                _scoreManager = FindObjectOfType<ScoreManager>();
            return _scoreManager;
        }
    }

    private TileDistrubite _tileDistrubite;
    public TileDistrubite tileDistrubite
    {
        get
        {
            if (_tileDistrubite == null)
                _tileDistrubite = FindObjectOfType<TileDistrubite>();
            return _tileDistrubite;
        }
    }

    private TurnManager _turnManager;
    public TurnManager turnManager
    {
        get
        {
            if (_turnManager == null)
                _turnManager = FindObjectOfType<TurnManager>();
            return _turnManager;
        }
    }

    private Tiles currentSidePickTile;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        TileSerialization.RegisterCustomTypes();
        isGameReady = false;
    }

    // Yandan alınan taşı diğer scriptlerin okumasını sağlar
    public Tiles CurrentSidePickTile
    {
        get { return currentSidePickTile; }
    }

    private void Start()
    {
        EventDispatcher.RegisterFunction<HandData>("OnPlayerMoveFinished", CheckGameStatus);
        EventDispatcher.RegisterFunction<Tiles>("OnSideTilePicked", RecordSidePick);

        if (PhotonNetwork.IsMasterClient)
        {
            SetTableLimit(51);
        }
    }

    private void OnDestroy()
    {
        EventDispatcher.UnregisterListener<HandData>("OnPlayerMoveFinished", CheckGameStatus);
        EventDispatcher.UnregisterListener<Tiles>("OnSideTilePicked", RecordSidePick);
    }

    // --- OYUN AKIŞI ---
    public void SetGameReady()
    {
        isGameReady = true;
        Debug.Log("GameManager: Oyun Hazır.");
    }

    // --- RENK ÇARPANI HESAPLAMA ---
    public int GetCurrentColorMultiplier()
    {
        Tiles indicator = tileDistrubite.GetIndicatorTile();
        if (indicator == null)
            return 1; // Hata önleyici varsayılan

        // Eğer gösterge Sahte Okey ise (Resimli Taş), bu duruma "Roket" denir.
        if (indicator.type == TileType.FakeJoker)
            return 8;

        // KURAL: Standart Renk Çarpanları
        switch (indicator.color)
        {
            case TileColor.yellow:
                return 6; // Sarı
            case TileColor.red:
                return 5; // Kırmızı
            case TileColor.black:
                return 4; // Siyah
            case TileColor.blue:
                return 3; // Mavi
            default:
                return 1;
        }
    }

    // --- LİMİT GÜNCELLEME (KATLAMALI SİSTEM) ---
    public void TryUpdateTableLimit(int openedScore)
    {
        if (openedScore >= CurrentTableLimit)
        {
            SetTableLimit(openedScore);
            Debug.Log($"<color=green>MASA LİMİTİ YÜKSELDİ: Yeni Limit {openedScore}</color>");
        }
    }

    private void SetTableLimit(int limit)
    {
        Hashtable props = new Hashtable { { "TableLimit", limit } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        Debug.Log($"Limit Ayarlandı: {limit}");
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey("TableLimit"))
        {
            int newLimit = (int)propertiesThatChanged["TableLimit"];

            if (UIManager.Instance != null && scoreManager != null)
            {
                UIManager.Instance.UpdatePlayerStats(
                    scoreManager.totalScore,
                    scoreManager.pairTotalScore,
                    newLimit
                );
            }
            else
            {
                if (scoreManager == null)
                {
                    _scoreManager = FindObjectOfType<ScoreManager>();
                }
                // Debug.LogWarning("OnRoomPropertiesUpdate: UIManager veya ScoreManager henüz hazır değil!");
            }
        }
    }

    public void RecordSidePick(Tiles tile)
    {
        if (tile != null)
        {
            currentSidePickTile = tile;
            Debug.Log($"Yandan alınan taş kaydedildi: {tile.color} {tile.number} ID: {tile.id}");
        }
    }

    public void HandleFailedSidePick(Tiles tileToThrow)
    {
        int currentPlayerQue = tileDistrubite.GetQueueNumberOfPlayer(PhotonNetwork.LocalPlayer);

        // KURAL: Yandan alıp açamamanın cezası 250
        // DİKKAT: Artık PenaltySystem RPC'sini kullanabiliriz veya mevcut RPC'yi koruyabiliriz.
        // Mevcut RPC "AddGeneralPenaltyRPC" ScoreManager.UpdatePlayerScore çağırıyor, bu doğru.
        photonView.RPC("AddGeneralPenaltyRPC", RpcTarget.MasterClient, currentPlayerQue, 250);

        Tiles tileToReturn = (currentSidePickTile != null) ? currentSidePickTile : tileToThrow;
        StartCoroutine(RollbackSidePickProcess(currentPlayerQue, tileToReturn));
    }

    [PunRPC]
    public void AddGeneralPenaltyRPC(int playerQue, int penaltyScore)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            scoreManager.UpdatePlayerScore(playerQue, penaltyScore);
            Debug.Log($"[GENEL CEZA] Oyuncu {playerQue} için {penaltyScore} puan ceza işlendi.");
        }
    }

    [PunRPC]
    public void ApplySidePickSuccessPenaltyRPC(int currentPlayerQue, int pickedTileNumber)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        int penalty = (pickedTileNumber > 0) ? (pickedTileNumber * 10) : 250;
        int targetPlayerQue = (currentPlayerQue == 1) ? 4 : currentPlayerQue - 1;

        scoreManager.UpdatePlayerScore(targetPlayerQue, penalty);
        currentSidePickTile = null;

        Debug.Log(
            $"[YANDAN CEZA] Alan/Açan: {currentPlayerQue}, Cezayı Yiyen: {targetPlayerQue}, Puan: {penalty}"
        );
    }

    public void CancelSidePickAction()
    {
        if (currentSidePickTile == null)
            return;
        int currentPlayerQue = tileDistrubite.GetQueueNumberOfPlayer(PhotonNetwork.LocalPlayer);
        StartCoroutine(RollbackSidePickProcess(currentPlayerQue, currentSidePickTile, false));
    }

    private IEnumerator RollbackSidePickProcess(
        int playerQue,
        Tiles tileToReturn,
        bool giveNewTile = true
    )
    {
        tileDistrubite.photonView.RPC("ReturnTileToSide", RpcTarget.All, playerQue, tileToReturn);
        tileDistrubite.photonView.RPC(
            "RemoveTileFromPlayerListByValue",
            RpcTarget.AllBuffered,
            playerQue,
            tileToReturn
        );

        yield return new WaitForSeconds(0.2f);

        if (giveNewTile)
        {
            tileDistrubite.photonView.RPC(
                "AddTileFromMiddlePlayerList",
                RpcTarget.AllBuffered,
                playerQue
            );
        }
        else
        {
            turnManager.hasPickedFromSide = false;
            turnManager.canDrop = false;
        }
        currentSidePickTile = null;
    }

    [PunRPC]
    public void ApplyProcessingPenaltyRPC(int victimQue, int penaltyAmount)
    {
        // Bu fonksiyon artık PenaltySystem.ApplyPenaltyRPC ile aynı işi yapıyor.
        // Uyumluluk için bunu PenaltySystem'e yönlendirebiliriz veya mevcut halinde bırakabiliriz.
        // En temiz yöntem: PenaltySystem.Instance üzerinden işlem yapmak.

        if (PenaltySystem.Instance != null)
        {
            // Eğer master client ise uygula
            if (PhotonNetwork.IsMasterClient)
            {
                // Doğrudan scoreManager güncellemesi yerine PenaltySystem fonksiyonunu kullanmıyoruz
                // çünkü bu RPC zaten "Uygula" emridir.
                scoreManager.UpdatePlayerScore(victimQue, penaltyAmount);
                Debug.Log(
                    $"[İŞLEME CEZASI] Oyuncu {victimQue} üzerine taş işlendi. Ceza: +{penaltyAmount}"
                );
            }
        }
    }

    [PunRPC]
    public void ApplyBulkProcessingPenaltyRPC(int victimQue, int totalPenaltyAmount)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            scoreManager.UpdatePlayerScore(victimQue, totalPenaltyAmount);
            Debug.Log(
                $"<color=cyan>[OTOMATİK İŞLEME]</color> Oyuncu {victimQue} toplam {totalPenaltyAmount} ceza puanı aldı."
            );
        }
    }

    [PunRPC]
    public void RevertProcessingPenaltyRPC(int victimQue, int penaltyAmount)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            scoreManager.UpdatePlayerScore(victimQue, -penaltyAmount);
            Debug.Log(
                $"[İŞLEME CEZASI İPTAL] Oyuncu {victimQue} ceza iadesi yapıldı: -{penaltyAmount}"
            );
        }
    }

    // --- [DÜZELTME] CheckPenaltyRPC ---
    // Bu metot oyuncu bir taş attığında ceza yiyip yemeyeceğini kontrol eder.
    // Okey atma, işlek atma vb.
    [PunRPC]
    public void CheckPenaltyRPC(int playerQue, Tiles thrownTile)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        Tiles indicator = tileDistrubite.GetIndicatorTile();
        int penaltyAmount = 0;
        string reason = "";

        // --- OKEY TAŞINI BELİRLE ---
        int okeyNumber = -1;
        TileColor okeyColor = TileColor.black;

        if (indicator != null)
        {
            okeyColor = indicator.color;
            okeyNumber = indicator.number + 1;
            if (okeyNumber > 13)
                okeyNumber = 1;
        }

        bool isRealOkey = (thrownTile.color == okeyColor && thrownTile.number == okeyNumber);

        // 1. OKEY ATMA CEZASI
        if (
            thrownTile.type == TileType.Joker
            || (isRealOkey && thrownTile.type != TileType.FakeJoker)
        )
        {
            penaltyAmount = 250;
            reason = "Okey (Joker) Atıldı";
        }
        // 2. GÖSTERGE ATMA CEZASI
        else if (
            indicator != null
            && thrownTile.color == indicator.color
            && thrownTile.number == indicator.number
        )
        {
            penaltyAmount = 250;
            reason = "Gösterge Taşı (İşlek) Atıldı";
        }
        // 3. İŞLEK TAŞ ATMA CEZASI
        else
        {
            if (tileDistrubite.availableTiles != null && thrownTile.type != TileType.FakeJoker)
            {
                foreach (var t in tileDistrubite.availableTiles)
                {
                    if (
                        t.color == thrownTile.color
                        && t.number == thrownTile.number
                        && t.type != TileType.Joker
                    )
                    {
                        penaltyAmount = 250;
                        reason = "İşlek Taş Atıldı";
                        break;
                    }
                }
            }
        }

        if (penaltyAmount > 0)
        {
            scoreManager.UpdatePlayerScore(playerQue, penaltyAmount);
            Debug.Log(
                $"<color=red>CEZA KESİLDİ!</color> Sebep: {reason} -> Oyuncu {playerQue} +{penaltyAmount}"
            );
        }
    }

    // --- OYUN BİTİŞ KONTROLÜ ---
    public void CheckGameStatus(HandData data)
    {
        if (!isGameReady || isGameEnded)
            return;

        // 1. El Bitti (KAZANAN VAR)
        if (data.handTiles.Count == 0)
        {
            Debug.Log($"OYUN BİTTİ! Kazanan ActorNumber: {data.actorNumber}");
            photonView.RPC("FinishGameRPC", RpcTarget.All, data.actorNumber, false, false);
            return;
        }

        // 2. Taş Bitti (BERABERE)
        if (PhotonNetwork.IsMasterClient)
        {
            if (tileDistrubite.allTiles.Count == 0)
            {
                Debug.Log("OYUN BİTTİ! Ortada taş kalmadı. (Berabere)");
                photonView.RPC("FinishGameRPC", RpcTarget.All, -1, false, false);
            }
        }
    }

    public bool IsDoubleOpenedOnTable
    {
        get
        {
            if (
                PhotonNetwork.CurrentRoom != null
                && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(
                    "IsDoubleOpened",
                    out object val
                )
            )
            {
                return (bool)val;
            }
            return false;
        }
    }

    public void SetDoubleOpened()
    {
        if (!IsDoubleOpenedOnTable)
        {
            Hashtable props = new Hashtable { { "IsDoubleOpened", true } };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            Debug.Log("<color=green>MASADA ÇİFT AÇILDI!</color>");
        }
    }

    [PunRPC]
    public void FinishGameRPC(int winnerActorNumber, bool isOkeyShot, bool isDoubleFinish)
    {
        if (!isGameReady)
            return;
        isGameEnded = true;

        if (PhotonNetwork.IsMasterClient)
        {
            CalculateAndDistributeScores(winnerActorNumber, isOkeyShot, isDoubleFinish);
        }
    }

    private void CalculateAndDistributeScores(
        int winnerActorNumber,
        bool isOkeyShot,
        bool isDoubleFinish
    )
    {
        List<int> actors = new List<int>();
        List<int> listRewards = new List<int>();
        List<int> listPenalties = new List<int>();
        List<int> listNetScores = new List<int>();

        Tiles indicator = tileDistrubite.GetIndicatorTile();

        foreach (var player in PhotonNetwork.PlayerList)
        {
            actors.Add(player.ActorNumber);
            int pQue = tileDistrubite.GetQueueNumberOfPlayer(player);

            int myReward = 0;
            int myPenalty = 0;

            if (scoreManager.playerScores.ContainsKey(pQue))
            {
                myPenalty += scoreManager.playerScores[pQue];
            }

            bool isWinner = (winnerActorNumber != -1 && player.ActorNumber == winnerActorNumber);
            bool hasOpened = scoreManager.HasPlayerOpened(pQue);

            // --- YENİ PENALTY SYSTEM ÇAĞRISI ---
            // ScoreManager yerine PenaltySystem kullanıyoruz!
            int roundResult = PenaltySystem.Instance.CalculateEndGamePenalty(
                pQue,
                hasOpened,
                isWinner
            );

            if (roundResult < 0)
                myReward += roundResult;
            else
                myPenalty += roundResult;

            listRewards.Add(myReward);
            listPenalties.Add(myPenalty);
            listNetScores.Add(myReward + myPenalty);
        }

        photonView.RPC(
            "OnGameEndedDetailedRPC",
            RpcTarget.All,
            actors.ToArray(),
            listRewards.ToArray(),
            listPenalties.ToArray(),
            listNetScores.ToArray()
        );
    }

    [PunRPC]
    public void SyncOpenedPlayerRPC(int playerQue)
    {
        if (!scoreManager.playersWhoOpened.Contains(playerQue))
        {
            scoreManager.playersWhoOpened.Add(playerQue);
            Debug.Log($"[SYNC] Oyuncu {playerQue} açtı olarak sisteme işlendi.");
        }
    }

    [PunRPC]
    public void OnGameEndedDetailedRPC(
        int[] actors,
        int[] rewards,
        int[] penalties,
        int[] netScores
    )
    {
        isGameEnded = true;
        Dictionary<int, int> dictRewards = new Dictionary<int, int>();
        Dictionary<int, int> dictPenalties = new Dictionary<int, int>();
        Dictionary<int, int> dictNetScores = new Dictionary<int, int>();

        for (int i = 0; i < actors.Length; i++)
        {
            Player p = PhotonNetwork.CurrentRoom.GetPlayer(actors[i]);
            if (p != null && p.CustomProperties.TryGetValue("PlayerQue", out object q))
            {
                int pQue = (int)q;
                dictRewards[pQue] = rewards[i];
                dictPenalties[pQue] = penalties[i];
                dictNetScores[pQue] = netScores[i];
            }
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowDetailedGameOver(dictRewards, dictPenalties, dictNetScores);
        }
        StartCoroutine(RestartSequence());
    }

    public void CheckRoundEnd()
    {
        int currentRound = (int)PhotonNetwork.CurrentRoom.CustomProperties["CurrentRound"];
        int totalRounds = (int)PhotonNetwork.CurrentRoom.CustomProperties["TotalRounds"];

        if (currentRound < totalRounds)
        {
            Debug.Log($"El bitti! {currentRound}. el tamamlandı. Sonraki ele geçiliyor...");
            if (PhotonNetwork.IsMasterClient)
            {
                NextRoundSetup(currentRound + 1);
            }
        }
        else
        {
            Debug.Log("Tüm eller tamamlandı! Genel sonuçlar hesaplanıyor...");
        }
    }

    private IEnumerator RestartSequence()
    {
        int currentRound = (int)PhotonNetwork.CurrentRoom.CustomProperties["CurrentRound"];
        int totalRounds = (int)PhotonNetwork.CurrentRoom.CustomProperties["TotalRounds"];

        if (currentRound < totalRounds)
        {
            yield return new WaitForSeconds(10f);
            if (PhotonNetwork.IsMasterClient)
            {
                NextRoundSetup(currentRound + 1);
            }
        }
        else
        {
            Debug.Log("Tüm eller tamamlandı. Lobiye dönülüyor...");
            yield return new WaitForSeconds(10f);
            PhotonNetwork.LeaveRoom();
        }
    }

    private void NextRoundSetup(int nextRoundValue)
    {
        Hashtable cp = new Hashtable();
        cp.Add("CurrentRound", nextRoundValue);
        PhotonNetwork.CurrentRoom.SetCustomProperties(cp);
        photonView.RPC("RPC_PrepareNextRound", RpcTarget.All);
    }

    [PunRPC]
    public void RPC_PrepareNextRound()
    {
        isGameEnded = false;
        isGameReady = false;

        if (UIManager.Instance != null)
            UIManager.Instance.gameOverPanel.SetActive(false);

        turnManager.photonView.RPC("RPC_ResetTurnForNewRound", RpcTarget.All);

        if (PhotonNetwork.IsMasterClient)
        {
            SetTableLimit(51);
            Hashtable props = new Hashtable { { "IsDoubleOpened", false } };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        }

        if (scoreManager != null)
            scoreManager.ResetPlayerOpenStatus();

        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(WaitAndRedistribute());
        }
        else
        {
            if (tileDistrubite != null)
                tileDistrubite.ResetTableAndRedistribute();
        }
    }

    private IEnumerator WaitAndRedistribute()
    {
        yield return new WaitForSeconds(0.2f);
        if (tileDistrubite != null)
            tileDistrubite.photonView.RPC("ResetTableAndRedistribute", RpcTarget.All);
    }
}
