using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class RejoinTests
{
    [Test] public void Game_Scripts_Send_No_Buffered_Rpc() {
        // Yol Editor'ün çalışma klasörüne (proje kökü) göre
        var hits = Directory.GetFiles("Assets/Osman/Scripts", "*.cs", SearchOption.AllDirectories)
            .SelectMany(f => File.ReadAllLines(f).Select((l, i) => (f, i, l)))
            .Where(x => Regex.IsMatch(x.l, @"RpcTarget\.(AllBuffered|OthersBuffered|AllBufferedViaServer)"))
            .Select(x => $"{x.f}:{x.i + 1}").ToList();
        CollectionAssert.IsEmpty(hits, string.Join("\n", hits));
    }
    [Test] public void Record_Key_Is_Per_Instance() {
        Assert.AreEqual("Okey.Rejoin.device", RejoinRecord.KeyFor("device"));
        Assert.AreNotEqual(RejoinRecord.KeyFor("51Okey"), RejoinRecord.KeyFor("51Okey_clone_0"));
    }
    [Test] public void Identity_Reconnect_Only_When_Auth_Known_And_Different() {
        Assert.IsFalse(PhotonIdentity.NeedsReconnect("a", "a"));
        Assert.IsTrue(PhotonIdentity.NeedsReconnect("x", "a"));
        Assert.IsTrue(PhotonIdentity.NeedsReconnect(null, "a"));
        Assert.IsFalse(PhotonIdentity.NeedsReconnect("x", null));
    }
    [Test] public void Ctl_Bits() {
        Assert.IsTrue(CtlBits.Has(0b0101, 1)); Assert.IsFalse(CtlBits.Has(0b0101, 2)); Assert.IsTrue(CtlBits.Has(0b0101, 3));
        Assert.IsFalse(CtlBits.Has(5, 0)); Assert.IsFalse(CtlBits.Has(5, 5));
        Assert.AreEqual(8, CtlBits.Set(0, 4, true)); Assert.AreEqual(13, CtlBits.Set(15, 2, false)); Assert.AreEqual(5, CtlBits.Set(5, 9, true));
        Assert.AreEqual(5, CtlBits.Of(new[] { 1, 3 })); Assert.AreEqual(0, CtlBits.Of(new int[0]));
        CollectionAssert.AreEqual(new[] { false, true, false, true }, CtlBits.BotNow(5));
    }
    [Test] public void Ctl_Read() {
        Assert.AreEqual(0, CtlBits.Read(new Hashtable()));
        Assert.AreEqual(6, CtlBits.Read(new Hashtable { { "Ctl", 6 } }));
        Assert.AreEqual(0, CtlBits.Read(new Hashtable { { "Ctl", "x" } }));
    }
    [Test] public void TurnGate_Requires_Live_Control_And_Turn() {
        Assert.IsTrue(TurnGate.MayAct(true, true, true));
        Assert.IsFalse(TurnGate.MayAct(false, true, true));
        Assert.IsFalse(TurnGate.MayAct(true, false, true));
        Assert.IsFalse(TurnGate.MayAct(true, true, false));
    }
    [Test] public void Ctl_Start_Only_Active_Players() {
        Assert.AreEqual(0b1011, CtlBits.OfActive(new[] { (1, false), (2, false), (3, true), (4, false) }));
    }
    [Test] public void Bot_Cancel_Only_Before_Draw() {
        Assert.IsTrue(TurnGate.MayCancelBot(false));
        Assert.IsFalse(TurnGate.MayCancelBot(true));
    }
    [Test] public void Match_Over_Only_After_Last_Hand() {
        Assert.IsFalse(MatchState.IsOver(1, 2, true));
        Assert.IsTrue(MatchState.IsOver(2, 2, true));
        Assert.IsFalse(MatchState.IsOver(2, 2, false));
    }
    [Test] public void Names_From_Ctl() {
        CollectionAssert.AreEqual(new[] { "A", "Bot 1", "C (bot)", "D" },
            SeatNaming.Names(new[] { "A", null, "C", "D" }, CtlBits.BotNow(0b1001)));
    }
    [Test] public void Serial_Accepts_Only_Newer() {
        Assert.IsTrue(TurnSerialRule.Accept(0, 1)); Assert.IsFalse(TurnSerialRule.Accept(3, 3));
        Assert.IsFalse(TurnSerialRule.Accept(3, 2)); Assert.IsTrue(TurnSerialRule.Accept(3, 5));
    }
    [Test] public void Serial_Double_Send_For_Same_Turn_End_Is_Dropped() {
        int current = 3, captured = current + 1;       // tur sonu başlarken alınan seri
        Assert.IsTrue(TurnSerialRule.Accept(current, captured));
        current = captured;                            // ilk gönderim yerelde işlendi
        Assert.IsFalse(TurnSerialRule.Accept(current, captured)); // ikinci gönderim aynı seriyi taşır
    }
    [Test] public void Bot_End_Decision_Ignores_Latency() {
        Assert.IsTrue(BotEndRule.PileEmpty(1, true));
        Assert.IsFalse(BotEndRule.PileEmpty(1, false));
        Assert.IsTrue(BotEndRule.PileEmpty(0, false));
    }
    [Test] public void Finish_Snapshot_Drops_Board_Only() {
        Assert.AreEqual("turn=3\nhand1(2)=a b", DesyncDetector.StripBoard("turn=3\nhand1(2)=a b\nboard#1=x@0.1\nboard#2="));
    }
    static Tiles T(string id, int n = 5) => new Tiles { id = id, color = TileColor.red, number = n, type = TileType.Number };
    [Test] public void Rollback_Decide() {
        Assert.AreEqual(RollbackKind.CloseTurn, TurnRollback.Decide(3, 4));
        Assert.AreEqual(RollbackKind.Rollback, TurnRollback.Decide(3, 3));
        Assert.AreEqual(RollbackKind.Rollback, TurnRollback.Decide(0, 0));
    }
    [Test] public void Hand_After_Rollback_Is_Start_Plus_Drawn() {
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, TurnRollback.HandAfterRollback(new[] { T("a"), T("b") }, new[] { T("c") }).Select(t => t.id));
        CollectionAssert.AreEqual(new[] { "a", "b" }, TurnRollback.HandAfterRollback(new[] { T("a"), T("b") }, new Tiles[0]).Select(t => t.id));
        CollectionAssert.AreEqual(new[] { "a", "b" }, TurnRollback.HandAfterRollback(new[] { T("a"), T("b") }, new[] { T("b") }).Select(t => t.id));
    }
    [Test] public void Side_Taken_Tile_Is_Not_Kept() {
        var r = new TurnStartRecord { hand = new List<Tiles> { T("a"), T("b") }, sideTaken = T("x") };
        var hand = TurnRollback.HandAfterRollback(r.hand, r.drawn);
        Assert.IsFalse(hand.Any(t => t.id == "x")); Assert.AreEqual(2, hand.Count);
    }
    [Test] public void Bot_Draw_Rule() {
        Assert.IsFalse(BotDrawRule.ShouldDraw(12, true));
        Assert.IsTrue(BotDrawRule.ShouldDraw(14, false));
        Assert.IsFalse(BotDrawRule.ShouldDraw(15, false));
    }
    [Test] public void Limits_Refresh_Only_Before_Owner_Acts() {
        Assert.IsTrue(TurnRollback.MayRefreshLimits(0, false, 14, 14));
        Assert.IsFalse(TurnRollback.MayRefreshLimits(1, false, 15, 14));
        Assert.IsFalse(TurnRollback.MayRefreshLimits(0, true, 15, 14));
        Assert.IsFalse(TurnRollback.MayRefreshLimits(0, false, 11, 14)); // açış: eller azaldı
    }
    [Test] public void Swap_Added_Is_Tile_Not_Start_Drawn_Or_Side() {
        var start = new[] { T("a") }; var drawn = new[] { T("b") };
        Assert.IsTrue(TurnRollback.IsSwapAdded(start, drawn, T("s"), T("okey")));
        Assert.IsFalse(TurnRollback.IsSwapAdded(start, drawn, T("s"), T("a")));
        Assert.IsFalse(TurnRollback.IsSwapAdded(start, drawn, T("s"), T("b")));
        Assert.IsFalse(TurnRollback.IsSwapAdded(start, drawn, T("s"), T("s")));
    }
    [Test] public void Close_Turn_Finishes_Hand_On_Empty_Pile() {
        Assert.IsTrue(BotEndRule.PileEmpty(0, false));
        Assert.IsFalse(BotEndRule.PileEmpty(1, false));
    }
    [Test] public void Queue_First_To_Replay() {
        Assert.AreEqual(2, RestoreQueue.FirstToReplay(new[] { -1, 5, -1, 6, -1 }, 5));
        Assert.AreEqual(4, RestoreQueue.FirstToReplay(new[] { -1, 5, -1, 6 }, 6));
        Assert.AreEqual(2, RestoreQueue.FirstToReplay(new[] { -1, -1 }, 5)); // K yok: hepsi atılır
        Assert.AreEqual(0, RestoreQueue.FirstToReplay(new int[0], 5));
    }
    [Test] public void Gate_Replays_Only_After_Snapshot_Serial() {
        var log = new List<string>(); RestoreGate.Enter();
        Assert.IsTrue(RestoreGate.Defer(() => log.Add("draw6")));
        Assert.IsTrue(RestoreGate.Defer(() => log.Add("nt7"), 7));
        Assert.IsTrue(RestoreGate.Defer(() => log.Add("draw7")));
        CollectionAssert.IsEmpty(log);
        Assert.AreEqual(1, RestoreGate.GoLive(7));
        CollectionAssert.AreEqual(new[] { "draw7" }, log); Assert.IsTrue(RestoreGate.Live);
        Assert.IsFalse(RestoreGate.Defer(() => log.Add("x"))); RestoreGate.Reset();
    }
    [Test] public void Gate_New_Hand_Drops_Queue() {
        var log = new List<string>(); RestoreGate.Enter(); RestoreGate.Defer(() => log.Add("old"));
        RestoreGate.GoLiveFromNewHand(); CollectionAssert.IsEmpty(log); Assert.IsTrue(RestoreGate.Live); RestoreGate.Reset();
    }
    [Test] public void Gate_Drops_Whole_Queue_Without_Snapshot_Serial() {
        var log = new List<string>(); RestoreGate.Enter();
        RestoreGate.Defer(() => log.Add("draw6")); RestoreGate.Defer(() => log.Add("nt6"), 6);
        Assert.AreEqual(0, RestoreGate.GoLive(7)); CollectionAssert.IsEmpty(log); RestoreGate.Reset();
    }
    [Test] public void Policy_Clears_Record_Only_On_Room_Or_Actor_Gone() {
        Assert.AreEqual(RejoinStep.GiveUpClear, RejoinPolicy.OnFailure(32758, 1f));
        Assert.AreEqual(RejoinStep.GiveUpClear, RejoinPolicy.OnFailure(32748, 1f));
        Assert.AreEqual(RejoinStep.GiveUpClear, RejoinPolicy.OnFailure(-3, 1f));
        Assert.AreEqual(RejoinStep.Retry, RejoinPolicy.OnFailure(32746, 5f));
        Assert.AreEqual(RejoinStep.GiveUpKeep, RejoinPolicy.OnFailure(32746, 31f));
        Assert.AreEqual(RejoinStep.Retry, RejoinPolicy.OnFailure(32765, 5f));
        Assert.AreEqual(RejoinStep.GiveUpKeep, RejoinPolicy.OnFailure(0, 30f));
    }
    [Test] public void Policy_Reconnect_Only_For_Unexpected_Drop_In_Game() {
        Assert.IsTrue(RejoinPolicy.ShouldReconnect(false, true, true));
        Assert.IsFalse(RejoinPolicy.ShouldReconnect(true, true, true));
        Assert.IsFalse(RejoinPolicy.ShouldReconnect(false, false, true));
        Assert.IsFalse(RejoinPolicy.ShouldReconnect(false, true, false));
    }
    [Test] public void Decide_Cases() {
        Assert.AreEqual(HandshakeDecision.Grant, HandshakeRule.Decide(0xAB, 0xAB, true, false, false));
        Assert.AreEqual(HandshakeDecision.Wait, HandshakeRule.Decide(0xAB, 0xAB, true, true, false));
        Assert.AreEqual(HandshakeDecision.Wait, HandshakeRule.Decide(0xAB, 0xAB, true, false, true));
        Assert.AreEqual(HandshakeDecision.Again, HandshakeRule.Decide(0xAB, 0xAC, true, false, false));
        Assert.AreEqual(HandshakeDecision.Wait, HandshakeRule.Decide(null, 0xAB, true, false, false)); // özet yok: bekle
        Assert.AreEqual(HandshakeDecision.Again, HandshakeRule.Decide(0xAB, 0xAB, false, false, false));
    }
    [Test] public void Overlay_After_Five_Agains() {
        Assert.AreEqual("rejoin.joining_game", HandshakeRule.OverlayKey(4)); Assert.AreEqual("rejoin.retrying", HandshakeRule.OverlayKey(5));
    }
    [Test] public void Book_Grants_Once_And_Forgets_Leavers() {
        var b = new HandshakeBook(); b.Request(5); b.Request(5);
        Assert.IsTrue(b.IsRestoring(5)); CollectionAssert.AreEqual(new[] { 5 }, b.TakePending()); CollectionAssert.IsEmpty(b.TakePending());
        Assert.IsFalse(b.OnDecision(5, HandshakeDecision.Wait));
        Assert.IsTrue(b.OnDecision(5, HandshakeDecision.Again)); CollectionAssert.AreEqual(new[] { 5 }, b.TakePending());
        Assert.IsTrue(b.OnDecision(5, HandshakeDecision.Grant)); Assert.IsFalse(b.IsRestoring(5));
        Assert.IsFalse(b.OnDecision(5, HandshakeDecision.Grant));   // ikinci geri verme yok
        b.Request(7); b.Forget(7); Assert.IsFalse(b.IsRestoring(7)); CollectionAssert.IsEmpty(b.TakePending());
        b.Request(8); b.PendingToRestoring(); CollectionAssert.IsEmpty(b.TakePending()); Assert.IsTrue(b.OnDecision(8, HandshakeDecision.Grant));
    }
    [Test] public void Penalties_From_Log_By_Que() {
        var d = PenaltyRebuild.FromLog(new[] { (3, 250), (1, 40), (3, -100) }, new[] { 0, 3, 4, 1, 2 });   // sıra1=koltuk3, sıra3=koltuk1
        Assert.AreEqual(150, d[1]); Assert.AreEqual(0, d[2]); Assert.AreEqual(40, d[3]); Assert.AreEqual(0, d[4]);
    }
    [Test] public void FinishGate_Seed_From_Hand_Property() {
        int r = -1, w = -1;
        var win = HandBreakdown.Encode(HandBreakdown.Build(2, 3, 1, false, false, false, new[] { new SeatInput { seat = 3 } }, (s, x) => 0));
        FinishGate.Seed(2, win, ref r, ref w);
        Assert.IsFalse(FinishGate.Accept(2, -1, ref r, ref w)); Assert.IsFalse(FinishGate.Accept(2, 9, ref r, ref w));
        int r2 = -1, w2 = -1;
        var draw = HandBreakdown.Encode(HandBreakdown.Build(2, 0, 1, false, false, false, new[] { new SeatInput { seat = 1 } }, (s, x) => 0));
        FinishGate.Seed(2, draw, ref r2, ref w2);
        Assert.IsFalse(FinishGate.Accept(2, -1, ref r2, ref w2)); Assert.IsTrue(FinishGate.Accept(2, 9, ref r2, ref w2));
        int r3 = -1, w3 = -1; FinishGate.Seed(2, null, ref r3, ref w3); Assert.AreEqual(-1, r3);
    }
    [Test] public void Grant_Keeps_Longer_Local_Penalty_List() {
        Assert.IsTrue(HandshakeRule.KeepLocalList(new[] { 1, 2 }, new[] { 1, 2, 3 }));   // masa sahibininki baş: yerel kalır
        Assert.IsTrue(HandshakeRule.KeepLocalList(new int[0], new[] { 4 }));
        Assert.IsFalse(HandshakeRule.KeepLocalList(new[] { 1, 2, 3 }, new[] { 1, 2 })); // masa sahibininki uzun
        Assert.IsFalse(HandshakeRule.KeepLocalList(new[] { 1, 5 }, new[] { 1, 2, 3 })); // farklı
    }
}
