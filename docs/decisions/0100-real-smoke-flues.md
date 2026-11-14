# 0100 — Real Smoke flues and vents

- Status: Accepted
- Created: 2026-11-14
- Reflects: issue #101 and mod page comment https://mods.vintagestory.at/vssiding#cmt-245074; proposal `real-smoke-flues`; branch `real-smoke-flues`; `just probe realsmoke` against Real Smoke 2.0.0-pre.13 on game 1.22.7 (`RealSmokeScenarios`, written to `docs/compat/realsmoke-smoke.md`); Real Smoke's source at https://codeberg.org/xyrvexus/RealSmoke branch `rs_v2` (`SmokeFlowField.ReadCell`, `GasUtils.IsAdjacentSidePorous`, `SmokeUtils.ChimneyOutputPosition`); `SidingWallBlock.SideIsSolid`/`HasVent`/`VentId`; `GuestSealingPatches.SideSolidPostfix`; `blocktypes/wallvent.json` and `blocktypes/floorvent.json`; the trapdoor entry in `config/materials.json`; `patches/compat/realsmoke.json`; `WallShapeGen.Louvres`; `VentScenarios`, `VentBlocktypesTests`; every vanilla `SideIsSolid` caller in decompiled 1.22.7 `VintagestoryAPI`, `VintagestoryLib`, `VSSurvivalMod`, `VSEssentials` and `VSCreativeMod` (which has none); decisions 0001, 0019, 0020, 0035, 0042, 0050, 0068

## Summary
A wooden trapdoor clicked into a frame, a deck or a thin floor as its infill makes a vent, drawn as louvres in the trapdoor's wood.
A vent seals the room and dams water like any filled wall, and under Real Smoke it passes smoke, which every other filled wall, floor and deck holds back.
A sealed wall face is now side-solid, so a fire against it no longer vents its smoke into the wall's cell.

## Context
A player vents a kitchen through trellises so the smoke gets out, and the room loses its heat; they asked for a hearth in a wall, or a flue made from a trapdoor.
They never named the smoke mod.
Real Smoke is the likely one, and the one tested here.

Real Smoke 2.0.0-pre.13 decides where smoke goes in three places, re-read from the proposal's pre.7:

- `SmokeFlowField.ReadCell` opens every face of a block whose tags include `porous` or `semiporous`. For any other block it keeps the block's collision boxes for moving smoke, and seals a face where `GetLiquidBarrierHeightOnSide` reaches 1; a crossing is open only if neither side's face is sealed. The source has no 4x4 hole rule, which the mod page describes.
- `GasUtils.IsAdjacentSidePorous`, which picks the side a fire vents to, calls the virtual `SideIsSolid`.
- `SmokeUtils.ChimneyOutputPosition`, the flue search, reads the static `SideSolid` field.

Cached cell readings refresh on a chunk-dirty event or a block entity's `MarkDirty`, at most 3 s late.

A tag attaches to a block code, and a wall's code carries only its `layout` and `side`; its materials are block entity state (decision 0001).

The proposal predicted four gaps from that source and asked for a playtest.
It became an Atlas probe instead: `just probe realsmoke` lights a firepit in a stone box whose only way out is the block under test, and reads the smoke Real Smoke holds in a second stone box beyond it.

## Design

**What the probe found** (`docs/compat/realsmoke-smoke.md`; `docs/compat/realsmoke.md` is the generic hosting probe's table, and lists no blocks):

| Prediction | Probe | Outcome |
| --- | --- | --- |
| A filled wall, a glazed cell and a wall hosting furniture hold smoke, and a bare frame passes it | as predicted, from either face | no change |
| Bare floor joists hold smoke because the floor's top reads as sealed | held, but a bare deck reading barrier 0 on every face holds it too | dropped |
| A filled deck passes smoke because its top reads barrier 0 | bare and filled decks both hold it | dropped |
| A fire against a filled wall vents into the wall's cell | from the claimed face, smoke reached the far side (188 through clay, 274 through wattle, 156 through glass) | fixed |
| A chimney above a filled floor draws through it | the chimney course over a clay floor held the smoke | no change |

**Floors and decks are left as they were.**
`ReadCell` keeps a cell's collision boxes, and a thin floor and a deck each carry a top collision box across the cell, so both hold smoke whatever their liquid barrier answers.
A barrier change would only have changed where water rests, which was never the goal.
So decision 0050's rows (bare joists hold water, the top is `SideIsSolid`) and decision 0042's (no liquid on a deck's top) stand.

**A sealed wall face is side-solid.**
`SidingWallBlock.SideIsSolid` answers whether `GetLiquidBarrierHeightOnSide` reaches 1 on that face: true on a face the wall claims once its framing and infill retain, false on every other face.
`GuestSealingPatches` answers the same for a hosted cell, as it does for the barrier (decision 0035).
A fire against the claimed face of a filled wall now has its smoke held; a fire against the unclaimed side was already held by the barrier.
This supersedes decision 0020's `SideIsSolid` row, left false there as cosmetic.

Every vanilla caller, read in the decompiled DLLs:

| Caller | What changes |
| --- | --- |
| `LiquidTesselator` edges, `ChunkTesselator` liquid face culling | water against a sealed face draws as it does against stone |
| `BEBehaviorMPWaterWheel.blockedBy` | a sealed face in front of a water wheel blocks its flow |
| `SurfaceLayerTesselator` | a SurfaceLayer decor (wallpaper, plaster, paint, cave art) asks the face it sits on, and on a solid one draws a flat quad on the cell's face instead of projecting onto the model. Only a claimed face can answer true, and the panel lies flush against it, so the quad lands where the panel's outer face already is. The room face never claims, so a decor there is drawn as before. The call passes the cell beyond the decor's face as `pos`, so the override reads that cell's entity; it can only answer true where that cell is a sealed siding wall too. Not looked at in game |
| `BlockRequireSolidGround`, `BlockBehaviorChimney`, `ItemRoller`, `Timeswitch`, `BlockMicroBlock`, `EntityBehaviorControlledPhysics` step-up (`FindSteppableCollisionBox`, `FindSteppableCollisionboxSmooth`) | nothing: they ask the up or down face, which no wall claims |
| `BlockMultiblock` | nothing: it hands the question to its control block, which a siding wall never is |
| `BlockBehaviorUnstableRock` | nothing: it asks up and down, or two opposite faces, and no layout claims two opposite faces |
| `SystemSoundEngine` | nothing: it asks only stone, metal and similar materials |
| `BlockSpurGear.OnNeighbourBlockChange` | nothing: it breaks the gear unless the block it faces is a `BlockMPBase`, and a siding wall never is, whatever its side answers |

The thin floor keeps vanilla's answer from its flags, its top solid (decision 0050).

**The vent is a block code of its own.**
`wallvent.json` and `floorvent.json` are copies of `wall.json` and `floor.json` under new codes, with the same classes and variants, and out of the creative inventory; `VentBlocktypesTests` fails if a copy drifts from its source beyond the code.
A variant group on `wall.json` would rename every `wall-*` code and orphan the walls in saved worlds.

**A wooden trapdoor is the vent's infill.**
`config/materials.json` matches `game:trapdoor-solid-*` and `game:trapdoor-window-*`, vanilla's wooden trapdoors, with `Elements: "louvres"`, `TextureBlock` for the trapdoor's own wood (decision 0068), and `Vent: true`; one trapdoor goes in, and breaking the layer drops it.
Metal `plate` and `bars` trapdoors are not matched.
`WallShapeGen` generates `louvres` groups for `wall`, `cornerout`, `diagonal` and the floor; deck groups are cut from the floor's, so a wall's deck, both of a diagonal's decks and a post's deck carry them too.
A post takes no infill, but its deck takes a trapdoor as any deck does.

**A trapdoor anywhere in a cell puts the whole cell on the vent block.**
A trapdoor in a wall's infill or in either deck puts the cell on `wallvent` (`HasVent`, `VentId`), and one in a thin floor puts it on `floorvent`.
The swap rides the `ExchangeBlock` an infill change already makes for vanilla's rooms, keeps the block entity, and fires the chunk-dirty event that refreshes Real Smoke's reading of the cell.
Peeling the last trapdoor puts the cell back on `wall` or `floor`.
A filled wall's corner upgrade re-derives the vent through `OnInfillChanged`; a bare frame's upgrade exchanges straight to its target, so the target keeps the block's own first code part, and a frame under a trapdoor deck stays vented as a corner or a diagonal.
The porous cell opens every face to smoke, so a trapdoor deck opens the clay wall under it, and a trapdoor wall opens the clay deck over it; the probe reads smoke through both.
A cell hosting furniture holds the furniture block, not `wallvent`, so a vent passes no smoke while it hosts.

**Real Smoke reads the vent as porous; nothing else does.**
`patches/compat/realsmoke.json`, with `dependsOn: realsmoke`, adds the `porous` tag to `wallvent` and `floorvent`, so `ReadCell` opens every face of a vent's cell.
A vent keeps its retention and its liquid barrier, so vanilla's room test and water treat it as a filled wall, and without Real Smoke it is a sealed louvred wall.
The same patch sets `vssidingVentsSmoke` on `wallvent`, and `SideIsSolid` answers false for a block carrying it.
Without that flag the side-solid answer above held the smoke of a fire on a vent's room face, since `IsAdjacentSidePorous` reads `SideIsSolid` and not the tag.
`floorvent` needs no flag: a fire under a floor vents into its cell through the underside, which is not side-solid, and the probe's vent floor passes smoke.
The probe reads smoke through a vent wall from either face, through a vent floor and through a vent wall's deck, and a room roofed with a vent floor stays sealed; the amounts vary from run to run by tens of percent, so only whether smoke passes is asserted.

**No vanilla roof block serves as the flue.**
With one roof cell of a sealed room set to each, a wooden trapdoor keeps the room sealed and holds the smoke, and a window trapdoor, a barred trapdoor and a stick layer each leak the room.
So the handbook names only the Siding vent.

**Fires already host in a wall's cell.**
A firepit and a forge host in a filled wall's cell, and a firepit lit there for the whole run leaves the wall's wood layers intact: the firepit, forge and clay oven carry no `Burning` behaviour (only the `fire` block spreads flame), so they set nothing beside them alight, and no code was needed.
The clay oven is kept out by the solid-side rule of `SidingModSystem.HostRefusal`, contrary to the proposal's read of `clay/oven.json`, and left so.

## Alternatives considered
- **A vent infill on the plain wall block, with no liquid barrier, so `ReadCell`'s barrier rule opens it.** No new block code and no tag, but the vent would pass water too.
- **Real Smoke's chimney behaviour (`BlockEntityBehaviorRealGasPump`) on the vent**, the proposal's route. It moves smoke one way at a set rate, but which way its `front` and `back` point on a wall whose `side` is its only orientation was never read. The tag is one patch line that needs no orientation, and the probe reads smoke through a vent from either face.
- **A thin floor's top damming only once filled, and a deck's top damming while it retains** (two stages of the plan, dropped). The collision boxes already hold the smoke, so either would only change where water rests.
- **A wall's `SideSolid` field following its fill, for the flue search.** A static field per block type, which no override reaches; the probe's chimney course held the smoke anyway.
- **Name a vanilla flue in the handbook in place of a vent.** None keeps the room sealed.
- **Only a wall's own infill makes a wall cell a vent.** A deck vent would then need a block of its own to leave the wall below it closed; one vent block per cell, open on every side, was chosen instead.

## Consequences & open questions
- Water against a sealed wall face no longer draws its edge face, a sealed face blocks a water wheel, and a wallpaper or plaster decor on a sealed face draws flat on the cell's face; none of these was looked at in game.
- A vent that keeps the heat and passes the smoke is not physical. Accepted: heat beyond vanilla's room retention is out of scope for the mod.
- A trapdoor deck lets smoke through the filled wall below it, and a trapdoor wall through the filled deck above it; the handbook and the ModDB page say a vent opens every side of its cell.
- A post's deck taking a trapdoor and becoming a vent follows from the code, and no scenario covers it.
- In the probe, the room under a vent floor counted one skylight cell where the planks roof counted none; whether that matters for a cellar was not looked at.
- The probe's bare-frame, room-face row is not steady: one run's reading differed from the table's.
- Real Smoke 2.0.0 is in pre-release, so `ReadCell`'s rules and the `porous` tag may move before release; `just probe realsmoke` takes the latest release, so a rerun re-checks them.
- Real Smoke's repository has no licence file. Its source was read to learn which members it calls; nothing is copied from it, and the vent uses only a tag patch.
- The player never confirmed the smoke is Real Smoke's.
