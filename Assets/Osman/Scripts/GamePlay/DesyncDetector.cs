using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Sadece Editor / Development build: her hamleden sonra (Deal, NextTurn, Finish) her client
// kendi masa snapshot'ını alır, master'a yollar. Master kıyaslar, fark varsa [DESYNC] basar.
// Her client ayrıca tüm snapshot'ları dosyaya yazar (editörde <proje>/Logs, cihazda persistentDataPath).
public class DesyncDetector : MonoBehaviourPunCallbacks, IOnEventCallback
{
    const byte SnapshotEvent = 150;
    const int KeepMoves = 64;

    public static DesyncDetector Instance { get; private set; }

    [Tooltip("0 = rastgele. Master'da dağıtımdan önce girilirse aynı el tekrar dağıtılır.")]
    public int forcedSeed;

    struct Entry
    {
        public string label,
            snapshot;
    }

    int deal,
        move;
    StreamWriter log;
    readonly Dictionary<string, Dictionary<int, Entry>> received =
        new Dictionary<string, Dictionary<int, Entry>>();
    readonly Queue<string> receivedOrder = new Queue<string>();
    readonly HashSet<string> badKeys = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!Debug.isDebugBuild || Instance != null)
            return;
        var go = new GameObject("DesyncDetector");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<DesyncDetector>();
    }

    public override void OnDisable()
    {
        base.OnDisable();
        CloseLog();
    }

    public override void OnJoinedRoom()
    {
        // Yeni oda = yeni sayaç ve yeni log dosyası; yoksa önceki odadan kalan sayaçlar kayar.
        deal = move = 0;
        received.Clear();
        receivedOrder.Clear();
        badKeys.Clear();
        CloseLog();
    }

    public static int NextDealSeed()
    {
        int seed =
            Instance != null && Instance.forcedSeed != 0 ? Instance.forcedSeed : Environment.TickCount;
        Instance?.Write($"SEED {seed}");
        return seed;
    }

    public static void Report(string label, bool newDeal = false)
    {
        if (Instance == null || !PhotonNetwork.InRoom)
            return;
        Instance.Capture(label, newDeal);
    }

    void Capture(string label, bool newDeal)
    {
        if (newDeal)
        {
            deal++;
            move = 0;
        }
        else
            move++;

        string snapshot = BuildSnapshot();
        Write($"=== {deal}.{move} {label}\n{snapshot}");

        if (PhotonNetwork.IsMasterClient)
            Store(deal, move, PhotonNetwork.LocalPlayer.ActorNumber, label, snapshot);
        else
            PhotonNetwork.RaiseEvent(
                SnapshotEvent,
                new object[] { deal, move, label, snapshot },
                new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient },
                SendOptions.SendReliable
            );
    }

    // Skorlar (sadece master'da tam), availableTiles (zamanlamaya bağlı) ve room property'ler bilerek yok.
    static string BuildSnapshot()
    {
        var sb = new StringBuilder();
        var tm = FindObjectOfType<TurnManager>();
        var sm = FindObjectOfType<ScoreManager>();
        var gm = GameManager.Instance;

        sb.Append("turn=")
            .Append(tm != null ? tm.currentTurnPlayer : -1)
            .Append(" ready=")
            .Append(gm != null && gm.isGameReady)
            .Append(" ended=")
            .Append(gm != null && gm.isGameEnded)
            .AppendLine();
        if (sm != null)
            sb.Append("opened=").AppendLine(string.Join(",", sm.playersWhoOpened.OrderBy(q => q)));
        FindObjectOfType<TileDistrubite>()?.AppendSyncState(sb);
        return sb.ToString();
    }

    public void OnEvent(EventData e)
    {
        if (e.Code != SnapshotEvent || !PhotonNetwork.IsMasterClient)
            return;
        var d = (object[])e.CustomData;
        Store((int)d[0], (int)d[1], e.Sender, (string)d[2], (string)d[3]);
    }

    void Store(int dealNo, int moveNo, int actor, string label, string snapshot)
    {
        string key = $"{dealNo}.{moveNo}";
        if (!received.TryGetValue(key, out var byActor))
        {
            byActor = new Dictionary<int, Entry>();
            received[key] = byActor;
            receivedOrder.Enqueue(key);
            while (receivedOrder.Count > KeepMoves)
            {
                string old = receivedOrder.Dequeue();
                received.Remove(old);
                badKeys.Remove(old);
            }
        }
        byActor[actor] = new Entry { label = label, snapshot = snapshot };

        // Referans: master'ın kendi kopyası; o henüz yoksa ilk gelen.
        int me = PhotonNetwork.LocalPlayer.ActorNumber;
        IEnumerable<int> against =
            actor == me ? byActor.Keys.Where(a => a != me)
            : byActor.ContainsKey(me) ? new[] { me }
            : byActor.Keys.Take(1).Where(a => a != actor);
        foreach (int other in against.ToList())
            Compare(key, other, byActor[other], actor, byActor[actor]);

        int expected = PhotonNetwork.PlayerList.Count(p => !p.IsInactive);
        if (byActor.Count == expected && !badKeys.Contains(key))
            Debug.Log($"<color=green>[SYNC OK]</color> {key} {label} ({expected} client)");
    }

    void Compare(string key, int a, Entry ea, int b, Entry eb)
    {
        if (ea.label == eb.label && ea.snapshot == eb.snapshot)
            return;
        badKeys.Add(key);

        var la = Fields(ea.snapshot);
        var lb = Fields(eb.snapshot);
        var msg = new StringBuilder(
            $"[DESYNC] {key}: #{a} {Nick(a)} ({ea.label}) vs #{b} {Nick(b)} ({eb.label})"
        );
        foreach (string field in la.Keys.Concat(lb.Keys).Distinct())
        {
            la.TryGetValue(field, out string x);
            lb.TryGetValue(field, out string y);
            if (x != y)
                msg.Append($"\n  #{a}: {x ?? field + "=<yok>"}\n  #{b}: {y ?? field + "=<yok>"}");
        }
        Debug.LogError(msg);
        Write(msg.ToString());
    }

    // "hand2(14)=..." satırını "hand2" anahtarıyla tutar; satır kayması yanlış alarm vermesin.
    static Dictionary<string, string> Fields(string snapshot)
    {
        var map = new Dictionary<string, string>();
        foreach (string line in snapshot.Split('\n'))
        {
            if (line.Length == 0)
                continue;
            int cut = line.IndexOfAny(new[] { '=', '(' });
            map[cut > 0 ? line.Substring(0, cut) : line] = line;
        }
        return map;
    }

    static string Nick(int actor) => PhotonNetwork.CurrentRoom?.GetPlayer(actor)?.NickName ?? "?";

    public static string TileKey(Tiles t)
    {
        if (t == null)
            return "-";
        string id = string.IsNullOrEmpty(t.id) ? "?" : t.id.Substring(0, Math.Min(4, t.id.Length));
        // Joker'ın renk/sayısını kural motoru her client'ta farklı yazıyor; sadece tipini say.
        if (t.type == TileType.Number)
            return $"{"rbky"[(int)t.color]}{t.number}:{id}";
        return $"{(t.type == TileType.Joker ? "J" : "F")}:{id}";
    }

    public static string TileList(List<Tiles> tiles) =>
        tiles == null ? "-" : string.Join(" ", tiles.Select(TileKey));

    void Write(string text)
    {
        if (log == null)
        {
            string dir = Application.isEditor
                ? Path.Combine(Directory.GetCurrentDirectory(), "Logs")
                : Application.persistentDataPath;
            Directory.CreateDirectory(dir);
            int actor = PhotonNetwork.LocalPlayer?.ActorNumber ?? 0;
            string path = Path.Combine(dir, $"desync_{DateTime.Now:yyyyMMdd_HHmmss}_actor{actor}.log");
            log = new StreamWriter(path) { AutoFlush = true };
            Debug.Log($"[DESYNC] Log dosyası: {path}");
        }
        log.WriteLine(text);
    }

    void CloseLog()
    {
        log?.Dispose();
        log = null;
    }
}
