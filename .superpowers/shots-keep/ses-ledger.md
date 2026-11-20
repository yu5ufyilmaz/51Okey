# SDD ledger — plan: docs/superpowers/plans/2026-11-19-ses-sistemi.md
Spec: docs/superpowers/specs/2026-11-19-ses-sistemi-design.md. Branch MDC-001-firebase (no worktree). EditMode baseline 305. Clone editors are CLOSED (user asked): multi-editor sync test needs them reopened; solo test until then. Main editor restarted (3.1 GB).
User approved plan + asked controller to download the five Kenney packs ("Sen indir").

## Pre-flight scan
| Pair / task | Produces vs consumes | Finding |
|---|---|---|
| T1→T2,T3,T4 | Sfx.Play/PlayAt/Recent/UseLibrary/OtherVolume, SfxId | consistent |
| T1→T5 | SfxSetup.Defaults/Build; T5 changes Build rule to "fill empty clips" | consistent (T5 states the change) |
| T2 test helper AllIds() used by T4 test | same test file | ok |
| T3↔T4 | both edit TileUI.cs and TileDistrubite.cs | sequential; T4 adds Error/MeldShown/TileProcess calls, T3 owns pick/drop/snap/draw/discard |
| T3 rule "SnapBack not on rejected discard/process" vs T4 "Error there" | complementary | ok |
| T1 self | Play_With_Missing_Entry expects Recent empty; order in plan = entry lookup before Recent | ok |
| T1 self | Gate rule not stated in prose; tests determine: allow iff never played or now-last > minInterval | Ruling: that rule — tests are the spec — none |
| T5 | depends on downloaded packs outside repo | controller provides path |
Scan clean otherwise.

## Log
Packs downloaded by controller via built-in browser (kenney.nl "Continue without donating"), copied to scratchpad/kenney/ and extracted, one dir per pack: casino-audio 876,839 B (55 ogg), impact-sounds 800,850 B (130 ogg), interface-sounds 834,536 B (100 ogg), ui-audio 411,949 B (52 ogg), music-jingles 1,239,525 B (86 ogg). Each License.txt says CC0, commercial use ok, credit optional. Browser left five *.tmp copies in C:\Users\osman\Downloads (not deleted).
Task 1: implemented 10c7a80 (314 EditMode). Review dispatched; Task 2 implementer alongside (reviewer read-only).
Task 1: complete (commits 567b1da..10c7a80, review clean). EditMode 314.
Task 1: carry into Task 4 (it edits Sfx.cs anyway): quit-time guard (Application.quitting → no new audio object), gate negative-delta guard (now >= t), Mathf.Clamp01 on volume, SfxLibrary.Find null-entries guard.
Task 1: minor (deferred): shared AudioSource.pitch bends still-playing one-shots (pool of sources if audible on phone); failed Resources.Load retried every Play; Recent returns live list; test libs never destroyed; SfxSetup overwrites an asset that exists but fails to load; no test for the SyncAutoTest guard
Task 2: implemented 16fed47 (322 EditMode; 43 UiSfx; layout multiset unchanged). Review dispatched; Task 3 implementer alongside. Selection proposal ready: .superpowers/sdd/2026-11-19-ses-sistemi/sfx-selection.md (28/28 picks, loudness unmeasured → measure in Unity in Task 5).
Task 2: review 10c7a80..16fed47 → Needs fixes. Important: UiSfx runs after Button on the same object; buttons that disable themselves in onClick (Guest/Google, NameOk, Retry, NameSave, LinkGoogle, SignOut) go silent because IsInteractable() is already false. Fix round 1 (queued until Task 3 implementer releases the Editor — a recompile during its play/solo run would break it): record interactability in OnPointerDown, play in OnPointerClick if it was interactable; left button only.
Task 2: minor (deferred): UiSfx on a GameObject destroyed by its own onClick; UiKitTable matches by name only, no duplicate warning, saves after error; Sound() first-Selectable heuristic
Task 3: implemented 8dbd6d3 (322 EditMode, solo PASS 181 sn, 14 calls). Review (opus) dispatched. Task 2 fix round 1 sent.
Task 2: fix round 1 implemented 7d96bed (325 EditMode). Re-review dispatched.
Task 3: complete (commits 16fed47..8dbd6d3, review clean, opus). SimulateDragCore byte-identical to old body; +37/-0.
Task 3: carry into Task 4: Sfx must not use UnityEngine.Random (shared with the seeded deal / seat order) → private System.Random in Sfx. Ruling: fix now — a sound call must never be able to perturb gameplay randomness — cost: none.
Task 3: minor (deferred): buffered draw/side-pick/bot-discard RPCs would replay sounds on rejoin (no rejoin today); opponents hear no discard sound for the finishing discard (Win/Lose covers it); pile tile dropped on indicator slot plays TileDraw then TileSnapBack (pre-existing visual oddity)
Task 2: fix round 1/5 (1 addressed, 0 open; commits 8dbd6d3..7d96bed)
Task 2: complete (commits 10c7a80..16fed47 + 7d96bed, review clean after round 1). EditMode 325.
Task 2: minor (deferred): a button whose onClick deactivates its own GameObject never gets OnPointerClick on UiSfx (silent); keyboard/gamepad submit has no sound
Task 4: dispatched (BASE 7d96bed), includes Sfx hardening carried from Task 1 and 3 reviews.
Task 4: implemented 10ec383 (331 EditMode, solo PASS 184 sn, 32 calls of which 17 Error). Review (opus) dispatched; Task 5 implementer alongside (disjoint files). Ruling: Task 5 may set entry volumes from loudness measured in Unity (selection agent could not measure) and writes the same values into SfxSetup.Defaults — plan table volumes were guesses — cost: Osman retunes by ear anyway.
Task 4: complete (commits 7d96bed..10ec383, review clean, opus). No control-flow change found; 17 Error sites verified.
Task 4: minor (deferred → final fix wave candidates): take-back not-your-turn return silent (ScoreManager ~684); open buttons silent when limit passes but nothing is placed (~589, ~660); Sfx quit flag never reset if Enter Play Mode Options (no domain reload) is enabled; Win then Lose both play if a win RPC races a draw RPC (pre-existing race); TimeLow can fire again after a mid-turn timer reset; Error on a quick second discard during the discard lock; buffered RPC replay sounds on join
Task 5: implemented 4677425 (334 EditMode; 35 ogg, 364 KB; audit 0/0/0). Note: error.ogg renamed error_1.ogg because LocAudit's key regex matched "error.ogg" → add audio extensions to LocAudit's skip list in the fix wave. Review dispatched; controller runs solo test on 4677425.
Task 5: complete (commits 10ec383..4677425, review clean). All 35 files byte-identical to their Kenney sources (sha1), all listed in KAYNAK.md.
Task 5: minor (deferred → fix wave candidates): Build refills an entry emptied on purpose (should skip SilentOnPurpose); MeasureAll public, unused, rewrites import settings without try/finally; Every_Event test loops lib.entries (a deleted entry passes) — count against the enum; LocAudit should skip audio extensions so error.ogg can keep its name; UiPlayNow only ~3 dB below discard; TileSnapBack at floor still loud; TilesCollect/TilesDeal capped and 3-8 dB quiet (by ear)
Final whole-branch review dispatched on 567b1da..4677425 (opus).
Solo test on 4677425: PASS: 1 el, test oyuncularında hata yok (180 sn).
Final review (opus) on 567b1da..4677425: With fixes. Gameplay diff audio-only and safe. Required: I1 missing Error (nothing placed in PlaceValidPers/PlacePairPers; take-back not my turn), I2 SfxSetup.Build must never touch existing rows, I3 quitting flag sticks after a Play session and breaks EditMode tests, I4 real playback path — reviewer did not know Tasks 3-5 ran play mode without autotest (Recent read; AudioSource.isPlaying True in Task 5); rerun once after the wave. Same wave: M1 two AudioSources (jitter / fixed pitch), M2 Win/Lose only on first FinishGameRPC, M5 Inspector ranges, M6 MeasureAll → menu with try/finally, M7 no overwrite of an unloadable asset, M10 enum uniqueness test, M11 LocAudit skips audio extensions.
Reviewer on the missing multi-editor test: matters little — under SyncAutoTest every Sfx call returns immediately; run it when clones reopen, not a blocker.
Fix wave dispatched (BASE 4677425).
Fix wave: implemented 3c707e8 (337 EditMode, audits clean, solo PASS 182 sn, all findings fixed). Open question: implementer saw TilePickUp/TileDrop repeat with nobody touching the game in its first play run. Scoped re-review (opus) dispatched incl. that question.
Fix wave re-review (opus) on 4677425..3c707e8: CLEAN, all 12 ADDRESSED, no new breakage. Win/Lose guard proven safe (all isGameEnded writers listed). No code path plays TilePickUp/TileDrop without pointer input.
Residual minors (parked): Build saves unconditionally; quit hook lazy (quit before first sound); File.Exists refusal untested; draw plays Lose for everyone.
Task 6: notes written to spec, committed. Multi-editor sync test NOT run (clones closed by user request).
