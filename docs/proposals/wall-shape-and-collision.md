# Wall shape and collision

- Status: Draft
- Created: 2026-09-13
- Reflects: planning session on `prototype-proposals`; vanilla 1.22.2 `survival/blocktypes/wood/palisadewall.json`; `vsroofing_1.7.2` shipped assets; no code yet

## Summary
A siding wall is one block cell holding a quarter-block-thick slab pressed against one horizontal face of that cell.
Prototype 1 is pure JSON plus an empty block class: one texture, four orientations, a thin collision box, placeable and walkable-against in game.

## Context
The whole point of the mod is a wall thinner than a block.
Before layers, materials, or build flow mean anything, a thin wall has to exist in the world, face the right way, and stop the player at the right place.

Vanilla already ships a block that does almost exactly this: the palisade wall.
It is a 0.25-thick slab against one face, oriented by a `side` variant loaded from `abstract/horizontalorientation`, placed with the `HorizontalOrientable` behavior, with `collisionSelectionBoxes` rotated per side via `rotateYByType`.
No C# involved.
The Roofing mod's `roof.json` uses the same `side` variant axis, and it is its only variant axis (see decision 0001).

So this proposal is mostly "copy the palisade's skeleton, give it our own shape and class".

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
It picks orientation from where the player stands, not which half of the cell they clicked, so you can't yet say "put it against the *far* face".
Good enough to test the shape; revisit once the build flow exists.

**Block class: `vssiding.SidingWallBlock`, registered in `SidingModSystem`, empty for now.**
It exists so later proposals have somewhere to hang collision overrides, interaction, and mesh work without renaming the block code in existing worlds.

**Texture: one hard-coded vanilla texture (oak planks).**
Materials are proposal `wall-layer-state`'s job.

**Side flags: `sidesolid` and `sideopaque` all false, `faceCullMode: NeverCull`** — as the palisade does, so neighbours keep rendering their faces behind a thin wall.

## Alternatives considered
- **The chisel.** Players can already carve thin walls. It doesn't layer materials, it's slow per block, and each chiseled block is voxel data the game has to store and mesh. It's the problem this mod exists to solve, not a solution.
- **Vertical slabs (8/16).** Half a block is still a thick wall; doesn't read as siding.
- **Several thin walls per cell (both faces, or a corner in one cell).** Real need, but it turns one collision box into a combination of boxes driven by block entity state. Deferred until one wall per cell is working.
- **Gridless/entity walls** (Roofing's "gridless" tagline). Roofing is still blocks underneath — one `roof` block per cell. No reason for us to leave the grid.
- **Pick orientation from the clicked hit position.** Nicer placement, but it's C# for a prototype that only needs to prove the shape. Belongs with `in-world-build-flow`.

## Consequences & open questions
- **Does a thin wall seal a room?** This is the one that can quietly sink the mod. With `sidesolid` all false the room scanner will almost certainly walk straight through the cell, so a siding house is not a room — no cellar, no greenhouse, worse sleeping. Now I'm guessing: making the outer face `sidesolid` may be enough, but the scanner might treat the cell as interior air or exterior depending on which neighbour it enters from. Test it in the prototype session: build a closed 3x3 hut of siding and check whether the game recognises it as a room, first with all sides non-solid, then with the back face solid. Vanilla survival source is published and shows what the room registry actually checks (`sidesolid` vs `GetRetention`) — read that rather than guess. Roofing's recipe guide says one roof type "won't insulate (room)", so they hit the same question and answered it per material.
- **Corners leave a gap or overlap.** Two perpendicular walls in neighbouring cells meet at a 0.25x0.25 column that one of them has to own. The palisade solves this with `cornerin`/`cornerout` variants; we might need the same, or an auto-connect later.
- **Floors and ceilings are out of scope** — horizontal orientations only.
- The shape file and collision box must agree on thickness by hand. Fine for one thickness; a smell if thickness ever varies.
