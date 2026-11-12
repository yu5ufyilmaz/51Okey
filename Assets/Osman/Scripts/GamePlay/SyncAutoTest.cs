using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// Sadece Editor'de ve <proje>/Temp/SyncAutoTest/config.json varken çalışır (Tools/SyncTest/Run-SyncTest.ps1 yazar).
// Odaya girer, kendi koltuğunu insan kod yolundan oynar, durumunu status.json'a yazar.
public partial class SyncAutoTest : MonoBehaviourPunCallbacks
{
    public enum State
    {
        Connecting,
        Lobby,
        Joining,
        InTable,
        Playing,
        Done,
        Error,
    }

    const float StuckSeconds = 60f;
    const float MoveTimeout = 5f;
    const int MaxErrors = 30;

    public static SyncAutoTest Instance { get; private set; }
    public SyncAutoTestConfig Config { get; private set; }

    State state;
    float stateSince;
    float nextStatusWrite;
    bool wasReady;
    int deal;
    int myMoves;
    string root;
    RoomManager roomManager;
    TurnManager turnManager;
    bool moving;
    float lastSeatRequest = -10f;
    bool seatMoved;
    bool botsReleased;
    float humansSeatedSince = -1f;
    readonly SyncAutoTestStatus status = new SyncAutoTestStatus();
    readonly Dictionary<string, RoomInfo> rooms = new Dictionary<string, RoomInfo>();
    const string AnyRoom = "*";

    int MyQue =>
        PhotonNetwork.LocalPlayer != null
        && PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object q)
            ? (int)q
            : 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!Application.isEditor || Instance != null)
            return;
        string root = Directory.GetCurrentDirectory();
        if (!SyncAutoTestFiles.TryLoadConfig(root, out var config))
            return;
        var go = new GameObject("SyncAutoTest");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<SyncAutoTest>();
        Instance.root = root;
        Instance.Config = config;
        Debug.LogWarning(
            $"[AUTOTEST] config bulundu, otopilot aktif: {config.runId} {config.role} {config.nickName}"
        );
    }

    public override void OnEnable()
    {
        base.OnEnable();
        Application.logMessageReceived += OnLog;
        GameManager.HandResult += OnHandResult;
    }

    public override void OnDisable()
    {
        base.OnDisable();
        Application.logMessageReceived -= OnLog;
        GameManager.HandResult -= OnHandResult;
        journal?.Dispose();
    }

    void Start() => SetState(State.Connecting);

    void Update()
    {
        if (Time.unscaledTime >= nextStatusWrite)
            WriteStatus();
        if (state == State.Done || state == State.Error)
            return;

        if (state < State.InTable && Time.unscaledTime - stateSince > StuckSeconds)
        {
            Fail($"{state} durumunda {StuckSeconds} sn takıldı");
            return;
        }

        switch (state)
        {
            case State.Connecting:
                if (PhotonNetwork.InLobby)
                {
                    PhotonNetwork.NickName = Config.PhotonName;
                    SceneChangeManager.Instance.ChangeScene("LobbyMenu");
                    SetState(State.Lobby);
                }
                break;

            case State.Lobby:
                // ButtonFuncs.Start lobiye yeniden girer; o oturmadan CreateRoom/JoinRoom reddedilir.
                roomManager = FindObjectOfType<RoomManager>();
                if (
                    roomManager != null
                    && Time.unscaledTime - stateSince > 1f
                    && PhotonNetwork.NetworkClientState == ClientState.JoinedLobby
                )
                {
                    // "*": telefonda kurulan odanın adı bilinmiyor; lobide boş koltuğu olan ilk odaya gir.
                    if (Config.roomName == AnyRoom)
                    {
                        var open = rooms.Values.FirstOrDefault(r => r.IsOpen && r.PlayerCount < r.MaxPlayers);
                        if (open == null)
                            break;
                        Config.roomName = open.Name;
                        Debug.LogWarning($"[AUTOTEST] açık oda bulundu: {open.Name} ({open.PlayerCount}/{open.MaxPlayers})");
                    }
                    // Şifreli oda: aynı AppId'deki bir telefon build'i JoinRandom ile test odasına düşmesin.
                    if (Config.IsHost)
                    {
                        // Bot doldurma süresi sadece üst sınır: asıl bitiş, insanlar oturunca ReleaseBotsWhenHumansSeated'dan gelir.
                        if (Config.botFillDelaySec > 0)
                            SeatManager.BotFillDelayOverride = Config.botFillDelaySec;
                        roomManager.CreateCustomRoom(Config.roomName, Config.runId, Config.deals, true, Config.fillWithBots);
                    }
                    else
                        Join();
                    SetState(State.Joining);
                }
                break;

            case State.Joining:
                if (SceneManager.GetActiveScene().name != "Table")
                    break;
                var seats = FindObjectOfType<SeatManager>();
                if (seats == null)
                    break;
                // Listeden (şifreyle) giren koltuğunu kendisi seçer: ilk boş koltuğu ister, kapılırsa saniyede bir yeniden.
                if (!seats.IsSeated(PhotonNetwork.LocalPlayer))
                {
                    if (Time.unscaledTime - lastSeatRequest > 1f && seats.FirstFreeSeat() > 0)
                    {
                        lastSeatRequest = Time.unscaledTime;
                        seats.RequestSeat(seats.FirstFreeSeat());
                    }
                    break;
                }
                // Koltuk değiştirme de denensin: ilk misafir masa dolmadan bir kez başka boş koltuğa geçer.
                if (Config.nickName == "AT-g1" && !seatMoved && seats.FirstFreeSeat() > 0)
                {
                    seatMoved = true;
                    Debug.Log($"[AUTOTEST] koltuk değiştiriyor -> {seats.FirstFreeSeat()}");
                    seats.RequestSeat(seats.FirstFreeSeat());
                }
                if (PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey("SeatNumber"))
                {
                    if (Config.IsHost && Config.seed != 0 && DesyncDetector.Instance != null)
                        DesyncDetector.Instance.forcedSeed = Config.seed;
                    SetState(State.InTable);
                }
                break;

            case State.InTable:
            case State.Playing:
                if (Config.IsHost && Config.humans > 0 && !botsReleased)
                    ReleaseBotsWhenHumansSeated();
                bool ready = GameManager.Instance != null && GameManager.Instance.isGameReady;
                if (ready && !wasReady)
                {
                    deal++;
                    SetState(State.Playing);
                }
                wasReady = ready;
                if (state == State.Playing)
                    TickPlaying();
                break;
        }
    }

    // Beklenen insan sayısı 2 sn boyunca oturmuş kalırsa (AT-g1 koltuk değiştirmesi bitsin) botlar hemen dolsun.
    void ReleaseBotsWhenHumansSeated()
    {
        var seats = FindObjectOfType<SeatManager>();
        if (seats == null)
            return;
        if (PhotonNetwork.PlayerList.Count(p => seats.IsSeated(p)) < Config.humans)
        {
            humansSeatedSince = -1f;
            return;
        }
        if (humansSeatedSince < 0f)
            humansSeatedSince = Time.unscaledTime;
        else if (Time.unscaledTime - humansSeatedSince >= 2f)
        {
            SeatManager.BotFillDelayOverride = 0f;
            botsReleased = true;
            Debug.Log($"[AUTOTEST] {Config.humans} insan oturdu, botlar dolduruluyor");
        }
    }

    void TickPlaying()
    {
        var gm = GameManager.Instance;
        if (moving || !gm.isGameReady || gm.isGameEnded)
            return;
        if (turnManager == null)
            turnManager = FindObjectOfType<TurnManager>();
        if (turnManager != null && turnManager.IsPlayerTurn())
            StartCoroutine(PlayMyTurn());
    }

    IEnumerator PlayMyTurn()
    {
        moving = true;
        yield return StartCoroutine(Config.IsSmart ? SmartTurn() : DrawAndDiscard());
        moving = false;
    }

    // Hamleler insan oyuncunun yolundan: TileUI.OnBeginDrag/OnEndDrag. RPC'yi oyun kendisi atar.
    IEnumerator DrawAndDiscard()
    {
        int que = MyQue;
        Transform hand = GameObject.Find("PlayerTileContainer").transform;

        // 1. sıradaki oyuncu eli fazla taşla açar, çekmeden atar (canDrop baştan true).
        if (!turnManager.canDrop)
        {
            // Ortadaki TileUI'ler sadece görüntü; çekilen hangisi olursa allTiles[0] ile giydirilir. Üstteki = son sibling.
            Transform middle = GameObject.Find("MiddleTileContainer").transform;
            TileUI top =
                middle.childCount > 0 ? middle.GetChild(middle.childCount - 1).GetComponent<TileUI>() : null;
            Placeholder slot = hand.GetComponentsInChildren<Placeholder>()
                .FirstOrDefault(p => !p.isRight && !p.isMeldArea && p.transform.childCount == 0);
            if (top == null || slot == null)
            {
                Fail($"çekme: orta taş={top != null} boş slot={slot != null}");
                yield break;
            }
            if (!DragTo(top, slot.transform))
            {
                Fail("çekme reddedildi");
                yield break;
            }
            for (float t = 0; !(turnManager.canDrop && top.transform.parent == slot.transform); t += Time.unscaledDeltaTime)
            {
                if (GameManager.Instance.isGameEnded)
                    yield break;
                if (t > MoveTimeout)
                {
                    Fail("çekme zaman aşımı");
                    yield break;
                }
                yield return null;
            }
        }

        yield return new WaitForSeconds(Config.moveDelaySec);

        string id = FindObjectOfType<ScoreManager>().FindBestTileToDiscardForBot(que);
        TileUI tile = hand.GetComponentsInChildren<TileUI>()
            .FirstOrDefault(x => x.tileDataInfo != null && x.tileDataInfo.id == id);
        if (tile == null)
        {
            Fail($"atılacak taş elde yok: {id}");
            yield break;
        }
        // IndicatorTileContainer da isRight; atma yeri ıstakadaki RightTileContainer.
        if (!DragTo(tile, GameObject.Find("RightTileContainer").transform))
        {
            Fail("atma reddedildi");
            yield break;
        }
        for (float t = 0; turnManager.currentTurnPlayer == que && !GameManager.Instance.isGameEnded; t += Time.unscaledDeltaTime)
        {
            if (t > MoveTimeout)
            {
                Fail("atma zaman aşımı");
                yield break;
            }
            yield return null;
        }

        myMoves++;
        // Harness'in kendi testi: sadece yerel deste bozulur, master sonraki snapshot'ta [DESYNC] görmeli.
        if (Config.injectDesyncAtMove > 0 && myMoves == Config.injectDesyncAtMove)
        {
            var tiles = FindObjectOfType<TileDistrubite>().allTiles;
            tiles.RemoveAt(tiles.Count - 1);
            Debug.LogWarning("[AUTOTEST] desync enjekte edildi");
        }
        WriteStatus();
    }

    bool DragTo(TileUI tile, Transform target) => TileUI.SimulateDrag(tile, target);

    void Join() => roomManager.JoinRoomWithPassword(Config.roomName, Config.runId, Config.runId);

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        foreach (var r in roomList)
            if (r.RemovedFromList)
                rooms.Remove(r.Name);
            else
                rooms[r.Name] = r;
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        // Host odayı henüz kurmamış olabilir.
        if (state == State.Joining && !Config.IsHost)
            StartCoroutine(RetryJoin());
    }

    IEnumerator RetryJoin()
    {
        yield return new WaitForSeconds(1f);
        if (state == State.Joining && roomManager != null)
            Join();
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        if (state == State.Joining)
            Fail($"CreateRoom: {message}");
    }

    public override void OnLeftRoom()
    {
        if (state != State.InTable && state != State.Playing)
            return;
        if (deal >= Config.deals)
            SetState(State.Done);
        else
            Fail("odadan düştü");
    }

    public static bool IsGameError(string message, string stackTrace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            return false;
        // Master'ın desync'leri DesyncDetector sayacından okunur.
        if (message.StartsWith("[DESYNC]"))
            return false;
        // Stack'i Editor'de başlayan hatalar (Package Manager, Unity Remote/adb...) oyunla ilgisiz.
        return !(stackTrace ?? "").TrimStart().StartsWith("UnityEditor.");
    }

    void OnLog(string message, string stackTrace, LogType type)
    {
        if (!IsGameError(message, stackTrace, type) || status.errors.Count >= MaxErrors)
            return;
        string firstFrame = string.IsNullOrEmpty(stackTrace) ? "" : stackTrace.Split('\n')[0];
        status.errors.Add(firstFrame.Length > 0 ? $"{message} @ {firstFrame}" : message);
    }

    void Fail(string reason)
    {
        status.errors.Insert(0, reason);
        Debug.LogWarning($"[AUTOTEST] Error: {reason}");
        SetState(State.Error);
    }

    void SetState(State next)
    {
        state = next;
        stateSince = Time.unscaledTime;
        WriteStatus();
    }

    void WriteStatus()
    {
        nextStatusWrite = Time.unscaledTime + 1f;
        status.runId = Config.runId;
        status.role = Config.role;
        status.nickName = Config.nickName;
        status.state = state.ToString();
        status.actor = PhotonNetwork.LocalPlayer?.ActorNumber ?? 0;
        status.que = MyQue;
        status.deal = deal;
        status.myMoves = myMoves;
        status.isMyTurn =
            state == State.Playing && turnManager != null && turnManager.currentTurnPlayer == MyQue;
        var dd = DesyncDetector.Instance;
        if (Config.IsHost && dd != null)
        {
            status.sync.ok = dd.OkCount;
            status.sync.desync = dd.DesyncCount;
            status.sync.first = dd.FirstDesync;
        }
        status.desyncLog = dd?.LogPath;
        try
        {
            SyncAutoTestFiles.WriteStatus(root, status);
        }
        catch (IOException)
        {
            // Runner o an dosyayı okuyorsa Replace başarısız olur; Update 1 sn sonra tekrar yazar.
        }
    }
}
