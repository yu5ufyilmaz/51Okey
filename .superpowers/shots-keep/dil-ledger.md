# SDD ledger — plan: docs/superpowers/plans/2026-11-19-dil-sistemi.md
Spec: docs/superpowers/specs/2026-11-19-dil-sistemi-design.md. Branch MDC-001-firebase (no worktree: Unity project + clones share Assets; same as earlier packages). Docs commit d09fc9b. EditMode baseline 248.

## Pre-flight scan
| Pair / task | Produces vs consumes | Finding |
|---|---|---|
| T1→T2,T3,T5 | LocTable.Parse/Languages/Keys/DuplicateKeys/Get | consistent |
| T2→T4,T6,T7,T8 | Loc.T/Load/Language/Culture/Changed/NameOf | consistent; Loc.Next added in T6 (not in T2 block) — fine |
| T2→T5 | LocText.key | consistent |
| T3↔T2 | audit regex sees "lang.name" literal in Loc.cs; row exists | ok |
| T3↔T7 | constraint "keys are literals" vs JoinErrorText/NameFlow returning keys | plan already rules: literal at return site, variable Loc.T allowed there |
| T5↔T7 | UiKitLobby rounds field left blank by builder, filled at runtime in T7 | ok |
| T5↔T6 | both rerun Hesap Pencerelerini Kur | ok, T6 after T5 |
| T7↔T8 | SeatManager.cs:105 listed in both | T7 owns it; T8 skips |
| T1 self | tests vs interface | ok |
| T2 self | Load(null,null)=reload from Resources on next use; tests rely on it | ok |
| T3 self | Project_Table_Has_No_Errors at T3 time: table has only lang.name | ok (lang.name exempt from unused) |
| T4 self | RPC byte; OkeyRulesTests updated | ok |
| T5–T9 self | — | ok |
Scan clean, no rulings needed.

## Log
Task 1: complete (commits d09fc9b..5f28caa, review clean). EditMode 256.
Task 1: minor (deferred): delimiter detection reads first physical line only; comment/blank line before header breaks `;` files
Task 1: minor (deferred): unterminated quote swallows rest of file silently (audit should catch: key count drop)
Task 1: minor (deferred): duplicate language column not reported; Get(lang) case-sensitive
Task 1: minor (deferred): edge tests missing (CR-only, quoted header, trailing delimiter, unterminated quote, `;`+quoted `;`)
Task 2: implemented ec80b07 (272 EditMode), review dispatched. Ruling: Task 3 implementer runs while Task 2 review reads (T3 touches only new Editor files + LocTable; reviewer is read-only) — saves wall-clock — if T2 needs fixes they wait until T3 commits (one Editor).
Task 2: complete (commits 5f28caa..ec80b07, review clean). EditMode 272.
Task 2: minor (deferred): T(key,(object[])null) throws ArgumentNullException (not caught)
Task 2: minor (deferred): Language setter compares before table load (stale after Load(null,null); spurious Changed on cold start) — fix `_ = Table;` first
Task 2: minor (deferred): throwing Changed handler stops others; Load(real table) does not fire Changed; warned-set shared between missing-key and format; missing asset never logged; Language="TR" ignored
Task 2: minor (deferred): tests to add — warn once, null args, "zz" culture, empty-key LocText
Task 3: implemented 282c5f0 + pre-review fix 772d428 (283 EditMode; leftover 90, missing glyphs 0). Ruling: plan's HasCharacters glyph check is useless on a dynamic font; my replacement Font.HasCharacter also useless (always true); implementer used FontEngine.LoadFontFace+TryGetGlyphIndex, proven on 'Ж'. Review dispatched; Task 4 implementer runs alongside (reviewer read-only). Ruling: controller runs the multi-editor sync test for Task 4 itself (clone dialog handling), implementer does EditMode only.
Task 3: review ec80b07..772d428 → Needs fixes. Important 1 (plan-mandated): used-key regex treats "common.cs"/"error.log"-like strings as keys → false gate failure. Ruling: fix by skipping candidates ending in a file extension — cheap, keeps gate trustworthy — costs nothing if wrong. Important 2: placeholder regex ignores {0:D2} → broken table can pass. Fix round 1 queued until Task 4 implementer releases the Editor.
Task 3: minor (deferred): `throw`/`Debug.Log` matched by Contains not word boundary; /* */ not skipped; TurkishLiteral matches across two literals; MissingChars fallback useless + ignores TMP fallback fonts; LoadFontFace leaves FontEngine on this face (Editor only); no tests for MissingChars/exclusions
Task 4: implemented 211a1fb (286 EditMode). Also changed Loc.cs setter order (fixes Task 2 deferred minor: stale language after Load(null,null)). Review dispatched; sync test pending (controller). Task 3 fix round 1 sent to implementer.
Task 3: fix round 1/5 (2 addressed, 0 open; commits 211a1fb..0bbd66a). EditMode 289.
Task 3: complete (commits ec80b07..772d428 + 0bbd66a, review clean after round 1)
Task 3: minor (deferred): {{0}} escaped braces counted as placeholder
Task 4: review 772d428..211a1fb → Approved (opus). Sync test running (controller).
Task 4: minor (deferred): PenaltyReason values are wire format — give explicit numbers + "append only" comment; add test that Language set after Load(null,null) sticks
Ruling: Localization.csv is LF in the working tree, not CRLF as the plan said — parser handles both, Excel reads LF, git autocrlf would fight CRLF — keep LF, drop the CRLF rule from later dispatches.
Task 4: sync test PASS: 1 el, ok 50, desync 0 (350 sn) on 0bbd66a. Task 4: complete (commits 772d428..211a1fb, review clean). BASE for Task 5 = 0bbd66a.
Task 5: implemented b57e8c7 (289 EditMode, 36 keys). Review dispatched with code-only diff (scene/prefab diff is fileID churn; implementer's multiset layout check: 0 changes). Note: Editor system language is en → Editor play mode now starts in English. Task 6 implementer runs alongside.
Task 5: complete (commits 0bbd66a..b57e8c7, review clean). m_text multiset identical except rounds value; all 37 keys landed in assets.
Task 5: carry into Task 7: (a) texts that have LocText AND are overwritten at runtime (ButtonFuncs.cs:275 emptyTitle "Açık masa yok"/"Masalar yükleniyor...", name hint) — LocText.Refresh clobbers them on language change/re-enable; runtime must switch LocText.key or the builder must not put LocText there. (b) common.yes / common.ok rows not added yet — Task 7 adds them with UIRoot defaults.
Task 5: minor (deferred): SetLabel re-parses CSV per call; "anahtar yok" message misleading when CSV asset fails to load
Task 6: implemented 27801f0 (290 EditMode). Controller looked at settings-en/tr screenshots: row works, delete row visible; window body grown 900→1034 (near full height, empty space when legal rows hidden) — note for user. Review dispatched; Task 7 implementer alongside.
Task 6: review b57e8c7..27801f0 → Needs fixes. Important (plan-mandated): SettingsBodyHeight 1034 overflows logical canvas height on 20:9 (966) / 19.5:9 (978) phones; old 900 was sized for 966. Ruling: plan's "grow the body" was wrong for phones — body back to 900; privacy + terms share one row as two half-width link cells (same pattern as Ses/Müzik) so worst case is 5 rows = 656 ≤ 666 — costs a layout change the user has not seen, flagged in final summary. Fix round 1 queued until Task 7 implementer finishes (same files).
Task 6: minor (deferred): OnEnable does not call RefreshLanguage; ChangeLanguage saves even when unchanged; language stepper buttons named Minus/Plus; Next() tests for single/empty list
Task 7: implemented 65f5857 (291 EditMode, 62 keys). Review dispatched. Task 6 fix round 1 sent (incl. en 'Rounds: {0}' to avoid '1 rounds').
Task 7: review 27801f0..65f5857 (opus) → one Important: shop.active tr "Aktif" vs old literal "Active". Ruling: accepted, no fix — the plan itself (Task 8 table) specifies shop.active = Aktif/Active; "Active" was an untranslated English leftover in a Turkish UI, and the design doc lists it as such — cost if wrong: one cell. Flag to user in summary.
Task 7: complete (commits 27801f0..65f5857, review clean after ruling). EditMode 291.
Task 7: minor (deferred → Task 9 table polish): error.room_exists en too long ("Table name already taken"); account.sign_out_guest "is lost"→"will be lost"; common.quit_title "Exit" vs button "Quit"
Task 7: minor (deferred): nameError not re-translated on language switch + RefreshLanguage overwrites unsaved nameInput; ButtonFuncs.cs:294 unguarded GetComponent<LocText>() per redraw; duplicate key→text helper (MainMenuUIHandler:170 / LobbyAccountUI:143); NameFlow Failed reuses login.connection_failed
Cannot-verify (phone): GetCultureInfo("tr") dotted-İ casing under IL2CPP/Android
Task 6: fix round 1 implemented dd42304 (291 EditMode). Controller viewed settings-all-rows-20x9.png: title, X, 5 rows, version all inside at 2400x1080. Re-review dispatched; Task 8 implementer alongside.
Task 6: fix round 1/5 (1 addressed, 0 open; commits 65f5857..dd42304)
Task 6: complete (commits b57e8c7..27801f0 + dd42304, review clean after round 1). BASE for Task 8 = dd42304.
Task 8: implemented e80cbba (291 EditMode, solo PASS, 19 keys, leftover 14 justified). Controller viewed table-en/tr screenshots: button labels correct and fit in both languages. Review (opus) dispatched.
Task 8: complete (commits dd42304..e80cbba, review clean, opus). Sync test running on e80cbba (clones refreshed, 3 Reload dialogs clicked, clones verified on latest code).
Task 8: minor (deferred): table.waiting_player unused at runtime; UiKitTable binds extra matches silently (found != count) and saves after errors; duplicated normaliser UiKitLobby.cs:331; ShopController LocText branch dead + misleading comment
Task 8: minor (deferred → Task 9 table polish): en "Threshold: {0}" → "To open: {0}"; "Not enough to open! (Hand: {0}, needed: {1})" → "Points" instead of "Hand"
Sync test on e80cbba: two FAILs first (run 1: host "status yok frame=1 ticking=False"; run 2: AT-g1 "Connecting durumunda 60 sn takıldı"), then PASS: 1 el, ok 50, desync 0 (340 sn) with no code change. Cause: cold editors after the scene/prefab reimport (first play slow); a clone alone connects in ~10 s. Missing-script (Unknown) x4 warnings and ExitButton/SceneChangeManager NRE at play stop are pre-existing noise. One "DisconnectByServerTimeout ... ConnectingToMasterServer" on host after the game ended (returning to lobby) — network flake, verdict PASS. Lesson: warm each editor (enter/leave play once) after big reimports before Run-SyncTest.
Task 9: dispatched (verification + English table polish from deferred minors). Mixed-language live penalty check: controller.
Task 9: implemented 9389f5b (291 EditMode; audit 0 errors 0 warnings, leftover 16 justified, missing glyphs 0; 8 en cells changed; ; round trip 119 keys identical). Controller viewed sweep-lobby-cards-2400, sweep-create-table-1920, sweep-login-1920: fine. Finding for fix wave: create-table stepper value shows 'Rounds: 1' beside the label 'Rounds' (caused by controller's 'Rounds: {0}' ruling) → separate key for the stepper value (tr '{0} el', en '{0}'). Task 9: complete (verification task, no separate review; final review covers it).
Mixed-language live check (controller, on 9389f5b): host tr saw "+100 / Okey attı", clone_0 en saw "+100 / Discarded the okey" for the same AnnouncePenalty RPC. PROVEN. That run then FAILED "AT-host: atma zaman aşımı" (controller's 4 mid-game script evals froze the host; probe-induced, not rerun-proven).
Plain sync rerun on 9389f5b: FAIL host "status yok frame=1 ticking=False". Root cause found in Editor.log: entering play took a 56 s domain reload (LoadAssemblies 43 s) — main editor private memory 12.6 GB, machine RAM free 0.5 / 15.6 GB, four editors swapping. Environmental. Restarting the main editor was denied by the permission classifier → ask the user. Last PASS stands on e80cbba (text-only commit 9389f5b after it).
Final review (opus) on d09fc9b..9389f5b: With fixes. Required: (1) audit scene/prefab LocText keys, (2) build gate IPreprocessBuildWithReport, (3) SeriScore/CiftScore Turkish scene defaults before deal, (4) '?'-corruption heuristic, (5) Loc.CodeOf for all SystemLanguage, (6) mixed-language check — done above, (7) stepper value key. Fix wave dispatched with those + cheap minors (header validation, ANSI byte test, TryGetComponent, shop active label LocText + dead branch, drop table.waiting_player, save-before-set in ChangeLanguage, explicit PenaltyReason numbers, en polish x3, ButtonFuncs null guard).
Fix wave: implemented 58f3c55 (305 EditMode, audit 0/0), all 14 fixed. Scoped re-review dispatched (opus).
Fix wave re-review (opus) on 9389f5b..58f3c55: CLEAN, all 14 ADDRESSED, no new Critical/Important.
Residual minors (parked, no second wave): asset `key:` regex could match a future non-LocText field named key (collect only from LocText m_Script blocks); "used key missing" error does not name the asset file; '?' heuristic false-positives on `page?a=1` URLs inside cells; byte test asserts IsNotEmpty; Chinese variants all map to zh; unreachable seatFields branch in UiKitTable; shop price "14.99 TL" still hard-coded (out of scope).
Sync test on 58f3c55: running (one attempt; environment is memory-starved).
