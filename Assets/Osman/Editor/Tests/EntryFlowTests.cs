using NUnit.Framework;
using Screen = EntryFlow.Screen;

public class EntryFlowTests
{
    [Test]
    public void InitNotDone_IsLoading()
    {
        foreach (bool session in new[] { false, true })
        foreach (bool needsName in new[] { false, true })
        {
            Assert.AreEqual(Screen.Loading, EntryFlow.Next(false, false, session, needsName));
            Assert.AreEqual(Screen.Loading, EntryFlow.Next(false, true, session, needsName));
        }
    }

    [Test]
    public void InitFailed_IsRetry()
    {
        foreach (bool session in new[] { false, true })
        foreach (bool needsName in new[] { false, true })
            Assert.AreEqual(Screen.Retry, EntryFlow.Next(true, true, session, needsName));
    }

    [Test]
    public void NoSession_IsChoose()
    {
        Assert.AreEqual(Screen.Choose, EntryFlow.Next(true, false, false, false));
        Assert.AreEqual(Screen.Choose, EntryFlow.Next(true, false, false, true));
    }

    [Test]
    public void SessionWithDefaultName_IsAskName()
    {
        Assert.AreEqual(Screen.AskName, EntryFlow.Next(true, false, true, true));
    }

    [Test]
    public void SessionWithName_IsLobby()
    {
        Assert.AreEqual(Screen.Lobby, EntryFlow.Next(true, false, true, false));
    }
}
