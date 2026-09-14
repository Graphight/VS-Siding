# Wall shape and collision

- Status: Accepted
- Created: 2026-09-13
- Reflects: `wall-shape-and-collision` branch, commits `cab82d5`..`bbf5825`

## Summary
A siding wall is one block cell holding a quarter-block-thick slab pressed against one horizontal face of that cell.
Prototype 1 is JSON plus a block class with one override: one texture, four orientations, a straight and an outside-corner layout, thin collision boxes, and a closed hut of walls counts as a room.

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
One fixed thickness means collision boxes are static JSON, with no per-block-entity collision code.

**Variants: `layout` (`wall`, `cornerout`) × `side`, eight blocks.**
Decision 0001 keeps *materials* out of variants; shape and orientation are small, fixed axes and the palisade already uses exactly these.
`wall` is one box, `{x1: 0, z1: 0, x2: 0.25, z2: 1}`.
`cornerout` is an L: that box plus `{x1: 0.25, z1: 0, x2: 1, z2: 0.25}`, the palisade's own `cornerout` collision, covering both the west and north faces of the cell.
Its four `side` rotations give the four corners of a building.
Shape uses `shapeByType` with `rotateY` per side, collision uses `rotateYByType`, both lifted from the palisade pattern.
No `cornerin`: an inside corner of walls meets at a single point, which no player fits through, so it's a looks-only notch and can wait.

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

The rule: the hugged face returns what vanilla's default returns for a solid side (`1` for wood-like, `-1` for stone/ceramic, which counts as a cooling wall for cellars); the other faces return 0.
A `cornerout` claims both faces its L covers.
A face is claimed only if the slab physically covers all of it, so the room scan can never call a room sealed that a player can walk out of.
Using `GetRetention` rather than `sidesolid` keeps the room behaviour without claiming the face is solid for anything else (torch attachment, support for blocks above).

## Alternatives considered
- **The chisel.** Players can already carve thin walls. It doesn't layer materials, it's slow per block, and each chiseled block is voxel data the game has to store and mesh. It's the problem this mod exists to solve, not a solution.
- **Vertical slabs (8/16).** Half a block is still a thick wall; doesn't read as siding.
- **Several thin walls per cell (both faces, or a corner in one cell).** Real need, but it turns one collision box into a combination of boxes driven by block entity state. Deferred until one wall per cell is working.
- **Gridless/entity walls** (Roofing's "gridless" tagline). Roofing is still blocks underneath — one `roof` block per cell. No reason for us to leave the grid.
- **Claiming retention on a wall's short end faces instead of a corner piece.** An earlier draft did this to stop the fill running along a line of wall cells. It seals the hut above on paper while the 0.75 slot stays open — the scan says sealed, the player walks out. Retention must never claim a face the slab doesn't fully cover. (Caught in review of PR #2.)

## Consequences & open questions
- **Outside corners need the `cornerout` piece, and that's a real hole, not a looks problem.** Hut with interior `x=1..3, z=1..3`, walls hugging the outer faces: west walls in column `x=0`, north walls in row `z=0` — the layout you get building from outside. Put a plain west wall in the corner cell `(0,0)` and it covers `x 0..0.25`; the strip `x 0.25..1, z 0..0.25` where the north wall would continue is empty. That's a 0.75-wide slot a 0.6-wide player walks through. The room scan agrees: it goes `(0,1)` → `(0,0)` → out through `(0,0)`'s open north face. With a `cornerout` in `(0,0)` both faces are physically closed and both are claimed, so the scan and the collision agree it's sealed.
- **Walls hugging inside faces don't need corner pieces.** The missing 0.25x0.25 square sits outside the room and the two walls meet at a point. Looks notched from outside, seals honestly.
- **Test:** build the outside-hugging 3x3 hut with `cornerout` corners and check the room overlay (`RoomRegistry` draws exits red/green). Then swap one corner for a plain wall and confirm both that the overlay shows an exit *and* that you can walk through the gap. Scan and collision must agree in both directions.
- **Room cache refresh.** The registry drops cached rooms on `ChunkDirty`. Placing or breaking a wall dirties the chunk. Changing only block entity state (proposal `wall-layer-state`, e.g. adding infill to a frame) might not — check, and mark the chunk dirty ourselves if not.
- **Retention is the game's real temperature mechanic.** `GetRetention`'s sign already carries the cellar distinction, driven by the infill (see `wall-layer-state`). Its magnitude — how well a wall holds heat — is untouched for now; that's the hook a real heat-retention mechanic would use later.
- **Floors and ceilings are out of scope** — horizontal orientations only.
- The shape file and collision box must agree on thickness by hand. Fine for one thickness; a smell if thickness ever varies.
