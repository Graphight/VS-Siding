# Layered wall mesh

- Status: Accepted
- Created: 2026-09-13
- Reflects: `layered-wall-mesh` branch, commits `88aa297`..`aaf7609`

## Summary
One shared wall shape with four named texture slots — `framing`, `infill`, `front`, `back` — tesselated per block entity with a texture source that maps each slot to that wall's chosen material.
Meshes are cached by `(side, framing, infill, front, back)`, so identical walls share one mesh.

## Context
Decision 0001 flagged that Roofing swaps *one* material into a shape, and we composite several.
That sounds like several meshes stitched together, but it doesn't have to be.

A VS shape's elements reference textures by code (`#framing`), and the tesselator asks an `ITexPositionSource` "where is texture `framing`?" at tesselation time.
So one shape can carry every layer as separate elements, and the *texture source* does the compositing: same geometry, different answers to "which texture is `#infill`?".
That's the whole trick.

## Design

**One shape file, `shapes/block/wall/wall.json`, four element groups.**
Within the 0.25 slab from `wall-shape-and-collision`, from the hugged cell face inward: front finish 1/16, cavity 2/16, back finish 1/16.
The cavity holds framing *and* infill in the same plane, the way a real stud wall does: bottom plate, top plate, and two corner posts (the `framing` group, four elements), with one infill panel filling the gap between them.
With no finishes, that cavity *is* the wall's surface on both sides — a half-timbered wall.
Finishes cover it, one face at a time.
A center stud splitting infill into two panels is a natural follow-up once varied framing patterns matter; not needed for the prototype.

**`SidingWallEntity.OnTesselation` builds or fetches the mesh and adds it to the chunk mesher.**
Texture source (`SidingWallTexSource`) resolves each slot to the `Texture` of that part's dictionary entry, read off `Block.Attributes["Framings"/"Infills"/"Finishes"]`.
Unbuilt parts (`null` key) are skipped by passing only built element-group names to the tesselator (`selectiveElements`), so a frame-only wall shows just its frame.
Only the `wall` layout gets this treatment — `cornerout` has no four-layer shape yet, so its entity's `OnTesselation` returns `false` and it keeps rendering through the block's default JSON shape, untouched.

**Cache: `ObjectCacheUtil` keyed by `(side, Framing, Infill, Front, Back)`.**
A house of one material combination tesselates once per side, not once per block.

**Rotation from `side`, passed straight into `TesselateShape`'s `meshRotationDeg`.**
Same 0/90/180/270 values as `collisionSelectionBoxesbytype`'s `rotateYByType` — no separate rotation call needed once the tesselator does it at build time.

## Alternatives considered
- **One shape file per layer, meshes merged.** Works, but each material then needs to agree on geometry across files, and there's no benefit over element groups in one file until layers need genuinely different geometry per material. Switch if that day comes.
- **Per-material shape files as Roofing does (`Shape` in each dictionary entry).** Roofing needs it because straw and slate have different geometry. Our prototype layers are flat; texture is enough. Add `Shape` to an entry when a material needs its own geometry. The finish catalogue in decision 0003 already has two: lapped weatherboards (planks) and shakes (logs). So the face element groups will need an optional per-finish shape override soon after the prototype — just not for the texture-only prototype finishes.
- **Infill as a separate plane behind the frame.** Hides the frame from one side, so half-timbering can't be seen from both sides. Real walls put it between the studs.
- **Bake every combination as a static block model.** That's the variant explosion again.

## Consequences & open questions
- Textures must be in the block texture atlas to be addressable at tesselation time. Every material's `Texture` is a vanilla `game:` path already in the atlas, so this didn't block the prototype; a mod texture added by a compat patch may need explicit atlas registration at load. `SidingWallTexSource` falls back to `ITextureAtlasAPI.UnknownTexturePosition` on an atlas miss, so an unregistered texture shows as a placeholder rather than crashing tesselation.
- `cornerout` needs its own L-shaped version of `wall.json`, with the layers mitred or overlapped at the corner post. Same slot names, so the texture source doesn't change. Until then its entity opts out of custom tesselation entirely (`OnTesselation` returns `false` for that layout) and it keeps the single-texture shape from decision 0002.
- The inventory/hand-held rendering of a wall has no block entity to read. The shape's own inline `textures` dict gives each of the four slots a default (oak planks), so the default JSON render used for the held/inventory view shows a plausible four-layer wall rather than a missing-texture placeholder — not literally "frame only" as first floated, but the same spirit: a fixed stand-in until there's a wall item.
- Half-timbering often uses diagonal braces and varied patterns between neighbouring cells. One frame layout per framing material won't cover that; a frame-pattern choice (tool mode, or per-material `Shape`) is a likely follow-up.
- `NeverCull` from the shape proposal means some overdraw between neighbouring walls. Probably invisible at house scale; measure if a big build stutters.
- Nothing sets `Framing`/`Infill`/`Front`/`Back` on a placed wall yet (`in-world-build-flow` isn't written), so every wall in the world today has all four keys `null`. An empty `selectiveElements` array matches zero shape elements rather than "no filter", so `OnTesselation` returns `false` in that case and the wall keeps rendering through the block's default JSON shape instead of going invisible. This decision's layered mesh itself can't be eyeballed in game until that proposal lands or a debug setter exists.
