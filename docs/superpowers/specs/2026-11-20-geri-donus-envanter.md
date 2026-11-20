# Rejoin mid-hand: technical inventory (read-only)

Date: 2026-11-20. Branch MDC-001-firebase. All paths are under `Assets/Osman/Scripts/` unless they start with `Assets/Photon`.
Line numbers are from the files as they are now (TileDistrubite.cs has 2235 lines; ScoreManager.cs has 2454).

Legend for the "Snapshot" column used below:
- **S** = must be in the snapshot.
- **D** = can be derived on the rejoiner from other snapshot items (or from room/player properties the server keeps).
- **P** = already persisted by Photon (room or player custom properties survive an inactive actor), so it arrives on rejoin by itself.
- **N** = not needed for continuing the hand (dead code, cosmetic, or local scratch that is empty at a safe point).

Important facts found while reading (not in the brief):
- A disconnect while in a room calls `OnLeftRoom` (`Assets/Photon/PhotonRealtime/Code/LoadBalancingClient.cs:3148-3158`). `ExitButton.OnLeftRoom` then loads LobbyMenu (`UI/ExitButton.cs:29-33`). `LeftRoomCleanup` destroys every runtime-instantiated PhotonView locally, because the room has AutoCleanUp (`Assets/Photon/PhotonUnityNetworking/Code/PhotonNetworkPart.cs:257-279`). So a dropped client loses `TileManager(Clone)`, `ScoreManager(Clone)` and the Table scene. Nothing local survives.
- On a rejoin (`JoinMode.RejoinOnly`), the client does not send player properties (`LoadBalancingClient.cs:2861-2863`). The server keeps the inactive actor's `SeatNumber`, `PlayerQue` and `PlayerScore`. The client reads them back in its own Join event (`LoadBalancingClient.cs:3347-3352`). `LocalPlayer.HasRejoined` is true (`:3361`).
- Other clients get `OnPlayerLeftRoom` when a player only goes inactive (`LoadBalancingClient.cs:3380-3408`). They get `OnPlayerEnteredRoom` when that player returns (`:3376`, with `HasRejoined = true`, `:3351`).
- `AssignQueueToPlayer` changes the cached `player.CustomProperties` table in place and then re-sends the whole table (`TILES/TileDistrubite.cs:371-373`). That is why `PlayerQue` is visible locally at once on every client. It also means every replay of this RPC re-sends all of that player's properties.
- The human discard (`RemoveTileFromPlayerList`) is sent with `RpcTarget.All`, not buffered (`GamePlay/TileUI.cs:990-995`). Draws, however, are buffered (`TileUI.cs:763-767`). The RPC buffer therefore cannot rebuild any hand, even in principle.
- `TurnManager.isBotActive` is only ever written (`GamePlay/TurnManager.cs:28,374,399,413`). Nothing reads it to make a decision. `isBotLogicPaused` is set (`:299,317,527`) but `Update` never reads it (`:60-119`). Who acts as a bot is decided only by `!p.IsInactive && PlayerQue == currentTurnPlayer` (`:77-90`).
- No caller exists for `InstantiateActiveTiles` (`TileDistrubite.cs:1467`), `RemoveActiveTileFromPlayerList` (`:1683`) or `DeactivatePlayerTile` (`:832`). The same is true of `GameManager.ApplyProcessingPenaltyRPC`, `ApplyBulkProcessingPenaltyRPC` and `RevertProcessingPenaltyRPC` (`GamePlay/GameManager.cs:291-335`). They are dead code.
- The Table scene is loaded twice for a joiner: once by `RoomManager.OnJoinedRoom` through `SceneManager.LoadSceneAsync` (`Photons/RoomManager.cs:168`, `UI/SceneChangeManager.cs:28-38`), and once by PUN `LoadLevelIfSynced`, because the master's `sceneLoaded` writes `curScn` (`Assets/Photon/PhotonUnityNetworking/Code/PhotonNetworkPart.cs:1468-1479, 2093-2122, 2495`). Only the PUN load pauses the message queue.

---

## 1. Complete state inventory

### 1.1 Tiles and piles

| # | State | Type / owner | Mutated by | Same on all clients? | Rendered by | Snapshot |
|---|---|---|---|---|---|---|
| 1 | Four hands `playerTiles1..4` | `List<Tiles>` `TileDistrubite` (`TileDistrubite.cs:41-50`) | Deal: `DistributeTilesToAllPlayers` (`:439-518`), which appends and does not clear. Draw: `AddTileFromMiddlePlayerList` (`:607-636`). Side take: `AddTileFromDropPlayerList` (`:640-671`). Human discard: `RemoveTileFromPlayerList` by id (`:570-605`). Bot discard: `BotDiscardTileRPC` (`:2095-2163`). Melds, processing and rollback: `RemoveTileFromPlayerListByValue` (`:1911-1987`). Take-back, joker return and undo: `AddTileToPlayerHand` (`:1728-1772`). New hand: `ResetTableAndRedistribute` (`:2007-2068`). | Yes as data. **List order matters for the hash**: `AppendSyncState` writes hands in list order (`:2208-2212`). | Only the local que's hand gets GameObjects: `InstantiateTiles(j, tile)` at rack slot j (`:526-536`), `InstantiateTileInFirstEmptySlot` (`:1774-1808`), and TileUI drag. Other hands are never rendered. | **S** (all four, in list order, with ids) |
| 2 | Local rack layout (which tile sits in which of the 30 slots) | Only the GameObject hierarchy: children of `PlayerTileContainer` slots (`TileDistrubite.cs:66-67,97-107`); `TileUI.tilePlaceInt` (`TileUI.cs:74,144-154`) | Drag/drop `TileUI.OnEndDrag`/`SmoothMove` (`TileUI.cs:745-831,1061-1106`). Deal puts hand index j in slot j (`TileDistrubite.cs:475,498,504,510`). | Local only; the owner's. Other clients cannot rebuild it, because no client has it. It is lost on the dropper, since the scene is destroyed. | Rack GameObjects. `ScoreManager.GetSplittedGroups` reads slots 0-14 and 15-29 as groups (`ScoreManager.cs:295-406`). | **N** for correctness (any layout is legal; the player re-arranges). Hidden (inactive) rack tiles of pending melds do not exist at a safe point. |
| 3 | Middle pile and its order | `List<Tiles> allTiles` (`TileDistrubite.cs:32`) | Shuffle on master (`:148-165`). Indicator removed (`:193-194`). Joker types baked in on master before send (`:229-245,262-279`). `SyncShuffledTiles` replaces it (`:167-174`). Deal dequeues (`:470-471,489-490`). Every draw `RemoveAt(0)` (`:631`). Rollback draw (`GameManager.cs:273-279`). | Yes (whole order). In the hash as `pile(n)=` (`TileDistrubite.cs:2205`). | Data only. Visual: N **blank** TileUIs from `PlaceRemainingTilesInMiddleContainer` (`:538-544`). A drawn tile gets its data at drag time, `SetTileData(allTiles[0])` (`TileUI.cs:762`). Count label `UIManager.UpdateTileCount` (`TileDistrubite.cs:520-523,632-635`). Other clients never remove a middle visual when someone draws (`:607-636` has no Destroy), so the child count of `MiddleTileContainer` differs per client (local only). `AutoPlayTurn` draws by dragging the last middle child (`TurnManager.cs:158-167`). | **S** (order + ids). Visual count: **D** (render `allTiles.Count` blanks). |
| 4 | Indicator tile | **Only UI**: first child of `IndicatorTileContainer`. `GetIndicatorTile` reads `TileUI.tileDataInfo` (`TileDistrubite.cs:1863-1872`). | `SyncIndicatorTile` RPC.All, **not buffered** (`:225,247-260`). | Yes. Hash `ind=` (`:2204`). | `SyncIndicatorTile` instantiates `tilePrefab` with `isIndicatorTile = true`. | **S** |
| 5 | Okey identity | Baked into `Tiles.type` (Joker / FakeJoker) and the FakeJoker color/number on master (`TileDistrubite.cs:200-221,229-245,262-279`). Then serialized (`GamePlay/TileSerialization.cs:109-137`). | Shuffle only. | Yes. | `SetTileData` picks the sprite from type (`TileUI.cs:164-194`). | **D** (carried by tile type in items 1, 3, 6, 9) |
| 6 | Discard piles, top tile | `Tiles dropTile` (`TileDistrubite.cs:65`). One global "last discarded" tile, not per seat. | Set in `InstatiateSideTiles` (`:717`), so by human discard (`:594`), bot discard (`:2115`) and `ReturnTileToSide` (`:1897-1909`). **Not cleared** when taken: `AddTileFromDropPlayerList` adds it and keeps the reference (`:640-671`). Cleared on new hand (`:2022`). | Yes. Hash `drop=` (`:2206`). | Not directly. | **S** (including the "still set after a side take" quirk, or the hash differs) |
| 7 | Discard piles, full history per seat | **Only UI**: stacked TileUI children of `SeatManager.tiledropOffset[relativeIndex]` (`GamePlay/SeatManager.cs:15`; placement `TileDistrubite.cs:676-736`). `List<GameObject> droppedTiles` holds every discard visual of every seat in time order (`:638,718`). | Added by `InstatiateSideTiles`. On a side take, `DestroySideTiles` removes `droppedTiles.Last()` and destroys it on every client except the taker (`:738-772`); the taker's visual becomes its rack tile. `ReturnTileToSide` re-instantiates into the previous que's pile (`:1905-1908`). | Same intended picture on all clients. Not hashed (only `drop=`). | `InstatiateSideTiles`: seat from `seatOfQue[que]`, then `SeatMath.RelativeIndex(targetSeat, localSeat)` into `tiledropOffset` (`:682-708`). The local "take from the side" source is the object tagged `LeftTileContainer` (`TileUI.cs:114,412-440`). | **S** (per-seat list of discarded tiles in order, so the stacks and `droppedTiles` order can be rebuilt; `DestroySideTiles` always removes the last one) |

### 1.2 Table (melds, processed tiles, placeholders)

| # | State | Type / owner | Mutated by | Same on all? | Rendered by | Snapshot |
|---|---|---|---|---|---|---|
| 8 | Opened melds per que (data) | `List<List<Tiles>> meltedTiles1..4` (`TileDistrubite.cs:54-63`) + `List<List<Vector2Int>> meltedTilesPositions1..4` (row, column) (`:778-781`) | `MergeValidpers` AllBuffered, sent at the moment of opening (before commit) (`:783-805`; senders `ScoreManager.cs:880-886,998-1004,1165-1171`). `UnMergeValidPers` AllBuffered **clears all of that que's melds**, including melds from earlier turns (`TileDistrubite.cs:807-830`; sender `ScoreManager.cs:1250`). New hand (`TileDistrubite.cs:2032-2042`). | Yes. Hash `meld1..4=` (`:2213-2214`). Positions are not hashed. | Remote clients: `MeldTiles` (`:903-928`) calls `InstatiateMeldTiles` (`:932-1113`). It re-walks **all** melds of that que and skips filled slots. Columns: series `row*13 + number-1` (uses the tile's own `number`, `:1010`), group/pair `position.y` (`:1046,1084`). It skips the local que (`:940-941`), because the owner renders through `ScoreManager.PlaceValidPers` (`ScoreManager.cs:818-836`) / `PlacePairPers` (`:1105-1114`). | **D** if the board (item 9) is in the snapshot; but `meltedTiles` must equal the others for the hash. So **S** as data (lists + positions), board rendered from item 9. |
| 9 | Board as rendered (melds + processed tiles + joker swaps), per seat | **Only UI**: TileUI children under `"<NickName> meld"` → child(0) series (13-wide rows), child(1) group (4-wide rows), child(2) pair (2-wide rows) (`TileDistrubite.cs:977-982,1595-1600`; `ScoreManager.cs:74-80`). Area per seat = `SeatManager.meldTileOffsets[relative]`, renamed to `NickName + " meld"` (`SeatManager.cs:505-509`). | Open (owner): `PlaceValidPers`/`PlacePairPers`. Remote: `MeldTiles`→`InstatiateMeldTiles`, **sent only at discard** (`ScoreManager.CommitAndStoreMelds` `:1977-2006`, called from `TileUI.NextTurnRoutine` `:1014`). Processing (owner): `PerformTileProcessing` (`ScoreManager.cs:1728-1842`); remote `SyncProcessedTileRPC` OthersBuffered, also **sent only at discard** (`ScoreManager.cs:1846-1864`; handler `TileDistrubite.cs:1812-1856`; destroys the occupant, places by `(meldTypeInt, placeholderIndex)`). Undo (owner only, before discard): `UndoLastProcess` (`ScoreManager.cs:1906-1972`), `TakeBackPers` (`:1202-1276`). New hand: destroy all TileUIs (`TileDistrubite.cs:2013-2018,2049-2059`). | Yes after each discard. Hash `board#<actor>=TileKey@type.slot` (`:2219-2231`). **This is the best existing definition of "the table".** It is keyed by ActorNumber, so bot seats (no Player) have no line. | as described | **S** (per seat: tile id/type/color/number + meld type index + slot index) |
| 10 | What real tile an okey stands for | Implicit. Joker `Tiles.number/color` are **changed in place** by `OkeyRuleEngine.CheckPattern` (`GamePlay/OkeyRuleEngine.cs:85-137`) and `MultiColorCheck` (`:200-203`) on whichever client runs the check. `ResetJokerData` sets `number = 0` on take-back (`ScoreManager.cs:1279-1289`). `DesyncDetector.TileKey` hashes a Joker as `J:id` only, because these values differ per client (`GamePlay/DesyncDetector.cs:229-233`). | as listed | **No** (values drift per client) | Series placement uses the joker's `number` (`TileDistrubite.cs:1010`); swap targets are computed from slot index (`ScoreManager.cs:1432-1453`). | **D** from the slot index in item 9 (series: `slot % 13 + 1`, colour of row siblings; group: siblings' number; pair: sibling). Do not trust the joker's number field. |
| 11 | Placeholder "available" slots (işlek) and the işlek list | `Placeholder.available`, `.AvailableTileInfo` (`UI/Placeholder.cs:9-12`); `List<Tiles> TileDistrubite.availableTiles` (`TileDistrubite.cs:1117`) | `RecalculateAllAvailableSlots` (`:1537-1679`) rebuilds from the rendered board on every client, on NextTurn (`TurnManager.cs:338`), on processing and on open. It lights slots only if the local player has opened (`:1543-1545,1668-1672`). `ScoreManager.UpdateAvailableForPlaceholders` (`ScoreManager.cs:1351-1575`). | Derived. Timing-dependent; DesyncDetector excludes it (`DesyncDetector.cs:120`). The master's copy decides the 250 discard penalty (`GameManager.cs:340-363`). | Glow and drop targets (`GamePlay/ProcessAssist.cs:105-157`). | **D** (call `RecalculateAllAvailableSlots` after the board is built) |
| 12 | `willInstantiate` / `activePlacements` | `Placeholder.willInstantiate`, `TileDistrubite.activePlacements` (`TileDistrubite.cs:1465`) | `UpdateAvailableForPlaceholders` (TileDistrubite version) adds to it (`:1296-1303`). Nothing consumes it. | — | — | **N** |
| 13 | Who processed which tile | Only during the actor's turn: `ScoreManager.actionHistory` (`:548,1813-1826`), `pendingSyncActions` (`:1591,1794-1801`). Cleared at discard (`:2013-2021`). Not kept after the turn. | — | Local to the actor | — | **N** (not tracked after the turn; penalties already applied) |
| 14 | Own-meld bookkeeping of the local player | `ScoreManager.occupiedRows[4]`, `occupiedRowsNumber[4]`, `occupiedRowsPair[8]` (`:739-741`); `committedMelds` (`:750`) | Set in `PlaceValidPers`/`PlacePairPers` (`:878,996,1162`) and `CommitAndStoreMelds` (`:1986`). Cleared in `TakeBackPers` (`:1226-1243`) and `ResetPlayerOpenStatus` (`:164,173-178`). `committedMelds.Count == 0` decides whether take-back clears "opened" (`:1261-1266`). | Local; only the meld owner has them. Derivable by anyone from the owner's meld area. | — | **D** (from own seat's part of item 9: a row is occupied if it has any child; `committedMelds` non-empty iff own melds exist) |
| 15 | Pending (uncommitted) melds, processing and joker swaps of the actor | `pendingMeldInfos` (`ScoreManager.cs:747`), `pendingMeldedTiles` (`:753`), `meldTileGO` (`:754`), `pendingJokersToTake` (`:46`), `pendingActivePlacements` (`:1320`), `pendingSyncActions` (`:1591`), `actionHistory` (`:548`); `PenaltySystem.currentTurnPenalties` (`GamePlay/PenaltySystem.cs:23`) | Opening, processing, undo; committed in `NextTurnRoutine` (`TileUI.cs:877-879,943-948,1014-1019`). | Local to the acting client. **The data side was already broadcast** (hand removals, `MergeValidpers`, `SyncOpenedPlayerRPC`, joker `AddTileToPlayerHand`), but the visuals and penalties were not. | owner's board objects | **N** at a safe point (must be empty). See risk 3 for a drop in the middle of a turn. |

### 1.3 Open status, limits, badges

| # | State | Type / owner | Mutated by | Same on all? | Rendered by | Snapshot |
|---|---|---|---|---|---|---|
| 16 | Who opened (que set) | `HashSet<int> ScoreManager.playersWhoOpened` (`ScoreManager.cs:2047`) | `SyncOpenedPlayerRPC` AllBuffered (`GameManager.cs:612-620`; senders `ScoreManager.cs:1018-1022,1185-1189`). Cleared per hand by `ResetPlayerOpenStatus` (`:143`), not buffered. **Never removed on take-back.** | Yes. Hash `opened=` (`DesyncDetector.cs:136`). | Nothing direct; used by the limit rule (`ScoreManager.cs:2055`), the end penalty (`GameManager.cs:523`) and kafa (`TileUI.cs:957-961`). | **S** |
| 17 | Who opened pairs | `HashSet<int> playersWhoOpenedPairs` (`ScoreManager.cs:2048`) | `SyncOpenedPairsRPC` AllBuffered (`GameManager.cs:606-610`; sender `ScoreManager.cs:1191-1195`). | Yes (not hashed). | — (end-of-hand x2, `GameManager.cs:501,531,570,584`) | **S** |
| 18 | Local "I opened series / pairs" | `ScoreManager.hasOpenedSeries`, `hasOpenedPairs` (`:61-62`) | `PlaceValidPers` (`:1025`), `PlacePairPers` (`:1190`), `TakeBackPers` (`:1261-1266`), new hand (`:141-142`). | Local, about the local player. Other clients cannot see it directly. | Gates processing (`:1599`, `TileUI.cs:839`, `ProcessAssist.cs:102`) and placeholder lighting (`TileDistrubite.cs:1543-1545`). | **D** (pairs = `playersWhoOpenedPairs.Contains(myQue)`; series = `playersWhoOpened.Contains(myQue)` and own board has a series/group row; a pairs opener cannot open series, `ScoreManager.cs:552-557`). Cheap to put in as **S**. |
| 19 | Table limit (baraj) | Room prop `TableLimit`; getter `GameManager.CurrentTableLimit` (`GameManager.cs:18-34`) | `SetTableLimit` master at scene start (`:94-97`) and new hand (`:724`); `TryUpdateTableLimit` by the opener at discard (`:141-148`, from `ScoreManager.CommitFinalTableLimit` `:2285-2301`). | Yes | `UIManager.UpdatePlayerStats` from `OnRoomPropertiesUpdate` (`GameManager.cs:157-180`) and `CheckForPer` (`ScoreManager.cs:453-460`). | **P** |
| 20 | Double opened on table | Room prop `IsDoubleOpened` (`GameManager.cs:437-463`) | `SetDoubleOpened` (`ScoreManager.cs:1196`); reset per hand (`GameManager.cs:725-726`). | Yes | rule only | **P** |
| 21 | Open values / badges | `Dictionary<int,(int value,bool pairs)> GameManager.OpenValues` (`GameManager.cs:624`) | `OpenValueRPC` RPC.All, **not buffered** (`:626-631`; sender `ScoreManager.cs:2304-2311`); cleared on new hand (`GameManager.cs:711-712`). | Yes | `UI/Table/OpenBadges.cs:32-49` | **S** (display only) |
| 22 | Per-hand opening scratch of the actor | `ScoreManager.FirstOpenThisTurn` (`:2283`), `OpenedValueThisTurn`, `OpenedPairsThisTurn` (`:2279-2280`) | open/take-back/discard (`:1015-1017,1180-1184,1255-1257,2016,2314-2315`). | Local to the actor | — | **N** at a safe point (zero/false) |
| 23 | Rack scores of the local player | `ScoreManager.totalScore`, `pairTotalScore`, `totalPerCount`, `pairTotalPerCount`, `validPerss` (`:55-58,293`) | `CheckForPer` (`:408-461`), run by every `TileUI.Start` (`TileUI.cs:128`) and every `SmoothMove` (`:1103-1104`). | Local | `UIManager.UpdatePlayerStats` | **D** |

### 1.4 Turn

| # | State | Type / owner | Mutated by | Same on all? | Rendered by | Snapshot |
|---|---|---|---|---|---|---|
| 24 | Turn owner | `int TurnManager.currentTurnPlayer` (que) (`TurnManager.cs:12`) | `SetTurnDirectly` RPC.All (`:206-223`; sender `TileDistrubite.cs:341-345`); `StartRoundVisuals` sets 1 (`TurnManager.cs:523`); `NextTurn` ++ (`:321-360`); `RPC_ResetTurnForNewRound` sets 1 (`:294`). | Yes. Hash `turn=` (`DesyncDetector.cs:128-129`). | `UIManager.UpdateTurnIndicators` (`UI/UIManager.cs:75-91`), timer bar (`:102-138`). | **S** |
| 25 | Turn timer start | `double TurnManager.TurnStartTime` (`:36`) | `info.SentServerTime` in `SetTurnDirectly`/`NextTurn` (`:210,325`); `PhotonNetwork.Time` in `StartGame`/`StartRoundVisuals` (`:44,524`); reset by `AutoPlayTurn` (`:181-182`). | Nearly (server time) | Timer bar (`UIManager.cs:115-137`); `TickTurnTimer` fires `AutoPlayTurn` only on the turn owner's own client (`TurnManager.cs:123-140`). | **S** (the server time of the current turn start) |
| 26 | Turn permission flags of the local player | `TurnManager.canDrop`, `hasPickedFromSide`, `hasOpenedThisTurn`, `hasProcessedThisTurn` (`:16-18,253`) | `canDrop`: true when drawn (`TileUI.cs:768,780`), true on turn start for que 1 (`TurnManager.cs:50,221,546`), false via `ResetTurnFlags` (`:255-261`) on own `NextTurn` (`:345-352`) and new hand (`:295`), false before NextTurn (`TileUI.cs:1052`). The master also sets its own `canDrop=false` in `BotDiscardTileRPC` (`TileDistrubite.cs:2148`). `hasPickedFromSide`: `TileUI.cs:773`; cleared in rollback (`GameManager.cs:281,285`). `hasOpenedThisTurn`: `ScoreManager.cs:1014,1179,1254`. `hasProcessedThisTurn`: `:1829,1912`. | Local; meaningful only on the turn owner's client. Another client cannot see them, but they are **derivable**: `canDrop` = own hand has 15 tiles during own turn (the draw rule uses `playerTiles.Count >= 15`, `TileUI.cs:386,473`); the others are false at a safe point. | Gate drag/drop/open | **D** (set explicitly on hand-back; see section 4) |
| 27 | Side-pick obligation | `GameManager.currentSidePickTile` (`GameManager.cs:70`), `TurnManager.hasPickedFromSide` | `RecordSidePick` through the local `EventDispatcher` (`GameManager.cs:92,182-189`); cleared in rollback (`:288`) and on master in `ApplySidePickSuccessPenaltyRPC` (`:239`). | Local to the side-taker | Cancel button `UI/SidePickUIManager.cs:19-47` | **N** at a safe point (must be resolved). The bot ignores this obligation (risk 3). |
| 28 | Auto-play / drag locks | `TurnManager.autoPlaying` (`:40`); `TileUI.held`, `TileUI.discardLockUntil` (static) (`TileUI.cs:43-47`); `TurnManager.localPlayerTurn` (`:15`, written only) | `AutoPlayTurn`, drag, `ExecuteNextTurn` (`TileUI.cs:874`). | Local | — | **N** (fresh scene starts them clear; statics survive a scene load but are time-bound) |
| 29 | Bot driver state (master only) | `isBotMoving`, `botTimeoutTimer` (`TurnManager.cs:20-22`), `PlayBotTurn` coroutine; `isBotActive`, `isBotLogicPaused` (unused) | `Update` (`:93-118`), `PlayBotTurn` (`:440-517`), `NextTurn` (`:328-329`), `ForceSkipBotTurn` (`:192-203`). | Master only | — | **N** (master-side; see sections 4 and 6) |

### 1.5 Queue, seats, identity

| # | State | Type / owner | Mutated by | Same on all? | Snapshot |
|---|---|---|---|---|---|
| 30 | `PlayerQue` per human | Player custom property | Every client runs `AssignQueueToPlayer` AllBuffered (`TileDistrubite.cs:327-332,365-380`). Wiped on a fresh join by `RoomManager.SetPickSeat` (`Photons/RoomManager.cs:90-99`). | Yes | **P** (kept for the inactive actor) |
| 31 | `SeatNumber` per human | Player custom property; only master writes it (`SeatManager.cs:378-381`) | `AssignSeatToPlayer`; wiped by `SetPickSeat` | Yes | **P** |
| 32 | Que → seat map (covers bots too) | `int[5] TileDistrubite.seatOfQue` (`TileDistrubite.cs:674`) | `SyncQueToSeatMapping` AllBuffered (`:351,356-363`) | Yes. Hash `seats=` (`:2203`). | **S** (bot seats have no Player, so this is their only que↔seat link) |
| 33 | Actor → seat cache | `Dictionary<int,int> SeatManager.playerSeatMap` (`SeatManager.cs:12`) | `AssignSeatToPlayer` (`:382`); `FreeSeat` removes it, **also when a player only goes inactive** (`:266-270,407-428`). Lookup falls back to the `SeatNumber` property (`:432-447`). | Roughly | **D** (from `SeatNumber`) |
| 34 | Seat names / bot names | Room props `SeatNames` (`GamePlay/Score/MatchScores.cs:16,158-163`), `Seats` (`SeatManager.cs:184-198`), `SeatsLocked` (`:323-329`); master's `rememberedNames` (`:145`) | `PublishSeatNames` (`:148-181`), `StartGame` (`:669-673`) | Yes | **P** |
| 35 | Meld-area / drop-area GameObject names | `meldTileOffsets[rel].name = NickName + " meld"`, `tiledropOffset[rel].name = NickName` (`SeatManager.cs:505-512`); reset to scene defaults first (`:464-465,606-620`) | `UpdateSeatDisplay` (`:453-546`) | Local view, relative to the local seat | **D** (from PlayerList + `SeatNumber`) |
| 36 | `playerNamesByQueue` | `TileDistrubite.cs:33,442-452` | deal | — | **N** (no reader) |

### 1.6 Hand / match counters, scores, end state

| # | State | Type / owner | Mutated by | Same on all? | Snapshot |
|---|---|---|---|---|---|
| 37 | `isGameReady`, `isGameEnded` | `GameManager` (`GameManager.cs:14-15`) | `SetGameReady` from `SyncShuffledTiles` (`:107-111`, `TileDistrubite.cs:178-181`); `FinishGameRPC` (`GameManager.cs:468-485`), `OnGameEndedDetailedRPC` (`:636-652`); `RPC_PrepareNextRound` (`:708-709`); `Awake` (`:80`). | Yes. Hash `ready= ended=`. | **S** (or set on restore) |
| 38 | Round counters | Room props `CurrentRound`, `TotalRounds` (`RoomManager.cs:64-65`; `GameManager.cs:696-703`) | master `NextRoundSetup` | Yes | **P** |
| 39 | In-hand penalties (authoritative) | `Dictionary<int,int> ScoreManager.playerScores` keyed by que (`ScoreManager.cs:35`) | **Master only**: `UpdatePlayerScore` (`:190-216`) from `AddGeneralPenaltyRPC` (`GameManager.cs:204-213`), `ApplySidePickSuccessPenaltyRPC` (`:227-244`), `CheckPenaltyRPC` (`:340-363`), `PenaltySystem.ApplyPenaltyRPC` (`PenaltySystem.cs:197-214`). Zeroed per hand on all clients (`ScoreManager.cs:151,221-240`). | **No**: correct only on master; zeros elsewhere. | **N for the rejoiner** (a non-master never reads it). Needed only if the master changes (section 6). |
| 40 | `PlayerScore` player property | Player custom property | Master mirrors `playerScores[que]` onto the human holding that que (`ScoreManager.cs:244-273`); bots have none. **Reset to 0** by `ScoreManager.Start` (`:66-68`), `InitializeScores` (`:234-236`) and `RoomManager.OnJoinedRoom` (`RoomManager.cs:164-166`). | Yes | **P** (but the rejoiner's own `RoomManager.OnJoinedRoom` and `ScoreManager.Start` overwrite it with 0) |
| 41 | In-hand penalty log (display) | `MatchScores.current` list of (seat, amount, reason) (`MatchScores.cs:40,121-135`) | `ShowPenaltyRPC` RPC.All, not buffered (`GameManager.cs:216-225`); cleared by `StartNewHand` (`MatchScores.cs:122`; `GameManager.cs:710`). Every real penalty path calls `AnnouncePenalty`. | Yes | **S** (display; it is also the only per-seat penalty record outside the master, see section 6) |
| 42 | Match history | `MatchScores.History`, `SeatNames`, `byHand`, `raw` (`MatchScores.cs:19-20,41-42`) from room props `Hand{n}`, `SeatNames`; read on `Awake` (`:48-61`) | master `WriteHand` (`:151-156`) | Yes | **P** (`byHand` penalty lists of earlier hands are **N**/lost: display only) |
| 43 | Finish gate / restart guard | `GameManager.finishedRound`, `finishedWinner` (master), `restartRound` (all) (`GameManager.cs:465-466,647-651`) | `FinishGameRPC`, `OnGameEndedDetailedRPC` | — | **N** mid-hand (all are reset by the round number) |
| 44 | DesyncDetector counters | `deal`, `move`, `seededDeals`, `received` (`DesyncDetector.cs:36-43`) | `Capture` (`:96-118`); **reset by `OnJoinedRoom`** (`:61-71`) | Meant to be lockstep across clients | **S** for sync tests only (`deal.move` key), otherwise **N** |

### 1.7 What `DesyncDetector` counts as shared state

`DesyncDetector.BuildSnapshot` (`DesyncDetector.cs:121-139`) + `TileDistrubite.AppendSyncState` (`TileDistrubite.cs:2201-2232`) **include**:
- `turn` = `currentTurnPlayer`, `ready` = `isGameReady`, `ended` = `isGameEnded`;
- `opened` = sorted `playersWhoOpened`;
- `seats` = `seatOfQue[1..4]`;
- `ind` = indicator; `pile(n)` = full `allTiles` in order; `drop` = `dropTile`;
- `hand1..4(n)` = each hand **in list order**; `meld1..4` = `meltedTiles` lists;
- `board#<ActorNumber>` = every TileUI under that player's meld area as `TileKey@typeIndex.slotIndex`, sorted (Player list ordered by ActorNumber).

`TileKey` = colour letter + number + first 4 id characters. A Joker is `J:id` and a FakeJoker `F:id` (`:225-234`), so okey substitution values are ignored.

**Left out on purpose** (comment `:120`): scores (master-only), `availableTiles` (timing), room properties (TableLimit, IsDoubleOpened, CurrentRound).

**Also left out (as I read it)**:
- `playersWhoOpenedPairs` and `OpenValues`;
- per-turn flags and `TurnStartTime`;
- `meltedTilesPositions`;
- the discard history per seat (only the global top `dropTile`);
- joker substituted values;
- rack layout and the middle-pile visual count;
- `MatchScores.current`;
- local open flags, `committedMelds`, `occupiedRows*`;
- `PlayerQue` / `SeatNumber` props;
- the board of seats with no Player (bots). There is no `board#` line for those seats, so a bot seat's board is never compared. Bots never meld, but a seat whose human left permanently (TTL expired) keeps melds that are no longer compared or found.

Snapshot keys are `deal.move`. A rejoiner's counters restart at 0 (`:61-71`), so its reports will not line up unless the key is restored.

---

## 2. How the Table scene is built at hand start

Master side, first hand:
1. `SeatManager.Start` → `OnJoinedRoom()` (manual, because already in room) (`SeatManager.cs:51-62,71-111`).
   - `AssignPositionAndInstantiate`: `PhotonNetwork.Instantiate(playerPrefab)` + buffered `SetPlayerName` / `SetPlayerSeat` (`:338-365`; `TestPlayer/SetupPlayer.cs:13-38`). This is cosmetic.
   - Master: `InstantiateRoomObject(tileManagerPrefab)` creates `TileManager(Clone)` (`:86-94`) + `AssignSeatToPlayer` AllBuffered (`:96-101`). For a bot room it starts `StartWithBotsAfterDelay` (`:104-108,213-234`).
   - `UpdateSeatDisplay` names the `"<Nick> meld"` areas (`:453-546`).
2. `TileDistrubite.Start`: finds containers, `InitializePlaceholders`, `GeneratePlayerTiles` (106 tiles) (`TileDistrubite.cs:78-146`).
3. When 4 are seated (`TryStartGame` `SeatManager.cs:316-320`) or the bot timer fires: `StartGame` (`:655-678`). This locks seats, closes the room and publishes seat names, then runs `CountdownAndShuffle` (`:682-703`).
4. `CountdownAndShuffle` waits **3 × 1 s**, then `InstantiateRoomObject(scoreManagerPrefab)` creates `ScoreManager(Clone)`, then calls `tileDistrubite.ShuffleTiles()`.
5. `ShuffleTiles` (`TileDistrubite.cs:148-165`): seed from `DesyncDetector.NextDealSeed`, Fisher-Yates, then `SetIndicatorTile` (`:187-226`).
   - Removes `allTiles[0]` as the indicator and bakes the joker types (`:229-245` / `:262-279`).
   - Each branch starts `DistributeWithDelay` (`:244,278`), whose first step runs at once.
   - Then sends `SyncIndicatorTile` RPC.All (`:225`), which instantiates the indicator (`:247-260`).
6. `DistributeWithDelay` (`:281-292`): `AssignPlayerQueue` RPC.All (`:284`). Only the master runs its body (`:295-353`):
   - picks a random start seat;
   - `AssignQueueToPlayer` AllBuffered ×N humans, which writes `PlayerQue` (`:327-332,365-380`);
   - `SetTurnDirectly(1)` RPC.All (`:341-345`; `TurnManager.cs:206-223`), which sets `canDrop` for que 1;
   - `SyncQueToSeatMapping` AllBuffered, then `AssignQueueComplete` AllBuffered (`:351-352`). The latter runs master `RecalculateBotStates` and, on all clients, `TurnManager.StartRoundVisuals` (`:417-437`; `TurnManager.cs:520-551`): turn = 1, `TurnStartTime`, `canDrop` if que 1, MyTurn SFX.

   It then **waits 0.5 s** and sends `SyncShuffledTiles(allTiles[])` RPC.All, **not buffered** (`:288-291`).
7. `SyncShuffledTiles` (`:167-184`), all clients:
   - finds `ScoreManager(Clone)`; NRE if missing (`:170`);
   - replaces `allTiles` and runs `DistributeTilesToAllPlayers` (`:439-524`): deal SFX, append 15/14/14/14 to `playerTiles1..4`, `InstantiateTiles(j, tile)` for the local que at rack slot j, `PlaceRemainingTilesInMiddleContainer` (blank TileUIs), `UpdateTileCount`;
   - then `GameManager.SetGameReady()` and `DesyncDetector.Report("Deal", newDeal: true)`.
8. Next frame, each new TileUI runs `TileUI.Start` (`TileUI.cs:110-142`):
   - needs `TurnManager`, `TileManager(Clone)` and `ScoreManager(Clone)` by name;
   - caches `playerTiles = tileDistrubite.GetPlayerTiles()`, the **list reference** of the local hand (`:125-126`, `TileDistrubite.cs:550-567`, which needs `PlayerQue`);
   - calls `scoreManager.CheckForPer()`, `FitToParent`, and collects meld placeholders.
9. `ScoreManager.Start` (`ScoreManager.cs:64-89`) runs on every client when `ScoreManager(Clone)` appears:
   - writes `PlayerScore = 0`;
   - finds `"<LocalNick> meld"`; NRE if `UpdateSeatDisplay` has not named it yet;
   - `turnManager.StartGame()` (`TurnManager.cs:42-58`) sets `TurnStartTime` and sets `canDrop = true` if the local que is 1 (unboxes `PlayerQue`, NRE if absent, `:46-47`);
   - `playerScores = new`, master `InitializeScores(4)`, `InitializeMeldPlaceholders` (marks `isMeldArea`).
10. `GameManager.Start` (`GameManager.cs:89-98`): registers `OnPlayerMoveFinished` / `OnSideTilePicked`. The master sets `TableLimit = 51`.

Hand 2 onward:
- All clients run `OnGameEndedDetailedRPC` → `RestartSequence`, which **waits 10 s** (`GameManager.cs:636-694`).
- Master `NextRoundSetup` (`:696-703`): `CurrentRound++`, `RPC_PrepareNextRound` RPC.All (`:705-741`).
  - All clients: flags false, `MatchScores.StartNewHand`, `OpenValues.Clear`, scoreboard hidden, `ResetPlayerOpenStatus`.
  - Master: `RPC_ResetTurnForNewRound` RPC.All (`TurnManager.cs:290-306`), `TableLimit` 51, `IsDoubleOpened` false, then **waits 0.2 s** (`WaitAndRedistribute`, `:743-748`) and sends `ResetTableAndRedistribute` RPC.All.
  - Non-master: calls `ResetTableAndRedistribute` locally (`:738-739`). That destroys every TileUI and clears hands, melds and placeholders; the master regenerates and shuffles (`TileDistrubite.cs:2007-2068`). Then steps 5-8 repeat. `ScoreManager(Clone)` and `TileManager(Clone)` are **not** re-instantiated.

Reusable for a snapshot restore (direct calls with restored data):
- `InitializePlaceholders` (Start).
- `SyncIndicatorTile` body (only instantiates).
- `InstantiateTiles(slot, tile)` / `InstantiateTileInFirstEmptySlot` (rack).
- `PlaceRemainingTilesInMiddleContainer` (count-driven).
- `SyncProcessedTileRPC` body (places one board tile by `(type, slot)`; plays SFX; recalcs).
- `RecalculateAllAvailableSlots`.
- `UIManager.UpdateTileCount`, `UpdateTurnIndicators`, `UpdatePlayerStats`.
- `SeatManager.UpdateSeatDisplay`.
- `OpenBadges.Refresh` via `GameManager.OpenValueShown`.
- `ScoreManager.CheckForPer` and `InitializeMeldPlaceholders`.

Usable only with care:
- `InstatiateSideTiles` also sets `dropTile` and appends to `droppedTiles` (`TileDistrubite.cs:717-718`).
- `InstatiateMeldTiles` skips the local que (`:940-941`), ignores processed tiles, and places series jokers by their mutable `number` (`:1010`).

Assume a fresh deal (do not reuse as-is):

| Method | Why it assumes a fresh deal |
|---|---|
| `SyncShuffledTiles` / `DistributeTilesToAllPlayers` | Appends without clearing, dequeues from `allTiles`, deal SFX, `newDeal` report. |
| `AssignPlayerQueue` / `AssignQueueToPlayer` | Random start seat; property re-write. |
| `AssignQueueComplete` → `StartRoundVisuals` | Turn = 1, `canDrop` for que 1. |
| `SetTurnDirectly` | Turn start. |
| `TurnManager.StartGame` (via `ScoreManager.Start`) | `canDrop` for que 1 **regardless of whose turn it is**; `TurnStartTime = now`. |
| `ScoreManager.Start` | `PlayerScore = 0`; master `InitializeScores`. |
| `GameManager.Start` | Master sets the limit to 51. |
| `ShuffleTiles` / `SetIndicatorTile` | Consumes a DesyncDetector seed. |
| `ResetTableAndRedistribute`, `RPC_PrepareNextRound`, `RPC_ResetTurnForNewRound` | New-hand resets. |

Waits and coroutines on the fresh path:
- `CountdownAndShuffle`: 3 s.
- `DistributeWithDelay`: 0.5 s.
- `WaitAndRedistribute`: 0.2 s.
- `RestartSequence`: 10 s.
- `TileUI.Start`: one frame later.
- TileUI `Slide` / `SmoothMove`: 0.3 s (`TileUI.cs:57,1148-1175`).

Ordering constraints a restore must respect:
1. `"<Nick> meld"` names (`UpdateSeatDisplay`), `PlayerQue` / `SeatNumber` props, `TileManager(Clone)` and `ScoreManager(Clone)` must exist **before** any TileUI is instantiated. `TileUI.Start` needs them all and calls `CheckForPer`, which needs the indicator.
2. The hand lists must be filled **in place** (same `List` object). `TileUI.playerTiles` holds the reference taken at Start (`TileUI.cs:126`), and drag rules read `.Count` (`:386,473`).

---

## 3. Buffered RPC inventory

`PhotonNetwork.RemoveRPCs`, `OpRemoveCompleteCache`, `OpCleanRpcBuffer`: **no call anywhere** in `Assets/Osman` (grep).

Things that shape what a rejoiner gets:
- With `CleanupCacheOnLeave = true` (`RoomManager.cs:13,54`), the server drops a sender's cached RPCs only when that actor **leaves for good** (TTL expiry), not while it is inactive.
- Room-object instantiations (`TileManager`, `ScoreManager`) are cached globally and stay. So once any player has left permanently, the buffer is also missing that player's events.
- On rejoin, the buffer replays **from the first hand of the match**, because nothing is ever removed and the new-hand RPCs (`RPC_PrepareNextRound`, `ResetTableAndRedistribute`, `SyncShuffledTiles`, `SyncIndicatorTile`) are **not** buffered.
- Replay happens when the message queue runs after the PUN `LoadLevel("Table")` (`PhotonNetwork.cs:3067,3109`; resumed in `NewSceneLoaded` `PhotonNetworkPart.cs:1468-1475`). That is before any snapshot could be requested and answered.
- RPCs for a PhotonView that does not exist yet are dropped by PUN. Exceptions in handlers are collected and rethrown after the dispatch loop; dispatch continues (`PhotonHandler.cs:223-249`).

| # | RPC (handler) | Sender site(s) | Target | Mutates | Effect when replayed on a rejoiner's fresh Table scene | Needs buffering today? |
|---|---|---|---|---|---|---|
| B1 | `SeatManager.AssignSeatToPlayer` (`SeatManager.cs:369-388`) | `:96-101`, `:227`, `:250`, `:311` | AllBuffered | `playerSeatMap`, master writes `SeatNumber`, seat display, `PublishSeats` | Replays every seat assignment and change: seat SFX once per entry, map rebuilt (last wins). If the rejoiner is master at that moment, it **re-writes `SeatNumber`** for each entry in order. | No. The `SeatNumber` property already carries this to late joiners (fallback `:440-444`). |
| B2 | `SeatManager.FreeSeat` (`:407-428`) | `OnPlayerLeftRoom` (`:266-270`), which also fires for **inactive** players | AllBuffered | removes the actor from `playerSeatMap` | Harmless (lookup falls back to the property). It also removes the rejoiner's own entry each time it dropped. | No |
| B3 | `SetupPlayer.SetPlayerName` / `SetPlayerSeat` (`TestPlayer/SetupPlayer.cs:13-38`) | `SeatManager.cs:358-359`, on every `OnJoinedRoom` | AllBuffered | cosmetic player prefab | Every rejoin `Instantiate`s **another** player prefab (`:345-350`) and adds two more buffered RPCs. | No |
| B4 | `TileDistrubite.AssignQueueToPlayer` (`TileDistrubite.cs:365-380`) | `:327-332` (one per human per hand) | AllBuffered | `PlayerQue` property; the handler **re-sends the whole property table** of that player (`:371-373`) | The rejoiner replays every hand's queue assignment. Each one changes its local cache and **sends** the stale `PlayerQue` (plus that player's other cached props) to the server for all four players, one after another. Every client sees the PlayerQue of each player jump through old values mid-hand (risk 2). The final value equals the last hand's only if the cache order is intact. | No (the property persists) |
| B5 | `TileDistrubite.SyncQueToSeatMapping` (`:356-363`) | `:351` | AllBuffered | `seatOfQue` | The last hand's map wins. Fine, but redundant with a snapshot. | Only for a late/rejoining client; a snapshot covers it. |
| B6 | `TileDistrubite.AssignQueueComplete` (`:417-437`) → `TurnManager.StartRoundVisuals` (`TurnManager.cs:520-551`) | `TileDistrubite.cs:352` | AllBuffered | turn = 1, `TurnStartTime = now`, `canDrop = true` if local que 1, `isBotLogicPaused = false`; master `RecalculateBotStates` | Resets the turn to 1 once per hand in the buffer. If the rejoiner is que 1, it gets `canDrop = true` out of turn. MyTurn SFX. | No |
| B7 | `TurnManager.NextTurn` (`:321-360`) | `TileUI.cs:1053` (human), `TileDistrubite.cs:2149` (bot discard, master), `:2160` (bot error, master). Not buffered: `ForceSkipBotTurn` (`TurnManager.cs:202`). | AllBuffered | turn ++, `TurnStartTime`, `RecalculateAllAvailableSlots`, `RackMismatch` log, `ResetTurnFlags` if mine, SFX, `DesyncDetector.Report` | Turn = 1 + count since the last replayed B6. **Wrong** whenever a watchdog skip (unbuffered) happened or a departed actor's NextTurns were purged. Each one also logs `[ISTAKA]` mismatches and sends DesyncDetector events with wrong keys. | No |
| B8 | `TileDistrubite.AddTileFromMiddlePlayerList` (`:607-636`) | `TileUI.cs:763-767` (human draw), `TurnManager.cs:476-480` (bot draw), `GameManager.cs:274-278` (side-pick rollback) | AllBuffered | hand += `allTiles[0]`, `allTiles.RemoveAt(0)` | Empty `allTiles` (`SyncShuffledTiles` is not buffered), so `allTiles[0]` throws **ArgumentOutOfRange** once per draw of the whole match. If the snapshot was applied first: **every draw is applied again**, giving tiles to the wrong hands and draining the pile (duplicates). | No |
| B9 | `TileDistrubite.AddTileFromDropPlayerList` (`:640-671`) | `TileUI.cs:775-779` | AllBuffered | hand += `dropTile`; `DestroySideTiles` | On a fresh scene `dropTile == null`, so a **null is added** to a hand (NRE later in id lookups and the hash). After a snapshot it adds a duplicate and destroys the wrong discard visual. | No |
| B10 | `TileDistrubite.RemoveTileFromPlayerListByValue` (`:1911-1987`) | `GameManager.cs:261-266`; `ScoreManager.cs:869-874, 987-992, 1153-1158, 1806-1811, 1942-1947` | AllBuffered | hand −= id; destroys the rack visual if local | Fresh: "not found" warnings. After a snapshot: if the id is in the hand again (take-back or undo put it back), it is **removed again**. Paired add/remove replays only balance if applied to the exact state of that moment. | No |
| B11 | `TileDistrubite.MergeValidpers` (`:783-805`) | `ScoreManager.cs:880-886, 998-1004, 1165-1171` | AllBuffered | `meltedTilesN` + positions | Appends **melds from every earlier hand** (que numbers change per hand), so `meltedTiles` is wrong and `meld1..4` desyncs. After a snapshot: duplicates. | No |
| B12 | `TileDistrubite.UnMergeValidPers` (`:807-830`) | `ScoreManager.cs:1250` | AllBuffered | clears **all** of that que's melds | Erases melds out of order during replay. (It also erases earlier-turn melds in live play, see risk 9.) | No |
| B13 | `GameManager.SyncOpenedPlayerRPC` (`GameManager.cs:612-620`) | `ScoreManager.cs:1018-1022, 1185-1189` | AllBuffered | `playersWhoOpened` | Adds the ques of **every earlier hand's** openers; the per-hand reset is not buffered. Wrong limit rule (`AnyoneElseOpened`) and kafa. Also needs `ScoreManager(Clone)`, else NRE. | No |
| B14 | `GameManager.SyncOpenedPairsRPC` (`:606-610`) | `ScoreManager.cs:1191-1195` | AllBuffered | `playersWhoOpenedPairs` | Same as B13. | No |
| B15 | `TileDistrubite.AddTileToPlayerHand` (`:1728-1772`) | `ScoreManager.cs:1217-1222` (take-back), `:1304-1309` (joker commit), `:1835-1840` (joker swap), `:1932-1937` (undo) | AllBuffered | hand += tile; if local que, **instantiates a rack tile** | Adds tiles to hands; for the rejoiner's own que (by the replayed PlayerQue of that time) it **creates rack GameObjects**, possibly for a tile now on the board or from an earlier hand. Tile duplication (visual and data). | No |
| B16 | `TileDistrubite.MeldTiles` (`:903-928`) → `InstatiateMeldTiles` (`:932-1113`) | `ScoreManager.cs:1991-1996` (one per committed tile) | AllBuffered | board visuals of that que | Renders from the (wrong) `meltedTiles` of B11. `GameObject.Find(NickName + " meld").transform` has **no null check** (`:977-979`), so NRE before the seat display is built. Earlier hands' melds are drawn onto this hand's board. | No |
| B17 | `TileDistrubite.SyncProcessedTileRPC` (`:1812-1856`) | `ScoreManager.cs:1852-1858` | **OthersBuffered** | board slot (destroys the occupant) + recalc | Replays every processed tile of every hand into the current board slots and destroys whatever sits there. Whether the cache delivers OthersBuffered events back to their **own sender** on rejoin is **not verified**. | No |
| B18 | `TileDistrubite.BotDiscardTileRPC` (`:2095-2163`) | `TurnManager.cs:503-508` | AllBuffered | bot hand −= id, `InstatiateSideTiles` (sets `dropTile`); on master: `FinishGameRPC` if the pile is empty, else `NextTurn` AllBuffered | Fresh: "tile not found" error. After a snapshot: removes again and **adds a duplicate discard visual**. If the rejoiner is master at replay time, it sends **new NextTurns** (stuck or skipped turns). | No |

No pre-game RPC truly needs buffering:
- B1/B2 (seat assignment for the waiting phase) are covered by the `SeatNumber` player property and `UpdateSeatDisplay` on `OnPlayerPropertiesUpdate` (`SeatManager.cs:392-405`).
- B3 is cosmetic.
- B4/B5/B6 are hand-start RPCs. The room closes when the game starts (`SeatManager.cs:666-668`), so the only one that could ever replay them is a **rejoiner**, and for a rejoiner they are wrong (B4, B6) or redundant with a snapshot (B5).
- B7-B18 are buffered for no working reason: hands and the indicator cannot be rebuilt from the buffer, since the deal and the human discard are not buffered.

---

## 4. Safe hand-back points

### 4.1 Bot turn (master only)

- Trigger: `TurnManager.Update`, every frame on the master (`TurnManager.cs:60-119`), when `isGameReady && !isGameEnded` and no **active** Player has `PlayerQue == currentTurnPlayer` (`:77-90`).
- If `!isBotMoving`, it starts `PlayBotTurn(currentTurnPlayer)`. Otherwise it counts `botTimeoutTimer` and calls `ForceSkipBotTurn` after 6 s (`:22,103-110`): `StopAllCoroutines`, then **unbuffered** `NextTurn` (`:192-203`).
- When the current seat is human, it sets `isBotMoving = false` and `botTimeoutTimer = 0`. It **does not stop** a running `PlayBotTurn` (`:113-118`).
- `OnPlayerLeftRoom` (`:362-384`) only updates the unused `isBotActive` / `RecalculateBotStates`.

`PlayBotTurn(que)` (`:440-517`), step by step:

| t | Action | Table quiet? |
|---|---|---|
| 0 | `isBotMoving = true`, timer 0 | quiet (nothing sent) |
| 0-1.5 s | `WaitForSeconds(1.5)` (`:447`) | quiet in data, but the coroutine is live |
| 1.5 s | checks `currentTurnPlayer != que` or ended; that is the only re-check (`:450-457`) | — |
| 1.5 s | if bot hand < 15: `AddTileFromMiddlePlayerList` AllBuffered (`:473-483`) | **not quiet** (drawn, not discarded) |
| 1.5-2.7 s | `WaitForSeconds(1.2)` | **not quiet** |
| 2.7 s | `FindBestTileToDiscardForBot` (on a copy, `ScoreManager.cs:2319-2326`), `BotDiscardTileRPC` AllBuffered (`TurnManager.cs:501-508`) | **not quiet** until NextTurn |
| on arrival | each client removes the tile and shows the discard; the master checks pile-empty → `FinishGameRPC`, else sets its own `canDrop = false` and sends `NextTurn` AllBuffered (`TileDistrubite.cs:2104-2151`) | not quiet until `NextTurn` runs |
| NextTurn | `isBotMoving = false`, turn ++ (`TurnManager.cs:321-333`) | **quiet** |

The bot never opens, processes or takes from the side. It draws only if hand < 15 (`:473`). If the human dropped after drawing, the bot just discards.

Quiet for the bot seat:
- (a) any time it is **not** that seat's turn;
- (b) at that seat's turn, after `NextTurn` arrived and **before** `PlayBotTurn` started, which is a one-frame window on the master;
- (c) during the first 1.5 s wait, but the coroutine must then be cancelled or it continues. It re-checks only the turn owner, not who controls the seat.

Not quiet: from the draw RPC until the following `NextTurn` has run on every client.

### 4.2 Human turn (on the human's client)

1. Turn start: `NextTurn` on the owner runs `ResetTurnFlags` (`TurnManager.cs:345-352`). Quiet.
2. Draw. Middle: `SetTileData(allTiles[0])` + `AddTileFromMiddlePlayerList` AllBuffered + `canDrop = true` (`TileUI.cs:758-770`). Side: `hasPickedFromSide = true`, `OnSideTilePicked`, `AddTileFromDropPlayerList` AllBuffered, `canDrop = true` (`:771-782`). After a middle draw with nothing else done, the state is quiet in data (hand 15, nothing pending). After a side pick it is **not** quiet: there is an open obligation (`CanFinishTurn` `TurnManager.cs:263-288`) and `currentSidePickTile` exists only locally.
3. Opening (`ScoreManager.cs:756-1200`), per tile: `DeactivatePlayerTileByIndex` All + `RemoveTileFromPlayerListByValue` AllBuffered, then `MergeValidpers` AllBuffered, `SyncOpenedPlayerRPC` (+ `SyncOpenedPairsRPC`, `IsDoubleOpened`) AllBuffered. Other clients now have the data but **no board visuals** until the discard. **Not quiet** until the discard.
4. Processing (`PerformTileProcessing` `ScoreManager.cs:1728-1842`): local board tile, `RemoveTileFromPlayerListByValue` AllBuffered, joker swap `AddTileToPlayerHand` AllBuffered, penalty kept in `PenaltySystem.currentTurnPenalties`, sync kept in `pendingSyncActions`. **Not quiet.**
5. Take-back / undo (`:1202-1276`, `:1906-1972`) send compensating buffered RPCs. Not quiet.
6. Discard: `ExecuteNextTurn` (`TileUI.cs:871-883`) runs `CommitAllTurnPenalties` (`ApplyPenaltyRPC` All) and `CommitFinalTableLimit` (room prop + `OpenValueRPC`). Then `NextTurnRoutine` (`:887-1057`):
   - side success penalty → Master;
   - `OnPlayerMoveFinished` → `CheckGameStatus` (`FinishGameRPC` if the hand ended);
   - slide 0.3 s;
   - `RemoveTileFromPlayerList` All, wait 0.05 s;
   - `CommitAndStoreMelds` (`MeldTiles` AllBuffered ×n), `CommitJokerTransactions`, `ExecutePendingSyncs` (`SyncProcessedTileRPC` OthersBuffered), `ClearTurnHistory`;
   - `CheckPenaltyRPC` → Master, wait 0.05 s;
   - pile empty → `FinishGameRPC`; else `canDrop = false`, `NextTurn` AllBuffered.

   **Not quiet** from the discard drop until `NextTurn` has run everywhere (about 0.4 s plus latency).
7. Time-out: `AutoPlayTurn` (`TurnManager.cs:143-184`) on the owner: cancel the side pick, simulate a middle draw, simulate a discard; it waits up to 3+3+5 s. Not quiet while running. It is blocked by `TileUI.Busy` (`:136`).

**Quiet points for any seat:**
- **Q1**: between `NextTurn` having run on all clients and the new turn owner's first action (draw, or bot `PlayBotTurn` start).
- **Q2**: any moment of another seat's turn, for the seat being handed back. Its own state cannot change then; but "the table" is quiet only if the acting seat is itself between actions, which matters for building the snapshot, not for the seat switch.
- The safest snapshot instant is right after `NextTurn` on the master (Q1). The switch for the returning seat is safest while it is **not** that seat's turn, or at Q1 of its own turn before anything was drawn.

### 4.3 What the returning client must set to act

Returning **during another seat's turn**:
- `TurnManager.currentTurnPlayer` = snapshot; `TurnStartTime` = the turn's start server time (the timer bar and `TickTurnTimer` need > 0, `TurnManager.cs:132`, `UIManager.cs:115`).
- `canDrop = false`; `hasPickedFromSide = hasOpenedThisTurn = hasProcessedThisTurn = false`; `autoPlaying = false`.
- `GameManager.isGameReady = true`, `isGameEnded` from the snapshot.
- `ScoreManager.hasOpenedSeries/hasOpenedPairs`, `occupiedRows*`, `committedMelds` (from item 18/14). `playersWhoOpened/Pairs` from the snapshot.
- Pending lists empty; `FirstOpenThisTurn = false`, `OpenedValueThisTurn = 0`; `GameManager.currentSidePickTile = null`; `PenaltySystem.currentTurnPenalties` empty.
- The next own turn then works through the normal `NextTurn` → `ResetTurnFlags` path.
- `TurnManager.StartGame` / `StartRoundVisuals` must **not** run afterwards, or que 1 gets `canDrop` (`TurnManager.cs:48-51,544-549`).

Returning **during its own turn** (only at Q1, or after the bot has drawn):
- Same as above, plus `canDrop = (own hand count == 15)`. 15 means a tile was already drawn this turn (by the human before the drop, or by the bot, `TurnManager.cs:473`), so the next legal action is a discard, open or process. 14 means draw first.
- `hasPickedFromSide = false` (the bot never side-picks; a human side pick in flight is lost with the dropped client).
- `TurnStartTime`: the original turn start. If it has already expired, `AutoPlayTurn` fires on the next frame (`TurnManager.cs:123-140`). A fresh time would need to be agreed by everyone, because the timer bar is drawn on all clients from their own `TurnStartTime`.
- The master must no longer run `PlayBotTurn` for that seat, and any running one must be cancelled (`TurnManager.cs:113-118` does not do that today).
- MyTurn SFX / indicators: `UIManager.UpdateTurnIndicators(currentTurnPlayer)`.

---

## 5. Identity and seat-lookup blockers

PUN keeps inactive players in `PlayerList` / `CurrentRoom.Players` until TTL expiry. A rejoiner comes back with the **same ActorNumber**, same NickName and the same stored props. `OnPlayerLeftRoom` fires on inactive (`LoadBalancingClient.cs:3408`), `OnPlayerEnteredRoom` on return (`:3376`). After TTL expiry the Player disappears for good, and that seat has no Player, like a bot seat.

Loops over `PlayerList` / `Players`, and what each does today:

| Site | Inactive player | Comment |
|---|---|---|
| `TurnManager.Update` (`TurnManager.cs:77-90`) | **skipped**, so the bot plays that seat | The only place that switches control. It flips the instant the rejoiner is active again, before it has rebuilt. |
| `TurnManager.RecalculateBotStates` (`:403-426`) | included (marked human) | Feeds only the unused `isBotActive`. |
| `TileDistrubite.AssignPlayerQueue` (`TileDistrubite.cs:302-347`, `CurrentRoom.Players.Values`) | included | The next hand may start at an inactive seat; it still gets a que (props of inactive actors can be set). OK. |
| `TileDistrubite.DistributeTilesToAllPlayers` (`:443-452`) | included | Only the unused `playerNamesByQueue`. |
| `InstatiateMeldTiles` (`:934-1112`) | included (good, a dropped player's melds keep rendering) | After TTL expiry there is no Player for that que, so the melds are **silently not rendered**. Also NRE if the name lookup fails (`:977-979`). |
| `UpdateAvailableForPlaceholders` (`:1242-1262`) | included | Same Find NRE risk (`:1260-1262`). |
| `InstantiateActiveTiles` (`:1476-1486`) | included | Dead code. |
| `RecalculateAllAvailableSlots` (`:1557,1587`) | included | Seats without a Player (bot or expired) are **ignored**, so the işlek list misses their melds, which gives wrong 250 penalties (`GameManager.cs:347-352`). |
| `SyncProcessedTileRPC` (`:1821-1830`) | included | No Player for the que → **returns**: the processed tile is lost on that client (the sender already removed it from the hand). |
| `AppendSyncState` board (`:2219-2231`) | included | Seats without a Player are not hashed. |
| `ScoreManager.UpdatePlayerCustomProperties` (`ScoreManager.cs:248-272`) | included (writes `PlayerScore` on the inactive actor) | OK. |
| `ScoreManager.ActivePers` (`:1624-1631`) | included | Cannot process onto seats without a Player. |
| `ScoreManager.GetOwnerQueFromPlaceholder` (`:2250-2275`) | included | Container name must equal `NickName + " meld"`, else -1 and the drop is refused. |
| `ProcessAssist.Collect` (`ProcessAssist.cs:108-123`) | included | Same as ActivePers. |
| `GameManager.CalculateAndDistributeScores` (`GameManager.cs:509-543`) | included | Seats without a Player (bot or expired) get **no row** in `OnGameEndedDetailedRPC`; `WriteHandBreakdown` covers all seats via `seatOfQue` (`:572-589`). |
| `SeatManager.PublishSeatNames` (`SeatManager.cs:155-162`) | included (name kept, not "(bot)") | Display. |
| `SeatManager.PublishSeats` (`:189`) | included | Lobby list, pre-game only. |
| `SeatManager.StartWithBotsAfterDelay` (`:223-228`) | included | Pre-game. |
| `SeatManager.TakenSeats` (`:281-286`) | included (seat stays taken, good) | — |
| `SeatManager.UpdateSeatDisplay` (`:476-514`) | included (name shown, meld/drop areas keep the NickName) | After TTL expiry the areas are renamed back to scene defaults (`:464-465`), which cuts off all name lookups for that seat's board. |
| `SeatManager.QuesInTableOrder` (`:587-597`) | included | — |
| `SeatManager.GetRelativePlayerOrder` (`:622-650`) | included | No caller. |
| `DesyncDetector.Store` expected count (`DesyncDetector.cs:177`) | **excluded** | OK. |

Further identity blockers:
- **Board lookups key on NickName**, through `GameObject.Find(NickName + " meld")` (sites above and `ScoreManager.cs:74`). Two players with the same display name (`PhotonNetwork.NickName = DisplayName`, `UI/MainMenuUIHandler.cs:113`, `UI/LobbyAccountUI.cs:114`) resolve to the same object, so melds and processing go to the wrong area. A NickName set differently on the rejoined client (for example a changed profile name) breaks every lookup.
- `SeatManager.MeldAreaOfQue` / `QueOfMeldArea` (`SeatManager.cs:563-581`) already give a seat-keyed lookup through `seatOfQue`. The gameplay code above does not use them yet.
- **No `AuthValues.UserId`** is set (grep: none in `Assets/Osman/Scripts`). Photon assigns a UserId at authentication (`LoadBalancingClient.cs:2790-2795`), which lives only for the app session. `ReconnectAndRejoin` / `RejoinRoom` work only within that process. After an app restart the client cannot rejoin as the same actor. The app's own stable id is `GameServices.Auth.UserId` (`Services/GameServices.cs:39`).
- `FreeSeat` removes the actor from `playerSeatMap` on every client when it goes inactive (`SeatManager.cs:266-270`). Nothing re-adds it on return; lookups fall back to the `SeatNumber` prop (`:440-444`). OK, but `playerSeatMap` stays stale.
- `SeatManager.OnPlayerEnteredRoom` on the **original master** does nothing for a rejoiner (`gameIsStart` true, `:239-256`). On a **new master** `gameIsStart` is false, but the rejoiner already has a seat and `StartGame` is a no-op without `tileDistrubite` (`:662`). `RequestSeatRPC` is accepted on a new master (`:307`), see section 6.

---

## 6. Master leaves: master-only state

| State | Where | Lost / effect when the master drops |
|---|---|---|
| In-hand penalties `playerScores` | `ScoreManager.cs:35`; written only on master (`GameManager.cs:209,237,305,318,330,357`; `PenaltySystem.cs:202-207`) | New master's dictionary is all zeros, so end-of-hand penalties are lost (`GameManager.cs:517-520,587`). Partial copies: `PlayerScore` prop of humans (`ScoreManager.cs:262`), but not for bots. `MatchScores.current` on every client has (seat, amount) for **every** announced penalty (`GameManager.cs:216-225`, `MatchScores.cs:125-135`); every live penalty path announces, so the per-seat sum matches `playerScores` (keyed by que, so map through `seatOfQue`). |
| RPCs in flight to `MasterClient` | `AddGeneralPenaltyRPC` (`GameManager.cs:198`), `ApplySidePickSuccessPenaltyRPC` (`TileUI.cs:933-938`), `CheckPenaltyRPC` (`TileUI.cs:1026-1031`), `RequestSeatRPC` (`SeatManager.cs:302`) | Dropped if the master goes away before processing. Penalty lost. |
| Finish gate | `GameManager.finishedRound/finishedWinner` (`GameManager.cs:465`) | New master accepts a second result for the same hand (draw then win) → double `OnGameEndedDetailedRPC`; `restartRound` (all clients) still prevents a second restart (`:647-651`). |
| Bot driver | `isBotMoving`, `botTimeoutTimer`, running `PlayBotTurn` (`TurnManager.cs:20-22,440-517`) | Mid bot turn: draw sent, discard not. New master starts a fresh `PlayBotTurn`; hand is 15, so it discards only. Self-healing. `isBotActive` is unused. |
| New-hand pipeline coroutines | `WaitAndRedistribute` 0.2 s (`GameManager.cs:743-748`), `ShuffleTiles` / `DistributeWithDelay` 0.5 s (`TileDistrubite.cs:281-292`), `CountdownAndShuffle` (`SeatManager.cs:682-703`) | Master drops inside the window: non-masters have already cleared the table (`GameManager.cs:736-740`), and no one deals again. **Stuck forever.** `RestartSequence` runs on all clients and checks `IsMasterClient` at the end (`:680-686`), so the 10 s wait itself survives. |
| Shuffle RNG / seed | `Random` state, `DesyncDetector.seededDeals` (`DesyncDetector.cs:76-85`) | Next deal reproducibility only. |
| Seat-manager master state | `gameIsStart` (`SeatManager.cs:28`, set only on master `:666`), `tileDistrubite` / `sManager` refs (`:94,700`), `rememberedNames` (`:145`), `lastPublished` (`:200`), `StartWithBotsAfterDelay` coroutine | On the new master, `gameIsStart = false`, so `RequestSeatRPC` (`:307`) **lets a player change seats mid-hand**. `OnPlayerEnteredRoom` may assign seats (`:239-253`). `PublishSeats` is gated by `SeatsLocked` (`:186`), which is fine. `rememberedNames` is rebuilt from `SeatNames` (`:202-211,168-173`). |
| DesyncDetector comparison | `received`, `badKeys`, counters (`DesyncDetector.cs:36-43,149-183`) | Test bookkeeping only. |
| Master's own per-turn local state, if it is its turn | the same local items as any human (section 1.2 #15, 1.4 #26-28) | Same mid-turn loss as any human (risk 3). |
| Data every client has (survives) | hands, pile, indicator UI, `seatOfQue`, `playersWhoOpened/Pairs`, `OpenValues`, board, room props (`TableLimit`, `IsDoubleOpened`, `CurrentRound`, `SeatNames`, `Hand{n}`) | — |

When the old master rejoins it is a **non-master**: its new scene's master-only fields stay default, which is correct.

Its buffered RPCs (B4-B7, B18 and everything else it sent) **stay in the cache while it is inactive**. They are **purged** if its TTL expires (`CleanupCacheOnLeave`). The room objects it instantiated stay (global cache).

If every remaining player is inactive or gone (the solo "Play with bots" room, `RoomManager.cs:106-107`), nobody is master to play the bots. With `EmptyRoomTtl = 0` (`RoomManager.cs:10,55`) the room probably closes as soon as no active actor is left, which would make a rejoin impossible. This is Photon behaviour I did not verify here.

---

## 7. Scene and flow entry points

Today's path:
1. **MainMenu scene**: `PhotonManager.Start` (DontDestroyOnLoad) waits for services, then `ConnectUsingSettings` (`Photons/PhotonManager.cs:11-57`). `OnConnectedToMaster` calls `JoinLobby` (`:59-63`). `MainMenuUIHandler.EnterLobby` waits for `InLobby`, sets `NickName = DisplayName`, then `ChangeScene("LobbyMenu")` (`UI/MainMenuUIHandler.cs:90-115`).
2. **LobbyMenu**: the user creates or joins.
   - `RoomManager.CreateCustomRoom` (`RoomManager.cs:41-76`), `JoinRandomRoomOrCreateAuto` (`:110-120`), `JoinRoomAtSeat` / `JoinRoomManual` / `JoinRoomWithPassword` (`:132-158`).
   - Each calls **`SetPickSeat`** (`:73,117,155`), which nulls `SeatNumber`, `PlayerQue`, `WantedSeat` and sets `PickSeat` (`:90-99`).
   - `RoomManager.Start` sets `AutomaticallySyncScene = true` (`:30`).
3. `RoomManager.OnJoinedRoom` (`:160-169`): `PlayerScore = 0`, `ChangeScene("Table")` (plain `SceneManager.LoadSceneAsync`, `SceneChangeManager.cs:28-38`). For a non-master, PUN also runs `LoadLevelIfSynced` → `PhotonNetwork.LoadLevel("Table")`, which pauses the message queue (`PhotonNetworkPart.cs:2093-2122,2495`; `PhotonNetwork.cs:3104-3109`). The master's `NewSceneLoaded` wrote `curScn = "Table"` (`PhotonNetworkPart.cs:1476-1479`). Result: **two loads** of Table for every joiner.
4. **Table**: `SeatManager.Start` → `OnJoinedRoom()` (`SeatManager.cs:51-111`), then section 2.

What happens on a drop today:
- There is **no `OnDisconnected` handler** anywhere in `Assets/Osman` (grep).
- PUN `OnLeftRoom` fires on disconnect (`LoadBalancingClient.cs:3156-3158`). `ExitButton.OnLeftRoom` loads LobbyMenu (`UI/ExitButton.cs:29-33`). `LeftRoomCleanup` destroys the room objects locally (`PhotonNetworkPart.cs:257-279`).
- In the lobby the client stays disconnected. `PhotonManager` connects only from `Start` / the retry button (`PhotonManager.cs:42-57`). `ExitButton.OnConnectedToMaster → JoinLobby` died with the Table scene.
- A voluntary exit during the game also leaves inactive (`ExitButton.cs:18`: `becomeInactive = !IsOpen`).

Where a rejoin entry would hook in:
- (a) A DontDestroyOnLoad callback owner (PhotonManager is one) implementing `OnDisconnected` and calling `PhotonNetwork.ReconnectAndRejoin()` (`PhotonNetwork.cs:1988-2008`), in the same app session, before or instead of the LobbyMenu switch that `ExitButton.OnLeftRoom` does.
- (b) Or from the lobby, `PhotonNetwork.RejoinRoom(roomName)` (`PhotonNetwork.cs:1952`), which needs the room name remembered and the same Photon UserId.
- Either way the client then joins with `RejoinOnly`. `LocalPlayer.HasRejoined` is true (`LoadBalancingClient.cs:3361`) and can tell the Table code it is a rejoin.

What `RoomManager.OnJoinedRoom`, `SetPickSeat` and the Table start would wrongly do on a rejoin:
- `SetPickSeat` (`RoomManager.cs:90-99`), if a normal join method is reused: nulls `SeatNumber` and `PlayerQue` in the **local** cache. On a `RejoinOnly` join the props are not sent (`LoadBalancingClient.cs:2861-2863`) and the server copy is merged back from the Join event (`:3349`). This is probably safe, **not verified**. A plain `JoinRoom` (not rejoin) for a closed room fails anyway (`IsOpen = false`, `SeatManager.cs:667`).
- `RoomManager.OnJoinedRoom`: `PlayerScore = 0` (`:164-166`), so the rejoiner's displayed penalty resets (the master's `playerScores` is untouched; the next `UpdatePlayerScore` rewrites it). `ChangeScene("Table")` causes the double load above. If the plain load finishes after PUN's load, the scene that received the buffered RPCs (and any snapshot) can be replaced.
- `DesyncDetector.OnJoinedRoom` resets `deal` / `move` (`DesyncDetector.cs:61-71`).
- `SeatManager.OnJoinedRoom` on the rejoiner:
  - `UIRoot.HideLoading`;
  - **another** player prefab plus 2 buffered RPCs (`:338-365`);
  - `RequestWantedSeat` skipped (seat > 0);
  - if the rejoiner is **master** at that moment (everyone else inactive), it runs `InstantiateRoomObject(TileManager)` **again** (`:86-94`), `AssignSeatToPlayer`, and in a FillWithBots room `StartWithBotsAfterDelay` → `StartGame` → `CountdownAndShuffle`: a second `ScoreManager` and a **new shuffle mid-hand** (`gameIsStart` is false in the new scene, `:28,213-234,655-703`).
- `ScoreManager.Start` (from the cached room-object instantiation) on the rejoiner: `PlayerScore = 0` (`ScoreManager.cs:66-68`); `turnManager.StartGame()` gives `canDrop = true` if the local que is 1 and sets `TurnStartTime = now` (`TurnManager.cs:42-58`); master-only `InitializeScores`.
- `GameManager.Start` on a rejoiner that is master would set `TableLimit = 51` mid-hand (`GameManager.cs:94-97`). `GameManager.Awake` sets `isGameReady = false` (`:80`), which correctly holds bots on that client until restore, but only if it is master.
- `MatchScores.Awake` re-reads room props (`MatchScores.cs:48-61`). Correct; it restores history and seat names.

---

## 8. Risk list (most likely to lose or duplicate a tile, or stick a turn)

1. **Buffered gameplay RPC replay onto the rebuilt table** (B7-B18).
   - `AddTileFromMiddlePlayerList` throws on the empty pile (`TileDistrubite.cs:615-631`). After a snapshot it re-applies every draw of the match.
   - `AddTileToPlayerHand` creates rack GameObjects for the local que (`:1763-1771`).
   - `MergeValidpers` / `MeldTiles` / `SyncProcessedTileRPC` draw earlier hands' melds onto this board (`:783-805, 903-1113, 1812-1856`).
   - `AddTileFromDropPlayerList` adds `null` (`:648`).
   - Reason: nothing removes RPCs, the new-hand RPCs are not buffered, and the replay runs automatically as soon as the scene's message queue resumes.
2. **Replayed `AssignQueueToPlayer` re-writes everyone's `PlayerQue`** (`TileDistrubite.cs:365-380`, sent `:327-332`). The rejoiner sends stale queue numbers for all four players, so mid-hand every client's `GetPlayerTiles`, `IsPlayerTurn`, `GetQueueNumberOfPlayer` and meld ownership briefly read old values. Wrong hand dragged or discarded, wrong seat "human", bot plays a human seat or vice versa.
3. **Drop in the middle of a human turn leaves half-sent actions**:
   - opened tiles were removed from the hand on all clients (`ScoreManager.cs:869-874`) and `meltedTiles` / `playersWhoOpened` were set (`:880-886,1018-1022`);
   - but `MeldTiles` and `SyncProcessedTileRPC` are only sent at discard (`TileUI.cs:1014-1018`), and the pending penalties (`PenaltySystem.cs:23`) and side-pick state (`GameManager.cs:70`) die with the client.
   - The bot then discards (`TurnManager.cs:473-508`), so those tiles are **gone from every hand and from every board**.
   - If the drop falls between `RemoveTileFromPlayerList` (unbuffered, `TileUI.cs:990-995`) and `NextTurn` (`:1053`), the bot draws a second time in the same turn (`TurnManager.cs:473`).
4. **Bot coroutine not stopped when the seat becomes human again.** `Update` only clears `isBotMoving` (`TurnManager.cs:113-118`). `PlayBotTurn` re-checks only the turn owner, once (`:450-457`), and still sends the draw (`:476-480`) and the discard (`:503-508`). Meanwhile the restored human may also draw or discard: double draw or double discard on one seat, and two `NextTurn`s (turn skipped).
5. **Control switches on `IsInactive`, before the rejoiner has rebuilt** (`TurnManager.cs:81-89`). If the rejoin lands at that seat's turn before `PlayBotTurn` starts, no bot plays and the human client is not ready: `TurnStartTime` is 0 or `canDrop` is false, and `TickTurnTimer` is blocked (`:126-139`). Nobody moves, so the turn is **stuck**. The watchdog only runs for bot turns (`:101-111`).
6. **Fresh-deal side effects on the rejoiner set wrong permissions.** `ScoreManager.Start` → `TurnManager.StartGame` (`TurnManager.cs:48-51`) and the replayed `AssignQueueComplete` → `StartRoundVisuals` (`:536-549`) give `canDrop = true` to the que-1 player out of turn and reset `currentTurnPlayer = 1` (`:523`). An out-of-turn draw (`TileUI.cs:758-770`) broadcasts `AddTileFromMiddlePlayerList`, giving an extra tile and a pile desync.
7. **Discard-pile bookkeeping is positional.** `DestroySideTiles` destroys `droppedTiles.Last()` on every non-taker (`TileDistrubite.cs:738-772`), and `InstatiateSideTiles` overwrites `dropTile` (`:717-718`). If the restored discard visuals are not appended in the original time order, or `dropTile` is not restored exactly (it stays set after a take, `:640-671`), the next side take removes the wrong visual (ghost or missing tile on screen). A later take adds the wrong data tile.
8. **The indicator exists only as a UI object** (`TileDistrubite.cs:1863-1872`); `SyncIndicatorTile` is not buffered (`:225`). Any TileUI created before the indicator runs `CheckForPer` (`TileUI.cs:128`) with a null indicator. Rule checks (`OkeyRuleEngine.IsSingleColor`), the joker-swap eligibility (`ScoreManager.cs:1316-1317`), discard penalties (`GameManager.cs:348-353`) and the colour multiplier (`:114-138`) all read it. If the indicator is missing, okey-related tiles are misjudged: wrong 250 penalties and swaps refused or allowed wrongly.
9. **Board lookups by `NickName + " meld"`**: `InstatiateMeldTiles` has no null check (`TileDistrubite.cs:977-979`) and `SyncProcessedTileRPC` silently drops the tile (`:1821-1830`). Other sites: `ScoreManager.cs:74`, `:1629`, `:2258-2271`; `ProcessAssist.cs:110`. The names are only correct after `UpdateSeatDisplay` with seat props known (`SeatManager.cs:453-514`); they are wrong for duplicate nicknames and reset after TTL expiry (`:464-465`). Restoring board tiles before the names are set loses them. In live play, `UnMergeValidPers` clears **all** of a que's `meltedTiles`, including earlier turns (`TileDistrubite.cs:807-830`). That data never matches what is on the board; a snapshot built from `meltedTiles` instead of the board would lose those melds.
10. **Master drop during the new-hand pipeline or with penalties in flight**: `WaitAndRedistribute` 0.2 s (`GameManager.cs:743-748`) and `DistributeWithDelay` 0.5 s (`TileDistrubite.cs:281-292`). Non-masters have already wiped the table (`GameManager.cs:736-740`, `TileDistrubite.cs:2007-2059`), so nobody re-deals: **stuck**. `playerScores` (`ScoreManager.cs:35`) and the MasterClient-targeted penalty RPCs are lost. A new master with `gameIsStart = false` accepts `RequestSeatRPC` mid-hand (`SeatManager.cs:304-313`), which moves a `SeatNumber` and so shifts every relative seat and drop/meld area under live tiles.

Also worth tracking (just outside the top ten):
- **Hand list identity and order**: `TileUI.playerTiles` caches the list reference (`TileUI.cs:126`). Replacing the list object instead of refilling it breaks the 15-tile draw guard (`:386,473`) and `RackMismatch` (`TileDistrubite.cs:2169-2198`). Hand **order** is in the hash (`:2212`).
- **Joker values**: they drift per client (`OkeyRuleEngine.cs:96-134,202`; `ScoreManager.cs:1286`), and series placement uses the joker's `number` (`TileDistrubite.cs:1010`). An okey restored from its data fields can land one slot off; restore from the slot index.
- **Double Table load** (`RoomManager.cs:168` plus PUN `LoadLevelIfSynced`): the first `sceneLoaded` resumes the queue (`PhotonNetworkPart.cs:1468-1475`) and the second load can replace a scene that already got the buffer or the snapshot.
- **Extra player prefab and buffered RPCs on every rejoin** (`SeatManager.cs:345-359`). If a rejoiner becomes master, `OnJoinedRoom` instantiates a second `TileManager(Clone)` (`:86-91`). `GameObject.Find("TileManager(Clone)")` (`ScoreManager.cs:72`, `TileUI.cs:123`) and `FindObjectOfType<TileDistrubite>` (`GameManager.cs:54`) may then pick different instances.
- **Drop detected late**: a client with a dead link stays "active" until the server times it out. During that time only its own client could auto-play (`TurnManager.cs:123-140`), so its turn hangs until Photon marks it inactive. Client `DisconnectTimeout = 30000` (`PhotonManager.cs:27`).
