using System.Collections.Generic;
using System.Linq;

// Test oyuncusunun "beklenen sonuç"u. Kural kitabı (51 Okey.pdf) + kullanıcı kararlarından
// (docs/superpowers/specs/2026-11-11-okey-kurallari.md) BAĞIMSIZ yazılır; oyunun OkeyRuleEngine,
// PenaltySystem ya da GameManager fonksiyonlarını çağırmaz, yoksa kod kendisiyle kıyaslanır.
public class EndState
{
    public int WinnerQue; // -1 = beraberlik (deste bitti)
    public Tiles Indicator;
    public Dictionary<int, List<Tiles>> Hands;
    public HashSet<int> Opened,
        OpenedPairs;
    public bool OkeyFinish,
        Kafa;
    public Dictionary<int, int> InHandPenalties;
}

public static class RuleOracle
{
    const int Big = 250;

    static bool IsOkey(Tiles t) => t.type == TileType.Joker;

    static bool IsIndicatorCopy(Tiles t, Tiles ind) =>
        ind != null && t.type == TileType.Number && t.color == ind.color && t.number == ind.number;

    // Okey ya da işlek atmak 250; gösterge kopyası sadece masada çift açılmışsa işlek sayılır; bitiren atış cezasız.
    public static int DiscardPenalty(
        Tiles thrown,
        Tiles indicator,
        bool pairsOpenedOnTable,
        IEnumerable<Tiles> islek,
        bool finishing
    )
    {
        if (finishing)
            return 0;
        if (IsOkey(thrown))
            return Big;
        if (IsIndicatorCopy(thrown, indicator))
            return pairsOpenedOnTable ? Big : 0;
        if (
            thrown.type != TileType.FakeJoker
            && islek.Any(t => !IsOkey(t) && t.color == thrown.color && t.number == thrown.number)
        )
            return Big;
        return 0;
    }

    // Masadaki taşlardan işlek liste (atanın açıp açmamasından bağımsız). rows: (tür, yuva sırasıyla taşlar; boş = null),
    // tür 0 seri (13'lük satır, yuva = sayı-1), 1 sayı grubu (4'lük), 2 çift (2'lik).
    public static List<Tiles> Islek(IEnumerable<(int type, Tiles[] row)> rows)
    {
        var islek = new List<Tiles>();
        void Add(TileColor c, int n) => islek.Add(new Tiles(c, n, TileType.Number));

        foreach (var (type, row) in rows)
        {
            if (type == 0)
            {
                // Boşlukla ayrılan her dizi ayrı seri: iki uç + okeyin tuttuğu sayı (yuvası).
                for (int s = 0; s < row.Length; s++)
                {
                    if (row[s] == null || (s > 0 && row[s - 1] != null))
                        continue;
                    int e = s;
                    while (e + 1 < row.Length && row[e + 1] != null)
                        e++;
                    var real = row.Skip(s).Take(e - s + 1).FirstOrDefault(t => !IsOkey(t));
                    if (real == null)
                        continue;
                    if (s > 0)
                        Add(real.color, s);
                    if (e < 12)
                        Add(real.color, e + 2);
                    for (int i = s; i <= e; i++)
                        if (IsOkey(row[i]))
                            Add(real.color, i + 1);
                }
            }
            else
            {
                var tiles = row.Where(t => t != null).ToList();
                var real = tiles.FirstOrDefault(t => !IsOkey(t));
                if (real == null)
                    continue;
                var colors = tiles.Where(t => !IsOkey(t)).Select(t => t.color).ToList();
                if (type == 1 && tiles.Count >= 3)
                {
                    foreach (TileColor c in System.Enum.GetValues(typeof(TileColor)))
                        if (!colors.Contains(c))
                            Add(c, real.number);
                }
                else if (type == 2 && tiles.Any(IsOkey))
                    Add(real.color, real.number);
            }
        }
        return islek;
    }

    // Başkasının perine işlenen sayının 10 katı (gösterge kopyası 20 katı) per sahibine; kendi perine ceza yok.
    public static int ProcessPenalty(Tiles processed, int representedNumber, Tiles indicator, bool ontoOwnMeld)
    {
        if (ontoOwnMeld)
            return 0;
        if (IsIndicatorCopy(processed, indicator))
            return indicator.number * 20;
        return representedNumber * 10;
    }

    public static int SidePickPenalty(int tileNumber) => tileNumber * 10;

    // Katlamalı: ilk açan en az 51, sonrakiler yeri geçmeli; açmış oyuncuya limit yok.
    public static bool OpenAllowed(int score, int tableLimit, bool anyoneElseOpened, bool iHaveOpened)
    {
        if (iHaveOpened)
            return true;
        return anyoneElseOpened ? score > tableLimit : score >= tableLimit;
    }

    // Sadece oyuncunun ilk açılışı limiti yükseltir.
    public static int NewTableLimit(int tableLimit, int openScore, bool firstOpen) =>
        firstOpen && openScore >= tableLimit ? openScore : tableLimit;

    // Roket (gösterge sahte okey) 8, sarı 6, kırmızı 5, siyah 4, mavi 3.
    public static int ColorMultiplier(Tiles indicator)
    {
        if (indicator.type == TileType.FakeJoker)
            return 8;
        switch (indicator.color)
        {
            case TileColor.yellow:
                return 6;
            case TileColor.red:
                return 5;
            case TileColor.black:
                return 4;
            default:
                return 3;
        }
    }

    // que → el sonucu (el içi cezalar dahil, düşer negatif).
    public static Dictionary<int, int> EndResults(EndState s)
    {
        int color = ColorMultiplier(s.Indicator);
        bool hasWinner = s.WinnerQue != -1;
        int finish = 1;
        if (hasWinner)
        {
            if (s.OpenedPairs.Contains(s.WinnerQue))
                finish *= 2;
            if (s.OkeyFinish)
                finish *= 2;
            if (s.Kafa)
                finish *= 2;
        }

        var result = new Dictionary<int, int>();
        foreach (var kv in s.Hands)
        {
            int que = kv.Key;
            int inHand = s.InHandPenalties.TryGetValue(que, out int p) ? p : 0;
            int end;
            if (hasWinner && que == s.WinnerQue)
                end = -100 * color;
            else if (!s.Opened.Contains(que))
                end = 600 * finish;
            else
            {
                int okeys = kv.Value.Count(IsOkey);
                int normal = kv.Value.Count - okeys;
                int pairs = s.OpenedPairs.Contains(que) ? 2 : 1;
                end = (normal * 10 * color * pairs + okeys * Big) * finish;
            }
            result[que] = inHand + end;
        }
        return result;
    }
}
