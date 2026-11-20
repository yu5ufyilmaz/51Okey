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
}
