# 0071 — Finishes on glazed cells

- Status: Accepted
- Created: 2026-10-11
- Reflects: branch `window-frame-depth`, PR #116; issue #98 and mod page comment https://mods.vintagestory.at/vssiding#cmt-245199 with both screenshots; `VSSiding.Tests/WallShapeGen` (`RimPieces`, `CornerRimPieces`, `RimPiece`, and the frame pieces in `WallElements`, `FloorElements` and `CornerOutElements`); `SidingWallEntity.FrameParts`, `RimNames` and `SelectiveElements`; `SidingFloorEntity.SelectiveElements`; the `selectiveElements` of `blocktypes/wall.json` and `floor.json`; `config/materials.json`'s leaded pane entry; decisions 0019, 0062, 0066, 0069, 0070; the playtest of the full-depth frame on 2026-10-11, and a first playtest of the rims the same day, on vertical boards only

## Summary
A glazed cell takes a finish on each face like any other wall, drawn as a rim round the glass and cut from the finish's own pieces.
The window frame reaches every edge of its cell.
Leaded glass panes are an infill.

## Context
Two screenshots on the mod page (issue #98) show gaps round a glazed cell.
- A see-through slit one cell high beside a glazed cell at an inside corner, on a wall whose tooltip reads "Seals the room". The slit is only as tall as the glazed cell, with the rows above and below it closed.
- A bright line between a glazed cell and the board finish below it.

Both read as the frame's geometry in `WallShapeGen`, in voxels.
- A finished wall is 4 deep (x 0 to 4), and its neighbours' finishes fill the slots at x 0 to 1 and x 3 to 4. The frame was 2 deep (x 1 to 3), and a glazed cell took no finish, so it stood 1 voxel short on each face. Two walls at an inside corner touch along one edge, so that voxel opens a slit.
- Each frame member stopped a quarter-voxel short of the cell's edge while the pane ran edge to edge, which left a sliver of bare glass. Decision 0019 put the quarter-voxel there so that four overlapping members shared no plane.

This reading comes from the two screenshots and the shape's numbers.
It has not been reproduced in game.

**The first attempt.**
The first attempt made the frame fill the wall's full depth (x 0 to 4) in the framing's wood, with the quarter-voxel gone.
Played on 2026-10-11, it looked right beside plank finishes and wrong beside every other finish.
It put one material in the slot that everywhere else belongs to the player's finish.
So the frame went back to the framing's depth, and the finish slot went back to the finish.

## Design
**The frame keeps the framing's depth and reaches the cell's edge.**
With full reach, a stile and a rail that overlapped would share the cell's boundary planes, and no nudge is left to separate them.
So the rails run between the stiles, and a filler takes the corner block when the stile on that side has merged away, since the rail must still reach the cell's edge there.
This is the piece per corner that decision 0019's "overlap instead of mitred corners" avoided, and it amends the "Geometry" paragraph of 0019 without editing it.
A filler and the stile on its side are never drawn together, so no two members overlap.

The wall's eight pieces (x depth, y up, z along the run, in voxels, unrotated), all at depth x 1 to 3:

| Piece | From | To | Drawn when |
| --- | --- | --- | --- |
| `glazing-left` | (1, 0, 0) | (3, 16, 2) | left does not join |
| `glazing-right` | (1, 0, 14) | (3, 16, 16) | right does not join |
| `glazing-top` | (1, 14, 2) | (3, 16, 14) | above does not join |
| `glazing-bottom` | (1, 0, 2) | (3, 2, 14) | below does not join |
| `glazing-top-left` | (1, 14, 0) | (3, 16, 2) | above does not join and left joins |
| `glazing-top-right` | (1, 14, 14) | (3, 16, 16) | above does not join and right joins |
| `glazing-bottom-left` | (1, 0, 0) | (3, 2, 2) | below does not join and left joins |
| `glazing-bottom-right` | (1, 0, 14) | (3, 2, 16) | below does not join and right joins |

A filler is also drawn at an L's inside corner, described below.
A wall's rails leave out their two end faces, because each always stands against another piece.
A filler keeps all six: its outer end face because the neighbouring pane's own rail may have merged away, and its inner one because at an inside corner it stands alone.

**An L's inside corner.**
A sheet of three cells in an L turns its frame in the middle cell, which has merged on both of the sides that meet there and so draws neither rail nor stile.
The stile of one arm and the rail of the other then touch only along an edge, and the 2 by 2 corner between them is open.
The playtest of 2026-10-11 picked it out on a glass floor.
Decision 0019's frame, as described there, leaves the same corner open, which was not built to check.
So a cell that joins on both sides of a corner also draws that corner's filler, and each finish's rim on it, when the cell diagonally across carries no glazing on (`InnerCorners` on the wall's and the floor's block, passed to `FrameParts`).
Four merged cells round a point leave it clear.
The pieces are the fillers above, so no shape gains an element.
A floor asks the cell diagonally across from both of the cells between, because a deck there has to reach both of the edges that meet at the corner.
A glazed corner merges only up and down, so it has no inside corner.

A floor lays the same eight pieces flat at y 13 to 15, the framing's depth between the underside finish and the top finish.
The stiles (`glazing-left`, `glazing-right`) run along z at x 0 to 2 and 14 to 16, the rails (`glazing-top`, `glazing-bottom`) along x at z 0 to 2 and 14 to 16 between them, and a filler takes each 2 by 2 corner.
Every floor piece keeps all six faces, because a deck cuts those boxes and a cut end has nothing standing against it.

A glazed `cornerout` draws a `glazing` group in place of the ordinary posts and plates, whatever the framing's `Elements` say.
- The corner post is two boxes, (1, 0, 1) to (3, 16, 4) and (3, 0, 1) to (4, 16, 3).
- An end post stands at the far end of each leg, (1, 0, 14) to (3, 16, 16) and (14, 0, 1) to (16, 16, 3).
- `glazing-top` is one rail per leg between the posts, (1, 14, 4) to (3, 16, 14) and (4, 14, 1) to (14, 16, 3), and `glazing-bottom` is the same at y 0 to 2.

So each leg's opening is 4 to 14 along its run and y 2 to 14.
The posts never drop (decision 0019), so a corner has no fillers.

**The rim.**
A glazed face draws the finish's own pieces cut down to a border, and leaves the middle open.
`WallShapeGen.RimPieces` takes each finish group of a shape (`front`, `front-weatherboard`, `back-brick`, a floor's `front-boards`) and cuts its boxes to the footprint of each frame piece on the face.
A cut piece is named `<group>-rim<part>`, where `<part>` is the frame piece's own suffix, so `front-weatherboard-rim-top-left` is the weatherboard cut to the `glazing-top-left` filler.
Four properties hold for every rim piece.
- It keeps the finish's depth and relief, so a lap or a brick course is the finish's own and not an imitation.
- It carries `ClippedFrom`, so its texture runs on from the uncut box, as the pieces of `DiagonalFinish` and `DeckGroups` do. Laps and joints then line up with the finished cells beside the window.
- A cut end draws its face, even where the group limits its faces, since the rim's inner edge looks onto the glass and its outer edges onto the next piece.
- A cell draws only the rim pieces whose frame piece it draws, so a rim edge drops wherever panes merge.

`CornerRimPieces` cuts a corner's groups round each leg's opening, read off that leg's own rails.
It makes three parts per group: `<group>-rim` (everything beside the opening, full height), `<group>-rim-top` and `<group>-rim-bottom`.
A `front` group belongs to leg 1, a `secondfront` group to leg 2, and a `back` box to the leg whose back slab holds it.

`RimPieces` runs on x and z for the floor, and `DeckGroups` cuts the deck and ledge groups from the floor's pieces including its rims, so a glazed deck takes rims like a glazed floor.

**A deck against a rim.**
A deck stops a voxel short of the frame, at the slot the wall's room-side finish takes, and draws a `ledge` copy into that slot while the side is bare (decision 0062).
A full finish fills the slot, but a rim leaves it open across the glass, and the rim's top pieces lie inside the deck's height.
So under a deck the room side's rim leaves out its top pieces, fillers included, and the deck draws ledge copies where they were, carried down to the deck's underside.
`ledgetop` lies between the stiles, or along each leg's opening on a corner, and `ledgetopleft` and `ledgetopright` reach the cell's edge where that stile has merged away.
`SidingWallEntity.Ledges` picks the copies and `WallShapeGen.RimLedges` cuts each to the footprint of the rim piece it replaces, so a ledge meets the rim's stiles edge to edge.

**One list of parts.**
`SidingWallEntity.FrameParts` returns the suffixes of the frame pieces a cell draws, from the joins.
`SelectiveElements` uses that one list for the frame and, through `RimNames`, for every rim, and `SidingFloorEntity.SelectiveElements` does the same for a floor and a deck.
A rim is therefore drawn exactly where its frame piece is.

**What a glazed cell does now.**
A glazed wall, corner, floor and deck finish, restyle and peel like any other, through the same click path and with the same cost and drops.
The glazed check in `SidingWallBlock.OnBlockInteractStart` stays for a glazed `diagonal`, which draws plain plates and no frame (decision 0066), so a slab over it would only hide its glass.
The `build-glazed` message now reads "A glazed diagonal takes no finish", which is the one case left.
The same checks on floors and decks are gone.
This amends 0019's "should not take a plank slab over it" and its "merging and refusing finishes belong to glass".
It also ends the glazed case in the fall-through list of decision 0069's corner upgrade: a glazed wall follows the ordinary rule, so planks board an unfinished glazed face as they board any other.

**Block types name what the default shape draws.**
The `*-wall-*` and `*-cornerout-*` variants and the floor hid pieces from their default shape with long `ignoreElements` lists, and the rims add eight names per finish group.
They now use `selectiveElements`, as the diagonal's variants already did, so a piece added to a shape is left out of the default by default.
The wall's and the corner's lists name 188 and 173 pieces, of which 179 and 164 are deck, ledge and step pieces.
They were kept to exactly what the default shapes draw on `main`, by a comparison of each variant's drawn set, not by a decision that the default should draw them.
`FinishElementGroupsTests` now checks that a finish or rim group exists in its shape and is not named by the list.

**Leaded glass.**
`config/materials.json` has an infill template `glasspane-leaded-{wood}`, matching `game:glasspane-leaded-*-ew` on its `wood` variant, with vanilla's leaded texture, `BlockMaterial: "Glass"` and `Transparent: true`.
It consumes one pane and drops the pane that went on, as the glass slab's entry does (decision 0070).
Glazed cells join on `Transparent` whatever the key (decision 0019), so a leaded pane merges with glass and slab cells.

**Tests.**
- `NoTwoFacesOfAGlazedCellsFramePiecesShareAPlaneAndOverlap` checks, for the wall, the floor and the corner and every set of merged sides, that no two faces of the frame, pane and rim pieces drawn together lie in one plane, look the same way and overlap.
- `EveryRimPieceDrawsAFaceOnEachPlaneItWasCutAt` and the two tests that add a cut's faces to a group limiting its faces (wall and floor) hold the cut ends.
- `RimPiecesKeepTheUncutFinishesUv` and `FloorCutsItsFinishesToTheGlazingFramesFootprints` hold the texture and the floor's cut on x and z.
- `EveryRimPieceSamplesWhatTheUncutFinishSamplesThere` compares every rim piece's texture coordinates with the uncut finish's at each corner of each face both draw, worked out from vanilla's `CubeMeshUtil` tables the way `ShapeTesselator` reads a face's rect and rotation. It was added after the playtest below, because the hand-written rows of the test above had passed a wrong value.
- Name tables for `SelectiveElements` on a wall, a corner and a floor, and `EveryRimElementAGlazedCellCanAskForExistsAndIsLeftOutOfTheDefaultShape` and its floor and deck forms, hold that every rim name exists in the generated shape.
- `AMergedCornerIsFramedOnlyWhereTheCellDiagonallyAcrossIsNoGlazing` and `AnInsideCornerDrawsItsFillerAndEachFinishesRimOnIt` hold an inside corner's rule and the names it draws on a wall, a floor and a deck, and the shared-plane test above runs the four fillers alone. Which cell counts as diagonally across is read from the world and has no test.
- `ADeckedGlazedCellsRoomSideRimLeavesItsTopToTheLedge` and `ADecksLedgesFillWhatTheRoomSideFinishLeavesOpen` are the name tables for a deck against a rim, and `ADeckedGlazedCellsLedgeSharesNoPlaneWithItsRimOrFrame` checks the pieces those names draw.

## Alternatives considered
- **A frame filling the full depth in the framing's wood.** Built, played on 2026-10-11 and dropped, as above. It closes the slit, but replaces the finish with the framing's wood at every window.
- **A flat rim in the finish's texture, without its relief.** A weatherboard's laps and a brick wall's joints would stop at the window instead of running on from the next cell.
- **Keeping the members' overlap, with a smaller nudge or with dropped faces.** A smaller nudge shrinks the sliver of bare glass without closing it. A dropped face shows as a hollow where a window stands under open air.
- **Flat walls only.** Floors, decks and corners carry the same frame and the same seam.
- **A rim on a glazed diagonal.** Left out, as decision 0066 leaves a glazing frame out of the diagonal.
- **Glass from other mods.** Waits until a player asks.

## Consequences & open questions
- An unfinished glazed cell beside finished walls shows the same seam any unfinished wall does, which includes issue #98's slit at an inside corner. The answer is to finish it. Issue #98 closes with this PR only if the playtest agrees.
- The shape files grew. Against `main`, `wall.json` went from 762 to 1808 elements, `cornerout.json` from 890 to 1804, `floor.json` from 137 to 245 and `diagonal.json` from 1630 to 2218, the last only through its decks. Rim pieces are 748, 750, 104 and 584 of them, and the ledges behind a rim are 286 and 152 more on the wall and the corner. `Shape.TryGet` parses the whole file on every call, and it was called for every mesh built, so `SidingWallEntity.ParsedShape` now keeps each shape once parsed; meshes were cached already. Frame time has not been measured.
- The default shapes of the wall and the corner name every deck, ledge and step piece they drew on `main`. Whether they should is open.
- A glazed deck on a diagonal takes rims, though the diagonal's own faces take none.
- A rim costs what a full finish costs, since it is laid by the same click.
- The rims were first played on 2026-10-11, on walls and a corner finished in vertical boards. That found two faults in the board rim of a corner's second leg, both in how a cut reads a turned face's texture: it was cut along the wrong axes, and then out of step with the uncut finish by up to a texel, since that leg is 15 long. Both are fixed in `WallShapeGen.UvLayout` and `ClippedUv`.
- An inside corner is drawn by a cell that shares no face with the cell deciding it, and vanilla tells only face neighbours of a change. A layer going on or coming off redraws the cells diagonally across (`MarkNeighboursDirty`). A whole glazed block removed at once tells them nothing, so one in another chunk may keep a stale corner until that chunk next redraws; this has not been tried.
- Still to look at: an inside corner on a wall, a floor and a deck, the rim beside brick, daub, weatherboard and shakes, merged and L-shaped windows, floors and decks, a deck against a finished window, leaded panes, and a world saved before this change.
- A glazed corner still merges only up and down (decision 0019), so a glazed wall beside it keeps its stile against the corner's end post. The `glazed-corner-merge` proposal covers that.
