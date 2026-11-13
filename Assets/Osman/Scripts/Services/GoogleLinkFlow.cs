using System;
using System.Threading.Tasks;

// "Google ile gir" kararları (tasarım 5.3). Firebase'e ve Play Games'e dokunmaz: adımlar dışarıdan verilir, EditMode testi var.
// Profiller birleştirilmez: Google hesabı başka profile bağlıysa ya o hesaba geçilir (misafir silinir) ya misafir kalınır.
public static class GoogleLinkFlow
{
    public enum Step { Ok, Cancelled, Failed, InUse }

    // signIn: Play Games girişi + sunucu kodu. link: anonim kullanıcıya bağla (Ok / InUse / Failed).
    // confirm: oyuncuya sorulur. switchAccount: misafiri sil, kayıtlı hesaba gir (yeni sunucu koduyla; kod tek kullanımlık).
    public static async Task<LinkResult> RunAsync(
        Func<Task<Step>> signIn,
        Func<Task<Step>> link,
        Func<Task<bool>> confirm,
        Func<Task<Step>> switchAccount)
    {
        var step = await signIn();
        if (step != Step.Ok)
            return step == Step.Cancelled ? LinkResult.Cancelled : LinkResult.Failed;
        step = await link();
        if (step == Step.Ok)
            return LinkResult.Linked;
        if (step != Step.InUse)
            return LinkResult.Failed;
        if (!await confirm())
            return LinkResult.Cancelled;
        return await switchAccount() == Step.Ok ? LinkResult.SwitchedToExistingAccount : LinkResult.Failed;
    }
}
