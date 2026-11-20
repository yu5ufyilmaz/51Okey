# Benzersiz isim + çıkış Profil'de: uygulama raporu

Plan: `docs/superpowers/plans/2026-11-19-benzersiz-isim.md`. Dal `MDC-001-firebase`.

## Commits

- `d851d00` Hesaptan çık Profil panelinde (Paket A)
- `5be090d` Benzersiz isim: isim kaydı, otomatik misafir ismi (Paket B)

Neither is pushed. The files listed as "not yours" were not committed or reverted. `LobbyMenu.unity` and `MainMenu.unity` are clean; MainMenu was not changed.

## Paket A: "Hesaptan çık" moved to the Profile panel

Scene `Assets/Osman/Scenes/LobbyMenu.unity`: both objects were reparented with `SetParent(parent, false)`. No object was destroyed or rebuilt, so the onClick and field references are the same objects as before.
- `SignOutButton`: `SettingsPanel/Box` → `ProfilePanel/Box`, sibling placed after `LinkStatus`, pos (0,-715) 440x84.
- `SignOutStatus`: same move, pos (0,-783), width 600 → 700 to match the other status lines in the Profile box.
- Profile `CloseButton`: -715 → -873. Profile `Box`: 760x800 → 760x960.
- Settings `CloseButton`: -635 → -485. Settings `Box`: 700x700 → 700x570, which closes the gap.
- Note: the panels are direct children of `Canvas`, not of `SafeArea`. A 960 box fits inside the 1080 canvas.

`Assets/Osman/Scripts/UI/LobbyAccountUI.cs`:
- `signOutButton` and `signOutStatus` moved to the `[Header("Profil")]` fields (lines 30-31). Serialization is by name, so the scene references are kept.
- `signOutStatus.text = ""` is now cleared in `OpenProfile` (line 116) instead of `OpenSettings`.
- The visibility rule is unchanged: visible only when `CanSignOut`.

Manual check (Editor, local backend, play mode):
- `signout=True signoutInProfile=True` in the Profile panel; no sign-out in Settings.
- `signOutButton.onClick.Invoke()` → `scene=MainMenu choice=True session=False`.
- Guest button → back in the lobby with the same profile.
- Screenshots: `C:\Users\osman\AppData\Local\Temp\claude\F--Documents-UnityProjects-51Okey\03f344cb-9d2e-405d-8e9c-2514c31a1d8f\scratchpad\nm-a-profile.png` and `...\scratchpad\nm-a-settings.png`. I read both: nothing overlaps and nothing is cut off.
- Gate: EditMode 159/159; `secrets: clean (2 files)`.

## Paket B: unique names

### Code (file:line in HEAD)

- `Services/PlayerProfile.cs:9`: `public bool NameClaimed;`. `:15`: `public enum NameResult { Ok, Invalid, Taken, Failed }`.
- `Services/IProfileStore.cs:7`: `Task<NameResult> SetDisplayNameAsync(string name)`.
- `Services/GameConfig.cs`:
  - `:11` `GuestNamePrefix = "Misafir-"`.
  - `:17` `IsValidName`: storable and does not start with "misafir-" (OrdinalIgnoreCase).
  - `:21` `IsStorableName`: 2-16 after trim, no '/'.
  - `:27` `GuestName(int)`: `% 100000`, D5; negative numbers wrap.
  - `:29` `IsGuestName`: exactly the prefix plus 5 ASCII digits.
  - `DefaultName` removed, and its 3 tests with it; no caller was left.
- `Services/NameFlow.cs` (new):
  - `Step { None, AssignGuest, ClaimCurrent }` and the pure `Next(isGuest, nameClaimed, name)`.
  - `ErrorText(NameResult)`: the texts shared by both UIs.
- `Services/GameServices.cs`:
  - `:12` `GuestNameAttempts = 5`. `:24` one static `System.Random`.
  - `:41` `NeedsName = CanChangeName && (!NameClaimed || IsGuestName(DisplayName))`. `:43` `CanChangeName = HasSession && !Auth.IsGuest`.
  - `:113` `ChooseNameAsync`: returns `Invalid` for a guest or a name that fails `IsValidName`; otherwise calls `Profile.SetDisplayNameAsync`.
  - `:206` `LoadOrCreateAsync(uid, NewGuestName())`. `:209` `await EnsureNameAsync()` runs inside the load Task.
  - `:216` `EnsureNameAsync`:
    - `ClaimCurrent` → one `SetDisplayNameAsync(current)`.
    - `AssignGuest` → tries the current name first if it is a guest name, then random guest names, at most 5 tries; it stops on any result other than `Taken`.
    - When all 5 tries are `Taken`, it logs one warning with the user id only. It never throws for `Taken` or `Failed`.
- `Services/Local/LocalProfileStore.cs:58`: `SetDisplayNameAsync`:
  - `Invalid` when the name is not storable or there is no profile.
  - `Ok` with no write when the name is the same and already claimed.
  - Otherwise writes `DisplayName` + `NameClaimed=true` with Mutate → `Ok`, or `Failed` if the file write fails (rolled back).
- `Services/Firebase/FirestoreProfileStore.cs`:
  - `:63` `SetDisplayNameAsync` transaction (details in the next section).
  - `:148` `DeleteAsync`: if `NameClaimed`, reads `names/{DisplayName}` and deletes it when `uid` matches, before the ledger and the user document.
  - `:225` `FromFields` maps `nameClaimed` (false when the field is missing).
- `UI/MainMenuUIHandler.cs:156` `ConfirmName`:
  - Calls `ChooseNameAsync`; an exception becomes `Failed`.
  - The button is re-enabled on every result.
  - `nameError = NameFlow.ErrorText(result)`; `Ok` → `Refresh()` (lobby).
- `UI/LobbyAccountUI.cs`:
  - New fields `nameText` and `nameHint` (`:23-24`).
  - `Refresh` (`:78-87`): the input, Save and NameError are visible only when `CanChangeName`; otherwise NameText (the plain name) and NameHint are visible.
  - `SaveName` (`:124`): same four results and texts; the button is re-enabled on every path; on `Ok` it calls `SyncNickName()` + `RefreshCurrent()`.
  - `LinkGoogle`: `Linked` → `linkStatus = "İsmini seçebilirsin"` (`:176`), and `RefreshCurrent` opens the name box because `CanChangeName` becomes true.
  - `OnDestroy` is unchanged.
- Scene `LobbyMenu.unity`, under `ProfilePanel/Box`:
  - `NameText`: copy of `AccountText` (same font and material), at (0,-165) 700x84, auto-size max 52. It takes the place of the input row.
  - `NameHint`: copy of `LinkStatus`, at (0,-232) 700x40, text "İsim seçmek için Google'a bağlan". It sits in the NameError slot; NameError is hidden whenever the hint shows.
  - Both are wired through `SerializedObject`. `nameText=NameText` and `nameHint=NameHint` were read back as non-null.
- `firebase/firestore.rules`: the plan's `match /names/{name}` block was added verbatim after the `users` block. Nothing was published.

### Firestore transaction: read and write set (`SetDisplayNameAsync`)

Inputs: `uid`, the new `name`, and `oldName`. `oldName` is the current `DisplayName` only when `NameClaimed` is true and the name differs; otherwise it is null.

Reads (all reads come before any write):
1. `names/{name}`. If it exists with `uid != ours`, the transaction returns false → `Taken`, with no writes.
2. `names/{oldName}`, only when `oldName != null`.

Writes:
- `names/{name}` Set `{ uid, createdAt: ServerTimestamp }`, only if it did not exist. If we already own it, nothing is written (the rules allow no update).
- `names/{oldName}` Delete, only if it exists and `uid == ours` (the rules allow only the owner to delete).
- `users/{uid}` Update `{ displayName: name, nameClaimed: true, lastSeenAt: ServerTimestamp }`. The `coins` field is still in the resulting document, so the users rule still holds.

The name record and the profile change in one atomic commit, so neither can end up pointing at the other alone. Memory is updated (`DisplayName`, `NameClaimed`, `Notify`) only after the commit succeeds.

Errors:
- An exception (network, permissions, or an invalid document id such as `..` or `__x__`) → `Failed`.
- It logs one warning: `[Profil] isim kaydedilemedi: <ExceptionType> <uid>`. No name and no message text are logged.

### TDD evidence

RED: I wrote the tests first and ran `powershell -ExecutionPolicy Bypass -File Tools\SyncTest\Run-EditModeTests.ps1 -Filter Tests`:
```
COMPILE FAILED
Assets\Osman\Editor\Tests\LocalServicesTests.cs(373,12): error CS0246: The type or namespace name 'NameResult' could not be found
Assets\Osman\Editor\Tests\GameConfigTests.cs(38,72): error CS0246: The type or namespace name 'NameFlow' could not be found
...
```
The RED is a compile failure: the new types and members did not exist yet. No failing-assertion stage was captured.

GREEN, same command after the implementation: `passed 186/186`.
- The count went from 159 to 186: 4 tests removed (3 `DefaultName` tests and `NeedsName_TrueForDefaultName_FalseAfterRename`), 31 added.
- `GameConfigTests`: `GuestName` ×2, `IsGuestName` ×6, `IsValidName` ×6, `NameFlow_Next` table ×8, `NameFlow_ErrorText` ×4.
- `LocalServicesTests`: new profile is not claimed; Ahmet → Ok, claimed and persisted; 17 characters → Invalid and unchanged; the same name again → Ok.
- `GameServicesTests.LocalGuest_HasClaimedGuestName_CannotChooseName`:
  - `IsGuestName` and `NameClaimed` are true; `NeedsName` and `CanChangeName` are false.
  - `ChooseNameAsync("Ahmet")` → `Invalid`, and the name is unchanged.
- `InitGateTests.FirestoreFields_MapToProfileWithDefaults`: `nameClaimed` true is mapped; a missing field gives false.

### Play mode values read back (Editor, local backend)

- Profile file before: `"DisplayName":"Deneme Oyuncu"` with no NameClaimed field. A backup is at `...\scratchpad\nm-profile-before.json`.
- After the first play: `"DisplayName":"Misafir-58823","NameClaimed":true`. The repair path works, and it ran in play mode before any EditMode test touched the file.
- `chip='Misafir-58823 · Seviye 1'`, `nick='Misafir-58823'`, `needsName=False`, `inLobby=True`. I was not asked for a name at start.
- Profile panel:
  - Hidden: `input=False save=False err=False`.
  - Shown: `nameText=True:'Misafir-58823'`, `hint=True:'İsim seçmek için Google'a bağlan'`.
  - `signout=True`, `link=False`, `canChange=False`.
- Screenshots (both read; no overlap, nothing cut off): `...\scratchpad\nm-b-lobby.png` and `...\scratchpad\nm-b-profile.png`.
- Console errors during play: none. The only new errors in the session are the intended RED compile errors and the known test-subscriber exception `abone-hatasi` from `LocalServicesTests`.

### Gates

- EditMode: `passed 186/186`.
- Solo sync (`Run-SyncTest.ps1 -Solo -Mode smart -Seed 11`, Editor stopped, MainMenu open), first run: `PASS: 1 el, test oyuncularında hata yok (177 sn)`, desync 0. Log: `...\scratchpad\nm-solo.log`.
- Secret scan: `secrets: clean (16 files)`.
- After the sync test, the Editor is stopped with `Assets/Osman/Scenes/MainMenu.unity` open and not dirty.

## Judgment calls

1. The plan names the decision outcomes `Yok / MisafirAta / BugünküyüKaydet`. I named the enum values `NameFlow.Step.None / AssignGuest / ClaimCurrent` to match the English identifiers the codebase uses (`EntryFlow.Screen`, `LinkResult`). The Turkish meaning is in the comment.
2. I added the helper `GameConfig.IsStorableName` (2-16 characters, no '/'). It is the store-side check the plan describes ("biçim denetimi yalnız 2-16 ve /"), and `IsValidName` reuses it, so the check is not duplicated.
3. `LocalProfileStore.SetDisplayNameAsync` returns `Failed` when the file cannot be written. The plan says "Ok ya da Invalid", but reporting `Ok` for a write that was rolled back would be wrong.
4. The four result texts live in `NameFlow.ErrorText` so that both UIs use one source. They are tested.
5. `EnsureNameAsync` tries the name the new profile was created with first, when it is already a guest name. This avoids a second rename on a fresh profile.
6. `NameHint` and `NameError` share one slot. They are never visible together: NameError is hidden when `CanChangeName` is false.
7. `FirestoreProfileStore.DeleteAsync` releases the name record only when `NameClaimed` is true. A name that was never claimed is not this account's record.
8. I changed the header comment of `GameServicesTests`: the old text talked about the removed rename test.

## Not verified

- All Firebase paths are untested: the transaction (`Taken`, `Failed`, releasing the old name), deleting the name record in `DeleteAsync`, the `nameClaimed` round trip, and the new rules. The Editor uses the local backend, and the rules are not published (the main session does that).
- I could not exercise the name panels for Google accounts in the Editor:
  - The MainMenu name panel.
  - The Profile name box after `Linked`, with "İsmini seçebilirsin".
  - The `Taken` and `Failed` texts.
- For those paths I checked the code by reading it and through the pure `NameFlow` and `ErrorText` table tests.
- Device test (APK) is left to the main session.

## Known limits

- Firestore document ids that are valid names but rejected ids (`..`, `__foo__`) return `Failed` ("Bağlanılamadı"), not `Invalid`.
- A Google account whose current-name claim fails with a network error is asked for a name on that launch, because the name is not claimed yet.

## Fix round 1 (commit `858f609` "Benzersiz isim: gözden geçirme düzeltmeleri")

### 1. The transaction takes the old name from the server (`FirestoreProfileStore.cs:63-128`)

The early return is unchanged: if the name is the same as in memory and memory says it is claimed, the method returns `Ok` without reading or writing. Otherwise the transaction runs in this order, with every read before any write.

Reads:
1. `users/{uid}` (`:81`).
   - If the document does not exist, return `Failed` with no writes. One warning is logged after the transaction: `kullanıcı belgesi yok <uid>`.
   - The server's `displayName` and `nameClaimed` come from `ToDictionary()`.
   - If the server already has this name claimed for this user, return `Ok` with no writes. Memory is then resynced (`:123-127`); this is the recovery for case (a).
   - The old name is the server `displayName`, and only when the server says `nameClaimed` is true.
2. `names/{new}` (`:92`). If it exists and its `uid` is someone else's, return `Taken` with no writes.
3. `names/{old}` (`:95`), only when there is an old claimed name. It cannot be the same as the new name, because that case already returned `Ok`.

Writes:
1. `names/{new}`: `Set { uid, createdAt }`, only when the document does not exist. An existing document is never written.
2. `names/{old}`: `Delete`, only when it exists and its `uid` is this user.
3. `users/{uid}`: `Update { displayName, nameClaimed: true, lastSeenAt }`.

Reading `users/{uid}` inside the transaction means two renames of the same user now touch the same document. Firestore detects the conflict and reruns one of them, which then derives the old name again (case b).

Rules re-check (read only, against `firebase/firestore.rules`):
- `users` get is the owner read. `names` get only needs `auth != null`.
- A `names` create happens only for a document that does not exist, with `uid` equal to the auth uid.
- There is never an update to a `names` document.
- A `names` delete happens only for a document that exists and is owned by this user.
- The `users` update keeps `coins`, so `coins is int && >= 0` still holds.

**This is unverified at runtime. No Firestore path can run in the Editor, and the rules are not published.**

### 2. `DeleteAsync` deletes the name and the user document in one batch (`FirestoreProfileStore.cs:156-188`)

Order:
1. Ledger documents, one by one, as before.
2. A `WriteBatch` (`db.StartBatch()`):
   - Read `users/{uid}`. When the server says `nameClaimed` with a `displayName`, read `names/{displayName}`. If it exists and is owned by this user, add `batch.Delete(names/{name})`.
   - Add `batch.Delete(users/{uid})`.
   - `CommitAsync`: both deletes succeed or fail together.

Judgment call: the name to delete comes from the server's user document, not from memory. This is the same reasoning as fix 1: memory can be stale after a lost commit reply. It costs one extra read.

This is also unverified at runtime.

### 3. `GameConfig.IsStorableName` (`GameConfig.cs:20-34`)

It now also rejects:
- `..` (`.` was already rejected by the minimum length).
- Names of 4 or more characters that start and end with `__`.
- Any `char.IsControl` character.
- Any character whose `UnicodeCategory` is `Format`.

The control and format checks run on the trimmed name. `IsValidName` uses `IsStorableName`, so chosen names are covered. Guest names pass through the same store check.

New table tests in `GameConfigTests.IsStorableName_RejectsBadDocumentIds`:
- False: `"."`, `".."`, `"__ab__"`, `"a​b"`, `"a\0b"`, `"a\tb"`.
- True: `"a.b"`, `"_ab_"`, `"Ali Veli"`.

### 4. Lobby hint after any successful Google result (`LobbyAccountUI.cs:174-177`)

The `else if (result == LinkResult.Linked)` branch is now `else if (GameServices.NeedsName)`. The branches before it handle `Cancelled` and `Failed`, so this branch covers `Linked` and `SwitchedToExistingAccount`. The text is still "İsmini seçebilirsin".

### Commands and output

- `powershell -ExecutionPolicy Bypass -File Tools\SyncTest\Run-EditModeTests.ps1 -Filter Tests` → `passed 195/195` (186 + 9 new cases). The new tests were written together with the fix; no separate RED run was captured for them.
- `editor status`: `"compiling": false`, `"playMode": "stopped"`. The active scene is `Assets/Osman/Scenes/MainMenu.unity`, not dirty.
- `Tools\Security\Check-Secrets.ps1` with the 4 files staged → `secrets: clean (4 files)`.
- The commit contains only the 4 files: `GameConfigTests.cs`, `FirestoreProfileStore.cs`, `GameConfig.cs` and `LobbyAccountUI.cs`.
- No sync run, as requested.
