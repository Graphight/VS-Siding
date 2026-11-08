# 0088 — Bamboo

- Status: Accepted
- Created: 2026-11-07
- Reflects: issue #136 and mod page comment https://mods.vintagestory.at/vssiding#cmt-251694; branch `136-bamboo`, PR #152 and its playtest; `config/materials.json` (`bamboo`, `bamboopoles`), `patches/bamboo-frame-behavior.json`, `SidingWallBlock.FramesAsFilled`/`MatchFraming`/`CanTakeDeck`/`TryAddDeck`, `SidingWallEntity.InfillElements`/`SelectiveElements`/`DeckElements`, `SidingFloorEntity.SelectiveElements`, `WallShapeGen.Stems`/`StemBands`/`DiagonalStems`/`OnFloor` and the shapes it writes (`wall`, `cornerout`, `diagonal`, `floor`), `lang/en.json`; `VSSiding.E2E.Tests/BambooScenarios.cs`, `SidingWallBlockRetentionTests`, `WallShapeGenTests`, `FinishElementGroupsTests`, `SidingWallEntityTests`, `SidingFloorTests`, `SidingWallTexSourceTests`; vanilla 1.22 `bamboostakes.json`, `stackedbamboo.json`, `recipes/grid/stackedbamboo.json` and the textures `block/wood/planks/bamboo-side`, `block/plant/bamboo/bamboowoven`, `stem-brown`, `stem-green`; decisions 0021, 0043, 0058, 0060, 0061, 0066, 0074, 0082, 0083, 0084; unit and end-to-end tests pass; played on 2026-11-07 and 2026-11-08

## Summary
A player asked for bamboo, "to hold a house above a swamp".
This adds a bamboo framing and a bamboo pole infill, both from vanilla's bamboo stakes.
The pole infill is drawn as poles standing side by side, through an `Elements` key an infill entry can now carry, as a framing's does.
Bamboo posts stacked in a corner, with a floor laid onto the top one, are the stilts, and a fix lets a post take a deck from stakes or sticks.

## Context
Vanilla 1.22 ships everything needed: `game:bamboostakes`, an item with a `behaviors` array as sticks have, and the textures.
Bamboo slots in as sticks (0058) and bone (0060) did: material entries, lang keys and a `PlaceWallFrame` patch.
Posts (0082) and a floor that decks a wall (0084) take any framing, so stilts needed no block of their own.
Every infill drew the same `infill`, `infill-top` and `infill-bottom` groups, a flat slab, and no entry could pick its own shape; decision 0061 had solved the same problem for framings.

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

**A bamboo pole infill.**
A `bamboopoles` entry in `Infills`, "Bamboo Pole Infill", textured `bamboo-side` as the frame is, consuming and dropping 2 stakes.
It names `BlockMaterial: "Wood"`, so it burns and is not a cooling wall.
Seven poles two voxels wide stand touching across a cell's 14-voxel infill span, each with a proud ridge down each face so the seams read, and two thin proud nodes at heights staggered pole to pole (`StemNodes`).
All of it stays within the frame's thickness, x 1 to 3 on a wall, so any finish still covers it, which `EveryStemsBoxStaysInsideTheFramesThickness` checks.
The nodes sit a quarter voxel off the grid and the ridges off the planes of the rough frames' stubs and lashings, so no face of the poles shares a plane with a frame or finish it is drawn with (`NoFaceOfTheStemsSharesAPlaneWithItsFrameOrAFinishAndOverlaps`).

**An infill names its own groups.**
An infill entry may carry `Elements`, as a framing does (0061), and `SidingWallEntity.InfillElements` reads it, `infill` when absent.
`SelectiveElements` draws that prefix, with its `-top` and `-bottom` join pieces, in place of the plain fill, and the floor's `SelectiveElements` and `DeckElements` pass it through.
Glazed panes and the `infill-front` fills of a bare front facing another front (0083) are untouched, so a pole wall's facing front stays the flat infill texture.
`EveryInfillElementGroupExistsAndIsLeftOutOfTheDefaultShape` checks every real infill's prefix is in the wall, `cornerout`, diagonal and floor shapes and in no layout's default `selectiveElements`.

**Why `stems`.**
A name under `infill` would be caught by `IsInfillElement`, which sends a glazed cell's infill groups to the transparent pass.
`poles` names the rough framings' groups, which `DeckGroups` clips by a rule of their own, and `front`, `back` and `glazing` carry the rim rules.

**The stems groups.**
`WallShapeGen.Stems` cuts the poles to a span and a height band, and `StemBands` lays them at the infill's three heights.
A `cornerout` clips each leg to its own span, the second leg swapped onto x.
A diagonal repeats the poles cell by cell along the run (`DiagonalStems`), as `DiagonalFinish` lays a finish, rather than stretching each piece across it as `DiagonalRun` does a slab, so the poles keep their spacing and nodes through the run's ends, with `-left` and `-right` copies (0066).
A floor lays them flat (`OnFloor`), east-west across the north-south joists; its join pieces at z 0 to 1 and 15 to 16 are half poles, so two joined floors make one pole over their seam.
A deck's and a ledge's copies come from `DeckGroups`, which needed no change.

**Porous with no gaps.**
The poles touch, but the entry is still `Porous` like wattle (0074), so a bare pole wall passes light until a face is finished.
Light is read from the entry's flag, not the shape.

**The stake clash.**
Stakes are a framing and an infill at once, as sticks are framing and wattle.
`MatchFraming` already handles it: a plain stake click on a bare frame fills it with bamboo poles, and a sneak-click reaches `PlaceWallFrame`, which stacks or extends.
A stake deck then needs a filled wall, as a stick one does.

**Stilts.**
A column of bamboo posts placed in Post mode, each click on the post below, and a bamboo floor run laid onto the top post's cell, which decks it (0084).
`BambooScenarios` builds three posts and the floor and checks the top post's deck is `bamboo`.

**A post frames as filled.**
That scenario found that a floor run never decked a bamboo post.
A post takes no infill, so `MatchFraming` read stakes on it as the pole infill for the bare post and matched no framing, and `CanTakeDeck` refused.
`SidingWallBlock.FramesAsFilled` counts a `cornerin` as filled, and `CanTakeDeck`, `TryAddDeck` and the Floor-mode guard in `OnBlockInteractStart` read it in place of the infill check.
Sticks had the same gap since decision 0082, so a stick post now takes a stick deck and lays a floor too.
The Floor-mode guard keeps stakes in step with planks there: with Floor picked and the deck lit, stakes on a post's side lay a floor in the next cell rather than deck the post, and `BambooScenarios` checks that too.

## Alternatives considered
- **A woven bamboo infill.** Vanilla's `bamboowoven` texture on the plain slab, the issue's "maybe also", taken first because the texture was ready; after playing #152 the author chose real poles. Two infills cannot share stakes as `Consumes`, so the woven one went entirely.
- **A stacked bamboo finish.** A flat slab of `bamboo-side` over a face; it would hide the poles, and a framed pole wall is complete without it.
- **Treating a post as filled inside `MatchFraming`.** `MatchFraming` is static and takes the filled flag from its callers, so the post check went into the three callers that compute it.
- **MightyBamboo's green stakes and stacked blocks.** Out of scope; a family could pick them up when the mod is present.

## Consequences & open questions
- A bamboo frame and pole infill burn, and the pole infill does not cool a cellar.
- A stick post takes a deck and a floor where it did not before.
- A pole wall cell hands the tesselator 264 quads against a wattle wall's 30, a `cornerout` 454 and a floor 306 (`EachBuiltCellHandsTheTesselatorThisManyQuads`).
- Every deck and ledge copy carries the poles too, so the five generated shape files grew from 9.1 MB to 11.3 MB; each is parsed once and kept (`ParsedShape`).
- Any later infill can name its own groups with `Elements`; the groups have to be generated into every shape, which the infill test enforces.
- The frame, decks, corners, posts, stilts and the pole infill were played in #152 and worked as designed.
