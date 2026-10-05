# Wall junctions

- Status: Draft
- Created: 2026-10-04
- Reflects: a creative build of the crossing on 2026-10-05, found unsatisfactory; mod page comments https://mods.vintagestory.at/vssiding#cmt-243545, #cmt-243968, #cmt-243661 with its second screenshot, and #cmt-244456; `SidingWallBlock.OnBlockInteractStart`'s corner upgrade, `CorneroutSecondFace`, `shapes/block/wall/wall.json`'s layer extents; vanilla `player.json`; the handbook text in `lang/en.json`; decisions 0002, 0008, 0026, 0027, 0042; the parked `multiple-walls-per-cell` and `auto-corners` notes in `README.md`; none of the players' own builds reproduced

## Summary
Four players describe places where walls meet that they could not build: a partition butting a wall, a four-way crossing, walls back to back, a "small corner", a one-wide stairwell.
On paper the two layouts the mod has cover the first two, and nothing in the game says so.
In play the T works and the crossing, built from a corner, did not come out well enough.
The proposal writes the T into the guide, lets a filled wall be upgraded to a corner, and leaves the crossing and every new shape open until it is known what fell short.

## Context
**What was reported.**
- "corners that end up in other blocks are impossible to construct if they're like a small corner."
- "Internal walls create a subdivision in the house that won't fill."
- "Putting two walls back-to back (for room symmetry) makes it so there is a gap between them, and since the facing is technically covered I have been unable to place the final siding component to complete either wall. floor joists wont fill this space either."
- "for internal walls it may be value-added to include a 4-way room corner".
- "A single wide 'stair well' option for framing, that just is a wall on the 'insides of a block', like that left wall, but both left and right", with a screenshot of a one-cell-wide stair between thin floors.
- A fourth player's workaround: "i fixed butting walls by chiseling a door frame as wide as the wall and made it an open doorway".

**What the two layouts cover.**
A wall is a 4/16 slab on one face of its cell, and a `cornerout` is an L on two adjacent faces (decision 0002).
A wall line is a plane between cells, so any two lines that meet do so at a cell's corner.

- *T-junction.* A partition along the plane z = 5 meets an outer wall in column x = 0. The outer wall's cell (0, 5) holds a slab on its west face and 12/16 of open cell beside it, which is the slot the fourth player chiselled shut. A `cornerout` in that cell closes it, and decision 0026 upgrades a bare frame to one in place.
- *Crossing.* Walls along x = 5 and z = 5: every cell in column 5 takes its west face, every cell in row 5 its north face. Only cell (5, 5) is in both, and a `cornerout` there claims both faces. Every face on both planes is claimed, so on paper the four rooms seal from each other and the slabs touch with no slot.

**Played, 2026-10-05.**
The crossing was built with a corner as above and was not satisfactory.
What fell short is not recorded yet, and it decides the fix.
Three candidates: each line has to hug one side of its plane along its whole length, so arms raised from different rooms meet with a 4/16 jog; the corner upgrade refuses a wall that is already filled; or the frames and finishes do not meet cleanly where the neighbours butt the corner.
A dedicated four-way piece has been suggested.
In this geometry the piece that joins four aligned arms is the corner itself, so a new piece only helps if the trouble is something a new shape changes.

**What they do not cover.**
- *The inside-corner notch.* Two walls on the inside faces of their cells meet at a point and leave a 4/16 square open in a third cell. Decision 0002 calls it cosmetic, since no player fits through a point. A post to fill it would stand in a cell neither wall owns, which may be the "small corner" that "ends up in other blocks".
- *Two walls in one cell.* Slabs on opposite faces of a cell leave 8/16 between them. The player's collision box is 0.6 wide (`player.json`), so a stair between two such walls could not be walked. The stairwell works with one wall in the stair's cell (decision 0046) and the other in the cell next door.
- *Front to front.* Two walls on either side of one cell boundary put their fronts on the same plane. The front finish is the outer 1/16 of each slab, so two unfinished fronts leave a 2/16 void between the framings, and no click can reach either face. This may be the "back-to-back" report: both rooms get the open side of a cell, which would be the symmetry.
- *A floor through a wall's cell.* A thin floor and a wall cannot share a cell; the wall's deck is the floor there (decision 0042). Laying floors up to an internal wall without a deck leaves a strip, which may be the "subdivision that won't fill".

**Why the covered cases are missed.**
The guide's whole account of a corner is "an outside corner, two faces meeting", and that a bare frame can be upgraded.
It never mentions a partition.
The upgrade only runs while `Infill == null`: on a filled wall a corner-mode click with planks falls through to the finish match, or places a corner in the next cell.
Decision 0026 kept it to frames so a conversion would not "build the new leg's infill and finish for free", and a player meets the gap when the partition arrives, which is usually after the outer wall is filled.

**What is not known.**
Each report above has more than one reading, and "may be" marks a guess.
None has been built to check.

## Design
**A junctions paragraph in the guide.**
"Corners and junctions", in `gamemechanicinfo-siding-text`: where a partition meets a wall, the cell the lines share takes a Corner; a wall's own cell takes a deck and not a floor; a stair shares a cell with one wall.
The crossing stays out of the guide until it builds well.
Text only.

**Upgrade a filled wall.**
A `cornerout` costs what a `wall` costs at every layer: the frame, the infill and the back are shared between the legs and charged once, with no multiplier for layout.
So a filled wall turned into a corner in place is owed nothing, as a bare frame is owed nothing today.
`Framing`, `Infill` and `Back` carry over, and the new leg's own front starts unfinished.
It needs the checks filling a wall already makes, since the new leg closes space: nothing standing in it, and the room, liquid and light updates `OnInfillChanged` fires.
A wall with a step refuses, as it does now.

**No new shapes yet.**
A corner post, a fill for the front-to-front void and a second wall per cell each answer one reading of one comment, and wait for the players' answers.
A four-way piece waits for the played crossing's fault to be written down.

## Alternatives considered
- **Walls pick their own corners from their neighbours** (`auto-corners`, parked). Still parked for its own reason: it would fight a player placing pieces on purpose. The guide and a later upgrade leave the player in charge.
- **Several walls per cell** (`multiple-walls-per-cell`, parked). Its revival test was a real build that needs it. The stairwell is the first candidate, and at 8/16 it cannot be walked, so it stays parked.
- **A `cornerin` post now.** One state on `layout`, four more blocks, a shape, and a piece that claims no face. Cheap, but it is a guess at what one sentence meant.
- **Treat front-to-front walls as joined, and draw the void filled.** It would be right if that is what the player built; a reply will say.
- **Refuse the corner click on a filled wall with a message.** Tells the player to peel the wall, where the upgrade can simply do it.

## Consequences & open questions
- Ask the three players, with a screenshot each if they can: which cells held the two "back-to-back" walls and which faces they hugged; what the "small corner" was meant to join; whether the strip that "won't fill" is in the floor or the wall.
- The gesture for upgrading a filled wall. Planks are a finish on a filled wall, so a corner-mode plank click on its face already means "board this face". The wall's end face is free on a wall that is not mid-run, and so is a sneak-click.
- Whether a wall hosting furniture (decision 0035) can be upgraded, or refuses until the furniture is out.
- A cornerout takes no step (`build-step-corner`), so a stair cannot share the cell at a junction.
