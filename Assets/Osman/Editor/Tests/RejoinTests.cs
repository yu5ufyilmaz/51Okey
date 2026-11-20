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
}
