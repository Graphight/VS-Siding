# Modded furniture hosting

- Status: Accepted
- Created: 2026-10-23
- Reflects: mod page comment https://mods.vintagestory.at/vssiding#cmt-243661, #cmt-245074 and #cmt-246455; decisions 0020, 0035, 0036, 0049, 0079; `SidingModSystem.HostRefusal`/`IsHostable`, `RunCellId`, `BedFootprintPrefix`, `DoubleBedFootprintHosts`, `BedCells`, `IsHostableTests`, `FootprintHostsTests`, `RunCellPatchTests`; `patches/table-hostable.json` and `patches/compat/`; `CakeBuild` `FetchProbe`, `VSSiding.E2E.Tests/CompatProbeScenarios.cs` and `CelticBedScenarios.cs`, `docs/compat/`; Kevins Furniture, Vintage Engineering, Containers Bundle and More Inventorys as probed on 2026-10-23 against game 1.22.7, More Inventorys' `TryPlaceBlock` overrides read decompiled; branch `modded-furniture-hosting`; the probe, the Atlas scenarios and a playtest on 2026-10-30

## Summary
Players asked for Kevins Furniture cabinets, Vintage Engineering machines, and More Inventorys and Containers Bundle storage to sit in a siding wall's cell.
Decision 0035's hosting rule read their blocks as full cubes and refused them.
A block can now carry a `vssidingHostable` attribute that settles the question for itself, and Siding ships that attribute as JSON patches for vanilla tables and for three mods' furniture.
A probe that builds a wall against another mod's blocks found which blocks still refuse and why.
Two of them needed more than the attribute: More Inventorys' racks get a Harmony patch into that mod's placement check, and Kevins Furniture's Celtic bed gets a footprint rule for a double bed.

## Context
`IsHostable` decides from a block's own properties whether it may take a wall's cell (decision 0035).
Two of its rules read other mods' furniture wrongly.
A block with a solid side is refused as a cube, with one exception: a solid top alone on a block with a block entity.
A block with the `Multiblock` behavior is refused unless it is a trunk (decision 0036).

Kevins Furniture's `nightstandfancy` sets its solid sides with the keys `top` and `bottom`, which are not facing codes, so its bottom keeps the default, solid, and the rule refused it.
Vintage Engineering's machines set a solid bottom, which the cabinet exception does not cover.
Containers Bundle's cupboards and crates run a trunk class that derives from `BlockGenericTypedContainer` and not from vanilla's trunk, so the Multiblock rule refused them.
Decision 0036 left vanilla tables out, since a table's top is solid and it has no block entity.
More Inventorys' racks pass every rule, but their own `TryPlaceBlock` requires air in each cell above and beside the main one before it sets a filler block there.
Kevins Furniture's Celtic bed is a vanilla `BlockBed` with the Multiblock behavior, so the Multiblock rule refused it.

## Design
**A block answers for itself.**
`HostRefusal` returns the name of the rule that keeps a block out of a cell, or null when it hosts, and `IsHostable` is that answer being null.
The `vssidingHostable` attribute is read first.
`false` refuses the block.
`true` lifts the solid-side rule and the Multiblock rule only.
A multiblock that declares `true` must still carry a `side` variant, since the footprint rule of decision 0036 takes its long axis from it; one without is refused as `multiblock without side`.
The safety rules stay in force whatever a block says: a siding wall or floor, a replaceable block, an unplaceable one, a plant, a fluid, a draw type that is not JSON, a microblock, a part block, a door and a mechanical power block.
A block without the attribute goes through the rules as before.

**Patches ship the attribute.**
`patches/table-hostable.json` sets it on vanilla `game:table`, unconditionally.
This reverses decision 0036's line that tables stay out, by attribute and not by rule, so top slabs and metal sheets stay out.
`patches/compat/` holds one file per mod, each conditional on that mod being loaded:
- Kevins Furniture: the cabinet, the doorless cabinet, the fancy nightstand and the coffee table.
- Vintage Engineering: 20 machines under the domain `vinteng`, conditional on `vintageengineering`.
The creosote oven is left out, since its 3x4x3 footprint is three cells deep and always reaches open room cells.
- Containers Bundle: 7 cupboards and crates.
With the attribute, the trunk footprint rule of decision 0036 admits them against a straight run two high and two long.
- Kevins Furniture's Celtic bed, below.

**More Inventorys' blocks count a wall of the run as air.**
Its racks, clothing rack, double crate and large cabinet test each extra cell with `GetBlockId(pos) != 0` in their own `TryPlaceBlock`, then `SetBlock` a filler there.
A transpiler on every `TryPlaceBlock` the mod declares passes each test through `RunCellId`.
With the main cell a wall, a cell of the same straight run (same side, same plane, no deck or step) reads as air, and every other cell, open air included, reads as taken.
A rack fits the run or not at all, as a trunk does.
The fillers draw `empty`, so no guest panel would be drawn in their cells, and `patches/compat/moreinventorys.json` gives them vanilla's filler setup instead, `json` with the `nothing` shape.
A filler shifts by its main block's inset, read through its block entity's `MainBlockPos`.

**A double bed takes its headboard or either long side.**
The Celtic bed places a filler beside each of its two cells, all on one side: north of an east or west bed, west of a north or south one.
Its head draws the whole bed.
Vanilla runs the Multiblock check with the held head's offsets whichever way the bed faces, so `MultiblockFootprintPostfix` leaves beds alone and `BedFootprintPrefix` reads the fillers from the head it will place.
The headboard goes against a run, with the head and its filler in the wall and the feet row open.
Or a long side does: either row in the wall and the other open, the panel on the face away from the open row.
The bed shifts as one by whichever of its cells holds a guest (`BedCells`), so a head standing in the room with only its fillers in the wall shifts by theirs.

**A probe finds what hosts.**
`just probe <modid>` downloads the mod's latest release and its dependencies into the gitignored `compat/mods/`, then boots Atlas with them.
Atlas treats a folder as one mod and fails on a missing path, so the download fills six fixed zip slots, padded with empty content mods, and writes `domains.txt` from the probed mod's own assets folders.
`CompatProbeScenarios` (trait `Category=Probe`, left out of `just e2e`) builds a wall run three cells high and two long for each block type, hosts one representative block through the wall's `OnBlockInteractStart`, which is the player's right-click path, and writes `docs/compat/<modid>.md` with a verdict per block type: `refused` with the rule from `HostRefusal`, `placement failed`, or `hosts` once every wall the block took holds a guest and comes back when the block is broken, with the count of walls for a multiblock.
The player stands two cells out in front of the panel, facing it, since a bed takes its facing from where the player stands and More Inventorys places a block's second column to the player's right.
A run one cell big gave false negatives for a vertical block (the Vintage Engineering fridge) and a two-wide block (crates).
`CelticBedScenarios` runs in the same probe and places the Celtic bed along both long sides.

The collision of a hosted block that overhangs its cell is a separate fix, decision 0079.

## Alternatives considered
- **Let any block with boxes smaller than its cell be hosted.** It would cover every mod with no patch, but it changes the answer for every block in every mod at once.
- **Patch the cabinets' `sidesolid`.** That changes another mod's block for every other consumer of the flag (decision 0020's list).
- **A list of block codes in Siding's config.** The same switch kept in the wrong place: an attribute rides on the block and can be set by the mod that owns it.
- **A rule for a solid bottom alone on a block with a block entity.** It would admit every Vintage Engineering machine with no patch, and every other mod's bottom-solid block with them.
- **Let a rack overhang the wall.** Its cells past the wall would collide and select a quarter cell off from where it is drawn.
- **A Celtic bed's long side only where its fillers face the room.** Less code, but which walls take a long side would depend on the wall's compass direction.

## Consequences & open questions
- The More Inventorys transpiler rewrites calls in that mod's compiled code.
If an update changes how it tests a cell, the transpiler finds no `GetBlockId` call and changes nothing, and the probe shows the racks as `placement failed` again.
- The probe first read More Inventorys' double crate and large cabinet as `hosts` because its player faced a way that put their second cell outside the wall.
Against a wall run they failed like the racks until the transpiler.
- The deferred restore in `HostChangePrefix` keeps its own copy of the cell's position.
Vanilla's Multiblock removal passes one `BlockPos` that it moves on to each cell, so the restore had put back the last cell of the walk and left a filler walked before it empty, with its guest still stored.
Containers Bundle's linen and long crates, placed west, lost a wall in five of six tries over three probe runs before the copy, and in none of six over three runs after.
The chunk-load restore of #64 may be why the loss came and went; that was not checked.
Vanilla's trunk has the same offsets but got its walls back with or without the copy in an Atlas scenario, by a path not found, so no scenario pins the copy.
- Vanilla's Multiblock check for the Celtic bed looks at the held head's cells and not the real fillers, so a filler can still replace a block standing in an open cell of its footprint, as it does away from any wall.
- A hosted block solid on its room side answers retention for that face itself, so a cellar wall behind a Kevins Furniture cabinet, doorless cabinet or coffee table counts as one non-cooling face.
The guest wall answers only the faces it claims (`GuestSealingPatches`), and vanilla's `RoomRegistry` reads the neighbouring block's face toward the room, which is the hosted block's.
The same block standing loose in a stone cellar counts the same way, since vanilla `GetRetention` gives a solid wooden face 1.
Vanilla tables, the fancy nightstand, the Vintage Engineering machines and Containers Bundle's blocks are not solid on that face, so the wall's infill answers for them.
- Kevins Furniture's `tallshelftop`, the upper half its tall shelf places, reads `hosts, but a wall does not come back when the block breaks` when placed and broken alone.
The tall shelf broken from below brings both walls back.
Breaking a hosted tall shelf by its upper half is not checked.
- Kevins Furniture's windows are refused for solid sides, and its canopy and leather flap door as multiblocks.
They are openings, which belong to `chiselling-walls` or a window layout and not to hosting.
- Electrical Progressive and Primitive Survival are not patched.
Electrical Progressive's motor and generator classes name `BlockMPBase` in the mod's source, which stays refused; it was not probed.
Primitive Survival ships no furniture that the rule refuses.
- The probe tries one representative variant per block type, on the east face only.
An exception while placing is recorded as `placement failed`, which can mean a block that refuses placement outside a wall for its own reasons.
A verdict is evidence for the variant tried and not for every variant.
- Other mods' authors can set `"vssidingHostable": true` in a block's attributes and the patch is not needed.
- Played on 2026-10-30, and each check worked as expected: a Kevins cabinet, a Vintage Engineering machine and a Containers Bundle cupboard placed against a wall and walked into from the room; items hung on a More Inventorys rack; a Celtic bed placed with only its fillers in the wall, lain in and got up from.
