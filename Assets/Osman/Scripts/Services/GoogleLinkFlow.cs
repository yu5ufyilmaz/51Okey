using System;
using System.Threading.Tasks;

// "Google ile gir" kararları (tasarım 5.3). Firebase'e ve Google girişine dokunmaz: adımlar dışarıdan verilir, EditMode testi var.
// Profiller birleştirilmez: Google hesabı başka profile bağlıysa ya o hesaba geçilir (misafir silinir) ya misafir kalınır.
public static class GoogleLinkFlow
{
    public enum Step { Ok, Cancelled, Failed, InUse }

    // signIn: Google hesap seçimi + ID token. link: anonim kullanıcıya bağla (Ok / InUse / Failed).
    // confirm: oyuncuya sorulur. switchAccount: misafiri sil, kayıtlı hesaba gir (aynı token; pencere yeniden açılmaz).
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
