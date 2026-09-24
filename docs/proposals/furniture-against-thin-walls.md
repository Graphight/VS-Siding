# Furniture against thin walls — the guest wall

- Status: Draft
- Created: 2026-09-20
- Reflects: 2026-09-24 decompiled 1.22.2 source review, and the snap-flush prototype played on branch `furniture-against-thin-walls` (draft PR #41, not merged)

## Summary
Furniture placed into a wall's dead space becomes the real block in that cell.
The wall's state moves to a guest record at the same position, and patches keep it sealing, rendering, colliding and holding things up.

## Context
A wall is a quarter-block panel on one face of its cell (decision 0002); vanilla allows one block per cell, so furniture against the open side stands in the next cell, three quarters off the panel.
The snap-flush prototype drew furniture onto the panel from the next cell, and play rejected it: that cell stayed owned, with collision and an outline where nothing is drawn.

## Design
**The inversion.** Placing a hostable block into the gap writes the wall's state (layers, styles, `layout`, `side`) to a guest record keyed by position, removes the wall block, and places the furniture as its ordinary vanilla self.
Vanilla then finds what it expects at that position, and everything the wall used to answer is re-answered from the guest record.

**Storage** is decided by the first stage's spike:
- Chunk mod data: saved with the chunk and sent in the chunk packet (`Packet_ServerChunk.Moddata`); live edits need a mod network message.
- A decor on the hugged face: vanilla saves, syncs and renders decors beside any block (`DecorFlags.CanAddToAnything`, `Removable`, `HasSidedVariants`), and `JsonTesselator.doMesh` calls `OnJsonTesselation` with the position, so a decor can draw the wall's mesh. Material keys still need mod data (decision 0001).

**Host changes.** `BlockAccessorBase.SetBlock` calls `chunk.BreakAllDecorFast(pos)` before `OnBlockRemoved` on every solid-block change, with the new id already written; one prefix there handles all of them:
- air or replaceable → restore the wall block with its state;
- another hostable block → keep the guest;
- anything else → drop the wall's layers as items.

`ExchangeBlock` (torch burnout, firepit lit/unlit) bypasses it, so guests survive exchanges.
Our own removal of the wall during placement needs a guard.

**Placement.** `SidingWallBlock.OnBlockInteractStart` takes a right-click on the open side with a hostable block and no saw, stores the guest, and calls the held block's `TryPlaceBlock` for that cell; on failure the wall goes back.

**Hostable** starts from the prototype's `GapShiftQualifies`: no solid sides, plain JSON shape, not a microblock, door, multiblock, bed, mechanical-power block or siding wall.

**Hosted blocks sit off the panel.** A hosted block's model assumes the whole cell and would clip the panel, so it is drawn, collided and selected shifted away by the panel thickness less its own inset.
This reuses the prototype's offset toolkit, reversed: the `TesselateBlock` transpiler, the box postfix on every `Block` subclass, and the lid, firepit, pot, sign, particle and crack-decal patches.

**Consumers** (decision 0020's rule: find them all first), each a postfix on every declaring `Block` subclass or an edit to our own patch:

| Consumer | Guest wall's answer |
| --- | --- |
| `GetRetention`, `GetLiquidBarrierHeightOnSide` | the wall's, on its claimed faces |
| `CanAttachBlockAt` | true on claimed faces; a hosted torch mounts on its own cell's panel, so the neighbour across the panel defers to the guest |
| `GetLightAbsorption` (both overloads) | at least the sealed wall's |
| `RoomSunlight` (0015), sealed-cell light (0018/0034), rain-fall distance (0034) | test for a guest as well as `SidingWallBlock` |
| `GetCollisionBoxes` | host's (shifted) plus the panel's framing boxes (0008) |
| `GetSelectionBoxes` | host's (shifted) plus the panel's, tagged with an id |
| Interaction and breaking on the panel | swallowed, so nothing opens or breaks the furniture through the wall; remove the furniture to change the wall |
| Panel rendering | emitted after the host, from the `TesselateBlock` hook or the decor |
| Tooltip (0030) | host's, plus one line naming the wall |

Sounds, resistance, drops, snow and spawning stay the host's.

## Alternatives considered
- **Snap flush.** Built and played on PR #41: it hid the gap but kept the front cell owned, and every unpatched consumer (a trunk's second cell first) showed the seam.
- **Host the furniture in the wall's block entity.** Vanilla furniture finds its own block entity by position more than 50 times, and firepits `ExchangeBlock` themselves.
- **Chisel melding.** `ItemChisel.IsValidChiselingMaterial` refuses non-cube shapes, and voxels carry textures, not behaviour.
- **A melding mod.** None exists; Place On Slabs and Chiseled Ground Placement both use the rejected offset.
- **A built-in niche.** The wall's own block entity holds pots; no chests or anything with a GUI.
- **Two panels per cell, or back-to-back walls.** Each room gets a flush face, but the furniture still loses the cell.

## Consequences & open questions
- The largest change yet: eight vanilla consumers plus four of our patches, and lighting is where 0015, 0016, 0018 and 0034 each shipped a bug.
- Every subclass postfix runs for every block in the world, so each needs a one-lookup bail-out, such as a per-chunk "has guests" flag.
- Open: storage, settled by the spike.
- Open: swallowing panel clicks needs a hook that sees the `BlockSelection`; interaction has one, `OnBlockBroken` does not (hence decision 0013's `ServerBreakSelection`).
- Open: whether a sneak-click should still place in the front cell.
- Out of the first version: multi-cell furniture, other mods' renderers, blocks overriding `OnAsyncClientParticleTick`.
- The subclass list and IL anchors are 1.22's, on decision 0020's game-update checklist.

## Stages
On branch `furniture-against-thin-walls`, removing the snap-flush rule first and keeping its toolkit.
1. **Spike:** mod data or decor, by rendering a guest panel beside a hosted chest through save/reload and a host exchange.
2. **Guest, placement, host changes:** a chest goes into the gap, breaking it restores the wall, exchanges keep it.
3. **Sealing:** retention, liquid, light absorption, and our four light/rain patches; checked with `/sidingroom`.
4. **Boxes and the panel:** collision and selection include the panel; its clicks and hits are swallowed.
5. **Off the panel:** retarget the offset toolkit.
6. **Hanging things:** hosted torches and signs mount on the panel.
7. **Graduate** and rewrite the handbook's Quirks paragraph.
