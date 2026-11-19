using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Saf metin yardımcıları (test edilir)
public static class ScoreboardText
{
    public static string Signed(int v) => v > 0 ? "+" + v.ToString(CultureInfo.InvariantCulture) : v.ToString(CultureInfo.InvariantCulture);
    public static string Dash(int v) => v == 0 ? "-" : Signed(v);
    // from'dan 0'a; eksiye düşmez
    public static int Countdown(float startedAt, float now, int from) => Math.Max(0, (int)Math.Floor(from - (now - startedAt)));
}

// Masa skor tablosu: el bitince kendiliğinden (kapatılamaz, geri sayım), el içinde TABLO düğmesiyle (aç/kapa).
// Hücreler builder'da hazır kurulur; burada yalnız metin ve renk yazılır.
public class ScoreboardView : MonoBehaviour
{
    public static ScoreboardView Instance { get; private set; }

    const int CountFrom = 10, MaxHands = 7;
    static readonly Color Green = new Color(0.42f, 0.86f, 0.33f), Red = new Color(1f, 0.38f, 0.33f), Mute = new Color(0.62f, 0.70f, 0.78f);
    static readonly Color NameMine = new Color(1f, 0.92f, 0.55f);

    [Serializable]
    public class RowRefs
    {
        public GameObject root;
        public Image bg;           // yerel oyuncu vurgusu (yalnız onun satırında açık)
        public TMP_Text[] cells;   // üst: ad, kalan, ceza, bitirme, bu el; alt: ad, 7 el, toplam
        public TMP_Text marker;    // alt: "önde"
    }

    [SerializeField] GameObject window;
    [SerializeField] UiWindow uiWindow;
    [SerializeField] GameObject closeButton;
    [SerializeField] TMP_Text header, penaltyLine, footer;
    [SerializeField] TMP_Text[] handHeaders;   // 7
    [SerializeField] RowRefs[] topRows;        // 4, koltuk sırası
    [SerializeField] RowRefs[] botRows;        // 4

    [NonSerialized] public int previewLocalSeat;   // yalnız ekran görüntüsü doğrulaması

    MatchScores hooked;
    HandResultData shown;      // el sonu gösterilen el
    bool handEnd;
    int lastHand = -1, lastSecs = -1;
    float startedAt;

    bool Open => window != null && window.activeSelf;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    void OnEnable() { Loc.Changed += Redraw; Hook(); }

    void OnDisable()
    {
        Loc.Changed -= Redraw;
        Unhook();
    }

    // MatchScores aynı sahnede, Awake sırası belli değil: bulunana kadar Update dener
    void Hook()
    {
        var m = MatchScores.Instance;
        if (m == null || hooked == m) return;
        Unhook();
        hooked = m;
        m.HandArrived += OnHandArrived;
        m.Changed += Redraw;
    }

    void Unhook()
    {
        if (hooked == null) return;
        hooked.HandArrived -= OnHandArrived;
        hooked.Changed -= Redraw;
        hooked = null;
    }

    void OnHandArrived(HandResultData d)
    {
        if (d == null) return;
        // panel kapandıktan sonra gelen aynı el kopyası yeni elin üstünü açmasın
        if (!handEnd && d.handIndex == lastHand) return;
        ShowHandEnd(d);
    }

    public void ShowHandEnd(HandResultData d)
    {
        if (d == null || window == null) return;
        if (handEnd && shown != null && shown.handIndex == d.handIndex && Open)
        {
            shown = d;   // aynı elin değişen içeriği: yalnız yenile, geri sayım sürer
            Redraw();
            return;
        }
        handEnd = true;
        shown = d;
        lastHand = d.handIndex;
        startedAt = Time.unscaledTime;
        lastSecs = -1;
        uiWindow.closable = false;
        closeButton.SetActive(false);
        window.SetActive(true);
        Redraw();
    }

    // TABLO düğmesi
    public void Toggle()
    {
        if (window == null || (handEnd && Open)) return;
        if (Open) { window.SetActive(false); return; }
        handEnd = false;
        shown = null;
        uiWindow.closable = true;
        closeButton.SetActive(true);
        window.SetActive(true);
        Redraw();
    }

    public void Hide()
    {
        handEnd = false;
        shown = null;
        if (window != null) window.SetActive(false);
    }

    void Update()
    {
        if (hooked == null) Hook();
        if (!handEnd || !Open) return;
        int s = ScoreboardText.Countdown(startedAt, Time.unscaledTime, CountFrom);
        if (s != lastSecs) { lastSecs = s; footer.text = FooterText(s); }
    }

    bool IsLast(int hand, MatchScores m) => m.TotalRounds > 0 && hand >= m.TotalRounds;

    string FooterText(int secs) =>
        shown != null && IsLast(shown.handIndex, MatchScores.Instance)
            ? Loc.T("table.back_to_lobby_in", secs)
            : Loc.T("table.next_hand_in", secs);

    static int LocalSeat(int preview)
    {
        if (preview > 0) return preview;
        try
        {
            var p = PhotonNetwork.LocalPlayer;
            if (p != null && p.CustomProperties != null && p.CustomProperties.TryGetValue("SeatNumber", out object o) && o is int s) return s;
        }
        catch (Exception) { }
        return 0;
    }

    static Color TotalColor(int v) => v < 0 ? Green : v > 0 ? Red : Color.white;

    string NameOf(int seat, MatchScores m) =>
        seat >= 1 && seat <= 4 && !string.IsNullOrEmpty(m.SeatNames[seat - 1]) ? m.SeatNames[seat - 1] : "#" + seat;

    void Redraw()
    {
        var m = MatchScores.Instance;
        if (!Open || m == null) return;
        var hist = m.History;
        var hands = hist.Hands;
        int me = LocalSeat(previewLocalSeat);
        int rounds = Mathf.Clamp(m.TotalRounds > 0 ? m.TotalRounds : Mathf.Max(1, hands.Count), 1, MaxHands);

        var present = new bool[5];
        for (int s = 1; s <= 4; s++) present[s] = !string.IsNullOrEmpty(m.SeatNames[s - 1]);
        foreach (var h in hands) foreach (var r in h.rows) if (r.seat >= 1 && r.seat <= 4) present[r.seat] = true;
        int leader = hist.LeaderSeat;

        // başlık
        header.text = HeaderText(m, hist);

        // üst tablo + ceza dökümü
        var mine = new List<string>();
        var running = new int[5];
        if (shown == null)
        {
            foreach (var e in m.CurrentHandPenalties)
            {
                if (e.seat >= 1 && e.seat <= 4) running[e.seat] += e.amount;
                if (e.seat == me) mine.Add(PenaltyLine(e));
            }
        }
        else
        {
            foreach (var e in m.PenaltiesOf(shown.handIndex)) if (e.seat == me) mine.Add(PenaltyLine(e));
        }
        penaltyLine.gameObject.SetActive(mine.Count > 0);
        if (mine.Count > 0) penaltyLine.text = Loc.T("table.your_penalties", string.Join(" · ", mine));

        for (int i = 0; i < 4; i++)
        {
            int seat = i + 1;
            var row = topRows[i];
            row.root.SetActive(present[seat]);
            if (!present[seat]) continue;
            Paint(row, seat == me, seat == me ? Loc.T("table.you") : NameOf(seat, m));
            var c = row.cells;
            for (int k = 1; k < c.Length; k++) { c[k].text = "-"; c[k].color = Color.white; }
            if (shown == null)
            {
                c[2].text = ScoreboardText.Dash(running[seat]);
            }
            else
            {
                HandRow? found = null;
                foreach (var r in shown.rows) if (r.seat == seat) { found = r; break; }
                if (found.HasValue)
                {
                    var r = found.Value;
                    c[1].text = shown.winnerSeat == seat ? "-"
                        : r.opened ? Loc.T("table.tiles_left", r.tilesLeft, ScoreboardText.Signed(r.handPoints))
                        : Loc.T("table.not_opened", ScoreboardText.Signed(r.handPoints));
                    c[2].text = ScoreboardText.Dash(r.inHandPenalty);
                    c[3].text = ScoreboardText.Dash(r.finishReward);
                    c[4].text = ScoreboardText.Signed(r.handTotal);
                    c[4].color = TotalColor(r.handTotal);
                }
            }
        }

        // alt tablo: eller + toplam
        for (int n = 0; n < MaxHands; n++)
        {
            bool on = n < rounds;
            handHeaders[n].gameObject.SetActive(on);
            if (on) handHeaders[n].text = Loc.T("table.hand_n", n + 1);
        }
        for (int i = 0; i < 4; i++)
        {
            int seat = i + 1;
            var row = botRows[i];
            row.root.SetActive(present[seat]);
            if (!present[seat]) continue;
            Paint(row, seat == me, seat == me ? Loc.T("table.you") : NameOf(seat, m));
            for (int n = 0; n < MaxHands; n++)
            {
                var cell = row.cells[1 + n];
                cell.gameObject.SetActive(n < rounds);
                if (n >= rounds) continue;
                HandResultData h = null;
                foreach (var x in hands) if (x.handIndex == n + 1) { h = x; break; }
                if (h == null) { cell.text = "."; cell.color = Mute; continue; }
                HandRow? found = null;
                foreach (var r in h.rows) if (r.seat == seat) { found = r; break; }
                if (!found.HasValue) { cell.text = "-"; cell.color = Mute; continue; }
                cell.text = ScoreboardText.Signed(found.Value.handTotal);
                cell.color = TotalColor(found.Value.handTotal);
            }
            int total = hist.Total(seat);
            var tc = row.cells[1 + MaxHands];
            tc.text = ScoreboardText.Signed(total);
            tc.color = TotalColor(total);
            row.marker.text = leader == seat ? Loc.T("table.leading") : "";   // hücre yer tutar: satırlar hizalı kalır
        }

        footer.text = handEnd && shown != null ? FooterText(ScoreboardText.Countdown(startedAt, Time.unscaledTime, CountFrom)) : "";
        FitAll();
    }

    string HeaderText(MatchScores m, MatchHistory hist)
    {
        if (shown == null) return Loc.T("table.scoreboard_title");
        var d = shown;
        if (IsLast(d.handIndex, m)) return Loc.T("table.match_over", NameOf(hist.LeaderSeat, m));
        string s = Loc.T("table.hand_over", d.handIndex);
        if (d.winnerSeat == 0) return s + " · " + Loc.T("table.pile_empty");
        s += " · " + Loc.T("table.finished_by", NameOf(d.winnerSeat, m));
        var parts = new List<string>();
        if (d.okeyFinish) parts.Add(Loc.T("table.with_okey"));
        if (d.pairsFinish) parts.Add(Loc.T("table.from_pairs"));
        if (d.kafa) parts.Add(Loc.T("table.kafa"));
        if (d.finishMultiplier > 1) parts.Add("x" + d.finishMultiplier);
        return parts.Count > 0 ? s + " (" + string.Join(", ", parts) + ")" : s;
    }

    static string PenaltyLine(MatchScores.PenaltyEntry e) => PenaltyReasonText.Of(e.reason) + " " + ScoreboardText.Signed(e.amount);

    static void Paint(RowRefs row, bool mine, string name)
    {
        row.bg.enabled = mine;
        row.cells[0].text = name;
        row.cells[0].color = mine ? NameMine : Color.white;
    }

    // Aynı sütundaki hücreler en dar hücrenin boyuna iner: tek hücre küçülüp sütun dengesiz görünmesin
    void FitAll()
    {
        Canvas.ForceUpdateCanvases();
        var names = Col(topRows, 0); names.AddRange(Col(botRows, 0));
        FitGroup(names, 36f, 22f);
        for (int k = 1; k <= 4; k++) FitGroup(Col(topRows, k), 36f, 22f);
        var handCells = new List<TMP_Text>();
        for (int k = 1; k <= MaxHands; k++) handCells.AddRange(Col(botRows, k));
        FitGroup(handCells, 36f, 20f);
        FitGroup(Col(botRows, MaxHands + 1), 36f, 22f);
        FitGroup(handHeaders.Where(h => h.gameObject.activeSelf).ToList(), 28f, 18f);
    }

    // görünen satırların k. sütun hücreleri
    static List<TMP_Text> Col(RowRefs[] rows, int k)
    {
        var l = new List<TMP_Text>();
        foreach (var r in rows) if (r.root.activeSelf && r.cells[k].gameObject.activeSelf) l.Add(r.cells[k]);
        return l;
    }

    void FitGroup(List<TMP_Text> cells, float max, float min)
    {
        if (cells.Count == 0) return;
        float low = max;
        foreach (var c in cells)
        {
            c.enableAutoSizing = true;
            c.fontSizeMin = min;
            c.fontSizeMax = max;
            c.ForceMeshUpdate();
            low = Mathf.Min(low, c.fontSize);
        }
        foreach (var c in cells) { c.enableAutoSizing = false; c.fontSize = low; }
    }
}
