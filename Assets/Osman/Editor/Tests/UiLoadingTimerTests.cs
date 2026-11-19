using NUnit.Framework;

public class UiLoadingTimerTests
{
    [Test] public void TimesOutOnceAfter15Seconds()
    {
        var t = new UiLoadingTimer(); t.Show(100f);
        Assert.IsFalse(t.Tick(114.9f)); Assert.IsTrue(t.Active);
        Assert.IsTrue(t.Tick(115f)); Assert.IsFalse(t.Active);
        Assert.IsFalse(t.Tick(200f));
    }

    [Test] public void HideBeforeTimeout_NeverFires()
    {
        var t = new UiLoadingTimer(); t.Show(0f); t.Hide();
        Assert.IsFalse(t.Tick(999f));
    }

    [Test] public void ShowAgain_RestartsClock()
    {
        var t = new UiLoadingTimer(); t.Show(0f); t.Show(10f);
        Assert.IsFalse(t.Tick(20f)); Assert.IsTrue(t.Tick(25f));
    }

    [Test] public void NoTimeout_NeverFires()
    {
        var t = new UiLoadingTimer(); t.Show(0f, false);
        Assert.IsTrue(t.Active);
        Assert.IsFalse(t.Tick(9999f)); Assert.IsTrue(t.Active);
        t.Hide(); Assert.IsFalse(t.Active);
    }

    [Test] public void ShowWithTimeoutAfterNoTimeout_Fires()
    {
        var t = new UiLoadingTimer(); t.Show(0f, false); t.Show(100f);
        Assert.IsFalse(t.Tick(114f)); Assert.IsTrue(t.Tick(115f));
    }
}
