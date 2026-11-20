# SDD ledger — plan: docs/superpowers/plans/2026-11-19-ayarlar-profil.md

Spec: docs/superpowers/specs/2026-11-19-menu-ui-yenileme-design.md (Paket 4 + three carried-notes sections)
Branch MDC-001-firebase, no worktree. One commit per task, no push. User's dirty files never staged.
User asleep, standing authority to proceed without questions (2026-11-19 night). Hard limits kept: no push, no Google sign-in, no sign-out, no profile rename, no purchase, NO real account deletion (never confirm the second delete dialog).
Clones: after a scene rebuild, force AssetDatabase.Refresh on clones and click Reload on "modified externally" dialogs before a sync run.
Package 3 ended at ae73344 (shop X + retry toast fix wave, UI-only, not yet covered by a multi-editor run; the package-4 final run covers it). Package 3 ledger kept at .superpowers/shots-keep/lobi-ledger.md.

## Pre-flight scan
| Tasks | Shared | Finding |
|---|---|---|
| T1/T2 | GameServices.DeleteAccountAsync, DeleteResult | Match. |
| T1 self | tests | 5 tests, plan says 236. Consistent. Plan allows extracting AccountDeletion.RunAsync for testability. |
| T2 self | builder scope vs BuildLobbyScreen | T2 replaces ProfilePanel/SettingsPanel only; BuildLobbyScreen requires those names and refuses unknown refs into the rebuilt lobby part: T2's avatar must use its own objects (carried note). |
| T2 self | "Hesabımı Sil" row visibility | Shown whenever a session exists, guests included. Consistent with spec (Play requires in-app deletion for any account). |

## Tasks
Task 1: implemented 7d64477..1459630 (implementer a519a6fd4de351961, opus). EditMode 237. Both stores already release the name claim.
Task 1: design defect found via implementer concern: Firebase refuses User.Delete without a recent sign-in (the common case), and as built the profile was wiped first, then "log in again" shown; guests cannot re-authenticate at all.
Ruling: (R1) recency is checked BEFORE anything is deleted (non-anonymous, last sign-in older than 4 min -> NeedsRecentLogin, nothing deleted); (R2) for guests, if identity deletion fails after the profile is gone, the anonymous credential is dropped locally and the result is Ok (orphaned anonymous auth record holds no data); (R3) new result DataDeletedAccountRemains for the rare non-guest case where the profile is gone and the identity delete still failed — cost if wrong: none of this can be exercised on a device tonight; Firebase parts are code-read only -> MUST be tested on a throwaway account on the phone before release.
Task 1: fix round 1 dispatched.
Task 1: fix round 1/5 (R1-R5; commits 1459630..b4a358f). EditMode 239. New API: DeleteResult { Ok, NeedsRecentLogin, Failed, DataDeletedAccountRemains }; IAuthService.NeedsRecentLoginToDelete, AbandonGuestAsync(). NeedsRecentLogin and Failed both guarantee nothing was deleted.
Task 1: review dispatched; Task 2 dispatched in parallel, BASE b4a358f.
Task 1: review of 7d64477..b4a358f (opus): decision logic and tests match the contract; NEEDS FIXES in the Firestore deletion it relies on:
  I1 offline: Firestore writes queue and never complete -> DeleteAsync hangs forever under a no-timeout overlay; queued deletes fire on next launch with no result reported.
  I2 `Failed` can still have deleted ledger docs (deleted one by one before the batch).
  I3 one round trip per ledger doc can outlast the 60 s recency margin -> named user lands on the data-loss path.
  Minors: M6 no GoogleSignIn.SignOut() after deleting a Google account (picker may auto-select); M8 test counters omit abandons / identities; M9 e.Message of Firestore errors may quote names/{displayName}; M7 local store reports Ok even if the file could not be deleted (Editor only); M4 name/wallet writes not serialised with deletion (UI overlay must block them).
Ruling: T1 fix round 2 (I1-I3, M6, M8, M9) runs AFTER Task 2 finishes — a script change now would domain-reload the Editor under Task 2's play-mode checks — cost if wrong: an hour of delay.
Task 2: told to catch the synchronous InvalidOperationException from DeleteAccountAsync.
Device checklist from this review (10 items) to be copied into the spec.
Task 2: implemented b4a358f..4355c1b (implementer a721d21972f76d2be, opus). EditMode 239, Solo PASS. Controller viewed settings-all-rows.png and profile-guest.png.
Task 2: controller finding: with six rows the settings list scrolls and "Hesabımı Sil" is below the fold (880-high window); candidate fix: Ses and Müzik share one row -> five rows fit without scrolling.
Task 2: review dispatched (opus). Task 1 fix round 2 dispatched (Firestore: server reads, single batch, 20 s timeouts, Google client sign-out after delete, type-only logs, tests).
Task 2: review (opus) Approved, no Critical/Important. Scene change proven surgical (385 common YAML docs, 2 changed), all listeners resolve.
Ruling: fix round for Task 2 anyway, after Task 1's fix round 2 frees the Editor: (a) Ses and Müzik share one row so all rows fit without scrolling and "Hesabımı Sil" is always visible; (b) NeedsRecentLogin and DataDeletedAccountRemains are shown as a notice dialog (new tiny UIRoot.Notice = Confirm with a single "Tamam" button) instead of a 2.5 s toast — the text is an instruction the player must be able to read — cost if wrong: one extra tap.
Task 2: minor (deferred): XP bar fill up to ~5 % off at the extremes (96-99 XP looks full); settings window half empty while URLs are missing; confirm stacked over settings shows a double frame (kit-level); fuzzy avatar edge (built-in sprite at 220); back key during deletion offers "quit" (existing no-timeout-loading behaviour).
Task 1: fix round 2/5 (I1-I3, M6-M9; commits b4a358f..2dfc1c2). EditMode 242. Firestore DeleteAsync: Source.Server reads, single WriteBatch up to 498 ledger docs (450-chunks above), 20 s timeouts on profile/identity/abandon, Google client sign-out after deleting a Google account, type-only logs, local store throws if the file cannot be deleted. ALL Firebase parts code-read only. Scoped re-review folded into the final package review.
Task 1: complete (commits 7d64477..2dfc1c2), device testing outstanding.
Task 2: fix round 1 dispatched (Ses+Müzik one row / no scroll; UIRoot.Notice for NeedsRecentLogin and DataDeletedAccountRemains).
Task 2: fix round 1/5 (F1, F2; commits 2dfc1c2..00952d6). EditMode 242. Controller read the UI diff (UIRoot.Notice via Ask(showNo), DeleteAccount branches) and viewed settings-all-rows-20x9.png (five rows, no scroll, Hesabımı Sil visible) and notice-recent-login.png (single Tamam button, text readable).
Task 2: complete (commits b4a358f..00952d6).
Final package review (opus) and multi-editor sync run started on 00952d6.
Final review (opus): Fix first. T1 fix round 2 items addressed except: the 20 s timeout stops WAITING but not the step — a slow deletion can commit after "Hesap silinemedi" was shown (contract violation). T2 fix round 1: all addressed, no new breakage.
Ruling: final fix wave (after the running sync test): (W1) deadline inside FirestoreProfileStore.DeleteAsync — no commit starts after ~15 s — and the abandoned task is observed; (W2) recency by a monotonic in-process record of the last successful Google sign-in/link instead of the device clock vs LastSignInTimestamp (a skewed phone clock could lock a player out of deletion or push them onto the data-loss path); (W3) back key ignored while a deletion is running (quitting mid-deletion leaves a silent outcome) — cost if wrong: W2 makes every restored Google session answer "sign in again first", which is what Firebase would demand anyway.
Deferred (spec notes): Failed toast cannot say "unknown outcome"; settings window half empty until URLs arrive; confirm over settings shows a double frame; settings from the table not delivered (table work); late identity completion after timeout can orphan a starter users doc (rare).
Device checklist (16 items) from the final review to be copied into the spec.
Multi-editor sync on 00952d6: PASS: 1 el, ok 50, desync 0 (308 sn)
Final fix wave: 00952d6..7060150 (W1 commit budget + observed abandoned task; W2 monotonic in-process recency, LastSignInTimestamp removed; W3 UIRoot.BackBlocked during deletion). EditMode 248, Solo PASS. Controller read the full diff: correct.
Ruling: scoped re-review of the final fix wave done by the controller reading the diff — cost if wrong: a missed edge in Firebase code that cannot run here anyway; device testing is the real gate.
Multi-editor sync on 7060150 started. Spec notes + device checklist committed.
