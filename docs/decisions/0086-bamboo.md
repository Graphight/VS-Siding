# 0086 — Bamboo

- Status: Accepted
- Created: 2026-11-07
- Reflects: issue #136 and mod page comment https://mods.vintagestory.at/vssiding#cmt-251694; branch `136-bamboo`; `config/materials.json` (`bamboo`, `bamboowoven`, `stackedbamboo`), `patches/bamboo-frame-behavior.json`, `SidingWallBlock.FramesAsFilled`/`MatchFraming`/`CanTakeDeck`/`TryAddDeck`, `lang/en.json`; `VSSiding.E2E.Tests/BambooScenarios.cs`, `SidingWallBlockRetentionTests`, `SidingWallBlockDropsTests`, `FinishElementGroupsTests.EveryStyledFinishOffersItsWholePickerRow`; vanilla 1.22 `bamboostakes.json`, `stackedbamboo.json`, `recipes/grid/stackedbamboo.json` and the textures `block/wood/planks/bamboo-side`, `block/plant/bamboo/bamboowoven`, `stem-brown`, `stem-green`; decisions 0007, 0043, 0051, 0058, 0060, 0061, 0074, 0082, 0084; unit and end-to-end tests pass

## Summary
A player asked for bamboo, "to hold a house above a swamp".
This adds a bamboo framing and a woven bamboo infill, both from vanilla's bamboo stakes, and a stacked bamboo finish that lays its stems upright or lying.
Bamboo posts stacked in a corner, with a floor laid onto the top one, are the stilts, and a fix lets a post take a deck from stakes or sticks.

## Context
Vanilla 1.22 ships everything needed: `game:bamboostakes`, an item with a `behaviors` array as sticks have, `game:stackedbamboo-{ud,ns,we}`, crafted from four stakes with no tool, and the textures.
Bamboo slots in as sticks (0058) and bone (0060) did: material entries, lang keys and a `PlaceWallFrame` patch.
Posts (0082) and a floor that decks a wall (0084) take any framing, so stilts needed no block of their own.

## Design
**A bamboo framing.**
A `bamboo` entry in `Framings`, drawn with the `poles` groups (0061) and consuming and dropping 2 stakes, the count planks and bones take.
A post costs 1 (`PostQuarter`), and a frame raised with its deck 4.
`patches/bamboo-frame-behavior.json` puts `PlaceWallFrame` on `game:bamboostakes`, after vanilla's `GroundStorable`, as `stick-frame-behavior.json` does for sticks.
The entry names no `BlockMaterial`, so it falls back to `Wood` and burns (0043).

**The texture is `bamboo-side`.**
It is the side of vanilla's stacked bamboo block, the block the stakes craft into, with the stems upright.
The issue suggested the plant's `stem-brown` and `stem-green`, but both are sprite sheets for the plant's shape with empty regions (219 and 255 of 1024 pixels at alpha 0), so a pole face whose UVs landed on an empty region would draw a hole.
`MaterialTextureOpacityTests` would not catch it, since it flags only partial alpha; `bamboo-side` is opaque throughout.

**A woven bamboo infill.**
A `bamboowoven` entry in `Infills`, textured `block/plant/bamboo/bamboowoven`, consuming and dropping 2 stakes.
It is porous like wattle (0074), so it passes light until a face is finished, and it names `BlockMaterial: "Wood"`, so it burns and is not a cooling wall.

**The stake clash.**
Stakes are a framing and an infill at once, as sticks are framing and wattle.
`MatchFraming` already handles it: a plain stake click on a bare frame fills it with woven bamboo, and a sneak-click reaches `PlaceWallFrame`, which stacks or extends.
A stake deck then needs a filled wall, as a stick one does.
Stakes are not a finish, so a stake never matches all three layers.

**A stacked bamboo finish.**
A `stackedbamboo` entry in `Finishes`, consuming one stacked bamboo block of any orientation (`game:stackedbamboo-*`) and dropping `game:stackedbamboo-ud`, the block the recipe makes.
It takes the board row's styles, `boards` and `hboards`, with the plank entry's `Elements` and `FloorElements`, so floor and deck faces take both too (0051).
It has no weatherboard; its default is `boards`.

**Upright or lying through a turned texture.**
The `boards` groups turn their outward face a quarter (rotation 270), because oak's grain ships horizontal, and the `hboards` groups draw the texture as shipped.
`bamboo-side` ships upright, so drawn as shipped "Vertical boards" laid the stems flat and "Horizontal boards" stood them up.
The finish's texture is `{ base: bamboo-side, rotation: 90 }`: the atlas bakes the turn (`CompositeTexture.Bake` appends `@90` and `TextureAtlasManager.LoadCompositeBitmap` turns the pixels), so the stored texture lies and `boards` stands it back up.
Vanilla's own `stackedbamboo-ns` turns `bamboo-side` the same way on its east and west faces.
No PNG is copied, and no shape group or code is added.
The log row would not do: its groups never turn the texture, so both its styles would draw the stems one way.

**Stilts.**
A column of bamboo posts placed in Post mode, each click on the post below, and a bamboo floor run laid onto the top post's cell, which decks it (0084).
`BambooScenarios` builds three posts and the floor and checks the top post's deck is `bamboo`.

**A post frames as filled.**
That scenario found that a floor run never decked a bamboo post.
A post takes no infill, so `MatchFraming` read stakes on it as woven bamboo for the bare post and matched no framing, and `CanTakeDeck` refused.
`SidingWallBlock.FramesAsFilled` counts a `cornerin` as filled, and `CanTakeDeck`, `TryAddDeck` and the Floor-mode guard in `OnBlockInteractStart` read it in place of the infill check.
Sticks had the same gap since decision 0082, so a stick post now takes a stick deck and lays a floor too.
The Floor-mode guard keeps stakes in step with planks there: with Floor picked and the deck lit, stakes on a post's side lay a floor in the next cell rather than deck the post, and `BambooScenarios` checks that too.

## Alternatives considered
- **Stakes as the finish too.** A stake would then match the framing, the infill and a finish, and `MatchFraming` settles only two.
- **A copied PNG turned a quarter.** The atlas already bakes a rotation from the texture entry.
- **New upright and lying shape groups for bamboo.** The board row's two styles already differ by a quarter turn.
- **The log row's styles.** Its groups never turn the texture.
- **Treating a post as filled inside `MatchFraming`.** `MatchFraming` is static and takes the filled flag from its callers, so the post check went into the three callers that compute it.
- **MightyBamboo's green stakes and stacked blocks.** Out of scope; a family could pick them up when the mod is present.

## Consequences & open questions
- A bamboo frame and woven bamboo infill burn, and woven bamboo does not cool a cellar.
- Stacked bamboo comes back as its `ud` block whatever orientation went on.
- A stick post takes a deck and a floor where it did not before.
- Not yet played.
