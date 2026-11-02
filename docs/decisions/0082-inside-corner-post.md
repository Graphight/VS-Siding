# 0082 — Inside corner post

- Status: Accepted
- Created: 2026-11-01
- Reflects: branch `inside-corner-post`; `blocktypes/wall.json`'s `cornerin` layout, `shapes/block/wall/cornerin.json`, `SidingWallBlock.PostQuarter`/`PostConsumes`/`PostFaces`/`HostsClick`, `PlaceWallFrame.PostSide`, `SidingModePicker`'s `post` mode, `VSSiding.Tests/WallShapeGen` (`PostDeckStrips`), `VSSiding.E2E.Tests/PostScenarios.cs`; decisions 0002, 0035, 0042, 0053, 0066, 0069, 0071; the mod page comments listed in the retired `inside-corner-post` proposal (#cmt-243545, #cmt-243968, #cmt-243661, #cmt-244456, #cmt-245074, #cmt-245199, #cmt-245197, #cmt-249019); not yet played

## Summary
A `cornerin` post is a 4/16 square post in one corner of a cell, claiming no face and sealing nothing.
It closes the notch two walls leave at an inside corner, and the notch an L-shaped house leaves at its reflex corner.
It takes framing and a finish on each of its two cell-facing faces, at a quarter of a wall's cost.

## Context
Two walls on the inside faces of their cells meet at a point and leave a 4/16 square open in a third cell.
Decision 0002 calls it cosmetic, since no player fits through a point.
Players asked for it closed: "corners that end up in other blocks are impossible to construct if they're like a small corner", "maybe a corner finish might be nice", and, for an L-shaped house, "a corner post that just needs siding and no infill".
The one screenshot of the notch has a glazed cell beside the slit, and decision 0071 has since changed how a glazed cell meets its neighbour.
An unglazed inside corner and one beside a finished glazed cell were built in creative before this work began, and recorded outside this repository; this decision does not restate what they showed.

## Design
**Layout.**
`cornerin` is one more state on `layout`, with the four `side` variants.
The post stands in the corner between the `side` face and `CorneroutSecondFace[side]`, the corner a `cornerout` of the same side turns (`PostFaces`).
It claims no face, seals nothing and absorbs no light; the walls on either side already close the cell's faces.
Collision is the post's own box.
`WallShapeGen` generates `cornerin.json`: a post frame and one finish group per face.

**Post mode and placement.**
The saw picker's framing row gains `post`.
A plank click in Post mode puts the post in the target cell's corner nearest the click (`PostSide` on the hit position inside the cell), not the player's facing.
A wall or corner clicked in Post mode does what it does today; there is no upgrade to or from a post.

**Cost.**
Framing and each finish charge `PostQuarter`, a quarter of the wall's quantity rounded up, at least 1 (`PostConsumes`).
Peeling and drops refund the same amount.

**Two finish faces, no infill.**
The face pointing at `side`'s opposite maps to `front`, and the face pointing at `CorneroutSecondFace[side]`'s opposite to `secondfront`.
A post takes no infill, so a framed post goes straight to finishes.

**No step.**
A step click on a post places nothing and shows `build-step-post`.

**Deck.**
A post takes a deck covering the cell minus the post: an L of two strips (`PostDeckStrips`), at the full deck cost and with the deck's usual layers (decision 0053).
The deck draws no ledge, as the diagonal's draws none (decision 0067's way of cutting groups from strips).
`cornerin`'s default `selectiveElements` leave the deck element names out, as the diagonal's do.

**Hosting.**
A post's cell hosts furniture on either of its two faces (`HostsClick`).
The hosted block's shift is the sum of the shifts away from both faces, which is the diagonal shift of 4/16 off the post (decision 0035).

## Alternatives considered
- **A post with infill.** Rejected: the post is 4/16 wide and the walls beside it seal the cell.
- **Upgrading a wall or corner to a post in place.** Rejected: a post stands in a cell neither wall owns, so no existing piece is in it.
- **Placement by the player's facing, as `HorizontalOrientable` does.** Rejected: the post's corner is a property of where the click lands in the cell, not of where the player looks.
- **A post at full wall cost.** Rejected: it fills a sixteenth of the cell's area.
- **A post anywhere in the cell, not only a corner.** Out of scope.
- **Issue #132's slit.** A line beside a corner in one orientation, not the 4/16 notch; left alone.

## Consequences & open questions
- `blocktypes/wall.json` grows by four blocks, and `cornerin.json` by the post, its two finish groups and the deck groups in every deck style.
- A post takes no infill, no step and no in-place upgrade.
- A post's deck is the full deck cost for the L of the cell, not scaled to the post.
- Played: not yet played in game; the playtest is the last step before merge.
