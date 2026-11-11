using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Photon.Pun;
using UnityEngine;

// "smart" mod: test oyuncusu. Beyin (TestPlayerBrain) tek adım söyler; burada adım insan kod yolundan uygulanır
// (sürükle-bırak, gerçek butonlar), oyunun tepkisi gözlenir ve RuleOracle'ın beklediğiyle karşılaştırılır.
// Fark = [BUG] (koşu devam eder, sonunda FAIL). Her şey Logs/autotest_<runId>_<nick>.log günlüğüne yazılır.
public partial class SyncAutoTest
{
    const float ObserveTimeout = 3f;
    const int MaxStepsPerTurn = 40;

    TestPlayerBrain brain;
    AutoTestJournal journal;
    int brainDeal = -1;
    bool firstOpenThisTurn;
    int openScoreThisTurn;
    bool processedThisTurn;
    readonly Dictionary<int, int> pendingProcessPenalty = new Dictionary<int, int>();
    string finishKind;

    string Key => DesyncDetector.CurrentKey;
    string Who => $"{Config.nickName} que{MyQue}";

    TileDistrubite Td => FindObjectOfType<TileDistrubite>();
    ScoreManager Sm => FindObjectOfType<ScoreManager>();

    void EnsureSmart()
    {
        if (brain != null)
            return;
        int seed = Config.seed != 0 ? Config.seed : StableHash(Config.runId);
        brain = new TestPlayerBrain(seed * 31 + StableHash(Config.nickName), Config.mistakeRate);
        journal = new AutoTestJournal(Path.Combine(root, "Logs", $"autotest_{Config.runId}_{Config.nickName}.log"));
        status.journal = journal.Path;
        journal.Line(Key, Config.nickName, $"seed {seed}, hata oranı {Config.mistakeRate}");
    }

    // string.GetHashCode süreçler arasında aynı olmak zorunda değil; seed tekrarlanabilir olsun.
    static int StableHash(string s)
    {
        int h = 17;
        foreach (char c in s ?? "")
            h = unchecked(h * 31 + c);
        return h & 0x7fffffff;
    }

    IEnumerator SmartTurn()
    {
        EnsureSmart();
        if (deal != brainDeal)
        {
            brainDeal = deal;
            brain.NewDeal();
        }
        else
            brain.NewTurn();
        firstOpenThisTurn = processedThisTurn = false;
        openScoreThisTurn = 0;
        pendingProcessPenalty.Clear();
        if (brain.TurnMistake != Mistake.None)
            journal.Line(Key, Who, $"bu tur denenecek hata: {brain.TurnMistake}");

        int que = MyQue;
        for (int i = 0; i < MaxStepsPerTurn; i++)
        {
            if (GameManager.Instance.isGameEnded || turnManager.currentTurnPlayer != que || state == State.Error)
                yield break;
            TableView v = BuildTableView();
            PlanStep step = turnManager.canDrop ? brain.NextAction(v) : brain.PlanDraw(v);
            journal.Line(
                Key,
                Who,
                $"el({v.Hand.Count}): {string.Join(" ", v.Hand.Select(TestPlayerBrain.Name))} | karar: {step.Kind} "
                    + $"{TestPlayerBrain.Name(v.Hand.Concat(new[] { v.SideTile }).FirstOrDefault(t => t != null && t.id == step.TileId))} | sebep: {step.Reason}"
                    + (step.Mistake != Mistake.None ? $" | [BİLEREK HATA] {step.Mistake}" : "")
            );
            if (step.Mistake != Mistake.None)
                status.mistakes++;

            yield return StartCoroutine(Execute(step, v));
            WriteStatus();

            if (step.Kind == StepKind.Discard && (turnManager.currentTurnPlayer != que || GameManager.Instance.isGameEnded))
            {
                myMoves++;
                yield break;
            }
            yield return new WaitForSeconds(Config.moveDelaySec);
        }
        if (state != State.Error)
            Fail($"tur {MaxStepsPerTurn} adımda bitmedi");
    }

    // --- Görünüm ---

    TableView BuildTableView()
    {
        var td = Td;
        var sm = Sm;
        int me = MyQue;
        var v = new TableView
        {
            MyQue = me,
            Hand = HandSolver.Copy(td.GetPlayerTiles()),
            Indicator = HandSolver.Copy(new[] { td.GetIndicatorTile() })[0],
            TableLimit = GameManager.Instance.CurrentTableLimit,
            AnyoneElseOpened = sm.playersWhoOpened.Any(q => q != me),
            IOpenedSeries = sm.hasOpenedSeries,
            IOpenedPairs = sm.hasOpenedPairs,
            PairsOpenedOnTable = GameManager.Instance.IsDoubleOpenedOnTable,
            CanDrop = turnManager.canDrop,
            PickedFromSide = turnManager.hasPickedFromSide,
            PileCount = td.allTiles.Count,
        };
        TileUI side = SideTileUI();
        if (side != null && side.tileDataInfo != null)
            v.SideTile = HandSolver.Copy(new[] { side.tileDataInfo })[0];

        foreach (var player in PhotonNetwork.PlayerList)
        {
            var container = GameObject.Find(player.NickName + " meld");
            if (container == null || !player.CustomProperties.TryGetValue("PlayerQue", out object q))
                continue;
            for (int type = 0; type < container.transform.childCount; type++)
            {
                Transform typeContainer = container.transform.GetChild(type);
                foreach (Transform ph in typeContainer)
                {
                    var p = ph.GetComponent<Placeholder>();
                    if (p == null || !p.available || p.AvailableTileInfo == null)
                        continue;
                    int idx = ph.GetSiblingIndex();
                    int rowSize = type == 0 ? 13 : type == 1 ? 4 : 2;
                    int row = idx / rowSize;
                    v.Slots.Add(
                        new SlotView
                        {
                            Key = $"{player.NickName}|{type}|{idx}",
                            OwnerQue = (int)q,
                            MeldType = type,
                            MeldKey = $"{(int)q}:{type}:{row}",
                            HighEnd = type == 0 && IsHighEnd(typeContainer, row, idx % 13),
                            Needed = HandSolver.Copy(new[] { p.AvailableTileInfo })[0],
                        }
                    );
                }
            }
        }
        return v;
    }

    static bool IsHighEnd(Transform seriesContainer, int row, int col)
    {
        for (int c = col + 1; c < 13; c++)
        {
            int i = row * 13 + c;
            if (i < seriesContainer.childCount && seriesContainer.GetChild(i).childCount > 0)
                return false; // sağında taş var: sol uç
        }
        return true;
    }

    TileUI SideTileUI()
    {
        var left = GameObject.FindWithTag("LeftTileContainer");
        if (left == null || left.transform.childCount == 0)
            return null;
        return left.transform.GetChild(left.transform.childCount - 1).GetComponent<TileUI>();
    }

    Transform Rack => GameObject.Find("PlayerTileContainer").transform;

    TileUI HandTileUI(string id) =>
        Rack.GetComponentsInChildren<TileUI>().FirstOrDefault(t => t.tileDataInfo != null && t.tileDataInfo.id == id);

    Transform EmptyRackSlot() =>
        Rack.GetComponentsInChildren<Placeholder>()
            .FirstOrDefault(p => !p.isRight && !p.isMeldArea && p.transform.childCount == 0)
            ?.transform;

    // Masadaki tüm perler satır satır: (tür, yuva sırasıyla taşlar; boş yuva null). Tür 0 seri 13'lük, 1 grup 4'lük, 2 çift 2'lik.
    List<(int type, Tiles[] row)> BoardRows()
    {
        var rows = new List<(int, Tiles[])>();
        foreach (var player in PhotonNetwork.PlayerList)
        {
            var container = GameObject.Find(player.NickName + " meld");
            if (container == null)
                continue;
            for (int type = 0; type < container.transform.childCount && type < 3; type++)
            {
                Transform typeContainer = container.transform.GetChild(type);
                int rowSize = type == 0 ? 13 : type == 1 ? 4 : 2;
                for (int start = 0; start + rowSize <= typeContainer.childCount; start += rowSize)
                {
                    var row = new Tiles[rowSize];
                    for (int i = 0; i < rowSize; i++)
                    {
                        Transform slot = typeContainer.GetChild(start + i);
                        row[i] = slot.childCount > 0 ? slot.GetChild(0).GetComponent<TileUI>()?.tileDataInfo : null;
                    }
                    rows.Add((type, row));
                }
            }
        }
        return rows;
    }

    // Bu ID'li taşın sağındaki ilk boş yuva; yoksa herhangi bir boş yuva.
    Transform EmptyRackSlotAfter(string id)
    {
        TileUI anchor = HandTileUI(id);
        int after = anchor != null ? anchor.transform.parent.GetSiblingIndex() : -1;
        return Rack.GetComponentsInChildren<Placeholder>()
                .FirstOrDefault(p =>
                    !p.isRight && !p.isMeldArea && p.transform.childCount == 0 && p.transform.GetSiblingIndex() > after
                )
                ?.transform
            ?? EmptyRackSlot();
    }

    // --- Gözlem ---

    class Obs
    {
        public Dictionary<int, int> Scores;
        public int Hand;
        public bool OpenedSeries,
            OpenedPairs,
            CanDrop,
            PickedSide;
        public int Limit;
    }

    // Master puan tablosunun sahibi: oyunun bot koltukları dahil herkesi oradan okur (botların PlayerScore özelliği yok).
    Obs Observe() =>
        new Obs
        {
            Scores = PhotonNetwork.IsMasterClient && Sm.playerScores != null
                ? new Dictionary<int, int>(Sm.playerScores)
                : PhotonNetwork.PlayerList.Where(p => p.CustomProperties.ContainsKey("PlayerQue"))
                    .ToDictionary(
                        p => (int)p.CustomProperties["PlayerQue"],
                        p => p.CustomProperties.TryGetValue("PlayerScore", out object s) ? (int)s : 0
                    ),
            Hand = Td.GetPlayerTiles().Count,
            OpenedSeries = Sm.hasOpenedSeries,
            OpenedPairs = Sm.hasOpenedPairs,
            CanDrop = turnManager.canDrop,
            PickedSide = turnManager.hasPickedFromSide,
            Limit = GameManager.Instance.CurrentTableLimit,
        };

    static string Deltas(Dictionary<int, int> d) =>
        d.Count == 0 ? "ceza yok" : string.Join(", ", d.OrderBy(kv => kv.Key).Select(kv => $"que{kv.Key} {kv.Value:+0;-0}"));

    static Dictionary<int, int> Diff(Obs before, Obs after) =>
        after.Scores.Where(kv => kv.Value - (before.Scores.TryGetValue(kv.Key, out int b) ? b : 0) != 0)
            .ToDictionary(kv => kv.Key, kv => kv.Value - (before.Scores.TryGetValue(kv.Key, out int b) ? b : 0));

    // Cezalar master RPC'siyle geç gelir: beklenen gelene ya da süre dolana kadar bekle.
    IEnumerator WaitFor(Func<bool> condition, float timeout = ObserveTimeout)
    {
        for (float t = 0; t < timeout && !condition(); t += Time.unscaledDeltaTime)
            yield return null;
    }

    void Check(string what, string expected, string observed)
    {
        bool ok = expected == observed;
        journal.Check(Key, Who, what, expected, observed, ok);
        if (ok)
            return;
        status.bugs++;
        if (status.errors.Count < MaxErrors)
            status.errors.Add($"[BUG] {Key} {Config.nickName}: {what} beklenen {expected} gözlenen {observed}");
    }

    void DriverNote(string text) => journal.Line(Key, Who, $"SÜRÜCÜ: {text}");

    // --- Adımlar ---

    IEnumerator Execute(PlanStep step, TableView v)
    {
        Obs before = Observe();
        switch (step.Kind)
        {
            case StepKind.DrawMiddle:
            case StepKind.DrawSide:
            {
                bool side = step.Kind == StepKind.DrawSide;
                TileUI tile = side ? SideTileUI() : TopMiddleTile();
                Transform slot = step.TileId != null ? EmptyRackSlotAfter(step.TileId) : EmptyRackSlot();
                if (tile == null || slot == null || !DragTo(tile, slot))
                {
                    Fail($"{step.Kind}: taş={tile != null} slot={slot != null}");
                    yield break;
                }
                yield return StartCoroutine(WaitFor(() => turnManager.canDrop && Td.GetPlayerTiles().Count == before.Hand + 1));
                Obs after = Observe();
                Check(step.Kind.ToString(), $"el {before.Hand + 1}, çekti", $"el {after.Hand}, {(after.CanDrop ? "çekti" : "çekmedi")}");
                if (side)
                    status.sidePicks++;
                break;
            }

            case StepKind.CancelSide:
            {
                FindObjectOfType<SidePickUIManager>().OnCancelButtonClicked();
                yield return StartCoroutine(WaitFor(() => !turnManager.hasPickedFromSide && !turnManager.canDrop));
                yield return new WaitForSeconds(0.5f);
                Obs after = Observe();
                Check(
                    "geri bırak",
                    $"el {before.Hand - 1}, ceza yok",
                    $"el {after.Hand}, {Deltas(Diff(before, after))}"
                );
                break;
            }

            case StepKind.Arrange:
                yield return StartCoroutine(Arrange(step.Layout));
                break;

            case StepKind.OpenSeries:
            case StepKind.OpenPairs:
            {
                bool pairs = step.Kind == StepKind.OpenPairs;
                MeldSet best = pairs ? HandSolver.BestPairs(v.Hand, v.Indicator) : HandSolver.BestSeries(v.Hand, v.Indicator);
                bool iOpened = v.IOpenedSeries || v.IOpenedPairs;
                bool allowed =
                    best.Melds.Count > 0
                    && v.Hand.Count - best.Melds.Sum(m => m.Count) >= 1
                    && RuleOracle.OpenAllowed(best.Score, v.TableLimit, v.AnyoneElseOpened, iOpened);
                var button = FindObjectOfType<ExitButton>();
                if (pairs)
                    button.MeldPairTileButton();
                else
                    button.MeldTileButton();
                yield return StartCoroutine(WaitFor(() => allowed ? Td.GetPlayerTiles().Count < before.Hand : false, allowed ? ObserveTimeout : 1f));
                Obs after = Observe();
                bool opened = after.Hand < before.Hand;
                Check(
                    $"{step.Kind} ({best.Score}, limit {v.TableLimit})",
                    allowed ? "açıldı" : "açılmadı",
                    opened ? "açıldı" : "açılmadı"
                );
                if (opened)
                {
                    status.opens++;
                    if (!iOpened)
                    {
                        firstOpenThisTurn = true;
                        openScoreThisTurn = best.Score;
                    }
                }
                break;
            }

            case StepKind.Process:
            {
                TileUI tile = HandTileUI(step.TileId);
                Transform slot = SlotByKey(step.SlotKey);
                SlotView sv = v.Slots.First(s => s.Key == step.SlotKey);
                if (tile == null || slot == null)
                {
                    Fail($"işleme: taş={tile != null} slot={slot != null}");
                    yield break;
                }
                bool expectAccept = step.Mistake == Mistake.None;
                string tileId = tile.tileDataInfo.id;
                DragTo(tile, slot);
                // Okey değişiminde el sayısı aynı kalır (taş gider, okey gelir); ölçü taşın elden çıkması.
                System.Func<bool> gone = () => !Td.GetPlayerTiles().Any(t => t.id == tileId);
                yield return StartCoroutine(WaitFor(gone, expectAccept ? ObserveTimeout : 1f));
                bool accepted = gone();
                Check($"işle {TestPlayerBrain.Name(tile.tileDataInfo)} -> que{sv.OwnerQue}", expectAccept ? "işlendi" : "reddedildi", accepted ? "işlendi" : "reddedildi");
                if (accepted)
                {
                    status.processes++;
                    processedThisTurn = true;
                    int pen = RuleOracle.ProcessPenalty(tile.tileDataInfo, sv.Needed.number, v.Indicator, sv.OwnerQue == v.MyQue);
                    if (pen > 0)
                        pendingProcessPenalty[sv.OwnerQue] = (pendingProcessPenalty.TryGetValue(sv.OwnerQue, out int p) ? p : 0) + pen;
                }
                break;
            }

            case StepKind.Discard:
                yield return StartCoroutine(Discard(step, v, before));
                break;

            default:
                Fail($"desteklenmeyen adım {step.Kind}");
                break;
        }
    }

    TileUI TopMiddleTile()
    {
        Transform middle = GameObject.Find("MiddleTileContainer").transform;
        return middle.childCount > 0 ? middle.GetChild(middle.childCount - 1).GetComponent<TileUI>() : null;
    }

    Transform SlotByKey(string key)
    {
        var parts = key.Split('|');
        var container = GameObject.Find(parts[0] + " meld");
        return container?.transform.GetChild(int.Parse(parts[1])).GetChild(int.Parse(parts[2]));
    }

    // Hedef slotlar boşaldıkça taşları yerine sürükle; döngüde kalırsa birini boş bir slota park et.
    IEnumerator Arrange(Dictionary<string, int> layout)
    {
        Transform rack = Rack;
        var targets = new HashSet<int>(layout.Values);
        for (int pass = 0; pass < 30; pass++)
        {
            var pending = layout.Where(kv => HandSlotOf(kv.Key) != kv.Value).ToList();
            if (pending.Count == 0)
                break;
            bool moved = false;
            foreach (var kv in pending)
            {
                Transform target = rack.GetChild(kv.Value);
                TileUI tile = HandTileUI(kv.Key);
                if (tile == null || target.childCount > 0)
                    continue;
                DragTo(tile, target);
                moved = true;
                yield return new WaitForSeconds(0.35f); // SmoothMove 0.3 sn sonra parent'ı değiştirir
            }
            if (!moved)
            {
                var stuck = pending.Select(kv => HandTileUI(kv.Key)).FirstOrDefault(t => t != null);
                Transform park = Enumerable.Range(0, 2 * RackLayout.RowSize)
                    .Select(rack.GetChild)
                    .FirstOrDefault(s => s.childCount == 0 && !targets.Contains(s.GetSiblingIndex()));
                if (stuck == null || park == null)
                    break;
                DragTo(stuck, park);
                yield return new WaitForSeconds(0.35f);
            }
        }
        yield return new WaitForSeconds(0.3f);
        int wrong = layout.Count(kv => HandSlotOf(kv.Key) != kv.Value);
        if (wrong > 0)
            DriverNote($"dizilim tamamlanamadı, {wrong} taş yerinde değil");
        else
            journal.Line(Key, Who, $"dizildi: seri {Sm.totalScore}, çift {Sm.pairTotalScore}");
    }

    int HandSlotOf(string id)
    {
        var tile = HandTileUI(id);
        return tile != null ? tile.transform.parent.GetSiblingIndex() : -1;
    }

    IEnumerator Discard(PlanStep step, TableView v, Obs before)
    {
        TileUI tile = HandTileUI(step.TileId);
        if (tile == null)
        {
            Fail($"atılacak taş ıstakada yok: {step.TileId}");
            yield break;
        }
        Tiles thrown = HandSolver.Copy(new[] { tile.tileDataInfo })[0];
        int que = v.MyQue;
        var expected = new Dictionary<int, int>();
        void Add(int q, int amount)
        {
            if (amount != 0)
                expected[q] = (expected.TryGetValue(q, out int e) ? e : 0) + amount;
        }

        bool sideIncomplete = v.PickedFromSide && !firstOpenThisTurn && !v.IOpenedSeries && !v.IOpenedPairs && !processedThisTurn;
        bool finishing = v.Hand.Count == 1 && !sideIncomplete;
        if (sideIncomplete)
            Add(que, 250); // yandan alıp açmadan atma: tek ceza, taş yana döner, ortadan 1 taş
        else
        {
            // Yanan yuvalar (v.Slots) sadece açmış oyuncuda var; işlek masadaki taşlardan, herkes için aynı.
            var islek = RuleOracle.Islek(BoardRows());
            Add(que, RuleOracle.DiscardPenalty(thrown, v.Indicator, v.PairsOpenedOnTable, islek, finishing));
            foreach (var kv in pendingProcessPenalty)
                Add(kv.Key, kv.Value);
            if (v.PickedFromSide && (firstOpenThisTurn || processedThisTurn || v.IOpenedSeries || v.IOpenedPairs))
                Add(que == 1 ? 4 : que - 1, RuleOracle.SidePickPenalty(TurnManagerSideNumber()));
        }
        if (Config.injectOracleBugAtMove > 0 && myMoves + 1 == Config.injectOracleBugAtMove)
        {
            Add(que, 1);
            journal.Line(Key, Who, "ÖZ-TEST: beklenen ceza bilerek +1 bozuldu");
        }

        DragTo(tile, GameObject.Find("RightTileContainer").transform);
        if (sideIncomplete)
            yield return StartCoroutine(WaitFor(() => !turnManager.hasPickedFromSide));
        else
            yield return StartCoroutine(WaitFor(() => turnManager.currentTurnPlayer != que || GameManager.Instance.isGameEnded, 5f));
        yield return StartCoroutine(WaitFor(() => ScoresMatch(before, expected)));
        Obs after = Observe();
        Check($"at {TestPlayerBrain.Name(thrown)}{(finishing ? " (bitiş)" : "")}", Deltas(expected), Deltas(Diff(before, after)));

        if (!sideIncomplete && firstOpenThisTurn)
        {
            int expectedLimit = RuleOracle.NewTableLimit(v.TableLimit, openScoreThisTurn, true);
            yield return StartCoroutine(WaitFor(() => GameManager.Instance.CurrentTableLimit == expectedLimit, 2f));
            Check("masa limiti", expectedLimit.ToString(), GameManager.Instance.CurrentTableLimit.ToString());
        }
        if (finishing)
        {
            finishKind =
                thrown.type == TileType.Joker ? "okey"
                : firstOpenThisTurn && !v.AnyoneElseOpened ? "kafa"
                : v.IOpenedPairs ? "çift"
                : "normal";
            status.finishType = finishKind;
            lastFinish = new FinishInfo
            {
                OkeyFinish = thrown.type == TileType.Joker,
                Kafa = firstOpenThisTurn && !v.AnyoneElseOpened,
            };
        }
    }

    int TurnManagerSideNumber() =>
        GameManager.Instance.CurrentSidePickTile != null ? GameManager.Instance.CurrentSidePickTile.number : 0;

    bool ScoresMatch(Obs before, Dictionary<int, int> expected) => Deltas(Diff(before, Observe())) == Deltas(expected);

    // --- El sonu ---

    class FinishInfo
    {
        public bool OkeyFinish,
            Kafa;
    }

    FinishInfo lastFinish;

    // Okey ve kafa bilgisi sadece bitirende var: el sonunu bitiren kontrol eder (beraberlikte host).
    void OnHandResult(int[] actors, int[] rewards, int[] penalties, int[] netScores)
    {
        if (!Config.IsSmart || journal == null)
            return;
        bool iFinished = lastFinish != null;
        bool draw = rewards.All(r => r >= 0);
        if (!iFinished && !(draw && Config.IsHost))
            return;

        var td = Td;
        var sm = Sm;
        var endState = new EndState
        {
            WinnerQue = iFinished ? MyQue : -1,
            Indicator = td.GetIndicatorTile(),
            Hands = Enumerable.Range(1, 4).ToDictionary(q => q, q => HandSolver.Copy(td.GetPlayerTilesForBot(q))),
            Opened = new HashSet<int>(sm.playersWhoOpened),
            OpenedPairs = new HashSet<int>(sm.playersWhoOpenedPairs),
            OkeyFinish = iFinished && lastFinish.OkeyFinish,
            Kafa = iFinished && lastFinish.Kafa,
            InHandPenalties = Observe().Scores,
        };
        var expected = RuleOracle.EndResults(endState);
        for (int i = 0; i < actors.Length; i++)
        {
            var p = PhotonNetwork.CurrentRoom.GetPlayer(actors[i]);
            if (p == null || !p.CustomProperties.TryGetValue("PlayerQue", out object q))
                continue;
            int que = (int)q;
            Check($"el sonu que{que}", expected[que].ToString(), netScores[i].ToString());
        }
        lastFinish = null;
        WriteStatus();
    }
}
