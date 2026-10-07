# 0060 — Tent walls

- Status: Accepted
- Created: 2026-10-04
- Reflects: split from `primitive-framing` on 2026-10-04; mod page comment https://mods.vintagestory.at/vssiding#cmt-242905 and the reply at #cmt-243431; branch `feat/tent-walls`; `config/materials.json`, `SidingWallBlock.LayerMaterial`, `patches/bone-frame-behavior.json`, `TentScenarios`, `SidingWallBlockLayerMaterialTests`; vanilla 1.22 `bone.json`, `hide.json`, `hide-species.json`, `cloth.json` and the decompiled `Block`, `CollectibleObject` and `BlockMaterialUtil`; decisions 0001, 0010, 0043, 0058; unit and end-to-end tests pass; not yet played

## Summary
A player asked for "bone frames/walls and cloth/hide/pelt walls" as "a 'neolithic tent' option".
This adds a bone framing and pelt and cloth infills, and lets a framing name its block material so a bone frame does not burn.
Decision 0058 supplies the build signal a stone-age player needs to raise any frame.

## Context
The mod page reply promised these as part of primitive walls.
They sat inside `primitive-framing` until that proposal was split: the stick frame and the stone signal are what unblock the stone age, and these are materials on top.

The proposal expected to draw three textures, since it took vanilla to draw bones, pelts and cloth only as items.
Vanilla ships tiling block textures for all three: `block/creature/bone`, `block/cloth/pelt` and `block/cloth/linen/{color}` for each of the thirteen cloth colours, each 32x32 and fully opaque.
The mod ships no texture of its own.

## Design
**A bone framing entry.**
A `bone` entry in `Framings`, consuming and dropping 2 `game:bone`, the count planks take (sticks take 4).
Bones come from kills, a few per animal, so they are scarcer than sticks.
The texture is `game:block/creature/bone`.
`patches/bone-frame-behavior.json` puts `PlaceWallFrame` on `game:bone`, appended after vanilla's `GroundStorable` and `GroundStoredProcessable`.
Bones match no infill, so the stick and wattle clash decision 0058 handles does not arise.
Bone joists for a thin floor come with it and need no floor code, as stick joists did.

**Framings name their block material.**
`SidingWallBlock.LayerMaterial` returned the caller's fallback, the block's own `Wood`, for a bare frame and for a deck.
It now reads `Framings`: a bare frame looks up its framing key and a deck its own.
An entry with no `BlockMaterial` still falls back, so planks and sticks are unchanged.
`bone` names `BlockMaterial: "Other"`, and decision 0043 burns only a `Wood` top layer, so a bone frame and a bone deck do not burn.
`TryBurnLayer`, on the wall and on the floor, handed a bare frame back to vanilla to delete, which was right while every bare frame was `Wood`.
It now keeps a bare frame that is not `Wood`.
Vanilla rechecks a fire's fuel once a second and runs its burn timer every 25 ms, so a burnout can land on a bone frame whose pelt has just gone.

**A pelt infill, one per hide size.**
A `pelt-{size}` template in `InfillFamilies`, matching `game:hide-pelt-*` on its `size` variant.
It expands to four infills (small, medium, large, huge) that share one display name, "Pelt Infill", and one texture, `game:block/cloth/pelt`.
Each consumes and drops one pelt of its own size.

**A cloth infill, one per colour.**
A `cloth-{color}` template in `InfillFamilies`, matching `game:cloth-*` on `color`.
It expands to thirteen infills, each consuming and dropping one cloth, drawing `game:block/cloth/linen/{color}`, with one lang key each.

**Hide and cloth burn.**
Pelt and cloth name `BlockMaterial: "Wood"`, as `straw` does, so they burn (decision 0043) and take the plank layer sounds.
They seal a room like any infill and are not cooling infills, since decision 0003's rule reads Stone, Ore, Soil and Ceramic.

**End-to-end scenarios.**
`TentScenarios` raises a bone frame, layers a small pelt and sets a fire; the fire burns the pelt off and the same wall block stands with only its bone framing.
It then reads the bare frame's combustible properties as null, which fails if the shipped `bone` entry loses its `BlockMaterial`, and runs a second burnout against the bare frame, which must stand.
A second scenario runs that burnout against bare bone joists, and a third layers blue cloth.
These are the only checks that the item codes are real and that the bone patch applies.

**The `Other` consumer audit.**
`GetBlockMaterial` now answers `Other` for a bare bone frame, a value no wall reported before.
Nothing in the four decompiled 1.22 assemblies (`VintagestoryAPI`, `VintagestoryLib`, `VSSurvivalMod`, `VSEssentials`) names `EnumBlockMaterial.Other`, so it only lands in the "none of the above" branch of a check.
Per consumer:
- Vanilla's retention checks (`Block`, doors, trapdoors, coverable) return -1 for Ore, Stone, Soil and Ceramic and 1 otherwise, so `Other` answers as `Wood` did.
- No vanilla tool lists `Other` in its mining speeds, so no tool breaks a bare bone frame faster than a bare hand does, where an axe speeds a plank frame.
- `BlockMaterialUtil` sets `Other`'s blast resistance to 1, 1 and 12 across its three blast types, against `Wood`'s 2, 2 and 24, so a bare bone frame is half as blast resistant as a plank one.
- Decor placement rejects only Snow and Ice hosts, the art pigment's material list does not include `Other` so pigment does not paint a bare bone frame, and `EntityAgent` checks only Snow.
- The block's `BlockMaterial` field is untouched and stays `Wood`, so readers of the field (tesselators, the sound engine) are unaffected.
- `Other` has no `LayerSounds` or `LayerResistance` entry in `wall.json`, so a bare bone frame falls back to the plank sounds and the base resistance.

This table is 1.22's and is to be redone on a game update.

**The roof is Roofing's.**
A tent is walls and a roof, and the Roofing mod already ships a cloth roof in leather and linen (its 1.3.0 changelog), taking hide and leather since 1.4.1 (https://mods.vintagestory.at/show/mod/30143).
Siding stays walls; a stick-framed thin floor with a pelt infill is as near as it gets.

**Changed from the proposal.**
- Textures: the proposal called three drawn textures most of the session.
Vanilla ships all of them, so the question of tinting one cloth texture per colour does not arise.
- Pelts: the proposal had one `pelt` entry consuming `game:hide-pelt-*`.
A wall stores only its material key, so one entry has one `Drops`: a huge pelt would go on and whichever pelt the entry named would come off.
The wildcard also matches the bear head, body and complete pelts of `hide-species-headed.json`, which are trophies.
The size family drops the pelt that went on.
Fox and raccoon pelts (`hide-species.json`) and bear pelts have no `size` variant, so `MaterialFamilies.Expand` skips them and they match no infill.
This reverses the proposal, which wanted fox and raccoon pelts to match.
- Bone frame cost, left open in the proposal, is 2.

## Alternatives considered
- **Ship these with the stick frame.** Three layers and a burn rule to test, for a frame that works without them.
- **Tag pelt and cloth `Cloth` and let 0043 burn `Cloth` too.** The truer tag, but a second burning material in `ResolveLayerCombustible` and its own layer sounds, where straw's `Wood` tag already covers it.
- **Pelt and cloth as finishes over wattle.** A tent wall is the hide and nothing else; a finish would need an infill behind it before it could go on.
- **Draw the textures.** Not needed, vanilla ships them.
- **One wildcard `pelt` entry dropping a fixed pelt.** Lossy, and it takes trophies.
- **Explicit entries for fox and raccoon pelts so each drops itself.** Three more entries and a texture per species; left until asked.
- **A `LayerSounds` entry for `Other`.** The plank fallback is acceptable for bone.

## Consequences & open questions
- Not yet played.
`dotnet test` and the headless server never exercise the texture atlas, so how the three textures look on a wall is unchecked: a bone frame is posts two texels wide in a pale texture and may not read as bone.
A shaped bone frame belongs with `rough-pole-frames`.
- Fox, raccoon and bear pelts are not accepted.
- Cloth needs a loom, so it is not stone age; its recipe gates it, as every late layer's does.
- A bare bone frame is slower to break with an axe than a plank one and weaker to blasts, from the audit above.
- Raw hide and leather are left out: raw hide is a step on the way to a pelt, and leather needs a barrel, which needs planks.
