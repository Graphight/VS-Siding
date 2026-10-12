# Glazed corners merge along their legs

- Status: Draft
- Created: 2026-10-11
- Reflects: branch `window-frame-depth` at `e9493df` (PR #116) and its playtest of 2026-10-11, whose screenshot shows a glazed corner between glazed walls; decisions 0019 and 0071; `SidingWallBlock.NeighbourJoins`, `SameRunNeighbour` and `ContinuesGlazing`; `SidingWallEntity.FrameParts` and `Ledges`; `WallShapeGen.CornerOutElements`, `CornerRimPieces` and `RimLedges`; nothing built, and no player has reported it

## Summary
A glazed wall beside a glazed corner keeps its stile, and the corner keeps its end post, so two members stand side by side between panes that a flat run would draw as one sheet.
The proposal drops the wall's stile there first, which is small, and the corner's end post second, if one member between the panes still reads as a break.

## Context
**What merges today.**
Decision 0019 merges glazing in all four directions on a flat wall, and keeps a `cornerout`'s three posts, merging it only up and down: "corners are structural and a corner has nothing to merge along".
A corner does have something to merge along, which is each leg's far end, where a flat wall carries the same face on.

**What stands there.**
On the unrotated corner, leg 1 is the slab at x 0 to 4 running along z, and leg 2 the slab at z 0 to 4 running along x.
The corner post is at the low end of both.
Each leg's end post is 2 along its run, at z 14 to 16 on leg 1 and x 14 to 16 on leg 2.
The flat wall beyond it draws a stile 2 wide against the same boundary.
Together that is a member 4 wide, with each cell's rim over its own half once the faces are finished (decision 0071).

**Why neither side joins.**
`SameRunNeighbour` takes a neighbour only when its layout and its side both match, so a flat wall never counts a corner, nor a corner a wall.
`NeighbourJoins` returns a corner's answer before it asks about either side.

**What cannot change.**
Any infill makes a wall's collision the full slab, so no merge reaches collision or selection (decision 0019).
Nothing is stored: what a cell draws is worked out from its neighbours each time its mesh is built.

## Design
**Step 1: the wall gives up its stile.**
A flat wall's `ContinuesGlazing` also takes a glazed corner beside it, when one of the corner's legs lies on the wall's face and that leg's far end is the one at the shared boundary.
The wall then drops its stile through `FrameParts` as it does beside any glazed wall, and the fillers decision 0071 added carry its rails to the cell's edge, where they meet the corner's end post.
The corner draws exactly what it draws now.
One member 2 wide is left between the panes, which is what a single window frame looks like.
No shape changes, so this is one predicate, its rotation table, a redraw of the walls beside a corner when its glass changes, and the shared-plane test run across the wall and the corner together.

**Play it before step 2.**
If one member between a corner and a run reads as a frame and not as a break, the proposal ends there.

**Step 2: the corner gives up its end post.**
`NeighbourJoins` answers a corner's two sideways slots, which are free on a corner, one for each leg's far end, on the same predicate turned round.
Both cells must agree, or a wall drops its stile against a post that has also gone: one test asks both sides for every rotation.
The corner's `glazing` group is one name for four boxes, so the two end posts take names of their own, and each leg gains a top and a bottom filler for its rails to reach the cell's edge, as the wall has.
`CornerRimPieces` cuts a rim for each new part, in every finish group on that leg.
`Ledges` and `RimLedges` gain the end pieces a deck needs where a leg's end has merged, as `ledgetopleft` and `ledgetopright` serve the wall.
The corner post never drops.

**Which end is which.**
Decision 0019 pinned "left along a run" for the wall against the rotation with a test and not a comment.
The corner's far ends get the same: one table of side, leg, and the cell beyond its far end.

## Alternatives considered
- **Leave it as decision 0019 has it.** The corner reads as a unit of its own, and nothing is built. The doubled member is the cost, and it is what the playtest picked out.
- **Step 2 without step 1.** The full merge in one piece of work. Step 1 is most of the look for a small part of the cost, and its predicate is needed by step 2 anyway.
- **Merge at the corner post's end too**, where a wall carries straight on past a corner at a T-junction. The corner post is where the two legs meet, so nothing there can drop.
- **Narrow both members to 1** so the pair reads as one frame. Every glazed cell would change, and the rims and fillers of decision 0071 are cut to members 2 wide.

## Consequences & open questions
- Decision 0019's "merges only vertically" is amended by whatever decision this graduates into.
- A build that stands already changes its look on load, with no migration, since nothing is stored.
- Both steps need a mesh rebuilt that is not rebuilt today. `SidingWallBlock.OnNeighbourBlockChange` returns early for a corner on any sideways change, and the infill-change path that redraws a wall's run neighbours returns early for a corner too. Step 1 needs a corner whose glass goes on or comes off to redraw the walls beyond its legs, and step 2 needs the corner to redraw when one of those walls changes.
- Two corners meeting leg to leg, as on a bay two cells wide, would under step 2 leave rails with no post between two corner posts. Whether that is wanted is open.
- Step 2 grows `cornerout.json`, which stands at 1804 elements. The count is not worked out.
- `EveryRimPieceSamplesWhatTheUncutFinishSamplesThere` covers any new rim piece's texture as it stands, and the shared-plane and name tests of decision 0071 need the corner's new parts added.
