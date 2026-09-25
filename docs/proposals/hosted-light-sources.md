# Hosted light sources

- Status: Draft
- Created: 2026-09-25
- Reflects: decision 0035 (`GuestLightPatches`, `GuestWalls.Absorption`), 0016, 0018, 0034; decompiled 1.22 `VintagestoryLib.Common.ChunkIlluminator`

## Summary
A torch or lantern hosted into a sealed wall's cell (decision 0035) stops lighting the room.
`GuestLightPatches` raises the host's `GetLightAbsorption` to the sealed wall's value, which is 99 (`SidingWallBlock.ComputeLightAbsorption`, `SidingWallBlock.cs:596`) — opaque.
Vanilla's light-spread walk subtracts a cell's own absorption from the light leaving that cell, so 99 kills every step past the source in one shot.
The fix exempts the source's own cell from the raise, only inside vanilla's block-light walk, so the wall stays opaque to daylight.

## Context
`GuestLightPatches.AbsorptionAccessorPostfix`/`AbsorptionChunkPostfix` (`GuestLightPatches.cs:33-46`) run whenever vanilla asks a hostable block's cell for light absorption, and combine the host's own answer with the guest wall's via `Math.Max` — the comment above the class says this exists so a hosted chest doesn't leak light into a sealed room and break decision 0016's corner shading.
That reasoning holds for absorption of *incoming* light (sunlight, or another source's light passing through the cell).
It does not hold for the cell's own emitted light, and vanilla's spread code does not separate the two: one absorption value gates both directions.

Traced in `ChunkIlluminator.CollectLightValuesForLightSource` (`ChunkIlluminator.cs:947-999`, decompiled): the walk seeds the source cell's own record with the block's full `LightHsv[2]` (`b`), unconditionally.
Then, before fanning out to the six neighbours, it computes `num8 = (stored range) - GetLightAbsorptionAt(source cell) - 1`, and only enqueues neighbours `if (num8 > 0)`.
With the source cell's absorption at 99 (a torch's own is 0, but `Math.Max(0, 99) = 99`), `num8` comes out negative on the very first step, so nothing reaches the six neighbouring cells.
The source cell itself still holds full brightness — a hosted torch would still show as lit in its own block if anyone queried that cell directly — but no neighbour, including the room the torch faces, gets any of it.
This matches "casts no light any more": light doesn't fail to spawn, it fails to leave the cell.

`GetLightAbsorptionAt` (`WorldChunk.cs:493-501`) reads exactly the overload `GuestLightPatches` postfixes, and `ChunkIlluminator` is the only spreader (`ChunkIlluminator.cs:198,242,333,382,459,555,594,653,811,916,966` and the two call sites above) — server and client relight (`ServerSystemRelight.cs:95-96`, `ClientSystemRelight.cs:92-93`) both go through the same block method, so the bug is server-authoritative, not a rendering artifact.

The 0018/0034 sealed-cell-light patches are a different mechanism and ruled out: both are `[HarmonyPatch]`es on `ChunkTesselator.BuildExtendedChunkData`/`TCTCache.CalcBlockFaceLight`, client-side mesh-time rgb rewrites that never touch `ChunkIlluminator`, `LightHsv`, or the server's light data at all (0018's summary: "client-side rendering only").
They rewrite what a face's smooth-lighting corners sample when meshing a *sealed wall's own cell*; a hosted light source's cell is not the sealed wall's cell (the wall is a guest record, not a block occupying that position) and is never a target of that postfix's scan.
They cannot be the cause and don't need to change for this fix.

Torches and lanterns pass `SidingModSystem.IsHostable` (`SidingModSystem.cs:651-665`): no solid side, `JSON` draw type, none of the excluded classes.
Either way a torch reaches the wall's cell, it is hosted there: `TryHost` from a panel click, or, with a saw in the off hand, vanilla's `OnBlockBuild` through the transpiled `ClickedIsReplacableBy` on the panel's inner face (decision 0035).
Both leave a guest record under a light-emitting host, which is the case this fixes.

## Design
**Exempt the source cell, only while its own light is walked.**
Every block-light path reaches `CollectLightValuesForLightSource`: placing a light (`PlaceBlockLight`, `ChunkIlluminator.cs:707`), removing one and a full relight (`:162`) all go through `UpdateLightAt` (`:845-852`), which walks each nearby source in turn.
Sunlight never does; it has its own walks (`Sunlight`, `SunlightFlood`, `SpreadSunlightAt`, `:167-630`) that read the same `GetLightAbsorptionAt`.

So a Harmony prefix on `CollectLightValuesForLightSource` records the source position (its first three arguments) in a `[ThreadStatic]` field, and a finalizer clears it.
`AbsorptionChunkPostfix` skips the guest raise when the queried position is that recorded source.
The torch's light leaves its cell at the torch's own absorption, and then meets every other cell at its real value.

Everything else keeps the raise: sunlight flooding through the cell still meets 99, so a sealed room stays dark by day, and another torch's light walking through a hosted cell still stops there, as it would at the wall.
`IsSealed` reads the wall's own absorption, not the host's, so decisions 0016, 0018 and 0034 see no change.

`CollectLightValuesForLightSource` is private, so the patch names it by string, the way `SidingModSystem` already patches `RoomRegistry` (0015); a game update that renames it breaks the fix silently, and decision 0020's sweep should list it.

## Alternatives considered
- **Skip the raise for any host whose `LightHsv` is non-zero.** Two lines and no private-method patch, but `GetLightAbsorption` is one value for both block light and sunlight: a lantern on a sealed wall would drop its cell to absorption 0 and let daylight flood through it into the room all day. It trades one visible bug for another.
- **Per-face handling (raise absorption only on the wall's claimed faces, not the cell as a whole).** `GetLightAbsorption` has no face parameter; vanilla's spread is cell-granular, so there is no per-face value to return.
- **Transpiling the `GetLightAbsorptionAt` call inside the walk.** Same effect as the prefix, but a transpiler breaks on any change to the method's IL; a prefix and a position compare only depend on its signature.

## Consequences & open questions
- The walk is cell-granular, so a hosted torch's light also leaves through the panel to the outdoor cell beside it: a torch on a wall's inside face lights a strip outside, where a torch against a solid wall would not. Check in the playtest whether it is noticeable.
- Only `AbsorptionChunkPostfix` needs the check: the walk calls `WorldChunk.GetLightAbsorptionAt` (`WorldChunk.cs:492-501`), which asks the block's `IWorldChunk` overload.

## Stages
1. **Fix:** the `CollectLightValuesForLightSource` prefix and finalizer recording the source position, and the source-cell check in `AbsorptionChunkPostfix`.
2. **Playtest:** host a torch and a lantern on a sealed wall's inner face; confirm the room lights as it would with the torch free-standing, confirm a hosted chest (no light) still seals decision 0016's corners, confirm a sealed room with a hosted lantern gets no daylight at noon, and look outside for the light strip. Graduate.
