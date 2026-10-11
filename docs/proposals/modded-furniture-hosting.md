# Modded furniture hosting

- Status: Draft
- Created: 2026-10-05
- Reflects: mod page comment https://mods.vintagestory.at/vssiding#cmt-243661, edited on 2026-10-05 to add the request; the shipped block types of Kevins Furniture 1.9.0 (no DLL decompiled, as decision 0001); `SidingModSystem.IsHostable`, `IsHostableTests`; mod page comment https://mods.vintagestory.at/vssiding#cmt-245074; the shipped block types of Vintage Engineering 0.6.1, Primitive Survival 5.1.4 and Electrical Progressive: Basics 3.3.1 and Industry 0.7.0, read the same way on 2026-10-08; decisions 0020, 0035, 0036, 0049; not played with any of those mods

## Summary
A player asked for "compatability with Kevins furniture cabinets etc".
Most of that mod's furniture already passes the hosting rule, but its two cabinets declare every side solid, which the rule reads as a full cube and refuses.
The proposal lets a block say outright whether it may be hosted, and ships that answer for the two cabinets.
A second player keeps machinery flush to walls, and every Vintage Engineering machine is refused by the same rule for a solid bottom; most of them also fill their cell, which the attribute does not settle.

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

**A second request: machinery.**
"I use the vintage engineering and progressive electric mods, and I tend to keep machinery flush to walls."
The same comment names Hex's Mechworks and Boric Alchemy as of no major concern, and Primitive Survival for "tables, racks, hooks, etc. that either mount to walls or the ceiling or sit flush to the wall", which the player had not tested.

Read against the same rule, again without loading them:

- *Vintage Engineering* (61k downloads). All 21 machines under `lv/` and `mv/` set `sidesolid: { all: false, down: true }`, and the fluid tank the top as well. The exception in the rule admits a solid top alone, so a solid bottom refuses every one of them. The creosote oven and the fridge carry `Multiblock` too. The trash can sets no solid side and passes.
- *Their boxes.* 18 of the 21 have a collision box across the whole cell, x and z from 0 to 1. The generator is 14/16 wide, the fluid tank 12/16 and the pump 8/16. A wall's panel is 4/16 of its cell, so a hosted block 16 wide is shifted off the panel and stands 4/16 into the next cell, the overhang `guest-furniture-collision` describes.
- *Electrical Progressive.* Basics (122k downloads) has 27 block types: 10 carry `Multiblock`, 4 declare every side solid, and the motor and generator classes name `BlockMPBase` in the mod's source, which the rule refuses; whether they extend it was not read. Industry has 7, four of them multiblock. Equipment ships no block types.
- *Primitive Survival* (992k downloads). 5.1.4 ships 69 block type files, 32 of them texture overrides, and all of the other 37 set `sidesolid: { all: false }`. None is a table, rack or hook: the blocks are traps, fishing gear, rafts and moulds, and the nearest to furniture are a smoker, a jar holder and a lantern. So nothing in it is refused for solid sides, and the furniture the player remembers is from another mod or an older version.

So for machinery the solid-side rule is the first refusal and not the last: with it lifted, most machines are still a full cube beside a panel.

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
- Vintage Engineering by attribute is a patch over 21 block types. A second exception in the rule, a solid bottom alone on a block with a block entity, would admit them all with no patch, and every other mod's bottom-solid block with them, which is the kind of change the first alternative was turned down for.
- Whether a full-cube machine should host at all. It would touch the panel and stand 4/16 into the cell in front, where a player walks into its box (`guest-furniture-collision`). The other reading of "flush" is no hosting: the machine keeps its own cell and the wall stands on the face nearest it, which is a question of where a placed wall lands (decision 0069).
- Which machines the player means. The three Vintage Engineering blocks narrower than their cell, and Electrical Progressive's single-cell blocks that set no solid side, may already be all they need; ask before patching 21.
