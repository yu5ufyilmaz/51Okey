# SFX selection (proposal)

## 1. Method
No ffprobe/ffmpeg on PATH and no Ogg decoder available. Duration computed from Ogg container (last page granule / Vorbis sample rate) with `sfxpick/dur.py`. **Loudness NOT measured**: max dB shown as `?`; volume multipliers are guesses from material (impact-sounds assumed louder than casino/UI) and must be tuned by ear.

## 2. Families per pack

### casino-audio
| prefix | count | duration (s) | suggests |
|---|---|---|---|
| Preview | 1 | 35.11-35.11 | cards/chips/dice |
| card-fan- | 2 | 0.72-1.31 | cards/chips/dice |
| card-place- | 4 | 0.46-0.88 | cards/chips/dice |
| card-shove- | 4 | 0.69-0.81 | cards/chips/dice |
| card-shuffle | 1 | 3.06-3.06 | cards/chips/dice |
| card-slide- | 8 | 0.46-0.67 | cards/chips/dice |
| cards-pack-open- | 2 | 0.73-0.94 | cards/chips/dice |
| cards-pack-take-out- | 2 | 0.50-0.63 | cards/chips/dice |
| chip-lay- | 3 | 0.17-0.24 | cards/chips/dice |
| chips-collide- | 4 | 0.22-0.26 | cards/chips/dice |
| chips-handle- | 6 | 0.23-0.83 | cards/chips/dice |
| chips-stack- | 6 | 0.17-0.37 | cards/chips/dice |
| dice-grab- | 2 | 0.70-0.83 | cards/chips/dice |
| dice-shake- | 3 | 1.38-1.52 | cards/chips/dice |
| dice-throw- | 3 | 0.40-0.63 | cards/chips/dice |
| die-throw- | 4 | 0.47-0.73 | cards/chips/dice |

### impact-sounds
| prefix | count | duration (s) | suggests |
|---|---|---|---|
| footstep_carpet | 5 | 0.14-0.14 | impact materials; footsteps |
| footstep_concrete | 5 | 0.11-0.11 | impact materials; footsteps |
| footstep_grass | 5 | 0.59-0.78 | impact materials; footsteps |
| footstep_snow | 5 | 0.37-0.37 | impact materials; footsteps |
| footstep_wood | 5 | 0.25-0.25 | impact materials; footsteps |
| impactBell_heavy | 5 | 0.30-1.74 | impact materials; footsteps |
| impactGeneric_light | 5 | 0.12-0.14 | impact materials; footsteps |
| impactGlass_heavy | 5 | 0.17-0.43 | impact materials; footsteps |
| impactGlass_light | 5 | 0.21-0.21 | impact materials; footsteps |
| impactGlass_medium | 5 | 0.54-0.54 | impact materials; footsteps |
| impactMetal_heavy | 5 | 0.12-0.36 | impact materials; footsteps |
| impactMetal_light | 5 | 0.21-0.48 | impact materials; footsteps |
| impactMetal_medium | 5 | 0.11-0.27 | impact materials; footsteps |
| impactMining | 5 | 0.80-0.99 | impact materials; footsteps |
| impactPlank_medium | 5 | 0.78-0.78 | impact materials; footsteps |
| impactPlate_heavy | 5 | 0.35-0.56 | impact materials; footsteps |
| impactPlate_light | 5 | 0.49-0.66 | impact materials; footsteps |
| impactPlate_medium | 5 | 0.52-0.65 | impact materials; footsteps |
| impactPunch_heavy | 5 | 0.46-0.65 | impact materials; footsteps |
| impactPunch_medium | 5 | 0.40-0.54 | impact materials; footsteps |
| impactSoft_heavy | 5 | 0.50-0.57 | impact materials; footsteps |
| impactSoft_medium | 5 | 0.12-0.18 | impact materials; footsteps |
| impactTin_medium | 5 | 0.13-0.21 | impact materials; footsteps |
| impactWood_heavy | 5 | 0.31-0.31 | impact materials; footsteps |
| impactWood_light | 5 | 0.27-0.27 | impact materials; footsteps |
| impactWood_medium | 5 | 0.33-0.33 | impact materials; footsteps |

### interface-sounds
| prefix | count | duration (s) | suggests |
|---|---|---|---|
| back | 4 | 0.06-0.09 | UI blips |
| bong | 1 | 0.12-0.12 | UI blips |
| click | 5 | 0.01-0.10 | UI blips |
| close | 4 | 0.15-0.32 | UI blips |
| confirmation | 4 | 0.29-0.54 | UI blips |
| drop | 4 | 0.11-0.29 | UI blips |
| error | 8 | 0.10-0.53 | UI blips |
| glass | 6 | 0.11-0.69 | UI blips |
| glitch | 4 | 0.01-0.03 | UI blips |
| maximize | 9 | 0.19-0.53 | UI blips |
| minimize | 9 | 0.19-0.53 | UI blips |
| open | 4 | 0.15-0.32 | UI blips |
| pluck | 2 | 0.10-0.16 | UI blips |
| question | 4 | 0.33-0.49 | UI blips |
| scratch | 5 | 0.12-0.33 | UI blips |
| scroll | 5 | 1.00-1.00 | UI blips |
| select | 8 | 0.04-1.94 | UI blips |
| switch | 7 | 0.50-0.62 | UI blips |
| tick | 3 | 0.02-0.05 | UI blips |
| toggle | 4 | 0.07-0.14 | UI blips |

### ui-audio
| prefix | count | duration (s) | suggests |
|---|---|---|---|
| Preview | 1 | 14.14-14.14 | UI clicks/switches |
| click | 5 | 0.03-0.09 | UI clicks/switches |
| mouseclick | 1 | 0.06-0.06 | UI clicks/switches |
| mouserelease | 1 | 0.07-0.07 | UI clicks/switches |
| rollover | 6 | 0.06-0.23 | UI clicks/switches |
| switch | 38 | 0.03-0.51 | UI clicks/switches |

### music-jingles
| prefix | count | duration (s) | suggests |
|---|---|---|---|
| Preview | 1 | 13.02-13.02 | short instrument jingles |
| jingles_NES | 17 | 0.37-1.76 | short instrument jingles |
| jingles_HIT | 17 | 0.28-1.16 | short instrument jingles |
| jingles_PIZZI | 17 | 0.46-1.32 | short instrument jingles |
| jingles_SAX | 17 | 0.37-1.74 | short instrument jingles |
| jingles_STEEL | 17 | 0.59-1.55 | short instrument jingles |

Notes: casino-audio also has Preview (35 s, ignore); music-jingles has 5 style folders (8-Bit=NES, Hit=HIT, Pizzicato=PIZZI, Sax=SAX, Steel=STEEL; 17 each, 0.28-1.76 s); ui-audio switch family is 38 files 0.03-0.51 s. Names below are `pack/Audio/<name>.ogg` unless jingle (`music-jingles/<Style> jingles/jingles_XXnn.ogg`). Abbreviations: CA=casino-audio, IM=impact-sounds, IF=interface-sounds, UA=ui-audio, MJ=music-jingles.

## 3. Selection (max dB unknown everywhere)
| no | SfxId | chosen files | dur (s) | max dB | vol | project name(s) |
|---|---|---|---|---|---|---|
| 0 | TilePickUp | CA/chips-stack-2, CA/chips-stack-5 | 0.17, 0.19 | ? | 0.6 | tile_pickup_1/2.ogg |
| 1 | TileDrop | CA/chip-lay-1, -2, -3 | 0.17, 0.23, 0.24 | ? | 0.8 | tile_drop_1/2/3.ogg |
| 2 | TileSnapBack | IM/impactSoft_medium_000 | 0.12 | ? | 0.6 | tile_snap_back.ogg |
| 3 | TileDraw | CA/chips-handle-4, CA/chips-handle-6 | 0.36, 0.46 | ? | 0.7 | tile_draw_1/2.ogg |
| 4 | TileTakeSide | CA/card-slide-4 | 0.46 (+15% over) | ? | 0.7 | tile_take_side.ogg |
| 5 | TileDiscard | CA/chips-collide-1, -3, -4 | 0.26, 0.26, 0.22 | ? | 1.0 | tile_discard_1/2/3.ogg |
| 6 | TilesDeal | CA/dice-shake-2, CA/dice-shake-1 | 1.38, 1.48 | ? | 0.8 | tiles_deal_1/2.ogg |
| 7 | OpenRuns | MJ/Steel/jingles_STEEL00 | 0.93 | ? | 0.7 | open_runs.ogg |
| 8 | OpenPairs | MJ/Pizzicato/jingles_PIZZI01 | 1.00 | ? | 0.7 | open_pairs.ogg |
| 9 | MeldShown | CA/chips-handle-5 | 0.83 | ? | 0.7 | meld_shown.ogg |
| 10 | TileProcess | IM/impactWood_light_000, _001 | 0.27, 0.27 | ? | 0.6 | tile_process_1/2.ogg |
| 11 | TilesCollect | CA/chips-handle-2 | 0.55 | ? | 0.7 | tiles_collect.ogg |
| 12 | PutBack | CA/chips-handle-3 | 0.23 | ? | 0.6 | put_back.ogg |
| 13 | Error | IF/error_001 | 0.16 | ? | 0.6 | error.ogg |
| 14 | PenaltyTaken | MJ/Sax/jingles_SAX03 | 1.12 | ? | 0.7 | penalty_taken.ogg |
| 15 | PenaltyGiven | IF/bong_001 | 0.12 (just under range) | ? | 0.5 | penalty_given.ogg |
| 16 | MyTurn | IM/impactBell_heavy_004 | 0.30 | ? | 0.4 | my_turn.ogg |
| 17 | TimeLow | IF/tick_004 | 0.05 (50% under) | ? | 0.6 | time_low.ogg |
| 18 | Win | MJ/8-Bit/jingles_NES00 | 1.76 | ? | 0.8 | win.ogg |
| 19 | Lose | MJ/Sax/jingles_SAX07 | 1.74 | ? | 0.7 | lose.ogg |
| 20 | SeatTaken | IF/drop_002 | 0.19 | ? | 0.5 | seat_taken.ogg |
| 21 | UiClick | UA/click2 | 0.06 | ? | 0.5 | ui_click.ogg |
| 22 | UiClose | IF/close_001 | 0.15 | ? | 0.5 | ui_close.ogg |
| 23 | UiToggle | IF/toggle_004 | 0.07 | ? | 0.4 | ui_toggle.ogg |
| 24 | UiPlayNow | IF/confirmation_003 | 0.32 (7% over) | ? | 0.6 | ui_play_now.ogg |
| 25 | UiCreateTable | IF/open_001 | 0.15 | ? | 0.5 | ui_create_table.ogg |
| 26 | UiPlayBots | IF/maximize_003 | 0.21 | ? | 0.5 | ui_play_bots.ogg |
| 27 | UiEnterRoom | IF/maximize_004 | 0.42 | ? | 0.5 | ui_enter_room.ogg |

No NONE rows. Volumes are blind guesses (discard loudest, UI lowest); re-balance after measuring.

## 4. Why / runner-up
0 chips-stack: shortest light plastic ticks; runner-up IM/impactGeneric_light_001.
1 chip-lay is the only numbered 3-family of firm 0.2 s placements; runner-up chips-stack-1/3/4.
2 impactSoft_medium is a dull tap by name; runner-up impactSoft_medium_002.
3 chips-handle-4/6 are mid-length handling slides; runner-up card-slide-4/5.
4 card-slide differs in material from TileDraw; runner-up chips-handle-1.
5 chips-collide = hard sharp plastic knocks; runner-up IM/impactWood_heavy_000-002 (0.31, wood on table, probably duller).
6 dice-shake is a hard-pieces rattle in range; runner-up CA/card-fan-2 (1.31) or chips-handle-5.
7 steel-drum jingle guessed bright; runner-up IF/confirmation_002.
8 pizzicato guessed lighter/different from steel; runner-up MJ/Hit/jingles_HIT11.
9 chips-handle-5 is the longest handling, several pieces; runner-up chips-handle-1.
10 impactWood_light has body but wood-like; runner-up chips-stack-3 (0.37).
11 chips-handle-2 gather feel; runner-up card-shove-3.
12 chips-handle-3 short soft place; runner-up impactSoft_medium_001.
13 error_001 short; runner-up error_004 (0.10) / error_008.
14 SAX03 guessed downward sax; runner-up IM/impactPunch_heavy_002 (thud, not descending).
15 bong_001 small notification; runner-up IM/impactBell_heavy_004.
16 bell_heavy_004 short ring; runner-up IF/confirmation_001.
17 tick_004 only tick fitting; runner-up IF/bong_001 is taken, use IF/select_007.
18 NES00 longest 8-bit jingle, likely fanfare; runner-up MJ/Steel/jingles_STEEL07.
19 SAX07 longest sax, guessed sad; runner-up MJ/Sax/jingles_SAX12.
20 drop_002 soft pop; runner-up IF/pluck_001.
21 click2 plain; runner-up UA/click4. 22 close_001 "back" feel; runner-up IF/back_003.
23 toggle_004 shortest toggle; runner-up IF/tick_002. 24 confirmation_003 satisfying; runner-up confirmation_004.
25 open_001 opening; runner-up IF/open_002. 26 maximize_003 distinct; runner-up IF/select_003.
27 maximize_004 transition; runner-up IF/switch_003 (0.50).

## 5. Least confident
1. 14 PenaltyTaken (descending unverifiable by name; jingle guess).
2. 7/8/18/19 jingle moods chosen blind from instrument style only.
3. 6 TilesDeal (dice shake vs tile rattle) and 16 MyTurn (heavy bell may be too big).
