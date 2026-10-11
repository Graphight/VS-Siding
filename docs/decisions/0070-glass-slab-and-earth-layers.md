# 0070 — Glass slab and earth layers

- Status: Accepted
- Created: 2026-10-11
- Reflects: issues #97 and #109, with the mod page comments https://mods.vintagestory.at/vssiding#cmt-245334 and #cmt-245616; branch `slab-and-earth-materials`; `config/materials.json`'s `InfillFamilies` and `Finishes`, `EarthScenarios`, `SidingWallBlockRetentionTests`, `MaterialTextureOpacityTests`; vanilla `glass/slab-*.json`, `rammed.json`, `packeddirt.json`; decisions 0019, 0051, 0053, 0059, 0064; extends 0019 and 0059 and supersedes neither

## Summary
Three entries in `config/materials.json`, with vanilla textures and no code: a glass slab infill beside the full glass block, a packed dirt finish, and a rammed earth infill in five patterns.
Decision 0059 shipped packed dirt as an infill only and rammed earth as a finish only; this adds the other layer of each.

## Context
One player found full glass blocks expensive as windows (#97).
Roofing glazes with a full block or a slab, by a second player's check in game and not read from Roofing's assets.
Vanilla saws one glass block into two `glassslab-{type}-down-free`, so a slab window costs half the glass.

Another player asked for packed dirt and rammed earth as both finish and infill (#109).
`packeddirt` sat in `Infills` and `rammed-{pattern}` in `FinishFamilies`, each in one layer.

## Design
**The slab is an infill beside the block.**
A `glassslab-{type}` family matches `game:glassslab-*-down-free` on its `type` variant, with `Transparent: true`, and consumes and drops one slab.
The full block stays, as it does in Roofing.

**The slab has its own key and the block's lang keys.**
The key differs because it consumes and drops a different block; an entry has one `Consumes`.
Its `DisplayName` points at the `infill-glass-{type}` keys, since both draw the same zero-thickness pane from the same texture and the tooltip names them alike.
Glazed cells join on `Transparent` whatever the key (decision 0019), so a slab cell and a block cell merge into one pane.

**Packed dirt is a finish as well as an infill.**
A `packeddirt` entry in `Finishes` consumes and drops one `game:packeddirt`, with the infill's texture and `BlockMaterial: "Soil"`.
It is a flat face with no `Elements`, `Styles` or `FloorElements`, as plaster is (decision 0064), so it draws the plain slab on a wall, a floor's top and a ceiling.

**Rammed earth is an infill as well as a finish.**
A `rammed-{pattern}` entry in `InfillFamilies` matches `game:rammed-light-*`, the one `soil` colour vanilla has, in all five patterns, since an unfinished wall shows its infill.
It is `Soil`, so it cools a cellar like clay and packed dirt (decision 0003).

**One block, two layers.**
The held block is matched against the infills while the wall has no infill and against the finishes after (`SidingWallBlock.OnBlockInteractStart`).
The same block therefore fills a bare frame and faces a filled one.
Floors and decks read the same dictionaries (decisions 0051, 0053), so none of this needs code.

**An end-to-end check.**
No unit test checks that a `Consumes.code` names a real block (decision 0059).
`EarthScenarios` plays each new click on a headless server: rammed earth then packed dirt on a frame, and a glass slab on a frame.
A slab entry that dropped a full block would double glass on every saw cut, so a unit sweep now holds every shipped entry's `Drops` to its `Consumes`, the stained plank's orientation excepted.

## Alternatives considered
- **Replace the full glass block with the slab.** A wall already glazed holds a `glass-{color}` key, and a key no entry names counts as not built; Roofing keeps both as well.
- **A separate "Glass Slab Infill" name.** Two names for one pane: the two draw alike and merge.
- **The slab as the same key as the block, at a second cost.** An entry has one `Consumes`.
- **A cheaper earth finish.** A finish is the outer 1/16 of the wall, so a wall filled and finished in the same earth costs three blocks for one cell. A fractional cost would make earth the one finish priced unlike plaster and polished rock, which each take a block.
- **Styles on the packed dirt finish.** Nobody asked for one, and a flat face needs no shape work (decision 0064).
- **Other rammed earth colours.** Vanilla has `light` only, so there is nothing to match.
- **A `drypackeddirt` infill.** Left until someone asks, as in decision 0059.

## Consequences & open questions
- A glass slab window costs half the glass; a wall filled and finished in the same earth costs three blocks.
- Not yet played: a slab window beside a block window, an unfinished rammed earth wall, and a packed dirt floor face.
- A second rammed earth colour would expand to the same `rammed-{pattern}` keys, so it needs its own entry or a second placeholder in the key (decision 0068).
