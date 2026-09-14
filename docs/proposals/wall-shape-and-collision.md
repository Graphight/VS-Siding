# Wall shape and collision

- Status: Draft
- Created: 2026-09-13
- Reflects: planning session on `prototype-proposals`; vanilla 1.22.2 `survival/blocktypes/wood/palisadewall.json`; `vsessentialsmod` `Systems/RoomRegistry.cs` and `vsapi` `Block.GetRetention` (GitHub main); `vsroofing_1.7.2` shipped assets; no code yet

## Summary
A siding wall is one block cell holding a quarter-block-thick slab pressed against one horizontal face of that cell.
Prototype 1 is JSON plus a block class with one override: one texture, four orientations, a thin collision box, and a closed hut of walls counts as a room.

## Context
The whole point of the mod is a wall thinner than a block.
Before layers, materials, or build flow mean anything, a thin wall has to exist in the world, face the right way, and stop the player at the right place.

Vanilla already ships a block that does almost exactly this: the palisade wall.
It is a 0.25-thick slab against one face, oriented by a `side` variant loaded from `abstract/horizontalorientation`, placed with the `HorizontalOrientable` behavior, with `collisionSelectionBoxes` rotated per side via `rotateYByType`.
No C# involved.
The Roofing mod's `roof.json` uses the same `side` variant axis, and it is its only variant axis (see decision 0001).

So this proposal is mostly "copy the palisade's skeleton, give it our own shape and class", plus making it count as a room wall — which the palisade deliberately does not.

## Design

**Geometry: a 4/16 slab against the cell's west face, rotated for the other three sides.**
Thickness 0.25 matches the palisade, so we inherit a thickness vanilla already considers walkable and selectable.
The thickness is fixed for the prototype; layers (proposal `layered-wall-mesh`) subdivide it visually, they don't change it.
One fixed thickness means one collision box per side and no per-block-entity collision code.

**Orientation: `side` variant group, four blocks, nothing else as a variant.**
Consistent with decision 0001 — orientation is the one axis that is a real variant.
Shape uses `shapeByType` with `rotateY` per side, collision uses one box with `rotateYByType`, both lifted from the palisade pattern.

**Placement: vanilla `HorizontalOrientable` behavior.**
Wall faces relative to the player, zero code.
The wall hugs the target cell's face nearest the player, so it appears directly in front of them — the placement `in-world-build-flow` keeps.
Check in game which `side` variant that picks, and rotate the shape to match.

**Block class: `vssiding.SidingWallBlock`, registered in `SidingModSystem`, overriding only `GetRetention` for now.**
It exists so later proposals have somewhere to hang collision overrides, interaction, and mesh work without renaming the block code in existing worlds.

**Texture: one hard-coded vanilla texture (oak planks).**
Materials are proposal `wall-layer-state`'s job.

**Side flags: `sidesolid` and `sideopaque` all false, `faceCullMode: NeverCull`** — as the palisade does, so neighbours keep rendering their faces behind a thin wall.

**Rooms: `SidingWallBlock` overrides `GetRetention` to report the slab's face as a wall.**
A siding house must count as a room exactly as if it were built from solid blocks.
Vanilla's room scan (`RoomRegistry.cs` in the public `vsessentialsmod` source) is a flood fill that, for every cell it visits and every one of its six faces, asks two questions:
does *this* block report nonzero `GetRetention` on that face, and does the *neighbour* report nonzero `GetRetention` on the opposite face?
Either one stops the fill there.
The code comment names the case directly: "e.g. chiselled block with solid side".
So rooms are decided per face, not per cell, and a thin wall only needs to claim the face it hugs.

Worked through for a west-hugging wall at `x=0` with the room interior at `x=1`: the fill steps from `x=1` into the wall's cell (its east face reports 0, so it's open), then tries to go west, where the wall's own west face reports nonzero — stopped.
The room includes the wall's cell, and it's sealed.
It works the same way whichever face the player hugs, because the solid face is a plane that the fill can't cross from either side.

The rule: the hugged face returns what vanilla's default returns for a solid side (`1` for wood-like, `-1` for stone/ceramic, which counts as a cooling wall for cellars); the other faces return 0 — except the two short end faces, see corners below.
Using `GetRetention` rather than `sidesolid` keeps the room behaviour without claiming the face is solid for anything else (torch attachment, support for blocks above).

## Alternatives considered
- **The chisel.** Players can already carve thin walls. It doesn't layer materials, it's slow per block, and each chiseled block is voxel data the game has to store and mesh. It's the problem this mod exists to solve, not a solution.
- **Vertical slabs (8/16).** Half a block is still a thick wall; doesn't read as siding.
- **Several thin walls per cell (both faces, or a corner in one cell).** Real need, but it turns one collision box into a combination of boxes driven by block entity state. Deferred until one wall per cell is working.
- **Gridless/entity walls** (Roofing's "gridless" tagline). Roofing is still blocks underneath — one `roof` block per cell. No reason for us to leave the grid.

## Consequences & open questions
- **Corners leak rooms unless the end faces also retain.** Hut with interior `x=1..3, z=1..3`, west walls in column `x=0`, north walls in row `z=0`. The corner cell `(0,0)` holds a west wall. The fill enters `(0,1)` from inside, goes north into `(0,0)` (open end face), then north again out of the hut — `(0,0)`'s north face is open. One leak, `ExitCount` 1, not a room. Proposed fix: the wall's two end faces (north/south for a west wall) also return nonzero retention. Then the fill can't pass along a line of wall cells at all, and the corner seals even if `(0,0)` is left empty. This can't fake a seal across a real gap: a doorway cell's outward face belongs to the doorway cell, not the wall beside it. Traced on paper, not tested — the 3x3 hut is the first in-game check, with the room debug overlay (`RoomRegistry` draws exits red/green).
- **Corners also look wrong.** Two perpendicular walls meet at a 0.25x0.25 column that one of them has to own. The palisade solves this with `cornerin`/`cornerout` variants; we might need the same, or an auto-connect later. Visual problem only once the end faces retain.
- **Room cache refresh.** The registry drops cached rooms on `ChunkDirty`. Placing or breaking a wall dirties the chunk. Changing only block entity state (proposal `wall-layer-state`, e.g. adding infill to a frame) might not — check, and mark the chunk dirty ourselves if not.
- **Retention is the game's real temperature mechanic.** `GetRetention`'s sign already carries the cellar distinction, driven by the infill (see `wall-layer-state`). Its magnitude — how well a wall holds heat — is untouched for now; that's the hook a real insulation mechanic would use later.
- **Floors and ceilings are out of scope** — horizontal orientations only.
- The shape file and collision box must agree on thickness by hand. Fine for one thickness; a smell if thickness ever varies.
