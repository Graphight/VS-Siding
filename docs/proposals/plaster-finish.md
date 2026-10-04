# Plaster finish

- Status: Draft
- Created: 2026-10-04
- Reflects: mod page comments https://mods.vintagestory.at/vssiding#cmt-243765 and #cmt-244452; `config/materials.json`, `MaterialFamilies.Expand`; vanilla `plaster-plain.json`, `plaster-fancy.json`, `overlay/plaster.json`, the grid recipes under `recipes/grid/plaster/` and the textures under `block/stone/plaster/`; decisions 0001, 0010, 0048; not yet played

## Summary
Two players asked for a plaster finish, one because "the daubs just dont go light enough" and one for interiors.
The proposal adds vanilla's four plaster blocks as finishes through two family templates in `config/materials.json`, with vanilla's textures and no code.

## Context
The lightest finish today is a daub, and every daub is an earth colour.
A player who wants a white interior has nothing to reach for.

Vanilla ships plaster as full blocks.
`plaster-plain` comes from quicklime over sand in the grid, with no tool.
`plaster-square` adds four planks around a plain block, and `plaster-diagonal` and `plaster-stripes` are its other timbered patterns, each with `ns`, `we` and `ud` orientations that drop the `ud` block.
Their textures are `block/stone/plaster/plain`, `square`, `diagonal` and `stripes`.
Plaster over timber is the half-timbered look this mod's walls already lean towards.

A finish that is a flat face needs no shape work: `plate-{metal}` (decision 0048) and `polished-{rock}` are entries with a texture and no `Elements`.

## Design
**Two templates in `FinishFamilies`.**
`plaster-{type}`, matching the blocks `game:plaster-plain` and `game:plaster-square` on `type`, consuming and dropping one `game:plaster-{type}`.
A second template for the oriented pair, matching `game:plaster-*-ud` on `type`, consuming and dropping `game:plaster-{type}-ud`.
Two are needed because the oriented blocks carry a third code part: one template's `Consumes` cannot name both `plaster-plain` and `plaster-diagonal-ud`.
Both use `Texture: "game:block/stone/plaster/{type}"` and `BlockMaterial: "Stone"`, which is what the vanilla block names.

**A flat face.**
No `Elements` and no `Styles`, so it draws as the default flat panel on a wall, a floor's top or a ceiling.

**Display names.**
Four `DisplayName` keys in `lang/en.json`, and plaster joins the handbook's list of finishes.

## Alternatives considered
- **Consume quicklime directly**, as one player suggested. Vanilla's recipe already sets what a block of plaster costs, and the plaster block is what a player has in hand once they have made any. The mod would be inventing a second price for the same wall.
- **A lighter daub.** Daub colours come from vanilla's `daubraw-*` items; a white one would be a new item and recipe where plaster already exists.
- **Vanilla's plaster overlay decor (`overlay-plaster-*`).** It is a decor layer for full block faces, excluded from the handbook, and decor on a wall face is not a layer the wall peels or drops.
- **Plain only.** It is the one that was asked for. The other three cost one more template and three lang keys, and the timbered patterns suit the mod, so they come along.

## Consequences & open questions
- Whether `stripes` and `diagonal` read the right way up on a wall face, and which way they run on a floor.
- One block per face, as cobblestone and polished rock take; a full plaster block for a 1/16 skin is generous to vanilla's recipe.
- Whether the first template's match is a regex (`game:@plaster-(plain|square)`, as `stone-{rock}` uses) or two explicit entries; four entries in all, so either is small.
