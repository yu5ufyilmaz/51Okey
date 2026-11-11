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

    [Test]
    public void IsGameError_IgnoresEditorSideErrors()
    {
        Assert.IsFalse(
            SyncAutoTest.IsGameError(
                "Unable to perform online search:",
                "UnityEditor.EditorApplication:Internal_CallUpdateFunctions ()\n",
                LogType.Error
            )
        );
        Assert.IsFalse(SyncAutoTest.IsGameError("[DESYNC] 1.2: x", "DesyncDetector:Compare ()\n", LogType.Error));
        Assert.IsFalse(SyncAutoTest.IsGameError("warn", "TileUI:OnEndDrag ()\n", LogType.Warning));
    }

    [Test]
    public void IsGameError_KeepsGameErrors()
    {
        Assert.IsTrue(
            SyncAutoTest.IsGameError("NullReferenceException", "TileUI.OnEndDrag ()\n", LogType.Exception)
        );
        Assert.IsTrue(
            SyncAutoTest.IsGameError("Operation CreateGame not called", "UnityEngine.Debug:LogError (object)\n", LogType.Error)
        );
        Assert.IsTrue(SyncAutoTest.IsGameError("boom", "", LogType.Error));
    }

    [Test]
    public void SeedForDeal_StepsPerDeal()
    {
        Assert.AreEqual(42, DesyncDetector.SeedForDeal(42, 1));
        Assert.AreEqual(43, DesyncDetector.SeedForDeal(42, 2));
    }
}
