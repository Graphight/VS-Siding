# Layered wall mesh

- Status: Draft
- Created: 2026-09-13
- Reflects: planning session on `prototype-proposals`; decision 0001; VS 1.22.2 API (`BlockEntity.OnTesselation`, `ITesselatorAPI.TesselateShape`); no code yet

## Summary
One shared wall shape with four named texture slots — `framing`, `insulation`, `front`, `back` — tesselated per block entity with a texture source that maps each slot to that wall's chosen material.
Meshes are cached by `(side, framing, insulation, front, back)`, so identical walls share one mesh.

## Context
Decision 0001 flagged that Roofing swaps *one* material into a shape, and we composite several.
That sounds like several meshes stitched together, but it doesn't have to be.

A VS shape's elements reference textures by code (`#framing`), and the tesselator asks an `ITexPositionSource` "where is texture `framing`?" at tesselation time.
So one shape can carry every layer as separate elements, and the *texture source* does the compositing: same geometry, different answers to "which texture is `#insulation`?".
That's the whole trick.

## Design

**One shape file, `shapes/block/wall.json`, four element groups.**
Within the 0.25 slab from `wall-shape-and-collision`, from the hugged cell face inward: front finish 1/16, cavity 2/16, back finish 1/16.
The cavity holds framing *and* insulation in the same plane, the way a real stud wall does: studs plus top and bottom plates, with the insulation filling the gaps between them.
With no finishes, that cavity *is* the wall's surface on both sides — a half-timbered wall.
Finishes cover it, one face at a time.
Thicknesses are a first guess to be tuned by eye in game; the framing's stud spacing especially decides whether half-timbering reads right.

**`SidingWallEntity.OnTesselation` builds or fetches the mesh and adds it to the chunk mesher.**
Texture source resolves each slot to the `Texture` of that part's dictionary entry.
Unbuilt parts (`null` key) are skipped by passing only built element groups to the tesselator (`selectiveElements`), so a frame-only wall shows just its frame.

**Cache: `ObjectCacheUtil` keyed by the five values.**
A house of one material combination tesselates once per side, not once per block.

**Rotation from `side`**, applied to the tesselated mesh, same angles as the collision box.

## Alternatives considered
- **One shape file per layer, meshes merged.** Works, but each material then needs to agree on geometry across files, and there's no benefit over element groups in one file until layers need genuinely different geometry per material. Switch if that day comes.
- **Per-material shape files as Roofing does (`Shape` in each dictionary entry).** Roofing needs it because straw and slate have different geometry. Our prototype layers are flat; texture is enough. Add `Shape` to an entry when a material needs its own geometry (e.g. lapped weatherboard, or a diagonal-braced half-timber frame).
- **Insulation as a separate plane behind the frame.** Hides the frame from one side, so half-timbering can't be seen from both sides. Real walls put it between the studs.
- **Bake every combination as a static block model.** That's the variant explosion again.

## Consequences & open questions
- Textures must be in the block texture atlas to be addressable at tesselation time. Vanilla textures are; mod textures added by compat patches may need registering at load. Check what happens with a texture nothing else references.
- The inventory/hand-held rendering of a wall has no block entity to read. Until there's a wall item, the held block can just render frame-only.
- Half-timbering often uses diagonal braces and varied patterns between neighbouring cells. One frame layout per framing material won't cover that; a frame-pattern choice (tool mode, or per-material `Shape`) is a likely follow-up.
- `NeverCull` from the shape proposal means some overdraw between neighbouring walls. Probably invisible at house scale; measure if a big build stutters.
