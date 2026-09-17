# Cornerout second front

- Status: Draft
- Created: 2026-09-16
- Reflects: planning session on `docs/plan-later-proposals`, reading `SidingWallBlock.ResolveFinishFace` and `shapes/block/wall/cornerout.json` as of 71c1d9c

## Summary
A `cornerout` gets a second, independent front finish for its second leg.
The back stays shared.

## Context
Decision 0007 made both legs of a `cornerout` share one `Front` and one `Back`, so finishing one leg finishes both.
The backlog asked whether independent per-leg finishes are wanted at all.

For an outside corner of a house, sharing looks harmless: both fronts face outdoors, both backs face the room.
But a `cornerout` is also how a partition wall joins an outer wall (decision 0002: any gap at the join is a 0.75 slot a player walks through).
Work through that T-junction:

- Outer wall hugs the west face of column `x=0`. Partition runs east along the plane `z=5`.
- The join cell `(0,5)` holds a `cornerout` with `side: west`, covering its west face and its north face (the `z=5` plane).
- West leg front faces west: **outdoors**.
- North leg front faces north, into cell `(0,4)`: **room A**, north of the partition.
- Both backs face into the cell's own interior: **room B**, south of the partition.

So bricking the outside of the house also bricks a strip of room A's wall.
That's a defect, not a preference.

The backs can't have the same problem.
Both back faces look into the same quarter of the same cell, so they are always in the same space.
Splitting them buys nothing.

It also covers the ordinary case the backlog was worried about: a brick front façade meeting weatherboard side walls happens exactly at an outside corner.

## Design

**`SidingWallEntity` gains `SecondFront` (tree key `secondfront`), same nullable-string rules as the other four.**
It only means anything on a `cornerout`; a `wall` never sets it.

**`ResolveFinishFace` returns `"secondfront"` for the second leg's hugged face** instead of `"front"`.
The second leg's inset face still returns `"back"`.
`OnBlockInteractStart` branches on the three values; the already-finished check reads the matching field.

**Shape: the second leg's front elements get their own group names.**
In `cornerout.json` the second leg's `front` slab becomes `secondfront`, and its weatherboard elements become `secondfront-weatherboard`, textured `#secondfront`.
`SelectiveElements` for the second leg appends `second` to whatever the finish's `Elements.front` names, so `front-weatherboard` → `secondfront-weatherboard` with no new field on finish entries.
`SidingWallTexSource` maps `secondfront` to the `Finishes` dictionary like `front`.
`wall.json`'s block `ignoreElements` lists gain the new names.

**Drops and cache key include `SecondFront`.**
`ComputeDrops` adds it; `CacheKey` adds it.

**Existing corners.**
A saved `cornerout` with `Front` set and no `SecondFront` shows its second leg unfinished after this ships, and that leg can be finished again.
Pre-release (0.1.0), accepted rather than migrated: the original finish was only charged once, so nothing is duplicated, and there's no way to tell a legacy wall from one deliberately left half-finished.

## Alternatives considered
- **Keep sharing.** Leaves the T-junction bleed described above.
- **Split both front and back per leg.** Backs always face one space; the extra field and element groups are dead weight.
- **Build T-junctions from two plain walls instead.** Leaves the 0.75 gap decision 0002 already ruled out.
- **Name fields by compass direction (`WestFront`, `NorthFront`).** Rotation makes that wrong for three of the four `side` variants; "second" follows `CorneroutSecondFace`, which rotates with the block.

## Consequences & open questions
- **Partitions line up, because no wall sits mid-cell.** Every slab lies against a cell face, so every wall lies on one of the same grid planes. A partition on the plane `z=5` hugs either the north faces of row `z=5` (slab at `z 5..5.25`) or the south faces of row `z=4` (slab at `z 4.75..5`). The first joins with a `cornerout` in `(0,5)`, `side: west`; the second with one in `(0,4)`, `side: south`. Both put the corner's leg flush with the partition. The player's only job is putting the corner in the cell on the partition's slab side.
- **Auto-connecting corners, fence-style**, would do that choice for the player: placing a wall whose end meets a perpendicular slab swaps that neighbour to the matching `cornerout`. Decision 0005 deferred it; it needs the neighbour-dirtying that `framing-only-collision` introduces, plus a rule for what the new leg costs. Worth its own proposal once corners have been placed by hand in a real build.
- Is the join cell's north leg front even reachable to click from room A? It sits at `z=5` facing north, so yes, from cell `(0,4)`; confirm in game.
- A `wall` layout could in principle have the same room-bleed if players use it as a partition end. It doesn't: a plain wall has one front plane and one room on each side.
- Tests: extend the `ResolveFinishFace` and `SelectiveElements` cases; whole-array asserts as the existing tests do.
