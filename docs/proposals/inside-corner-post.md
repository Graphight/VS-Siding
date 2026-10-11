# Inside corner post

- Status: Draft
- Created: 2026-10-10
- Reflects: decision 0069 and the `wall-junctions` proposal it graduated from; mod page comments https://mods.vintagestory.at/vssiding#cmt-243545, #cmt-243968, #cmt-243661 with its second screenshot, #cmt-244456, the questions put to those players on 2026-10-05 (#cmt-244724, #cmt-244732) and the answers of 2026-10-06 (#cmt-245074, #cmt-245199 with its first screenshot, #cmt-245197 with both screenshots); issue #98; decisions 0002, 0042, 0053; none of the players' own builds reproduced

## Summary
Two walls meeting at an inside corner leave a 4/16 square open in a third cell, and one player has asked for it to be closed.
The proposal is a post for that notch, held until an unglazed build shows whether the notch looks as decision 0002 says.
The other shapes players described are listed separately, because each waits on an answer that has not come.

## Context
**The inside-corner notch.**
Two walls on the inside faces of their cells meet at a point and leave a 4/16 square open in a third cell.
Decision 0002 calls it cosmetic, since no player fits through a point.
A post to fill it would stand in a cell neither wall owns, which is the "small corner" that "ends up in other blocks".
One player wrote "corners that end up in other blocks are impossible to construct if they're like a small corner."
A second player, other than the one who reported it, answered on 2026-10-06: "Yes, the little air gap is on the inside of the corners, which is a different block, so maybe a corner finish might be nice".
Their screenshot shows a slit one cell high where two walls meet, with the water outside visible through it, on a wall whose tooltip reads "Seals the room".
The cell beside the slit is glazed.
A glazed cell takes no finish, so the two walls stop touching there for a second reason (issue #98), and the screenshot does not settle what the notch looks like on its own.
The screenshot also looks taken from inside, while decision 0002 puts the open square outside the room.

**Not yet answered.**
The player who reported the "small corner" and the one who reported the strip that "won't fill" have not answered.

## Design
**Build first.**
Build an inside corner with no glazing and look at it from both sides.
If the notch shows as decision 0002 says, a slit that only appears next to glass is issue #98 and not a missing piece.

**The post, if the build shows the notch.**
A `cornerin` post is one more state on `layout`, four more blocks and a shape.
It claims no face and seals nothing, since the walls on either side already close the cell's faces.
What it is made of is open.
It stands in a cell neither wall owns, and "a corner finish" asks for the walls' finish to carry round it, so the material may have to come from the walls' layers and not from the post's own block.

## Alternatives considered
- **A `cornerin` post before the build.** Held as a guess at what one sentence meant. A second player has since confirmed the sentence, but the one picture of it has a glazed cell in it, so the shape waits on an unglazed corner.
- **Leave the inside corner as decision 0002 has it**, looks only. Two players have now raised it, one with a picture.

## Consequences & open questions
- The unglazed build has to come first, and nobody has made it.
- What the post is made of, and whether it takes a finish.

## Other shapes waiting on players' answers
None of these is proposed yet.
Each answers one reading of one comment, and the players were asked on 2026-10-05.

**Front to front.**
Two walls on either side of one cell boundary put their fronts on the same plane.
The front finish is the outer 1/16 of each slab, so two unfinished fronts leave a 2/16 void between the framings, and no click can reach either face.
This may be the "back-to-back" report: both rooms get the open side of a cell, which would be the symmetry.
The player who reported it wrote: "Putting two walls back-to back (for room symmetry) makes it so there is a gap between them, and since the facing is technically covered I have been unable to place the final siding component to complete either wall. floor joists wont fill this space either."
Their answer of 2026-10-06 was: "place two walls back-to-back, then fill and side both. Next look where the two walls would touch on the inside edges and you'll note that there is a gap."
They suggest "door/window joist framing to cover the gaps", or to "allow for edge-on placement of siding".
No screenshot yet.
The answer does not settle it: "fill and side both" may mean only the faces a click can reach, and "edge-on placement of siding" points at the walls' end faces and not their fronts.
A fill for the void would be right if the walls were built front to front, and a reply will say.
Still open: a screenshot of the walls, which the player has said will follow.

**A floor through a wall's cell, and the strip that "won't fill".**
A thin floor and a wall cannot share a cell; the wall's deck is the floor there (decision 0042).
Laying floors up to an internal wall without a deck leaves a strip, which may be the report "Internal walls create a subdivision in the house that won't fill."
Still open: whether the strip that won't fill is in the floor or in the wall.

**The decked wall's own top.**
A deck fills the open 12/16 of its cell (decision 0042) and takes the floor's layers (decision 0053), and the wall's own 4/16 stays the wall's.
Under an upper wall nobody sees it.
Round a stairwell the wall stops at the floor, so its top shows as a strip in the wall's materials across a floor of another wood.
This may be the oak beside acacia in the stairwell player's screenshots, taken from above and from the stair below, with the comment: "the top is acacia, and the wall for the stairwell is oak, and a full block of either ruins the other".
Still open: whether the decked wall's top is what reads wrong in the stairwell.
A floor finish over that top, or a second wall per cell (`multiple-walls-per-cell`, parked), would each answer one reading of it.
