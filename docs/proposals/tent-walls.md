# Tent walls

- Status: Draft
- Created: 2026-10-04
- Reflects: split from `primitive-framing` on 2026-10-04; mod page comment https://mods.vintagestory.at/vssiding#cmt-242905 and the reply at #cmt-243431; `config/materials.json`; `SidingWallBlock.LayerMaterial`/`ResolveLayerCombustible`; vanilla `bone.json`, `hide.json`, `hide-species.json`, `cloth.json`; the Roofing mod page's changelog; decisions 0001, 0010, 0043; not yet played

## Summary
A player asked for "bone frames/walls and cloth/hide/pelt walls" as "a 'neolithic tent' option".
The proposal adds a bone framing and pelt and cloth infills, and lets a framing name its block material so a bone frame does not burn.
It follows decision 0058, which supplies the build signal a stone-age player needs to raise any frame.

## Context
The mod page reply promised these as part of primitive walls.
They sat inside `primitive-framing` until that proposal was split: the stick frame and the stone signal are what unblock the stone age, and these are materials on top.

Each one needs a texture the game does not ship.
Vanilla draws bones, pelts and cloth only as items, so there is no tiling block texture to borrow, where every other layer so far has borrowed one.
That art is most of this session.

## Design
**A bone framing entry.**
A `bone` entry in `Framings`, consuming and dropping `game:bone`, with `PlaceWallFrame` patched onto bones as it is onto planks.
Bones match no infill, so the stick and wattle clash decision 0058 handles does not arise.

**Framings name their block material.**
`LayerMaterial` looks a layer's `BlockMaterial` up in `Infills` and `Finishes` only, so a bare frame answers the block's own `Wood`: it sounds like planks and catches fire (decision 0043).
That is right for planks and sticks and wrong for bone, so `LayerMaterial` reads `Framings` too, and `bone` names `BlockMaterial: "Other"`, which never burns.

**Pelt and cloth infills.**
A `pelt` entry in `Infills`, shown as "Pelt", consuming `game:hide-pelt-*`, which matches the plain hides and the fox and raccoon ones alike.
A `cloth-{color}` template in `InfillFamilies`, matching `game:cloth-*` on `color`.
Each takes one item, of any hide size, so a small pelt walls a cell as well as a huge one; sizing the cost by hide is a second matching rule for a looks difference.
Raw hide and leather are left out: raw hide is a step on the way to a pelt, and leather needs a barrel, which needs planks.
Cloth needs a loom, so it is not stone age, but it is a tent material the requester named, and its recipe gates it as every late layer is gated.

**Hide and cloth burn.**
Decision 0043 lets only a `Wood` top layer catch, and `straw` already names `BlockMaterial: "Wood"` to burn.
`pelt` and `cloth-{color}` do the same, so a tent wall burns down to its frame like a straw one, and takes the plank sounds straw does.

**The roof is Roofing's.**
A tent is walls and a roof, and the Roofing mod already ships a cloth roof in leather and linen (its 1.3.0 changelog), taking hide and leather since 1.4.1 (https://mods.vintagestory.at/show/mod/30143).
Siding stays walls; a stick-framed thin floor with a pelt infill is as near as it gets.

## Alternatives considered
- **Ship these with the stick frame.** Three textures to draw and a burn rule to test, for a frame that works without them.
- **Tag pelt and cloth `Cloth` and let 0043 burn `Cloth` too.** The truer tag, but a second burning material in `ResolveLayerCombustible` and its own layer sounds, where straw's `Wood` tag already covers it.
- **Pelt and cloth as finishes over wattle.** A tent wall is the hide and nothing else; a finish would need an infill behind it before it could go on.

## Consequences & open questions
- Whether a pelt or cloth wall seals a room like any infill, or reads as a tent and leaves retention to the roof; the proposal assumes it seals.
- Frame cost in bones, against planks' 2.
- Three textures to draw: a bone lattice, a pelt and a weave that takes cloth's colours.
- Whether one cloth texture tinted per colour is enough, or each colour needs its own.
