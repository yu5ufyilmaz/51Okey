using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public class SyncAutoTestDataTests
{
    string root;

    [SetUp]
    public void SetUp()
    {
        root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(SyncAutoTestFiles.Dir(root));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }

    void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(SyncAutoTestFiles.Dir(root), "config.json"), json);

    [Test]
    public void TryLoadConfig_ReadsRunnerJson()
    {
        WriteConfig(
            "{\"runId\":\"r1\",\"role\":\"guest\",\"roomName\":\"autotest_r1\",\"nickName\":\"AT-g1\","
                + "\"deals\":2,\"seed\":7,\"injectDesyncAtMove\":5,\"moveDelaySec\":0.3}"
        );

        Assert.IsTrue(SyncAutoTestFiles.TryLoadConfig(root, out var c));
        Assert.AreEqual("r1", c.runId);
        Assert.AreEqual("AT-g1", c.nickName);
        Assert.AreEqual(2, c.deals);
        Assert.AreEqual(7, c.seed);
        Assert.AreEqual(5, c.injectDesyncAtMove);
        Assert.IsFalse(c.IsHost);
    }

    [Test]
    public void TryLoadConfig_MissingFile_ReturnsFalse()
    {
        Assert.IsFalse(SyncAutoTestFiles.TryLoadConfig(root, out _));
    }

    [Test]
    public void TryLoadConfig_Malformed_ReturnsFalse()
    {
        WriteConfig("{not json");
        Assert.IsFalse(SyncAutoTestFiles.TryLoadConfig(root, out _));
    }

    [Test]
    public void TryLoadConfig_EmptyRunId_ReturnsFalse()
    {
        WriteConfig("{}");
        Assert.IsFalse(SyncAutoTestFiles.TryLoadConfig(root, out _));
    }

    [Test]
    public void WriteStatus_ReplacesWholeFile()
    {
        var s = new SyncAutoTestStatus { state = "Playing" };
        s.errors.Add("a");
        s.errors.Add("b");
        SyncAutoTestFiles.WriteStatus(root, s);
        SyncAutoTestFiles.WriteStatus(root, new SyncAutoTestStatus { state = "Done" });

        string dir = SyncAutoTestFiles.Dir(root);
        var read = JsonUtility.FromJson<SyncAutoTestStatus>(
            File.ReadAllText(Path.Combine(dir, "status.json"))
        );
        Assert.AreEqual("Done", read.state);
        Assert.AreEqual(0, read.errors.Count);
        Assert.IsEmpty(Directory.GetFiles(dir, "*.tmp"));
    }
}
