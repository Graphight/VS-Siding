# 0064 — Plaster finish

- Status: Accepted
- Created: 2026-10-07
- Reflects: mod page comments https://mods.vintagestory.at/vssiding#cmt-243765 and #cmt-244452; branch `feat/bark-and-plaster-finishes`; `config/materials.json`'s `Finishes`, `MaterialFamilies.Expand`, `MaterialTextureOpacityTests`, `MaterialDisplayNameTests`; vanilla `plaster-plain.json`, `plaster-fancy.json`, `overlay/plaster.json`, the grid recipes under `recipes/grid/plaster/` and the textures under `block/stone/plaster/`; decisions 0001, 0010, 0048; unit tests pass; not yet played

## Summary
Two players asked for a plaster finish, one because "the daubs just dont go light enough" and one for interiors.
Vanilla's four plaster blocks are now finishes, as four explicit entries in `Finishes` in `config/materials.json`, with vanilla's textures and no code.

## Context
The lightest finish was a daub, and every daub is an earth colour.
A player who wanted a white interior had nothing to reach for.

Vanilla ships plaster as full blocks.
`plaster-plain` comes from quicklime over sand in the grid, with no tool.
`plaster-square` adds four planks around a plain block, and `plaster-diagonal` and `plaster-stripes` are its other timbered patterns, each with `ns`, `we` and `ud` orientations that drop the `ud` block.
Their textures are `block/stone/plaster/plain`, `square`, `diagonal` and `stripes`.
Plaster over timber is the half-timbered look this mod's walls already lean towards.

A finish that is a flat face needs no shape work: `plate-{metal}` (decision 0048) and `polished-{rock}` are entries with a texture and no `Elements`.

## Design
**Four explicit entries in `Finishes`.**
The keys are `plaster-plain`, `plaster-square`, `plaster-diagonal` and `plaster-stripes`.
Each has `Texture: "game:block/stone/plaster/{type}"` for its own type and `BlockMaterial: "Stone"`, which is what the vanilla block names.
None has `Elements`, `Styles` or `Match`.
Plain and square consume and drop one `game:plaster-{type}`.
Diagonal and stripes consume and drop one `game:plaster-{type}-ud`, since the oriented blocks carry a third code part.

**A flat face.**
No `Elements` and no `Styles`, so each draws as the default flat panel on a wall, a floor's top or a ceiling.

**Display names.**
Four `DisplayName` keys in `lang/en.json`: "Plain Plaster Finish", "Square Plaster Finish", "Diagonal Plaster Finish" and "Striped Plaster Finish".
The walls guide lists plaster among its example finish materials, and the ModDB finish table has a Plaster row.

## Alternatives considered
- **Two family templates, as first proposed.** One for `plaster-{type}` matching `game:plaster-plain` and `game:plaster-square`, one for the oriented pair matching `game:plaster-*-ud`. Two templates cannot both be keyed `plaster-{type}` in one dictionary, and a `game:plaster-*` match would also catch `plaster-diagonal-ns`. Four explicit entries avoid both. The cost is that another mod's plaster variants do not come along, since nothing matches them.
- **Consume quicklime directly**, as one player suggested. Vanilla's recipe already sets what a block of plaster costs, and the plaster block is what a player has in hand once they have made any. The mod would be inventing a second price for the same wall.
- **A lighter daub.** Daub colours come from vanilla's `daubraw-*` items; a white one would be a new item and recipe where plaster already exists.
- **Vanilla's plaster overlay decor (`overlay-plaster-*`).** It is a decor layer for full block faces, excluded from the handbook, and decor on a wall face is not a layer the wall peels or drops.
- **Plain only.** It is the one that was asked for. The other three cost one more entry and one more lang key each, and the timbered patterns suit the mod, so they come along.

## Consequences & open questions
Finding, settled:
- `MaterialTextureOpacityTests.NoMaterialTextureHasPartialAlpha` resolved all four plaster textures and found no partial alpha.

One block per face, as cobblestone and polished rock take; a full plaster block for a 1/16 skin is generous to vanilla's recipe.

Nothing here has been played.
Still open, and only play can settle it:
- Whether `stripes` and `diagonal` read the right way up on a wall face, and which way they run on a floor.
