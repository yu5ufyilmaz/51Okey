using System;
using System.Threading;
using System.Threading.Tasks;

// Başarı önbelleğe alınır; hata ya da zaman aşımı alınmaz, sonraki çağrı yeniden dener.
public class InitGate
{
    readonly Func<Task> work;
    readonly int timeoutMs;
    Task running;
    Task lastWork;

    public bool IsReady { get; private set; }
    public event Action<Exception> Failed;

    public InitGate(Func<Task> work, int timeoutMs)
    {
        this.work = work;
        this.timeoutMs = timeoutMs;
    }

    // Aynı karede iki çağrı aynı Task'ı alır.
    public Task RunAsync()
    {
        if (running == null || running.IsFaulted || running.IsCanceled)
            running = RunOnceAsync();
        return running;
    }

    async Task RunOnceAsync()
    {
        try
        {
            // Zaman aşımına uğrayan iş sürüyorsa ya da sonradan bittiyse yenisi başlatılmaz (ikinci anonim hesap açılmasın).
            var w = lastWork != null && !lastWork.IsFaulted && !lastWork.IsCanceled ? lastWork : (lastWork = work());
            // Yerel arka uç aynı karede biter: zamanlayıcı kurulmaz, akışın zamanlaması değişmez.
            if (!w.IsCompleted)
            {
                using (var cts = new CancellationTokenSource())
                {
                    if (await Task.WhenAny(w, Task.Delay(timeoutMs, cts.Token)) != w)
                        throw new TimeoutException("servisler " + timeoutMs + " ms içinde hazır olmadı");
                    cts.Cancel();
                }
            }
            await w;
            IsReady = true;
        }
        catch (Exception e)
        {
            Failed?.Invoke(e);
            throw;
        }
    }
}
