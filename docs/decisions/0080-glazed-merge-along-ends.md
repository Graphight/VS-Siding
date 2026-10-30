# 0080 — Glazed cells merge along their ends

- Status: Accepted
- Created: 2026-10-30
- Reflects: branch `glazed-corner-merge`, which graduates the proposal of the same name (2026-10-11, from the playtest of PR #116); `SidingWallBlock.End`/`Ends`/`LeftRightEnds`/`EndsMeet`/`JoinsAtEnd`/`EndContinuesGlazing`/`GlassRunsOn`/`NeighbourJoins`/`InnerCorners`/`MarkNeighboursDirty`/`OnNeighbourBlockChange`/`OnBlockInteractStart`, `SidingWallEntity.FrameParts`/`SelectiveElements`/`CacheKey`, `VSSiding.Tests/WallShapeGen` (`CornerOutElements`, `CornerRimPieces`, `DiagonalFrame`, `DiagonalRims`), `shapes/block/wall/cornerout.json` and `diagonal.json`, `lang/en.json`'s `build-glazed`; `WallShapeGenTests`' cross-cell shared-plane and flush tests, `SidingWallBlockCollisionTests`' end table, `VSSiding.E2E.Tests/DiagonalScenarios.cs`; decisions 0019, 0066, 0071; the interview of 2026-10-30; unit tests (827) and e2e scenarios (92) pass

## Summary
A glazed wall, corner and diagonal drop the upright between them wherever their glass carries on through the shared boundary, so a run of glass is one sheet round a corner or along a diagonal run.
A glazed diagonal gets a glazing frame and takes a finish on each face as a rim, as a wall does.
Each cell's ends are named by the face they lie on and the way they point, so one rule answers for every pair of layouts.

## Context
Decision 0019 merges glazing in all four directions on a flat wall and keeps a `cornerout` merged only up and down, since "a corner has nothing to merge along".
Decision 0066 gives a diagonal plain plates and no bezel for glass.
Decision 0071 gives a glazed wall and corner a rim round the glass, and a glazed diagonal none, which the `build-glazed` refusal enforced.

The playtest of PR #116 showed a glazed corner between glazed walls with two members side by side where a flat run draws one sheet.
A corner has a far end on each leg, where a flat wall carries the same face on, and a diagonal has an end at each corner of its cell, so all three layouts have something to merge along.

## Design
**Ends.**
`End` is the face an end lies on, the direction the run carries on from it, and whether its upright is droppable.
`Ends(layout, side)` lists them: a wall's left and right, a cornerout's two leg ends then its corner post's two ends, a diagonal's two mitre ends.
A cornerout and a diagonal end right on `side` and left on the second face (`CorneroutSecondFace`).
`LeftRightEnds` names the two a cell's left and right joins answer for, which on a corner are leg 2 and leg 1.
`EndsMeet` is two ends on the same face pointing at each other, and `JoinsAtEnd` asks whether any end of the neighbouring cell meets this one.
A wall stile, a corner leg's end post and a diagonal's join post are droppable; a corner post and a diagonal's mitre filler never drop.
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
Each `DiagonalFinish` group is cut into rims by the rails, and where the half post stays, `DiagonalRims` also draws a full-height `G-rim-join-{end}` over it.
`SelectiveElements` draws the glazing member and the rims when the diagonal is glazed, so `OnBlockInteractStart` no longer refuses a finish on one, and the `build-glazed` key and the handbook sentence that told players a glazed diagonal takes none are removed.

**Two glazed diagonals in a run.**
`GlassRunsOn` answers, per end, that the run carries on and the diagonal it carries on into is glazed in the same stack.
The cell then draws `glazing-top-join-{end}`, `glazing-bottom-join-{end}` and `infill-pane-join-{end}` in place of `framing-join-{end}`, and its rims follow the rails only.
Glass into a wattle-filled or bare diagonal keeps the post, as 0019 has it.
`GlassRunsOn` joins the mesh cache key.

**Redraws.**
`MarkNeighboursDirty` marks a cornerout's four sideways neighbours at dy -1 to 1, and a wall's or diagonal's end neighbours as before, so a cell redraws when a glazed cell beside it changes.
`OnNeighbourBlockChange` redraws a glazed corner when a cell at one of its leg ends changes, and a wall when a cell at either of its ends does.

**Tests.**
A rotation-pinned table test turns each layout's unrotated end probes with `Cuboidf.RotatedCopy`, and a symmetry test asks both cells of every pair for every rotation, so a stile never drops against a post that has stayed.
For each new merge, a cross-cell `SharedPlaneOffenders` run and a cross-cell flush test run with plain slab finishes and with none: two-way where both cells drop their member, one-way where one keeps it and its face is the jamb.
The room-side mitre ledge and the 0.01 hairs at the filler's top and bottom are listed in the diagonal tests as expected-uncovered faces.
The existing name, rim, plane and deck tests are extended to the new parts.
The cross-cell tests generate each shape once per run and reuse it, since regenerating the diagonal's for every pairing took the suite from about 20 seconds to over two minutes.

## Alternatives considered
- **Leave it as 0019 has it.** The corner reads as a unit and nothing is built. The doubled member is the cost, and it is what the playtest picked out.
- **Step 1 only, then play it.** The proposal staged the wall's stile first and the corner's end post second, gated on a playtest of the first. The gate was dropped: both cells must agree on a join in any case, so the predicate for step 2 is the one step 1 needs, and the two ship together.
- **Leave the corner post's T-junction unmerged.** The proposal rejected a merge there because the corner post cannot drop. The corner post still stays, but a wall carrying straight on past it now drops its own stile, so the corner post is the one member on the wall's side too.
- **A turned stile on a diagonal.** The mitre filler already closes the diagonal's end and serves as its stile, so a second member would stand beside it.
- **Reshape the mitre filler to remove the room-side ledge.** The ledge (decision 0066's 1.17) and the 0.01 hairs are accepted as they stand; changing the filler would change every diagonal, opaque ones included.
- **Narrow both members to 1.** Every glazed cell would change, and the rims and fillers of decision 0071 are cut to members 2 wide.
- **Check the join per pair of layouts.** A table keyed by layout pair grows with each layout; ends on a shared face and direction are one rule.

## Consequences & open questions
- `cornerout.json` grows from 1804 to 2132 elements, and `diagonal.json` from about 2.1 MB and 2218 elements to about 2.7 MB and 2894.
- No migration: nothing is stored, so a build that stands changes its look on load.
- A straight wall standing where a diagonal run passes its corner is a clashing build, which `JoinsAtEnd` ignores (a `shortcut:` comment marks it), so its stile may drop against a diagonal that does not truly carry on.
- A hosted cell's wall is read through `WallAt`, so a guest joins glass as any wall does, but `MarkNeighboursDirty` reaches block entities only, so a guest's panel is not redrawn when its neighbour's glass changes (decision 0035).
- Stacks of differing height across a glazed diagonal run are merged and the flush tests do not cover them.
- Decisions 0019, 0066 and 0071 are amended in part and say so in their status lines.
- Not played yet; the Atlas scenario asserts only that a finish goes on a glazed diagonal. To check: a bay of two glazed corners, a glazed wall running into a corner's post end at a T, and an octagon of glazed diagonals, each with and without finishes, from both sides.
