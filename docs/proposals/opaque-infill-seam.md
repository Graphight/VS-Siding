# Opaque infill seam

- Status: Draft
- Created: 2026-09-25
- Reflects: decisions 0008, 0019, 0021, 0025, 0028; `VSSiding.Tests/WallShapeGen.cs`'s `UvRule`/`EmitFace`; `SidingWallEntity.SelectiveElements`; `SidingWallBlock.NeighbourJoins`/`JoinsAbove`; `docs/screenshots/Framing.jpg`

## Summary
A two-high wall of straw, wattle, clay or rubble infill shows a line at the join, where decision 0008 drops the middle plate.
The cause is `EmitFace`'s `Flat` UV rule (`WallShapeGen.cs` line 530-534): every infill box maps its v-axis to `(0, box height)`, starting fresh at its own base rather than at its position in the wall.
The join's extension slivers, `infill-top` and `infill-bottom` (`WallShapeGen.cs` lines 138-140, 249-251, 257-259), are 1-voxel-tall boxes that each restart the texture at v 0 immediately beside the main `infill` box's own v-0 restart, so the join shows two independent origins meeting instead of one continuous surface.
The fix is the same one decision 0028 already shipped for weatherboard: switch the infill groups to `UvRule.Positional`.

## Context
`SidingWallEntity.SelectiveElements` (`SidingWallEntity.cs` lines 192-205) draws a filled cell as `infill` plus, when the cell joins its stack neighbour, `infill-top` and/or `infill-bottom`:
```
names.Add("infill");
if (joins.above) names.Add("infill-top");
if (joins.below) names.Add("infill-bottom");
```
`joins.above`/`joins.below` come from `SidingWallBlock.NeighbourJoins` (`SidingWallBlock.cs` lines 112-134), which for opaque infill is `JoinsAbove(continuesAbove, cellsBelow) => continuesAbove && cellsBelow % 2 == 0` (line 147) — decision 0008's alternating cross-beam.
Where a join drops the plate, `infill-top`/`infill-bottom` fill the 1-voxel gap the plate used to occupy, so the panel still reads as unbroken from y 1 to y 31 across two blocks.

The three infill element groups in `WallShapeGen.cs`:
```
new("infill-top", (1.5, 15, 1), (2.5, 16, 15), "infill", UvRule.Flat),
new("infill", (1.5, 1, 1), (2.5, 15, 15), "infill", UvRule.Flat),
new("infill-bottom", (1.5, 0, 1), (2.5, 1, 15), "infill", UvRule.Flat),
```
(lines 138-140, mirrored for the two legs of `cornerout.json` at 249-251 and 257-259) are all `UvRule.Flat`.
`EmitFace`'s `Span` function (`WallShapeGen.cs` lines 530-534) is what `Flat` resolves to:
```
(double, double) Span(char axis) =>
    element.RunAxis == axis ? (Lo(axis), Hi(axis)) : (0.0, Hi(axis) - Lo(axis));
```
None of the infill elements set `RunAxis`, so for every one of them `v0 = 0` and `v1 = Hi(y) - Lo(y)`: the box's own height, measured from its own base, independent of where that box sits in the block or the stack.
`infill` is 14 voxels tall (y 1-15) and maps to v 0-14; `infill-top` and `infill-bottom` are each 1 voxel tall (y 15-16, y 0-1) and map to v 0-1 apiece.

Put together at a join: the lower cell's `infill` box ends its texture at v 14, and immediately above it (in world space) sits that same cell's `infill-top` box, sampling the texture fresh from v 0.
One voxel further up, the upper cell's own `infill` box begins, also sampling fresh from v 0.
So from the lower `infill` up, a joined stack samples v 0-14, then 0-1, 0-1 (the upper cell's `infill-bottom`) and 0-14 again: three jumps back to v 0 within three voxels of the boundary.
That is the seam in `Framing.jpg`: visible on straw, wattle, clay and rubble, because each infill texture is a distinct, non-uniform pattern (weave, plaster, stone) that shows a restart; invisible on glazing, whose merge takes a different path (below).

This is exactly the bug decision 0028 already fixed once, for weatherboard: "every board restarted its grain at v 0" (0028, quoting decision 0007's original choice), fixed by switching `UvRule.Course`/`Flat`-style per-board restart to `UvRule.Positional`, whose `v = 16 - y` is measured against the block's own 0..16 rather than the element's own box, "so a stacked wall has no seam at the boundary — the same property the shake groups (decision 0022) have always relied on" (0028).
Positional elements never restart mid-block because `v` tracks world-relative `y` directly; two boxes carved out of the same 0..16 span (like `infill` at y 1-15 and `infill-top` at y 15-16) sample adjoining slices of the same texture instead of each restarting at 0.

Glazing does not have this problem because it takes a different geometry, not a different UV rule.
Decision 0019's `infill-pane` (`WallShapeGen.cs` line 141) is `UvRule.Flat` too, but it is a single box spanning the full cell, `(2, 0, 0)` to `(2, 16, 16)` — there is no extension sliver, because `SelectiveElements`'s glazed branch (`SidingWallEntity.cs` line 198) draws only `infill-pane`, never `infill-top`/`infill-bottom`.
One box per cell, abutting the next cell's own full-height box edge-on, reads as continuous even though each restarts its v at 0, because there is no partial-height sliver at the join to expose the restart, and glass carries no grain pattern to show a scale mismatch either way.
Masonry finishes (`RunningBond`, `RubbleGrid`, decision 0025) are already `UvRule.Positional` throughout (`WallShapeGen.cs` line 63, 79, 124) for the same reason 0028 gives — they are faces, not infill, but the infill panel behind straw/wattle/clay/rubble reuses the one generic `infill`/`infill-top`/`infill-bottom` group regardless of material (`SidingWallTexSource.ResolveTexture`, `SidingWallTexSource.cs` lines 50-73, picks the texture only; the geometry and its UV rule are the same three boxes for every infill key).

## Design
**Switch `infill`, `infill-top` and `infill-bottom` to `UvRule.Positional`, in both `wall.json`'s and `cornerout.json`'s tables** (`WallShapeGen.cs` lines 138-140, 249-251, 257-259).
No `From`/`To` boxes change — only the fourth constructor argument, the same one-line-per-element change 0028 made for weatherboard.
`v = 16 - y` then reads directly off each box's own y coordinates: `infill` (y 1-15) maps to v 1-15, `infill-top` (y 15-16) to v 0-1, `infill-bottom` (y 0-1) to v 15-16.
The three boxes then sample three adjoining slices of one continuous 0..16 texture instead of three independent 0-based restarts, and — per 0028's reasoning — the next block up samples the identical 0..16 range again, so a tiling infill texture (the same assumption weatherboard and the shake courses already depend on) carries across the join with no visible restart.

**`infill-pane` is untouched.** Glazing already has no seam and no extension slivers; nothing about this proposal's cause applies to it.

**`RunAxis` is not needed here.** `RunAxis` (0021) exists for a box that is a slice cut out of a longer run along a horizontal axis (shake courses, running bond); infill's three boxes are stacked along y, and `Positional` already measures y directly, so no `RunAxis` value applies to this axis.

**The golden test moves with it**, per 0021's rule that new UV arithmetic needs its own literal-value assertion: the infill-group tests in `VSSiding.Tests` currently pinning `Flat`'s v 0-14/0-1/0-1 are replaced with `Positional`'s v 1-15/0-1/15-16, and `just shapes` regenerates both committed JSON files.

## Alternatives considered
- **Leave `infill` `Flat` but make the extension slivers sample from where the main box left off (a per-element v offset).** Works arithmetically but is bespoke machinery only these two elements would use, where 0028 already established the general answer — measure against the block's own y — for exactly this class of problem.
- **Drop `infill-top`/`infill-bottom` and extend the main `infill` box's own `To`/`From` at mesh time instead of adding sliver elements.** Would need per-join geometry generated at runtime, which decision 0021 rejected wholesale in favour of a static, generated shape table; the elements already exist for this and only their UV rule is wrong.
- **Give straw/wattle/clay/rubble seamless (pre-tiled) textures and leave `Flat`.** A texture tiles at 16, and `Flat` jumps from v 14 back to v 0, so even a perfectly tiling texture still breaks at the join.
- **Merge the infill panel across a join into one box, like glazing's `infill-pane`.** Rejected: 0008's alternating cross-beam means an opaque join isn't always present (only every second cell), so the infill panel's height varies with the cross-beam pattern in a way glazing's unconditional full merge does not; matching glazing's geometry would mean re-deriving 0008's cross-beam logic into the shape generator rather than `SelectiveElements`.

## Consequences & open questions
- Every infill material's texture now needs to tile vertically across a 16-voxel span for the seam fix to hold, the same requirement 0028 already imposed on plank/weatherboard art and the shake courses (0022) already relied on; `game:block/wood/wattle`, `game:block/hay/normal-side`, `game:block/clay/blueclay` and rubble's texture are vanilla assets not authored for this mod, so whether they tile cleanly needs a playtest look, not just the golden test.
- This only changes the infill panel's own texture; `IsBuried` (`WallShapeGen.cs` lines 490-508) and face culling are untouched, since neither depends on `UvRule`.
- No shape or collision geometry changes — `ComputeCollisionBoxes`, `FramingBoxes` and 0008's cross-beam counting are all untouched; this is a texture-mapping fix only.

## Stages
1. **UV rule change:** flip `infill`, `infill-top`, `infill-bottom` to `UvRule.Positional` in both element tables; update the golden-test literal UVs; run `just shapes` to regenerate `wall.json` and `cornerout.json`.
2. **Playtest:** a two-high (and taller) stack of each opaque infill against decision 0008's cross-beam pattern, a `cornerout`'s two legs, and a mixed stack (infill changing partway up) for any new mismatch at a material boundary.
3. **Graduate** as a decision extending 0008, alongside 0028.
