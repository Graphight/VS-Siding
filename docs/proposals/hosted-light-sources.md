# Hosted light sources

- Status: Draft
- Created: 2026-09-25
- Reflects: decision 0035 (`GuestLightPatches`, `GuestWalls.Absorption`), 0016, 0018, 0034; decompiled 1.22 `VintagestoryLib.Common.ChunkIlluminator`

## Summary
A torch or lantern hosted into a sealed wall's cell (decision 0035) stops lighting the room.
`GuestLightPatches` raises the host's `GetLightAbsorption` to the sealed wall's value, which is 99 (`SidingWallBlock.ComputeLightAbsorption`, `SidingWallBlock.cs:596`).
Vanilla's light-spread walk subtracts a cell's own absorption from the light leaving that cell, so no light gets past the source cell.
The fix exempts the source's own cell from the raise, only inside vanilla's block-light walks, so the wall stays opaque to daylight.

## Context
`GuestLightPatches.AbsorptionAccessorPostfix`/`AbsorptionChunkPostfix` (`GuestLightPatches.cs:33-46`) run whenever vanilla asks a hostable block's cell for light absorption, and combine the host's own answer with the guest wall's via `Math.Max`, so a hosted chest cannot leak light into a sealed room or break decision 0016's corner shading.
That reasoning holds for absorption of *incoming* light (sunlight, or another source's light passing through the cell).
It does not hold for the cell's own emitted light, and vanilla's spread code does not separate the two: one absorption value gates both directions.

`ChunkIlluminator.CollectLightValuesForLightSource` (`ChunkIlluminator.cs:926-996`) seeds the source cell with the block's full brightness, then, before fanning out, subtracts that cell's own absorption and enqueues neighbours only if something is left (`:966-970`).
A torch absorbs 0, but the raise makes its cell 99, so nothing is left and no neighbour gets any light.
The torch's own cell stays lit; the light never leaves it.

`ChunkIlluminator` is vanilla's only light spreader, and server and client relight both use it (`ServerSystemRelight.cs:95-96`, `ClientSystemRelight.cs:92-93`), so this is the server's light data, not a rendering artifact.

The 0018/0034 sealed-cell-light patches are ruled out: they rewrite mesh colours on the client (`ChunkTesselator.BuildExtendedChunkData`, `TCTCache.CalcBlockFaceLight`) and never touch `ChunkIlluminator` or the server's light data.

Torches and lanterns pass `SidingModSystem.IsHostable` (`SidingModSystem.cs:651-665`): no solid side, `JSON` draw type, none of the excluded classes.
Either way a torch reaches the wall's cell, it is hosted there: `TryHost` from a panel click, or, with a saw in the off hand, vanilla's `OnBlockBuild` through the transpiled `ClickedIsReplacableBy` on the panel's inner face (decision 0035).
Both leave a guest record under a light-emitting host, which is the case this fixes.

## Design
**Exempt the source cell, only while its own light is walked or taken away.**
Placing a light (`PlaceBlockLight`, `ChunkIlluminator.cs:707`) and a full relight (`:162`) go through `UpdateLightAt` (`:845-852`), which walks each nearby source with `CollectLightValuesForLightSource`.
Removing one does too, but first `RemoveBlockLight` sizes its darkness pass from the source cell's own absorption (`:811`), and `SpreadDarkness` does nothing when that comes out at zero or below (`:863`).
Sunlight never goes through either; it has its own walks (`Sunlight`, `SunlightFlood`, `SpreadSunlightAt`, `:167-630`) that read the same `GetLightAbsorptionAt`.

So Harmony prefixes on `CollectLightValuesForLightSource` and `RemoveBlockLight` record the source position (the first three coordinates of each) in a `[ThreadStatic]` field, and finalizers put back whatever was there before, since `RemoveBlockLight` calls `CollectLightValuesForLightSource` for every other source nearby.
`AbsorptionChunkPostfix` skips the guest raise when the queried position is the recorded source.
The torch's light leaves its cell at the torch's own absorption and meets every other cell at its real value, and when the torch burns out or breaks, the darkness leaves the same way.

Without the `RemoveBlockLight` half, a basic torch burning out (`BlockEntityTransient` swaps the block after 48 in-game hours) keeps its guest, the cell reads 99 again, no darkness spreads, and the room keeps the torch's light for good.
A rule keyed on the host's `LightHsv` would miss this too, since the burnt-out torch that is there at removal time emits nothing.

Everything else keeps the raise: sunlight flooding through the cell still meets 99, so a sealed room stays dark by day, and another torch's light walking through a hosted cell still stops there, as it would at the wall.
`IsSealed` reads the wall's own absorption, not the host's, so decisions 0016, 0018 and 0034 see no change.

`CollectLightValuesForLightSource` is private, so the patch names it by string, the way `SidingModSystem` already patches `RoomRegistry` (0015); a game update that renames it breaks the fix silently, and decision 0020's sweep should list it.

## Alternatives considered
- **Skip the raise for any host whose `LightHsv` is non-zero.** Two lines and no private-method patch, but `GetLightAbsorption` is one value for both block light and sunlight: a lantern on a sealed wall would drop its cell to absorption 0 and let daylight flood through it into the room all day.
- **Per-face handling (raise absorption only on the wall's claimed faces, not the cell as a whole).** `GetLightAbsorption` has no face parameter; vanilla's spread is cell-granular, so there is no per-face value to return.
- **Transpiling the `GetLightAbsorptionAt` call inside the walk.** Same effect as the prefix, but a transpiler breaks on any change to the method's IL; a prefix and a position compare only depend on its signature.

## Consequences & open questions
- The walk is cell-granular, so a hosted torch's light also leaves through the panel to the outdoor cell beside it: a torch on a wall's inside face lights a strip outside, where a torch against a solid wall would not. Check in the playtest whether it is noticeable.
- Only `AbsorptionChunkPostfix` needs the check: both walks call `WorldChunk.GetLightAbsorptionAt` (`WorldChunk.cs:492-501`), which asks the block's `IWorldChunk` overload.

## Stages
1. **Fix:** the `CollectLightValuesForLightSource` and `RemoveBlockLight` prefixes and finalizers recording the source position, and the source-cell check in `AbsorptionChunkPostfix`.
2. **Playtest:** host a torch and a lantern on a sealed wall's inner face; confirm the room lights as it would with the torch free-standing, confirm a hosted chest (no light) still seals decision 0016's corners, confirm a sealed room with a hosted lantern gets no daylight at noon, look outside for the light strip, and check the room goes dark again when a hosted torch burns out and when one is broken. Graduate.
