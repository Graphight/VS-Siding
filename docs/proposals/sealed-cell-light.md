# Sealed cell light

- Status: Draft
- Created: 2026-09-19
- Reflects: playtest on branch `sealed-wall-glow` at f87a9d2; vanilla 1.21 `TCTCache.CalcBlockFaceLight`, `ChunkIlluminator.SpreadSunlightAt` (decompiled from `VintagestoryLib.dll`)

## Summary
A sealed siding room still glows along the floor at the wall base and in its corners.
Make a sealed wall's cell hold no light, so the faces that sample it read dark.

## Context
Decision 0016 stopped the walls themselves glowing, but a face whose own light sample is a wall cell still shows the ~23 sunlight that cell stores (decision 0015).
The playtest ruled out stale meshes and panel orientation; the glow is the engine's real light.
Spoilage and room detection already read 0 (decision 0015), so this is cosmetic.

## Design
Two places to patch, both Harmony.

- **Tessellator, client only.** Before a chunk is meshed, overwrite the extended light array (`currentChunkRgbsExt`) at each sealed wall cell with something dark, e.g. the dimmest neighbour. Only rendering changes; the lighting simulation stays vanilla. Cost: finding wall cells in every chunk mesh, so it wants a cheap per-chunk list of wall positions rather than a scan.
- **Lighting engine, both sides.** Stop `SpreadSunlightAt` (and the column passes) storing light in a cell whose absorption is at least the incoming light. Fixes it at the source and would make decision 0015's `RoomRegistry` transpiler unnecessary. Cost: patching a hot loop in several methods, and it changes what vanilla stores for every opaque block, not just ours.

Leaning tessellator: smaller blast radius, and nothing outside rendering reads the wrong value any more.

## Alternatives considered
- **Side AO on the wall.** Shipped in decision 0016; it changes neighbour averaging, not a face's own sample.
- **Redraw after relight.** Ruled out by the 0016 playtest.

## Consequences & open questions
- Which exact faces read a wall cell as their own sample: confirm with `/debug` light readings at a glowing floor square before patching.
- A tessellator patch touches every chunk mesh; measure the cost.
