# Modded furniture hosting

- Status: Accepted
- Created: 2026-10-23
- Reflects: mod page comment https://mods.vintagestory.at/vssiding#cmt-243661, #cmt-245074 and #cmt-246455; decisions 0020, 0035, 0036, 0049, 0079; `SidingModSystem.HostRefusal`/`IsHostable`, `IsHostableTests`; `patches/table-hostable.json` and `patches/compat/`; `CakeBuild` `FetchProbe`, `VSSiding.E2E.Tests/CompatProbeScenarios.cs`, `docs/compat/`; Kevins Furniture, Vintage Engineering, Containers Bundle and More Inventorys as probed on 2026-10-23 against game 1.22.7; branch `modded-furniture-hosting`; not played, the evidence is the probe and the table Atlas scenario

## Summary
Players asked for Kevins Furniture cabinets, Vintage Engineering machines, and More Inventorys and Containers Bundle storage to sit in a siding wall's cell.
Decision 0035's hosting rule read their blocks as full cubes and refused them.
A block can now carry a `vssidingHostable` attribute that settles the question for itself, and Siding ships that attribute as JSON patches for vanilla tables and for four mods' furniture.
A probe that builds a wall against another mod's blocks found which blocks still refuse and why.

## Context
`IsHostable` decides from a block's own properties whether it may take a wall's cell (decision 0035).
Two of its rules read other mods' furniture wrongly.
A block with a solid side is refused as a cube, with one exception: a solid top alone on a block with a block entity.
A block with the `Multiblock` behavior is refused unless it is a trunk (decision 0036).

Kevins Furniture's `nightstandfancy` sets its solid sides with the keys `top` and `bottom`, which are not facing codes, and the rule read it as solid on every side.
Vintage Engineering's machines set a solid bottom, which the cabinet exception does not cover.
Containers Bundle's cupboards and crates run a trunk class that derives from `BlockGenericTypedContainer` and not from vanilla's trunk, so the Multiblock rule refused them.
Decision 0036 left vanilla tables out, since a table's top is solid and it has no block entity.

## Design
**A block answers for itself.**
`HostRefusal` returns the name of the rule that keeps a block out of a cell, or null when it hosts, and `IsHostable` is that answer being null.
The `vssidingHostable` attribute is read first.
`false` refuses the block.
`true` lifts the solid-side rule and the Multiblock rule only.
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

**A probe finds what hosts.**
`just probe <modid>` downloads the mod's latest release and its dependencies into the gitignored `compat/mods/`, then boots Atlas with them.
Atlas treats a folder as one mod and fails on a missing path, so the download fills six fixed zip slots, padded with empty content mods, and writes `domains.txt` from the probed mod's own assets folders.
`CompatProbeScenarios` (trait `Category=Probe`, left out of `just e2e`) builds a wall run two cells high and two long for each block type, hosts one representative block through the wall's `OnBlockInteractStart`, which is the player's right-click path, and writes `docs/compat/<modid>.md` with a verdict per block type: `hosts`, `refused` with the rule from `HostRefusal`, or `placement failed`.
A run one cell big gave false negatives for a vertical block (the Vintage Engineering fridge) and a two-wide block (crates).

The collision of a hosted block that overhangs its cell is a separate fix, decision 0079.

## Alternatives considered
- **Let any block with boxes smaller than its cell be hosted.** It would cover every mod with no patch, but it changes the answer for every block in every mod at once.
- **Patch the cabinets' `sidesolid`.** That changes another mod's block for every other consumer of the flag (decision 0020's list).
- **A list of block codes in Siding's config.** The same switch kept in the wrong place: an attribute rides on the block and can be set by the mod that owns it.
- **A rule for a solid bottom alone on a block with a block entity.** It would admit every Vintage Engineering machine with no patch, and every other mod's bottom-solid block with them.
- **Host the Celtic bed.** A second multiblock footprint rule beside the trunk's, for one bed.

## Consequences & open questions
- More Inventorys' 17 racks (16 rack types and the clothing rack) override `TryPlaceBlock` to require air in the cells above and beside before they place filler blocks, so no rule and no JSON patch can host them.
Hosting them needs a Harmony patch into that mod's code.
Its shelves, cabinets, crates, basket and stands already host and carry no patch.
- Vintage Engineering's creosote oven is left out for its footprint.
- The Celtic bed, the canopy and the leather flap stay refused as multiblocks.
- Kevins Furniture's windows are refused for solid sides.
They are openings, which belong to `chiselling-walls` or a window layout and not to hosting.
- Electrical Progressive and Primitive Survival are not patched.
Electrical Progressive's motor and generator classes name `BlockMPBase` in the mod's source, which stays refused; it was not probed.
Primitive Survival ships no furniture that the rule refuses.
- The probe tries one representative variant per block type, on the east face only.
An exception while placing is recorded as `placement failed`, which can mean a block that refuses placement outside a wall for its own reasons.
A verdict is evidence for the variant tried and not for every variant.
- Other mods' authors can set `"vssidingHostable": true` in a block's attributes and the patch is not needed.
- Not played.
None of the hosted modded blocks has been placed by hand in the game client.
To check: place a Kevins cabinet, a Vintage Engineering machine and a Containers Bundle cupboard against a wall, and walk into each from the room.
