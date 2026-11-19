# 0101 — A trapdoor hatch over a ladder in a wall's cell

- Status: Accepted
- Created: 2026-11-16
- Reflects: issues #138 and #127; branch `138-trapdoor-hatch`; `SidingModSystem.TrapdoorIntoWallPrefix`, `LadderIntoHatchPrefix`, `HatchClimbablePostfix`, `IsLadderId`, `AssetsFinalize`; `GapShiftCollisionPatches.IsHostedLadder`, `TrapdoorOpenPartPostfix`, `OpenPartSquash`, `RestoreGuestWall`; `SidingWallEntity.Ladder`; `SidingWallBlock.LadderDrop`; `GuestWalls.WithLadder`; `TrapdoorScenarios`; decompiled `BlockBehaviorTrapDoor`, `BEBehaviorTrapDoor`, `BlockBehaviorLadder` (`VSSurvivalMod`) and `EntityBehaviorControlledPhysics` (`VSEssentials`); decisions 0035, 0094, 0100

## Summary
A wooden or metal trapdoor can be placed into a straight wall's cell as furniture, flush at the top of the cell and 12/16 of it wide, so a hatch can sit over a ladder that runs up a wall.
Where the hatch and a ladder share a cell, the ladder is a layer of the wall, and the player still climbs through it.

## Context
A player building a cellar tower out of Siding walls could not put a trapdoor in a wall's cell (#138, split from #127; the rest of #127 shipped in 1.5.0).

Placement fails because `BlockBehaviorTrapDoor.TryPlaceBlock` accepts a cell only where its solid layer is air (`GetBlock(pos, 1).Id == 0`), so the wall's `IsReplacableBy` is never asked, and the player sees "Cannot place this block here".

Placing it is only half of the problem.
`EntityBehaviorControlledPhysics` counts a player as climbing only while a climbable block stands in their own column, at their feet's cell or the one above.
A ladder that stops one cell under the hatch leaves a block of wall between the top rung and the floor.
A ladder and a trapdoor cannot share a cell in vanilla, so the cell needs one block id for the trapdoor and a climbable answer for the ladder.

## Design

**The hatch is the real vanilla trapdoor, hosted in the wall's cell** as furniture is (decision 0035), so its lock, swing, sound and room sealing are vanilla's.
A prefix on `BlockBehaviorTrapDoor.TryPlaceBlock` takes a `SidingWallBlock` target and runs `CanPlaceBlock` and `placeDoor` itself, with a selection whose `Face` is `DOWN` and whose `HitPosition` is kept: the closed hatch lands at the top of the cell, flush with a thin floor or a deck above, and the point aimed at still picks the hinge edge.
Any block with the `TrapDoor` behaviour counts, modded and vanilla metal ones included.
A saw or stone in the off hand still makes a vent (decision 0100); hosting is the path without the build signal.

**The hatch is shrunk to the open part.**
A postfix on `BEBehaviorTrapDoor`'s private `getTfMatrix` multiplies in `OpenPartSquash` when the cell's guest is a straight wall, so the chunk mesh, the animation renderer and the collision and selection boxes all take the 12/16 of the cell the panel leaves open, and nothing overhangs into the next cell.
`UnderDeckDropFor` returns null and `FaceShifts` zeros for a trapdoor, so no hanger or under-deck shift moves it.

**Refusals.**
Only the `wall` layout takes a hatch; `cornerout`, `cornerin` and `diagonal` answer "A hatch needs a straight wall".
A decked or stepped wall answers decision 0094's "Remove the deck first" or "Remove the step first" through `OpenPartRefusal`, since a trapdoor does not count as fitting under a deck.

**The ladder is a layer of the wall in the hatch's cell.**
`SidingWallEntity.Ladder` holds the ladder block's code, whose last part is the wall's `side`, saved in the tree so `GuestWalls` carries it to the client.
The entity draws that block's default mesh shifted off the panel by `PanelOffset`, once per ladder and side, scaled to the 13/16 under the closed hatch's slab so the rails' tops and the hatch's top face do not fight.
A postfix on `Block.IsClimbable`, which only `BlockClutter` overrides, answers true for a trapdoor host whose guest carries a ladder; it is gated on a per-block-id flag array (`Hatches`) so the physics hot path allocates and writes nothing.
That answer is what the climb check needs: the player's feet or head cell holds a climbable block, as vanilla reads it.
`DropsOf` adds the ladder to the drops whenever the guest is dropped, so no path of breaking the wall loses it.
Elsewhere, ladders stay ordinary hosted blocks.

**The cell joins either way round.**
- A trapdoor placed on a hosted ladder over a straight wall's guest (the click is offset, so the clicked cell is `Position - Face`) takes the ladder as the guest's layer and replaces it as host.
- A prefix on `BlockBehaviorLadder.TryPlaceBlock` adds the layer when the clicked cell, or the cell a stack-up from the ladder below would grow into, is a trapdoor host over a ladderless straight wall's guest. It keeps vanilla's sneak rule for a flexible ladder, consumes one ladder, and works for any block with the `Ladder` behaviour (wooden, crude, rope).
Both set the layer through `SetLadderLayer`, on the server only, after a land-claim check.
A client's guess at the layer outlived a placement the server refused in playtest, and drew two ladders in one cell; the server's record now reaches the client a tick after the block, and the ladder is missing from the cell for that tick.

**The server skips its replaceable test for trapdoors and ladders.**
A click on a hosted ladder or a hatch offsets into the cell past the clicked face, and the server tests that cell (`TryModifyBlockInWorld`) though the join places in the clicked cell; at a cellar's floor layer that cell holds a thin floor, and the server refused the hatch.
`AssetsFinalize` sets vanilla's `ignoreServerReplaceableTest` on every block with the `TrapDoor` or `Ladder` behaviour.
Each still checks its own target: vanilla's trapdoor places only into air, and a ladder, stacked or not, through `CanPlaceBlock`.

**A hosted ladder collides as its panel.**
A ladder's box is 3/16 deep, so a climber against a hosted one stood 7/16 off the cell's outer edge, and a player 0.6 wide reached 0.6/16 past the cell, under the next cell's floor, and could not rise through the hatch's 12/16 opening.
`GapShiftCollisionPatches` drops a hosted ladder's own collision boxes and keeps the panel's, which the climb check accepts as the climbable block's boxes; a climber stands against the panel, 4/16 to 13.6/16, and the ladder keeps its selection box.
This holds for every ladder in a wall's cell, under a hatch or not, and a climber's body passes through the ladder's rails.

**Breaking the hatch leaves the ladder.**
`RestoreGuestWall`, the one place a host's leaving restores its guest, makes the ladder block the new host and clears the layer when the guest carries one, on the server and in the client's prediction (#58).
The trapdoor drops, and a second break takes the ladder as an ordinary hosted block.

## Alternatives considered
- **The hatch as a wall layer**: the deck becomes a hatch that opens, drawn by the wall in two states, and the vanilla ladder hosts the cell. Flush too, but it loses vanilla's lock and swing and needs its own open and close.
- **Vanilla's way**: the ladder runs up through the floor-layer wall cell, and the trapdoor lies on top of the floor in the next storey's wall cell, 3/16 proud. The smallest change, but not flush.
- **Only the trapdoor, no ladder layer.** The player is then a block short of the floor, since the climb check reads the column's own cells.
- **The deck kept under the hatch, or dropped.** A decked wall refuses instead, as decision 0094 refuses any block that does not fit under a deck; a ladder cell has no deck anyway.
- **Corners and posts.** Left out: their open part is not one 12/16 by 16/16 rectangle, and the squash is a single scale.

## Consequences & open questions
- The squash reads the guest record when the block entity initialises, so a client that meshes the trapdoor before the guest record arrives draws it full width until the next redraw; not yet looked at in game after a world reload, and no scenario covers the client.
- A hatch in a decked wall needs the deck removed first, so the floor run stops one cell short of the wall at the hatch.
- A cell holding a hatch and a ladder is one guest wall with a ladder layer, so changing the wall's own layers is refused until the hatch is broken, as for any hosted block.
- A trapdoor clicked onto a wall's top face goes into the cell above, not the wall's, since `ClickedIsReplacableBy` sends only an open-side click into the wall's cell; there vanilla places it on the wall's top. Not looked at in game.
- Only a straight wall's guest is climbable; no other host answers `IsClimbable`.
- An opened hatch hinged on the room edge stands 13.75/16 to 16/16 across the cell, leaving a climber against the panel 0.15/16 of room; not looked at in game.
- Placing a block on top of a closed hatch needs a sneak click, since a plain right-click on any trapdoor opens it.
