using System;
using System.Collections.Generic;
using System.Linq;

// Test oyuncusunun kararları. Saf C#: sürücü (SyncAutoTest) sahneden TableView kurar, her seferinde tek adım ister,
// adımı insan kod yolundan uygular. Hata modu tur başında seed'li zarla seçilir, uygun an gelince bir kez uygulanır.
public class TableView
{
    public int MyQue;
    public List<Tiles> Hand;
    public Tiles Indicator;
    public Tiles SideTile;
    public List<SlotView> Slots = new List<SlotView>();
    public int TableLimit;
    public bool AnyoneElseOpened,
        IOpenedSeries,
        IOpenedPairs,
        PairsOpenedOnTable,
        CanDrop,
        PickedFromSide;
    public int PileCount;
}

public class SlotView
{
    public string Key; // sürücünün slotu bulduğu anahtar
    public int OwnerQue;
    public int MeldType; // ScoreManager.MeldType: 0 seri, 1 grup, 2 çift
    public string MeldKey; // aynı per (sahip + tip + satır)
    public bool HighEnd; // seride büyük uç mu
    public Tiles Needed;
    public bool JokerSwap;
}

public enum StepKind
{
    DrawMiddle,
    DrawSide,
    CancelSide,
    Arrange,
    OpenSeries,
    OpenPairs,
    Process,
    AutoProcess,
    Discard,
}

public enum Mistake
{
    None,
    SidePickNoOpen,
    SidePickCancel,
    DiscardOkey,
    DiscardIndicator,
    DiscardIslek,
    OpenBelowLimit,
    ProcessBeforeOpen,
    ThirdOnSeriesEnd,
}

public class PlanStep
{
    public StepKind Kind;
    public string TileId;
    public string SlotKey;
    public Dictionary<string, int> Layout;
    public string Reason;
    public Mistake Mistake;
}

public class TestPlayerBrain
{
    readonly Random rng;
    readonly double mistakeRate;
    bool? pathPairs;
    bool arrangedThisTurn,
        mistakeUsed,
        sideCancelled,
        sideForMistake;
    readonly Dictionary<string, int> processedAtEnd = new Dictionary<string, int>();

    public TestPlayerBrain(int seed, double mistakeRate)
    {
        rng = new Random(seed);
        this.mistakeRate = mistakeRate;
    }

    public bool PathIsPairs => pathPairs == true;
    public Mistake TurnMistake { get; private set; }

    public void NewDeal()
    {
        pathPairs = null;
        NewTurn();
    }

    public void NewTurn()
    {
        arrangedThisTurn = mistakeUsed = sideCancelled = sideForMistake = false;
        processedAtEnd.Clear();
        TurnMistake = Mistake.None;
        if (mistakeRate > 0 && rng.NextDouble() < mistakeRate)
        {
            var kinds = Enum.GetValues(typeof(Mistake)).Cast<Mistake>().Where(m => m != Mistake.None).ToList();
            TurnMistake = kinds[rng.Next(kinds.Count)];
        }
    }

    bool Opened(TableView v) => v.IOpenedSeries || v.IOpenedPairs;

    static bool IsOkey(Tiles t) => t.type == TileType.Joker;

    static bool IsIndicatorCopy(Tiles t, Tiles ind) =>
        ind != null && t.type == TileType.Number && t.color == ind.color && t.number == ind.number;

    // Yol: açtıysa açtığı; değilse ilk karar anında yüksek puanlı (el boyunca sabit).
    bool ChoosePath(TableView v, IList<Tiles> hand)
    {
        if (v.IOpenedPairs && !v.IOpenedSeries)
            pathPairs = true;
        else if (v.IOpenedSeries)
            pathPairs = false;
        else if (pathPairs == null)
            pathPairs = HandSolver.BestPairs(hand, v.Indicator).Score > HandSolver.BestSeries(hand, v.Indicator).Score;
        return pathPairs.Value;
    }

    MeldSet Best(TableView v, IList<Tiles> hand) =>
        ChoosePath(v, hand) ? HandSolver.BestPairs(hand, v.Indicator) : HandSolver.BestSeries(hand, v.Indicator);

    bool CanOpen(TableView v, IList<Tiles> hand, MeldSet best)
    {
        if (best.Melds.Count == 0)
            return false;
        int meldTiles = best.Melds.Sum(m => m.Count);
        return hand.Count - meldTiles >= 1
            && RuleOracle.OpenAllowed(best.Score, v.TableLimit, v.AnyoneElseOpened, Opened(v));
    }

    SlotView MatchingSlot(TableView v, Tiles tile, bool respectEndLimit) =>
        v.Slots.FirstOrDefault(s =>
            s.Needed != null
            && s.Needed.color == tile.color
            && s.Needed.number == tile.number
            && (!respectEndLimit || s.MeldType != 0 || EndCount(s) < 2)
        );

    int EndCount(SlotView s) => processedAtEnd.TryGetValue(s.MeldKey + (s.HighEnd ? "h" : "l"), out int c) ? c : 0;

    static PlanStep Step(StepKind kind, string reason, Mistake mistake = Mistake.None, Tiles tile = null) =>
        new PlanStep
        {
            Kind = kind,
            TileId = tile?.id,
            Reason = reason,
            Mistake = mistake,
        };

    bool TakeMistake(Mistake m)
    {
        if (mistakeUsed || TurnMistake != m)
            return false;
        mistakeUsed = true;
        return true;
    }

    // Çekme kararı; el zaten 15 ise (canDrop) null.
    public PlanStep PlanDraw(TableView v)
    {
        if (v.CanDrop)
            return null;
        if (v.SideTile != null && !sideCancelled)
        {
            if (TakeMistake(Mistake.SidePickNoOpen) || TakeMistake(Mistake.SidePickCancel))
            {
                sideForMistake = true;
                return Step(StepKind.DrawSide, $"yandan {Name(v.SideTile)} alıyorum, açmayacağım", TurnMistake);
            }
            var withSide = v.Hand.Concat(new[] { v.SideTile }).ToList();
            if (!Opened(v) && CanOpen(v, withSide, Best(v, withSide)))
                return Step(StepKind.DrawSide, $"yandan {Name(v.SideTile)}: {PathName()} {Best(v, withSide).Score} ile açabilirim");
            if (Opened(v) && !IsOkey(v.SideTile) && MatchingSlot(v, v.SideTile, true) != null)
                return Step(StepKind.DrawSide, $"yandan {Name(v.SideTile)}: işleyebilirim");
        }
        return Step(StepKind.DrawMiddle, sideCancelled ? "yandakini bıraktım, ortadan çekiyorum" : "ortadan çekiyorum");
    }

    // Çektikten sonraki tek adım: diz → aç → işle → at.
    public PlanStep NextAction(TableView v)
    {
        var hand = v.Hand;

        if (v.PickedFromSide && sideForMistake)
        {
            sideForMistake = false;
            if (TurnMistake == Mistake.SidePickCancel)
            {
                sideCancelled = true;
                return Step(StepKind.CancelSide, "geri bırak", Mistake.SidePickCancel);
            }
            var any = hand.FirstOrDefault(t => !IsOkey(t)) ?? hand[0];
            return Step(StepKind.Discard, "yandan aldım, açmadan atmaya çalışıyorum", Mistake.SidePickNoOpen, any);
        }

        var best = Best(v, hand);
        bool canOpen = CanOpen(v, hand, best);

        if (canOpen && !arrangedThisTurn)
        {
            arrangedThisTurn = true;
            var inMelds = new HashSet<string>(best.Melds.SelectMany(m => m).Select(t => t.id));
            var layout = RackLayout.Place(best.Melds, hand.Where(t => !inMelds.Contains(t.id)).ToList());
            if (layout != null)
                return new PlanStep
                {
                    Kind = StepKind.Arrange,
                    Layout = layout,
                    Reason = $"{PathName()} {best.Score}: {string.Join(" | ", best.Melds.Select(m => string.Join(" ", m.Select(Name))))}",
                };
        }
        if (canOpen)
            return Step(
                PathIsPairs ? StepKind.OpenPairs : StepKind.OpenSeries,
                $"{PathName()} {best.Score} ile açıyorum (limit {v.TableLimit})"
            );
        if (!Opened(v) && best.Melds.Count > 0 && TakeMistake(Mistake.OpenBelowLimit))
            return Step(
                PathIsPairs ? StepKind.OpenPairs : StepKind.OpenSeries,
                $"limit altında açmayı deniyorum ({best.Score})",
                Mistake.OpenBelowLimit
            );

        if (hand.Count > 1)
        {
            foreach (var tile in hand.Where(t => !IsOkey(t)))
            {
                var exact = MatchingSlot(v, tile, true);
                if (exact != null && Opened(v))
                    return Process(exact, tile, $"{Name(tile)} işlek, işliyorum", Mistake.None);
                if (exact != null && !Opened(v) && TakeMistake(Mistake.ProcessBeforeOpen))
                    return Process(exact, tile, $"açmadan {Name(tile)} işlemeyi deniyorum", Mistake.ProcessBeforeOpen);
                var blocked = MatchingSlot(v, tile, false);
                if (blocked != null && Opened(v) && blocked.MeldType == 0 && TakeMistake(Mistake.ThirdOnSeriesEnd))
                    return Process(blocked, tile, $"aynı uca 3. taşı ({Name(tile)}) deniyorum", Mistake.ThirdOnSeriesEnd);
            }
        }

        if (v.PickedFromSide && !v.IOpenedSeries && !v.IOpenedPairs)
        {
            sideCancelled = true;
            return Step(StepKind.CancelSide, "yandan aldım ama açamadım, geri bırakıyorum");
        }

        return Discard(v, best);
    }

    PlanStep Process(SlotView slot, Tiles tile, string reason, Mistake mistake)
    {
        if (slot.MeldType == 0 && mistake != Mistake.ThirdOnSeriesEnd)
        {
            string k = slot.MeldKey + (slot.HighEnd ? "h" : "l");
            processedAtEnd[k] = EndCount(slot) + 1;
        }
        var step = Step(StepKind.Process, reason, mistake, tile);
        step.SlotKey = slot.Key;
        return step;
    }

    PlanStep Discard(TableView v, MeldSet best)
    {
        var hand = v.Hand;
        if (hand.Count == 1)
            return Step(StepKind.Discard, $"son taş {Name(hand[0])}, bitiriyorum", Mistake.None, hand[0]);

        bool islek(Tiles t) => !IsOkey(t) && MatchingSlot(v, t, false) != null;
        Tiles okey = hand.FirstOrDefault(IsOkey);
        Tiles indicatorCopy = hand.FirstOrDefault(t => IsIndicatorCopy(t, v.Indicator));
        Tiles islekTile = hand.FirstOrDefault(islek);
        if (okey != null && TakeMistake(Mistake.DiscardOkey))
            return Step(StepKind.Discard, "okeyi atıyorum", Mistake.DiscardOkey, okey);
        if (indicatorCopy != null && TakeMistake(Mistake.DiscardIndicator))
            return Step(StepKind.Discard, "gösterge kopyasını atıyorum", Mistake.DiscardIndicator, indicatorCopy);
        if (islekTile != null && TakeMistake(Mistake.DiscardIslek))
            return Step(StepKind.Discard, $"işlek {Name(islekTile)} atıyorum", Mistake.DiscardIslek, islekTile);

        var keep = new HashSet<string>(best.Melds.SelectMany(m => m).Select(t => t.id));
        var safe = hand.Where(t => !IsOkey(t) && !IsIndicatorCopy(t, v.Indicator) && !islek(t)).ToList();
        var pick =
            safe.Where(t => !keep.Contains(t.id)).OrderByDescending(t => t.number).FirstOrDefault()
            ?? safe.OrderByDescending(t => t.number).FirstOrDefault()
            ?? hand.Where(t => !IsOkey(t)).OrderByDescending(t => t.number).FirstOrDefault()
            ?? hand[0];
        return Step(StepKind.Discard, $"{Name(pick)} atıyorum (perde değil, işlek değil)", Mistake.None, pick);
    }

    string PathName() => PathIsPairs ? "çift" : "seri";

    public static string Name(Tiles t)
    {
        if (t == null)
            return "-";
        if (t.type == TileType.Joker)
            return "OKEY";
        return $"{"rbky"[(int)t.color]}{t.number}{(t.type == TileType.FakeJoker ? "*" : "")}";
    }
}
