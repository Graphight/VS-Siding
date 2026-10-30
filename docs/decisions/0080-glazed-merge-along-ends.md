# 0080 — Glazed cells merge along their ends

- Status: Accepted
- Created: 2026-10-30
- Reflects: branch `glazed-corner-merge`, which graduates the proposal of the same name (2026-10-11, from the playtest of PR #116); `SidingWallBlock.End`/`Ends`/`LeftRightEnds`/`EndsMeet`/`JoinsAtEnd`/`EndContinuesGlazing`/`GlassRunsOn`/`NeighbourJoins`/`InnerCorners`/`MarkNeighboursDirty`/`MarkDiagonalRunDirty`/`OnNeighbourBlockChange`/`OnBlockInteractStart`, `SidingWallEntity.FrameParts`/`SelectiveElements`/`CacheKey`, `VSSiding.Tests/WallShapeGen` (`CornerOutElements`, `CornerRimPieces`, `DiagonalFrame`, `DiagonalJoined`, `DiagonalRims`), `shapes/block/wall/cornerout.json` and `diagonal.json`, `lang/en.json`'s `build-glazed`; `WallShapeGenTests`' cross-cell overlap and flush tests, `SidingWallBlockCollisionTests`' end table and world-backed inside-corner test, `VSSiding.E2E.Tests/DiagonalScenarios.cs`; decisions 0019, 0066, 0071; the interview of 2026-10-30 and the review of PR #144; unit tests (866) and e2e scenarios (92) pass

## Summary
A glazed wall, corner and diagonal drop the upright between them wherever their glass carries on through the shared boundary, so a run of glass is one sheet round a corner or along a diagonal run.
A glazed diagonal gets a glazing frame and takes a finish on each face as a rim, as a wall does.
Each cell's ends are named by the face they lie on and the way they point, so one rule answers for every pair of layouts.

## Context
Decision 0019 merges glazing in all four directions on a flat wall and keeps a `cornerout` merged only up and down, since "a corner has nothing to merge along".
Decision 0066 gives a diagonal plain plates and no bezel for glass.
Decision 0071 gives a glazed wall and corner a rim round the glass, and a glazed diagonal none: `OnBlockInteractStart` sent the `build-glazed` error in place of placing a finish on one.

The playtest of PR #116 showed a glazed corner between glazed walls with two members side by side where a flat run draws one sheet.
A corner has a far end on each leg, where a flat wall carries the same face on, and a diagonal has an end at each corner of its cell, so all three layouts have something to merge along.

## Design
**Ends.**
`End` is the face an end lies on and the direction the run carries on from it.
`Ends(layout, side)` lists each layout's ends left then right: a wall's two, a cornerout's leg 2 and leg 1 ends then its corner post's two, a diagonal's two mitre ends.
A cornerout and a diagonal end left on the second face (`CorneroutSecondFace`) and right on `side`.
`LeftRightEnds` is the first two, the ends a cell's left and right joins stand for, which on a corner are leg 2 and leg 1.
`EndsMeet` is two ends on the same face pointing at each other, and `JoinsAtEnd` asks whether any end of the neighbouring cell meets this one.
A wall's stile and a corner leg's end post drop at the ends `LeftRightEnds` names, where `NeighbourJoins` finds the glass running on, and a diagonal's join post drops where `GlassRunsOn` does.
A corner post's ends are not among a corner's left and right, and a diagonal's left and right say only whether its run carries on, so a corner post is always drawn, and a diagonal's mitre filler stays against a wall or a corner.
`EndContinuesGlazing` combines `JoinsAtEnd` with the existing `ContinuesGlazing`, so glass still merges only into glass in the same stack.
`NeighbourJoins` and `InnerCorners` both go through it, so a cell draws its inside corner by the same neighbour its post is dropped for.

**Wall against corner and diagonal.**
A glazed wall drops its stile beside a glazed corner's leg far end, and at a T-junction where the wall carries straight on past the corner post's end, coplanar with a leg.
It drops its stile against a glazed diagonal's end, and the diagonal's mitre filler stays as the one member.

**Corner against anything.**
A glazed corner drops a leg's end post where the glass runs on, by the same table.
The corner post stays.
Two glazed corners leg to leg, a bay two cells wide, merge with no post between.
`CornerOutElements` names the end posts `glazing-right` (leg 1) and `glazing-left` (leg 2) and gives each leg a top and a bottom filler, so the rails reach the cell's edge when a post has gone.
`CornerRimPieces` cuts a rim for each new part in every finish group on its leg, and `SelectiveElements` rims a corner's front on leg 1 only and its second front on leg 2 only.
`FrameParts` gives a glazed corner `""` plus the stiles and fillers a wall has, and an opaque corner keeps `""` and its rails alone.

**Glazed diagonal.**
`DiagonalFrame` gains `glazing-top` and `glazing-bottom` rails running end to end between the mitre fillers, with `-left` and `-right` pieces to the corner.
A diagonal takes no turned stile: the mitre filler is its stile.
Each `DiagonalFinish` group is cut into rims by the rails, and where the half post stays, `DiagonalRims` also cuts a `G-rim-join-{end}` over the post's whole height, which the cell draws in place of the rail-height pieces there, as a wall's stile rim runs full height.
`SelectiveElements` draws the glazing member and the rims when the diagonal is glazed, so `OnBlockInteractStart` places a finish on a glazed diagonal as on any glazed cell, and the `build-glazed` key and the handbook sentence that told players a glazed diagonal takes none are removed.

**Two glazed diagonals in a run.**
`GlassRunsOn` answers, per end, that the run carries on and the diagonal it carries on into is glazed in the same stack.
The cell then draws `glazing-top-join-{end}`, `glazing-bottom-join-{end}` and `infill-pane-join-{end}` in place of `framing-join-{end}`, and its rims follow the rails only.
Glass into a wattle-filled or bare diagonal keeps the post, as 0019 has it.
`GlassRunsOn` joins the mesh cache key.

**A stepped run of glazed diagonals.**
Where one cell is merged up or down and the diagonal over or under the next cell along carries no glass on, the sheet is an L, as on a wall.
`InnerCorners` names that corner for a glazed diagonal at each end its glass runs on through, from the cell over or under the next one along.
The cell then draws the rail it has merged away through the post's place, `glazing-top-join-{end}` or `glazing-bottom-join-{end}`, and its rims, so the next cell's rail ends against it.
`DiagonalElements` gives each rail's join pieces a face at the end toward the cell, as a wall's filler draws all six, so the piece is closed where no rail lies beside it.
The inside corners join the mesh cache key as a wall's do.

**Redraws.**
`MarkNeighboursDirty` marks the cell beyond each end at dy -1 to 1: a wall's two as before, a cornerout's four, which point every way, and a diagonal's two, where a wall or a corner meets its mitre filler.
`MarkDiagonalRunDirty` marks a diagonal's run neighbours at dy -1 to 1 where it marked them at its own height alone, so an inside corner redraws when glass goes in or out over or under the next cell.
`OnNeighbourBlockChange` redraws a glazed corner when a cell at one of its leg ends changes, and a wall when a cell at either of its ends does.

**Tests.**
A rotation-pinned table test turns each layout's unrotated ends with `Cuboidf.RotatedCopy`, and an agreement table lists, for each joining end of a west wall and a west corner, every cell it meets and whether that cell drops its own member.
The `Meetings` tests place every pair of cells whose ends meet, bar two diagonals, where at least one drops its member, with plain slab finishes and with none.
No two faces across their boundary share a plane and overlap, and every face a dropped end draws there is covered by the cell beyond, bar the listed slots, ledges and hairs; where the cell beyond keeps its member, its face is the jamb.
Two glazed diagonals whose glass runs on are checked the same way for every pair of stack heights, with the left cell facing either way, and the flush test also requires every rail end along the run to be closed by a face or carried on by another piece.
A kept join post's front and back are checked to be rimmed over the whole cell height however the cell is stacked.
A world-backed test reads a glazed corner's and a glazed diagonal's joins and inside corners through a stand-in `IBlockAccessor`.
The room-side mitre ledge and the 0.01 hairs at the filler's top and bottom are listed in the diagonal tests as expected-uncovered faces.
The existing name, rim, plane and deck tests are extended to the new parts.
The shape tests generate each shape once per run and reuse it, since regenerating the diagonal's for every pairing took the suite from about 20 seconds to over two minutes.

## Alternatives considered
- **Leave it as 0019 has it.** The corner reads as a unit and nothing is built. The doubled member is the cost, and it is what the playtest picked out.
- **Step 1 only, then play it.** The proposal staged the wall's stile first and the corner's end post second, gated on a playtest of the first. The gate was dropped: both cells must agree on a join in any case, so the predicate for step 2 is the one step 1 needs, and the two ship together.
- **Leave the corner post's T-junction unmerged.** The proposal rejected a merge there because the corner post cannot drop. The corner post still stays, but a wall carrying straight on past it now drops its own stile, so the corner post is the one member on the wall's side too.
- **A turned stile on a diagonal.** The mitre filler already closes the diagonal's end and serves as its stile, so a second member would stand beside it.
- **Reshape the mitre filler to remove the room-side ledge.** The ledge (decision 0066's 1.17) and the 0.01 hairs are accepted as they stand; changing the filler would change every diagonal, opaque ones included.
- **Narrow both members to 1.** Every glazed cell would change, and the rims and fillers of decision 0071 are cut to members 2 wide.
- **Check the join per pair of layouts.** A table keyed by layout pair grows with each layout; ends on a shared face and direction are one rule.

## Consequences & open questions
- `cornerout.json` grows from 1804 to 2132 elements, and `diagonal.json` from about 2.1 MB and 2218 elements to about 3.1 MB and 2916.
- No migration: nothing is stored, so a build that stands changes its look on load.
- A straight wall standing where a diagonal run passes its corner is a clashing build that `JoinsAtEnd` does not tell apart, since it compares the two ends alone (a `shortcut:` comment marks it), so the wall's stile may drop against a diagonal whose run goes on into the next diagonal there instead.
- A hosted cell's wall is read through `WallAt`, so a guest joins glass as any wall does, but `MarkNeighboursDirty` reaches block entities only, so a guest's panel is not redrawn when its neighbour's glass changes (decision 0035).
- Stacks of differing height across a glazed diagonal run frame the inside corner where one stops short of the other, and the flush tests cover every pair of stack heights.
- Decisions 0019, 0066 and 0071 are amended in part and say so in their status lines.
- Not played yet; the Atlas scenario asserts only that a finish goes on a glazed diagonal. To check: a bay of two glazed corners, a glazed wall running into a corner's post end at a T, and an octagon of glazed diagonals, each with and without finishes, from both sides.
