using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Photon.Pun;
using UnityEngine;

// Masa sahnesindeki skor geçmişi: oda özelliklerini (Hand{n}, SeatNames) okur, ceza kaydı tutar.
// Yalnız gösterim verisi; oyun durumuna yazmaz.
public class MatchScores : MonoBehaviourPunCallbacks
{
    public static MatchScores Instance { get; private set; }

    public const string SeatNamesKey = "SeatNames";
    const string HandPrefix = "Hand";

    public MatchHistory History { get; } = new MatchHistory();
    public string[] SeatNames { get; private set; } = new string[] { "", "", "", "" };
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    int previewRounds;
#endif
    public int TotalRounds
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (previewRounds > 0) return previewRounds;
#endif
            return PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("TotalRounds", out object v) && v is int n ? n : 0;
        }
    }

    public event Action Changed;
    public event Action<HandResultData> HandArrived;

    public struct PenaltyEntry { public int seat, amount; public PenaltyReason reason; }

    readonly List<PenaltyEntry> current = new List<PenaltyEntry>();
    readonly Dictionary<int, List<PenaltyEntry>> byHand = new Dictionary<int, List<PenaltyEntry>>();
    readonly Dictionary<int, int[]> raw = new Dictionary<int, int[]>();

    public IReadOnlyList<PenaltyEntry> CurrentHandPenalties => current;
    public IReadOnlyList<PenaltyEntry> PenaltiesOf(int handIndex) =>
        byHand.TryGetValue(handIndex, out var l) ? l : (IReadOnlyList<PenaltyEntry>)new List<PenaltyEntry>();

    void Awake()
    {
        Instance = this;
        ReadAll();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // Sahneye geç yüklenen istemci de var olan özellikleri alır
    void ReadAll()
    {
        if (PhotonNetwork.CurrentRoom == null) return;
        try { Apply(PhotonNetwork.CurrentRoom.CustomProperties); } catch (Exception e) { Debug.LogException(e); }
    }

    public override void OnRoomPropertiesUpdate(Hashtable changed)
    {
        try { Apply(changed); } catch (Exception e) { Debug.LogException(e); }
    }

    void Apply(Hashtable props)
    {
        if (props == null) return;
        bool changed = false;
        var arrived = new List<(HandResultData d, bool first)>();
        foreach (DictionaryEntry kv in props)
        {
            string key = kv.Key as string;
            if (key == null) continue;
            if (key == SeatNamesKey)
            {
                if (kv.Value is string[] names && names.Length == 4) { SeatNames = names.Select(n => n ?? "").ToArray(); changed = true; }
            }
            else if (key.StartsWith(HandPrefix) && key.Length > HandPrefix.Length && char.IsDigit(key[HandPrefix.Length]))
            {
                var arr = kv.Value as int[];
                var d = HandBreakdown.Decode(arr);
                if (d == null) continue;
                raw.TryGetValue(d.handIndex, out int[] old);
                var kind = HandBreakdown.Classify(old, arr);
                if (kind == HandBreakdown.Arrival.Same) continue;
                raw[d.handIndex] = arr;
                History.Set(d);
                changed = true;
                arrived.Add((d, kind == HandBreakdown.Arrival.New));
            }
        }
        foreach (var (d, first) in arrived)
        {
            // süren elin cezaları yalnız ilk gelişte bu ele taşınır
            if (first && current.Count > 0) { byHand[d.handIndex] = new List<PenaltyEntry>(current); current.Clear(); }
            Debug.Log(ScoreLine(d.handIndex));
            if (HandArrived != null)
                foreach (Action<HandResultData> h in HandArrived.GetInvocationList())
                    try { h(d); } catch (Exception e) { Debug.LogException(e); }
        }
        if (changed && Changed != null)
            foreach (Action h in Changed.GetInvocationList())
                try { h(); } catch (Exception e) { Debug.LogException(e); }
    }

    // [SKOR] el {n}: 1={t1} ... ; satırı olmayan koltuk "-"
    string ScoreLine(int handIndex)
    {
        var sb = new StringBuilder("[SKOR] el ").Append(handIndex).Append(':');
        for (int seat = 1; seat <= 4; seat++)
        {
            bool has = History.Hands.Any(h => h.rows.Any(r => r.seat == seat));
            sb.Append(' ').Append(seat).Append('=').Append(has ? History.Total(seat).ToString() : "-");
        }
        return sb.ToString();
    }

    // Yeni el başlarken: önceki elin yazılmamış cezaları yeni ele taşınmaz
    public void StartNewHand() => current.Clear();

    // Fotoğraftan kurulum: süren elin ceza listesi fotoğraftakiyle değişir.
    public void RestoreCurrent(IEnumerable<PenaltyEntry> entries)
    {
        current.Clear();
        current.AddRange(entries);
    }

    // ShowPenaltyRPC'den; sıra bilinmiyorsa sessizce atlar
    public void RecordPenalty(int que, int amount, PenaltyReason reason)
    {
        try
        {
            var td = GameManager.Instance != null ? GameManager.Instance.tileDistrubite : null;
            int seat = SeatMath.MeldSeat(td != null ? td.seatOfQue : null, que);
            if (seat < 0) return;
            current.Add(new PenaltyEntry { seat = seat, amount = amount, reason = reason });
        }
        catch (Exception e) { Debug.LogException(e); }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Yalnız ekran görüntüsü doğrulaması: sahte oda verisi (oyun akışı kullanmaz)
    public void PreviewSetRounds(int n) => previewRounds = n;
    public void PreviewSetSeatNames(string[] names) => SeatNames = names;
    public void PreviewAddPenalty(int handIndex, int seat, int amount, PenaltyReason reason)
    {
        var e = new PenaltyEntry { seat = seat, amount = amount, reason = reason };
        if (handIndex <= 0) { current.Add(e); return; }
        if (!byHand.TryGetValue(handIndex, out var l)) byHand[handIndex] = l = new List<PenaltyEntry>();
        l.Add(e);
    }
#endif

    // master
    public void WriteHand(HandResultData d)
    {
        var a = HandBreakdown.Encode(d);
        if (a == null || PhotonNetwork.CurrentRoom == null) return;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { HandPrefix + d.handIndex, a } });
    }

    public void WriteSeatNames(string[] names)
    {
        if (names == null || names.Length != 4 || PhotonNetwork.CurrentRoom == null) return;
        if (names.SequenceEqual(SeatNames)) return;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { SeatNamesKey, names } });
    }
}
