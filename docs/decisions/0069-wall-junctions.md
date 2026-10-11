# 0069 — Wall junctions

- Status: Accepted
- Created: 2026-10-10
- Reflects: branch `wall-junctions`; `SidingWallBlock.TryUpgradeLayout`, `FrontMovesToSecondFace` and `OnBlockInteractStart`'s filled path; `VSSiding.E2E.Tests/JunctionScenarios.cs`; the "Corners and junctions" paragraph and the Framing bullet in `gamemechanicinfo-siding-text` in `lang/en.json`; a creative build of the crossing on 2026-10-04; mod page comments https://mods.vintagestory.at/vssiding#cmt-243545, #cmt-243968, #cmt-243661 with its second screenshot, #cmt-244456, #cmt-245074, #cmt-245199 with its first screenshot and #cmt-245197 with both screenshots; vanilla `player.json`; decisions 0002, 0026, 0042, 0058; the `wall-junctions` proposal; none of the players' own builds reproduced

## Summary
A `cornerout` in the shared cell already makes a T-junction and a crossing, and nothing in the game said so.
The guide now says it, and a filled wall can be turned into a corner in place.

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
- *Crossing.* Walls along x = 5 and z = 5: every cell in column 5 takes its west face, every cell in row 5 its north face. Only cell (5, 5) is in both, and a `cornerout` there claims both faces. Every face on both planes is claimed, so the four rooms seal from each other, and the slabs touch with no slot. A "4-way corner" is one corner piece.

**Played, 2026-10-04.**
The crossing was built in creative as above: a corner at the shared cell, four arms, clay infill and a drystone finish on every face.
It reads as a clean cross with no slot and no jog, and the corner's tooltip lists all four directions finished.
A first attempt the same day did not come out, and what differed was not written down; the likely slip, a line that changes side partway, is the one a player without the rule would make.
Four sealed rooms around it were not checked with `/sidingroom`.

**Why the covered cases were missed.**
The guide's whole account of a corner was "an outside corner, two faces meeting", and that a bare frame can be upgraded.
It never mentioned a partition or a crossing, and the name Corner itself points away from both.
The upgrade only ran while `Infill == null`: on a filled wall a Corner-mode click with planks fell through to the finish match, or placed a corner in the next cell.
Decision 0026 kept it to frames so a conversion would not "build the new leg's infill and finish for free", and a player meets the gap when the partition arrives, which is usually after the outer wall is filled.

**Which side a wall lands on.**
Vanilla's `Block.SuggestedHVOrientation` hands `HorizontalOrientable` the direction the player is looking, and that becomes the wall's `side`.
So a wall lands on the far face of its block from where the player stands, and the player sees its back across the block's open part.
Decision 0002 expected the near face.
This decision records the far face as what the code does and what play showed: the crossing's wall placed from the lower left stands on the far side of its block.
Keeping a line of walls on one side therefore means placing every wall in it while facing the same way.

**The stairwell.**
The stairwell needs no wall in the stair's cell at all.
A wall in the cell on each side, hugging the face towards the stair, stands flush against a full-width stair.
Built that way in creative on 2026-10-05.
Each wall's open side faces the room beyond, so neither room loses the space.
Two walls in one cell would not do it: slabs on opposite faces leave 8/16 between them, and the player's collision box is 0.6 wide (`player.json`), so a stair between them could not be walked.
The player who reported the stairwell sent two screenshots of a built one, from above and from the stair below, and wrote: "the top is acacia, and the wall for the stairwell is oak, and a full block of either ruins the other".

## Design
**A "Corners and junctions" paragraph in the guide.**
It sits in `gamemechanicinfo-siding-text` between the saw modes and "More".
Where a partition meets a wall, or two walls cross, the block the two lines share takes a Corner.
Each line of walls keeps to one side of its blocks, which means facing the same way for every wall in the line, since a wall lands on the far side of its block from the player.
A wall's own block takes a deck and not a floor, because a thin floor and a wall cannot share a cell (decision 0042).
A stair one block wide is walled from the block on each side of it, and where that block is also part of the floor above, the wall carries a deck.
The paragraph is text only; the played crossing is meant as its picture on the mod page.
The Framing bullet's upgrade sentence gains a second case: a filled wall upgrades the same way when the face clicked is already finished, and on an unfinished face planks board it.

**Upgrade a filled wall: `wall` to `cornerout` only.**
`SidingWallBlock.TryUpgradeLayout` is the method decision 0026's bare-frame upgrade became, and the filled path of `OnBlockInteractStart` now calls it.
A diagonal is not a target, and neither is any layout but `wall`.

**Nothing is charged.**
A `cornerout` costs what a `wall` costs at every layer: the frame, the infill and the back are shared between the legs and charged once, with no multiplier for layout.
So a filled wall turned into a corner is owed nothing, as a bare frame is owed nothing.
This reverses the cost reasoning in decision 0026, which kept the upgrade to frames because converting a filled wall would "build the new leg's infill and finish for free".
The legs share framing, infill and back, so no layer is built for free that was not already paid for, and the new leg's own front starts unfinished.
Decision 0026 stays Accepted; this decision extends it and does not supersede it.

**What carries over.**
`ExchangeBlock` keeps the block entity, so `Framing`, `Infill`, `Back` and the deck carry over untouched.
The wall's old front finish keeps its face.
Whether that face is the corner's front or its second face depends on the end clicked: `FrontMovesToSecondFace` is true when the chosen `cornerout` is the one whose second face is the wall's old side.
In that case the old `Front` and its style move to the `SecondFront` slot, and `Front` is left empty for the new leg.
Otherwise the old finish stays in `Front`.

**Refusals.**
A wall with a step is not upgraded, as on a bare frame, and the player is shown `build-stepped`, because a `cornerout` takes no step.
A wall is not upgraded while an entity stands in the corner's boxes, and the player is shown `build-occupied`, since the new leg closes space.
The check uses the target's collision boxes, not the wall's.

**`OnInfillChanged` fires on the target block.**
The new leg changes retention, the liquid barrier and light, which `OnInfillChanged` updates.
It exchanges the block for its own `Id`, so called on the old `wall` it would swap the wall back.
It is called on the target `cornerout`.

**The gesture: only where the click cannot board the face.**
A plain Corner-mode click with a framing material on the front or back face upgrades a filled wall only where that click fell through to `PlaceWallFrame` before.
Those are the three fall-throughs of the finish path: the held item is no finish (sticks, bones), the cell is glazed, or the face is already finished and the click is not a restyle.
An unfinished face still takes planks as a finish, so the existing boarding gesture is untouched.

## Alternatives considered
- **Walls pick their own corners from their neighbours** (`auto-corners`, parked). Still parked for its own reason: it would fight a player placing pieces on purpose. The guide and the upgrade leave the player in charge.
- **Several walls per cell** (`multiple-walls-per-cell`, parked). Its revival test was a real build that needs it. The stairwell was the first candidate, and at 8/16 it cannot be walked, so it stays parked.
- **A dedicated four-way piece.** It would be the corner under another name. What players lacked was the rule, not the block.
- **Refuse the corner click on a filled wall with a message.** Tells the player to peel the wall, where the upgrade can simply do it.
- **A stair layer on a thin floor**, so a stair and a floor share a cell as a stair and a wall do. The clash in the stairwell report reads as a wall and a floor both falling in the same cell beside the stair, and a wall with a deck is both (decision 0042).
- **A sneak-click routed through `PlaceWallFrame`** (decision 0058's route). It does not clash with boarding, but it is a second gesture to learn for what is the same Corner pick.
- **Corner mode outranking boarding.** A player boarding with Corner still picked would turn walls into corners, with no way back short of peeling.
- **A filled wall to a diagonal.** Not built, because nobody asked for it.

## Consequences & open questions
- The same click does two things depending on whether the face is finished: it boards an unfinished face and upgrades a finished one. The guide states this in one sentence.
- A wall hosting furniture cannot be upgraded. The cell's block is the furniture and the wall is a guest (decision 0035), so no wall click reaches it. The furniture comes out first.
- A `cornerout` takes no step, so a stair cannot share the cell at a junction.
- The upgraded corner's look and the four rooms' seal have Atlas coverage for layers and per-face retention only. There has been no in-game playtest and no `/sidingroom` check yet.
- The crossing screenshot for the mod page is not yet taken.
- What did not ship lives in the `inside-corner-post` proposal: the inside-corner post, and the shapes that wait on the players' answers.
