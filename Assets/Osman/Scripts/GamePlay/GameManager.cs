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

    // --- LİMİT GÜNCELLEME (KATLAMALI SİSTEM) ---
    public void TryUpdateTableLimit(int openedScore)
    {
        // Eğer açılan puan mevcut limitten büyükse güncelle
        if (openedScore > CurrentTableLimit)
        {
            SetTableLimit(openedScore);
            Debug.Log($"<color=green>MASA LİMİTİ YÜKSELDİ: Yeni Limit {openedScore}</color>");
        }
    }

    private void SetTableLimit(int limit)
    {
        Hashtable props = new Hashtable { { "TableLimit", limit } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
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
        scoreManager.UpdatePlayerScore(currentPlayerQue, 100);

        Tiles tileToReturn = (currentSidePickTile != null) ? currentSidePickTile : tileToThrow;
        StartCoroutine(RollbackSidePickProcess(currentPlayerQue, tileToReturn));
    }

    public void ApplySidePickSuccessPenalty()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;
        if (currentSidePickTile == null)
            return;

        int currentPlayerQue = tileDistrubite.GetQueueNumberOfPlayer(PhotonNetwork.LocalPlayer);
        int previousPlayerQue = turnManager.GetPreviousPlayerQue(currentPlayerQue);
        int penalty = currentSidePickTile.number * 10;

        scoreManager.UpdatePlayerScore(previousPlayerQue, penalty);
        currentSidePickTile = null;
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

    public void CalculateThrowPenalty(Tiles thrownTile)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        Tiles indicator = tileDistrubite.GetIndicatorTile();
        int penaltyAmount = 0;
        string reason = "";

        // --- DETAYLI LOG (Hata varsa sebebini görmek için) ---
        Debug.Log(
            $"[CEZA KONTROLÜ] Atılan: {thrownTile.color} {thrownTile.number} ({thrownTile.type})"
        );

        // 1. OKEYİ HESAPLA (Matematiksel)
        int okeyNumber = -1;
        TileColor okeyColor = TileColor.black;

        if (indicator != null)
        {
            okeyColor = indicator.color;
            okeyNumber = indicator.number + 1;
            if (okeyNumber > 13)
                okeyNumber = 1;

            Debug.Log($"[CEZA KONTROLÜ] Bu elin Okeyi: {okeyColor} {okeyNumber} olmalı.");
        }

        // 2. KONTROLLERİ YAP

        // --- A) OKEY ATMA CEZASI ---
        // Kural: Atılan taşın rengi ve numarası Okey ile aynıysa...
        // VE bu taş "Sahte Okey" (Resimli taş) değilse...
        // O zaman bu taş %100 Gerçek Okeydir (Jokerdir).

        if (thrownTile.color == okeyColor && thrownTile.number == okeyNumber)
        {
            if (thrownTile.type != TileType.FakeJoker)
            {
                // FakeJoker değilse ve numarası tutuyorsa, bu Jokerdir.
                penaltyAmount = 101;
                reason = "Okey Atıldı";
                Debug.Log("<color=red>OKEY TESPİT EDİLDİ!</color>");
            }
            else
            {
                Debug.Log(
                    "Atılan taş Okey sayılarına sahip ama 'Sahte Okey' olduğu için ceza yok."
                );
            }
        }
        // Eğer tipi direkt Joker olarak geliyorsa (ekstra güvenlik)
        else if (thrownTile.type == TileType.Joker)
        {
            penaltyAmount = 101;
            reason = "Okey (Type=Joker) Atıldı";
        }
        // --- B) GÖSTERGE ATMA CEZASI ---
        else if (
            indicator != null
            && thrownTile.color == indicator.color
            && thrownTile.number == indicator.number
        )
        {
            penaltyAmount = 101;
            reason = "Gösterge Atıldı";
        }
        // --- C) İŞLEK TAŞ ATMA CEZASI ---
        else
        {
            bool isProcessable = false;
            if (tileDistrubite.availableTiles != null && thrownTile.type != TileType.FakeJoker)
            {
                foreach (var t in tileDistrubite.availableTiles)
                {
                    // Tipine bakmaksızın renk ve numara tutuyor mu?
                    if (t.color == thrownTile.color && t.number == thrownTile.number)
                    {
                        // Ama o yer Joker için ayrılmışsa (Type=Joker) ve biz normal sayı atıyorsak yine de işlektir
                        // Sadece Jokerin kendisini (available listesindeki Joker tipi) hariç tutmaya gerek yok
                        // Çünkü available listesindeki her şey "Buraya taş konabilir" demektir.

                        isProcessable = true;
                        Debug.Log($"İşlek Bulundu: {t.color} {t.number} masada boş.");
                        break;
                    }
                }
            }
            if (isProcessable)
            {
                penaltyAmount = 101;
                reason = "İşlek Atıldı";
            }
        }

        // CEZAYI UYGULA
        if (penaltyAmount > 0)
        {
            int targetPlayer = turnManager.currentTurnPlayer;
            scoreManager.UpdatePlayerScore(targetPlayer, penaltyAmount);
            Debug.Log(
                $"<color=red>CEZA KESİLDİ!</color> Sebep: {reason} -> Oyuncu {targetPlayer} -{penaltyAmount} Puan"
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

            // BURADA FinishGameRPC çağrılıyor. Parametre olarak Kazananın ID'si gidiyor.
            photonView.RPC("FinishGameRPC", RpcTarget.All, data.actorNumber, false, false);
            return;
        }

        // 2. Taş Bitti (BERABERE)
        if (PhotonNetwork.IsMasterClient)
        {
            if (tileDistrubite.allTiles.Count == 0)
            {
                Debug.Log("OYUN BİTTİ! Ortada taş kalmadı. (Berabere)");

                // BURADA FinishGameRPC çağrılıyor. Parametre olarak -1 gidiyor.
                photonView.RPC("FinishGameRPC", RpcTarget.All, -1, false, false);
            }
        }
    }

    // GameManager.cs içine (Eski CalculateThrowPenalty yerine bunu kullanıyoruz):

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

        // --- 1. OKEY ATMA (Kesin Kontrol) ---
        // Tip Joker ise VEYA Renk/Numara tutuyorsa (ve sahte değilse)
        if (
            thrownTile.type == TileType.Joker
            || (
                thrownTile.color == okeyColor
                && thrownTile.number == okeyNumber
                && thrownTile.type != TileType.FakeJoker
            )
        )
        {
            penaltyAmount = 101;
            reason = "Okey Atıldı";
        }
        // --- 2. GÖSTERGE ATMA ---
        else if (
            indicator != null
            && thrownTile.color == indicator.color
            && thrownTile.number == indicator.number
        )
        {
            penaltyAmount = 101;
            reason = "Gösterge Atıldı";
        }
        // --- 3. İŞLEK TAŞ ATMA ---
        else
        {
            // Atılan taş, masada işlenebilir (available) listesinde var mı?
            if (tileDistrubite.availableTiles != null && thrownTile.type != TileType.FakeJoker)
            {
                foreach (var t in tileDistrubite.availableTiles)
                {
                    // Rengi ve numarası tutuyor mu? (Joker tipi hariç, normal sayı olarak)
                    if (
                        t.color == thrownTile.color
                        && t.number == thrownTile.number
                        && t.type != TileType.Joker
                    )
                    {
                        penaltyAmount = 101;
                        reason = "İşlek Atıldı";
                        break;
                    }
                }
            }
        }

        // --- CEZAYI UYGULA ---
        if (penaltyAmount > 0)
        {
            // KRİTİK NOKTA: Cezayı 'playerQue' (Taşı atan kişi) yer.
            // Asla TurnManager.currentTurnPlayer kullanmıyoruz.
            scoreManager.UpdatePlayerScore(playerQue, penaltyAmount);

            Debug.Log(
                $"<color=red>CEZA KESİLDİ!</color> Sebep: {reason} -> Oyuncu {playerQue} -{penaltyAmount} Puan"
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
            // !!! İŞTE SORDUĞUN FONKSİYON BURADA ÇAĞIRILIYOR !!!
            CalculateAndDistributeScores(winnerActorNumber, isOkeyShot, isDoubleFinish);
        }
    }

    // --- PUAN HESAPLAMA (MASTER ONLY) ---
    private void CalculateAndDistributeScores(
        int winnerActorNumber,
        bool isOkeyShot,
        bool isDoubleFinish
    )
    {
        List<int> actors = new List<int>();
        List<int> finalScores = new List<int>();

        // 1. Çarpanları Belirle
        int finishMultiplier = 1;
        if (isDoubleFinish)
            finishMultiplier *= 2;
        if (isOkeyShot)
            finishMultiplier *= 2;

        Debug.Log(
            $"Puan Hesaplanıyor... Kazanan Actor: {winnerActorNumber}, Çarpan: {finishMultiplier}"
        );

        // 2. Odadaki tüm oyuncuları gez
        foreach (var player in PhotonNetwork.PlayerList)
        {
            actors.Add(player.ActorNumber);

            // A) Oyuncunun mevcut ceza puanını ScoreManager'dan al
            int currentPenalty = 0;
            int pQue = tileDistrubite.GetQueueNumberOfPlayer(player);

            if (scoreManager.playerScores.ContainsKey(pQue))
            {
                currentPenalty = scoreManager.playerScores[pQue];
            }

            // B) Eğer bu oyuncu KAZANAN ise puan düş (Ödül)
            // Eğer winnerActorNumber -1 ise (Berabere), kimse ödül almaz.
            if (winnerActorNumber != -1 && player.ActorNumber == winnerActorNumber)
            {
                currentPenalty -= (101 * finishMultiplier);
            }

            finalScores.Add(currentPenalty);
        }

        // 3. Sonuçları herkese gönder (UI açılsın)
        photonView.RPC("OnGameEndedRPC", RpcTarget.All, actors.ToArray(), finalScores.ToArray());
    }

    // --- SONUÇLARI AL VE UI AÇ ---
    [PunRPC]
    public void OnGameEndedRPC(int[] actors, int[] scores)
    {
        isGameEnded = true;

        Dictionary<int, int> scoreboardData = new Dictionary<int, int>();
        for (int i = 0; i < actors.Length; i++)
        {
            Player p = PhotonNetwork.CurrentRoom.GetPlayer(actors[i]);
            if (p != null && p.CustomProperties.TryGetValue("PlayerQue", out object q))
            {
                int pQue = (int)q;
                if (!scoreboardData.ContainsKey(pQue))
                    scoreboardData.Add(pQue, scores[i]);
                else
                    scoreboardData[pQue] = scores[i];
            }
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowGameOver(scoreboardData);
        }
        else
        {
            Debug.LogError("UIManager bulunamadı, tablo açılamıyor!");
        }

        StartCoroutine(RestartSequence());
    }

    private IEnumerator RestartSequence()
    {
        Debug.Log("10 saniye sonra lobiye dönülüyor...");
        yield return new WaitForSeconds(10f);
        PhotonNetwork.LeaveRoom();
    }
}
