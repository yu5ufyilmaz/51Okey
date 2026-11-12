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
    public void Config_ReadsSmartFields()
    {
        WriteConfig(
            "{\"runId\":\"r1\",\"role\":\"guest\",\"mode\":\"smart\",\"mistakeRate\":0.3,\"injectOracleBugAtMove\":5}"
        );
        Assert.IsTrue(SyncAutoTestFiles.TryLoadConfig(root, out var c));
        Assert.AreEqual("smart", c.mode);
        Assert.AreEqual(0.3f, c.mistakeRate, 1e-6);
        Assert.AreEqual(5, c.injectOracleBugAtMove);
    }

    [Test]
    public void Config_DefaultsToSimpleMode()
    {
        WriteConfig("{\"runId\":\"r1\"}");
        Assert.IsTrue(SyncAutoTestFiles.TryLoadConfig(root, out var c));
        Assert.AreEqual("simple", c.mode);
    }

    [Test]
    public void Config_PhotonNameIsDisplayNameWhenGiven()
    {
        var c = JsonUtility.FromJson<SyncAutoTestConfig>("{\"nickName\":\"AT-g1\",\"displayName\":\"Ayni\"}");
        Assert.AreEqual("Ayni", c.PhotonName);
        Assert.AreEqual("AT-g1", c.nickName);
    }

    [Test]
    public void Config_PhotonNameFallsBackToNickName()
    {
        var c = JsonUtility.FromJson<SyncAutoTestConfig>("{\"nickName\":\"AT-host\"}");
        Assert.AreEqual("AT-host", c.PhotonName);
        Assert.AreEqual(0f, c.botFillDelaySec);
    }

    [Test]
    public void Status_WritesCounters()
    {
        var s = new SyncAutoTestStatus { opens = 2, processes = 3, sidePicks = 1, mistakes = 4, bugs = 1, finishType = "okey" };
        SyncAutoTestFiles.WriteStatus(root, s);
        var read = JsonUtility.FromJson<SyncAutoTestStatus>(
            File.ReadAllText(Path.Combine(SyncAutoTestFiles.Dir(root), "status.json"))
        );
        Assert.AreEqual(2, read.opens);
        Assert.AreEqual(3, read.processes);
        Assert.AreEqual(1, read.sidePicks);
        Assert.AreEqual(4, read.mistakes);
        Assert.AreEqual(1, read.bugs);
        Assert.AreEqual("okey", read.finishType);
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
