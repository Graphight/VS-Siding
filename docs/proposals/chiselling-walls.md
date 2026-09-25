# Chiselling walls

- Status: Draft
- Created: 2026-09-25
- Reflects: decisions 0001, 0003, 0008, 0013, 0035; `SidingWallBlock.cs`; `wall.json`; decompiled 1.22.7 `ItemChisel.IsValidChiselingMaterial`, `BlockEntityMicroBlock.WasPlaced`; vanilla `plankslab.json`/`plankstairs.json`

## Summary
A chisel does nothing to a siding wall, and says nothing either: vanilla refuses any block that isn't a cube.
Vanilla already chisels non-cube blocks, plank slabs and stairs, by building the microblock from their collision boxes.
The proposal opts the wall into that path, then rebuilds the microblock's materials from the wall's layers so the chiselled wall looks like the wall it was.

## Context
**What refuses it.**
`ItemChisel.IsValidChiselingMaterial` (`ItemChisel.cs:278-337`) returns false at line 305 for any block whose `DrawType` isn't `Cube` and whose shape isn't `block/basic/cube`.
A wall is `drawtype: "json"` with its own shapes (`wall.json:234-244`), so the click falls through silently.
Two checks run before that one: `IConditionalChiselable.CanChisel` (line 289), which can refuse with a message, and a `canChisel` attribute (lines 294-302), which, if true, allows chiselling outright.

**What a chiselled block is.**
`ItemChisel` swaps the block for `chiseledblock` and calls `BlockEntityMicroBlock.WasPlaced(oldBlock, ...)` (`BlockEntityMicroBlock.cs:638`).
A microblock is a list of voxel cuboids (`VoxelCuboids`), each tagged with an index into `BlockIds`, the material blocks it textures from (`ToUint`, line 1555).
By default `WasPlaced` makes one full cube of the old block.
With the `chiselShapeFromCollisionBox` attribute it makes one cuboid per collision box instead (lines 646-656), which is how `plankslab.json` and `plankstairs.json` chisel into their own shape.

**Why that isn't enough alone.**
It would give one material, the wall block itself, textured from `wall.json`'s default textures rather than the layers.
And by the time `WasPlaced` runs, the block has been replaced, so the `SidingWallEntity` holding the layers (decision 0003) is already gone.

## Design
**Opt in.**
`canChisel: true` and `chiselShapeFromCollisionBox: true` on the wall's attributes.
That gets vanilla's conversion, its world config (`microblockChiseling`) and its tool modes for free.

**Capture the layers before the swap.**
A prefix on the chisel's conversion reads the wall's entity at that position and holds its keys for the one call that follows.

**Rebuild the microblock from the layers.**
A postfix on `BlockEntityMicroBlock.WasPlaced`, when the old block is a `SidingWallBlock`, replaces the single cuboid with one per built element: framing, infill and each finish.
Boxes come from the wall's shape elements rounded to whole voxels; fractional profiles (weatherboard taper, shake relief, 0022/0023) flatten to slabs.
Each element's material is a new `ChiselBlock` code on its `Framings`/`Infills`/`Finishes` entry: e.g. `game:planks-{wood}` for framing and plank finishes, `game:daub-*-wattle` for wattle, `game:cobblestone-*` for rubble, a hay block for straw, glass for glazing.
An entry with no `ChiselBlock` falls back to the framing's block.

**One way.**
A chiselled wall is an ordinary microblock: no peeling (0013), no room-sealing from infill retention, no cellar strength (0015).
A cell hosting furniture (0035) holds a guest, not a wall, so it isn't reachable by the chisel.

## Alternatives considered
- **Refuse with a clear message through `IConditionalChiselable`.** Cheap and honest, but it makes a missing feature explicit instead of adding it; kept as the fallback if the rebuild stalls.
- **`canChisel` alone, no rebuild.** The wall turns into a slab of its default textures and loses every layer's look.
- **Keep the wall a `SidingWallBlock` and chisel its layers in place.** Would need our own voxel storage, mesher and tool modes, duplicating the microblock system.

## Consequences & open questions
- Every material entry grows a `ChiselBlock`; families (0010) template it like their other keys.
- Open: straw's stand-in block, and whether a translucent glass material in a microblock renders right.
- Open: which `ItemChisel` method to prefix, since both the conversion and `WasPlaced` must see the same position in one call.
- A wall's room-sealing drops when it's chiselled; the microblock's own per-face solidity takes over.

## Stages
1. **Opt in:** `canChisel` and `chiselShapeFromCollisionBox` alone, played to see the conversion work and what it looks like.
2. **Rebuild:** the capture prefix, the `WasPlaced` postfix, `ChiselBlock` on every entry, and a test mapping each element to a voxel cuboid.
3. **Playtest:** a framed, infilled and finished wall on both faces, a glazed wall, a cornerout, and a chiselled wall reloaded.
4. **Graduate** as a decision.
