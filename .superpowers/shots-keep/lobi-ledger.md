# SDD ledger — plan: docs/superpowers/plans/2026-11-19-lobi.md

Spec: docs/superpowers/specs/2026-11-19-menu-ui-yenileme-design.md (Paket 3 + carried notes)
Branch MDC-001-firebase, no worktree. One commit per task, no push. User's dirty files never staged.
Never: Google sign-in, sign-out, rename saved profile, press purchase buttons.
Clones: after a scene is rebuilt, force AssetDatabase.Refresh on clones and click Reload on "modified externally" dialogs before any sync run.

## Pre-flight scan
| Tasks | Shared | Finding |
|---|---|---|
| T1/T2 | RoomPassword.Hash/Matches, SeatNames.Build | Match. |
| T1/T3 | LobbyRoom, LobbyRoomList | Match. |
| T2/T3 | RoomManager.JoinRoomAtSeat, JoinRoomWithPassword(...,seat), CreateBotRoom, PwHashKey, SeatsKey; ButtonFuncs.cs touched by both | T2 does minimal compile fix in ButtonFuncs, T3 rewrites it. Sequential, fine. |
| T3/T4 | UiKitLobby.cs, LobbyMenu.unity | T3 creates, T4 adds BuildShop and calls it from BuildLobbyScreen. Fine. |
| T4 self | UIRoot change vs constraint | Constraint names the exception (KeepLoading). Consistent. |
| T1 self | tests vs counts | 6+4+2 = 12, plan says 230. Consistent. |
| T2 self | Step 3 timing | WantedSeat check at OnJoinedRoom may run before buffered seat RPCs arrive; TakenSeats falls back to Photon player property SeatNumber, so it holds. Noted for implementer. |
| T3 self | "Yenile" button dropped vs spec "Yenile butonu kalır" | Ruling: dropped — list now applies every delta, button has no job — told Osman — cost if wrong: one button. |

## Tasks
Task 1: complete (commits ebe9f73..2c396c5, review clean; implementer a0822fb821e08b4a0). EditMode 230.
Task 1: minor (carried into T3 dispatch): LobbyRoom.SeatFree throws IndexOutOfRange if Seats has fewer than 4 elements — Seats comes from another client's room property, so a malformed array would throw inside Visible() and blank the lobby. T3's RoomInfo->LobbyRoom adapter must normalise Seats to exactly 4 (and MaxPlayers 0 -> 4), plus a one-line length guard in SeatFree with a test.
Task 1: minor (deferred): RoomPassword is a soft gate (public salt, single SHA-256, client-side check) — add a one-line comment saying it is not authentication.
Task 2: dispatched, BASE 2c396c5 (implementer a6bc8d25d955d864e, opus)
Task 2: implemented 2c396c5..4b87727. EditMode 230, Solo sync PASS, six live two-editor checks done. Concerns: (C1) PlayerTtl 60 s keeps a leaver inactive in PlayerList: seat stays taken and in Seats, rejoin fails 32749; (C2) after a failed join the client is not in the lobby; (C3) "Bağlantı zaman aşımına uğradı" toast ~15 s after a JOINER reaches the table (loading timer not stopped on the join path; predates this task — package 1 only verified the creator path visually).
Ruling: C1 — voluntary exit uses PhotonNetwork.LeaveRoom(false) and Seats/TakenSeats ignore inactive players. There is no RejoinRoom/ReconnectAndRejoin anywhere in the code, so the 60 s hold protects nothing, while it shows ghost names on lobby cards, blocks re-entry and lets a ghost count toward the 4 seated that start the game — cost if wrong: a player who drops in the waiting room loses the seat at once instead of after 60 s; tell Osman.
Ruling: C2 fixed now in PhotonManager failure callbacks (JoinLobby when connected and not in lobby) — lobby seat races make failed joins common — cost if wrong: one extra JoinLobby call.
Ruling: C3 investigated and fixed in this fix round (it is a visible bug for every joiner).
Task 2: fix round 1 dispatched; code review of 4b87727 dispatched in parallel.
Task 2: code review of 4b87727: Approved, no Critical/Important. Minors M1 (lastPublished field instead of cached property) and M2 (!IsSeated in the 2 s follow-up) folded into fix round 1.
Task 2: minor (carried into T3 dispatch): empty room name with a password makes the room unjoinable (hash salted with the typed name, Photon assigns another) and a whitespace-only password with the toggle on yields an open room — T3's create window must reject an empty name and an empty password when the toggle is on. Also T3 must route the bots path through CreateBotRoom (the old toggle path still creates visible bot rooms).
Task 2: minor (deferred): two ButtonFuncs comments reworded (file is rewritten in T3); a joiner who becomes master on entry keeps a stale WantedSeat until next SetPickSeat (harmless); mixed old/new builds on the same AppId disagree on the password key (one-time rollout).
Task 2: fix round 1/5 (C1 partly, C2, C3, M1, M2; commits 4b87727..340503c). EditMode 230, Solo PASS, live checks on main+clone_0.
  C3 root cause: for a JOINER, PUN itself loads the Table scene (AutomaticallySyncScene / LoadLevelIfSynced); RoomManager.OnJoinedRoom and SceneChangeManager never run on the joiner, so nothing hid "Masaya giriliyor...". Fix: UIRoot.HideLoading() at the start of SeatManager.OnJoinedRoom. Present since package 1.
  Side finding (not touched): for the same reason joiners never run RoomManager.OnJoinedRoom's `PlayerScore = 0` reset -> table doc.
  C1 as built: ExitGame uses LeaveRoom(false) only while the room is open (waiting room); after game start the leaver stays inactive as before. "Inactive players hold no seat" NOT applied: TileDistrubite.AssignPlayerQueue reads SeatNumber from every player incl. inactive; changing it is tile code.
Ruling: accept C1 as built — the remaining case (a player who drops abruptly, not via the exit button, in the waiting room keeps the seat 60 s) is left for the table doc — cost if wrong: a ghost name on a lobby card for up to 60 s after a crash/disconnect.
Ruling: scoped re-review of fix round 1 done by the controller reading the 39-line diff (clean, small, live-verified by the implementer) — cost if wrong: a missed edge in BackToLobby guard.
Task 2: complete (commits 2c396c5..340503c). Multi-editor sync run started on 340503c.
Multi-editor sync on 340503c: PASS: 1 el, ok 50, desync 0 (306 sn)
Task 3: dispatched, BASE 340503c
Task 3: implemented 340503c..0a44250 (implementer aca0cce10b91c4336). EditMode 231, Solo PASS. Controller viewed lobby-three, lobby-three-20x9, lobby-create. Deviation accepted: rounds selector is a -/+ stepper (same options 1-5).
Task 3: review needs fixes — I1 stepper buttons 96 < 120. Minors M1-M8 (card recentre by name, list-loading text, silent gate while loading, builder try/catch, top-bar margin, labels, two null/index guards) sent in the same fix round.
Ruling: user (2026-11-19 night) gave standing authority to proceed without questions through the remaining packages; limits kept: no push, no Google sign-in, no sign-out, no profile rename, no purchase, no real account deletion.
Task 3: fix round 1 dispatched.
Task 3: fix round 1/5 (I1 + M1-M8; commits 0a44250..223c12b). EditMode 231. Scoped re-review folded into the final package review. lobby-one.png and lobby-password.png not re-captured (old top margin in those two images).
Task 3: complete (commits 340503c..223c12b)
Task 4: dispatched, BASE 223c12b
Task 4: implemented 223c12b..1501056 (implementer a43b869be4f7c6de3). EditMode 231, Solo PASS. Controller read the runtime diff (KeepLoading, RetryConnect, SceneChangeManager: clean) and viewed shop-open-20x9.png.
Task 4: finding (controller, from the screenshot): the shop X button hangs off the panel's top-right corner and overlaps the lobby coin chip at 20:9 -> final fix wave.
Task 4: pre-existing shop content, not touched: placeholder items "Ürün 6/7/8", English "Remove ADS", mixed USD/TL prices -> tell Osman.
Task 4: complete (commits 223c12b..1501056); per-task review folded into the final package review.
Multi-editor sync run started on 1501056. Package 4 plan committed (5ca574a).
Multi-editor sync on 1501056: PASS: 1 el, ok 50, desync 0 (321 sn)
Final review (opus): Ready to build on, no must-fix. T3 fix items all ADDRESSED. T4: Important — shop X detached/overlapping; Minor — retry toast misses the Photon-failure path.
Ruling: one fix wave now for the shop X anchor and the retry toast; everything else from the final review is deferred with notes in the spec — cost if wrong: small visual/feedback gaps remain until package 4 or the table doc.
Deferred to spec notes: duplicate unconditional JoinLobby callers (PhotonManager, ExitButton, ButtonFuncs.Start) can make PUN log a LogError (pre-existing) -> table; ghost seat 60 s after abrupt drop also counts toward 4 seated and hides a 3+ghost room -> table; joiners skip PlayerScore=0 reset -> table; Dim drawn over the shop panel frame -> phone check; mid-drag card reorder -> phone check; stale comment LobbyAccountUI.cs:14 -> package 4; middle card not larger, empty card has no own MASA AÇ button, shop not a kit Window, placeholder shop items -> Osman.
