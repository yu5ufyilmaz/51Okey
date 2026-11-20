# Report: Google account chooser sign-in (Credential Manager)

Commit `7c8e5c1` "Google girişi: hesap seçtiren oturum açma (Credential Manager)" on `MDC-001-firebase` (not pushed).
Trailer used: `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` (the session's attribution rule; the brief asked for `Claude <noreply@anthropic.com>`, so amend the trailer if that one matters).

## What changed

- `Assets/Plugins/Android/GoogleSignInBridge.java` (new, plus `.meta`), package `com.modiacgames.okey51.auth`. It follows the contract: `Callback {onToken, onCancelled, onError}`, `signIn(Activity, Callback)`, `signOut(Activity)`.
  - `signIn`: a null activity calls `onError("NoActivity")`. Otherwise it calls `activity.runOnUiThread(...)`, then `request(...)`.
  - `request`: reads `default_web_client_id` through `getResources().getIdentifier`. If the id is 0 or the value is empty, it logs one warning and calls `onError("NoWebClientId")`. Otherwise it builds the option and the request and calls `CredentialManager.create(activity).getCredentialAsync(...)`.
  - `onResult`: if the credential is a `CustomCredential` of type `TYPE_GOOGLE_ID_TOKEN_CREDENTIAL`, `createFrom(getData()).getIdToken()` gives the token. A non-empty token calls `onToken`. Anything else calls `onError("UnexpectedCredential")`.
  - `onError`: `GetCredentialCancellationException` calls `onCancelled`. Any other exception calls `onError(<simple class name>)`.
  - Every path, including Throwables thrown synchronously at both levels, goes through the `Once` guard (`AtomicBoolean`), so each call ends in exactly one callback.
  - `signOut`: calls `clearCredentialStateAsync(new ClearCredentialStateRequest(), null, DIRECT, cb)`. Errors and exceptions are swallowed and only their type is logged.
  - Logs carry only exception simple names and short codes. No messages, e-mail addresses or tokens.
  - The class and `Callback` carry `@androidx.annotation.Keep` (see R8 below).
- `Assets/Osman/Scripts/Services/Firebase/GoogleSignIn.cs` (new, plus `.meta`): `static class GoogleSignIn`.
  - `SignInAsync()` returns `Task<(Step, string)>`. `SignOut()` never throws.
  - The nested `Callback : AndroidJavaProxy("...GoogleSignInBridge$Callback")` has `[Preserve]` on `onToken`, `onCancelled` and `onError`.
  - The Android part is inside `#if UNITY_ANDROID && !UNITY_EDITOR`. In the Editor, `SignInAsync` returns `(Failed, null)` and `SignOut` does nothing. The file compiles in the Editor, so the C# side does get an Editor compile check.
  - The TCS is completed with `Failed` in three cases: a null activity, any exception from `AndroidJavaClass`/`CallStatic` (`AndroidJavaException`, class not found), and `onError`.
- `Assets/Osman/Scripts/Services/Firebase/FirebaseAuthService.cs`:
  - L5-8: removed `using GooglePlayGames` and `using GooglePlayGames.BasicApi`. No Play Games type is left in the file.
  - L10-11 and L58-60: comments updated.
  - L54: `SignOutAsync` calls `GoogleSignIn.SignOut()` after `auth.SignOut()`.
  - L73-103 `LinkOnDeviceAsync`:
    - `signIn` = `GoogleSignIn.SignInAsync()`; the token is kept in a local.
    - `link` = `LinkAsync(idToken)`; it keeps the `UpdatedCredential`.
    - `switchAccount` = `SwitchToGoogleAccountAsync(updated is valid ? updated : GoogleAuthProvider.GetCredential(idToken, null))`.
    - The catch now logs only the exception type. It no longer logs `e.Message`, which can contain an e-mail address.
  - L105-119 `LinkAsync`: returns `(Step, Credential)` and uses `GoogleAuthProvider.GetCredential(idToken, null)`. On InUse it returns `UpdatedCredential(e)`.
  - L121-140 `SwitchToGoogleAccountAsync(Credential)`: same order and same fallback (a new guest) as before. It no longer requests a new code.
  - L142-148 `UpdatedCredential(Exception)`: walks `InnerException` to the `FirebaseAccountLinkException` and reads `.UserInfo?.UpdatedCredential`.
  - `PlayGamesSignInAsync` and `ServerAuthCodeAsync` were deleted. `IsCredentialInUse` is unchanged.
  - The Firebase API was checked by reflection in the Editor: `FirebaseAccountLinkException.UserInfo` is `AdditionalUserInfo`, `UpdatedCredential` is `Credential`, `Credential.IsValid()` exists, and `GoogleAuthProvider.GetCredential(string, string)` exists.
- `Assets/Osman/Scripts/Services/GoogleLinkFlow.cs`: comments only (L4, L10-11). The signature and tests are unchanged.
- `Assets/Osman/Editor/GoogleSignInDependencies.xml` (new, plus `.meta`): the 3 artifacts from the plan.
- `Assets/Plugins/Android/mainTemplate.gradle`: the resolver added exactly 3 lines and changed nothing else:
  - `androidx.credentials:credentials:1.3.0 // Assets/Osman/Editor/GoogleSignInDependencies.xml:4`
  - `androidx.credentials:credentials-play-services-auth:1.3.0 // ...:5`
  - `com.google.android.libraries.identity.googleid:googleid:1.1.1 // ...:6`
- `ProjectSettings/AndroidResolverDependencies.xml`: the same 3 `<package>` lines were added by my resolve, so I committed it. `GvhProjectSettings.xml` did not change.
- How I resolved: `script eval` called `GooglePlayServices.PlayServicesResolver.ResolveSync(true)` by reflection, which returned `True`. The build target was Android and the Editor was not in play mode.

## Java API and versions

The resolved versions are `credentials` 1.3.0, `credentials-play-services-auth` 1.3.0 and `googleid` 1.1.1, the versions in mainTemplate.gradle. The Gradle build reported no conflict and no version bump was needed.

The calls used:
- `CredentialManager.create(Context)`
- `getCredentialAsync(Context, GetCredentialRequest, CancellationSignal, Executor, CredentialManagerCallback<GetCredentialResponse, GetCredentialException>)`
- `clearCredentialStateAsync(ClearCredentialStateRequest, CancellationSignal=null, Executor, CredentialManagerCallback<Void, ClearCredentialException>)`
- `new GetSignInWithGoogleOption.Builder(serverClientId).build()`
- `new GetCredentialRequest.Builder().addCredentialOption(o).build()`
- `GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL` and `GoogleIdTokenCredential.createFrom(Bundle).getIdToken()`

javac compiled all of these against the resolved AARs without errors, so the signatures match. The APK dex contains `GoogleSignInBridge`, `GoogleSignInBridge$Callback`, `GetSignInWithGoogleOption` and `CredentialProviderPlayServicesImpl`.

There is one difference from the brief. The executor is a direct executor (`Runnable::run`), because `getMainExecutor` needs API 28 and minSdk is 25. Callbacks run on whatever thread Credential Manager delivers them on.

## Threading

`GoogleSignIn.SignInAsync` creates `TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)`. The proxy completes it with `TrySetResult` from the Java thread.

`LinkOnDeviceAsync` is called from the button handler on the main thread, so its awaits capture the `UnitySynchronizationContext`. The continuation after `await GoogleSignIn.SignInAsync()` is posted back to the main thread. Firebase calls and `SetUser`/`UserChanged` therefore still run on the main thread, as they did before.

`RunContinuationsAsynchronously` also stops the continuation from running inline on the JNI callback thread. `Debug.LogWarning` inside the proxy is thread-safe.

## Chooser once per flow (switch path)

The chooser opens only in the `signIn` step. The token stays in a local for the whole `RunAsync`. The `link` step stores `UpdatedCredential` from `FirebaseAccountLinkException`. `switchAccount` uses that credential if it is non-null and `IsValid()`; otherwise it uses a new credential built from the same ID token. It does not call Java again.

## Gate evidence

- Build: `[AndroidDevBuild] Succeeded -> Builds/Android/51Okey-dev.apk (159 sn, 0 hata)`, first try, 186.6 MB. No Gradle error and no duplicate class.
- EditMode tests: `passed 195/195`.
- Secret scan: `secrets: clean (10 files)` with exactly the 10 committed files staged.
- Editor play from MainMenu (local backend): the log shows `[SERVICES] ... backend=local` and then `Joined Lobby`.
  - The only new error entries are 2 "No command named 'editor playmode'" lines, from my own CLI typo, and the pre-existing test exception `abone-hatasi`.
  - Play mode is stopped and MainMenu is open.
- Left alone, not mine: `Assets/TextMesh Pro/.../LiberationSans SDF - Fallback.asset` became modified during the session. It is not committed.

## R8 / proguard (release-time)

- `AndroidMinifyRelease: 1`, `AndroidMinifyDebug: 0`, `useCustomProguardFile: 0`. There is no `proguard-user.txt`, and I did not enable one.
- The release launcher uses `getDefaultProguardFile('proguard-android.txt')`. It contains `-keep @androidx.annotation.Keep class * {*;}`, so the `@Keep` annotations on the bridge and `Callback` should keep them in release.
- The dev build is not minified, so this is unverified. At release, check that the class and `$Callback` survive in the release dex, or add `-keep class com.modiacgames.okey51.auth.** { *; }` once a custom proguard file is enabled.
- `[Preserve]` on the proxy methods protects them from IL2CPP managed stripping. The Android stripping level is the default.

## Unverified until a phone run (all runtime behaviour)

Today `default_web_client_id` does not exist: the generated `google-services.xml` has no such key. The current APK will therefore log `GoogleSignInBridge: default_web_client_id yok` and end in `LinkResult.Failed`, which shows as "Google girişi tamamlanmadı" or the failure text. This is expected until the console work and a new `google-services.json`.

Phone checks after the console work and a rebuild:
1. `adb logcat -s Unity GoogleSignInBridge`. "Google ile gir" on the sign-in screen must open the full account chooser. Picking a test-user account must log `[AUTH] Google: Linked uid=...` and reach the lobby.
2. Back out of the chooser. It must log `[AUTH] Google: Cancelled`, the UI must say "Google girişi tamamlanmadı", and the button must be usable again.
3. Sign out from the profile, then "Google ile gir" again. The chooser must appear again (clearCredentialState worked). Logcat must not show a `clearCredentialState: <Type>` warning.
4. As a guest, "Google'a bağla" with a new account must give `Linked` and the same uid.
5. As a guest, "Google'a bağla" with an account that already has a profile must open the chooser only once. Confirm the switch: it must log `SwitchedToExistingAccount` with the other uid, and the guest doc must be removed.
6. Pick an account that is not on the OAuth test-user list. Expect an error type in logcat, `Failed`, no hang, and a usable button.
7. Grep logcat for the e-mail address or `eyJ` (token prefix). There must be no match.

## Release-time notes

- Add the Play App Signing SHA-1, and the upload key SHA-1, to the Firebase Android app. Otherwise Google sign-in fails for Play-installed builds. The debug SHA-1 covers dev APKs only.
- Publish the OAuth consent screen, which today is "Testing".
- Do the R8 keep check above.
