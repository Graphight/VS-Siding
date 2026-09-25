# Floors between storeys

- Status: Draft
- Created: 2026-09-25
- Reflects: decisions 0002, 0008, 0035; `SidingWallBlock.PanelCollisionBoxes`/`NeighbourJoins`; `SidingModSystem.IsHostable`; a player report; not yet played

## Summary
A player asked how to floor an upper storey right up to the wall, and reports that multi-storey walls look wrong.
A wall's cell is a 4/16 panel on one face and 12/16 of open air (decision 0002), so an upper floor laid in the room cells stops at the wall's cell and leaves a 12/16 slot running the length of every wall, straight down to the storey below.
The proposal adds a floor deck to the wall itself: a layer that fills the cell's open part at floor height with the held plank or slab material.

## Context
**The slot.**
Take a ground storey of walls at `y=1..3` and an upper floor of planks at `y=4`.
The wall column carries on at `y=4` as another wall block, so the floor can only go in the room cells beside it.
Between the floor's edge and the panel is the wall cell's open 12/16, and nothing in it collides: `PanelCollisionBoxes` (`SidingWallBlock.cs:102-108`) gives the full panel for a filled wall and posts and a top plate for a bare frame (0008), both hugging the panel face.
A player is 0.6 wide, so they fit through a 0.75 slot and drop a storey.
Looking down from above, the slot shows the storey below and the back of the wall below it.

**What we have not seen.**
Nobody has built a two-storey house on the mod; the "jank" is the player's word.
The slot follows from the geometry, but there may be more: 0008's plates alternate by `cellsBelow % 2` (`JoinsAbove`, `SidingWallBlock.cs:147`), so a cross-beam may land at a height that has nothing to do with the floor, and a floor's underside may meet the dead-space lighting of decisions 0018 and 0034.
Stage 1 is a screenshot, before building anything.

**The workaround today.**
A course of full blocks at floor height in the wall column, a sill beam of logs or planks, gives the floor something solid to butt against.
It is what a timber-framed house has there anyway, and it costs nothing, but it breaks the siding's finish for one course and the player has to know to plan for it.
Worth saying in the handbook page (0029) whatever else is decided.

## Design
**A deck layer on the wall.**
A new optional layer, `Deck`, on `SidingWallEntity`, holding a material key and a height: `full`, `top` (upper half) or `bottom` (lower half), matching a plank block and the two slab positions.
It draws one box filling the open 12/16 of the cell at that height, textured from the material the way framing is, and adds that box to `PanelCollisionBoxes`.
A `cornerout` fills its open square the same way.

**Built with the held block.**
Planks or a slab in hand, saw in the off hand, clicking a wall's top face from the room side.
The open question is the gesture, because a room-side click with planks already means "finish the back face" (`ResolveFinishFace`, `SidingWallBlock.cs:319`) and a top-face click frames a new wall above (`PlaceWallFrame.cs:44`).
Candidates: a fourth picker row, `floor`, whose lit option turns the next wall click into a deck; or a sneak-click.
The picker row is the more discoverable and reuses decision 0040 whole.

**Retention.**
A deck is a floor, so it should seal the cell's `UP`/`DOWN` faces for room detection the way the panel seals its own face; a `full` deck seals both, a slab deck one.
Whether vanilla's room walk ever asks a wall cell's vertical faces with the room on the panel's open side needs checking in `RoomRegistry` before building.

**Breaking.**
The deck peels first when struck from the room side, before a back finish (0013's one-layer-per-break order).

## Alternatives considered
- **Guest-hosting a floor block in the wall's cell (0035).** `IsHostable` (`SidingModSystem.cs:651-665`) refuses any block with a solid side, which is every plank and slab, and a guest is drawn shifted off the panel into the open part, one block per cell. A plank block hosted there would overlap the panel, and every consumer patch in 0035's table would have to answer for a full cube it was never built for.
- **Auto-detecting a floor in the room cell and drawing a deck to match.** No gesture to learn, but slab heights and mixed materials make the guess wrong often, and a wall that changes when its neighbour changes is the auto-corner trap the `auto-corners` parked proposal already rejected.
- **A shorter wall variant for storey boundaries.** A new `layout` multiplies block variants (decision 0001) and only helps a player who planned ahead.
- **Documenting the sill-beam workaround and nothing else.** Honest and free, and the proposal keeps it; but the player asked for floor right up to the wall, and a sill beam is not that.

## Consequences & open questions
- The deck is the first layer that is not in the wall's plane; `SidingWallEntity`'s tree attributes, the tooltip (0030) and `GetBlockInfo` all grow a line.
- A deck under a hosted chest (0035) would be where the chest sits; check whether a hosted block and a deck can share the cell or the deck refuses.
- Whether floor carpets and rugs place on a deck's top face (`CanAttachBlockAt`, `SideSolid`).

## Stages
1. **Playtest:** two storeys of filled walls with a plank floor between, both on a straight run and at a `cornerout`; screenshot the slot, the plates and the lighting.
2. **Picker and state:** the `floor` row, the `Deck` layer on the entity, saved and synced.
3. **Geometry and collision:** deck boxes for `wall` and `cornerout` at each height from `WallShapeGen`, added to `PanelCollisionBoxes`.
4. **Retention and breaking:** seal `UP`/`DOWN`, peel order, tooltip line.
5. **Playtest** the same house, then **graduate**.
