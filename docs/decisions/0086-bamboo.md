# 0086 — Bamboo

- Status: Accepted
- Created: 2026-11-07
- Reflects: issue #136 and mod page comment https://mods.vintagestory.at/vssiding#cmt-251694; branch `136-bamboo`; `config/materials.json` (`bamboo`, `bamboowoven`), `patches/bamboo-frame-behavior.json`, `SidingWallBlock.FramesAsFilled`/`MatchFraming`/`CanTakeDeck`/`TryAddDeck`, `lang/en.json`; `VSSiding.E2E.Tests/BambooScenarios.cs`, `SidingWallBlockRetentionTests`; vanilla 1.22 `bamboostakes.json`, `stackedbamboo.json`, `recipes/grid/stackedbamboo.json` and the textures `block/wood/planks/bamboo-side`, `block/plant/bamboo/bamboowoven`, `stem-brown`, `stem-green`; decisions 0043, 0058, 0060, 0061, 0074, 0082, 0084; unit and end-to-end tests pass

## Summary
A player asked for bamboo, "to hold a house above a swamp".
This adds a bamboo framing and a woven bamboo infill, both from vanilla's bamboo stakes.
Bamboo posts stacked in a corner, with a floor laid onto the top one, are the stilts, and a fix lets a post take a deck from stakes or sticks.

## Context
Vanilla 1.22 ships everything needed: `game:bamboostakes`, an item with a `behaviors` array as sticks have, and the textures.
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
- **Treating a post as filled inside `MatchFraming`.** `MatchFraming` is static and takes the filled flag from its callers, so the post check went into the three callers that compute it.
- **MightyBamboo's green stakes and stacked blocks.** Out of scope; a family could pick them up when the mod is present.

## Consequences & open questions
- A bamboo frame and woven bamboo infill burn, and woven bamboo does not cool a cellar.
- A stick post takes a deck and a floor where it did not before.
- Not yet played.
