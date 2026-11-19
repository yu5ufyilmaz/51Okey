package com.modiacgames.okey51.auth;

import android.app.Activity;
import android.os.CancellationSignal;
import android.util.Log;

import androidx.annotation.Keep;
import androidx.credentials.ClearCredentialStateRequest;
import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.ClearCredentialException;
import androidx.credentials.exceptions.GetCredentialCancellationException;
import androidx.credentials.exceptions.GetCredentialException;

import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;

import java.util.concurrent.Executor;
import java.util.concurrent.atomic.AtomicBoolean;

// "Google ile oturum aç" (Credential Manager, hesap seçme penceresi). C# tarafı GoogleSignIn.cs; AndroidJavaProxy ile çağrılır.
// Loglara ve C#'a yalnız istisna türü gider: ileti metni, e-posta ya da token taşınmaz.
// @Keep: sınıf C#'tan adıyla bulunur, Callback yansımayla çağrılır; sürüm (R8) derlemesinde adlar korunmalı.
@Keep
public final class GoogleSignInBridge {
    static final String TAG = "GoogleSignInBridge";
    // Geri çağrı doğrudan Credential Manager'ın verdiği iş parçacığında çalışır; C# sonucu ana iş parçacığına kendi taşır.
    static final Executor DIRECT = Runnable::run;

    @Keep
    public interface Callback {
        void onToken(String idToken);
        void onCancelled();
        void onError(String type);
    }

    private GoogleSignInBridge() {}

    public static void signIn(final Activity activity, final Callback cb) {
        final Once once = new Once(cb);
        if (activity == null) {
            once.error("NoActivity");
            return;
        }
        try {
            // Pencere aktiviteye bağlı: istek UI iş parçacığından gönderilir.
            activity.runOnUiThread(() -> {
                try {
                    request(activity, once);
                } catch (Throwable t) {
                    once.error(t.getClass().getSimpleName());
                }
            });
        } catch (Throwable t) {
            once.error(t.getClass().getSimpleName());
        }
    }

    static void request(Activity activity, final Once once) {
        int id = activity.getResources().getIdentifier("default_web_client_id", "string", activity.getPackageName());
        String clientId = id == 0 ? null : activity.getString(id);
        if (clientId == null || clientId.isEmpty()) {
            Log.w(TAG, "default_web_client_id yok (google-services.json'da web istemcisi yok)");
            once.error("NoWebClientId");
            return;
        }
        GetSignInWithGoogleOption option = new GetSignInWithGoogleOption.Builder(clientId).build();
        GetCredentialRequest request = new GetCredentialRequest.Builder().addCredentialOption(option).build();
        CredentialManager.create(activity).getCredentialAsync(activity, request, new CancellationSignal(), DIRECT,
            new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                @Override
                public void onResult(GetCredentialResponse response) {
                    try {
                        Credential c = response.getCredential();
                        if (c instanceof CustomCredential
                                && GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(c.getType())) {
                            String token = GoogleIdTokenCredential.createFrom(c.getData()).getIdToken();
                            if (token != null && !token.isEmpty()) {
                                once.token(token);
                                return;
                            }
                        }
                        once.error("UnexpectedCredential");
                    } catch (Throwable t) {
                        once.error(t.getClass().getSimpleName());
                    }
                }

                @Override
                public void onError(GetCredentialException e) {
                    if (e instanceof GetCredentialCancellationException)
                        once.cancelled();
                    else
                        once.error(e.getClass().getSimpleName());
                }
            });
    }

    // Çıkışta kayıtlı seçim temizlenir: sonraki girişte hesap yeniden sorulur. Hata yutulur, yalnız türü loglanır.
    public static void signOut(final Activity activity) {
        if (activity == null)
            return;
        try {
            CredentialManager.create(activity).clearCredentialStateAsync(new ClearCredentialStateRequest(), null, DIRECT,
                new CredentialManagerCallback<Void, ClearCredentialException>() {
                    @Override
                    public void onResult(Void result) {}

                    @Override
                    public void onError(ClearCredentialException e) {
                        Log.w(TAG, "clearCredentialState: " + e.getClass().getSimpleName());
                    }
                });
        } catch (Throwable t) {
            Log.w(TAG, "clearCredentialState: " + t.getClass().getSimpleName());
        }
    }

    // Her signIn tam bir geri çağrıyla biter; ikinci çağrı yok sayılır.
    static final class Once {
        final Callback cb;
        final AtomicBoolean done = new AtomicBoolean();

        Once(Callback cb) { this.cb = cb; }

        void token(String t) { if (done.compareAndSet(false, true)) cb.onToken(t); }
        void cancelled() { if (done.compareAndSet(false, true)) cb.onCancelled(); }
        void error(String type) {
            if (!done.compareAndSet(false, true))
                return;
            Log.w(TAG, "signIn: " + type);
            cb.onError(type);
        }
    }
}
