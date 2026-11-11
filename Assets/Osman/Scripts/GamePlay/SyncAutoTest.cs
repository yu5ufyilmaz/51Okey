using System.Collections;
using System.IO;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

// Sadece Editor'de ve <proje>/Temp/SyncAutoTest/config.json varken çalışır (Tools/SyncTest/Run-SyncTest.ps1 yazar).
// Odaya girer, kendi koltuğunu insan kod yolundan oynar, durumunu status.json'a yazar.
public class SyncAutoTest : MonoBehaviourPunCallbacks
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
    readonly SyncAutoTestStatus status = new SyncAutoTestStatus();

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
    }

    public override void OnDisable()
    {
        base.OnDisable();
        Application.logMessageReceived -= OnLog;
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
                    PhotonNetwork.NickName = Config.nickName;
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
                    // Şifreli oda: aynı AppId'deki bir telefon build'i JoinRandom ile test odasına düşmesin.
                    if (Config.IsHost)
                        roomManager.CreateCustomRoom(Config.roomName, Config.runId, Config.deals, true, false);
                    else
                        Join();
                    SetState(State.Joining);
                }
                break;

            case State.Joining:
                if (
                    SceneManager.GetActiveScene().name == "Table"
                    && PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey("SeatNumber")
                )
                {
                    if (Config.IsHost && Config.seed != 0 && DesyncDetector.Instance != null)
                        DesyncDetector.Instance.forcedSeed = Config.seed;
                    SetState(State.InTable);
                }
                break;

            case State.InTable:
            case State.Playing:
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

    void TickPlaying() { }

    void Join() => roomManager.JoinRoomWithPassword(Config.roomName, Config.runId, Config.runId);

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

    void OnLog(string message, string stackTrace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            return;
        // Master'ın desync'leri DesyncDetector sayacından okunur. Unity Remote hatası Editor'ün
        // Android hedefte her Play'de bastığı adb uyarısı, oyunla ilgisi yok.
        if (
            message.StartsWith("[DESYNC]")
            || message.Contains("Unity Remote requirements check failed")
            || status.errors.Count >= MaxErrors
        )
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
