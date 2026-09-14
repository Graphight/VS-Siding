# Layered wall mesh

- Status: Draft
- Created: 2026-09-13
- Reflects: planning session on `prototype-proposals`; decision 0001; VS 1.22.2 API (`BlockEntity.OnTesselation`, `ITesselatorAPI.TesselateShape`); no code yet

## Summary
One shared wall shape with three named texture slots — `framing`, `insulation`, `exterior` — tesselated per block entity with a texture source that maps each slot to that wall's chosen material.
Meshes are cached by `(side, framing, insulation, exterior)`, so identical walls share one mesh.

## Context
Decision 0001 flagged that Roofing swaps *one* material into a shape, and we composite *three*.
That sounds like three meshes stitched together, but it doesn't have to be.

A VS shape's elements reference textures by code (`#framing`), and the tesselator asks an `ITexPositionSource` "where is texture `framing`?" at tesselation time.
So one shape can carry all three layers as separate elements, and the *texture source* does the compositing: same geometry, different answers to "which texture is `#insulation`?".
That's the whole trick.

## Design

**One shape file, `shapes/block/wall.json`, three element groups.**
Within the 0.25 slab from `wall-shape-and-collision`, from the cell's face inward: exterior 1/16, cavity 2/16, interior face 1/16.
The cavity holds framing *and* insulation in the same plane, the way a real stud wall does: studs plus top and bottom plates, with the insulation filling the gaps between them.
A frame-only wall reads as a frame; an insulated wall seen from inside looks like an unfinished wall, studs and fill showing.
The interior face is left empty for now — reserved for a later interior finish layer (plaster, boards, wallpaper) so the inside can be styled without touching the insulation that decides the wall's function.
Thicknesses are a first guess to be tuned by eye in game.

**`SidingWallEntity.OnTesselation` builds or fetches the mesh and adds it to the chunk mesher.**
Texture source resolves `framing`/`insulation`/`exterior` to the `Texture` of that layer's dictionary entry.
Unbuilt layers (`null` key) are skipped by passing only built element groups to the tesselator (`selectiveElements`), so a half-built wall shows just its frame.

**Cache: `ObjectCacheUtil` keyed by the four values.**
A house of one material combination tesselates once per side, not once per block.

**Rotation from `side`**, applied to the tesselated mesh, same angles as the collision box.

## Alternatives considered
- **Three shape files, one per layer, meshes merged.** Works, but each material then needs to agree on geometry across three files, and there's no benefit over element groups in one file until layers need genuinely different geometry per material. Switch if that day comes.
- **Per-material shape files as Roofing does (`Shape` in each dictionary entry).** Roofing needs it because straw and slate have different geometry. Our prototype layers are flat boards; texture is enough. Add `Shape` to an entry when a material needs its own geometry (e.g. lapped weatherboard).
- **Bake every combination as a static block model.** That's the variant explosion again.

## Consequences & open questions
- Textures must be in the block texture atlas to be addressable at tesselation time. Vanilla textures are; mod textures added by compat patches may need registering at load. Check what happens with a texture nothing else references.
- The inventory/hand-held rendering of a wall has no block entity to read. Until there's a wall item, the held block can just render frame-only.
- Does each layer's face need to be culled against its neighbour wall (two walls side by side)? `NeverCull` from the shape proposal means some overdraw. Probably invisible cost at house scale; measure if a big build stutters.
