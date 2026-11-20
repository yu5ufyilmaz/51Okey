using System;
using System.Collections;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

// Dönen istemci ile masa sahibi arasındaki el sıkışma (hepsi tur sınırında, anahtar = el + tur serisi):
// istek (160) -> fotoğraf (161) -> her NextTurn'de özet raporu (162) -> geri verme (163) ya da yeniden (164).
// DDOL; TurnManager üstünde değil (ForceSkipBotTurn oradaki coroutine'leri durdurur). Mesajlar RaiseEvent: oda nesnelerine bağlı değil.
public class RejoinHandshake : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public const byte RequestEvent = 160, SnapshotEvent = 161, ReportEvent = 162, GrantEvent = 163, AgainEvent = 164;

    static RejoinHandshake inst;
    static HandshakeBook book = new HandshakeBook();
    static bool rejoined, // bu odaya oyun ortasında dönüldü
        tableReady;
    static int agains;

    public static bool Granted { get; private set; } // bu odada koltuk geri verildi
    public static event Action GrantedNow;

    // Dönen istemci, koltuk geri verilene kadar: özet satırları kıyasa girmez.
    public static bool Restoring => rejoined && !Granted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (inst != null)
            return;
        var go = new GameObject("RejoinHandshake");
        DontDestroyOnLoad(go);
        inst = go.AddComponent<RejoinHandshake>();
    }

    // Master: aktör defterde mi (DesyncDetector onu saymaz).
    public static bool IsRestoring(int actor) => PhotonNetwork.IsMasterClient && book.IsRestoring(actor);

    static int Round =>
        PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("CurrentRound", out object v) && v is int r ? r : 0;

    static void Send(byte code, object content, RaiseEventOptions to) => PhotonNetwork.RaiseEvent(code, content, to, SendOptions.SendReliable);

    static RaiseEventOptions ToMaster => new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient };

    static RaiseEventOptions To(params int[] actors) => new RaiseEventOptions { TargetActors = actors };

    public override void OnJoinedRoom()
    {
        book = new HandshakeBook();
        Granted = false;
        tableReady = false;
        agains = 0;
        rejoined = PhotonNetwork.LocalPlayer.HasRejoined && SeatManager.SeatsLocked;
        StopAllCoroutines();
        if (rejoined)
            StartCoroutine(WaitTableThenRequest());
    }

    public override void OnLeftRoom()
    {
        rejoined = tableReady = false;
        StopAllCoroutines();
    }

    // Table'ın oda nesneleri (önbellekten yeniden kurulur) gelince istek. Yeni elle canlı olsa da istenir: defter onu tanımalı.
    IEnumerator WaitTableThenRequest()
    {
        while (PhotonNetwork.InRoom
            && !(SceneManager.GetActiveScene().name == "Table" && GameManager.Instance != null
                && GameObject.Find("TileManager(Clone)") != null && GameObject.Find("ScoreManager(Clone)") != null))
            yield return null;
        if (!PhotonNetwork.InRoom)
            yield break;
        tableReady = true;
        Debug.Log($"[DONUS] masa hazır, canlı {RestoreGate.Live}");
        if (PhotonNetwork.IsMasterClient)
            ReconnectFlow.HandOffMaster(); // devir sonrası OnMasterClientSwitched isteği yollar
        else
            Request();
    }

    static void Request()
    {
        Debug.Log($"[DONUS] fotoğraf istendi (el {Round})");
        Send(RequestEvent, new object[] { Round }, ToMaster);
    }

    static void ShowOverlay() => UIRoot.ShowLoading(Loc.T(HandshakeRule.OverlayKey(agains)), false);

    // TurnManager.NextTurn sonu (TableDigestLog.AtTurnBoundary'den sonra).
    public static void OnTurnBoundary()
    {
        if (!PhotonNetwork.InRoom)
            return;
        if (PhotonNetwork.IsMasterClient)
            SendSnapshots();
        else if (Restoring && tableReady && RestoreGate.Live)
            Report();
    }

    static void SendSnapshots()
    {
        var pend = book.TakePending();
        if (pend.Count == 0)
            return;
        var s = TableDigestLog.Last;
        var tm = GameManager.Instance != null ? GameManager.Instance.turnManager : null;
        if (s == null || tm == null || s.round != Round || s.turnSerial != tm.TurnSerial)
        {
            Debug.LogWarning("[DONUS] fotoğraf bu turda kurulamadı, sonraki tura");
            foreach (int a in pend)
                book.Request(a);
            return;
        }
        Send(SnapshotEvent, SnapshotCodec.Encode(s), To(pend.ToArray()));
        Debug.Log($"[DONUS] fotoğraf yollandı seri {s.round}.{s.turnSerial} -> #{string.Join(",#", pend)}");
    }

    // İstemci: kendi ıstakasını veriden kurar, özeti masa sahibine bildirir.
    static void Report()
    {
        var gm = GameManager.Instance;
        var td = gm != null ? gm.tileDistrubite : null;
        var sm = gm != null ? gm.scoreManager : null;
        if (td == null || sm == null || gm.turnManager == null)
            return;
        td.RebuildLocalRack();
        string rack = td.RackMismatch();
        bool flags = sm.OpenFlagsConsistent();
        bool ready = rack == null && flags;
        var s = TableDigestLog.Last;
        uint digest = TableDigestLog.LastDigest;
        if (s == null || s.round != Round || s.turnSerial != gm.turnManager.TurnSerial)
        {
            s = SnapshotBuilder.FromScene(); // yayın yapısında (master değilken) AtTurnBoundary kurmaz
            if (s == null)
                return;
            digest = TableDigest.Of(s);
        }
        Send(ReportEvent, new object[] { s.round, s.turnSerial, unchecked((int)digest), ready }, ToMaster);
        Debug.Log($"[DONUS] özet raporu {s.round}.{s.turnSerial} {digest:x8} hazır {ready}{(rack != null ? " ıstaka: " + rack : "")}{(flags ? "" : " açış bayrakları tutarsız")}");
    }

    // RPC_PrepareNextRound başı.
    public static void OnPrepareNextRound(bool wasLive)
    {
        if (PhotonNetwork.IsMasterClient)
            book.PendingToRestoring();
        else if (!wasLive && Restoring)
            Debug.Log("[DONUS] yeni elle canlı");
    }

    public void OnEvent(EventData e)
    {
        if (e.Code < RequestEvent || e.Code > AgainEvent || !PhotonNetwork.InRoom)
            return;
        bool fromMaster = PhotonNetwork.MasterClient != null && e.Sender == PhotonNetwork.MasterClient.ActorNumber;
        try
        {
            switch (e.Code)
            {
                case RequestEvent:
                    if (PhotonNetwork.IsMasterClient)
                        OnRequest(e.Sender);
                    break;
                case ReportEvent:
                    if (PhotonNetwork.IsMasterClient)
                        OnReport(e.Sender, (object[])e.CustomData);
                    break;
                case SnapshotEvent:
                    if (fromMaster && Restoring)
                        OnSnapshot(e.CustomData as object[]);
                    break;
                case GrantEvent:
                    if (fromMaster && Restoring)
                        OnGrant((object[])e.CustomData);
                    break;
                case AgainEvent:
                    if (fromMaster && Restoring)
                        OnAgain();
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    // --- Masa sahibi ---

    static void OnRequest(int actor)
    {
        var p = PhotonNetwork.CurrentRoom.GetPlayer(actor);
        var sm = SeatManager.Instance;
        int seat = p != null && sm != null ? sm.GetSeatNumberOfPlayer(p) : -1;
        if (seat < 1)
            return;
        // Fotoğraf isteyen koltuğunu oynayamaz (geri almada kurulum tutmadıysa): önce bota.
        if (CtlBits.Has(sm.IntendedCtl, seat))
        {
            Debug.Log($"[DONUS] koltuk {seat} fotoğraf istedi, bota alınıyor");
            if (GameManager.Instance != null)
                GameManager.Instance.HandleSeatLoss(seat);
            else
                sm.SetSeatControl(seat, false);
        }
        book.Request(actor);
        Debug.Log($"[DONUS] istek #{actor} koltuk {seat}");
    }

    static void OnReport(int actor, object[] d)
    {
        int round = (int)d[0], serial = (int)d[1];
        uint digest = unchecked((uint)(int)d[2]);
        bool ready = (bool)d[3];
        var gm = GameManager.Instance;
        var sm = SeatManager.Instance;
        var p = PhotonNetwork.CurrentRoom.GetPlayer(actor);
        if (gm == null || sm == null || p == null || gm.turnManager == null || gm.tileDistrubite == null)
            return;
        int seat = sm.GetSeatNumberOfPlayer(p);
        int que = SeatMath.QueOfSeat(gm.tileDistrubite.seatOfQue, seat);
        uint? mine = TableDigestLog.Ring.TryGet(round, serial, out uint m) ? m : (uint?)null;
        var tm = gm.turnManager;
        var dec = HandshakeRule.Decide(mine, digest, ready, tm.currentTurnPlayer == que, tm.BotRunningFor(que));
        Debug.Log($"[DONUS] karar #{actor} koltuk {seat} {round}.{serial} {digest:x8}/{(mine.HasValue ? mine.Value.ToString("x8") : "yok")} hazır {ready} -> {dec}");
        if (!book.OnDecision(actor, dec))
            return;
        if (dec == HandshakeDecision.Again)
        {
            Send(AgainEvent, new object[] { round, serial }, To(actor));
            return;
        }
        sm.SetSeatControl(seat, true);
        // Cezalar özette yok: el içi ceza listesi geri vermeyle taşınır.
        var pen = MatchScores.Instance != null ? MatchScores.Instance.CurrentHandPenalties.ToList() : new System.Collections.Generic.List<MatchScores.PenaltyEntry>();
        Send(GrantEvent, new object[] { round, serial, pen.Select(x => x.seat).ToArray(), pen.Select(x => x.amount).ToArray(), pen.Select(x => (int)x.reason).ToArray() }, To(actor));
        Debug.Log($"[DONUS] geri verildi koltuk {seat} seri {serial}");
    }

    // --- Dönen istemci ---

    static void OnSnapshot(object[] payload)
    {
        if (RestoreGate.Live)
        {
            Debug.Log("[DONUS] fotoğraf yok sayıldı (zaten canlı)");
            return;
        }
        TableSnapshot s = null;
        try { s = payload != null ? SnapshotCodec.Decode(payload) : null; }
        catch (Exception ex) { Debug.LogWarning($"[DONUS] fotoğraf çözülemedi: {ex.Message}"); }
        if (s == null)
        {
            Debug.LogWarning("[DONUS] bozuk fotoğraf");
            Request();
            return;
        }
        Debug.Log($"[DONUS] fotoğraf alındı seri {s.round}.{s.turnSerial}");
        SnapshotApplier.Apply(s);
        int replayed = RestoreGate.GoLive(s.turnSerial);
        Debug.Log($"[DONUS] kuruldu seri {s.turnSerial} ({replayed} sıradaki oynatıldı)");
    }

    static void OnGrant(object[] d)
    {
        var gm = GameManager.Instance;
        var td = gm != null ? gm.tileDistrubite : null;
        var sm = gm != null ? gm.scoreManager : null;
        if (td == null || sm == null || gm.turnManager == null)
            return;
        td.RebuildLocalRack();
        string rack = td.RackMismatch();
        bool flags = sm.OpenFlagsConsistent();
        if (!RestoreGate.Live || rack != null || !flags)
        {
            Debug.LogWarning($"[DONUS] geri alma tutmadı (canlı {RestoreGate.Live}, ıstaka {rack ?? "tamam"}, bayraklar {flags}); yeni fotoğraf");
            RestoreGate.Enter();
            ShowOverlay();
            Request();
            return;
        }
        int[] seats = (int[])d[2], amounts = (int[])d[3], reasons = (int[])d[4];
        MatchScores.Instance?.RestoreCurrent(seats.Select((s, i) => new MatchScores.PenaltyEntry { seat = s, amount = amounts[i], reason = (PenaltyReason)reasons[i] }));
        Granted = true;
        agains = 0;
        var tm = gm.turnManager;
        tm.canDrop = tm.IsPlayerTurn() && td.GetPlayerTiles().Count == 15;
        UIRoot.HideLoading();
        Debug.Log($"[DONUS] geri alındı seri {(int)d[0]}.{(int)d[1]}");
        GrantedNow?.Invoke();
    }

    static void OnAgain()
    {
        agains++;
        RestoreGate.Enter();
        ShowOverlay();
        Debug.Log($"[DONUS] yeniden {agains}");
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (newMasterClient.IsLocal)
            book = new HandshakeBook(); // eski defter geçersiz: bekleyenler isteği yeniden yollar
        if (Restoring && !tableReady)
            return; // masa kurulunca WaitTableThenRequest devreder ya da ister
        // Canlı olmayan istemci masa sahibi olamaz (masa sahibinin ceza RPC'leri kapısız).
        if (newMasterClient.IsLocal && PhotonNetwork.InRoom && (!RestoreGate.Live || Restoring))
        {
            ReconnectFlow.HandOffMaster();
            return;
        }
        if (!Restoring || !tableReady || !PhotonNetwork.InRoom)
            return;
        RestoreGate.Enter();
        ShowOverlay();
        Request();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (PhotonNetwork.IsMasterClient)
            book.Forget(otherPlayer.ActorNumber);
    }
}
