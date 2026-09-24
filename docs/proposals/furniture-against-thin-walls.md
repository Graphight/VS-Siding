# Furniture against thin walls — the guest wall

- Status: Draft
- Created: 2026-09-20
- Reflects: 2026-09-24 session — decompiled 1.22.2 source review, and a played snap-flush offset prototype (branch `furniture-against-thin-walls`, draft PR #41, not merged)

## Summary
Let furniture, pots and torches truly live in the empty three quarters of a wall's cell.
The furniture becomes the real block in that cell; the wall steps aside into a *guest* record at the same position and keeps sealing, rendering, colliding and holding things up through patches that ask "is there a guest wall here?" wherever vanilla asks the block at that position.

## Context
A wall is a quarter-block panel hugging one face of its cell (decision 0002).
Vanilla allows one block per cell, so the other three quarters are dead space: a chest, pot or torch against the wall's open side has to stand in the *next* cell, three quarters of a block off the panel.
Against an interior partition one of the two rooms always eats this.

A snap-flush prototype was built and played first (see Alternatives): the furniture stayed in the next cell and only its picture and boxes slid three quarters back onto the panel.
It looked right, but the cell in front of the wall stayed owned by the furniture, and every consumer of "where the block is" that was not patched showed the seam: an outline and collision where nothing is drawn, a trunk's second cell left behind.
Play settled it: the furniture has to actually be in the wall's cell.

## Design

### The inversion
Hosting the furniture *inside* the wall's block entity is impossible (Alternatives), so it goes the other way round.
When furniture is placed into a wall's dead space, the wall's state (`Framing`, `Infill`, `Front`, `Back`, `SecondFront`, styles, plus `layout` and `side`) is written to a guest record keyed by position, the wall block is removed, and the furniture is placed as the ordinary vanilla block in that cell.
Vanilla code then finds exactly what it expects at that position — a chest with its own block entity, a firepit that can exchange itself lit and unlit, a ground-storage pile — so nothing in vanilla has to know about us.
Everything the *wall* used to answer for its cell is re-answered by patches that consult the guest record.

### Where the guest record lives
Two candidates, settled by the first stage's spike:
- **Chunk mod data** (`IWorldChunk.SetModdata`): saved with the chunk and sent to clients inside the chunk packet (`Packet_ServerChunk.Moddata`); live changes need a small mod network message because a chunk is not re-sent for one edit.
- **A decor block** on the hugged face: vanilla already stores, saves, syncs and renders decors alongside any solid block, and `DecorFlags` has `CanAddToAnything`, `Removable`, `HasSidedVariants`, `NotFullFace` and `DrawIfCulled`. `JsonTesselator.doMesh` calls `block.OnJsonTesselation(ref mesh, …, pos, …)` for decors too, so a decor could swap in the wall's material mesh per position. The material keys still need mod data (decision 0001: materials are attribute keys, not blocks), so the decor would only carry presence, rendering and selection.

### The one choke point for "the host changed"
`BlockAccessorBase.SetBlock` calls `chunk.BreakAllDecorFast(pos)` immediately before `OnBlockRemoved` whenever the solid block at a position changes, with the new block id already written.
One Harmony prefix there sees every removal and replacement:
- new block is air or replaceable → put the wall block back with its block-entity state and clear the guest;
- new block is another hostable block → keep the guest;
- anything else (a solid block placed by world edit, say) → drop the wall's layers as items and clear the guest.

`ExchangeBlock` (torch burnout, firepit lit/unlit, most state swaps) does not go through it, touches no decors and calls no `OnBlockRemoved`, so a guest survives exchanges untouched.

### Placing furniture into the gap
`SidingWallBlock.OnBlockInteractStart` already sees every right-click on a wall.
A right-click on the open side with a hostable block in hand (and no saw in the off hand, so layering is unaffected) moves the wall into a guest record and calls the held block's own `TryPlaceBlock` for that cell; on failure the wall is put back.
The choke-point prefix needs a guard so our own removal of the wall is not mistaken for the host changing.

### What counts as hostable
Start from the prototype's eligibility list (`GapShiftQualifies`): no solid sides, plain JSON shape, not a microblock, door, multiblock filler, bed or mechanical-power block, not a siding wall.
Multi-cell furniture (trunks, beds) is out of the first version.

### Hosted blocks sit off the panel
A hosted block's model assumes the whole cell, so a chest would sink three sixteenths into the panel and a wall torch would be buried in it.
Each hosted block is drawn, collided and selected shifted away from the panel by just enough to clear it: `0.25` minus the block's own inset toward the panel, so a chest moves `3/16` and a wall torch `4/16`.
This is the prototype's offset toolkit pointed the other way, and it carries over whole: the `ChunkTesselator.TesselateBlock` draw-origin transpiler, the collision/selection postfix on every declaring `Block` subclass with its re-entrancy guard, and the chest-lid, firepit-contents, pot, sign-text, particle and crack-decal patches.

### Every consumer, and its answer
Decision 0020's lesson: find every vanilla consumer of "the block at this position" before shipping.
Each row below becomes a postfix on every declaring `Block` subclass (the prototype's enumeration), or an edit to one of our own existing patches:

| Consumer | Guest wall's answer |
| --- | --- |
| `GetRetention` | the wall's retention on its claimed faces (rooms still seal) |
| `GetLiquidBarrierHeightOnSide` | the wall's barrier on its claimed faces |
| `CanAttachBlockAt` | true on claimed faces; and a hosted torch/sign mounts on its own cell's panel, so the neighbour across the panel is asked "is the cell beside you a guest wall facing this way?" |
| `GetLightAbsorption` (both overloads) | at least the sealed wall's absorption, so sunlight doesn't flood in through the wall's cell |
| `RoomSunlight` (0015), sealed-cell light (0018/0034), rain-fall distance (0034) | our own patches; they test `is SidingWallBlock` and learn to test for a guest too |
| `GetCollisionBoxes` | host's boxes (shifted) plus the panel's framing boxes (0008) |
| `GetSelectionBoxes` | host's boxes (shifted) plus the panel's boxes tagged with an id |
| Interaction and breaking on the panel part | swallowed while hosting: a click or hit on the tagged panel boxes must never open or break the chest from the other side of the wall; to change the wall, remove the furniture first |
| Rendering the panel | emitted after the host at the same position, from the `TesselateBlock` hook or a decor |
| Tooltip (0030) | host's, plus one line naming the wall |

Sounds, resistance, drops, snow and spawning stay the host's own.

## Alternatives considered
- **Snap flush (built and played, rejected).** The furniture stays in the cell in front of the wall; its render, boxes, animations, particles and crack decal slide three quarters onto the panel. It removed the visible gap but not the lost cell. The real block still owned the cell in front, so collision and an outline sat in visually empty space, and every unpatched consumer (a trunk's multiblock filler, first of all) showed the seam. Its machinery survives here, reversed.
- **Host the furniture inside the wall's block entity.** Dead: chest, firepit, shelf and ground-storage classes look up their own block entity by position more than 50 times, and every lookup would find the wall. Firepits also `ExchangeBlock` themselves, which would replace the wall.
- **Vanilla chisel melding.** `ItemChisel.IsValidChiselingMaterial` refuses non-cube draw types without `canChisel`, and chiselling voxelises the default shape: the layers are lost, and voxels carry textures, not behaviour.
- **Depend on a melding mod.** None exists. Place On Slabs and Chiseled Ground Placement both offset a block that stays logically in its own cell, which is the rejected snap-flush approach.
- **A built-in niche.** The wall's own block entity holds pots and bowls in its gap. Robust, all our own code, but no chests, firepits or anything with a GUI.
- **Two panels in one cell** (`multiple-walls-per-cell`, parked) and **back-to-back walls in two cells.** Both give each room a flush face without furniture sharing a cell; both cost the cell the furniture needs.

## Consequences & open questions
- This is the largest change the mod has attempted: eight vanilla consumers re-answered, plus four of our own patches. The lighting rows carry the most risk; decisions 0015, 0016, 0018 and 0034 each shipped a bug in this area.
- Every `Block`-subclass postfix taxes every block in the world, so each one must bail out on one cheap lookup; a per-chunk "has any guests" check is the obvious first gate.
- **Open: storage.** Mod data or decor; the spike stage decides, by proving a panel renders, saves, reloads and survives a firepit being lit.
- **Open: panel interaction.** Swallowing clicks on the tagged panel boxes needs a hook that sees the `BlockSelection` (interaction does; `OnBlockBroken` does not, which is why decision 0013 keeps `ServerBreakSelection`). This needs checking before the stage that builds it.
- **Open: gesture.** Aiming at the wall's open side with a chest currently places it in the cell in front; after this, it goes into the gap. Whether a sneak-click should keep the old behaviour is a playtest question.
- Multi-cell furniture, other mods' block-entity renderers, and blocks that override `OnAsyncClientParticleTick` are out of the first version, as they were for snap-flush.
- The subclass list and every IL anchor are 1.22's, on the same game-update checklist as decision 0020's table.

## Stages
Built on branch `furniture-against-thin-walls`, whose snap-flush rule is removed first and whose offset toolkit is kept.
1. **Spike: storage and render.** Decide mod data versus decor by getting one guest panel to render beside a hosted chest, survive save/reload, and survive the host being exchanged. Output: a short decision on storage, plus the throwaway removed.
2. **Guest record, placement and the choke point.** Right-click puts a chest into the gap; breaking the chest brings the wall back; exchanges keep it.
3. **Sealing.** Retention, liquid barrier and light absorption answer for guests; the four lighting/rain patches learn guests. Checked with `/sidingroom` and the lighting playtest method.
4. **Boxes and the panel part.** Collision and selection include the panel; interaction and breaking on it are swallowed.
5. **Hosted blocks sit off the panel.** Retarget the offset toolkit to the ¼-away rule.
6. **Hanging things.** Torches and signs hosted in the gap mount on the panel; the flush side still takes attachments.
7. **Graduate** to a decision, rewrite the handbook's Quirks paragraph.
