# 0085 — The decked wall's top

- Status: Accepted; the diagonal taking no cap superseded by 0096
- Created: 2026-11-01
- Reflects: branch `inside-corner-post`; `SidingWallEntity.Capped`, `SidingWallBlock.FreeAbove`, `VSSiding.Tests/WallShapeGen` (`cap-*` groups), `shapes/block/wall/wall.json`, `cornerout.json` and `cornerin.json`, `SidingFloorTests`, `WallShapeGenTests`; decisions 0042, 0053; played in creative on 2026-11-02

## Summary
A decked wall whose cell above holds nothing draws the deck's top finish over the wall's own 4/16 top.
Round a stairwell, the wall's top then reads as part of the floor.

## Context
A deck fills the open 12/16 of its cell (decision 0042) and takes the floor's layers (decision 0053), and the wall's own 4/16 stays the wall's.
Under an upper wall nobody sees it.
Round a stairwell the wall stops at the floor, so its top shows as a strip in the wall's materials across a floor of another wood.
This may be the oak beside acacia in a stairwell player's screenshots: "the top is acacia, and the wall for the stairwell is oak, and a full block of either ruins the other".

## Design
**The cap.**
`WallShapeGen` adds `cap-*` groups: the deck's top-finish groups clipped to the wall's footprint, top face only, 0.05 voxel above the block top.
They exist for the wall, `cornerout` and `cornerin`, one set per deck finish style.
The diagonal and a glazed deck take no cap.

**When it draws.**
`Capped` is true when the layout is not a diagonal, the deck has a top finish and `FreeAbove` holds: the cell above is air or replaceable, and holds no wall or guest wall.
The entity then draws the cap.

## Alternatives considered
- **Splitting the front and back slabs' top 1/16 into separate elements and dropping them under a cap.** Not built: the 0.05 voxel lift keeps every existing element as it is; it stays the fallback if the lift z-fights.
- **A floor finish over the top as its own click.** Rejected: the deck's finish already says which wood.
- **A second wall per cell** (`multiple-walls-per-cell`, parked).

## Consequences & open questions
- The playtest raised no z-fighting between the cap and the slab top; the slab split stays the fallback if it shows.
- The cap stands 0.003 block into the cell above, which `FreeAbove` keeps to air or a replaceable block.
- The diagonal and a glazed deck keep their own tops.
- Whether the decked wall's top is what reads wrong in the stairwell is unconfirmed.
- Played: in creative on 2026-11-02; the playtest raised no issue.
