using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;
using Step = GoogleLinkFlow.Step;

// "Google ile oturum aç" (Android Credential Manager, hesap seçme penceresi). Java tarafı
// Assets/Plugins/Android/GoogleSignInBridge.java. Editörde ve Android dışında giriş yok: Failed.
static class GoogleSignIn
{
    const string Bridge = "com.modiacgames.okey51.auth.GoogleSignInBridge";

    // Ok + ID token / Cancelled / Failed. Token loglanmaz.
    public static Task<(Step step, string idToken)> SignInAsync()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // RunContinuationsAsynchronously: Java iş parçacığında tamamlanır, await eden ana iş parçacığında sürer.
        var done = new TaskCompletionSource<(Step, string)>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            if (activity == null)
            {
                Debug.LogWarning("[AUTH] Google girişi: etkinlik yok");
                done.TrySetResult((Step.Failed, null));
            }
            else
            {
                using var bridge = new AndroidJavaClass(Bridge);
                bridge.CallStatic("signIn", activity, new Callback(done));
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[AUTH] Google girişi başlatılamadı: " + e.GetType().Name);
            done.TrySetResult((Step.Failed, null));
        }
        return done.Task;
#else
        return Task.FromResult((Step.Failed, (string)null));
#endif
    }

    // Kayıtlı hesap seçimini temizler (sonraki girişte yeniden sorulur). Hata atmaz.
    public static void SignOut()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var bridge = new AndroidJavaClass(Bridge);
            bridge.CallStatic("signOut", activity);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[AUTH] Google çıkışı: " + e.GetType().Name);
        }
#endif
    }

    // Java'daki GoogleSignInBridge.Callback; yöntemler Java'dan adıyla çağrılır (Preserve: IL2CPP ayıklamasın).
    sealed class Callback : AndroidJavaProxy
    {
        readonly TaskCompletionSource<(Step, string)> done;

        public Callback(TaskCompletionSource<(Step, string)> done) : base(Bridge + "$Callback") => this.done = done;

        [Preserve] public void onToken(string idToken) => done.TrySetResult((Step.Ok, idToken));
        [Preserve] public void onCancelled() => done.TrySetResult((Step.Cancelled, null));

        [Preserve]
        public void onError(string type)
        {
            Debug.LogWarning("[AUTH] Google girişi: " + type);
            done.TrySetResult((Step.Failed, null));
        }
    }
}
