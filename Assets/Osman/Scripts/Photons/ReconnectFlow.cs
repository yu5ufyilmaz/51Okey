using System.Collections;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

// Oyundayken beklenmeyen kopma ve açılışta kayıtlı odaya dönüş: nötr "Reconnect" sahnesine geçilir, oradan yeniden katılınır
// (Table'da kalınca PUN sahneyi yeniden yüklemiyor, oda nesneleri ölü sahneye düşüyor). PhotonManager nesnesinde (DDOL) durur.
public class ReconnectFlow : MonoBehaviourPunCallbacks
{
    public const string MatchOverKey = "MatchOver";

    public static bool DeliberateLeave; // ExitButton.ExitGame ve maç sonu LeaveRoom öncesi true
    public static bool Active { get; private set; }
#if UNITY_EDITOR
    public static float TestHoldSeconds; // harness: ilk denemeden önce bekleme (float.MaxValue = sıfırlanana kadar)
#endif

    static ReconnectFlow inst;
    static bool quitting;
    int attempt;
    float windowStart;
    bool inFlight,
        waitingMaster;

    void Awake()
    {
        inst = this;
        Application.quitting += () => quitting = true;
    }

    // Soğuk açılış "Dön": lobideyken kayıtlı odaya.
    public static void RejoinSaved(string roomName)
    {
        if (inst == null || Active)
            return;
        Debug.Log($"[DONUS] kayıtlı odaya dönülüyor: {roomName}");
        inst.Begin();
    }

    // Maç kurtarılamaz: odadan tamamen çıkılır, kayıt silinir, "Oyun sona ermiş".
    public static void EndUnrecoverable()
    {
        Debug.Log("[DONUS] oyun sona ermiş");
        Active = false;
        DeliberateLeave = true;
        RejoinRecord.Clear();
        PhotonNetwork.AutomaticallySyncScene = true;
        bool leavingTable = SceneManager.GetActiveScene().name == "Table" && PhotonNetwork.InRoom;
        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom(false);
        else if (PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InLobby)
            PhotonNetwork.JoinLobby();
        UIRoot.HideLoading();
        if (inst != null)
            inst.StartCoroutine(NoticeInLobby());
        // Table'da ExitButton.OnLeftRoom (bilerek çıkış) lobiye götürür; iki sahne yüklemesi yarışmasın.
        if (!leavingTable)
            SceneChangeManager.Instance.ChangeScene("MainMenu");
    }

    // Sahne değişimi açık pencereyi kapatır: bildirim yolun sonunda, lobide.
    static IEnumerator NoticeInLobby()
    {
        for (float t0 = Time.unscaledTime; SceneManager.GetActiveScene().name != "LobbyMenu"; )
        {
            if (Time.unscaledTime - t0 > 60f)
                yield break;
            yield return null;
        }
        yield return null;
        _ = UIRoot.Notice(Loc.T("rejoin.ask_title"), Loc.T("rejoin.game_over"));
    }

    // Canlı olmayan istemci masa sahibi olamaz: görevi Ctl biti açık etkin bir insana verir; yoksa maç kurtarılamaz.
    public static void HandOffMaster()
    {
        var props = PhotonNetwork.CurrentRoom?.CustomProperties;
        var seats = SeatManager.Instance;
        int ctl = CtlBits.Read(props);
        var to = seats == null ? null : PhotonNetwork.PlayerListOthers.FirstOrDefault(p => !p.IsInactive && CtlBits.Has(ctl, seats.GetSeatNumberOfPlayer(p)));
        if (to == null)
        {
            Debug.Log("[DONUS] masa sahibi oldum, devredecek canlı insan yok");
            EndUnrecoverable();
            return;
        }
        Debug.Log($"[DONUS] masa sahibi oldum, görev #{to.ActorNumber} {to.NickName} oyuncusuna");
        PhotonNetwork.SetMasterClient(to);
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (newMasterClient.IsLocal && PhotonNetwork.InRoom && !RestoreGate.Live)
            HandOffMaster();
    }

    public override void OnLeftRoom() => OnDropped("OnLeftRoom");

    public override void OnDisconnected(DisconnectCause cause)
    {
        if (Active)
        {
            if (inFlight)
                Failed(0, cause.ToString());
            return;
        }
        OnDropped("OnDisconnected " + cause);
    }

    void OnDropped(string why)
    {
        if (Active || quitting)
            return;
        bool inGame = SceneManager.GetActiveScene().name == "Table";
        if (!RejoinPolicy.ShouldReconnect(DeliberateLeave, RejoinRecord.Load() != null, inGame))
            return;
        Debug.Log($"[DONUS] koptu ({why}), yeniden bağlanılıyor");
        Begin();
    }

    void Begin()
    {
        Active = true;
        inFlight = waitingMaster = false;
        // Katılınca Table'ı biz yükleriz: maç bittiyse ya da kurtarılamazsa hiç yüklenmesin.
        PhotonNetwork.AutomaticallySyncScene = false;
        StopAllCoroutines();
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        SceneChangeManager.Instance.ChangeScene("Reconnect");
        while (SceneManager.GetActiveScene().name != "Reconnect")
            yield return null;
        yield return null; // SceneChangeManager yükleme sonunda katmanı kapatır
        UIRoot.ShowLoading(Loc.T("rejoin.reconnecting"), false);
#if UNITY_EDITOR
        for (float t0 = Time.unscaledTime; Time.unscaledTime - t0 < TestHoldSeconds; )
            yield return null;
#endif
        attempt = 0;
        windowStart = Time.unscaledTime;
        Attempt();
    }

    void Attempt()
    {
        attempt++;
        inFlight = true;
        string room = RejoinRecord.Load();
        var st = PhotonNetwork.NetworkClientState;
        Debug.Log($"[DONUS] deneme {attempt} başladı ({st})");
        if (room == null)
        {
            EndUnrecoverable();
            return;
        }
        if (st == ClientState.ConnectedToMasterServer || st == ClientState.JoinedLobby)
        {
            if (!PhotonNetwork.RejoinRoom(room))
                Failed(0, "RejoinRoom false");
        }
        else if (!PhotonNetwork.ReconnectAndRejoin())
        {
            // Önceki oda bilgisi yok (ör. bağlantı hâlâ kapanıyor): ana sunucuya bağlanıp RejoinRoom.
            waitingMaster = true;
            if (!PhotonNetwork.IsConnected && !PhotonNetwork.ConnectUsingSettings())
                Failed(0, "ConnectUsingSettings false");
        }
    }

    public override void OnConnectedToMaster()
    {
        if (!Active || !waitingMaster)
            return;
        waitingMaster = false;
        if (!PhotonNetwork.RejoinRoom(RejoinRecord.Load() ?? ""))
            Failed(0, "RejoinRoom false");
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        if (Active)
            Failed(returnCode, message);
    }

    void Failed(short code, string why)
    {
        if (!inFlight)
            return;
        inFlight = waitingMaster = false;
        Debug.Log($"[DONUS] deneme {attempt} {code} {why}");
        switch (RejoinPolicy.OnFailure(code, Time.unscaledTime - windowStart))
        {
            case RejoinStep.Retry:
                StartCoroutine(RetryLater());
                break;
            case RejoinStep.GiveUpClear:
                EndUnrecoverable();
                break;
            default:
                // Kayıt kalır: ana menüde dönüş sorusu çıkar.
                Debug.Log("[DONUS] vazgeçildi, kayıt kaldı");
                Active = false;
                PhotonNetwork.AutomaticallySyncScene = true;
                UIRoot.HideLoading();
                if (!PhotonNetwork.IsConnected)
                    _ = PhotonManager.ConnectWhenReadyAsync();
                else if (PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InLobby && !PhotonNetwork.InRoom)
                    PhotonNetwork.JoinLobby();
                SceneChangeManager.Instance.ChangeScene("MainMenu");
                break;
        }
    }

    IEnumerator RetryLater()
    {
        yield return new WaitForSecondsRealtime(RejoinPolicy.RetryDelaySec);
        if (Active && !inFlight)
            Attempt();
    }

    public override void OnJoinedRoom()
    {
        if (!Active)
        {
            DeliberateLeave = false; // yeni oda: önceki çıkışın niyeti taşınmasın
            return;
        }
        inFlight = false;
        DeliberateLeave = false;
        var room = PhotonNetwork.CurrentRoom;
        bool over = room.CustomProperties.TryGetValue(MatchOverKey, out object v) && v is bool b && b;
        bool othersActive = PhotonNetwork.PlayerListOthers.Any(p => !p.IsInactive);
        Debug.Log($"[DONUS] odaya dönüldü actor {PhotonNetwork.LocalPlayer.ActorNumber}, deneme {attempt}, maç bitti {over}, etkin başkası {othersActive}");
        if (over || !othersActive)
        {
            EndUnrecoverable();
            return;
        }
        Active = false;
        PhotonNetwork.AutomaticallySyncScene = true;
        // PUN'un kendi yüklemesi (ileti kuyruğu sahne yüklenene kadar durur); SceneChangeManager değil.
        PhotonNetwork.LoadLevel("Table");
    }
}
