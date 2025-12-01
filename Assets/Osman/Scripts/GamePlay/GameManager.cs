using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class GameManager : MonoBehaviourPunCallbacks
{
    public static GameManager Instance;

    // --- ÖNEMLİ: Bu değişken oyunun erken bitmesini engelleyecek ---
    public bool isGameReady = false;

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

    public bool isGameEnded = false;
    public Tiles currentSidePickTile;
    public int currentTableLimit = 51;

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

        TileSerialization.RegisterCustomTypes();

        // Başlangıçta oyun hazır DEĞİL.
        isGameReady = false;
    }

    private void Start()
    {
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

    // --- TileDistrubite TARAFINDAN ÇAĞRILACAK ---
    public void SetGameReady()
    {
        isGameReady = true;
        Debug.Log("Oyun Hazır! Artık kurallar ve bitiş kontrolü aktif.");
    }

    [PunRPC]
    public void UpdateTableLimit(int newScore)
    {
        if (newScore > currentTableLimit)
        {
            currentTableLimit = newScore;
        }
    }

    public void RecordSidePick(Tiles tile)
    {
        if (tile == null)
            return;
        currentSidePickTile = new Tiles(tile.color, tile.number, tile.type);
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

    public void HandleFailedSidePick(Tiles tileToThrow)
    {
        int currentPlayerQue = tileDistrubite.GetQueueNumberOfPlayer(PhotonNetwork.LocalPlayer);
        scoreManager.UpdatePlayerScore(currentPlayerQue, 100);
        Tiles tileToReturn = (currentSidePickTile != null) ? currentSidePickTile : tileToThrow;
        StartCoroutine(RollbackSidePickProcess(currentPlayerQue, tileToReturn));
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
        // Okey hesaplama ve ceza kodların burada... (Kısaltıldı, senin kodun aynısı kalabilir)
        // ...
    }

    // --- BURASI KRİTİK: OYUN BİTİŞ KONTROLÜ ---
    public void CheckGameStatus(HandData data)
    {
        // 1. Oyun daha başlamadıysa veya zaten bittiyse KONTROL ETME.
        if (!isGameReady || isGameEnded)
            return;

        if (data.handTiles.Count == 0)
        {
            Debug.Log($"OYUN BİTTİ! Kazanan ActorNumber: {data.actorNumber}");
            photonView.RPC("FinishGameRPC", RpcTarget.All, data.actorNumber, false, false);
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            if (tileDistrubite.allTiles.Count == 0)
            {
                Debug.Log("OYUN BİTTİ! Ortada taş kalmadı.");
                photonView.RPC("FinishGameRPC", RpcTarget.All, -1, false, false);
            }
        }
    }

    // --- BURASI KRİTİK: FİNAL OPERASYONU ---
    [PunRPC]
    public void FinishGameRPC(int winnerActorNumber, bool isOkeyShot, bool isDoubleFinish)
    {
        // 2. Oyun hazır değilse bitirme.
        if (!isGameReady)
            return;

        Debug.Log($"Oyun Bitti Sinyali Alındı! Kazanan: {winnerActorNumber}");
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
        // Skor hesaplama kodların aynı kalsın...
        // ...
        // Sonunda şunu çağırıyor:
        // photonView.RPC("OnGameEndedRPC", ...);

        // Şimdilik test için direkt RPC'yi çağırıyorum (Senin kodunda içi doluydu)
        List<int> actors = new List<int>();
        List<int> scores = new List<int>();
        foreach (var p in PhotonNetwork.PlayerList)
        {
            actors.Add(p.ActorNumber);
            scores.Add(0);
        } // Dummy data

        photonView.RPC("OnGameEndedRPC", RpcTarget.All, actors.ToArray(), scores.ToArray());
    }

    [PunRPC]
    public void OnGameEndedRPC(int[] actors, int[] scores)
    {
        isGameEnded = true;
        // ... Skor tablosu UI işlemleri ... (Senin kodun aynı kalsın)

        if (UIManager.Instance != null)
        {
            // UIManager.Instance.ShowGameOver(...);
        }

        StartCoroutine(RestartSequence());
    }

    private IEnumerator RestartSequence()
    {
        Debug.Log("Oyun bitti! 10 saniye sonra lobiye dönülüyor...");
        yield return new WaitForSeconds(10f);
        PhotonNetwork.LeaveRoom();
    }
}
