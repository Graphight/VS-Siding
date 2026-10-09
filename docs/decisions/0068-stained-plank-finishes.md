# 0068 — Stained plank finishes

- Status: Accepted
- Created: 2026-10-08
- Reflects: mod page comment https://mods.vintagestory.at/vssiding#cmt-244452; branch `feat/stained-plank-finishes`; Dyed Wood 2.1.0's shipped assets and its shipped source (the zip carries `src/`); Wood Stain 1.3.2's and Vanilla Varnished Planks 1.0.6's shipped assets only; the game 1.22.7 decompile for `BlockDropItemStack.Resolve`, `CompositeTexture.Bake`, `TextureAtlasManager.GetOrInsertTexture` and `TreeAttribute.IsSubSetOf`; `MaterialFamilies.Expand`, `SidingWallTexSource.ResolveTexture`/`AtlasPosition`/`AnyVaries`, `SidingWallBlock.MatchConsumes`, `config/materials.json`; decisions 0001, 0007, 0010; unit tests for each; none of the three mods has been played with Siding

## Summary
A player asked for "compat with the dyed wood mod", and three mods could be meant, each storing a coloured plank differently.
Vanilla Varnished Planks needs no change.
Wood Stain and Dyed Wood come as two finish templates, which needed three small additions: a family template filling several placeholders, a material entry that takes its texture from a block, and a `Consumes` rule that also matches a held stack's attributes.
The stained and dyed planks are finishes only; stained framings are not built.

## Context
A plank finish comes from the `planks-{wood}` template, which matches any `*:plank-*` item on its `wood` variant and names the textures `{domain}:block/wood/planks/{wood}*`.
`Expand` filled one placeholder per template, from one variant of one registered item or block.
`MatchConsumes` matched a held stack by its code alone.

- **Vanilla Varnished Planks** (`vanillavarnished`) ships `plank-{wood}` items under wood codes of its own (`varnished-oak`, `oxidized-maple`, `bleached-walnut`), each with a texture of the same name, so `planks-{wood}` already yields `planks-varnished-oak`.
- **Wood Stain** (`woodstain`) ships the block `stainedplanks-{stain}-{wood}-{orientation}`, with orientation `ns`, `we` or `ud`.
  It drops and lists `-ns`, so `-ns` is what a player holds.
  Every texture is the wood's plank texture with `blendedOverlays: [{ base: "woodstain:block/wood/stain/overlay-{stain}", blendMode: "Overlay" }]` and explicit `alternates`.
  The aged woods (`aged`, `veryaged`, `agedebony`, `agedacacia`, `rottenebony`) and Wildcraft Trees woods keep their base textures at other paths, so no path template fits them all.
- **Dyed Wood** (`dyedwood`) ships one block, `dyedwood:planks`, whose stack carries `attributes: { types: { color, wood } }` (Attribute Rendering Library), drawn with `dyedwood:block/wood/planks/{color}{wood}1`.
  Its source also registers a hidden real block per pair, `dyedwood:chiselmaterial-{color}-{wood}`, with variants `color` and `wood`.
  That block is what lets `Expand` enumerate the 154 pairs.
- `BlockDropItemStack` has `Attributes`, and `Resolve` copies them onto the stack, so a `Drops` entry with `attributes` needs no code.
- `ITextureAtlasAPI.GetOrInsertTexture(CompositeTexture, ...)` bakes the texture and looks it up by baked name, which includes blended overlays and their blend mode.
- `TreeAttribute.IsSubSetOf(IWorldAccessor, IAttribute)` is what vanilla uses to match a wanted attribute tree against a stack's.

## Design
**`Match.variant` takes a string or a list of strings.**
In `MaterialFamilies.Expand`, a candidate must carry every named variant, and each `{name}` is replaced in the key and in every string of the entry; `{domain}` works as before.
A `variant` that is neither a string nor a non-empty list of strings is skipped with the existing warning.
A template whose mod is absent matches nothing and expands to nothing.

**A material entry may carry `TextureBlock` instead of `Texture`.**
`SidingWallTexSource.ResolveTexture` takes a trailing block-texture lookup; for an entry with `TextureBlock` and no matching `StyleTextures` entry it returns what the lookup gives for that block code, or null with no lookup.
The production lookup is `capi.World.GetBlock(code)?.Textures.Values.FirstOrDefault()`, called from the `SidingWallTexSource` indexer and `SidingFloorEntity.TexSource`.
A Wood Stain block's texture already carries the overlay, its blend mode and the alternates, so Siding names the block and does not rebuild the composite.
`AtlasPosition` now picks the per-cell alternate from a texture's explicit `Alternates` as well as from a wildcard base's; a wildcard base is still baked first.
`AnyVaries` is true for an entry with `TextureBlock`, so the cell keeps its position hash.

**`Consumes.attributes` is matched against the held stack.**
`SidingWallBlock.MatchConsumes` takes the held stack's attributes as an optional third argument.
After the code matches, an entry whose `Consumes.attributes` exists is skipped unless the held attributes are non-null and the wanted tree is a subset of them (`IsSubSetOf`).
A malformed `attributes` value, one that is not a tree, skips the entry.
Only the three finish lookups pass the held stack's attributes: the two in `SidingWallBlock` and the one in `SidingFloorBlock`.
Framing and infill lookups keep matching by code alone.
An entry without `attributes` matches by code, as before.

**Two templates, two prefixes.**
- `stained-{stain}-{wood}` matches `woodstain:stainedplanks-*-*-ns` on `stain` and `wood`, takes `TextureBlock: woodstain:stainedplanks-{stain}-{wood}-ns`, consumes any orientation of that block, and drops the `-ns` block.
- `dyed-{color}-{wood}` matches `dyedwood:chiselmaterial-*-*` on `color` and `wood`, takes `Texture: dyedwood:block/wood/planks/{color}{wood}1`, consumes `dyedwood:planks` with `attributes: { types: { color, wood } }`, and drops `dyedwood:planks` with the same attributes.
- Both reuse the plank template's `Elements`, `FloorElements`, `Styles` and `BlockMaterial: "Wood"`, so the boards come as weatherboard, vertical and horizontal on a wall, floor and deck.
- The prefixes differ so that both mods loaded together do not collide on the key `red-oak`.
- Wood Stain takes `TextureBlock` and Dyed Wood a path because Dyed Wood's textures follow one path pattern and Wood Stain's do not.
- Neither entry has a `DisplayName`, so the tooltip shows the title-cased key: "Stained Red Oak" and "Dyed Red Oak".
- Each is one block per face, as other block finishes take.

**Vanilla Varnished Planks needs no change.**
The existing `planks-{wood}` template picks up its `plank-{wood}` items, as it does Wildcraft Trees' planks, and as plank items they also frame.

## Alternatives considered
- **Explicit entries per stain and wood.** Wood Stain alone is 11 stains by every wood, written out by hand and wrong the day either mod adds one.
- **One tint rule of Siding's own for every stain mod.** Wood Stain blends an overlay and Dyed Wood ships a drawn texture per pair, so a tint of Siding's would match neither mod's planks standing next to the wall.
- **A patch shipped by the other mod.** `FinishFamilies` is open to other mods' patches, but a two-variant block could not be expressed in it, so the template work came first either way.
- **A path template limited by regex to the 12 vanilla woods.** It needs no texture code, since the base and overlay could be named from paths, but it leaves Wood Stain's aged woods and the Wildcraft Trees woods unsupported.
- **Candidates taken from a block's creative inventory stacks.** It was the proposal's way to enumerate Dyed Wood's pairs, and it is not needed once `dyedwood:chiselmaterial-{color}-{wood}` was found.
- **Stained framings.** The frame is mostly covered once a wall is finished; left until someone asks.

## Consequences & open questions
**Not seen in play.**
The unit tests pin the expanded entries, the texture resolution and the attribute match; none of the three mods was loaded beside Siding.
- That the atlas returns a block's already-packed texture by its baked name, overlay and blend mode included.
- The shape of the `types` attribute on a held Dyed Wood stack, which `IsSubSetOf` must find the wanted tree inside.
- Whether a peeled stack merges with the stack it came from, for both mods.
- How each mod's plank looks on a wall and a floor beside the mod's own block.

**The payload grows.**
Every siding block carries its own copy of the expanded attributes.
Wood Stain adds 11 stains over each wood it registers, about 60 woods and so about 660 entries with Wildcraft Trees loaded; Dyed Wood adds 154 pairs.
This lands on top of the roughly 500 expanded entries counted in decision 0030.
Join time with every mod loaded is not measured.

**No Atlas scenario.**
The templates match nothing without the third-party mods, so a headless run of the built mod expands neither.

**Stained framings are not built.**
A stained plank is a finish only, apart from the varnished planks, which are plank items and also frame.
