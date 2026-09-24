# 0035 — Guest wall

- Status: Accepted
- Created: 2026-09-20
- Reflects: branch `furniture-against-thin-walls`; commits `8182fa1`, `62c09b0`, `59b54eb`, `7ee7fe3`, `d2b1a99`, `9984cb1`, `9aa8c94`, `42904b1`, `15ec276`, `0f00189`, `ca24d39`, `075a30c`, the twelve commits since the snap-flush prototype; vanilla 1.22 decompiled source

## Summary
Furniture placed into a wall's dead space becomes the real block in that cell.
The wall's state moves to a guest record at the same position, and patches keep it sealing, rendering, colliding and holding things up.

## Context
A wall is a quarter-block panel on one face of its cell (decision 0002); vanilla allows one block per cell, so furniture against the open side stood in the next cell, three quarters off the panel.
The snap-flush prototype drew furniture onto the panel from the next cell, and play rejected it: that cell stayed owned, with collision and an outline where nothing is drawn.

## Design
**The inversion.** Placing a hostable block into the gap writes the wall's state to a guest record keyed by position, then swaps the wall block for the furniture, which stays its ordinary vanilla self.
Vanilla then finds what it expects at that position, and everything the wall used to answer is re-answered from the guest record.

**Storage is chunk mod data, and no spike was needed.** `GuestWalls` keys a `[ProtoContract]` `GuestRecord` (the wall's block code plus its `SidingWallEntity` tree bytes) by chunk-local index3d in `LiveModData["vssiding:guests"]`, loaded lazily from `ModData` exactly as `ModSystemSupportBeamPlacer`'s support-beam data is.
`ServerChunk` already flushes `LiveModData` into `ModData` on save and on the client chunk packet, so initial sync is free; a live edit rides its own network channel.
`GuestAt(chunk or accessor, pos)` builds a transient `SidingWallEntity` (`Block`, `Pos`, `Api` set) so the existing `Compute*`/`OnTesselation` code runs unchanged.
A decor would still need mod data for the material keys (decision 0001) and vanilla breaks every decor on a host change, so it bought nothing the proposal's spike was meant to find.
Readers never write `LiveModData`, because they run on the lighting, tesselation and physics threads; every lookup takes the caller's own side-correct `api`, because singleplayer runs client and server in one process and a wrong side's accessor would silently read the other's state.

**Placement never goes through air, and any placement hosts.** `SidingWallBlock.IsReplacableBy` answers yes to every hostable block, so `Block.CanPlaceBlock`'s own check turns placement into a single `SetBlock` from wall to host.
`HostChangePrefix` sees a hostable block replacing a wall while the wall's entity is still in place, and writes that state as the guest in the same call.
So a right-click on the panel's inner face (`TryHost`, which places into the wall's cell where vanilla would use the cell in front), a click on the floor in the gap, and a sneak-placement all host alike.
Vanilla also asks the *clicked* block `IsReplacableBy` in the client's `OnBlockBuild`, to decide whether a click places into that cell or past its face; there a wall answers no (a transpiled `ClickedIsReplacableBy`), so a torch on a wall's outer face or a chest on its top still lands beyond it.
Pots are `Unplaceable` blocks that go down as ground storage, whose placement insists on `Replaceable >= 6000`; a prefix on `CollectibleBehaviorGroundStorable.Interact` sends a wall's cell through `BlockGroundStorage.CreateStorage` instead, and the same prefix hosts it.

**Host changes.** A prefix on `WorldChunk.BreakAllDecorFast` — called on every solid-block `SetBlock`, with the new id already written into `chunk.Data[index3d]` — classifies the change: air or replaceable restores the wall a tick later (state re-read via `FromTreeAttributes`, re-checked in case something else changed the cell first), another hostable block keeps the guest, and anything else drops the wall's layers as items and removes the guest.
`ExchangeBlock` bypasses `BreakAllDecorFast` entirely, so a firepit's lit/unlit exchange and a torch's burnout keep their guest for free, as the proposal expected.
A player's break restores the wall straight away, in a prefix on the server's `TriggerNeighbourBlocksUpdate`, which runs once the break is done and before any neighbour is told; the deferred callback is left for every other way a host goes.
Otherwise the cell sits as air for a tick: the wall flashes out on the client, and a torch on the wall's far side drops before the wall comes back.
For those other paths, `CanAttachBlockAt` also answers for air (or clutter) still holding a guest record.
Blocks the wall can replace (tall grass, loose stones, snow layer) are not hostable, since the restore would take them over on the next tick, and neither are `Unplaceable` ones.

**Rendering** goes through the JSON tesselator's own mesh-pool helper.
A postfix on `ChunkTesselator.TesselateBlock` runs after the host's mesh, points `vars` at the guest wall at the cell's unshifted position, and calls the transient entity's `OnTesselation`.
It restores `vars` afterwards, so the next block starts clean.

**Hosted blocks sit off the panel.** The snap-flush prototype's offset toolkit was retargeted rather than rebuilt: a hosted block shifts away from each claimed face by the panel thickness (4/16) less its own inset, read from its default selection boxes and cached per block id and face; a cornerout guest shifts on both claimed axes.

**Consumers**, each patched on every declaring `Block` subclass override through `EveryOverridePatches` (or an edit to our own patches), binding `__args` so a mod's differently named parameters don't refuse the patch, and reading the `Hostable` table before any chunk lookup wherever the asked block is the host:

| Consumer | Guest wall's answer |
| --- | --- |
| `GetPlacedBlockInfo` (tooltip, `GuestTooltipPatches`) | host's, plus one line naming the guest wall (`SidingWallEntity.GetBlockInfo`) |
| `GetRetention`, `GetLiquidBarrierHeightOnSide`, `CanAttachBlockAt` (`GuestSealingPatches`) | the wall's, on its claimed faces; elsewhere the host's. `CanAttachBlockAt` also answers across the panel, so a hosted torch or sign hangs on it |
| `GetLightAbsorption` (both overloads), `DoEmitSideAo`/`DoEmitSideAoByFlag` (`GuestLightPatches`) | at least the sealed wall's, plus the guest's side AO |
| `RoomSunlight` (0015), sealed-cell light (0018/0034), rain-fall distance (0034), `NeighbourJoins` | all go through a `WallAt` lookup that answers the real wall or the guest, so stacked walls and skylight/wind still work across a guest |
| `GetCollisionBoxes`, `GetParticleCollisionBoxes` (`GapShiftCollisionPatches`) | host's (shifted) plus the panel's framing boxes (decision 0008) |
| `GetSelectionBoxes` (`GapShiftCollisionPatches`) | host's (shifted) plus the panel's, tagged with a `PanelSelectionBox` marker |
| Interaction and breaking on the panel (`PanelInteractionPatches`) | swallowed: `OnBlockInteractStart` prefix, `OnGettingBroken` postfix, and the server `BreakBlock` event guards creative-mode instant breaks the other two can't reach |
| Panel rendering | emitted after the host, from the `TesselateBlock` postfix |

`GetParticleCollisionBoxes`, `DoEmitSideAo`/`DoEmitSideAoByFlag`, and `NeighbourJoins` are consumers the proposal's table missed — particle collision needed the same shift and panel boxes as ordinary collision, side AO needed the guest's contribution alongside the sealed wall's, and stacked walls stopped joining across a hosted cell until `NeighbourJoins` went through `WallAt` too.

## Alternatives considered
- **A decor on the hugged face**, vanilla's own mechanism for drawing a mesh beside a block. Rejected before any code: it still needs mod data for material keys, and a host change breaks every decor on the cell, which is exactly the state loss the guest record exists to avoid.
- **Snap flush.** Built and played on PR #41: it hid the gap but kept the front cell owned, and every unpatched consumer showed the seam.
- **Host the furniture in the wall's block entity.** Vanilla furniture finds its own block entity by position more than 50 times, and firepits `ExchangeBlock` themselves.
- **Chisel melding, a melding mod, a built-in niche, two panels per cell or back-to-back walls.** Unchanged from the proposal; see there for the detail.

## Consequences & open questions
- The patch surface is now large: `EveryOverridePatches` covers thirteen methods across every loaded `Block` subclass.
Each needs a cheap bail-out, and each group whose answer isn't idempotent needs its own re-entrancy depth guard, since an override calling `base.` runs both patched methods.
- Recheck on a game update, alongside decision 0020's list: `WorldChunk.BreakAllDecorFast`, `ChunkTesselator.TesselateBlock` (postfix and the `finalZ` anchor), the `TCTCache` and `JsonTesselator` members, the renderer anchors, and the overrides `EveryOverridePatches` walks.
The patch-target tests fail on most of these.
- Playtest is still pending for everything visual: panel placement, offsets, and the tooltip line, across save/reload and a host exchange, in a real build rather than a flat creative room.
- Out of scope, as the proposal left it: multi-cell furniture (a trunk, a bed), other mods' renderers, blocks overriding `OnAsyncClientParticleTick`.
- The client predicts a break locally before the server's restore arrives, so the wall may still flicker for a frame or two in multiplayer latency.
- A sneak-click on the panel still places in the front cell, because sneak bypasses `OnBlockInteractStart`; that keeps the old placement one keypress away, which answers the proposal's open question.
