# Stairs against walls

- Status: Draft
- Created: 2026-09-25
- Reflects: playtest of decision 0042's deck on PR #52; decision 0035; `SidingModSystem.IsHostable`; not yet played

## Summary
A staircase built along a wall stands in the room cells beside the wall column, so every step leaves the wall cell's open 12/16 between its side and the panel.
The deck's opt-in is right for a stairwell's upper floor, but the stairs themselves still cannot reach the wall.
The proposal adds a step fill: a wall layer that continues the stair beside it into the wall's open part, built by clicking the wall with that stair in hand.

## Context
**Why stairs don't reach.**
A wall's panel hugs one face of its cell and leaves 12/16 open (decision 0002), and a vanilla stair is a full cell wide, so it can only stand beside the wall column.
Hosting it in the wall's cell (decision 0035) is refused: `IsHostable` turns away any block with a solid side, and a stair has several.
Even hosted, it would be shifted 4/16 off the panel and poke 4/16 into the next cell, which on a two-wide staircase is the next stair.

**What play showed.**
The deck works and the upper room still seals; a stairwell left undecked keeps its gap as intended, but the stairs up to it stand three quarters of a block off the wall.

## Design
**A step layer, like the deck.**
A new optional layer on `SidingWallEntity`, `Step`, holding the stair's material and its orientation and half (up or down).
It draws the stair's profile clipped to the open 12/16, so the tread and riser continue flush from the stair in the room cell to the panel.
Collision and selection add the same clipped boxes, the way `AddDeckBox` does for the deck.

**Built from the stair itself.**
Saw in the off hand, a stair block in hand, a click on the wall: the step takes the held stair's material, and its orientation from the stair in the room cell beside the clicked face, or from the player's facing if there is none.
Copying on the click, not watching the neighbour, keeps the wall from rewriting itself when the room changes (the `auto-corners` trap).
It costs the held stair.

**One per cell, not with a deck.**
A deck and a step both fill the open part, so a cell takes one or the other.
The top step of a flight meets the deck of the floor above at the next course, which is the flush stairwell edge the playtest wanted.

## Alternatives considered
- **Host the stair (0035), exempting it from the solid-side rule.** The shift pushes it 4/16 into the next cell, and the solid-side refusal exists because a shifted solid face lies to culling, retention and attachment; the whole 0035 consumer table would need re-answering for a full-cube host.
- **Thin stairs, 12/16 wide.** A new block per material and orientation, and it still leaves a 4/16 gap on the room side unless the next stair is thin too.
- **Detect the stair beside the wall and fill automatically.** Wrong the moment a player wants a gap, and a wall that changes when its neighbour changes.

## Consequences & open questions
- `Deck` and `Step` are both "the open part"; the second one to be built may want them folded into one `Fill` layer with a kind.
- A stair running into the wall (perpendicular), rather than along it, has its back against the panel already; this only covers stairs running along the wall.
- Whether vanilla's stair shapes can be clipped cheaply at mesh time or need a generated step shape in `WallShapeGen`.
- A `cornerout` step, where a flight turns at a corner, is left out until someone builds one.
