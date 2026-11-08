# 0082 — Inside corner post

- Status: Accepted; a bare bamboo post's flat faces superseded by 0092
- Created: 2026-11-01
- Reflects: branch `inside-corner-post`; `blocktypes/wall.json`'s `cornerin` layout, `shapes/block/wall/cornerin.json`, `SidingWallBlock.PostQuarter`/`PostConsumes`/`PostFaces`/`HostsClick`, `PlaceWallFrame.PostSide`, `SidingModePicker`'s `post` mode, `VSSiding.Tests/WallShapeGen` (`PostDeckStrips`), `VSSiding.E2E.Tests/PostScenarios.cs`; decisions 0002, 0035, 0042, 0053, 0066, 0069, 0071; the mod page comments listed in the retired `inside-corner-post` proposal (#cmt-243545, #cmt-243968, #cmt-243661, #cmt-244456, #cmt-245074, #cmt-245199, #cmt-245197, #cmt-249019); played in creative on 2026-11-02

## Summary
A `cornerin` post is a 4/16 square post in one corner of a cell, claiming no face and sealing nothing.
It closes the notch two walls leave at an inside corner, and the notch an L-shaped house leaves at its reflex corner.
It takes framing and a finish on each of its four faces, at a quarter of a wall's cost.

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
`WallShapeGen` generates `cornerin.json`: a 2/16 framing core and a 1/16 layer on each face, in every finish style and in the framing (`PostLayer`).

**Post mode and placement.**
The saw picker's framing row gains `post`.
A plank click in Post mode puts the post in the target cell's corner nearest the click (`PostSide` on the hit position inside the cell), not the player's facing.
A wall or corner clicked in Post mode does what it does today; there is no upgrade to or from a post.

**Cost.**
Framing and each finish charge `PostQuarter`, a quarter of the wall's quantity rounded up, at least 1 (`PostConsumes`).
Peeling and drops refund the same amount.

**Four finish faces, no infill.**
The face pointing at `side`'s opposite maps to `front`, the face pointing at `CorneroutSecondFace[side]`'s opposite to `secondfront`, the face pointing at `side` to `back`, and the face pointing at `CorneroutSecondFace[side]` to `secondback`, a slot only a post uses (`SidingWallEntity.SecondBack`).
A post's backs face the walls in an inside corner and show on a post standing free, as a stilt or at a wall's end.
A post takes no infill, so a framed post goes straight to finishes.
A bare face draws in the framing, so a bare post is the whole 4/16 timber, and a finish takes that face's outer 1/16.
Each face's layer owns the corner at one end of its run and meets the next face round in a mitre at the other, so each face shows its own finish, or the framing, to both edges.
A bare back against a wall is left out of the tooltip.
A finish clicked on a post's finished face does not pass to the face behind it (decision 0075): each face is clicked on its own, and an inside corner hides the backs, where a passed finish would cost a quarter of a wall to no visible end.

**No step.**
A step click on a post places nothing and shows `build-step-post`.

**Deck.**
A post takes a deck covering the cell minus the post: an L of two strips (`PostDeckStrips`), at the full deck cost and with the deck's usual layers (decision 0053).
The deck draws no ledge, as the diagonal's draws none (decision 0067's way of cutting groups from strips).
`cornerin`'s default `selectiveElements` leave the deck element names out, as the diagonal's do.

**Hosting.**
A post's cell hosts furniture on either of its two fronts (`HostsClick`); its backs look out of the cell.
The hosted block's shift is the sum of the shifts away from both faces, which is the diagonal shift of 4/16 off the post (decision 0035).

## Alternatives considered
- **A post with infill.** Rejected: the post is 4/16 wide and the walls beside it seal the cell.
- **Upgrading a wall or corner to a post in place.** Rejected: a post stands in a cell neither wall owns, so no existing piece is in it.
- **Placement by the player's facing, as `HorizontalOrientable` does.** Rejected: the post's corner is a property of where the click lands in the cell, not of where the player looks.
- **A post at full wall cost.** Rejected: it fills a sixteenth of the cell's area.
- **A post anywhere in the cell, not only a corner.** Out of scope.
- **Issue #132's slit.** A line beside a corner in one orientation, not the 4/16 notch; left alone.

## Consequences & open questions
- `blocktypes/wall.json` grows by four blocks, and `cornerin.json` by the post, its four layers in every style and the deck groups in every deck style.
- The four faces are generated in place rather than as one face turned by element `rotationY`, since how the game lights a quarter-turned element's face was not checked.
- A post takes no infill, no step and no in-place upgrade.
- A post's deck is the full deck cost for the L of the cell, not scaled to the post.
- Played: in creative on 2026-11-02, finishing both faces in planks, brick and plaster and stacking posts; the playtest found vertical boards stretched on the post's narrow faces, the front's finish wrapping the outer corner, and a free-standing post's two back faces taking no finish, all fixed before merge; the four-face post has not been played.
