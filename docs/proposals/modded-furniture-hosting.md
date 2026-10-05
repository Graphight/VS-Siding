# Modded furniture hosting

- Status: Draft
- Created: 2026-10-05
- Reflects: mod page comment https://mods.vintagestory.at/vssiding#cmt-243661, edited on 2026-10-05 to add the request; the shipped block types of Kevins Furniture 1.9.0 (no DLL decompiled, as decision 0001); `SidingModSystem.IsHostable`, `IsHostableTests`; decisions 0020, 0035, 0036, 0049; not played with that mod

## Summary
A player asked for "compatability with Kevins furniture cabinets etc".
Most of that mod's furniture already passes the hosting rule, but its two cabinets declare every side solid, which the rule reads as a full cube and refuses.
The proposal lets a block say outright whether it may be hosted, and ships that answer for the two cabinets.

## Context
Kevins Furniture is the largest furniture mod on the mod database, at 178k downloads.

`IsHostable` decides from a block's own properties whether it may take a wall's cell (decision 0035).
One of its tests refuses any block with a solid side, "a rule meant for full cubes" (decision 0036), with one exception: a solid top alone on a block with a block entity, which is how vanilla's cabinet gets in.

Reading the mod's 25 block types against that rule, without loading it:

- *Pass.* Both nightstands, the knife block, shoe rack, wardrobe, both halves of the tall shelf, the stool, floor cushion, stove and telescope, and the fur roll through the bed path (decision 0049).
- *Refused for solid sides.* `cabinet`, `doorlesscabinet` and `coffeetable` set `sidesolid: { all: true }`. The cabinet's box is half a block wide and half a block high, so the claim is not one its shape backs up.
- *Refused as a multiblock.* The Celtic bed carries the `Multiblock` behavior, which only trunks pass.
- *Refused, and not furniture.* Ten windows and flaps are `BlockTrapdoor` blocks, most of them solid and multiblock.

So the player's cabinet stands in the cell in front of the wall, a gap away from it, while the nightstand beside it sits flush.

## Design
**A block can answer for itself.**
`IsHostable` reads a `vssidingHostable` attribute first: `true` hosts the block whatever its sides say, `false` keeps it out, and a block without one goes through the rules as now.
It is one early return, and the first rule that another mod's author can satisfy from their own JSON.

**A patch for the two cabinets.**
Siding ships a JSON patch setting the attribute on `kevinsfurniture:cabinet-*` and `kevinsfurniture:doorlesscabinet-*`, conditional on that mod being loaded.
Nothing else in that mod is touched.

**Play the ones that already pass.**
Passing the rule is not the same as sitting right against a panel.
The wardrobe, tall shelf, stove and knife block run their own block classes, and the tall shelf is two blocks stacked, so each is placed against a wall and looked at before the mod page says they work.

## Alternatives considered
- **Let any block with boxes smaller than its cell be hosted.** It would cover every mod with no patch, but it changes the answer for every block in every mod at once, and decision 0036 kept tables out on purpose.
- **Patch the cabinets' `sidesolid` to top only.** That changes another mod's block for every other consumer of the flag (decision 0020's list), so anything attached to a cabinet's side would fall.
- **A list of block codes in Siding's config.** The same switch, kept in the wrong place: an attribute rides on the block and can be set by the mod that owns it.
- **Host the Celtic bed.** A second multiblock footprint rule beside the trunk's (decision 0036) for one bed; left until asked.

## Consequences & open questions
- A hosted block that claims solid sides still answers vanilla's solid-side questions for the cell: retention, liquid, attachment. How those combine with the guest wall's own answers in `GuestSealingPatches` needs reading before the patch ships, and a room built with one needs `/sidingroom`.
- Whether the coffee table should host. It is all-solid with no block entity, and a table against a wall is the case 0036 left out.
- "etc" may mean the windows. Those are openings, which is `chiselling-walls` or a window layout, not hosting.
- Whether Kevins Furniture's author would set the attribute themselves; the patch covers it until then.
