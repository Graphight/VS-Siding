# Siding cellar strength

- Status: Draft
- Created: 2026-09-18
- Reflects: playtest on branch `stone-infill` at ece631e; vanilla 1.21 `RoomRegistry` and `BlockEntityContainer.GetPerishRate` (decompiled from `VSEssentials.dll`)

## Summary
A sealed, dark siding cellar gets about half the spoilage bonus of an identical rammed-earth cellar.
Find out which of the room's counts our walls get wrong, then fix that.

## Context
Stone infill's playtest found three problems; this PR fixed two of them.
Rooms went stale after packing infill (fixed with `ExchangeBlock`), and sealed walls let sunlight through (fixed by making a sealed wall absorb light).

The comparison that's left: two buildings with the same outside size, rammed-earth floors and ceilings, and the same door.
The only difference is the walls, siding with stone infill against rammed earth.
Both rooms are green under `/debug rooms hi` (no exits), and both are small (siding 5/3/6 including the dead space, rammed earth 3/3/4).
The siding vessel spoils at about halfway between outside and the rammed-earth cellar.
The status HUD still shows "room", not "cellar", for the siding room; it reads the client's room registry, so it can disagree with the server.

## Design

**First, measure.**
Vanilla's `/debug rooms list` prints only the bounding box, so add a debug command (server side) that prints the room at the player: `CoolingWallCount`, `NonCoolingWallCount`, `SkylightCount`, `NonSkylightCount`, `ExitCount`, `IsSmallRoom`.
Run it in both buildings.

The cellar strength in a small room is `1 − 0.4 × skylight share − 0.5 × min(warm / cold, 1)`, and it pulls the temperature toward 5°C by that fraction.
"Halfway" means one of those two penalties is near its maximum, so the counts will say which.

**Suspects, in the order the counts would point at them:**
- **Skylight.** Skylight is sampled per column, at the first cell the flood fill visits in that column. With the dead space inside the room, the perimeter columns are wall cells. If the light stored in a wall's own cell stays at full sun (the cell was open before it was infilled, or light spreads in beside it), every perimeter column counts as sky: 18 of 30 columns here.
- **Warm faces.** Something returns positive retention: a `cornerout` claiming the wrong pair of faces, or a face asked from inside a wall's cell that we don't claim.
- **Cold faces not counted.** The flood reaches a wall cell but asks a face that returns 0, so the cold face never counts.

Fix whichever the counts show, with a unit test on the pure function involved.

## Alternatives considered
- **Sealing both faces of a wall, so the flood never enters a wall's cell.** Rejected in the playtest: it pushes the dead space out of the room, which is the space `furniture-against-thin-walls` wants inside it.
- **Guessing a fix without the counts.** Two plausible readings of the code (room size, then warm ceilings outweighing the walls) were each disproved in play.

## Consequences & open questions
- If the cause is skylight sampled in wall cells, the fix may belong in how a wall's cell holds light rather than in retention.
- Whether the HUD's "room" is a separate client-side staleness or the same cause; recheck it once the server counts are right.
