using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

public class InitGateTests
{
    SynchronizationContext saved;

    // Devamlar editörün ana iş parçacığına postalanırsa GetResult kilitlenir; testte havuz iş parçacığı kullanılır.
    [SetUp]
    public void SetUp()
    {
        saved = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
    }

    [TearDown]
    public void TearDown() => SynchronizationContext.SetSynchronizationContext(saved);

    [Test]
    public void Success_IsCachedAndWorkRunsOnce()
    {
        int runs = 0;
        var gate = new InitGate(() => { runs++; return Task.CompletedTask; }, 1000);
        var first = gate.RunAsync();
        Assert.IsTrue(first.IsCompleted, "anında biten iş aynı karede biter");
        Assert.AreSame(first, gate.RunAsync());
        Assert.AreEqual(1, runs);
        Assert.IsTrue(gate.IsReady);
    }

    [Test]
    public void Failure_IsNotCached_NextCallRetries()
    {
        int runs = 0;
        Exception seen = null;
        var gate = new InitGate(() => ++runs == 1 ? Task.FromException(new InvalidOperationException("ilk")) : Task.CompletedTask, 1000);
        gate.Failed += e => seen = e;
        Assert.Throws<InvalidOperationException>(() => gate.RunAsync().GetAwaiter().GetResult());
        Assert.IsFalse(gate.IsReady);
        Assert.IsInstanceOf<InvalidOperationException>(seen);
        gate.RunAsync().GetAwaiter().GetResult();
        Assert.AreEqual(2, runs);
        Assert.IsTrue(gate.IsReady);
    }

    [Test]
    public void SyncThrowingWork_IsNotCached()
    {
        int runs = 0;
        var gate = new InitGate(() => { if (++runs == 1) throw new InvalidOperationException("senkron"); return Task.CompletedTask; }, 1000);
        Assert.Throws<InvalidOperationException>(() => gate.RunAsync().GetAwaiter().GetResult());
        gate.RunAsync().GetAwaiter().GetResult();
        Assert.IsTrue(gate.IsReady);
    }

    [Test]
    public void Timeout_FailsThenRetryWaitsForSameWork()
    {
        int runs = 0;
        var slow = new TaskCompletionSource<bool>();
        Exception seen = null;
        var gate = new InitGate(() => { runs++; return slow.Task; }, 50);
        gate.Failed += e => seen = e;
        Assert.Throws<TimeoutException>(() => gate.RunAsync().GetAwaiter().GetResult());
        Assert.IsInstanceOf<TimeoutException>(seen);
        Assert.IsFalse(gate.IsReady);
        var retry = gate.RunAsync();
        slow.SetResult(true);
        retry.GetAwaiter().GetResult();
        Assert.AreEqual(1, runs, "süren iş yeniden başlatılmaz");
        Assert.IsTrue(gate.IsReady);
    }

    [Test]
    public void Timeout_ThenLateSuccess_RetryDoesNotRerunWork()
    {
        int runs = 0;
        var slow = new TaskCompletionSource<bool>();
        var gate = new InitGate(() => { runs++; return slow.Task; }, 50);
        Assert.Throws<TimeoutException>(() => gate.RunAsync().GetAwaiter().GetResult());
        slow.SetResult(true);
        gate.RunAsync().GetAwaiter().GetResult();
        Assert.AreEqual(1, runs);
        Assert.IsTrue(gate.IsReady);
    }

    [Test]
    public void ChooseFirebase_DeviceFirebase_EditorLocalUnlessPrefAndPlaying_AutoTestAlwaysLocal()
    {
        Assert.IsTrue(GameServices.ChooseFirebase(isEditor: false, editorPref: false, isPlaying: true, autoTest: false));
        Assert.IsFalse(GameServices.ChooseFirebase(isEditor: true, editorPref: false, isPlaying: true, autoTest: false));
        Assert.IsTrue(GameServices.ChooseFirebase(isEditor: true, editorPref: true, isPlaying: true, autoTest: false));
        Assert.IsFalse(GameServices.ChooseFirebase(isEditor: true, editorPref: true, isPlaying: false, autoTest: false), "EditMode testi Firebase'e dokunmaz");
        Assert.IsFalse(GameServices.ChooseFirebase(isEditor: true, editorPref: true, isPlaying: true, autoTest: true));
    }

    [Test]
    public void EditMode_UsesLocalBackend()
    {
        Assert.IsFalse(GameServices.UsesFirebase);
        Assert.IsInstanceOf<LocalAuthService>(GameServices.Auth);
    }

    [Test]
    public void FirestoreFields_MapToProfileWithDefaults()
    {
        var p = FirestoreProfileStore.FromFields("u1", new System.Collections.Generic.Dictionary<string, object>
        {
            { "displayName", "Osman" }, { "coins", 1200L }, { "level", 3L }, { "xp", 250L }, { "noAds", true }, { "schemaVersion", 1L },
        }, "Oyuncu0001");
        Assert.AreEqual("u1", p.UserId);
        Assert.AreEqual("Osman", p.DisplayName);
        Assert.AreEqual(1200, p.Coins);
        Assert.AreEqual(3, p.Level);
        Assert.AreEqual(250, p.Xp);
        Assert.IsTrue(p.NoAds);

        var empty = FirestoreProfileStore.FromFields("u2", new System.Collections.Generic.Dictionary<string, object>(), "Oyuncu0002");
        Assert.AreEqual("Oyuncu0002", empty.DisplayName);
        Assert.AreEqual(0, empty.Coins);
        Assert.AreEqual(1, empty.Level);
        Assert.IsFalse(empty.NoAds);
    }
}
