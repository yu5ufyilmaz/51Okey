# Task 10 + 11 report

Status: DONE_WITH_CONCERNS. Commits: `0e367e3` (Task 10 + Task 9 fixes a-c), `6a0e159` (Task 11).
EditMode 426/426 after Task 10, 429/429 after Task 11 (423 + 6). Pester 44/44. LocAudit errors 0.
Solo: Task 10 `-Solo -Mode smart -Seed 11` -> `PASS: 1 el, test oyuncularında hata yok (187 sn)`; Task 11 `-Solo -Mode smart -Seed 11 -SelfRestore` -> `PASS: 1 el, test oyuncularında hata yok (183 sn)` (4 `öz-kurulum eşit` in the journal).
Multi-editor runs (steps 6) not run: controller.

Interruption: an earlier solo run died with the main editor out of memory (machine at 414 MB free with three clones open; Editor.log `Could not allocate memory`). After the controller restarted the editor I found 4 duplicated old tests appended to `RejoinTests.cs` (CS0111); I removed the duplicates (not my code). Profiler recording turned off before the runs.

## Task 9 fixes
- a: `RestoreQueue.FirstToReplay` returns the queue length when serial K is not queued -> `GoLive` drops the whole queue. Test updated, new `Gate_Drops_Whole_Queue_Without_Snapshot_Serial`.
- b: `GameManager.RPC_PrepareNextRound` going live from a new hand: `DesyncDetector.AdoptKey(CurrentRound - 1, 0)` (the new hand's Deal report makes it `round.0`).
- c: `ReconnectFlow.HandOffMaster()`: `SetMasterClient` to an active player whose seat Ctl bit is on; none -> `EndUnrecoverable()`. Commit 1 calls it from `ReconnectFlow.OnMasterClientSwitched` when `!RestoreGate.Live`; commit 2 moves the call into `RejoinHandshake` (when not live or not yet granted) and when the rejoiner finds itself master at table-ready.

## Task 10 (file: method)
- `Photons/RejoinPolicy.cs` (brief verbatim). `Photons/ReconnectFlow.cs`: `DeliberateLeave`, `Active`, `TestHoldSeconds` (editor), `RejoinSaved`, `EndUnrecoverable`, `HandOffMaster`; `OnLeftRoom`/`OnDisconnected` (once, `ShouldReconnect`) -> scene `Reconnect`, `rejoin.reconnecting` layer, hold, attempts: `RejoinRoom` when on master/lobby, else `ReconnectAndRejoin`, else `ConnectUsingSettings` + `RejoinRoom` in `OnConnectedToMaster`; `OnJoinRoomFailed`/disconnect during an attempt -> `RejoinPolicy.OnFailure` (Retry after 2 s / GiveUpKeep -> MainMenu, record kept / GiveUpClear -> EndUnrecoverable); `OnJoinedRoom`: `MatchOver` or no other active player -> EndUnrecoverable, else `PhotonNetwork.LoadLevel("Table")`.
- `PhotonManager.Start` adds `ReconnectFlow`; `OnConnectedToMaster`, `OnJoinRoomFailed` return while `Active`.
- `ExitButton.ExitGame` sets `DeliberateLeave`; `OnLeftRoom` returns when not deliberate and a record exists.
- `MainMenuUIHandler.EnterLobby`: record present -> hide the "Connecting" layer, `UIRoot.Confirm(rejoin.ask_*)`; yes -> `RejoinSaved`, no -> clear, lobby.
- `GameManager.RestartSequence` last hand: master writes room `MatchOver = true`; `DeliberateLeave = true`; record cleared; `LeaveRoom()`. `Awake` logs `[DONUS] kurulum modu`.
- `Editor/UiKitReconnect.cs` (menu `Okey/Sahne/Yeniden Bağlanma Sahnesi`): writes `Scenes/Reconnect.unity` (Main Camera only) only if missing, adds it to Build Settings; second run: no diff (md5 same, `EditorBuildSettings.asset` unchanged).
- Texts: `rejoin.reconnecting`, `rejoin.ask_title`, `rejoin.ask_body`, `rejoin.ask_yes`, `rejoin.game_over`; `table.leave_body` reworded.
- Harness: `SyncAutoTest.TryDrop` sets `ReconnectFlow.TestHoldSeconds` (return: `dropOfflineSec`; noreturn/afterend: MaxValue; afterend released when `release.json` appears); `TickDropped` sets `restoreEntered`; `[DONUS] oyun sona ermiş` -> `gameOverSeen`, Done; every `[DONUS]` line -> `status.rejoinStage`, `status.donusLines` (last 60, with time) and the smart journal `Logs/autotest_<run>_<nick>.log`; `[SKOR]` lines deduplicated (MatchScores re-logs past hands on rejoin). Runner: return mode allowed; digest compare skips dropped editors only when not `return`; afterend dropped editor excluded from the score check; drop summary shows grants, moves after grant, restore mode, last stage; then each editor's last 25 `[DONUS]` lines.

## Task 11 (file: method)
- `Rejoin/HandshakeRule.cs`: `HandshakeDecision`, `HandshakeRule`, `HandshakeBook` (brief signatures).
- `Rejoin/RejoinHandshake.cs` (DDOL, AfterSceneLoad). Events 160 request {round} -> master; 161 snapshot (`SnapshotCodec.Encode`) -> targets; 162 report {round, serial, (int)digest, ready} -> master; 163 grant {round, serial, seats[], amounts[], reasons[]} (master's `MatchScores.current`); 164 again {round, serial}. All reliable, identified by ActorNumber.
  - Client: after rejoin waits for Table + `TileManager(Clone)` + `ScoreManager(Clone)`; master -> hand off, else 160 (also when already live via a new hand, so the master's book knows it). 161: ignored if live; decode failure -> `[DONUS] bozuk fotoğraf` + 160; else `Apply` -> `GoLive(serial)` -> `[DONUS] kuruldu seri k`. Turn boundary (live, not granted): `RebuildLocalRack`, ready = rack ok && open flags ok, digest (built if missing), 162. 163: rebuild rack, check rack/flags/live (fail -> `Enter`, layer, 160), replace `MatchScores.current`, `Granted`, `canDrop = IsPlayerTurn() && hand == 15`, hide layer, `[DONUS] geri alındı`, `GrantedNow`. 164: `Enter`, counter, layer `OverlayKey`, `[DONUS] yeniden n`. Master switch while not granted: me -> hand off; else `Enter` + 160.
  - Master: 160 -> if the seat's bit is on (grant did not hold) `HandleSeatLoss` first, then `Request`. Turn boundary: pending -> one encode of `TableDigestLog.Last` (checked to be this round/serial) to all. 162 -> `Ring.TryGet`, decision on current state (`currentTurnPlayer == que`, `BotRunningFor(que)`), `[DONUS] karar ...` line; Grant -> `SetSeatControl(seat, true)`, 163, `[DONUS] geri verildi koltuk s seri k`; Again -> 164. `OnPlayerLeftRoom` -> `Forget`; becoming master -> new book.
- `TurnManager.NextTurn` end: `RejoinHandshake.OnTurnBoundary()` after `TableDigestLog.AtTurnBoundary()`.
- `GameManager.RPC_PrepareNextRound`: first `wasLive` + `OnPrepareNextRound(wasLive)`; the layer now stays until the grant (Task 9 hid it at the new hand).
- `DesyncDetector.Store` ignores restoring actors; `expected` excludes them.
- `TableDigestLog`: while `RejoinHandshake.Restoring` the line tag is `[OZET-KUR]` (Run-SyncTest only compares `[OZET]`, i.e. keys after the grant).
- `SyncAutoTest`: `GrantedNow` -> `rejoinGrants++`, `movesAfterGrant` baseline, `deal = CurrentRound`, state Playing. Smart oracle: a seat counts as human only if active AND its Ctl bit is on (a returned but not yet granted seat is a bot seat).
- Text: `rejoin.retrying`.
- Runner verdict (return): `rejoinGrants >= 1`, `movesAfterGrant >= 1`, scores equal, desync 0, digests equal (post-grant keys).

## Deviations
1. Pre-game unexpected drop (no record) still goes to the lobby (accepted).
2. `ReconnectFlow` keeps `AutomaticallySyncScene` off during the flow and loads Table with `PhotonNetwork.LoadLevel` after the checks (accepted).
3. The "game over" notice is shown once LobbyMenu is active (a scene change closes open windows in `UIRoot.OnSceneChanged`); title `rejoin.ask_title`, body `rejoin.game_over`. In the harness the dropped editor stays in MainMenu, so `gameOverSeen` comes from the `[DONUS] oyun sona ermiş` log line.
4. The grant's rack/flags failure makes the client send 160 again; the master puts the seat back to bot before re-sending a snapshot (the brief had no failure path for the master).
5. Task 11 "see it fail" was not run separately (tests and code were applied together after the crash); Task 10's was (4 failing).

## Step 5b
`RejoinRecord.Save("yok-oda-0000")`, Play: question open in MainMenu (`.superpowers/shots-keep/donus/soru-en.png`, editor language en; tr text in the CSV, not shot). "Return" via the `yes` button: console `[DONUS] kayıtlı odaya dönülüyor`, `[DONUS] deneme 1 başladı (JoinedLobby)`, `[DONUS] deneme 1 32758 Game does not exist`, `[DONUS] oyun sona ermiş`, `[DONUS] kayıt silindi`; notice open in LobbyMenu (`bitti-en.png`). Second Play: no question.

## Not verified
- Any real rejoin (multi-editor). Grant/Again paths, 32746 retry, `HandOffMaster` with a live human, cold start into a live room.
- Whether Photon accepts `PlayerScore` writes on an inactive actor (the non-master oracle reads it after the grant).

## Round 2 (first real run, after-draw, by the controller)
Failure: `NullReferenceException` in `SnapshotApplier.Apply` line 15 (`td` null) on AT-g1; `kurulum modu` logged twice.
Cause: `ReconnectFlow.OnJoinedRoom` turned `AutomaticallySyncScene` on right before its own `LoadLevel("Table")`; PUN's `LoadLevelIfSynced` (room property update) still saw the active scene `Reconnect` and loaded Table a second time. The first copy received the room-object instantiations and sent the request, then died with them; the second copy never had `TileManager(Clone)`.
Fix (uncommitted, compiled, EditMode 429/429): sync is switched on only after Table is active and the message queue runs (`ReconnectFlow.SyncSceneWhenLoaded`); `RejoinHandshake.TableReady()` (queue running, GameManager in the active scene, td/sm/tm/SeatManager and both room objects present, plus one frame) gates the request; `OnSnapshot` catches a failed `Apply` (or not-ready table): `[DONUS] kurulum hatası n` warning, back to not-live, new request (at most 4 more).
Clones refreshed and verified (`TableReady` present on all three, compiling false). Multi-editor run NOT started: free RAM 1281 MB, then 360 MB (main 4.4 GB, clones 3.5-4.3 GB each).

## Round 3 (multi-editor runs by me)
Commits: `bbfb8af` (no double Table load; table-ready; Apply failure -> retry), `d7f2103` ([ISTAKA] check skipped while restoring: the bot plays the seat after GoLive and the local rack is not updated until the report rebuilds it), `b4b538f` (at grant the master writes the returner's `PlayerScore` from its table: a client that missed the new hand never reset its own PlayerScore, the oracle saw a stale baseline: +120 instead of +250).
- after-draw seed 11: `PASS: 1 el, ok 52, desync 0, kopma after-draw/return (275 sn)`; grant 1 at 1.16 (`b954e8df/b954e8df`), 9 moves after, 51 keys equal on 4 editors.
- offturn seed 11: `PASS: 1 el, ok 52, desync 0, kopma offturn/return (262 sn)`; grant 1 at 1.15, 9 moves after, 51 keys.
- after-open seed 1234: `PASS: 1 el, ok 29, desync 0, kopma after-open/return (269 sn)` (after d7f2103); Wait at 1.17 (seat's turn), grant at 1.18, 2 moves after, 28 keys.
- master-turn seed 11: FAIL `AT-host: [BUG] 1.51 AT-host: el sonu que2 beklenen 730 gözlenen 600`. The rejoin itself worked (grant 1 at 1.18, 8 moves after, 50 keys equal). Cause: in-hand penalties applied by the old master (que2 +130 at 1.3, que3 +120 at 1.4) are missing from the new master's `playerScores`; that rebuild (`PenaltyRebuild.FromLog` in `GameManager.OnMasterClientSwitched`) is Task 12. Not patched here.
- hand-end seed 11 -Deals 2: `PASS: 2 el, ok 102, desync 0, kopma hand-end/return (361 sn)` (after b4b538f); the returner came back after hand 2 had started, so it went live through a snapshot (2.1), grant at 2.2, 11 moves after, 98 keys.
- Regressions: `-Deals 2 -Mode smart` `PASS: 2 el, ok 88, desync 0 (338 sn)`; `-NoReturn -DropRejoin after-draw` `PASS: 1 el, ok 53, desync 0, kopma after-draw/noreturn (284 sn)`.
- EditMode 429/429, Pester 44/44. No `[DONUS] yeniden` (Again) in any run.

## Round 4: Task 12 (reduced) + scenario list
Commits: `9320082` (Task 12 reduced + leftovers), `df0599d` (afterend fix).
- `Rejoin/MasterRecovery.cs` `PenaltyRebuild.FromLog` (brief test verbatim). `GameManager.TakeOverAsMaster()` (called first in `SeatManager.OnMasterClientSwitched` when `SeatsLocked`, before the seat-loss scan): `playerScores` from `MatchScores.current` via `seatOfQue`, `PlayerScore` mirrored for every que, `[DEVIR] ceza tablosu ...` line; `FinishGate.Seed(CurrentRound, Hand{round})`. Evidence (master-turn run): `[DEVIR] ceza tablosu: que1=0 que2=130 que3=120 que4=0`.
- `FinishGate.Seed` (brief test verbatim; done since it is 6 lines).
- `SeatManager.RequestSeatRPC` and `OnPlayerEnteredRoom` also refuse when `SeatsLocked`.
- `TurnManager.PlayBotTurn`: before the discard, if the turn or serial changed since the bot started: warning, no discard.
- `GameManager.RollbackTurn` serial/record mismatch: `LogError`.
- `MatchScores.current` sources for a later master: snapshot `Apply` (`RestoreCurrent`), grant list, live `ShowPenaltyRPC` (gated, replayed). No other penalty path writes `playerScores` without `ShowPenaltyRPC` (the processing-penalty RPCs have no callers).
- Not done (cut): `NewHandRecovery` / 15 s watchdog; the brief's `New_Hand_Recovery` test omitted.
- `df0599d`: on `-ReturnAfterEnd` the returner joins the finished room; PUN instantiates the cached room objects into the `Reconnect` scene before `EndUnrecoverable` leaves; `TileDistrubite.Start` / `ScoreManager.Start` threw NRE. Both now return when the active scene is not Table.

Runs (4 editors, main editor <= 5.3 GB, clones <= 4.7 GB):
- `-Mode smart -Seed 11 -DropRejoin master-turn`: `PASS: 1 el, ok 37, desync 0, kopma master-turn/return (304 sn)`; old master rejoined as client, grant 1 at 1.20, 7 moves after, 50 keys equal.
- `-Mode smart -Seed 11 -NoReturn -DropRejoin master-turn`: `PASS: 1 el, ok 51, desync 0, kopma master-turn/noreturn (285 sn)`.
- `-Mode smart -Seed 11 -DropRejoin two`: `PASS: 1 el, ok 51, desync 0, kopma two/return (296 sn)`; one snapshot sent to both (`1.13 -> #3,#2`), both granted at 1.14, 9 moves after each, 51 keys.
- `-Mode smart -Seed 11 -DropRejoin offturn -ReturnAfterEnd`: `PASS: 1 el, ok 52, desync 0, kopma offturn/afterend (311 sn)` (after df0599d); returner: `odaya dönüldü ... maç bitti True, etkin başkası False`, `oyun sona ermiş`, `kayıt silindi`.
- `-Mode smart -Seed 11 -Deals 2 -DropRejoin hand-end -DropOfflineSec 1`: `PASS: 2 el, ok 102, desync 0, kopma hand-end/return (351 sn)`; `[DONUS] yeni elle canlı` (no snapshot), Wait at 2.1 (seat's turn), grant at 2.2, 11 moves after, 98 keys. Timing: drop -> first attempt ~5 s of scene change + hold; with the default 8 s the returner arrives after the deal (snapshot path), with 1 s before it. Hit once; not proven reliable under heavier load.
- `-Deals 3 -Mode smart`: `PASS: 3 el, ok 130, desync 0 (421 sn)`. `-Bots 2 -Mode smart -Seed 11`: `PASS: 1 el, ok 48, desync 0 (198 sn)`.
- EditMode 431/431, Pester 44/44.

## Known limits
- Master drop inside the new-hand chain (between hand end and the next deal): the new hand may never be dealt (no watchdog; Task 12's `NewHandRecovery` cut).
- Penalty RPCs on the way to a master that drops are lost; first hand's countdown window has the same gap.
- A returner whose session sees the room objects instantiated in `Reconnect` (finished match) relies on the Start guards; other room-object scripts with Awake side effects were not audited.
