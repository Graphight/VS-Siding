# Stained plank finishes

- Status: Draft
- Created: 2026-10-04
- Reflects: mod page comment https://mods.vintagestory.at/vssiding#cmt-244452; the shipped assets of Dyed Wood 2.1.0, Wood Stain 1.3.2 and Vanilla Varnished Planks 1.0.6 (no DLL decompiled, as decision 0001); `MaterialFamilies.Expand`, `SidingWallBlock.MatchConsumes`, `config/materials.json`'s `planks-{wood}`; decisions 0001, 0007, 0010; none of the three mods played with Siding

## Summary
A player asked for "compat with the dyed wood mod".
Three mods on the mod database could be meant, and each stores a coloured plank differently: one already matches, one needs family templates with two placeholders, and one needs a match on a held stack's attributes.
The proposal takes them in that order and stops wherever the player's answer says to.

## Context
A plank finish comes from the `planks-{wood}` template, which matches any `*:plank-*` item on its `wood` variant and names the textures `{domain}:block/wood/planks/{wood}*`.
`Expand` fills one placeholder per template, from one variant of one registered item or block.
`MatchConsumes` matches a held stack by its code alone.

- **Vanilla Varnished Planks** (`vanillavarnished`, 27k downloads) ships a `plank-{wood}` item. The existing template should already pick it up, as it does Wildcraft Trees since 1.3.0. Unconfirmed in play.
- **Wood Stain** (`woodstain`, 14k downloads) ships no plank item, only blocks: `stainedplanks-{stain}-{wood}-{orientation}`, 11 stains over every vanilla wood and five aged ones. Each is a real block code, with two variants where a template holds one. Its look is the vanilla plank texture under a blended `stain/overlay-{stain}` texture.
- **Dyed Wood** (`dyedwood`, 394 downloads) ships one block, `dyedwood:planks`, and keeps the colour and wood in the stack's attributes (`types: { color, wood }`), drawn through Attribute Rendering Library. Its 154 colour and wood pairs exist only as creative inventory stacks and as textures named `{color}{wood}1`.

The comment names "the dyed wood mod", which is Dyed Wood's exact name, and could as easily be a description of Wood Stain.

## Design
**Confirm the one that should already work.**
Load Vanilla Varnished Planks beside Siding and finish a wall.
If it fails, the fix belongs with the Wildcraft one, not here.

**Templates with more than one placeholder.**
`Match.variant` takes a list, and `Expand` fills every named placeholder from the candidate's variants.
A `planks-{stain}-{wood}` template then matches `woodstain:stainedplanks-*-*-ud`, consumes and drops that block, and names the same base and overlay Wood Stain's own block does; a finish's `Texture` already takes a composite, as `brick` does.
It reuses the plank finish's `Elements`, `FloorElements` and `Styles`, so stained boards come as weatherboard, vertical and horizontal.
With Wood Stain absent the template matches nothing and expands to nothing.

**Attribute stacks, only if asked.**
Dyed Wood needs three more things: candidates taken from a block's creative inventory stacks, `MatchConsumes` reading the held stack's attributes, and `Drops` that write them back.
That is a second matching rule through the build flow for a mod with 394 downloads, so it waits for the player to say Dyed Wood is the one.

## Alternatives considered
- **Explicit entries per stain and wood.** Wood Stain alone is 11 stains by every wood, written out by hand and wrong the day either mod adds one.
- **One tint rule of Siding's own for every stain mod.** Wood Stain blends an overlay and Dyed Wood ships a drawn texture per pair, so a tint of Siding's would match neither mod's planks standing next to the wall.
- **A patch shipped by the other mod.** `FinishFamilies` is open to other mods' patches already, but a two-variant block cannot be expressed in it today, so the template work comes first either way.
- **Stained framings.** The frame is mostly covered once a wall is finished; left until someone asks.

## Consequences & open questions
- Ask the player which mod they run.
- A stained plank block costs more to make than two plank items; one block per face, as other block finishes take.
- Whether a finish's `Texture` carries `blendedOverlays` and their blend mode through to the atlas as a block's textures do; `brick` only proves plain `overlays`.
- Whether Wood Stain's `ud` block is what a player holds after placing and breaking one.
- A two-placeholder template multiplies entries: one finish per stain and wood with Wood Stain loaded, on top of the roughly 500 expanded today (decision 0030's count).
