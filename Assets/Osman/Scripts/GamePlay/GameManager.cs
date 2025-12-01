using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class GameManager : MonoBehaviourPunCallbacks
{
    public static GameManager Instance;

    // --- LAZY LOADING İLE REFERANS BULMA ---
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

    [Header("Oyun Ayarları")]
    public bool isGameEnded = false;
    public Tiles currentSidePickTile; // Yandan alınan taşın hafızası (İade için)

    [Header("Masa Durumu (Katlamalı Oyun)")]
    public int currentTableLimit = 51; // Başlangıç barajı

    // Renk Çarpanları
    private Dictionary<TileColor, int> colorMultipliers = new Dictionary<TileColor, int>()
    {
        { TileColor.blue, 3 },
        { TileColor.black, 4 },
        { TileColor.red, 5 },
        { TileColor.yellow, 6 },
    };

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        // TileSerialization'ı başlat (Photon için)
        TileSerialization.RegisterCustomTypes();
    }

    private void Start()
    {
        // Event Dinleyicileri
        EventDispatcher.RegisterFunction<HandData>("OnPlayerMoveFinished", CheckGameStatus);
        EventDispatcher.RegisterFunction<Tiles>("OnTileThrown", CalculateThrowPenalty);
        EventDispatcher.RegisterFunction<Tiles>("OnSideTilePicked", RecordSidePick);
    }

    private void OnDestroy()
    {
        EventDispatcher.UnregisterListener<HandData>("OnPlayerMoveFinished", CheckGameStatus);
        EventDispatcher.UnregisterListener<Tiles>("OnTileThrown", CalculateThrowPenalty);
        EventDispatcher.UnregisterListener<Tiles>("OnSideTilePicked", RecordSidePick);
    }

    // --- MASA LİMİTİ GÜNCELLEME ---
    [PunRPC]
    public void UpdateTableLimit(int newScore)
    {
        if (newScore > currentTableLimit)
        {
            currentTableLimit = newScore;
            Debug.Log($"Masa barajı yükseldi! Yeni açma limiti: {currentTableLimit}");
        }
    }

    // --- YANDAN TAŞ ÇEKME MANTIĞI ---
    public void RecordSidePick(Tiles tile)
    {
        if (tile == null)
            return;
        currentSidePickTile = new Tiles(tile.color, tile.number, tile.type);
    }

    // BAŞARILI: Yandan aldı ve elini açtı -> Rakibe Ceza
    public void ApplySidePickSuccessPenalty()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (currentSidePickTile == null)
        {
            // Taş verisi kayıpsa varsayılan ceza uygula (Güvenlik)
            int currentPQue = tileDistrubite.GetQueueNumberOfPlayer(PhotonNetwork.LocalPlayer);
            int prevPQue = turnManager.GetPreviousPlayerQue(currentPQue);
            if (scoreManager != null)
                scoreManager.UpdatePlayerScore(prevPQue, 100);
            return;
        }

        int currentPlayerQue = tileDistrubite.GetQueueNumberOfPlayer(PhotonNetwork.LocalPlayer);
        int previousPlayerQue = turnManager.GetPreviousPlayerQue(currentPlayerQue);

        int penalty = currentSidePickTile.number * 10; // Kural: Sayı * 10

        scoreManager.UpdatePlayerScore(previousPlayerQue, penalty);
        currentSidePickTile = null;
    }

    // BAŞARISIZ: Yandan aldı ama açamadı -> Ceza + İade
    public void HandleFailedSidePick(Tiles tileToThrow)
    {
        int currentPlayerQue = tileDistrubite.GetQueueNumberOfPlayer(PhotonNetwork.LocalPlayer);
        scoreManager.UpdatePlayerScore(currentPlayerQue, 100);

        Tiles tileToReturn = (currentSidePickTile != null) ? currentSidePickTile : tileToThrow;
        StartCoroutine(RollbackSidePickProcess(currentPlayerQue, tileToReturn));
    }

    // İSTEĞE BAĞLI: Cezasız İade (Buton ile)
    public void CancelSidePickAction()
    {
        if (currentSidePickTile == null)
            return;
        int currentPlayerQue = tileDistrubite.GetQueueNumberOfPlayer(PhotonNetwork.LocalPlayer);
        StartCoroutine(RollbackSidePickProcess(currentPlayerQue, currentSidePickTile, false));
    }

    // Geri Alma Coroutine (Hem cezalı hem cezasız için)
    private IEnumerator RollbackSidePickProcess(
        int playerQue,
        Tiles tileToReturn,
        bool giveNewTile = true
    )
    {
        // 1. Taşı sola geri koy (Kimin koyduğunu parametre yolla)
        tileDistrubite.photonView.RPC("ReturnTileToSide", RpcTarget.All, playerQue, tileToReturn);

        // 2. Taşı elden sil
        tileDistrubite.photonView.RPC(
            "RemoveTileFromPlayerListByValue",
            RpcTarget.AllBuffered,
            playerQue,
            tileToReturn
        );

        yield return new WaitForSeconds(0.2f);

        if (giveNewTile)
        {
            // Ortadan ceza olarak taş ver
            tileDistrubite.photonView.RPC(
                "AddTileFromMiddlePlayerList",
                RpcTarget.AllBuffered,
                playerQue
            );
        }
        else
        {
            // Cezasız iadede durumları sıfırla, tekrar çekebilsin
            turnManager.hasPickedFromSide = false;
            turnManager.canDrop = false;
        }

        currentSidePickTile = null;
    }

    // --- CEZA HESAPLAMA (ATILAN TAŞ İÇİN) ---
    // GameManager.cs içindeki CalculateThrowPenalty metodunu bununla değiştir:

    public void CalculateThrowPenalty(Tiles thrownTile)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        Tiles indicator = tileDistrubite.GetIndicatorTile();
        int penaltyAmount = 0;
        string reason = "";

        // --- OKEY TAŞINI HESAPLA ---
        // Kural: Göstergenin aynı rengi ve bir fazlası Okey'dir.
        int okeyNumber = -1;
        TileColor okeyColor = TileColor.black; // Varsayılan

        if (indicator != null)
        {
            okeyColor = indicator.color;
            okeyNumber = indicator.number + 1;
            if (okeyNumber > 13)
                okeyNumber = 1; // 13 ise 1 olur
        }

        // 1. OKEY ATMA CEZASI (EN BÜYÜK SUÇ)
        // Atılan taşın rengi ve numarası Okey taşıyla aynı mı?
        if (thrownTile.color == okeyColor && thrownTile.number == okeyNumber)
        {
            penaltyAmount = 101; // Okey atmanın cezası genelde 101'dir (veya PDF'e göre 250)
            reason = "Okey Taşı Atıldı!";
        }
        // 2. SAHTE OKEY (JOKER RESİMLİ) ATMA
        else if (thrownTile.type == TileType.FakeJoker)
        {
            // Bazı kurallarda serbesttir, bazılarında cezadır. PDF'e göre ceza ise:
            // penaltyAmount = 101;
            // reason = "Sahte Okey Atıldı";
        }
        // 3. GÖSTERGE ATMA CEZASI
        else if (
            indicator != null
            && thrownTile.color == indicator.color
            && thrownTile.number == indicator.number
        )
        {
            penaltyAmount = 101; // PDF Kaynak 19
            reason = "Gösterge Atıldı";
        }
        // 4. İŞLEK TAŞ ATMA CEZASI
        else
        {
            // Masada yeri olan (Available) bir taşı mı attı?
            bool isProcessable = false;
            if (tileDistrubite.availableTiles != null)
            {
                foreach (var t in tileDistrubite.availableTiles)
                {
                    // Tam eşleşme (Renk, Numara)
                    if (t.color == thrownTile.color && t.number == thrownTile.number)
                    {
                        // Tip kontrolü (Joker değilse)
                        if (t.type != TileType.Joker && t.type != TileType.FakeJoker)
                        {
                            isProcessable = true;
                            break;
                        }
                    }
                }
            }

            if (isProcessable)
            {
                penaltyAmount = 101; // PDF Kaynak 13: 250 veya standart 101
                reason = "İşlek Taş Atıldı";
            }
        }

        if (penaltyAmount > 0)
        {
            int targetPlayer = turnManager.currentTurnPlayer;

            // Eğer PDF'te ceza 250 ise burayı 250 yap
            if (penaltyAmount == 101)
                penaltyAmount = 250;

            scoreManager.UpdatePlayerScore(targetPlayer, penaltyAmount);
            Debug.Log(
                $"<color=red>CEZA! {reason}. Oyuncu {targetPlayer} -> {penaltyAmount} Puan.</color>"
            );
        }
    }

    // --- OYUN BİTİŞ KONTROLÜ ---
    // GameManager.cs
    // --- BU FONKSİYON EKSİK OLDUĞU İÇİN HATA ALIYORSUN ---
    [PunRPC]
    public void FinishGameRPC(int winnerActorNumber, bool isOkeyShot, bool isDoubleFinish)
    {
        Debug.Log($"Oyun Bitti Sinyali Alındı! Kazanan ActorID: {winnerActorNumber}");

        // Oyunun bittiğini işaretle (Çift tetiklemeyi önlemek için)
        isGameEnded = true;

        // Skor hesaplamasını SADECE Master Client yapar ve herkese sonucunu yollar.
        // Böylece herkes kendi kafasına göre hesap yapıp senkronizasyonu bozmaz.
        if (PhotonNetwork.IsMasterClient)
        {
            CalculateAndDistributeScores(winnerActorNumber, isOkeyShot, isDoubleFinish);
        }
    }

    public void CheckGameStatus(HandData data)
    {
        // Eğer oyun zaten bittiyse işlem yapma
        if (isGameEnded)
            return;

        // --- 1. SENARYO: ELİM BİTTİ Mİ? ---
        // Bu kontrolü Master Client şartına bağlamıyoruz.
        // Kimin eli bittiyse (Local Player), o kişi sunucuya "BİTTİM" diye bağırır (RPC atar).

        if (data.handTiles.Count == 0)
        {
            Debug.Log($"OYUN BİTTİ TESPİT EDİLDİ! Kazanan ActorNumber: {data.actorNumber}");

            // Kendi elim bittiyse, herkese bittiğini ben haber veririm.
            // Master olmama gerek yok, kazanan benim.
            photonView.RPC("FinishGameRPC", RpcTarget.All, data.actorNumber, false, false);
            return; // Fonksiyondan çık
        }

        // --- 2. SENARYO: ORTADA TAŞ KALMADI MI? ---
        // Bu global bir durumdur. Herkesin aynı anda tetiklememesi için
        // bunu SADECE Master Client kontrol etmeli.

        if (PhotonNetwork.IsMasterClient)
        {
            if (tileDistrubite.allTiles.Count == 0)
            {
                Debug.Log("OYUN BİTTİ! Ortada taş kalmadı.");
                photonView.RPC("FinishGameRPC", RpcTarget.All, -1, false, false);
            }
        }
    }

    // --- SKORLARI HESAPLA VE DAĞIT (MASTER) ---
    private void CalculateAndDistributeScores(
        int winnerActorNumber,
        bool isOkeyShot,
        bool isDoubleFinish
    )
    {
        isGameEnded = true;

        List<int> actorNumbers = new List<int>();
        List<int> finalScores = new List<int>();

        int multiplier = 1;
        int finishMultiplier = 1;
        Tiles indicator = tileDistrubite.GetIndicatorTile();

        if (indicator != null)
        {
            if (colorMultipliers.ContainsKey(indicator.color))
                multiplier = colorMultipliers[indicator.color];
            if (indicator.type == TileType.FakeJoker)
                multiplier = 8;
        }
        if (isDoubleFinish)
            finishMultiplier *= 2;
        if (isOkeyShot)
            finishMultiplier *= 2;

        foreach (var player in PhotonNetwork.PlayerList)
        {
            int pQue = tileDistrubite.GetQueueNumberOfPlayer(player);
            int scoreChange = 0;

            if (player.ActorNumber == winnerActorNumber)
            {
                scoreChange = -100 * multiplier * finishMultiplier;
            }
            else
            {
                bool hasOpened = scoreManager.HasPlayerOpened(pQue);
                if (!hasOpened)
                {
                    scoreChange = 600 * finishMultiplier;
                }
                else
                {
                    int tileCount = tileDistrubite.GetPlayerHandCount(pQue);
                    scoreChange = (tileCount * 60) * finishMultiplier;
                }
            }

            int currentScore = 0;
            if (scoreManager.playerScores.ContainsKey(pQue))
                currentScore = scoreManager.playerScores[pQue];

            int totalFinalScore = currentScore + scoreChange;

            actorNumbers.Add(player.ActorNumber);
            finalScores.Add(totalFinalScore);
        }

        photonView.RPC(
            "OnGameEndedRPC",
            RpcTarget.All,
            actorNumbers.ToArray(),
            finalScores.ToArray()
        );
    }

    // --- HERKESTE ÇALIŞAN FİNAL OPERASYONU ---
    [PunRPC]
    public void OnGameEndedRPC(int[] actors, int[] scores)
    {
        isGameEnded = true;

        Dictionary<int, int> scoreboard = new Dictionary<int, int>();

        for (int i = 0; i < actors.Length; i++)
        {
            Player p = PhotonNetwork.CurrentRoom.GetPlayer(actors[i]);
            if (p != null)
            {
                int pQue = tileDistrubite.GetQueueNumberOfPlayer(p);
                // ScoreManager'ı güncelle
                if (scoreManager.playerScores.ContainsKey(pQue))
                    scoreManager.playerScores[pQue] = scores[i];
                else
                    scoreManager.playerScores.Add(pQue, scores[i]);

                scoreboard[pQue] = scores[i];
            }
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowGameOver(scoreboard);
        }

        StartCoroutine(RestartSequence());
    }

    // GameManager.cs -> RestartSequence

    // GameManager.cs içine:

    private IEnumerator RestartSequence()
    {
        // 10 saniye bekle (Skor tablosunu görsünler)
        Debug.Log("Oyun bitti, 10 saniye sonra herkes Lobiye dönecek...");
        yield return new WaitForSeconds(10f);

        // "ŞİMDİLİK" ÇÖZÜMÜ:
        // Sahneyi yeniden başlatmak yerine, herkesi odadan çıkartıyoruz.
        // Bu komutu çağıran kişi Photon odasından düşer ve 'OnLeftRoom' tetiklenir.
        PhotonNetwork.LeaveRoom();
    }
}
