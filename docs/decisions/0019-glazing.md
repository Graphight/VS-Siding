# 0019 — Glazing

- Status: Accepted
- Created: 2026-09-20
- Reflects: proposal `window-frames`, graduated from PR #24 at 0aa6f98; decompiled `VSSurvivalMod.dll` and `VintagestoryAPI.dll` 1.21 (`BlockBehaviorDoor.GetRetention`, `Block.GetLiquidBarrierHeightOnSide`, `BlockEntityMicroBlock`, `MeshData.AddMeshData`, `ShapeElement.RenderPass`); four playtests across that PR

## Summary
Glass is an infill material. A glazed wall seals a room and dams water like any other fill, but passes light.
Adjacent glazed cells merge in every direction, dropping the members between them, so a run of glass is one sheet framed only round its outside.
There is no `window` block layout; merging and refusing finishes belong to glass, not to a shape.

## Context
The backlog bundled windows and doors as extra frame tool modes.

**Doors are not ours.** `BlockBehaviorDoor.GetRetention` reports retention itself, gated on the door's own `airtight` attribute, and whether an *open* door seals is the world setting `openDoorsNotSolid`.
A closed vanilla door panel sits in the outer 2/16 of its cell, inside the 4/16 our wall occupies, so a doorway is just two empty cells, a wall above, and a vanilla door.
Overriding that from a wall mod would fight a setting players set deliberately.

**Windows are ours.** Vanilla glass panes seal, but sit mid-cell rather than against a face, so they never line up with a siding wall.

**The first draft added a `window` layout** — four block variants, a shape file with a sill and head rail, a third tool mode.
Playtesting deleted it, and that is the substance of this decision; see *Design*.

## Design

**`Transparent` on an infill entry splits "sealed" from "opaque".**
Decision 0015 defined `ComputeLightAbsorption` as `ComputeRetention(...) != 0 ? 99 : 0`; decision 0016 hung side AO on the same test, and decision 0018 hung both its rendering patches on `IsSealed`, which reads that absorption.
Glazing is the first thing that must seal *and* pass light, so the knot came apart: a sealed cell whose infill sets `Transparent` absorbs 0.
Four behaviours then fall out correctly with no further work, because all four ask `GetLightAbsorption` — the room skylight patch counts a window column as sky (as vanilla does for panes), side AO stops shading neighbours, and both 0018 patches skip the cell so it keeps its real daylight and smooth shading.
Retention is untouched, so the room still seals.

**Walls were never watertight, and it was not a glass bug.**
`Block.GetLiquidBarrierHeightOnSide` defaults to `SideSolid`, which decision 0002 turned off on every face so a thin wall does not cull its neighbours.
Every siding wall had therefore reported a barrier of 0 since the first release — wattle and stone as much as glass.
`SidingWallBlock` overrides it and asks exactly what `GetRetention` asks: what seals air seals water, glazing included.
Changing infill is block-entity state rather than a block change, so `OnInfillChanged` also calls `TriggerNeighbourBlockUpdate`; without it a wall only starts damming when something unrelated nearby forces a recalculation.
That this was found by a player watching water pour through a finished wall, three years of `sidesolid` bugs after the first, is why `sidesolid-derived-behaviour` exists.

**Render pass is set on the mesh, and the two halves are not merged.**
`ShapeElement.RenderPass` defaults to -1 and `TesselateShape` already writes one entry per quad, so only the glazing half is restamped to `Transparent`.
A per-element `renderPass` in the shape JSON was rejected: it forces a parallel `-glass` element for every infill element in every shape.
The two meshes go to `ITerrainMeshPool.AddMeshData` separately rather than being merged, because `MeshData.AddMeshData` offsets incoming indices by `Indices[IndicesCount - 1] + 1`, which only equals the target's vertex count when that last index is also its highest.
Vanilla's `BlockEntityMicroBlock` mixes passes within one mesh the same way, which is what confirmed the approach.

**No `window` layout: merging belongs to glass.**
A player reached for glass as an infill, got a window, and never noticed the dedicated tool mode existed — a wall's own posts and plates already frame each pane.
More importantly, merging and refusing finishes are properties of *glass*: a glazed wall should merge with the glazed wall beside it and should not take a plank slab over it, whichever layout it sits in.
So `NeighbourJoins` merges in all four directions when the infill is transparent, with no alternating cross-beam; opaque fill keeps decision 0008 exactly.
`ContinuesGlazing` requires the neighbour to be glazed too, so a pane against a wattle-filled cell keeps its post — that is a junction between two walls, not one sheet.
A `cornerout` keeps its three structural posts and merges only vertically.
"Left" along a run is `CorneroutSecondFace[side]`, the `z = 0` end of the unrotated shape, pinned by a test against the rotation rather than a comment.

**Geometry: one flat pane inside a bezel, both spanning the full cell.**
Glazing draws as a single zero-thickness full-cell pane rather than a slab plus fillers: three stacked boxes share a face at each seam, and two coincident transparent quads blend twice into a bright line exactly where a cross-beam would sit.
A full-cell pane also meets the pane above edge-on, so a glazed stack has no seams.
The bezel's four members each span their full cell edge, so the frame still reaches the pane where the member beside it was dropped; a plain wall's plates stop short at its posts, which is right while the posts are always drawn and leaves a notch once they are not.
Stiles keep full depth and height, rails are inset in x and y, so overlapping members share no plane — overlap instead of mitred corners, which would need a piece per corner.

**Collision needs nothing.** Merging requires transparent infill, and any infill already switches collision to the full slab, so the merge flags can never reach `FramingBoxes`.

## Alternatives considered
- **A `window` layout with its own sill and head rail.** Built, playtested, deleted; see above.
- **Glass as a finish.** A finish is cosmetic and never seals (decision 0003); glazing is exactly what should decide whether an opening is sealed.
- **Glazing as a fifth entity field.** The infill slot already means "what fills the cavity".
- **Deriving `Transparent` from `BlockMaterial == "Glass"`.** Cleverness at the JSON's expense; the next transparent material need not be glass.
- **A `liquidBarrierOnSides` JSON attribute instead of the override.** Static, and whether a wall dams depends on whether it is filled — block-entity state.
- **Keeping decision 0008's alternating cross-beam in a glazed stack.** Bands a tall glass wall and defeats the point.
- **A nine-patch infill.** Needed only if the pane is cell-sized and the fillers must avoid overlapping each other; a full-cell pane behind the frame collapses all nine to one.
- **Door frame tool mode, or making gates and open doors not seal.** Vanilla already decides both, per door type and per world.

## Consequences & open questions
- Interior corners still need a `cornerout`: two plain walls leave a 0.75-wide slot at the corner cell. `upgrade-frame-to-corner` covers making that swap in place.
- A glazed cell is deliberately outside both of decision 0018's patches, so a sealed wall beside a window borders a bright cell again. Night lighting around glazing looked slightly off in play and was left alone.
- Decision 0018 notes a sealed cell whose open side is a doorway can show a bright sliver; a merged run of unglazed frames is a large doorway and may show the same.
- Glazing merging with a differently-coloured pane is allowed and untested in play.
- `ContinuesGlazing`'s block-level half (same layout, same side, via a block accessor) is still untested; only the material rule it composes with is covered.
