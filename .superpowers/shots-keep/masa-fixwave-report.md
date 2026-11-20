# Fix wave report (commit f280a0c)

## A. Finish paths found
- GameManager.CheckGameStatus (~412): win (handTiles==0) -> FinishGameRPC(winner) and return; draw (master, allTiles==0) only for non-finishing discards. Triggered only on the discarder's client via TileUI.NextTurnRoutine event (TileUI:967).
- TileUI.NextTurnRoutine: finishing discard (isGameReallyOver) exits at ~970, BEFORE the draw send at TileUI:1037-1041. So at the send site a finishing discard never sends a draw already; the draw senders (TileUI:1037, TileDistrubite:2119 bot path, CheckGameStatus:429) only run for non-finishing discards. The double hand therefore comes from races across clients (a draw from another client/ master arriving around a win); `isGameEnded` is a local flag, so it cannot stop the other client's RPC. I could not reproduce it in the solo run (no multi-editor test run).
- FinishGameRPC had no once-per-hand guard on the master; OnGameEndedDetailedRPC started RestartSequence each time; NextRoundSetup then ran twice after 10 s.
## A. Change
- New `FinishGate.Accept(round, winner, ref doneRound, ref doneWinner)` (Score/FinishGate.cs), pure. Used in FinishGameRPC on master: draw then win -> win recomputed (Hand{n} overwritten); win then draw/same win -> ignored; new round accepted.
- OnGameEndedDetailedRPC: `restartRound` guard, RestartSequence started once per round.
- RestartSequence: after the 10 s wait, master calls NextRoundSetup only if CurrentRound still equals the round it was started for (covers master change / stale coroutine).
- No RPC signature changes. 3 new tests in ScoreTests.cs.
## B. MatchScores
- `StartNewHand()` clears `current`; called null-safe from GameManager.RPC_PrepareNextRound. Not unit-tested: MatchScores is a MonoBehaviourPunCallbacks needing GameManager/Photon (RecordPenalty), no Photon-free seam.
- Limit: a penalty that arrives after Hand{n} was filed is dropped at next hand start rather than filed under hand n.
## C. Deleted dictRewards/dictPenalties/dictNetScores (and their dead fill loop) and tempOpenedScore (4 writes + field). Grep of Assets/Osman, Tools: no readers.
## D. Localization.csv: only `table.kafa` contained "kafa"; en = "One shot". diff 1 line, BOM/LF preserved.
## E. UiKitTable.cs: BtnFont 31, Exit width 132->100, Settings 112->150 (label offsets 10), gap 8; both labels autosize 26..31; BindSettings now reapplies exit and rebuilds when Settings width differs. Cap height 31*0.633*1.132 = ~22 px at 2400x1080. Settings right edge 331 at 1920 reference (slot ~345). Table.unity run twice, md5 8bd4dfbf... unchanged on second run (scoreboard rebuilt once, large fileID diff as expected).

## Verification
- EditMode: 379 passed, 0 failed (376 + 3).
- LocAudit.CheckProject: errors=0 warnings=0.
- Solo: `PASS: 2 el, test oyuncularında hata yok (249 sn)`.
- Screenshot .superpowers/shots-keep/masa/table-buttons-tr-2400.png: rendered in EDIT mode (temp camera render of Table scene at 2400x1080, scene not saved), not a play-mode shot. Both labels clearly readable, clear of slot and plates. English label widths measured numerically only ("Settings" ~119 at 31 fits 130), not seen.
## Not verified
- Race (draw+win) not reproduced; no multi-editor test run.
