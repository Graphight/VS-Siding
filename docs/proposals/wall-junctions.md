# Wall junctions

- Status: Draft
- Created: 2026-10-04
- Reflects: a creative build of the crossing on 2026-10-04, one corner and four finished arms, which came out as a clean cross; mod page comments https://mods.vintagestory.at/vssiding#cmt-243545, #cmt-243968, #cmt-243661 with its second screenshot, and #cmt-244456; the questions put to those players on 2026-10-05 (#cmt-244724, #cmt-244732) and the answers of 2026-10-06 (#cmt-245074, #cmt-245199 with its first screenshot, #cmt-245197 with both screenshots); issue #98; `SidingWallBlock.OnBlockInteractStart`'s corner upgrade, `CorneroutSecondFace`, `shapes/block/wall/wall.json`'s layer extents; vanilla `player.json`; the handbook text in `lang/en.json`; decisions 0002, 0008, 0026, 0027, 0042; the parked `multiple-walls-per-cell` and `auto-corners` notes in `README.md`; none of the players' own builds reproduced

## Summary
Four players describe places where walls meet that they could not build: a partition butting a wall, a four-way crossing, walls back to back, a "small corner", a one-wide stairwell.
The two layouts the mod has already cover the first two, and nothing in the game says so.
The proposal writes that into the guide and lets a filled wall be upgraded to a corner.
One answer is in: a second player has raised the gap at an inside corner, and a post for it follows once it is told apart from a glazing gap; every other new shape still waits on what the players built.

## Context
**What was reported.**
- "corners that end up in other blocks are impossible to construct if they're like a small corner."
- "Internal walls create a subdivision in the house that won't fill."
- "Putting two walls back-to back (for room symmetry) makes it so there is a gap between them, and since the facing is technically covered I have been unable to place the final siding component to complete either wall. floor joists wont fill this space either."
- "for internal walls it may be value-added to include a 4-way room corner".
- "A single wide 'stair well' option for framing, that just is a wall on the 'insides of a block', like that left wall, but both left and right", with a screenshot of a one-cell-wide stair between thin floors.
- A fourth player's workaround: "i fixed butting walls by chiseling a door frame as wide as the wall and made it an open doorway".

**What the players answered, 2026-10-06.**
- On the "small corner", from a player other than the one who reported it: "Yes, the little air gap is on the inside of the corners, which is a different block, so maybe a corner finish might be nice". The screenshot shows a slit one cell high where two walls meet, with the water outside visible through it, on a wall whose tooltip reads "Seals the room". The cell beside the slit is glazed.
- On the back-to-back walls, from the player who reported them: "place two walls back-to-back, then fill and side both. Next look where the two walls would touch on the inside edges and you'll note that there is a gap." They suggest "door/window joist framing to cover the gaps", or to "allow for edge-on placement of siding". No screenshot yet.
- On the stairwell, two screenshots of a built one, from above and from the stair below, and: "the top is acacia, and the wall for the stairwell is oak, and a full block of either ruins the other".
- The strip that "won't fill" and the one who reported the "small corner" have not answered.

**What the two layouts cover.**
A wall is a 4/16 slab on one face of its cell, and a `cornerout` is an L on two adjacent faces (decision 0002).
A wall line is a plane between cells, so any two lines that meet do so at a cell's corner.

- *T-junction.* A partition along the plane z = 5 meets an outer wall in column x = 0. The outer wall's cell (0, 5) holds a slab on its west face and 12/16 of open cell beside it, which is the slot the fourth player chiselled shut. A `cornerout` in that cell closes it, and decision 0026 upgrades a bare frame to one in place.
- *Crossing.* Walls along x = 5 and z = 5: every cell in column 5 takes its west face, every cell in row 5 its north face. Only cell (5, 5) is in both, and a `cornerout` there claims both faces. Every face on both planes is claimed, so the four rooms seal from each other, and the slabs touch with no slot. A "4-way corner" is one corner piece.

**Played, 2026-10-04.**
The crossing was built in creative as above: a corner at the shared cell, four arms, clay infill and a drystone finish on every face.
It reads as a clean cross with no slot and no jog, and the corner's tooltip lists all four directions finished.
A first attempt the same day did not come out, and what differed was not written down; the likely slip, a line that changes side partway, is the one a player without the rule would make.
Four sealed rooms around it were not checked with `/sidingroom`.
A dedicated four-way piece was considered and dropped: the piece that joins four aligned arms is the corner itself.

**Which side a wall lands on.**
Vanilla's `Block.SuggestedHVOrientation` hands `HorizontalOrientable` the direction the player is looking, and that becomes the wall's `side`.
So a wall lands on the far face of its block from where the player stands, and the player sees its back across the block's open part.
Decision 0002 expected the near face; the played crossing agrees with the code, since the wall placed from the lower left stands on the far side of its block.
Keeping a line of walls on one side therefore means placing every wall in it while facing the same way.

**What they do not cover.**
- *The inside-corner notch.* Two walls on the inside faces of their cells meet at a point and leave a 4/16 square open in a third cell. Decision 0002 calls it cosmetic, since no player fits through a point. A post to fill it would stand in a cell neither wall owns, which is the "small corner" that "ends up in other blocks": a second player's answer names it, "on the inside of the corners, which is a different block". Their screenshot does not settle what it looks like, because the slit in it runs beside a glazed cell, and a glazed cell takes no finish, so the two walls stop touching there for a second reason (issue #98).
- *Two walls in one cell.* Slabs on opposite faces of a cell leave 8/16 between them. The player's collision box is 0.6 wide (`player.json`), so a stair between two such walls could not be walked. The stairwell needs no wall in the stair's cell at all: a wall in the cell on each side, hugging the face towards the stair, stands flush against a full-width stair. Built that way in creative on 2026-10-05. Each wall's open side faces the room beyond, so neither room loses the space.
- *Front to front.* Two walls on either side of one cell boundary put their fronts on the same plane. The front finish is the outer 1/16 of each slab, so two unfinished fronts leave a 2/16 void between the framings, and no click can reach either face. This may be the "back-to-back" report: both rooms get the open side of a cell, which would be the symmetry. The second description does not settle it: "fill and side both" may mean only the faces a click can reach, and "edge-on placement of siding" points at the walls' end faces and not their fronts.
- *A floor through a wall's cell.* A thin floor and a wall cannot share a cell; the wall's deck is the floor there (decision 0042). Laying floors up to an internal wall without a deck leaves a strip, which may be the "subdivision that won't fill".
- *A decked wall's own top.* A deck fills the open 12/16 of its cell (decision 0042) and takes the floor's layers (decision 0053), and the wall's own 4/16 stays the wall's. Under an upper wall nobody sees it. Round a stairwell the wall stops at the floor, so its top shows as a strip in the wall's materials across a floor of another wood. This may be the oak beside acacia in the stairwell screenshots.

**Why the covered cases are missed.**
The guide's whole account of a corner is "an outside corner, two faces meeting", and that a bare frame can be upgraded.
It never mentions a partition or a crossing, and the name Corner itself points away from both.
The upgrade only runs while `Infill == null`: on a filled wall a corner-mode click with planks falls through to the finish match, or places a corner in the next cell.
Decision 0026 kept it to frames so a conversion would not "build the new leg's infill and finish for free", and a player meets the gap when the partition arrives, which is usually after the outer wall is filled.

**What is not known.**
Each report above has more than one reading, and "may be" marks a guess.
The crossing and the stairwell have been built to check; the inside corner, the back-to-back walls and the decked wall's top have not.

## Design
**A junctions paragraph in the guide.**
"Corners and junctions", in `gamemechanicinfo-siding-text`: where a partition meets a wall, and where two walls cross, the cell the lines share takes a Corner, and each line of walls keeps to one side of its cells, which means facing the same way for every wall in the line, since a wall lands on the far side of its block from you; a wall's own cell takes a deck and not a floor; a one-wide stair is walled from the cell on each side, and where that cell is also part of the floor above, the wall carries a deck.
Text only, with the played crossing as its picture on the mod page.

**Upgrade a filled wall.**
A `cornerout` costs what a `wall` costs at every layer: the frame, the infill and the back are shared between the legs and charged once, with no multiplier for layout.
So a filled wall turned into a corner in place is owed nothing, as a bare frame is owed nothing today.
`Framing`, `Infill` and `Back` carry over, and the new leg's own front starts unfinished.
It needs the checks filling a wall already makes, since the new leg closes space: nothing standing in it, and the room, liquid and light updates `OnInfillChanged` fires.
A wall with a step refuses, as it does now.

**A corner post, after one build.**
A player has now asked for the inside corner to be closed, as "a corner finish".
Before any shape, build an inside corner with no glazing and look at it from both sides: decision 0002 puts the open square outside the room, and the screenshot looks taken from inside, so the slit in it may be issue #98 alone.
If the notch shows as 0002 says, the piece is a `cornerin` post: one more state on `layout`, four more blocks, a shape, and a piece that claims no face and seals nothing.
What it is made of is open, since it stands in a cell neither wall owns and "a corner finish" asks for the walls' finish to carry round it.

**No other new shapes yet.**
A fill for the front-to-front void, a second wall per cell and a floor finish over a decked wall's top each answer one reading of one comment.
They wait for the players' answers.

## Alternatives considered
- **Walls pick their own corners from their neighbours** (`auto-corners`, parked). Still parked for its own reason: it would fight a player placing pieces on purpose. The guide and a later upgrade leave the player in charge.
- **Several walls per cell** (`multiple-walls-per-cell`, parked). Its revival test was a real build that needs it. The stairwell is the first candidate, and at 8/16 it cannot be walked, so it stays parked.
- **A stair layer on a thin floor**, so a stair and a floor share a cell as a stair and a wall do. The clash in the stairwell report reads as a wall and a floor wanting the same cell beside the stair, and a wall with a deck is both (decision 0042).
- **A dedicated four-way piece.** It would be the corner under another name. What players lack is the rule, not the block.
- **A `cornerin` post before the build.** It was held as a guess at what one sentence meant, and a second player has since confirmed the sentence. The one picture of it has a glazed cell in it, so the shape still waits on an unglazed corner.
- **Leave the inside corner as decision 0002 has it**, looks only. Two players have now raised it, one with a picture.
- **Treat front-to-front walls as joined, and draw the void filled.** It would be right if that is what the player built; a reply will say.
- **Refuse the corner click on a filled wall with a message.** Tells the player to peel the wall, where the upgrade can simply do it.

## Consequences & open questions
- The three players were asked on 2026-10-05. Still open: a screenshot of the "back-to-back" walls, which the player has said will follow; whether the strip that "won't fill" is in the floor or the wall; whether the decked wall's top is what reads wrong in the stairwell.
- The gesture for upgrading a filled wall. Planks are a finish on a filled wall, so a corner-mode plank click on its face already means "board this face". The wall's end face is free on a wall that is not mid-run, and so is a sneak-click.
- Whether a wall hosting furniture (decision 0035) can be upgraded, or refuses until the furniture is out.
- A cornerout takes no step (`build-step-corner`), so a stair cannot share the cell at a junction.
