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

        // KURAL: Roket (Sahte Okey açıldıysa) -> 8 Katı
        if (indicator.type == TileType.FakeJoker)
            return 8;

        // KURAL: Renk Çarpanları
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
        // Eğer açılan puan mevcut limitten büyükse güncelle
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

        // Master Client kendi UI'ını ve limit bilgisini hemen tazelesin
        Debug.Log($"Limit Ayarlandı: {limit}");
    }

    public override void OnRoomPropertiesUpdate(
        ExitGames.Client.Photon.Hashtable propertiesThatChanged
    )
    {
        if (propertiesThatChanged.ContainsKey("TableLimit"))
        {
            int newLimit = (int)propertiesThatChanged["TableLimit"];

            // GÜVENLİK KONTROLÜ: Hem UIManager hem de scoreManager null olmamalı
            if (UIManager.Instance != null && scoreManager != null)
            {
                // Puanları ScoreManager'dan, yeni limiti gelen veriden alıp UI'ı tazele
                UIManager.Instance.UpdatePlayerStats(
                    scoreManager.totalScore,
                    scoreManager.pairTotalScore,
                    newLimit
                );
            }
            else
            {
                // Eğer scoreManager null ise referansı tekrar bulmayı dene
                if (scoreManager == null)
                {
                    _scoreManager = FindObjectOfType<ScoreManager>();
                }

                Debug.LogWarning(
                    "OnRoomPropertiesUpdate: UIManager veya ScoreManager henüz hazır değil!"
                );
            }
        }
    }

    // --- CEZA YÖNETİMİ ---
    public void RecordSidePick(Tiles tile)
    {
        if (tile != null)
            currentSidePickTile = new Tiles(tile.color, tile.number, tile.type);
    }

    public void HandleFailedSidePick(Tiles tileToThrow)
    {
        int currentPlayerQue = tileDistrubite.GetQueueNumberOfPlayer(PhotonNetwork.LocalPlayer);

        // KURAL: Yandan alıp açamamanın cezası 250
        photonView.RPC("AddGeneralPenaltyRPC", RpcTarget.MasterClient, currentPlayerQue, 250);

        Tiles tileToReturn = (currentSidePickTile != null) ? currentSidePickTile : tileToThrow;
        StartCoroutine(RollbackSidePickProcess(currentPlayerQue, tileToReturn));
    }

    [PunRPC]
    public void AddGeneralPenaltyRPC(int playerQue, int penaltyScore)
    {
        // Sadece Master Client işlesin ve dağıtsın
        if (PhotonNetwork.IsMasterClient)
        {
            scoreManager.UpdatePlayerScore(playerQue, penaltyScore);
            Debug.Log($"[GENEL CEZA] Oyuncu {playerQue} için {penaltyScore} puan ceza işlendi.");
        }
    }

    [PunRPC]
    public void ApplySidePickSuccessPenaltyRPC(int currentPlayerQue)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        int penalty = 0;

        // KURAL: Taşın değerinin 10 katı.
        if (currentSidePickTile != null)
        {
            penalty = currentSidePickTile.number * 10;
        }
        else
        {
            // Eğer taş null geldiyse varsayılan ceza 250
            Debug.LogWarning("Yandan alınan taş verisi NULL! Varsayılan 250 ceza uygulanıyor.");
            penalty = 250;
        }

        // HEDEF: Cezayı "Açan" değil, taşı "Atan" (Bir Önceki Oyuncu) yer.
        int targetPlayerQue = (currentPlayerQue == 1) ? 4 : currentPlayerQue - 1;

        // Cezayı Uygula
        scoreManager.UpdatePlayerScore(targetPlayerQue, penalty);

        // Taşı temizle
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
        if (PhotonNetwork.IsMasterClient)
        {
            scoreManager.UpdatePlayerScore(victimQue, penaltyAmount);
            Debug.Log(
                $"[İŞLEME CEZASI] Oyuncu {victimQue} üzerine taş işlendi. Ceza: +{penaltyAmount}"
            );
        }
    }

    // Geri Al (Undo) yapıldığında çağrılır: Kesilen cezayı iade eder.
    [PunRPC]
    public void RevertProcessingPenaltyRPC(int victimQue, int penaltyAmount)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            // Cezayı geri almak için negatif puan gönderiyoruz (Puanı düşürür)
            scoreManager.UpdatePlayerScore(victimQue, -penaltyAmount);
            Debug.Log(
                $"[İŞLEME CEZASI İPTAL] Oyuncu {victimQue} ceza iadesi yapıldı: -{penaltyAmount}"
            );
        }
    }

    // --- CEZA KONTROLÜ (ATILAN TAŞ İÇİN) ---
    [PunRPC]
    public void CheckPenaltyRPC(int playerQue, Tiles thrownTile)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        Tiles indicator = tileDistrubite.GetIndicatorTile();
        int penaltyAmount = 0;
        string reason = "";

        // Okey Hesabı
        int okeyNumber = -1;
        TileColor okeyColor = TileColor.black;
        if (indicator != null)
        {
            okeyColor = indicator.color;
            okeyNumber = indicator.number + 1;
            if (okeyNumber > 13)
                okeyNumber = 1;
        }

        // --- 1. OKEY ATMA CEZASI (250 PUAN) ---
        if (
            thrownTile.type == TileType.Joker
            || (
                thrownTile.color == okeyColor
                && thrownTile.number == okeyNumber
                && thrownTile.type != TileType.FakeJoker
            )
        )
        {
            penaltyAmount = 250;
            reason = "Okey Atıldı";
        }
        // --- 2. GÖSTERGE ATMA CEZASI (250 PUAN) ---
        else if (
            indicator != null
            && thrownTile.color == indicator.color
            && thrownTile.number == indicator.number
        )
        {
            penaltyAmount = 250;
            reason = "Gösterge Atıldı";
        }
        // --- 3. İŞLEK TAŞ ATMA CEZASI (250 PUAN) ---
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
                        reason = "İşlek Atıldı";
                        break;
                    }
                }
            }
        }

        // --- CEZAYI UYGULA ---
        if (penaltyAmount > 0)
        {
            scoreManager.UpdatePlayerScore(playerQue, penaltyAmount);
            Debug.Log(
                $"<color=red>CEZA KESİLDİ!</color> Sebep: {reason} -> Oyuncu {playerQue} -{penaltyAmount} Puan"
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

    // --- [YENİ] MASADA ÇİFT AÇILDI MI KONTROLÜ ---
    // Bu özellik, masada herhangi bir oyuncunun çift açıp açmadığını kontrol eder.
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

    // Biri çift açtığında bu fonksiyonu çağıracağız (Master Client olmasa bile herkes çağırabilir)
    public void SetDoubleOpened()
    {
        if (!IsDoubleOpenedOnTable)
        {
            Hashtable props = new Hashtable { { "IsDoubleOpened", true } };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            Debug.Log(
                "<color=green>MASADA ÇİFT AÇILDI! Artık herkes limite takılmadan çifte gidebilir.</color>"
            );
        }
    }

    // --- HERKESTE ÇALIŞAN FİNAL OPERASYONU ---
    [PunRPC]
    public void FinishGameRPC(int winnerActorNumber, bool isOkeyShot, bool isDoubleFinish)
    {
        if (!isGameReady)
            return;
        isGameEnded = true;

        Debug.Log("FinishGameRPC Tetiklendi. Puanlar hesaplanacak.");

        // Puan hesaplamayı SADECE Master Client yapar ve sonucu herkese dağıtır.
        if (PhotonNetwork.IsMasterClient)
        {
            CalculateAndDistributeScores(winnerActorNumber, isOkeyShot, isDoubleFinish);
        }
    }

    // --- DETAYLI PUAN HESAPLAMA (ÜÇLÜ TABLO) ---
    private void CalculateAndDistributeScores(
        int winnerActorNumber,
        bool isOkeyShot,
        bool isDoubleFinish
    )
    {
        List<int> actors = new List<int>();

        // GÖRSEL İÇİN AYRILMIŞ VERİLER
        List<int> listRewards = new List<int>(); // Üst Kısım: Düşerler (Eksi Puanlar)
        List<int> listPenalties = new List<int>(); // Orta Kısım: Cezalar (Artı Puanlar)
        List<int> listNetScores = new List<int>(); // Alt Kısım: Toplam (Net Skor)

        // 1. ÇARPANLARI HESAPLA
        int colorMultiplier = GetCurrentColorMultiplier();
        int finishTypeMultiplier = 1;

        if (isDoubleFinish)
            finishTypeMultiplier *= 2;
        if (isOkeyShot)
            finishTypeMultiplier *= 2;

        int totalMultiplier = colorMultiplier * finishTypeMultiplier;

        Debug.Log(
            $"[DETAYLI HESAP] Çarpanlar -> Renk: {colorMultiplier}, Bitiş: {finishTypeMultiplier}, Toplam: {totalMultiplier}"
        );

        foreach (var player in PhotonNetwork.PlayerList)
        {
            actors.Add(player.ActorNumber);
            int pQue = tileDistrubite.GetQueueNumberOfPlayer(player);

            int myReward = 0; // Örn: -600
            int myPenalty = 0; // Örn: +101, +250

            // A) OYUN İÇİ MEVCUT CEZALAR
            if (scoreManager.playerScores.ContainsKey(pQue))
            {
                myPenalty += scoreManager.playerScores[pQue];
            }

            // B) OYUN SONU DURUMLARI

            // --- KAZANAN ---
            if (winnerActorNumber != -1 && player.ActorNumber == winnerActorNumber)
            {
                // PDF Kuralı: Baz Puan(100) x Toplam Çarpan (Düşülür)
                myReward -= (100 * totalMultiplier);
                Debug.Log($"Oyuncu {pQue} (KAZANAN): Düşer: {myReward}, Ceza: {myPenalty}");
            }
            // --- KAYBEDENLER ---
            else if (winnerActorNumber != -1)
            {
                bool hasOpened = scoreManager.HasPlayerOpened(pQue);

                if (!hasOpened)
                {
                    // DURUM 1: HİÇ AÇMAMIŞ
                    // PDF Kuralı: "Açmayan kişi 600 ceza" -> Baz(100) x Toplam Çarpan
                    int notOpenedPenalty = 100 * totalMultiplier;
                    myPenalty += notOpenedPenalty;
                }
                else
                {
                    // DURUM 2: AÇMIŞ AMA BİTEMEMİŞ
                    // PDF Kuralı: "Açıp bitmeyen oyuncu elindeki taş başına ceza yer"
                    int handPenalty = scoreManager.GetHandPenaltyForOpenedPlayer(
                        pQue,
                        totalMultiplier
                    );
                    myPenalty += handPenalty;
                }
            }

            // --- C) LİSTELERE EKLE ---
            listRewards.Add(myReward);
            listPenalties.Add(myPenalty);
            listNetScores.Add(myReward + myPenalty);
        }

        // 3. SONUÇLARI GÖNDER (3 ayrı dizi)
        photonView.RPC(
            "OnGameEndedDetailedRPC",
            RpcTarget.All,
            actors.ToArray(),
            listRewards.ToArray(),
            listPenalties.ToArray(),
            listNetScores.ToArray()
        );
    }

    // --- [YENİ] AÇAN OYUNCUYU SENKRONİZE ETME ---
    [PunRPC]
    public void SyncOpenedPlayerRPC(int playerQue)
    {
        // Gelen oyuncuyu "Açanlar" listesine ekle
        if (!scoreManager.playersWhoOpened.Contains(playerQue))
        {
            scoreManager.playersWhoOpened.Add(playerQue);
            Debug.Log($"[SYNC] Oyuncu {playerQue} açtı olarak sisteme işlendi.");
        }
    }

    // --- DETAYLI SONUÇ ALMA VE UI AÇMA ---
    [PunRPC]
    public void OnGameEndedDetailedRPC(
        int[] actors,
        int[] rewards,
        int[] penalties,
        int[] netScores
    )
    {
        isGameEnded = true;

        // UI'a göndermek için veriyi paketle
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
            // YENİ FONKSİYONU ÇAĞIRIYORUZ
            UIManager.Instance.ShowDetailedGameOver(dictRewards, dictPenalties, dictNetScores);
        }
        else
        {
            Debug.LogError("UIManager bulunamadı!");
        }

        StartCoroutine(RestartSequence());
    }

    public void CheckRoundEnd()
    {
        // Mevcut odadaki el bilgilerini alıyoruz
        int currentRound = (int)PhotonNetwork.CurrentRoom.CustomProperties["CurrentRound"];
        int totalRounds = (int)PhotonNetwork.CurrentRoom.CustomProperties["TotalRounds"];

        // Skorları mevcut elin sonuçlarına göre güncelle (ScoreManager üzerinden)
        // ScoreManager.UpdateTotalScores(); // Bu metodun toplam skorları biriktirdiğinden emin ol

        if (currentRound < totalRounds)
        {
            // Daha oynanacak el var
            Debug.Log($"El bitti! {currentRound}. el tamamlandı. Sonraki ele geçiliyor...");

            // MasterClient bir sonraki eli hazırlar
            if (PhotonNetwork.IsMasterClient)
            {
                NextRoundSetup(currentRound + 1);
            }
        }
        else
        {
            // Oyun tamamen bitti
            Debug.Log("Tüm eller tamamlandı! Genel sonuçlar hesaplanıyor...");
            //ShowFinalResults();
        }
    }

    private IEnumerator RestartSequence()
    {
        // Mevcut oda özelliklerinden tur bilgilerini al
        int currentRound = (int)PhotonNetwork.CurrentRoom.CustomProperties["CurrentRound"];
        int totalRounds = (int)PhotonNetwork.CurrentRoom.CustomProperties["TotalRounds"];

        Debug.Log($"El bitti. Mevcut: {currentRound}, Toplam: {totalRounds}");

        if (currentRound < totalRounds)
        {
            // Daha oynanacak el var
            yield return new WaitForSeconds(10f); // Oyuncuların skor tablosuna bakması için süre

            if (PhotonNetwork.IsMasterClient)
            {
                // Bir sonraki eli hazırla
                NextRoundSetup(currentRound + 1);
            }
        }
        else
        {
            // Tüm eller bitti, artık lobiye dönme vakti
            Debug.Log("Tüm eller tamamlandı. 10 saniye içinde lobiye dönülüyor...");
            yield return new WaitForSeconds(10f);
            PhotonNetwork.LeaveRoom();
        }
    }

    private void NextRoundSetup(int nextRoundValue)
    {
        // 1. Oda özelliklerini güncelle
        Hashtable cp = new Hashtable();
        cp.Add("CurrentRound", nextRoundValue);
        PhotonNetwork.CurrentRoom.SetCustomProperties(cp);

        // 2. Masayı temizlemek ve yeni eli başlatmak için RPC gönder
        // Bu RPC; taşları siler, eli dağıtır ve statları sıfırlar
        photonView.RPC("RPC_PrepareNextRound", RpcTarget.All);
    }

    // GameManager.cs içindeki ilgili kısım
    // GameManager.cs içindeki RPC_PrepareNextRound metodunu güncelle
    [PunRPC]
    public void RPC_PrepareNextRound()
    {
        isGameEnded = false;
        isGameReady = false;

        if (UIManager.Instance != null)
            UIManager.Instance.gameOverPanel.SetActive(false);

        // --- TUR VE SIRA SIFIRLAMA ---
        // TurnManager üzerindeki sırayı ve izinleri tüm clientlarda sıfırla
        turnManager.photonView.RPC("RPC_ResetTurnForNewRound", RpcTarget.All);

        // --- LİMİT SIFIRLAMA ---
        if (PhotonNetwork.IsMasterClient)
        {
            SetTableLimit(51);
        }

        scoreManager.ResetPlayerOpenStatus();

        // Taşları dağıtmayı en sona bırak ki sıralar hazır olsun
        if (PhotonNetwork.IsMasterClient)
        {
            tileDistrubite.photonView.RPC("ResetTableAndRedistribute", RpcTarget.All);
        }
    }
}
