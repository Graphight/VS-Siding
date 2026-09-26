# 0041 — Opaque infill seam

- Status: Accepted
- Created: 2026-09-25
- Reflects: branch `opaque-infill-seam`; `VSSiding.Tests/WallShapeGen.cs`'s infill groups and `EmitFace`; `WallShapeGenTests.EveryInfillBoxSamplesItsOwnSliceOfTheTexture`; `SidingWallEntity.SelectiveElements`; `SidingWallBlock.NeighbourJoins`/`JoinsAbove`; extends 0008, alongside 0028

## Summary
A two-high wall of straw, wattle, clay or rubble infill showed a line at the join, where decision 0008 drops the middle plate.
The infill boxes sampled the texture from their own base rather than from where they sit in the block, so the join showed several restarts of the texture within a few voxels.
The fix is the one decision 0028 made for weatherboard: the infill groups are `UvRule.Positional`.

## Context
`SidingWallEntity.SelectiveElements` draws a filled cell as `infill` plus, when the cell joins its stack neighbour, `infill-top` and/or `infill-bottom`.
The joins come from `SidingWallBlock.NeighbourJoins`, which for opaque infill is `JoinsAbove(continuesAbove, cellsBelow) => continuesAbove && cellsBelow % 2 == 0`: 0008's alternating cross-beam.
Where a join drops the plate, `infill-top`/`infill-bottom` fill the 1-voxel gap the plate used to occupy.

All three groups were `UvRule.Flat`, and `EmitFace`'s `Span` maps a `Flat` box's v to `(0, box height)`.
So `infill` (y 1-15) sampled v 0-14, and each 1-voxel sliver sampled v 0-1.
From the lower cell's `infill` up, a joined stack sampled v 0-14, then 0-1, 0-1 and 0-14 again: three jumps back to v 0 within three voxels of the boundary.
Straw, wattle, clay and rubble are non-uniform patterns, so each restart showed.

Glazing never had the seam.
Its `infill-pane` is one full-height box per cell with no slivers, and glass has no pattern to show a restart.

## Design
**`infill`, `infill-top` and `infill-bottom` are `UvRule.Positional` in both `wall.json`'s and `cornerout.json`'s tables.**
No box moved; only the UV rule changed.
`Positional` maps v to `16 - y`, so `infill-bottom` (y 0-1) samples v 15-16, `infill` (y 1-15) v 1-15 and `infill-top` (y 15-16) v 0-1.
The three boxes are adjoining slices of one 0..16 texture, and the next block up samples the same 0..16 again, so a texture that tiles vertically carries across the join.
`EmitFace` applies `Positional` only to faces whose v axis is y, so the boxes' up/down faces stay `Flat`.

**`infill-pane` stays `Flat`.** It has no slivers and no seam.

**No `RunAxis`.** `RunAxis` (0021) is for boxes cut out of a longer horizontal run; the infill boxes stack along y, which `Positional` already measures.

**A literal-value test pins the UVs.**
The golden-file test passes by construction after `just shapes`, so `EveryInfillBoxSamplesItsOwnSliceOfTheTexture` asserts v 0-1, 1-15 and 15-16 on every side face of the three groups, for both layouts, per 0021's rule.

## Alternatives considered
- **A per-element v offset so the slivers continue where `infill` left off.** Works, but is bespoke machinery for two elements where 0028 already set the general answer: measure against the block's own y.
- **Drop the slivers and stretch `infill` at mesh time.** Needs per-join geometry generated at runtime, which 0021 rejected in favour of a static, generated shape table.
- **Seamless textures, keep `Flat`.** A texture tiles at 16, and `Flat` jumped from v 14 back to v 0, so even a perfectly tiling texture broke at the join.
- **Merge the panel across a join into one box, like `infill-pane`.** 0008's cross-beam means an opaque join only happens at every second cell, so the merge would pull the cross-beam logic into the shape generator instead of `SelectiveElements`.

## Consequences & open questions
- Every infill texture now has to tile vertically over 16 voxels for the join to hold, the same requirement 0028 put on weatherboard and the shake courses (0022) already had.
Playtested on 2026-09-25: the seam is gone on stacked walls of the vanilla straw, wattle, clay and rubble textures, on a `cornerout`'s two legs, and on a stack whose infill changes partway up.
- A cell without a join (plate present) now shows texture rows 1-15 rather than 0-14, a one-row shift nobody noticed in the playtest.
- Texture mapping only: `IsBuried`, face culling, collision, `FramingBoxes` and 0008's cross-beam counting are untouched.
