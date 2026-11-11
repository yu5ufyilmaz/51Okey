using System;
using System.IO;
using NUnit.Framework;

public class AutoTestJournalTests
{
    string path;

    [SetUp]
    public void SetUp() => path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".log");

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    [Test]
    public void Journal_FormatsLines()
    {
        var j = new AutoTestJournal(path);
        j.Line("1.17", "AT-g2 que3", "karar: DrawSide k9");
        j.Check("1.17", "AT-g2 que3", "DiscardIslek r6", "que3 +250", "que3 +250", true);
        j.Check("1.21", "AT-host que1", "Process r10", "que2 +100", "que2 +0", false);
        j.Dispose();

        var lines = File.ReadAllLines(path);
        Assert.AreEqual("1.17 | AT-g2 que3 | karar: DrawSide k9", lines[0]);
        Assert.AreEqual("1.17 | AT-g2 que3 | DiscardIslek r6 | beklenen: que3 +250 | gözlenen: que3 +250 | OK", lines[1]);
        Assert.AreEqual("1.21 | AT-host que1 | Process r10 | beklenen: que2 +100 | gözlenen: que2 +0 | [BUG]", lines[2]);
    }
}
