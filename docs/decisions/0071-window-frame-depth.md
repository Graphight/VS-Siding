# 0071 — Window frame depth

- Status: Accepted
- Created: 2026-10-11
- Reflects: branch `window-frame-depth`; issue #98; mod page comment https://mods.vintagestory.at/vssiding#cmt-245199 with both screenshots; `VSSiding.Tests/WallShapeGen` (`WallElements`, `FloorElements`, `CornerOutElements`), `SidingWallEntity.SelectiveElements` and `DeckDrawsLedge`, `WallShapeGenTests.NoTwoFacesOfAGlazedCellsFramePiecesShareAPlaneAndOverlap`; decisions 0019, 0062, 0066; the gaps were read from the two screenshots and the shape's numbers, and neither the gaps nor the fix has been seen in game

## Summary
A window frame now fills its wall's or floor's full depth and reaches every edge of its cell.
It holds for flat walls, glass floors and decks, and glazed corners.

## Context
Two screenshots on the mod page (issue #98) show gaps round a glazed cell.
- A see-through slit one cell high beside a glazed cell at an inside corner, on a wall whose tooltip reads "Seals the room". The slit is only as tall as the glazed cell, with the rows above and below it closed.
- A bright line between a glazed cell and the board finish below it.

Both read as the frame's geometry in `WallShapeGen`, in voxels.
- A finished wall is 4 deep (x 0 to 4). The frame was 2 deep (x 1 to 3), and a glazed cell takes no finish, so the cell stood 1 voxel short on each face. Two walls at an inside corner touch along one edge, so that voxel opens a slit.
- Each frame member stopped a quarter-voxel short of the cell's edge while the pane ran edge to edge, which left a sliver of bare glass. Decision 0019 put the quarter-voxel there so that four overlapping members shared no plane.

This reading comes from the two screenshots and the shape's numbers.
It has not been reproduced in game, and neither has the fix.

## Design
**The members stop overlapping.**
With full depth and full reach, a stile and a rail that overlapped would share the wall's face planes and the cell's boundary planes, and no nudge is left to separate them.
So the rails run between the stiles, and a filler takes the corner block when the stile on that side has merged away, since the rail must still reach the cell's edge there.
This is the piece per corner that decision 0019's "overlap instead of mitred corners" avoided.
This amends the "Geometry" paragraph of decision 0019 and does not edit it.

**The wall's eight pieces** (x depth, y up, z along the run, in voxels, unrotated):

| Piece | From | To | Drawn when |
| --- | --- | --- | --- |
| `glazing-left` | (0, 0, 0) | (4, 16, 2) | left does not join |
| `glazing-right` | (0, 0, 14) | (4, 16, 16) | right does not join |
| `glazing-top` | (0, 14, 2) | (4, 16, 14) | above does not join |
| `glazing-bottom` | (0, 0, 2) | (4, 2, 14) | below does not join |
| `glazing-top-left` | (0, 14, 0) | (4, 16, 2) | above does not join and left joins |
| `glazing-top-right` | (0, 14, 14) | (4, 16, 16) | above does not join and right joins |
| `glazing-bottom-left` | (0, 0, 0) | (4, 2, 2) | below does not join and left joins |
| `glazing-bottom-right` | (0, 0, 14) | (4, 2, 16) | below does not join and right joins |

A filler and the stile on its side are never drawn together.

**Faces.**
A wall's rails leave out their two end faces, and a filler leaves out its inner end face, because each always stands against another piece.
A filler keeps its outer end face, because the neighbouring pane's own rail may have merged away.
The floor's pieces keep all six faces, because a deck cuts those boxes and a cut end has nothing standing against it.
`WallShapeGenTests.NoTwoFacesOfAGlazedCellsFramePiecesShareAPlaneAndOverlap` checks that no two faces of the pieces a glazed cell draws together lie in one plane, look the same way and overlap.
It runs for the wall, the floor and the corner.

**The floor and the decks.**
`FloorElements` lays the same eight pieces flat in the slab y 12 to 16: stiles (`glazing-left`, `glazing-right`) along z on the west and east edges, 2 wide; rails (`glazing-top`, `glazing-bottom`) along x on the north and south edges between them; fillers in the four corners.
`SidingFloorEntity.SelectiveElements` calls the wall's `SelectiveElements` with layout `wall`, so a floor takes the wall's rule for which pieces are drawn, and the wall and floor shapes changed in one commit.
`DeckGroups` cuts the deck and ledge groups from `FloorElements`, so a deck takes the same frame.
The block types' `ignoreElements` lists name the new pieces, so the default shape draws none of them.

**The ledge.**
A deck draws no ledge on a glazed wall (`DeckDrawsLedge`).
Decision 0062's ledge reaches the frame through the room-side finish's slot (x 3 to 4) only while that slot is bare, and the frame now fills it.

**The corner.**
A glazed `cornerout` draws a `glazing` group of three full-depth posts, in place of the ordinary posts and plates, whatever the framing's own `Elements` say.
- The corner post, (0, 0, 0) to (4, 16, 4).
- An end post 2 long at the far end of each leg, (0, 0, 14) to (4, 16, 16) and (14, 0, 0) to (16, 16, 4).

The posts never drop (decision 0019).
`glazing-top` and `glazing-bottom` are one rail per leg between the posts: (0, 14, 4) to (4, 16, 14) and (4, 14, 0) to (14, 16, 4) at the top, and the same at y 0 to 2 at the bottom.
A corner has no fillers, since its posts are always drawn.

## Alternatives considered
- **Keeping the overlap and dropping faces to avoid shared planes.** The first plan. With full depth, one member must give up a face on the cell's boundary, which shows as a hollow when a window stands under open air. A rail kept a quarter-voxel back from the wall's face left a slot beside a deck.
- **A smaller nudge than a quarter-voxel.** It shrinks the bare-glass sliver without closing it.
- **Letting a glazed cell take a finish.** Decision 0019 ruled it out: a slab over the glass would only hide it.
- **Flat walls only.** Chosen against in planning: floors, decks and corners carry the same thin frame.
- **A frame on a glazed diagonal.** Left to a later session, as decision 0066 has it.

## Consequences & open questions
- A glazed `diagonal` wall keeps plain plates and no frame, though its deck takes the floor's frame.
- Decision 0061 says a glazed corner "has no bezel groups and takes the pole plates"; a glazed corner now draws the `glazing` group whatever the framing's `Elements` say, as a glazed wall does.
- With the ledge gone, a decked glazed wall has a recess two voxels high between the deck's underside and the window's top rail. It is not see-through, and it has not been played.
- A glazed corner beside a glazed wall keeps both posts, which leaves 4 voxels of frame between the panes.
- Nothing here has been played. Still to look at: the two gaps closing, any flicker on a frame face, merged and L-shaped windows, a window in the top course seen from above, decked glazed walls and corners, and a glass floor beside floorboards.
- Issue #98 closes with this PR if the playtest agrees.
