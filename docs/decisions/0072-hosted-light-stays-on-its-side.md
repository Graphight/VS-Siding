# 0072 — A hosted light stays on its side of the wall

- Status: Accepted
- Created: 2026-10-12
- Reflects: branch `hosted-light-and-wattle`; issue #93 and mod page comment https://mods.vintagestory.at/vssiding#cmt-243541; `GuestLightPatches.CollectPrefix`/`NeighbourSlot`, `SidingWallBlock.ClaimsFace`, `GuestWalls.Absorption`; `RoomSkylightPatchTests.LightSourceWalkKeepsTheFieldsTheHostedLightMarkWrites` and `NeighbourSlotsAreTheFourSidesAroundTheGridCentre`; `LightScenarios.HostLitTorch`; decisions 0035, 0057; vanilla 1.22 `ChunkIlluminator`, `ServerSystemRelight`, `ClientSystemRelight`, `WorldChunk` (decompiled); not yet played

## Summary
A torch or lantern hosted in a sealed wall's cell lights the cell on its open side and no longer lights the cell beyond the wall's panel.
This supersedes the first bullet of decision 0057's "Consequences & open questions", which accepted a full pool of light outside.

## Context
Decision 0057 exempted the hosted source's own cell from the sealed wall's absorption so the light could leave it.
The light-spread walk hands one value to all six neighbours, so the outdoor cell received the room side's level, and 0057 accepted that.
Issue #93 and the mod page comment asked for it to stop.

A wall's panel hugs the face named by `Variant["side"]` (`SidingWallBlock.ClaimsFace`), and its dead space opens the other way.
A hosted block sits in the dead space, so its light belongs on the open side and stops at the cell beyond each claimed face.
A `cornerout` claims two faces, so it stops the light at both.

## Design
`ChunkIlluminator.CollectLightValuesForLightSource` records the source's light on each of six neighbours, and skips a neighbour whose slot in the private `currentVisited` array already holds this walk's number.
The number is `++iteration`, taken after the method's early `block == null` return.

`GuestLightPatches.CollectPrefix` replaces the plain source prefix on that method.
It records the source as before.
When the source cell holds a guest wall with absorption above zero (`GuestWalls.Absorption`), it writes `iteration + 1` into the `currentVisited` slot of the neighbour behind each horizontal face the guest claims.
The walk then skips that neighbour: it is neither lit nor enqueued.
`NeighbourSlot` gives the slot for an offset from the source, `125023 + dx + dz * 63 + dy * 3969`.

`RemoveBlockLight` keeps the plain prefix and marks nothing, so the darkness pass still clears that cell.

A guest wall with absorption 0 is not marked, so a light hosted in an open frame, glazing or bare wattle (decision 0074) reaches both sides.

`RoomSkylightPatchTests` pins the three private fields the prefix reads and the slot arithmetic.
Decision 0020's sweep on a game update should cover them.

**The fixture was turned round.**
`LightScenarios.HostLitTorch` used to build its wall with the hosted torch on the outdoor side and name the cell behind the panel `inside`.
The old behaviour lit both sides equally, which hid the mistake.
The wall is now raised towards the outdoor cell and the torch is clicked into the room-side face.
`WallBuilder.BuildRoom` still puts its panels against the room, so a block hosted in one of those walls opens outdoors, and `HostedLantern_Should_LightASealedRoomButLetNoSunlightIn` raises its wall again from the inside for the same reason.

## Alternatives considered
- **A transpiler in the neighbour loop.** It would test each neighbour against the claimed faces and give exact behaviour, but it breaks on any change to the method's IL. A prefix writing a private array depends on a field name and a slot formula, which tests pin.
- **Undoing the mark partway through the walk.** There is no per-cell hook for it: `WorldChunk.GetLightAbsorptionAt` calls no block virtual for an air cell, so nothing runs when the walk reaches the cell beyond the wall.
- **Marking in `RemoveBlockLight` too.** The darkness pass has to clear the cell behind the wall, so it is left unmarked.

## Consequences & open questions
- The mark lasts the whole walk. The one cell directly behind a hosted light gets none of that light, even where it could wrap round a free-standing wall's end. Exact behaviour needs the transpiler above, which is not built.
- A light hosted in a wall with absorption above zero no longer lights the ground outside it, so the known issue on the mod page is removed.
- Not yet played.
