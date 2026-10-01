# 0057 — Hosted light sources

- Status: Accepted
- Created: 2026-09-25
- Reflects: branch `hosted-light-sources`; `GuestLightPatches.SourcePrefix`/`SourceFinalizer`/`AbsorptionChunkPostfix`, `GuestWalls.Absorption`; `LightScenarios`; decisions 0016, 0018, 0020, 0034, 0035; a self-review; vanilla 1.22 `ChunkIlluminator`, `ServerSystemRelight`, `WorldChunk` (decompiled); played on 2026-10-01

## Summary
A torch or lantern hosted into a sealed wall's cell (decision 0035) lit nothing past its own cell.
`GuestLightPatches` raises the host's `GetLightAbsorption` to the sealed wall's value, which is 99 (`SidingWallBlock.ComputeLightAbsorption`).
Vanilla's light-spread walk subtracts a cell's own absorption from the light leaving that cell, so no light got past the source cell.
The source's own cell is now exempt from the raise, only inside vanilla's block-light walks, so the wall stays opaque to daylight.

## Context
`GuestLightPatches.AbsorptionAccessorPostfix`/`AbsorptionChunkPostfix` run whenever vanilla asks a hostable block's cell for light absorption, and combine the host's own answer with the guest wall's via `Math.Max`, so a hosted chest cannot leak light into a sealed room or break decision 0016's corner shading.
That reasoning holds for absorption of *incoming* light (sunlight, or another source's light passing through the cell).
It does not hold for the cell's own emitted light, and vanilla's spread code does not separate the two: one absorption value gates both directions.

`ChunkIlluminator.CollectLightValuesForLightSource` seeds the source cell with the block's full brightness, then, before fanning out, subtracts that cell's own absorption and enqueues neighbours only if something is left.
A torch absorbs 0, but the raise made its cell 99, so nothing was left and no neighbour got any light.

`ChunkIlluminator` is vanilla's only light spreader, and server and client relight both use it (`ServerSystemRelight`, `ClientSystemRelight`), so this was the server's light data, not a rendering artifact.
The 0018/0034 sealed-cell-light patches rewrite mesh colours on the client and never touch `ChunkIlluminator`, so they could not fix it.

## Design
**Exempt the source cell, only while its own light is walked or taken away.**
Placing a light (`PlaceBlockLight`) and a full relight go through `UpdateLightAt`, which walks each nearby source with `CollectLightValuesForLightSource`.
Removing one does too, but first `RemoveBlockLight` sizes its darkness pass from the source cell's own absorption, and `SpreadDarkness` does nothing when that comes out at zero or below.
Sunlight never goes through either; it has its own walks (`Sunlight`, `SunlightFlood`, `SpreadSunlightAt`) that read the same `GetLightAbsorptionAt`.

So Harmony prefixes on `CollectLightValuesForLightSource` and `RemoveBlockLight` record the source position (`posX`/`posY`/`posZ`, by name) in a `[ThreadStatic]` field, and finalizers put back whatever was there before, since `RemoveBlockLight` calls `CollectLightValuesForLightSource` for every other source nearby.
`AbsorptionChunkPostfix` skips the guest raise when the queried position is the recorded source.
Only that postfix needs the check: both walks call `WorldChunk.GetLightAbsorptionAt`, which asks the block's `IWorldChunk` overload.

Without the `RemoveBlockLight` half, a basic torch burning out (`BlockEntityTransient` swaps the block after 48 in-game hours) keeps its guest, the cell reads 99 again, no darkness spreads, and the room keeps the torch's light for good.

Breaking a hosted torch takes a different path.
The guest wall is restored as the wall block itself, whose own absorption is 99 and which is not a hostable, so the exemption never reaches it.
The air-to-wall change that follows goes through `ServerSystemRelight`'s `UpdateBlockLight(0, 99)`, which spreads darkness from the cell's leftover block light, so the room goes dark anyway; `LightScenarios` asserts it.

Everything else keeps the raise: sunlight flooding through the cell still meets 99, so a sealed room stays dark by day, and another torch's light walking through a hosted cell still stops there, as it would at the wall.
`IsSealed` reads the wall's own absorption, not the host's, so decisions 0016, 0018 and 0034 see no change.

`CollectLightValuesForLightSource` is private, so the patch names it by string, the way `SidingModSystem` already patches `RoomRegistry` (0015).
`RoomSkylightPatchTests.LightSourceMethodsStillTakePos` fails `just test` if either method drops those parameter names, and decision 0020's sweep on a game update should cover both methods.

## Alternatives considered
- **Skip the raise for any host whose `LightHsv` is non-zero.** Two lines and no private-method patch, but `GetLightAbsorption` is one value for both block light and sunlight: a lantern on a sealed wall drops its cell to absorption 0 and lets daylight flood through it into the room. Tried against `LightScenarios`: sunlight in the sealed room reads 20 instead of 0. A rule keyed on `LightHsv` would also miss a burnt-out torch, which emits nothing at removal time.
- **Per-face handling (raise absorption only on the wall's claimed faces, not the cell as a whole).** `GetLightAbsorption` has no face parameter; vanilla's spread is cell-granular, so there is no per-face value to return.
- **Transpiling the `GetLightAbsorptionAt` call inside the walk.** Same effect as the prefix, but a transpiler breaks on any change to the method's IL; a prefix and a position compare only depend on its signature.

## Consequences & open questions
- The walk is cell-granular and hands one value to all six neighbours, so a hosted light reaches the outdoor cell beside the wall at the same level as the room-side cell and falls off identically from there: a full pool of light outside, not a strip. `LightScenarios` asserts the two levels are equal. The 2026-10-01 playtest passed with it in place.
- A lantern only places through a click against something it can attach to (`OmniAttachable`, failure code `requireattachable`), and the empty room behind a wall's open side is not that, so `LightScenarios` puts its lantern in with `SetBlock`.
