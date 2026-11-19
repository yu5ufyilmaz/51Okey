using NUnit.Framework;

public class UiBackStackTests
{
    static BackResult Back(UiBackStack<string> s, bool loading, System.Collections.Generic.List<string> closed,
        System.Func<string, bool> alive = null) =>
        s.Back(loading, alive ?? (_ => true), closed.Add);

    [Test] public void EmptyStack_AsksExit()
    {
        var closed = new System.Collections.Generic.List<string>();
        Assert.AreEqual(BackResult.AskExit, Back(new UiBackStack<string>(), false, closed));
        Assert.IsEmpty(closed);
    }

    [Test] public void BackClosesTopOnly()
    {
        var s = new UiBackStack<string>(); s.Push("profil"); s.Push("onay");
        var closed = new System.Collections.Generic.List<string>();
        Assert.AreEqual(BackResult.ClosedWindow, Back(s, false, closed));
        CollectionAssert.AreEqual(new[] { "onay" }, closed);
        Assert.AreEqual(1, s.Count);
    }

    [Test] public void BackIsSwallowedWhileLoading()
    {
        var s = new UiBackStack<string>(); s.Push("profil");
        var closed = new System.Collections.Generic.List<string>();
        Assert.AreEqual(BackResult.Swallowed, Back(s, true, closed));
        Assert.IsEmpty(closed); Assert.AreEqual(1, s.Count);
        Assert.AreEqual(BackResult.Swallowed, Back(new UiBackStack<string>(), true, closed));
    }

    [Test] public void DeadEntriesAreSkipped()
    {
        var s = new UiBackStack<string>(); s.Push("canlı"); s.Push("ölü");
        var closed = new System.Collections.Generic.List<string>();
        Assert.AreEqual(BackResult.ClosedWindow, Back(s, false, closed, w => w != "ölü"));
        CollectionAssert.AreEqual(new[] { "canlı" }, closed);
        Assert.AreEqual(0, s.Count);
        Assert.AreEqual(BackResult.AskExit, Back(s, false, closed, w => w != "ölü"));
    }

    [Test] public void PushTwice_KeepsOneEntry_RemoveDropsIt()
    {
        var s = new UiBackStack<string>(); s.Push("a"); s.Push("a");
        Assert.AreEqual(1, s.Count);
        s.Remove("a"); s.Remove("yok");
        Assert.AreEqual(0, s.Count);
    }
}
