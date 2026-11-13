using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Step = GoogleLinkFlow.Step;

public class GoogleLinkFlowTests
{
    int confirms, switches, links;

    LinkResult Run(Step signIn, Step link = Step.Ok, bool confirm = false, Step switchTo = Step.Ok)
    {
        confirms = switches = links = 0;
        return GoogleLinkFlow.RunAsync(
            () => Task.FromResult(signIn),
            () => { links++; return Task.FromResult(link); },
            () => { confirms++; return Task.FromResult(confirm); },
            () => { switches++; return Task.FromResult(switchTo); }
        ).GetAwaiter().GetResult();
    }

    [Test]
    public void SignInCancelled_StaysGuest_NoLink()
    {
        Assert.AreEqual(LinkResult.Cancelled, Run(Step.Cancelled));
        Assert.AreEqual(0, links);
    }

    [Test]
    public void SignInFailed_IsFailed()
    {
        Assert.AreEqual(LinkResult.Failed, Run(Step.Failed));
        Assert.AreEqual(0, links);
    }

    [Test]
    public void LinkOk_IsLinked_NoQuestion()
    {
        Assert.AreEqual(LinkResult.Linked, Run(Step.Ok, Step.Ok));
        Assert.AreEqual(0, confirms);
        Assert.AreEqual(0, switches);
    }

    [Test]
    public void LinkFailed_IsFailed_NoQuestion()
    {
        Assert.AreEqual(LinkResult.Failed, Run(Step.Ok, Step.Failed));
        Assert.AreEqual(0, confirms);
    }

    [Test]
    public void InUse_PlayerSaysNo_StaysGuest()
    {
        Assert.AreEqual(LinkResult.Cancelled, Run(Step.Ok, Step.InUse, confirm: false));
        Assert.AreEqual(1, confirms);
        Assert.AreEqual(0, switches, "onay yoksa misafir silinmez");
    }

    [Test]
    public void InUse_PlayerSaysYes_Switches()
    {
        Assert.AreEqual(LinkResult.SwitchedToExistingAccount, Run(Step.Ok, Step.InUse, confirm: true));
        Assert.AreEqual(1, switches);
    }

    [Test]
    public void InUse_SwitchFails_IsFailed()
    {
        Assert.AreEqual(LinkResult.Failed, Run(Step.Ok, Step.InUse, confirm: true, switchTo: Step.Failed));
    }

    [Test]
    public void StepException_ReachesCaller()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GoogleLinkFlow.RunAsync(
                () => Task.FromResult(Step.Ok),
                () => Task.FromException<Step>(new InvalidOperationException()),
                () => Task.FromResult(true),
                () => Task.FromResult(Step.Ok)
            ).GetAwaiter().GetResult());
    }
}
