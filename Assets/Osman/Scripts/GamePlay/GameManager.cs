using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        if ((propertiesThatChanged.ContainsKey("TableLimit") || propertiesThatChanged.ContainsKey("IsDoubleOpened")) && tileDistrubite != null)
            tileDistrubite.RefreshTurnStartLimits();
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
            AnnouncePenalty(playerQue, penaltyScore, PenaltyReason.TookSideCouldNotOpen);
            Debug.Log($"[GENEL CEZA] Oyuncu {playerQue} için {penaltyScore} puan ceza işlendi.");
        }
    }

    // Master ceza yazınca herkes, cezayı yiyenin isimliğinin üstünde miktarı ve sebebi görür.
    public void AnnouncePenalty(int playerQue, int amount, PenaltyReason reason) =>
        photonView.RPC(nameof(ShowPenaltyRPC), RpcTarget.AllViaServer, playerQue, amount, (byte)reason);

    [PunRPC]
    void ShowPenaltyRPC(int playerQue, int amount, byte reason)
    {
        MatchScores.Instance?.RecordPenalty(playerQue, amount, (PenaltyReason)reason); // ceza kaydı (yalnız gösterim)
        if (UIManager.Instance != null)
            UIManager.Instance.ShowPenaltyFloat(playerQue, amount, PenaltyReasonText.Of((PenaltyReason)reason));
    }

    [PunRPC]
    public void ApplySidePickSuccessPenaltyRPC(int currentPlayerQue, int pickedTileNumber)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        // Kural kitabı: alınan taşın sayısının 10 katı (okey için de, 250 değil).
        int penalty = pickedTileNumber * 10;
        int targetPlayerQue = (currentPlayerQue == 1) ? 4 : currentPlayerQue - 1;

        scoreManager.UpdatePlayerScore(targetPlayerQue, penalty);
        AnnouncePenalty(targetPlayerQue, penalty, PenaltyReason.SideTileTaken);
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
            RpcTarget.All,
            playerQue,
            tileToReturn
        );

        yield return new WaitForSeconds(0.2f);

        if (giveNewTile && tileDistrubite.allTiles.Count > 0)
        {
            // RPC sadece veriye ekler; ıstakaya da konmazsa oyuncu görmediği bir taşla kalır, eli hiç bitmez.
            Tiles drawn = tileDistrubite.allTiles[0];
            tileDistrubite.photonView.RPC(
                "AddTileFromMiddlePlayerList",
                RpcTarget.All,
                playerQue
            );
            tileDistrubite.InstantiateTileInFirstEmptySlot(drawn);
            // Ceza bir kez yazıldı; artık normal çekmiş sayılır, atabilir (yoksa her atma denemesi yeni +250).
            turnManager.hasPickedFromSide = false;
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

        // Atanın bu tur açtığı/işlediği taşlar az önce masaya oturdu; işlek listesi onlarla güncel olsun.
        tileDistrubite.RecalculateAllAvailableSlots();
        PenaltyReason? reason = DiscardPenaltyReason(
            thrownTile,
            tileDistrubite.GetIndicatorTile(),
            IsDoubleOpenedOnTable,
            tileDistrubite.availableTiles
        );
        if (reason != null)
        {
            int penaltyAmount = 250;
            scoreManager.UpdatePlayerScore(playerQue, penaltyAmount);
            AnnouncePenalty(playerQue, penaltyAmount, reason.Value);
            Debug.Log(
                $"<color=red>CEZA KESİLDİ!</color> Atılan: {thrownTile.color} {thrownTile.number} -> Oyuncu {playerQue} +{penaltyAmount}"
            );
        }
    }

    // Okey, işlek taş ya da (masada çift açıldıysa) gösterge kopyası atmak: 250. Bir atışta tek ceza.
    // Kural kitabı: gösterge "bu şartlar altında" (biri çift açınca işlenebildiği için) işlek sayılır.
    public static int DiscardPenalty(
        Tiles thrownTile,
        Tiles indicator,
        bool pairsOpenedOnTable,
        IEnumerable<Tiles> islekTiles
    ) => DiscardPenaltyReason(thrownTile, indicator, pairsOpenedOnTable, islekTiles) != null ? 250 : 0;

    // Ceza yoksa null; varsa sebep.
    public static PenaltyReason? DiscardPenaltyReason(
        Tiles thrownTile,
        Tiles indicator,
        bool pairsOpenedOnTable,
        IEnumerable<Tiles> islekTiles
    )
    {
        int okeyNumber = -1;
        TileColor okeyColor = TileColor.black;
        if (indicator != null)
        {
            okeyColor = indicator.color;
            okeyNumber = indicator.number % 13 + 1;
        }
        bool isRealOkey = thrownTile.color == okeyColor && thrownTile.number == okeyNumber;

        if (thrownTile.type == TileType.Joker || (isRealOkey && thrownTile.type != TileType.FakeJoker))
            return PenaltyReason.DiscardedOkey;
        if (
            indicator != null
            && thrownTile.type == TileType.Number
            && thrownTile.color == indicator.color
            && thrownTile.number == indicator.number
        )
            return pairsOpenedOnTable ? PenaltyReason.DiscardedIndicator : (PenaltyReason?)null;
        if (
            islekTiles != null
            && thrownTile.type != TileType.FakeJoker
            && islekTiles.Any(t =>
                t.color == thrownTile.color && t.number == thrownTile.number && t.type != TileType.Joker
            )
        )
            return PenaltyReason.DiscardedIslek;
        return null;
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
            // Çiftten bitiş master'da playersWhoOpenedPairs'ten hesaplanır; okey ve kafa sadece bitirenin client'ında bilinir.
            photonView.RPC("FinishGameRPC", RpcTarget.All, data.actorNumber, data.okeyFinish, data.kafa);
            return;
        }

        // 2. Taş Bitti (BERABERE)
        if (PhotonNetwork.IsMasterClient)
        {
            if (tileDistrubite.allTiles.Count == 0)
            {
                Debug.Log("OYUN BİTTİ! Ortada taş kalmadı. (Berabere)");
                photonView.RPC("FinishGameRPC", RpcTarget.AllViaServer, -1, false, false);
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

    int finishedRound = -1, finishedWinner = -1; // master: bu elin sonucu (FinishGate)
    int restartRound = -1; // yeniden başlatma dizisi bu el için başladı

    [PunRPC]
    public void FinishGameRPC(int winnerActorNumber, bool isOkeyShot, bool isKafa)
    {
        if (!isGameReady)
            return;
        bool sfxFirst = !isGameEnded; // ses: ikinci RPC tekrar çalmasın
        isGameEnded = true;
        if (sfxFirst) Sfx.Play(Sfx.FinishFor(winnerActorNumber, PhotonNetwork.LocalPlayer.ActorNumber));

        DesyncDetector.Report("Finish");

        // Son taşla biten kazanır: önce gelen beraberlik, sonra gelen kazancı engellemez; kazanç sonrası beraberlik yok sayılır
        if (PhotonNetwork.IsMasterClient
            && FinishGate.Accept((int)PhotonNetwork.CurrentRoom.CustomProperties["CurrentRound"], winnerActorNumber, ref finishedRound, ref finishedWinner))
        {
            CalculateAndDistributeScores(winnerActorNumber, isOkeyShot, isKafa);
        }
    }

    private void CalculateAndDistributeScores(int winnerActorNumber, bool isOkeyShot, bool isKafa)
    {
        List<int> actors = new List<int>();
        List<int> listRewards = new List<int>();
        List<int> listPenalties = new List<int>();
        List<int> listNetScores = new List<int>();

        // Özel bitiş çarpanı: çiftten, okey atarak ya da kafa bitiş.
        int finishMultiplier = 1;
        Player winner = winnerActorNumber != -1 ? PhotonNetwork.CurrentRoom.GetPlayer(winnerActorNumber) : null;
        if (winner != null)
        {
            int winnerQue = tileDistrubite.GetQueueNumberOfPlayer(winner);
            finishMultiplier = PenaltySystem.FinishMultiplier(
                scoreManager.playersWhoOpenedPairs.Contains(winnerQue),
                isOkeyShot,
                isKafa
            );
        }

        WriteHandBreakdown(winner, finishMultiplier, isOkeyShot, isKafa);

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
                isWinner,
                scoreManager.playersWhoOpenedPairs.Contains(pQue),
                finishMultiplier
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
            RpcTarget.AllViaServer,
            actors.ToArray(),
            listRewards.ToArray(),
            listPenalties.ToArray(),
            listNetScores.ToArray()
        );
    }

    // Gösterim: el dökümünü (koltuk 1-4, botlar dahil) oda özelliğine yazar. Hata olursa loglar, akışı durdurmaz.
    private void WriteHandBreakdown(Player winner, int finishMultiplier, bool isOkeyShot, bool isKafa)
    {
        try
        {
            if (MatchScores.Instance == null)
                return;
            int[] seatOfQue = tileDistrubite.seatOfQue;
            int colorMultiplier = GetCurrentColorMultiplier();
            int winnerSeat = 0;
            bool pairsFinish = false;
            if (winner != null)
            {
                int winnerQue = tileDistrubite.GetQueueNumberOfPlayer(winner);
                winnerSeat = Mathf.Max(0, SeatMath.MeldSeat(seatOfQue, winnerQue));
                pairsFinish = scoreManager.playersWhoOpenedPairs.Contains(winnerQue);
            }
            var seats = new List<SeatInput>();
            for (int seat = 1; seat <= 4; seat++)
            {
                int que = SeatMath.QueOfSeat(seatOfQue, seat);
                if (que < 1)
                    continue;
                List<Tiles> hand = tileDistrubite.GetPlayerTilesForBot(que);
                int okeys = hand.Count(t => t.type == TileType.Joker);
                seats.Add(new SeatInput
                {
                    seat = seat,
                    opened = scoreManager.HasPlayerOpened(que),
                    openedPairs = scoreManager.playersWhoOpenedPairs.Contains(que),
                    normalTiles = hand.Count - okeys,
                    okeys = okeys,
                    inHandPenalty = scoreManager.playerScores.TryGetValue(que, out int pen) ? pen : 0,
                });
            }
            int round = (int)PhotonNetwork.CurrentRoom.CustomProperties["CurrentRound"];
            var data = HandBreakdown.Build(round, winnerSeat, finishMultiplier, isOkeyShot, pairsFinish, isKafa, seats,
                (s, win) => EndPenaltyOf(s, win, colorMultiplier, finishMultiplier));
            MatchScores.Instance.WriteHand(data);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SKOR] el dökümü yazılamadı: {e}");
        }
    }

    // Mevcut döngüyle aynı kural: PenaltySystem.EndPenalty, aynı taş sayımı (okey = Joker).
    private int EndPenaltyOf(SeatInput s, bool win, int colorMultiplier, int finishMultiplier) =>
        PenaltySystem.EndPenalty(win, s.opened, s.openedPairs, s.normalTiles, s.okeys, colorMultiplier, finishMultiplier);

    // El sonunda çift açanın taşları 2 kat sayılır; master bunu bilsin diye herkese duyurulur.
    [PunRPC]
    public void SyncOpenedPairsRPC(int playerQue)
    {
        scoreManager.playersWhoOpenedPairs.Add(playerQue);
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

    // Açış puanı yalnızca isimlikte gösterilir; baraj, puan ve senkron kontrolüne dokunmaz.
    public static event System.Action<int, int, bool> OpenValueShown;
    public readonly Dictionary<int, (int value, bool pairs)> OpenValues = new Dictionary<int, (int, bool)>();

    [PunRPC]
    public void OpenValueRPC(int que, int value, bool isPairs)
    {
        OpenValues[que] = (value, isPairs);
        OpenValueShown?.Invoke(que, value, isPairs);
    }

    // Tur geri alınınca o sıranın açış değeri tur başındakine döner; etiketler yenilenir.
    public void RestoreOpenValue(int que, bool has, int value, bool pairs)
    {
        if (has)
            OpenValues[que] = (value, pairs);
        else
            OpenValues.Remove(que);
        OpenValueShown?.Invoke(0, 0, false);
    }

    // --- TUR GERİ ALMA (tur ortasında kopan oyuncu) ---
    // Master: bu seri için geri alma gönderildi, kendi işleyicisi henüz çalışmadı; TurnManager botu bekletir.
    public int pendingRollbackSerial = -1;
    int rollbackSentSerial = -1; // master: aynı seri için bir kez gönderir (iki geri çağrı çağırsa da)
    int rollbackDoneSerial = -1; // her istemci: aynı seri için bir kez uygular

    // Yalnız master: koltuk insanlıktan çıktı. Sırası o koltuktaysa yarım tur herkeste geri alınır.
    public void HandleSeatLoss(Player p) => HandleSeatLoss(SeatManager.Instance.GetSeatNumberOfPlayer(p));

    public void HandleSeatLoss(int seat)
    {
        if (!PhotonNetwork.IsMasterClient || SeatManager.Instance == null)
            return;
        SeatManager.Instance.SetSeatControl(seat, false);
        var tm = turnManager;
        var td = tileDistrubite;
        if (!isGameReady || isGameEnded || tm == null || td == null)
            return;
        int que = SeatMath.QueOfSeat(td.seatOfQue, seat);
        if (que != tm.currentTurnPlayer || rollbackSentSerial == tm.TurnSerial)
            return;
        rollbackSentSerial = pendingRollbackSerial = tm.TurnSerial;
        pendingSince = Time.time;
        rollbackResent = false;
        photonView.RPC(nameof(RollbackTurn), RpcTarget.AllViaServer, que, tm.TurnSerial);
    }

    float pendingSince;
    bool rollbackResent;

    // Bot bekletmesi ~3 sn'yi aşarsa (ör. masa sahibi gönderimle yankı arasında değişti) bir kez yeniden gönderilir; seri korumaları kopyayı zararsız yapar.
    public void RollbackWatchdog()
    {
        if (rollbackResent || pendingRollbackSerial < 0 || Time.time - pendingSince < 3f)
            return;
        rollbackResent = true;
        Debug.LogWarning($"[GERIAL] seri {pendingRollbackSerial} 3 sn'de uygulanmadı, yeniden gönderiliyor");
        photonView.RPC(nameof(RollbackTurn), RpcTarget.AllViaServer, turnManager.currentTurnPlayer, pendingRollbackSerial);
    }

    [PunRPC]
    void RollbackTurn(int que, int serial)
    {
        var tm = turnManager;
        var td = tileDistrubite;
        bool master = PhotonNetwork.IsMasterClient;
        var r = td.TurnStart;
        if (
            serial != tm.TurnSerial
            || que != tm.currentTurnPlayer
            || r == null
            || r.que != que
            || r.serial != serial
            || serial == rollbackDoneSerial
        )
        {
            // Kayıt bu seriye ait değil: yerelde bir şey geri alınmaz; master'ın bekletmesi yine de kalkar, oyun devam eder.
            if (r != null && serial == tm.TurnSerial && (r.que != que || r.serial != serial))
                Debug.LogWarning($"[GERIAL] kayıt uyuşmuyor (kayıt sıra {r.que} seri {r.serial}, mesaj sıra {que} seri {serial}), geri alınmadı");
            if (master && serial == pendingRollbackSerial)
                pendingRollbackSerial = -1;
            return;
        }
        rollbackDoneSerial = serial;
        var kind = TurnRollback.Decide(r.discards, td.DiscardCountOfSeat(SeatMath.MeldSeat(td.seatOfQue, que)));
        if (kind == RollbackKind.CloseTurn)
        {
            int swapped = td.RemoveSwapAddedFromHand(r);
            if (swapped > 0)
                Debug.Log($"[GERIAL] sıra {que} takasla gelen {swapped} taş elden düşüldü (okey tahtada kaldı)");
            td.RenderSeatMeldsFromData(que);
            // Bot, NextTurn serisi ilerleyene kadar bekler (pendingRollbackSerial burada temizlenmez).
            if (master)
            {
                // Atış sonrası destede taş kalmadıysa kopan oyuncunun kendi bitiş mesajı gitmemiştir: tur yerine el biter.
                if (BotEndRule.PileEmpty(td.allTiles.Count, false))
                    photonView.RPC(nameof(FinishGameRPC), RpcTarget.AllViaServer, -1, false, false);
                else
                    tm.SendNextTurn(true);
            }
        }
        else
        {
            td.ApplyRollback(r);
            if (master)
            {
                if (CurrentTableLimit != r.tableLimit)
                    SetTableLimit(r.tableLimit);
                if (IsDoubleOpenedOnTable != r.doubleOpened)
                    PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { "IsDoubleOpened", r.doubleOpened } });
                pendingRollbackSerial = -1;
            }
        }
        Debug.Log($"[GERIAL] sıra {que} seri {serial} {kind}");
    }

    // Test oyuncusu (SyncAutoTest) el sonucunu kendi beklentisiyle karşılaştırmak için dinler; abonesi yoksa etkisiz.
    public static event System.Action<int[], int[], int[], int[]> HandResult;

    [PunRPC]
    public void OnGameEndedDetailedRPC(
        int[] actors,
        int[] rewards,
        int[] penalties,
        int[] netScores
    )
    {
        isGameEnded = true;
        HandResult?.Invoke(actors, rewards, penalties, netScores);
        // El başına bir kez: kazanç sonradan beraberliği ezerse ikinci sonuç gelir ama geri sayım yeniden başlamaz
        int round = (int)PhotonNetwork.CurrentRoom.CustomProperties["CurrentRound"];
        if (restartRound == round)
            return;
        restartRound = round;
        StartCoroutine(RestartSequence());
    }

    public void CheckRoundEnd()
    {
        int currentRound = (int)PhotonNetwork.CurrentRoom.CustomProperties["CurrentRound"];
        int totalRounds = (int)PhotonNetwork.CurrentRoom.CustomProperties["TotalRounds"];

        if (!MatchState.IsOver(currentRound, totalRounds, true))
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

        if (!MatchState.IsOver(currentRound, totalRounds, true))
        {
            yield return new WaitForSeconds(10f);
            // El zaten ilerlediyse (eski master gitti, yeni master'ın sayacı gecikti) ikinci kez kurma
            int nowRound = (int)PhotonNetwork.CurrentRoom.CustomProperties["CurrentRound"];
            if (PhotonNetwork.IsMasterClient && nowRound == currentRound)
            {
                NextRoundSetup(currentRound + 1);
            }
        }
        else
        {
            Debug.Log("Tüm eller tamamlandı. Lobiye dönülüyor...");
            yield return new WaitForSeconds(10f);
            RejoinRecord.Clear();
            PhotonNetwork.LeaveRoom();
        }
    }

    private void NextRoundSetup(int nextRoundValue)
    {
        Hashtable cp = new Hashtable();
        cp.Add("CurrentRound", nextRoundValue);
        PhotonNetwork.CurrentRoom.SetCustomProperties(cp);
        photonView.RPC("RPC_PrepareNextRound", RpcTarget.AllViaServer);
        SeatManager.Instance?.PublishSeatNames(); // yeni elin başında koltuk adları (gösterim)
    }

    [PunRPC]
    public void RPC_PrepareNextRound()
    {
        isGameEnded = false;
        isGameReady = false;
        pendingRollbackSerial = rollbackSentSerial = rollbackDoneSerial = -1; // seri yeni elde 0'dan başlar
        MatchScores.Instance?.StartNewHand(); // geç gelen ceza kaydı yeni ele sızmasın
        OpenValues.Clear();
        OpenValueShown?.Invoke(0, 0, false); // yeni el: etiketleri boşalt

        if (ScoreboardView.Instance != null)
            ScoreboardView.Instance.Hide(); // skor tablosu sıradaki elde kapanır

        // Sıfırlama yalnızca masa sahibinden gider: her istemci yollarsa geç gelen kopyalar
        // StartRoundVisuals'ın açtığı canDrop'u siler ve 1. sıradaki masa sahibi takılır.
        if (PhotonNetwork.IsMasterClient)
            turnManager.photonView.RPC("RPC_ResetTurnForNewRound", RpcTarget.AllViaServer);

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
            tileDistrubite.photonView.RPC("ResetTableAndRedistribute", RpcTarget.AllViaServer);
    }
}
