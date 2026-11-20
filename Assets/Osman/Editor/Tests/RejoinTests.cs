using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

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
}
