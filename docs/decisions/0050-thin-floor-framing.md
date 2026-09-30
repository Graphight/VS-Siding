# 0050 — Thin floor framing

- Status: Accepted
- Created: 2026-09-25
- Reflects: branch `thin-floor-framing`; `SidingFloorBlock`, `SidingFloorEntity`, `config/materials.json`, `MaterialFamilies.MergeShared`, `WallShapeGen`'s floor table, `PlaceWallFrame`, `SidingModSystem.IsHostable`; decisions 0001, 0002, 0020, 0042; `SideSolid`/`SideOpaque` consumers read from decompiled 1.22.2 `VintagestoryAPI`, `VSEssentials` and `VSSurvivalMod`; a session discussion, not a player request; played over three rounds

## Summary
A floor built the way the walls are: a 4/16 layered panel flush with the top of its cell, placed as a new `floor` option on the picker's framing row.
Joists are the framing, pugging the infill, floorboards the top finish and lath and plaster the ceiling.
This is part 1: a floor that can be framed, filled, finished plainly, seal a room and be broken.
Finish styles and glass floors are `thin-floor-finishes`, the underside's lighting `thin-floor-lighting`, decks with the floor's layers `deck-as-floor`, and hanging from the underside `hanging-under-thin-floors`.

## Context
Vanilla floors are a full block of planks or a slab, which eats headroom and looks nothing like the walls.
The deck of decision 0042 closes the slot at the wall, and its flush-top 4/16 height was chosen to meet this floor.
Nobody has asked for thin floors yet; the deck made them the obvious next piece.

## Design
**Flush top, open underneath.**
The panel's top face is the whole 16×16 at the height a plank block's would be, so the block sets `sidesolid` on `UP` and leaves the other faces false.
`sideopaque` stays false on every face, as on the walls: no neighbour face is culled against a panel that can still be bare joists.

**Its own block, not a wall layout.**
`floor.json` with `SidingFloorBlock` and `SidingFloorEntity`, which do not derive from the wall's classes.
About thirty patch sites test `is SidingWallBlock`, `WallAt` or `GetBlockEntity<SidingWallEntity>` (guest hosting, `OpenSide`, step-up, skylight, `KillFire`, the bed and trunk footprints), and every one of them assumes a panel standing on a horizontal face.
A separate class keeps a floor out of all of them; the floor reuses the wall's static helpers instead.
The floor has no orientation: every floor's joists run north-south, three to a cell, so they tile across any run and never set a direction the finishes would have to follow.
The first playtest had `side` from `HorizontalOrientable` choosing the joist direction, which tied the floorboards' grain to it; board direction is now a finish style in `thin-floor-finishes`.
Two joists at the cell edges also read as a floor a player should fall through; three, never more than 4.5/16 apart, do not.

**One materials file for both.**
`Framings`, `Infills`, `Finishes` and their `*Families` move from `wall.json` to `config/materials.json`.
`SidingModSystem.AssetsFinalize` merges it into each siding block before expanding families; an entry in the block's own file wins, so `wall.json` and `floor.json` carry only what the other does not share.
A compatibility patch that adds a material then targets `config/materials.json` and reaches walls and floors together.
`LayerSounds` and `LayerResistance` stay in each block file, because `OnLoaded` reads them and a merge would then depend on load order.

**The same layers as a wall.**
Framing, infill and two finishes key into the shared dictionaries (0001), with the panel laid flat instead of standing.
The entity names them `Framing`, `Infill`, `Front` (the top) and `Back` (the underside), so `SelectiveElements`, `PeelLayer`, `ComputeDrops` and the retention helpers take them unchanged.
Glass infill gives an error on a floor for now; a glass floor is `thin-floor-finishes`' problem.

**What `sidesolid` on `UP` turns on.**
Decision 0020's seven `Block` members are unchanged in 1.22.2, and each takes its own value for a floor:

| Member | Top face | Other faces |
| --- | --- | --- |
| `GetRetention` | overridden: seals once framed and filled, as `ComputeRetention` | 0; the room below walks into the open part, then meets the top |
| `CanAttachBlockAt` | overridden: only once sealed, so nothing stands on bare joists | false; hanging is `hanging-under-thin-floors` |
| `GetLiquidBarrierHeightOnSide` | left to vanilla, 1 from the flag | 0 from the flag, so water may run in under the panel |
| `AllowSnowCoverage` | left true: an exposed floor takes snow like a plank floor | n/a |
| `CanCreatureSpawnOn` | left true, like a plank floor | n/a |
| `DisplacesLiquids` | false: the default needs all four sides and the base solid | |
| `SideIsSolid` | left true: water renders no edge against the top | false |

Outside `Block`, vanilla reads `SideSolid[UP]` or `IsSideSolid(…, UP)` directly, and for each of these a floor should count as ground:

| Reader | What it controls |
| --- | --- |
| `AiTaskWander`, `AiTaskIdle`, `AiTaskSeekEntity` and their `R` forms | creatures stand and path on the top |
| `BlockBehaviorFiniteSpreadingLiquid` (`SideSolid.Any`) | water poured on the floor spreads over it instead of falling through |
| `EntityRideableSeat` | a rider dismounts onto the floor |
| `BlockBehaviorBreakIfFloating`, `BlockSticksLayer`, `BEBehaviorMicroblockSnowCover` | things set on the floor count as supported |
| `AABBIntersectionTest.RayIntersectsBlockSelectionBox` (`SideSolid.Any`) | selection calls the floor's own `GetSelectionBoxes` |

The rest are world generation or blocks that never meet a floor.
A bare frame counts as a floor for these too: the flag is static, so water rests on joists.
That is the price of a static flag, and it matches the collision below, which already lets a player stand on a bare frame.

**Collision.**
One static box, the whole panel (y 12..16), whatever the layers.
Walls collide as their frame alone (0008) so a bare wall can be walked through while building; a bare floor that dropped the player a storey would be worse than one that holds them.

**Building one.**
The framing row gains `floor` beside `wall` and `corner`.
With it picked, planks frame a floor where a click would frame a wall: in the cell beside the clicked face.
Planks aimed at a wall frame the floor beside it instead of finishing the wall, adding a deck or upgrading a corner, so a floor runs off a decked wall with no helper block; the other rows' plank uses come back with `wall` or `corner` picked.
Infill goes on from any face of a framed floor, a finish on the top or the underside, and a break peels one layer from the hit face as a wall's does (0013).

**Extending a run from its top.**
Planks on a floor's top with `floor` picked would otherwise stack a floor 12/16 above it, which nobody wants.
Instead the click walks along the floor the way the player faces and frames the first cell past the end, if it can be built into (water can) and lies within four cells of the clicked floor; the cap keeps a stray click from framing a floor across a lake.
It works the way a rope ladder extends downward, and it is how a floor goes out over water: before it, the only face to click was the floor's edge, reached by crouching out past it and looking back.
A filled floor with no floorboards takes a plain click as floorboards and a sneak-click as an extension.

**Never a guest.**
`IsHostable` lets a block with only a solid top and a block entity into a wall's cell (a cabinet), and a floor is exactly that.
The wall's `IsReplacableBy` then returned true for a floor, so a plank click on a decked wall silently failed, and one on a bare wall would have tried to host the floor in the wall's cell.
Floors are excluded alongside walls.

**Meeting the wall.**
A wall with a deck at the same course; the deck is the floor's rim joist.

## Alternatives considered
- **A `floor` layout on the wall block.** Everything comes free, including every wall-only patch, each of which would then need a floor branch or a guard.
- **The floor reads `wall.json`'s dictionaries.** No file moves, but the floor depends on a wall block existing, and a floor could never add a material a wall lacks.
- **Vanilla slabs.** Already flush-top, but 8/16 thick and single-material; no joists, no ceiling finish.
- **Floor at the bottom of its cell.** Ceiling flush instead of floor, so rugs and furniture float 12/16 above the floor line. Walking surfaces matter more.

- **Joists following the player's facing.** The first build; the grain of the floorboards followed the joists, and so the way the player stood.
- **Two joists at the cell edges.** Also the first build; the gaps read as a floor to fall through.

## Consequences & open questions
- The lower storey loses 4/16 of headroom in the cell holding the floor.
- The underside renders badly lit one block off the ground; `thin-floor-lighting` measures it before choosing a fix.
- Moving the materials changes where a compatibility patch points. No other mod is known to patch `wall.json`.
- Water in a flooded cell draws a band over the outside of the walls there, and over a vanilla door beside them, so it is likely the walls' `DisplacesLiquids` staying false (0020) rather than the floor. Not yet tested with a lone wall and no floor.
- Played: framing off a decked wall and off other floors, the three slats, infill and both finishes, peeling from the top and from below, rooms sealing above and below, the tooltip, water held on top, extending a run over water with plain and sneak clicks and the four-cell cap, and existing walls keeping their materials after the move to `config/materials.json`.
